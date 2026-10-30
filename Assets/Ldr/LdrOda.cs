using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Odanın bir kapısı: başta KAPALI görünür ama içinden geçilebilir; oyuncu yaklaşınca açılır ve açık kalır.
/// Oda kilitlenince (savaş) kapanır ve katı engel olur; kilit çözülünce, daha önce açılmışsa tekrar açılır.
/// </summary>
public class LdrKapi
{
    public int yon;                  // 0 kuzey, 1 doğu, 2 güney, 3 batı
    public Vector2 konum;            // dünya
    public SpriteRenderer[] cizimler;
    public BoxCollider2D kilit;
    public bool bossKapisi;
    public bool Acik { get; private set; }
    public bool Kilitli { get; private set; }
    Sprite acikSpr, kapaliSpr;
    Vector2 yari;                    // yaklaşma (tetik) kutusunun yarı boyutu
    bool onceAcildi;

    public LdrKapi(int yon, Vector2 konum, SpriteRenderer[] cizimler, BoxCollider2D kilit, Sprite acik, Sprite kapali, Vector2 yari)
    {
        this.yon = yon; this.konum = konum; this.cizimler = cizimler; this.kilit = kilit; acikSpr = acik; kapaliSpr = kapali; this.yari = yari;
        Ayarla(true);
    }

    void Goster(bool acik)
    {
        Acik = acik;
        if (cizimler != null)
            foreach (var sr in cizimler) if (sr != null) sr.sprite = acik ? acikSpr : kapaliSpr;
    }

    /// <summary>Oyuncu kapının yaklaşma kutusuna girdiyse açar.</summary>
    public void Yaklas(Vector2 p)
    {
        if (Kilitli || Acik) return;
        if (Mathf.Abs(p.x - konum.x) <= yari.x && Mathf.Abs(p.y - konum.y) <= yari.y)
        {
            onceAcildi = true;
            Goster(true);
            LdrSes.Cal("sfx_door", 0.6f, 1f);
        }
    }

    /// <summary>false: kilitle (kapanır, katı olur). true: kilidi çöz (daha önce açıldıysa açık, değilse kapalı görünür ama geçilebilir).</summary>
    public void Ayarla(bool acik)
    {
        if (!acik) { Kilitli = true; Goster(false); if (kilit != null) kilit.enabled = true; }
        else { Kilitli = false; if (kilit != null) kilit.enabled = false; Goster(onceAcildi); }
    }
}

public enum LdrOdaTuru { Baslangic, Savas, Sandik, Cikis, Koridor }

public enum LdrOdaBoyu { Kucuk, Orta, Buyuk }

/// <summary>
/// Odanın düşman dalgası: toplam kaç düşman çıkacak, aynı anda en çok kaç düşman olacak,
/// iki çıkış arası bekleme. Boss odasında boss listesi de burada.
/// </summary>
public class LdrDalga
{
    public int toplam, esZamanli, cikan;
    public float beklemeEn, beklemeEnCok, bekle = 0.6f;
    public bool bossOdasi;
    public readonly List<int> bossSirasi = new List<int>();
}

/// <summary>
/// Oda. Şablondan (LdrSablon) kurulur: zemin ve duvar çizimleri LdrResim'daki karolardan, konuma bağlı karışık seçimle
/// tek resme birleştirilir; katı hücreler birleşik çarpıştırıcı olur. Bağlanmayan kapı boşlukları duvarla doldurulur.
/// </summary>
public class LdrOda
{
    public Transform kok;              // zemin, duvar, çarpışma, kapılar (hep açık)
    public Transform env;              // eşyalar, meşaleler, sandık: yalnızca içinde bulunulan odada etkin
    public LdrYerOda yer;
    public LdrSablon sablon;
    public Vector2 merkez;
    public Rect sinir;                 // dünya, yerleştirilmiş sınırlar
    public Rect zemin;
    public Vector2 baslangic;
    public readonly List<LdrKapi> kapilar = new List<LdrKapi>();
    public readonly List<LdrDusman> dusmanlar = new List<LdrDusman>();
    public LdrOdaTuru tur;
    public Vector2Int hucre;
    public LdrOdaBoyu boy;
    public LdrDalga dalga;
    public readonly List<Vector2> doluNoktalar = new List<Vector2>();
    public bool temiz;
    public bool ziyaret;
    public float gorunur;              // 0 karanlık (hiç girilmemiş), 1 tam görünür

