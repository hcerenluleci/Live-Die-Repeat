using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kat (bir derinlik). Zindan kurucusu (LdrZindan) harita grafiğindeki her oda için bir şablon seçer,
/// kapılara yaslanarak çakışmadan yerleştirir (geçitler dahil, kesintisiz bir dünya). Savaş odasına girince kapılar kilitlenir,
/// düşmanlar bitince açılır. Boss odasının kapıları, diğer tüm savaş odaları temizlenene kadar kilitlidir.
/// </summary>
public class LdrKat : MonoBehaviour
{
    public int derinlik;
    public readonly List<LdrOda> odalar = new List<LdrOda>();
    public LdrOda Baslangic { get; private set; }
    public LdrOda CikisOdasi { get; private set; }
    public LdrOda SandikOdasi { get; private set; }
    public LdrOda Gecerli { get; private set; }
    public bool Gecis { get; private set; }
    public Rect Sinir { get; private set; }

    LdrYasayan yasayan;
    LdrKamera kamera;
    System.Random rastgele;                                  // oyun sırasında düşman çıkışı için
    bool bossAcildi;
    public bool BossAcik { get { return bossAcildi; } }
    float kilitBekle;

    /// <summary>Katı kurar. Kat dünya merkezinde (0,0) durmalıdır.</summary>
    public static LdrKat Kur(Transform ebeveyn, int derinlik, int tohum, LdrYasayan y, LdrKamera k)
    {
        var go = new GameObject("Kat" + derinlik);
        go.transform.SetParent(ebeveyn, false);
        var kat = go.AddComponent<LdrKat>();
        kat.derinlik = derinlik;
        kat.yasayan = y;
        kat.kamera = k;
        kat.Uret(tohum);
        return kat;
    }

    void Uret(int tohum)
    {
        var rng = new System.Random(tohum);
        rastgele = new System.Random(tohum * 17 + 5);

        var yerler = LdrZindan.Uret(derinlik, rng);
        if (yerler == null)
        {
            // yedek: yalnızca giriş odası
            var s = LdrSablon.Bul("Giris_Avlu");
            var y = new LdrYerOda { dugum = 0, ebeveyn = -1, dugumTuru = 'E', sablon = s, alt = s.alt, ust = s.ust, konumlandi = true };
            y.kapilar = new LdrKapiVeri[s.kapilar.Length];
            for (int i = 0; i < y.kapilar.Length; i++) y.kapilar[i] = s.kapilar[i].Kopya();
            yerler = new List<LdrYerOda> { y };
        }

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var y in yerler)
        {
            var oda = LdrOda.Kur(transform, y);
            switch (y.dugumTuru)
            {
                case 'E': oda.tur = LdrOdaTuru.Baslangic; break;
                case 'B': oda.tur = LdrOdaTuru.Cikis; break;
                case 'H': oda.tur = LdrOdaTuru.Sandik; break;
                case 'C': oda.tur = LdrOdaTuru.Koridor; break;
                default: oda.tur = LdrOdaTuru.Savas; break;
            }
            odalar.Add(oda);
            minX = Mathf.Min(minX, oda.sinir.xMin); minY = Mathf.Min(minY, oda.sinir.yMin);
            maxX = Mathf.Max(maxX, oda.sinir.xMax); maxY = Mathf.Max(maxY, oda.sinir.yMax);
        }
        Sinir = new Rect(minX - 1f, minY - 1f, maxX - minX + 2f, maxY - minY + 2f);

        Baslangic = odalar[0];
        foreach (var o in odalar) { if (o.tur == LdrOdaTuru.Cikis) CikisOdasi = o; if (o.tur == LdrOdaTuru.Sandik && SandikOdasi == null) SandikOdasi = o; }
        if (CikisOdasi == null)
        {
            // grafiğinde boss olmayan haritalar: en son savaş odası çıkış odası olur
            for (int i = odalar.Count - 1; i >= 0; i--) if (odalar[i].tur == LdrOdaTuru.Savas) { CikisOdasi = odalar[i]; CikisOdasi.tur = LdrOdaTuru.Cikis; break; }
            if (CikisOdasi == null) CikisOdasi = Baslangic;
        }
        if (SandikOdasi == null) SandikOdasi = CikisOdasi;