    byte[] h;                          // şablon boyutunda hücreler (kapı kapatma uygulanmış)
    int gen, yuk;
    Vector2Int kayma;
    readonly List<SpriteRenderer> cizimler = new List<SpriteRenderer>();

    public Vector2Int Kayma { get { return kayma; } }

    public static LdrOda Kur(Transform ebeveyn, LdrYerOda y)
    {
        var o = new LdrOda();
        o.yer = y;
        o.sablon = y.sablon;
        var s = y.sablon;
        o.gen = s.Gen; o.yuk = s.Yuk;
        o.kayma = y.alt - s.alt;
        o.h = (byte[])s.hucre.Clone();
        o.sinir = new Rect(y.alt.x, y.alt.y, o.gen, o.yuk);
        o.zemin = new Rect(y.alt.x + 1, y.alt.y + 1, o.gen - 2, o.yuk - 2);
        o.merkez = o.sinir.center;

        o.kok = new GameObject("Oda_" + s.ad).transform;
        o.kok.SetParent(ebeveyn, false);
        o.env = new GameObject("Esyalar").transform;
        o.env.SetParent(o.kok, false);

        // bağlanmayan kapıların boşluğu duvarla doldurulur
        foreach (var k in y.kapilar)
            if (!k.bagli) o.KapiyiKapat(k);

        o.IcBoslukDoldur();
        o.Ciz();
        o.Carpismalar();
        o.KapilariKur();
        o.baslangic = o.EnYakinZemin(o.merkez);
        return o;
    }

    // ---------------------------------------------------------------- hücre erişimi (şablon koordinatı)

    int Ind(int tx, int ty) { return (ty - sablon.alt.y) * gen + (tx - sablon.alt.x); }
    bool Icerde(int tx, int ty) { return tx >= sablon.alt.x && tx <= sablon.ust.x && ty >= sablon.alt.y && ty <= sablon.ust.y; }
    byte Al(int tx, int ty) { return Icerde(tx, ty) ? h[Ind(tx, ty)] : (byte)0; }
    void Koy(int tx, int ty, byte v) { if (Icerde(tx, ty)) h[Ind(tx, ty)] = v; }

    /// <summary>Şablonun iç kısmında kalmış, zemini/duvarı olmayan boş hücreler (siyah boşluk gibi görünüyordu): yanında yürünebilir zemin varsa zemin olur.</summary>
    void IcBoslukDoldur()
    {
        for (int tur = 0; tur < 3; tur++)
        {
            var ekle = new List<int>();
            for (int ty = sablon.alt.y + 1; ty < sablon.ust.y; ty++)
                for (int tx = sablon.alt.x + 1; tx < sablon.ust.x; tx++)
                {
                    if (!Icerde(tx, ty)) continue;
                    byte v = h[Ind(tx, ty)];
                    if ((v & 7) != 0) continue;
                    if (ZeminKomsu(tx - 1, ty) || ZeminKomsu(tx + 1, ty) || ZeminKomsu(tx, ty - 1) || ZeminKomsu(tx, ty + 1)) ekle.Add(Ind(tx, ty));
                }
            foreach (int i in ekle) h[i] |= 1;
        }
    }

    bool ZeminKomsu(int tx, int ty) { byte v = Al(tx, ty); return (v & 1) != 0 && (v & 4) == 0; }

    const byte DuvarHucresi = 1 | 4 | 8;      // zemin + katı + harita: duvar olarak çizilir

    void KapiyiKapat(LdrKapiVeri k)
    {
        for (int ty = k.kapatAlt.y; ty <= k.kapatUst.y; ty++)
            for (int tx = k.kapatAlt.x; tx <= k.kapatUst.x; tx++)
                Koy(tx, ty, DuvarHucresi);
    }

    public bool Kati(int tx, int ty) { return (Al(tx, ty) & 4) != 0; }

    bool DunyaHucre(Vector2 p, out int tx, out int ty)
    {
        tx = Mathf.FloorToInt(p.x) - kayma.x;
        ty = Mathf.FloorToInt(p.y) - kayma.y;
        return Icerde(tx, ty);
    }

    /// <summary>Dünya noktası yürünebilir zeminde mi (zemin var, katı değil)?</summary>
    public bool Yuruyebilir(Vector2 p)
    {
        int tx, ty;
        if (!DunyaHucre(p, out tx, out ty)) return false;
        byte v = h[Ind(tx, ty)];
        return (v & 1) != 0 && (v & 4) == 0;
    }

    /// <summary>Nokta duvar/boşluk içinde ya da pay (hücre) kadar yakınında mı?</summary>
    public bool Engelli(Vector2 p, float pay)
    {
        int tx, ty;
        if (!DunyaHucre(p, out tx, out ty)) return true;
        int r = Mathf.CeilToInt(pay);
        for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
            {
                byte v = Al(tx + x, ty + y);
                if ((v & 4) != 0 || (v & 1) == 0) return true;
            }
        return false;
    }

    public bool MinimapVar(Vector2 p)
    {
        int tx, ty;
        if (!DunyaHucre(p, out tx, out ty)) return false;
        return (h[Ind(tx, ty)] & 8) != 0;
    }

    public bool Icinde(Vector2 p) { return sinir.Contains(p); }

    /// <summary>İstenen noktaya en yakın yürünebilir hücrenin merkezi.</summary>
    public Vector2 EnYakinZemin(Vector2 hedef)
    {
        Vector2 en = hedef; float ed = float.MaxValue;
        for (int ty = sablon.alt.y; ty <= sablon.ust.y; ty++)
            for (int tx = sablon.alt.x; tx <= sablon.ust.x; tx++)
            {
                byte v = h[Ind(tx, ty)];
                if ((v & 1) == 0 || (v & 4) != 0) continue;
                var p = new Vector2(tx + kayma.x + 0.5f, ty + kayma.y + 0.5f);
                float d = (p - hedef).sqrMagnitude;
                if (d < ed && !Engelli(p, 1)) { ed = d; en = p; }
            }
        return en;
    }

    /// <summary>Duvarlardan uzak rastgele bir zemin noktası.</summary>
    public Vector2 RastgeleNokta(System.Random rng, float pay)
    {
        for (int deneme = 0; deneme < 80; deneme++)
        {
            var p = new Vector2(sinir.xMin + 1f + (float)rng.NextDouble() * (sinir.width - 2f), sinir.yMin + 1f + (float)rng.NextDouble() * (sinir.height - 2f));
            if (!Engelli(p, pay)) return p;
        }
        return baslangic;
    }

    // ---------------------------------------------------------------- çizim

    static int Hash(int x, int y)
    {
        unchecked { int v = x * 73856093 ^ y * 19349663; v ^= v >> 13; v *= 1274126177; v ^= v >> 16; return v & 0x7fffffff; }
    }

    static int ZeminSecim(int hs)
    {
        // karo karışımı: ilk iki desen ağırlıklı, süslü karolar seyrek (%50, %25, %10, %7, %5, %3)
        if (hs < 500) return 0; if (hs < 750) return 1; if (hs < 850) return 2; if (hs < 920) return 3; if (hs < 970) return 4; return 5;
    }

    static int DuvarSecim(int hs)
    {
        // duvar karışımı: ilk iki desen ağırlıklı, süslü karolar seyrek (%45, %30, %10, %7, %5, %3)
        if (hs < 450) return 0; if (hs < 750) return 1; if (hs < 850) return 2; if (hs < 920) return 3; if (hs < 970) return 4; return 5;
    }

    void Ciz()
    {
        const int k = 16;
        int W = gen * k, H = yuk * k;
        var zemTex = YeniTex(W, H);
        var duvTex = YeniTex(W, H);
        var zemPx = new Color32[W * H];
        var duvPx = new Color32[W * H];
        var zemKaro = KaroPikselleri("floor_", 6);
        var duvKaro = KaroPikselleri("wall_", 6);

        for (int ty = sablon.alt.y; ty <= sablon.ust.y; ty++)
            for (int tx = sablon.alt.x; tx <= sablon.ust.x; tx++)
            {
                byte v = h[Ind(tx, ty)];
                int cx = tx - sablon.alt.x, cy = ty - sablon.alt.y;
                bool zm = (v & 1) != 0, on = (v & 2) != 0, kati = (v & 4) != 0;
                int hs = Hash(tx, ty) % 1000;
                if (zm || on) Karola(zemPx, W, cx, cy, zemKaro, kati ? 0 : ZeminSecim(hs));
                // yalnızca "katı" bitli (zemin/ön katmanı olmayan) hücre yürünebilir zemine bakıyorsa siyah kalmasın, duvar çizilsin
                bool bosKati = kati && !zm && !on && (ZeminKomsu(tx - 1, ty) || ZeminKomsu(tx + 1, ty) || ZeminKomsu(tx, ty - 1) || ZeminKomsu(tx, ty + 1));
                if (kati && (zm || on || bosKati)) Karola(duvPx, W, cx, cy, duvKaro, DuvarSecim(hs));
            }
        zemTex.SetPixels32(zemPx); zemTex.Apply();
        duvTex.SetPixels32(duvPx); duvTex.Apply();

        var mat = LdrGorsel.Isikli();
        var zs = Sprite.Create(zemTex, new Rect(0, 0, W, H), Vector2.zero, k);
        var ds = Sprite.Create(duvTex, new Rect(0, 0, W, H), Vector2.zero, k);
        zs.hideFlags = ds.hideFlags = HideFlags.HideAndDontSave;
        var konum = new Vector3(yer.alt.x, yer.alt.y, 0f);
        cizimler.Add(LdrGorsel.Ciz(kok, "Zemin", zs, konum, -10, mat));
        cizimler.Add(LdrGorsel.Ciz(kok, "Duvarlar", ds, konum, -9, mat));
        GorunurAyarla(0f);
    }