        foreach (var oda in odalar)
        {
            DalgaKur(oda, rng);
            Icerik(oda, rng);
            oda.temiz = oda.dalga == null;
            oda.env.gameObject.SetActive(false);
        }
        CikisOdasi.BossKapilariniKilitle();

        Gecerli = Baslangic;
        Baslangic.ziyaret = true;
        Baslangic.GorunurAyarla(1f);
        Baslangic.env.gameObject.SetActive(true);
    }

    // ---------------------------------------------------------------- düşman dalgaları

    // Dalga kuralı (oda boyu ve derinlik d = 0..4):
    //   toplam düşman   = taban + d x artış (rastgele aralık)
    //   aynı anda en çok = oda boyuna göre 2-3 / 3-4 / 4-6, her iki bölümde +1
    //   iki çıkış arası  = oda büyüdükçe ve derinlik arttıkça kısalır
    // her satır: { en az toplam, en çok toplam, en az aynı anda, en çok aynı anda, en kısa bekleme, en uzun bekleme (sn) } (d = 0 için) ve artışlar
    static readonly float[] KucukTaban = { 3, 5, 2, 3, 3.5f, 5.5f }, KucukArtis = { 1f, 1f, 0.5f, 0.5f, -0.25f, -0.3f };
    static readonly float[] OrtaTaban = { 6, 8, 3, 4, 3f, 5f }, OrtaArtis = { 1f, 1.5f, 0.5f, 0.5f, -0.25f, -0.3f };
    static readonly float[] BuyukTaban = { 9, 12, 4, 6, 2.5f, 4.5f }, BuyukArtis = { 2f, 2f, 0.5f, 0.5f, -0.2f, -0.3f };
    // boss odası: toplam = aynı anda; { en kısa, en uzun bekleme }
    static readonly float[] BossBekleme = { 1.5f, 3f };

    static float[] DalgaSatiri(float[] taban, float[] artis, int d)
    {
        var p = new float[6];
        for (int i = 0; i < 6; i++) p[i] = taban[i] + artis[i] * d;
        p[4] = Mathf.Max(1.2f, p[4]); p[5] = Mathf.Max(p[4] + 0.8f, p[5]);
        return p;
    }

    // düşman türü ağırlıkları (derinlik başına): Sızıntı, Kızıl Sızıntı, Ağıtçı, Bakıcı, Sürünen
    // ilk bölüm öğretici: çoğunlukla Sızıntı; derinleştikçe hızlı ve desenli yaratıklar artar
    static readonly int[][] TurAgirlik =
    {
        new[] { 4, 0, 1, 2, 1 }, new[] { 4, 1, 2, 3, 2 }, new[] { 4, 2, 3, 4, 3 }, new[] { 3, 3, 4, 3, 4 }, new[] { 3, 4, 4, 4, 5 },
    };

    // boss çeşitleri: (çizim türü, silah anahtarı, ton). Boss 2 = Kızıl Sızıntı büyük hali, Boss 1 = Sürünen büyük hali
    struct BossCesit { public int tur; public string silah; public Color ton; }
    static readonly BossCesit[] Bosslar =
    {
        new BossCesit { tur = LdrDusman.KizilSizinti, silah = "tabancapatron", ton = new Color(1f, 0.82f, 0.82f) },     // 0 kırmızı tabanca
        new BossCesit { tur = LdrDusman.KizilSizinti, silah = "seripatron", ton = new Color(0.82f, 0.9f, 1f) },         // 1 mavi seri ateş
        new BossCesit { tur = LdrDusman.KizilSizinti, silah = "pompalipatron", ton = new Color(0.85f, 1f, 0.85f) },     // 2 yeşil pompalı
        new BossCesit { tur = LdrDusman.Surunen, silah = "tabancapatron", ton = new Color(0.85f, 0.92f, 1f) },          // 3 mavi tabanca
        new BossCesit { tur = LdrDusman.Surunen, silah = "pompalipatron", ton = new Color(0.85f, 1f, 0.85f) },          // 4 yeşil pompalı
    };

    void DalgaKur(LdrOda oda, System.Random rng)
    {
        int d = Mathf.Clamp(derinlik, 0, 4);
        switch (oda.tur)
        {
            case LdrOdaTuru.Savas:
            {
                char t = oda.sablon.tur;
                oda.boy = t == 'S' ? LdrOdaBoyu.Kucuk : t == 'M' ? LdrOdaBoyu.Orta : LdrOdaBoyu.Buyuk;
                var p = oda.boy == LdrOdaBoyu.Kucuk ? DalgaSatiri(KucukTaban, KucukArtis, d)
                      : oda.boy == LdrOdaBoyu.Orta ? DalgaSatiri(OrtaTaban, OrtaArtis, d)
                      : DalgaSatiri(BuyukTaban, BuyukArtis, d);
                var w = new LdrDalga();
                w.toplam = rng.Next(Mathf.RoundToInt(p[0]), Mathf.RoundToInt(p[1]) + 1);
                w.esZamanli = Mathf.Min(w.toplam, rng.Next(Mathf.FloorToInt(p[2]), Mathf.FloorToInt(p[3]) + 1));
                w.beklemeEn = p[4]; w.beklemeEnCok = p[5];
                oda.dalga = w;
                break;
            }
            case LdrOdaTuru.Cikis:
            {
                var w = new LdrDalga { bossOdasi = true };
                int[] havuz;
                int adet;
                switch (d)
                {
                    case 0: havuz = new[] { 0 }; adet = 1; break;
                    case 1: havuz = new[] { 3 }; adet = 1; break;
                    case 2: havuz = new[] { 1, 2 }; adet = 2; break;
                    case 3: havuz = new[] { 0, 3, 4 }; adet = 2; break;
                    default: havuz = new[] { 1, 2, 4 }; adet = 3; break;
                }
                for (int i = 0; i < adet; i++) w.bossSirasi.Add(havuz[rng.Next(havuz.Length)]);
                w.toplam = w.esZamanli = adet;
                w.beklemeEn = BossBekleme[0]; w.beklemeEnCok = BossBekleme[1];
                oda.dalga = w;
                break;
            }
            default: oda.dalga = null; break;     // başlangıç ve sandık odası: düşman yok
        }
    }

    int TurSec(System.Random rng)
    {
        var a = TurAgirlik[Mathf.Clamp(derinlik, 0, 4)];
        int toplam = 0;
        for (int i = 0; i < a.Length; i++) toplam += a[i];
        int z = rng.Next(toplam);
        for (int i = 0; i < a.Length; i++) { if (z < a[i]) return i; z -= a[i]; }
        return LdrDusman.Sizinti;
    }

    /// <summary>Oyun sırasında dalganın bir sonraki düşmanını odanın doğma noktalarından birinde, oyuncudan uzak olanında doğurur.</summary>
    void Dogur(LdrOda oda)
    {
        var w = oda.dalga;
        Vector2 oyuncu = yasayan.transform.position, nokta = oda.RastgeleNokta(rastgele, 2f);
        var dg = oda.sablon.doguma;
        if (dg != null && dg.Length > 0)
        {
            float en = -1f;
            for (int i = 0; i < 3; i++)
            {
                var p = dg[rastgele.Next(dg.Length)] + (Vector2)(Vector2Int)oda.Kayma;
                float d = Vector2.Distance(p, oyuncu);
                if (d > en) { en = d; nokta = p; }
            }
        }

        LdrDusman yeni;
        if (w.bossOdasi)
        {
            var b = Bosslar[w.bossSirasi[Mathf.Min(w.cikan, w.bossSirasi.Count - 1)]];
            yeni = LdrDusman.BossOlustur(nokta, b.tur, b.silah, b.ton);
        }
        else yeni = LdrDusman.Olustur(nokta, TurSec(rastgele));
        yeni.transform.SetParent(oda.kok, true);
        oda.dusmanlar.Add(yeni);
        w.cikan++;
    }

    void DalgaIsle(LdrOda oda)
    {
        var w = oda.dalga;
        if (w == null || oda.DalgaBitti) return;
        w.bekle -= Time.deltaTime;
        if (w.bekle > 0f || oda.CanliDusman() >= w.esZamanli) return;
        Dogur(oda);
        w.bekle = Aralik(rastgele, w.beklemeEn, w.beklemeEnCok);
    }

    static float Aralik(System.Random rng, float a, float b) { return (float)(a + rng.NextDouble() * (b - a)); }

    /// <summary>Şablondaki eşyalar (fıçı, masa, meşale, sandık) ve ruh heykeli.</summary>
    void Icerik(LdrOda oda, System.Random rng)
    {
        var kayma = (Vector2)(Vector2Int)oda.Kayma;
        bool sandik = false;
        var yerler = new List<Vector2>();        // yerleştirilen eşyalar (heykeli bunlardan uzak koymak için)
        var mesaleler = new List<Vector2>();     // kafatası meşaleleri: çok sık olmasın diye seyreltilir
        const float MesaleAralik = 8f;
        for (int gecis = 0; gecis < 2; gecis++)      // önce meşaleler, sonra diğerleri (fıçı meşalenin üstüne binmesin)
        foreach (var e in oda.sablon.esyalar)
        {
            bool mesaleMi = e.tur == 'a' || e.tur == 'b' || e.tur == 'c';
            if ((gecis == 0) != mesaleMi) continue;
            var w = e.konum + kayma;
            switch (e.tur)
            {
                case 'B': case 'G':
                    // şablondaki bazı fıçılar duvarın içine denk geliyor (haritanın dışında gibi, itemi alınamaz): bunlar konmaz
                    if (!oda.Yuruyebilir(w) || !oda.Yuruyebilir(w + new Vector2(0f, 0.6f))) break;
                    bool meseleyeYakin = false;
                    foreach (var q in mesaleler) if ((q - w).sqrMagnitude < 2.4f * 2.4f) { meseleyeYakin = true; break; }
                    if (meseleyeYakin) break;
                    LdrFici.Kur(oda.env, w, e.tur == 'G', e.olcek);
                    yerler.Add(w);
                    break;
                case 'H': case 'V':
                    LdrMasa.Kur(oda, w, e.tur == 'V');
                    yerler.Add(w);
                    break;
                case 'a': case 'b': case 'c':
                {
                    bool yakin = false;
                    foreach (var q in mesaleler) if ((q - w).sqrMagnitude < MesaleAralik * MesaleAralik) { yakin = true; break; }
                    if (yakin) break;
                    mesaleler.Add(w);
                    LdrMesale.Kur(oda.env, new Vector3(w.x, w.y, 0f), e.tur);
                    yerler.Add(w);
                    break;
                }
                case 'y':
                    if (oda.tur == LdrOdaTuru.Sandik && !sandik) { LdrSandik.Kur(oda.env, w, derinlik, MuhurluAta(), Random.Range(1, 4)); sandik = true; }
                    break;
            }
        }
        if (oda.tur == LdrOdaTuru.Sandik && !sandik) LdrSandik.Kur(oda.env, oda.EnYakinZemin(oda.merkez), derinlik, MuhurluAta(), Random.Range(1, 4));
        if (oda.tur == LdrOdaTuru.Savas && rng.Next(100) < 40)
        {
            // heykel: diğer eşyalardan ve kapılardan uzak, açık bir yere
            for (int i = 0; i < 60; i++)
            {
                var p = oda.RastgeleNokta(rng, 2f);
                bool uygun = true;
                foreach (var q in yerler) if ((q - p).sqrMagnitude < 3.4f * 3.4f) { uygun = false; break; }
                foreach (var k in oda.kapilar) if (Vector2.Distance(k.konum, p) < 5f) { uygun = false; break; }
                if (!uygun) continue;
                LdrHeykel.Kur(oda.env, p);
                break;
            }
        }
    }

    // ---------------------------------------------------------------- sandıklar ve kaçış parçası

    int sandikGorulen, temizOda;
    bool parcaAtandi;

    /// <summary>Mühürlü sandık: bu derinliğin parçası alınmadıysa, görülen 1., 2., 3. sandık %40 / %70 / %100 şansla mühürlü olur.</summary>
    bool MuhurluAta()
    {
        sandikGorulen++;
        if (derinlik >= LdrParca.Gerekli || LdrParca.Var(derinlik) || parcaAtandi) return false;
        float sans = sandikGorulen == 1 ? 0.4f : (sandikGorulen == 2 ? 0.7f : 1f);
        if (Random.value >= sans) return false;
        parcaAtandi = true;
        return true;
    }

    /// <summary>Ödül sandığı: temizlenen savaş odasında oda boyuna göre şansla sandık çıkar
    /// (küçük %25-45, orta %35-55, büyük %45-65, boss %100); eşya sayısı küçükte 1, ortada 1-2, büyükte 2-3, bossta 3.
    /// Parça henüz bulunmadıysa (sandık yoksa) temizlenen odalarda artan şansla ruh kristali belirir.</summary>
    void SavasSandigi(LdrOda oda)
    {
        if (oda.tur == LdrOdaTuru.Baslangic || oda.tur == LdrOdaTuru.Koridor || oda.tur == LdrOdaTuru.Sandik) return;
        bool boss = oda == CikisOdasi;
        int enAz, enCok, sansAz, sansCok;
        // sandık hiçbir zaman boş çıkmaz
        if (boss) { enAz = 3; enCok = 3; sansAz = sansCok = 100; }
        else if (oda.boy == LdrOdaBoyu.Kucuk) { enAz = 1; enCok = 1; sansAz = 25; sansCok = 45; }
        else if (oda.boy == LdrOdaBoyu.Orta) { enAz = 1; enCok = 2; sansAz = 35; sansCok = 55; }
        else { enAz = 2; enCok = 3; sansAz = 45; sansCok = 65; }

        if (!boss) temizOda++;
        bool sandikCikar = Random.Range(1, 101) <= Random.Range(sansAz, sansCok + 1);
        if (sandikCikar)
        {
            Vector2 yer;
            if (SandikYeri(oda, out yer))
            {
                bool muhurlu = MuhurluAta();
                var sd = LdrSandik.Kur(oda.env, yer, derinlik, muhurlu, Random.Range(enAz, enCok + 1), true);   // sandık belirme efektiyle ortaya çıkar
                LdrVfx.Burst(yer + new Vector2(0f, 1f), muhurlu ? new Color(0.85f, 0.55f, 1f) : new Color(1f, 0.8f, 0.4f), Color.white, 20, 3.5f, 0.14f, 0.7f, 0f, 0, 40);
                LdrHud.Duyur(muhurlu ? "MÜHÜRLÜ BİR SANDIK BELİRDİ!" : "BİR SANDIK BELİRDİ", muhurlu ? new Color(0.85f, 0.6f, 1f) : new Color(1f, 0.85f, 0.45f));
                return;
            }
        }
        // sandık çıkmadıysa: parça henüz atanmadıysa temizlenen odalarda artan şansla ruh kristali
        if (!boss && derinlik < LdrParca.Gerekli && !LdrParca.Var(derinlik) && !parcaAtandi && Random.value <= 0.3f + 0.2f * temizOda)
        {
            parcaAtandi = true;
            LdrParcaNesnesi.Olustur(BosNokta(oda), derinlik, oda.env);
            LdrHud.Duyur("BİR KAÇIŞ PARÇASI BELİRDİ!", new Color(0.85f, 0.6f, 1f));
        }
    }

    /// <summary>Sandığın (2 x 1,7) sığacağı, kapılardan ve oyuncudan uzak, boş bir yer.</summary>
    bool SandikYeri(LdrOda oda, out Vector2 yer)
    {
        var filtre = new ContactFilter2D { useTriggers = false };
        var sonuc = new Collider2D[8];
        for (int i = 0; i < 40; i++)
        {
            var p = oda.RastgeleNokta(rastgele, 2.6f);
            bool uygun = true;
            foreach (var k in oda.kapilar) if (Vector2.Distance(k.konum, p) < 4.5f) { uygun = false; break; }
            if (!uygun) continue;
            if (yasayan != null && Vector2.Distance(yasayan.transform.position, p) < 2.8f) continue;
            if (oda.tur == LdrOdaTuru.Cikis && Vector2.Distance(p, oda.EnYakinZemin(oda.merkez)) < 3f) continue;
            if (!oda.Yuruyebilir(p) || !oda.Yuruyebilir(p + new Vector2(0f, 1.8f)) || !oda.Yuruyebilir(p + new Vector2(-1.3f, 0f)) || !oda.Yuruyebilir(p + new Vector2(1.3f, 0f))) continue;
            int n = Physics2D.OverlapBox(p + new Vector2(0f, 0.8f), new Vector2(3.0f, 2.8f), 0f, filtre, sonuc);
            bool bos = true;
            for (int j = 0; j < n; j++) if (sonuc[j] != null && sonuc[j].GetComponentInParent<LdrYasayan>() == null) { bos = false; break; }
            if (!bos) continue;
            yer = p;
            return true;
        }
        yer = Vector2.zero;
        return false;
    }

    /// <summary>Temizlenen odada, boş bir noktaya eşya düşürmek için.</summary>
    Vector2 BosNokta(LdrOda oda)
    {
        for (int i = 0; i < 20; i++)
        {
            var p = oda.RastgeleNokta(rastgele, 1f);
            if (Vector2.Distance(p, oda.kapilar.Count > 0 ? oda.kapilar[0].konum : Vector2.zero) < 3f) continue;
            if (oda.tur == LdrOdaTuru.Cikis && Vector2.Distance(p, oda.EnYakinZemin(oda.merkez)) < 2.4f) continue;
            return p;
        }
        return oda.EnYakinZemin(oda.merkez);
    }

    // ---------------------------------------------------------------- oyun akışı

    LdrOda OdaBul(Vector2 p)
    {
        if (Gecerli != null && Gecerli.sinir.Contains(p)) return Gecerli;
        foreach (var o in odalar) if (o.sinir.Contains(p)) return o;
        return null;
    }

    void Update()
    {
        if (Gecerli == null || yasayan == null) return;

        // görünürlük: girilen oda yavaşça aydınlanır
        foreach (var o in odalar)
            if (o.ziyaret && o.gorunur < 1f) o.GorunurAyarla(o.gorunur + Time.unscaledDeltaTime * 2.5f);

        if (!Gecis && !yasayan.Can.Olu)
        {
            Vector2 ayak = yasayan.transform.position;
            Gecerli.KapilariDene(ayak);
            foreach (var ko in odalar) if (ko != Gecerli && ko.kapilar.Count > 0) ko.KapilariDene(ayak);
            var o = OdaBul((Vector2)yasayan.transform.position + new Vector2(0f, -0.3f));
            if (o != null && o != Gecerli) Giris(o);
        }

        if (Gecerli.dalga != null && !Gecerli.temiz)
        {
            // kapılar, oyuncu kapıdan yeterince içeri girince kilitlenir
            if (!kilitli)
            {
                bool uzak = true;
                foreach (var k in Gecerli.kapilar) if (Vector2.Distance(k.konum, yasayan.transform.position) < 2.6f) uzak = false;
                if (uzak) { kilitli = true; Gecerli.KapilariAyarla(false); }
            }
            else
            {
                if (!Gecis && !yasayan.Donuk && !yasayan.Can.Olu) DalgaIsle(Gecerli);
                if (Gecerli.DalgaBitti && Gecerli.CanliDusman() == 0) Temizlendi(Gecerli);
            }
        }
    }

    bool kilitli;

    void Giris(LdrOda oda)
    {
        if (Gecerli != null && Gecerli != oda) Gecerli.env.gameObject.SetActive(false);
        Gecerli = oda;
        oda.env.gameObject.SetActive(true);
        oda.ziyaret = true;
        kilitli = false;
        if (oda.dalga != null && !oda.temiz) oda.dalga.bekle = 0.6f;
        if (kamera != null) kamera.sinir = Sinir;

        // komşu odalar girilmeden de hafifçe seçilsin (kapıdan bakınca simsiyah boşluk gibi görünmesin)
        foreach (var o in odalar)
            if (!o.ziyaret && (o.yer.ebeveyn == oda.yer.dugum || o.yer.dugum == oda.yer.ebeveyn) && o.gorunur < 0.55f) o.GorunurAyarla(0.55f);
    }

    void Temizlendi(LdrOda oda)
    {
        oda.temiz = true;
        kilitli = false;
        oda.KapilariAyarla(true);
        yasayan.Can.Iyiles(1);
        LdrHud.Duyur("ODA TEMİZLENDİ", new Color(0.8f, 0.7f, 1f));
        if (oda == CikisOdasi) { oda.BossKapilariniAc(); LdrCikis.Kur(oda.kok, oda.EnYakinZemin(oda.merkez)); }
        SavasSandigi(oda);

        // diğer tüm savaş odaları temizlendiyse boss odasının kapıları açılır
        if (!bossAcildi && oda != CikisOdasi)
        {
            foreach (var o in odalar) if (o.tur == LdrOdaTuru.Savas && !o.temiz) return;
            bossAcildi = true;
            CikisOdasi.BossKapilariniAc();
            LdrHud.Duyur("SON KAPI AÇILDI  -  HARİTADAN BOSS İŞARETİNE IŞINLAN", new Color(1f, 0.5f, 0.5f));
        }
    }

    /// <summary>Oyuncuyu kararma ile başka bir odanın verilen noktasına taşır (ışınlanma).</summary>
    public void Isinla(Vector2 konum, LdrOda oda, System.Action varinca = null)
    {
        if (Gecis || oda == null) return;
        StartCoroutine(GecisYap(oda, konum, varinca));
    }

    IEnumerator GecisYap(LdrOda hedef, Vector2 konum, System.Action varinca)
    {
        Gecis = true;
        yasayan.Donuk = true;

        float t = 0f;
        while (t < 0.14f)
        {
            t += Time.unscaledDeltaTime;
            LdrHud.Karartma = Mathf.Clamp01(t / 0.14f);
            yield return null;
        }
        LdrHud.Karartma = 1f;

        foreach (var m in FindObjectsByType<LdrMermi>(FindObjectsSortMode.None)) Destroy(m.gameObject);
        yasayan.Konumla(konum);
        Giris(hedef);
        kamera.sinir = Sinir;
        kamera.Atla();
        if (varinca != null) varinca();
        yield return null;

        t = 0f;
        while (t < 0.18f)
        {
            t += Time.unscaledDeltaTime;
            LdrHud.Karartma = 1f - Mathf.Clamp01(t / 0.18f);
            yield return null;
        }
        LdrHud.Karartma = 0f;
        if (!yasayan.Can.Olu) yasayan.Donuk = false;
        Gecis = false;
    }
}