    public void GorunurAyarla(float v)
    {
        gorunur = Mathf.Clamp01(v);
        var c = new Color(gorunur, gorunur, gorunur, 1f);
        for (int i = 0; i < cizimler.Count; i++) if (cizimler[i] != null) cizimler[i].color = c;
    }

    // ---------------------------------------------------------------- çarpışma

    void Carpismalar()
    {
        var go = new GameObject("Carpismalar");
        go.transform.SetParent(kok, false);
        int katman = LayerMask.NameToLayer("Duvar");
        if (katman >= 0) go.layer = katman;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        var cc = go.AddComponent<CompositeCollider2D>();
        cc.geometryType = CompositeCollider2D.GeometryType.Polygons;

        var kullanildi = new bool[gen * yuk];
        for (int cy = 0; cy < yuk; cy++)
            for (int cx = 0; cx < gen; cx++)
            {
                int i = cy * gen + cx;
                if (kullanildi[i] || (h[i] & 4) == 0) continue;
                int w = 1;
                while (cx + w < gen && !kullanildi[cy * gen + cx + w] && (h[cy * gen + cx + w] & 4) != 0) w++;
                int hh = 1;
                bool devam = true;
                while (devam && cy + hh < yuk)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int j = (cy + hh) * gen + cx + x;
                        if (kullanildi[j] || (h[j] & 4) == 0) { devam = false; break; }
                    }
                    if (devam) hh++;
                }
                for (int yy = 0; yy < hh; yy++) for (int xx = 0; xx < w; xx++) kullanildi[(cy + yy) * gen + cx + xx] = true;

                var kutu = new GameObject("K");
                kutu.transform.SetParent(go.transform, false);
                if (katman >= 0) kutu.layer = katman;
                kutu.transform.position = new Vector3(yer.alt.x + cx + w * 0.5f, yer.alt.y + cy + hh * 0.5f, 0f);
                var bc = kutu.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(w, hh);
                bc.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
    }

    // ---------------------------------------------------------------- kapılar

    void KapilariKur()
    {
        if (yer.dugumTuru == 'C') return;       // koridorlarda kapı yok
        foreach (var k in yer.kapilar)
        {
            if (!k.bagli) continue;
            bool dikey = k.yon == 1 || k.yon == 3;      // doğu-batı kapısı = dikey boşluk
            // boşluğun uzunluğu: kapı konumundan başlayarak katı olmayan hücreler
            int a = dikey ? k.konum.y : k.konum.x, b = a;
            while (a - 1 >= (dikey ? sablon.alt.y : sablon.alt.x) && b - a < 12 && !(dikey ? Kati(k.konum.x, a - 1) : Kati(a - 1, k.konum.y))) a--;
            while (b + 1 <= (dikey ? sablon.ust.y : sablon.ust.x) && b - a < 12 && !(dikey ? Kati(k.konum.x, b + 1) : Kati(b + 1, k.konum.y))) b++;
            float uzun = b - a + 1, orta = (a + b) * 0.5f + 0.5f;
            Vector2 merkezDunya;
            if (dikey) merkezDunya = new Vector2(k.konum.x + (k.yon == 1 ? 1f : 0f) + kayma.x, orta + kayma.y);
            else merkezDunya = new Vector2(orta + kayma.x, k.konum.y + (k.yon == 0 ? 1f : 0f) + kayma.y);

            string onEk = dikey ? "door_ew" : "door_ns";
            var acik = LdrResim.Get(onEk + "_open", 16f);
            var kapali = LdrResim.Get(onEk + "_closed", 16f);
            if (kapali == null) continue;
            const float birim = 2.75f;
            int adet = Mathf.Max(1, Mathf.RoundToInt(uzun / birim));
            float adim = uzun / adet;
            var srs = new SpriteRenderer[adet];
            for (int i = 0; i < adet; i++)
            {
                float off = -uzun * 0.5f + adim * (i + 0.5f);
                var p = dikey ? new Vector3(merkezDunya.x, merkezDunya.y + off, 0f) : new Vector3(merkezDunya.x + off, merkezDunya.y, 0f);
                srs[i] = LdrGorsel.Ciz(kok, "Kapi", kapali, p, -8, LdrGorsel.Isikli());
                float olcek = adim / birim;
                srs[i].transform.localScale = new Vector3(olcek, olcek, 1f);
                cizimler.Add(srs[i]);
            }

            var kgo = new GameObject("KapiKilit");
            kgo.transform.SetParent(kok, false);
            kgo.transform.position = merkezDunya;
            int katman = LayerMask.NameToLayer("Duvar");
            if (katman >= 0) kgo.layer = katman;
            var bc = kgo.AddComponent<BoxCollider2D>();
            bc.size = dikey ? new Vector2(1.5f, uzun) : new Vector2(uzun, 1.5f);
            var yari = dikey ? new Vector2(1.6f, uzun * 0.5f + 0.3f) : new Vector2(uzun * 0.5f + 0.3f, 1.6f);
            var kapi = new LdrKapi(k.yon, merkezDunya, srs, bc, acik, kapali, yari);
            kapilar.Add(kapi);
        }
        GorunurAyarla(gorunur);
    }

    public void KapilariAyarla(bool acik)
    {
        for (int i = 0; i < kapilar.Count; i++)
        {
            if (kapilar[i].bossKapisi && acik == true && !bossAcik) continue;
            kapilar[i].Ayarla(acik);
        }
    }

    public bool bossAcik = true;

    /// <summary>Oyuncu kapıya yaklaştıysa kapıyı açar.</summary>
    public void KapilariDene(Vector2 p)
    {
        for (int i = 0; i < kapilar.Count; i++) kapilar[i].Yaklas(p);
    }

    /// <summary>Boss odasının kapıları, diğer tüm odalar temizlenene kadar kilitlidir.</summary>
    public void BossKapilariniKilitle()
    {
        bossAcik = false;
        for (int i = 0; i < kapilar.Count; i++) { kapilar[i].bossKapisi = true; kapilar[i].Ayarla(false); }
    }

    public void BossKapilariniAc()
    {
        bossAcik = true;
        for (int i = 0; i < kapilar.Count; i++) kapilar[i].Ayarla(true);
    }

    /// <summary>Sütun gibi derinlik sıralı parçalar yok (duvarlar tek katman); çağıran kodlar için boş bırakıldı.</summary>
    public void Derinlik(float oyuncuAyakY) { }

    public bool DalgaBitti { get { return dalga == null || dalga.cikan >= dalga.toplam; } }

    public int CanliDusman()
    {
        int n = 0;
        for (int i = 0; i < dusmanlar.Count; i++)
            if (dusmanlar[i] != null && !dusmanlar[i].Can.Olu) n++;
        return n;
    }

    // ---------------------------------------------------------------- yardımcılar

    static Texture2D YeniTex(int w, int hh)
    {
        var t = new Texture2D(w, hh, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    static Color32[][] KaroPikselleri(string onEk, int adet)
    {
        var liste = new List<Color32[]>();
        for (int i = 0; i < adet; i++)
        {
            var t = LdrResim.Tex(onEk + i);
            if (t == null || t.width != 16 || t.height != 16) continue;
            var px = t.GetPixels32();
            for (int j = 0; j < px.Length; j++) px[j] = LdrTema.Boya(px[j]);     // bölüm rengine kaydır
            liste.Add(px);
        }
        if (liste.Count == 0) liste.Add(new Color32[16 * 16]);
        return liste.ToArray();
    }

    static void Karola(Color32[] hedef, int hedefGenislik, int kx, int ky, Color32[][] karolar, int secim)
    {
        var kaynak = karolar[Mathf.Min(secim, karolar.Length - 1)];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                hedef[(ky * 16 + y) * hedefGenislik + kx * 16 + x] = kaynak[y * 16 + x];
    }
}
