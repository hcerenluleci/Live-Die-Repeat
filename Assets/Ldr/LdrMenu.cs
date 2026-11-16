using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================================
// DERS GEREKSİNİMİ: ANA MENÜ (Main Menu)
// Ne yapar: Oyun açılınca ana menüyü çizer: OYNA, NASIL OYNANIR, BÖLÜM SEÇ, PUAN TABLOSU, ÇIKIŞ.
// Akış: Ana menü -> Karakter seçimi (Liz/Jan) -> Silah Pazarı -> oyun. Ölünce yine Pazar ekranına döner.
// Nasıl çizilir: Unity IMGUI (OnGUI) ile, LdrUI araç kutusu ve kendi piksel fontumuzla; ayrı bir sahne yok.
// Menü sesi: düğmelerde sfx_ui_click, arkada korku müziği (LdrMuzik) -> "menüde en az 1 ses" gereksinimi.
// =====================================================================================
/// <summary>
/// Menü akışı. "LdrOyun" sahnesinin üstüne çizilir (sahne ayrıca değişmez):
///   ANA MENÜ (OYNA / NASIL OYNANIR / BÖLÜM SEÇ / PUAN TABLOSU / ÇIKIŞ)  ->  KARAKTER SEÇ (oturumda bir kez)  ->  SİLAH PAZARI  ->  oyun.
/// Ölünce ÖLDÜN ekranından sonra yine SİLAH PAZARI açılır. Menü açıkken zaman durur (timeScale 0).
/// </summary>
public class LdrMenu : MonoBehaviour
{
    public enum Sayfa { Ana, Bolumler, Yardim, Puanlar, Karakter, Pazar, Kapali }

    public const string OyunSahnesi = "LdrOyun";
    public static readonly string[] BolumAdlari = { "İLK ÖLÜM", "HATIRLADIKLARIN", "DÖNGÜ SIKILAŞIR", "ÇIKIŞ YOK", "SON UYANIŞ" };

    public static LdrMenu Ornek { get; private set; }
    public static Sayfa Gecerli = Sayfa.Ana;
    public static bool Acik { get { return Gecerli != Sayfa.Kapali; } }

    static bool ilkAcilis = true;
    static bool karakterSecildi;                 // bu oturumda karakter seçildi mi
    static Sayfa sonrakiSayfa = Sayfa.Kapali;    // sahne yenilenince açılacak sayfa
    static int secilenBolum;
    static string sonYasamOzeti = "";

    bool oyunIci;            // oyun sırasında "KARAKTERİ DEĞİŞTİR" ile açıldı
    bool ilkKontrol;
    int secili;              // pazar: seçili silah sırası

    // süzülen közler
    const int KorSayisi = 90;
    readonly float[] kx = new float[KorSayisi], ky = new float[KorSayisi], kh = new float[KorSayisi], kf = new float[KorSayisi], kb = new float[KorSayisi];
    readonly int[] kr = new int[KorSayisi];

    Texture2D zeminDoku;
    GUIStyle stBaslik, stDugme, stKucuk, stPiksel, stYazi;
    GUIStyle pBaslik, pAd, pKucuk, pEtiket, kBaslik, kAd, kKucuk, kDugme;
    Texture2D[] onizleme;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Kur()
    {
        var go = new GameObject("LdrMenu");
        DontDestroyOnLoad(go);
        go.AddComponent<LdrMenu>();
    }

    void Awake()
    {
        if (Ornek != null) { Destroy(gameObject); return; }
        Ornek = this;
        for (int i = 0; i < KorSayisi; i++) KorDogur(i, true);
        SceneManager.sceneLoaded += SahneYuklendi;
        LiveDieRepeat.OnDeath += OlumOldu;
    }

    void OnDestroy()
    {
        if (Ornek == this)
        {
            SceneManager.sceneLoaded -= SahneYuklendi;
            LiveDieRepeat.OnDeath -= OlumOldu;
            Ornek = null;
        }
    }

    static bool OyunSahnesiMi() { return SceneManager.GetActiveScene().name == OyunSahnesi; }

    void KorDogur(int i, bool herYerde)
    {
        kx[i] = Random.value; ky[i] = herYerde ? Random.value : 1.05f;
        kh[i] = Random.Range(0.02f, 0.09f); kf[i] = Random.value * 6.28f; kb[i] = Random.Range(2f, 5f);
        kr[i] = Random.Range(0, 3);
    }

    // ---------------------------------------------------------------- sahne ve ölüm
    void SahneYuklendi(Scene s, LoadSceneMode m)
    {
        if (m != LoadSceneMode.Single) return;
        SahneHazir();
    }

    void SahneHazir()
    {
        if (!OyunSahnesiMi()) { Gecerli = Sayfa.Kapali; return; }
        if (ilkAcilis) { ilkAcilis = false; Gecerli = Sayfa.Ana; }
        else { Gecerli = sonrakiSayfa; }
        sonrakiSayfa = Sayfa.Kapali;
        oyunIci = false;
        Sifirla();
    }

    void Sifirla()
    {
        if (Gecerli == Sayfa.Pazar) PazarHazirla();
        Time.timeScale = Acik ? 0f : 1f;
    }

    // Ölüm olayını dinler: bu yaşamın ruhunu kalıcı toplama ekler, puan tablosuna yazar, ölüm özetini hazırlar.
    void OlumOldu()
    {
        long kazanilan = LdrPuan.Ruh;
        LdrIlerleme.RuhEkle(kazanilan);
        int d = Mathf.Clamp(LdrOyun.Derinlik, 0, BolumAdlari.Length - 1);
        LdrIlerleme.PuanEkle(LdrIlerleme.KarakterAdi(LdrIlerleme.Karakter), "BÖLÜM " + (d + 1) + " - " + BolumAdlari[d], kazanilan);
        sonYasamOzeti = "BU YAŞAMDA " + kazanilan + " RUH TOPLADIN  -  TOPLAM " + LdrIlerleme.ToplamRuh;
        LdrPuan.Sifirla();
        sonrakiSayfa = Sayfa.Pazar;
        secilenBolum = 0;
    }

    // ---------------------------------------------------------------- akış
    // Menüyü kapatıp oyunu başlatır.
    void OyunaGec()
    {
        // seçilen bölüm ve silahla sahneyi yeniden kur
        LdrOyun.Derinlik = secilenBolum;
        LdrPuan.Sifirla();
        sonrakiSayfa = Sayfa.Kapali;
        Gecerli = Sayfa.Kapali;
        Time.timeScale = 1f;
        SceneManager.LoadScene(OyunSahnesi);
    }

    void BolumdenBasla(int bolum)
    {
        secilenBolum = bolum;
        Gecerli = karakterSecildi ? Sayfa.Pazar : Sayfa.Karakter;
        if (Gecerli == Sayfa.Pazar) PazarHazirla();
    }

    // Seçilen karakteri (0 = Liz, 1 = Jan) kaydeder ve Silah Pazarı ekranına geçer.
    void KarakterSec(int k)
    {
        LdrIlerleme.Karakter = k;
        karakterSecildi = true;
        if (oyunIci)
        {
            oyunIci = false;
            Gecerli = Sayfa.Kapali;
            Time.timeScale = 1f;
            var oyun = LdrOyun.Ornek;
            if (oyun != null && oyun.yasayan != null) oyun.yasayan.GorunumuYenile();
        }
        else
        {
            Gecerli = Sayfa.Pazar;
            PazarHazirla();
        }
    }

    List<LdrSilahTanimi> Liste { get { return LdrSilahTablosu.Oyuncu; } }

    void PazarHazirla()
    {
        string son = LdrIlerleme.SeciliSilah;
        secili = 0;
        var l = Liste;
        for (int i = 0; i < l.Count; i++) if (l[i].ad == son && l[i].Acik) secili = i;
        if (!l[secili].Acik) secili = 0;
    }

    /// <summary>Duraklatma menüsündeki "ANA MENÜ": sahneyi yeniden yükler ve ana menüyü açar.</summary>
    public static void AnaMenuyeDon()
    {
        ilkAcilis = true;
        sonrakiSayfa = Sayfa.Kapali;
        LdrOyun.Derinlik = 0;
        LdrPuan.Sifirla();
        Time.timeScale = 1f;
        SceneManager.LoadScene(OyunSahnesi);
    }

    void Cikis()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- güncelleme
    void Update()
    {
        if (!ilkKontrol) { ilkKontrol = true; SahneHazir(); }
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < KorSayisi; i++)
        {
            ky[i] -= kh[i] * dt;
            if (ky[i] < -0.05f) KorDogur(i, false);
        }
        if (Acik)
        {
            if (Time.timeScale != 0f) Time.timeScale = 0f;
            var oyun = LdrOyun.Ornek;
            if (oyun != null && oyun.yasayan != null) oyun.yasayan.Donuk = true;   // menü tıklaması ateş etmesin
        }
    }

    // ---------------------------------------------------------------- çizim yardımcıları
    void ZeminHazirla()
    {
        if (zeminDoku != null) return;
        // zemin: oyunun kendi karoları, çok koyu ve mor tonlu
        var karolar = new Color32[6][];
        int kb2 = 16;
        for (int i = 0; i < 6; i++)
        {
            var t = LdrResim.Tex("floor_" + i);
            if (t == null) continue;
            try { karolar[i] = t.GetPixels32(); kb2 = t.width; } catch (System.Exception) { }
        }
        const int W = 320, H = 180;
        zeminDoku = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int ti = ((x / kb2) + (y / kb2) * 3) % 6;
                Color c = new Color(0.09f, 0.04f, 0.14f);
                if (karolar[ti] != null) { var f = karolar[ti][(y % kb2) * kb2 + (x % kb2)]; c = new Color(f.r / 255f, f.g / 255f, f.b / 255f); }
                float dx = (x - W * 0.5f) / (W * 0.5f), dy = (y - H * 0.5f) / (H * 0.5f);
                float vig = Mathf.Clamp01(1.15f - (dx * dx * 0.55f + dy * dy * 0.9f));
                c = Color.Lerp(c, new Color(0.15f, 0.05f, 0.2f), 0.5f) * (0.30f * vig + 0.04f);
                c.a = 1f; px[y * W + x] = c;
            }
        zeminDoku.SetPixels32(px); zeminDoku.Apply();
    }

    void StilHazirla()
    {
        if (stBaslik != null) return;
        stBaslik = LdrUI.MakeStyle(TextAnchor.MiddleLeft);
        stDugme = LdrUI.MakeStyle(TextAnchor.MiddleLeft);
        stPiksel = LdrUI.MakeStyle(TextAnchor.MiddleLeft);
        stKucuk = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        stYazi = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, wordWrap = true };
        pBaslik = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
        pAd = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
        pKucuk = LdrUI.MakeStyle(TextAnchor.UpperCenter, true);
        pEtiket = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
        kBaslik = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
        kAd = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
        kKucuk = LdrUI.MakeStyle(TextAnchor.UpperCenter, false);
        kDugme = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
    }

    static readonly Color[] BolumRengi =
    {
        new Color(0.7f, 0.35f, 1f),     // 1 mor
        new Color(0.35f, 0.95f, 0.5f),  // 2 zehir yeşili
        new Color(1f, 0.8f, 0.25f),     // 3 kehribar
        new Color(1f, 0.28f, 0.32f),    // 4 kan kırmızısı
        new Color(0.4f, 0.9f, 1f),      // 5 buz mavisi
    };

    // piksel font: yazı kutuya sığana dek 8'in katlarında küçült
    int sabitYaziBoyu;
    static int Sigdir(float istenen, string yazi, float bosluk)
    {
        int fs = LdrUI.Px(istenen);
        while (fs > 16 && fs * LdrUI.CharW * yazi.Length > bosluk) fs = LdrUI.StepDown(fs);   // 16'nın altına inmez: döngü kesinlikle biter
        return fs;
    }

    static bool Tikla(Rect r)
    {
        var ev = Event.current;
        if (ev.type == EventType.MouseDown && ev.button == 0 && r.Contains(ev.mousePosition)) { ev.Use(); return true; }
        return false;
    }

    bool Dugme(Rect r, string yazi, float sc, bool buyuk) { return Dugme(r, yazi, sc, buyuk, LdrUI.Purple); }

    bool Dugme(Rect r, string yazi, float sc, bool buyuk, Color vurgu)
    {
        bool ustte = r.Contains(Event.current.mousePosition);
        float kayma = ustte ? 10f * sc : 0f;
        Rect rr = new Rect(r.x + kayma, r.y, r.width, r.height);
        if (Event.current.type == EventType.Repaint)
        {
            Color koyu = new Color(vurgu.r * 0.12f, vurgu.g * 0.12f, vurgu.b * 0.14f, 0.9f);
            Color orta = new Color(vurgu.r * 0.32f, vurgu.g * 0.32f, vurgu.b * 0.36f, 0.97f);
            LdrUI.Fill(rr, ustte ? orta : koyu);
            LdrUI.Frame(rr, ustte ? vurgu : new Color(vurgu.r * 0.65f, vurgu.g * 0.65f, vurgu.b * 0.65f), 2f);
            LdrUI.Fill(new Rect(rr.x, rr.y, 6f * sc, rr.height), ustte ? LdrUI.Blood : new Color(0.45f, 0.1f, 0.2f));
            stDugme.fontSize = sabitYaziBoyu > 0 ? sabitYaziBoyu : Sigdir((buyuk ? 24 : 20) * sc, yazi, rr.width - 40 * sc);
            LdrUI.Text(new Rect(rr.x + 22 * sc, rr.y, rr.width - 30 * sc, rr.height), yazi, stDugme,
                ustte ? Color.Lerp(vurgu, Color.white, 0.75f) : new Color(0.88f, 0.82f, 0.96f), 1f);
        }
        bool basti = Tikla(r);
        if (basti) LdrSes.Cal("sfx_ui_click", 0.5f);
        return basti;
    }

    void Isik(Vector2 c, float boyut, Color renk) { LdrUI.Light(c, boyut, renk); }

    // ---------------------------------------------------------------- OnGUI
    // Her karede o anki menü sayfasını (Ana, Karakter, Pazar, Bölümler, Puanlar, Yardım) çizer ve tıklamaları işler.
    void OnGUI()
    {
        if (!OyunSahnesiMi()) return;
        StilHazirla();
        float sc = Screen.height / 720f;

        // oyun sırasında: sağ üstte "KARAKTERİ DEĞİŞTİR" + ölüm ekranı ek satırları
        if (!Acik)
        {
            GUI.depth = -301;
            if (LiveDieRepeat.Dying) { OlumEkIcerik(sc); return; }
            return;
        }

        GUI.depth = -1000;
        switch (Gecerli)
        {
            case Sayfa.Karakter: KarakterEkrani(sc); return;
            case Sayfa.Pazar: PazarEkrani(sc); return;
        }
        AnaEkran(sc);
    }

    void OyunIciDugme(float sc)
    {
        if (pEtiket == null) return;
        float bw = Mathf.Max(212 * sc, 18 * 16f * LdrUI.CharW + 24f), bh = Mathf.Max(26 * sc, 30f);
        Rect br = new Rect(Screen.width - bw - 16f, 64 * sc, bw, bh);
        var ev = Event.current;
        bool ustte = br.Contains(ev.mousePosition);
        if (ev.type == EventType.Repaint)
        {
            LdrUI.Fill(br, ustte ? new Color(0.3f, 0.12f, 0.5f, 0.95f) : new Color(0.1f, 0.04f, 0.18f, 0.88f));
            LdrUI.Frame(br, ustte ? LdrUI.Purple : new Color(0.4f, 0.2f, 0.6f), 2f);
            pEtiket.fontSize = LdrUI.Px(8 * sc);
            var gc = new GUIContent("KARAKTERİ DEĞİŞTİR");
            while (pEtiket.fontSize > 16 && pEtiket.CalcSize(gc).x > br.width - 14f * sc) pEtiket.fontSize = LdrUI.StepDown(pEtiket.fontSize);
            LdrUI.Text(br, "KARAKTERİ DEĞİŞTİR", pEtiket, ustte ? Color.white : new Color(0.85f, 0.78f, 0.96f), 1f);
        }
        else if (ev.type == EventType.MouseDown && ev.button == 0 && ustte)
        {
            ev.Use();
            oyunIci = true;
            Gecerli = Sayfa.Karakter;
            Time.timeScale = 0f;
        }
    }

    void OlumEkIcerik(float sc)
    {
        if (Event.current.type != EventType.Repaint || pEtiket == null) return;
        float H = Screen.height;
        pEtiket.fontSize = Mathf.Max(8, LdrUI.Px(10 * sc));
        LdrUI.Text(new Rect(0, H * 0.34f + 140 * sc, Screen.width, 24 * sc), sonYasamOzeti, pEtiket, new Color(0.95f, 0.85f, 0.9f), 1f);
        string yeni = LdrIlerleme.YeniAcilanlar();
        if (!string.IsNullOrEmpty(yeni))
        {
            pEtiket.fontSize = LdrUI.Px(12 * sc);
            LdrUI.Text(new Rect(0, H * 0.34f + 176 * sc, Screen.width, 28 * sc), yeni, pEtiket, new Color(1f, 0.85f, 0.3f), 1f);
        }
    }

    // ================================================================ ANA EKRAN
    void AnaEkran(float sc)
    {
        float t = Time.unscaledTime;
        float W = Screen.width, H = Screen.height;
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape && Gecerli != Sayfa.Ana) { Event.current.Use(); Gecerli = Sayfa.Ana; }

        ZeminHazirla();
        if (Event.current.type == EventType.Repaint)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), zeminDoku, ScaleMode.StretchToFill);
            // sis ve meşale ışıkları
            Isik(new Vector2(W * (0.25f + 0.05f * Mathf.Sin(t * 0.2f)), H * 0.7f), 520 * sc, new Color(0.45f, 0.1f, 0.6f, 0.18f));
            Isik(new Vector2(W * (0.75f + 0.05f * Mathf.Cos(t * 0.17f)), H * 0.35f), 480 * sc, new Color(0.1f, 0.5f, 0.7f, 0.12f));
            Mesale(new Vector2(W * 0.52f, H * 0.8f), sc, t, new Color(0.7f, 0.3f, 1f));
            Mesale(new Vector2(W - 46 * sc, H * 0.5f), sc, t + 1.3f, new Color(0.3f, 0.9f, 1f));
            // közler
            for (int i = 0; i < KorSayisi; i++)
            {
                float x = kx[i] * W + Mathf.Sin(t * 0.8f + kf[i]) * 14f * sc;
                float y = ky[i] * H;
                float a = Mathf.Clamp01(Mathf.Min(ky[i] + 0.05f, 1.05f - ky[i]) * 4f) * (0.5f + 0.5f * Mathf.Sin(t * 3f + kf[i]));
                Color c = kr[i] == 0 ? new Color(0.8f, 0.4f, 1f, a) : kr[i] == 1 ? new Color(0.4f, 0.95f, 1f, a) : new Color(1f, 0.5f, 0.25f, a);
                LdrUI.Fill(new Rect(x, y, kb[i] * sc, kb[i] * sc), c);
            }

            float lx0 = 80 * sc;
            // başlık
            float titrek = 0.9f + 0.1f * Mathf.PerlinNoise(t * 7f, 3f);
            stBaslik.fontSize = LdrUI.Px(10 * sc);
            LdrUI.Text(new Rect(lx0 + 4, 12 * sc, 520 * sc, 22 * sc), "LIVE - DIE - REPEAT", stBaslik, new Color(0.6f, 0.52f, 0.82f, 0.9f * titrek), 1f);
            stBaslik.fontSize = LdrUI.Px(60 * sc);
            string[] kelimeler = { "YAŞA.", "ÖL.", "TEKRARLA." };
            Color[] kr2 = { new Color(0.95f, 0.92f, 1f), new Color(0.95f, 0.18f, 0.25f), new Color(0.72f, 0.4f, 1f) };
            for (int i = 0; i < 3; i++)
            {
                Rect wr = new Rect(lx0, 40 * sc + i * 78 * sc, 700 * sc, 90 * sc);
                LdrUI.Text(new Rect(wr.x - 3, wr.y, wr.width, wr.height), kelimeler[i], stBaslik, new Color(0.2f, 0.9f, 1f, 0.35f * titrek), 0f);
                LdrUI.Text(wr, kelimeler[i], stBaslik, kr2[i] * titrek, 4f * sc * 0.5f);
            }
            stKucuk.fontSize = Mathf.RoundToInt(16 * sc); stKucuk.alignment = TextAnchor.MiddleLeft;
            stKucuk.normal.textColor = new Color(0.7f, 0.6f, 0.85f);
            GUI.Label(new Rect(lx0 + 4, 282 * sc, 600 * sc, 26 * sc), "Zindan seni hatırlar. Sen onu hatırlayacak mısın?", stKucuk);
        }

        float lx = 80 * sc;
        if (Gecerli == Sayfa.Ana) AnaSecenekler(sc, lx);
        else if (Gecerli == Sayfa.Bolumler) BolumSecenekleri(sc, lx);
        else if (Gecerli == Sayfa.Yardim) YardimSayfasi(sc, lx);
        else PuanSayfasi(sc, lx);

        if (Event.current.type == EventType.Repaint)
        {
            Kahramanlar(sc, t);
            // alt bilgi
            stKucuk.alignment = TextAnchor.MiddleLeft; stKucuk.fontSize = Mathf.RoundToInt(14 * sc);
            stKucuk.normal.textColor = new Color(0.55f, 0.5f, 0.7f);
            GUI.Label(new Rect(lx, H - 36 * sc, 900 * sc, 26 * sc),
                "ÖLÜM: " + LiveDieRepeat.Deaths + "     TOPLAM RUH: " + LdrIlerleme.ToplamRuh + "     EN DERİN BÖLÜM: " + (LdrIlerleme.EnDerin + 1), stKucuk);
            KacisPanosu(sc, t);
        }
    }

    void AnaSecenekler(float sc, float lx)
    {
        // yeni oyuncu önce "nasıl oynanır"ı görsün diye ikinci sırada
        string[] etiketler = { "OYNA", "NASIL OYNANIR", "BÖLÜM SEÇ", "PUAN TABLOSU", "ÇIKIŞ" };
        for (int i = 0; i < etiketler.Length; i++)
        {
            Rect r = new Rect(lx, 332 * sc + i * 60 * sc, 360 * sc, 50 * sc);
            if (Dugme(r, etiketler[i], sc, i == 0))
            {
                if (i == 0) BolumdenBasla(0);
                else if (i == 1) Gecerli = Sayfa.Yardim;
                else if (i == 2) Gecerli = Sayfa.Bolumler;
                else if (i == 3) Gecerli = Sayfa.Puanlar;
                else Cikis();
            }
        }
    }

    void BolumSecenekleri(float sc, float lx)
    {
        // hepsi aynı punto: en uzun etikete göre
        int ortak = 64;
        for (int i = 0; i < BolumAdlari.Length; i++)
            ortak = Mathf.Min(ortak, Sigdir(20 * sc, "BÖLÜM " + (i + 1) + " - " + BolumAdlari[i], 650 * sc - 40 * sc));
        sabitYaziBoyu = ortak;
        for (int i = 0; i < BolumAdlari.Length; i++)
        {
            bool acik = LdrIlerleme.HepsiAcik || i <= LdrIlerleme.EnDerin;
            Rect r = new Rect(lx, 322 * sc + i * 52 * sc, 650 * sc, 44 * sc);
            string et = "BÖLÜM " + (i + 1) + " - " + (acik ? BolumAdlari[i] : "KİLİTLİ");
            if (acik) { if (Dugme(r, et, sc, false, BolumRengi[i % BolumRengi.Length])) BolumdenBasla(i); }
            else if (Event.current.type == EventType.Repaint)
            {
                LdrUI.Fill(r, new Color(0.04f, 0.02f, 0.06f, 0.8f)); LdrUI.Frame(r, new Color(0.25f, 0.2f, 0.3f), 2f);
                stDugme.fontSize = sabitYaziBoyu > 0 ? sabitYaziBoyu : Sigdir(20 * sc, et, r.width - 40 * sc);
                LdrUI.Text(new Rect(r.x + 22 * sc, r.y, r.width, r.height), et, stDugme, new Color(0.5f, 0.45f, 0.55f), 1f);
            }
        }
        sabitYaziBoyu = 0;
        if (Dugme(new Rect(lx, 322 * sc + BolumAdlari.Length * 52 * sc + 10 * sc, 220 * sc, 44 * sc), "◄ GERİ", sc, false)) Gecerli = Sayfa.Ana;
    }

    static readonly string[][] Tuslar =
    {
        new[] { "WASD", "hareket" },       new[] { "SPACE", "takla at (dokunulmaz)" },
        new[] { "FARE", "nişan al" },      new[] { "SOL TIK", "ateş et" },
        new[] { "R", "şarjör değiştir" },  new[] { "T", "doğuş noktasına ışınlan" },
        new[] { "F", "ateş topu fırlat" }, new[] { "M", "haritayı aç / kapat" },
        new[] { "B", "ışık seviyesi" },    new[] { "ESC", "duraklat" },
        new[] { "ENTER", "bölüm bitince alt kata in" },
    };

    void YardimSayfasi(float sc, float lx)
    {
        float pw = 640 * sc, ph = 296 * sc;
        Rect r = new Rect(lx, 312 * sc, pw, ph);
        if (Event.current.type == EventType.Repaint)
        {
            LdrUI.Panel(r, LdrUI.Purple, sc, 0.9f, 0.5f);
            stPiksel.alignment = TextAnchor.MiddleCenter;
            stYazi.fontSize = Mathf.Max(13, Mathf.RoundToInt(15 * sc));
            stYazi.alignment = TextAnchor.MiddleLeft; stYazi.wordWrap = false;
            stYazi.normal.textColor = new Color(0.92f, 0.88f, 0.98f);
            float kolonG = (pw - 36 * sc) * 0.5f, satirY = 26 * sc, tusG = Mathf.Max(74 * sc, 96f);
            for (int i = 0; i < Tuslar.Length; i++)
            {
                int kol = i % 2, sat = i / 2;
                float x = r.x + 18 * sc + kol * kolonG, y = r.y + 16 * sc + sat * satirY;
                Rect kutu = new Rect(x, y, tusG, satirY - 6 * sc);
                LdrUI.Fill(kutu, new Color(0.18f, 0.08f, 0.3f, 1f));
                LdrUI.Frame(kutu, new Color(0.62f, 0.4f, 0.95f), 1.5f);
                stPiksel.fontSize = Sigdir(8 * sc, Tuslar[i][0], kutu.width - 8 * sc);
                LdrUI.Text(kutu, Tuslar[i][0], stPiksel, Color.white, 0f);
                float aciklamaG = kolonG - tusG - 12 * sc;
                int ayar = stYazi.fontSize;
                while (ayar > 10 && stYazi.CalcSize(new GUIContent(Tuslar[i][1])).x * ayar / Mathf.Max(1, stYazi.fontSize) > aciklamaG) ayar--;
                int eski = stYazi.fontSize; stYazi.fontSize = ayar;
                GUI.Label(new Rect(x + tusG + 8 * sc, y - 2 * sc, aciklamaG + 6 * sc, satirY), Tuslar[i][1], stYazi);
                stYazi.fontSize = eski;
            }
            stPiksel.alignment = TextAnchor.MiddleLeft;
            stYazi.alignment = TextAnchor.UpperLeft; stYazi.wordWrap = true;
            // ayraç + ipuçları
            float ty = r.y + 16 * sc + 6 * satirY + 6 * sc;
            LdrUI.Fill(new Rect(r.x + 18 * sc, ty - 6 * sc, pw - 36 * sc, 1.5f), new Color(0.5f, 0.25f, 0.75f, 0.7f));
            stYazi.fontSize = Mathf.Max(13, Mathf.RoundToInt(14 * sc));
            stYazi.normal.textColor = new Color(0.8f, 0.74f, 0.95f);
            GUI.Label(new Rect(r.x + 18 * sc, ty, pw - 36 * sc, r.yMax - ty - 8 * sc),
                "• Öldüğünde her şey baştan başlar, ama silahların kilidi kalıcı açılır.\n" +
                "• Mermin biterse ruhlar sana birkaç mermi sızdırır. Fıçıları kır: can, kalkan, cephane ve ateş düşer.\n" +
                "• Mor heykellerin yanında kısa bir nefes alırsın. Her derinlikte bir mühürlü sandık kaçış parçası saklar; dördünü topla.", stYazi);
        }
        if (Dugme(new Rect(lx, r.yMax + 12 * sc, 220 * sc, 44 * sc), "◄ GERİ", sc, false)) Gecerli = Sayfa.Ana;
    }

    void PuanSayfasi(float sc, float lx)
    {
        float pw = 640 * sc, ph = 280 * sc;
        Rect r = new Rect(lx, 312 * sc, pw, ph);
        if (Event.current.type == EventType.Repaint)
        {
            LdrUI.Panel(r, LdrUI.Purple, sc, 0.9f, 0.5f);
            stPiksel.alignment = TextAnchor.MiddleLeft;
            stPiksel.fontSize = LdrUI.Px(12 * sc);
            LdrUI.Text(new Rect(r.x + 18 * sc, r.y + 10 * sc, pw - 36 * sc, 26 * sc), "PUAN TABLOSU", stPiksel, new Color(0.95f, 0.3f, 0.4f), 1f);
            stPiksel.fontSize = LdrUI.Px(8 * sc);
            float sira = r.x + 18 * sc, isim = r.x + 90 * sc, bolum = r.x + 230 * sc, puan = r.xMax - 150 * sc;
            float hy = r.y + 46 * sc;
            Color baslikRengi = new Color(0.6f, 0.5f, 0.85f);
            LdrUI.Text(new Rect(sira, hy, 60 * sc, 18 * sc), "SIRA", stPiksel, baslikRengi, 0f);
            LdrUI.Text(new Rect(isim, hy, 100 * sc, 18 * sc), "İSİM", stPiksel, baslikRengi, 0f);
            LdrUI.Text(new Rect(bolum, hy, 200 * sc, 18 * sc), "BÖLÜM", stPiksel, baslikRengi, 0f);
            LdrUI.Text(new Rect(puan, hy, 140 * sc, 18 * sc), "PUAN", stPiksel, baslikRengi, 0f);
            LdrUI.Fill(new Rect(r.x + 18 * sc, hy + 22 * sc, pw - 36 * sc, 1.5f), new Color(0.5f, 0.25f, 0.75f, 0.7f));
            var kayitlar = LdrIlerleme.PuanTablosu();
            if (kayitlar.Count == 0)
                LdrUI.Text(new Rect(r.x + 18 * sc, r.y + 110 * sc, pw - 36 * sc, 24 * sc), "HENÜZ KAYIT YOK", stPiksel, new Color(0.7f, 0.62f, 0.85f), 0f);
            for (int i = 0; i < Mathf.Min(7, kayitlar.Count); i++)
            {
                float y = hy + 30 * sc + i * 24 * sc;
                Color c = i == 0 ? new Color(1f, 0.85f, 0.35f) : new Color(0.92f, 0.88f, 0.98f);
                LdrUI.Text(new Rect(sira, y, 60 * sc, 22 * sc), (i + 1).ToString(), stPiksel, c, 0f);
                LdrUI.Text(new Rect(isim, y, 130 * sc, 22 * sc), kayitlar[i].isim, stPiksel, c, 0f);
                string b = kayitlar[i].bolum;
                int ks = Sigdir(8 * sc, b, 230 * sc);
                int eski = stPiksel.fontSize; stPiksel.fontSize = ks;
                LdrUI.Text(new Rect(bolum, y, 290 * sc, 22 * sc), b, stPiksel, c, 0f);
                stPiksel.fontSize = eski;
                LdrUI.Text(new Rect(puan, y, 140 * sc, 22 * sc), kayitlar[i].puan.ToString("N0"), stPiksel, c, 0f);
            }
        }
        if (Dugme(new Rect(lx, r.yMax + 10 * sc, 220 * sc, 44 * sc), "◄ GERİ", sc, false)) Gecerli = Sayfa.Ana;
    }

    // kaçış parçaları: her yeni derinlikte bir kristal parça; dördü de toplanınca çıkış kapısı açılır
    static readonly string[] ParcaPiksel =
    {
        "....o....", "...oao...", "..oaabo..", "..oaabo..", ".oaaabbo.", ".oaabbbo.", ".oabbbco.",
        ".oabbcco.", "..obbcco.", "..obbco..", "...obco..", "....oo...", ".....o..."
    };
    static readonly string[] Romen = { "I", "II", "III", "IV" };

    void ParcaCiz(float x, float y, float px, bool acik, bool altin)
    {
        Color kenar = acik ? (altin ? new Color(1f, 0.95f, 0.7f) : new Color(0.98f, 0.93f, 1f)) : new Color(0.3f, 0.2f, 0.45f);
        Color ca = altin ? new Color(1f, 0.95f, 0.6f) : new Color(0.93f, 0.82f, 1f);
        Color cb = altin ? new Color(1f, 0.75f, 0.25f) : new Color(0.7f, 0.42f, 1f);
        Color cc = altin ? new Color(0.75f, 0.45f, 0.1f) : new Color(0.4f, 0.18f, 0.72f);
        Color bos = new Color(0.09f, 0.04f, 0.14f);
        for (int r = 0; r < ParcaPiksel.Length; r++)
            for (int c = 0; c < ParcaPiksel[r].Length; c++)
            {
                char ch = ParcaPiksel[r][c];
                if (ch == '.') continue;
                Color col = ch == 'o' ? kenar : !acik ? bos : ch == 'a' ? ca : ch == 'b' ? cb : cc;
                LdrUI.Fill(new Rect(Mathf.Round(x + c * px), Mathf.Round(y + r * px), Mathf.Ceil(px), Mathf.Ceil(px)), col);
            }
    }

    // kemerli niş: üstte basamaklı kemer, altta düz gövde
    static void Nis(Rect r, float s, Color c)
    {
        LdrUI.Fill(new Rect(r.x + 2 * s, r.y, r.width - 4 * s, s), c);
        LdrUI.Fill(new Rect(r.x + s, r.y + s, r.width - 2 * s, s), c);
        LdrUI.Fill(new Rect(r.x, r.y + 2 * s, r.width, r.height - 2 * s), c);
    }

    void KacisPanosu(float sc, float t)
    {
        int var2 = LdrParca.Sayi();
        int gerekli = LdrParca.Gerekli;
        bool hepsi = var2 >= gerekli;
        Color vurgu = hepsi ? new Color(1f, 0.8f, 0.3f) : new Color(0.62f, 0.32f, 1f);
        float yuva = 62 * sc, bosluk = 12 * sc, ic = 18 * sc;
        float lx = Screen.width * 0.72f - (gerekli * yuva + (gerekli - 1) * bosluk) * 0.5f;   // karakterlerle aynı eksen
        float w = gerekli * yuva + (gerekli - 1) * bosluk + ic * 2f;
        float ust = Screen.height * 0.34f + 150 * sc + 100 * sc;
        Rect panel = new Rect(lx - ic, ust, w, 128 * sc);
        LdrUI.Panel(panel, vurgu, sc, 0.9f, hepsi ? 1.2f : 0.5f);

        stPiksel.fontSize = LdrUI.Px(10 * sc);
        stPiksel.alignment = TextAnchor.MiddleCenter;
        LdrUI.Text(new Rect(panel.x, panel.y + 8 * sc, panel.width, 18 * sc),
            hepsi ? "KAÇIŞ YOLU AÇILDI" : "MÜHÜRLÜ ÇIKIŞ   " + var2 + "/" + gerekli, stPiksel,
            hepsi ? new Color(1f, 0.85f, 0.35f) : new Color(0.78f, 0.66f, 1f), 1f);
        stPiksel.alignment = TextAnchor.MiddleLeft;

        float px = 3f * sc, s = 2f * sc;
        for (int i = 0; i < gerekli; i++)
        {
            bool acik = LdrParca.Var(i);
            Rect nr = new Rect(lx + i * (yuva + bosluk), panel.y + 34 * sc, yuva, 78 * sc);
            Color kenar = acik ? vurgu : new Color(0.25f, 0.15f, 0.38f);
            Nis(new Rect(nr.x - s, nr.y - s, nr.width + 2 * s, nr.height + 2 * s), s, kenar);
            Nis(nr, s, new Color(0.02f, 0.008f, 0.04f));
            LdrUI.Fill(new Rect(nr.x, nr.y + nr.height * 0.55f, nr.width, nr.height * 0.45f), new Color(vurgu.r, vurgu.g, vurgu.b, acik ? 0.10f : 0.04f));
            float sallan = acik ? Mathf.Sin(t * 2f + i * 1.3f) * 3f * sc : 0f;
            float cx = nr.center.x, cy = nr.y + 34 * sc + sallan;
            if (acik) LdrUI.Light(new Vector2(cx, cy), 48 * sc, new Color(vurgu.r, vurgu.g, vurgu.b, 0.30f + 0.12f * Mathf.Sin(t * 3f + i)));
            ParcaCiz(cx - 4.5f * px, cy - 6.5f * px, px, acik, hepsi);
            if (acik && Mathf.Sin(t * 4f + i * 2.1f) > 0.93f) LdrUI.Fill(new Rect(cx - 2 * px, cy - 4.5f * px, 2 * px, 2 * px), Color.white);
            stPiksel.fontSize = LdrUI.Px(9 * sc); stPiksel.alignment = TextAnchor.MiddleCenter;
            LdrUI.Text(new Rect(nr.x, nr.yMax - 20 * sc, nr.width, 14 * sc), Romen[i], stPiksel,
                acik ? new Color(0.92f, 0.82f, 1f) : new Color(0.4f, 0.3f, 0.55f), 0f);
            stPiksel.alignment = TextAnchor.MiddleLeft;
        }
    }

    Texture2D Onizleme(int k)
    {
        if (onizleme == null) onizleme = new Texture2D[2];
        if (onizleme[k] == null)
        {
            var s = LdrKostum.SkinSprite(k, false, 16f);
            onizleme[k] = s != null ? s.texture : null;
        }
        return onizleme[k];
    }

    void Kahramanlar(float sc, float t)
    {
        // simetrik sahne: eksen (cx) etrafında iki karakter, aynı taban çizgisi, ortalanmış alt yazı
        float cw = 170 * sc, bosluk = 40 * sc;
        float cx = Screen.width * 0.72f;
        float y0 = Screen.height * 0.34f;
        float sallan = Mathf.Sin(t * 1.6f) * 5f * sc;            // ikisi aynı anda süzülür
        for (int i = 0; i < 2; i++)
        {
            var tex = Onizleme(i);
            float x = i == 0 ? cx - bosluk * 0.5f - cw : cx + bosluk * 0.5f;
            Color ton = i == 0 ? new Color(1f, 0.4f, 0.8f) : new Color(0.3f, 0.8f, 1f);
            Isik(new Vector2(x + cw * 0.5f, y0 + cw * 0.55f), cw * 1.1f, new Color(ton.r, ton.g, ton.b, 0.22f));
            LdrUI.Fill(new Rect(x + cw * 0.25f, y0 + cw - 4 * sc, cw * 0.5f, 3 * sc), new Color(0f, 0f, 0f, 0.35f));   // zemin gölgesi
            if (tex != null) GUI.DrawTexture(new Rect(x, y0 + sallan, cw, cw), tex, ScaleMode.ScaleToFit, true);
            stPiksel.fontSize = LdrUI.Px(16 * sc); stPiksel.alignment = TextAnchor.MiddleCenter;
            LdrUI.Text(new Rect(x, y0 + cw + 8 * sc, cw, 24 * sc), i == 0 ? "LIZ" : "JAN", stPiksel, Color.white, 1.5f);
            LdrUI.Fill(new Rect(x + cw * 0.3f, y0 + cw + 34 * sc, cw * 0.4f, Mathf.Max(2f, 2.5f * sc)), new Color(ton.r, ton.g, ton.b, 0.8f));
            stPiksel.alignment = TextAnchor.MiddleLeft;
        }
        stKucuk.alignment = TextAnchor.MiddleCenter; stKucuk.fontStyle = FontStyle.Normal; stKucuk.fontSize = Mathf.RoundToInt(14 * sc);
        stKucuk.normal.textColor = new Color(0.65f, 0.6f, 0.8f);
        GUI.Label(new Rect(cx - 220 * sc, y0 + cw + 44 * sc, 440 * sc, 24 * sc), "Karakterini oyunun başında seçersin", stKucuk);
        stKucuk.alignment = TextAnchor.MiddleLeft;
    }

    void Mesale(Vector2 c, float sc, float t, Color renk)
    {
        float titrek = 0.8f + 0.2f * Mathf.PerlinNoise(t * 6f, c.x);
        Isik(c + new Vector2(0, -30 * sc), 170 * sc * titrek, new Color(renk.r, renk.g, renk.b, 0.35f));
        var tutucu = LdrResim.Tex("skull_holder");
        if (tutucu != null)
        {
            float oran = tutucu.width / (float)tutucu.height;
            float h = 90 * sc, w = h * oran;
            GUI.DrawTexture(new Rect(c.x - w * 0.5f, c.y - h * 0.3f, w, h), tutucu, ScaleMode.ScaleToFit, true);
        }
        var alev = LdrResim.Tex("flame_" + (Mathf.FloorToInt(t * 8f) % 3));
        if (alev != null)
        {
            var o = GUI.color; GUI.color = renk * 1.3f;
            float oran = alev.width / (float)alev.height; float h = 56 * sc;
            GUI.DrawTexture(new Rect(c.x - h * oran * 0.5f, c.y - h * 1.05f, h * oran, h), alev, ScaleMode.ScaleToFit, true);
            GUI.color = o;
        }
    }

    // ================================================================ KARAKTER SEÇ
    static readonly string[] Anlatim =
    {
        "Sessiz adımlar.\nKaranlığın içinde\nyolunu hep bulur.",
        "Sakin ve kararlı.\nHer gölgenin ardına\nbir kez daha bakar."
    };
    static readonly Color[] KarakterRengi = { new Color(1f, 0.42f, 0.78f), new Color(0.38f, 0.88f, 1f) };

    void KarakterEkrani(float sc)
    {
        float t = Time.unscaledTime;
        float W = Screen.width, H = Screen.height;
        bool cizim = Event.current.type == EventType.Repaint;

        if (cizim)
        {
            LdrUI.Fill(new Rect(0, 0, W, H), new Color(0.018f, 0.006f, 0.035f, 1f));
            LdrUI.Light(new Vector2(W * 0.5f, H * 0.55f), H * 0.9f, new Color(0.35f, 0.1f, 0.55f, 0.22f));
            LdrUI.Light(new Vector2(W * (0.2f + 0.03f * Mathf.Sin(t * 0.3f)), H * 0.8f), H * 0.55f, new Color(0.6f, 0.08f, 0.2f, 0.14f));
            LdrUI.Light(new Vector2(W * (0.82f + 0.03f * Mathf.Cos(t * 0.27f)), H * 0.25f), H * 0.5f, new Color(0.1f, 0.5f, 0.7f, 0.12f));
            // süzülen közler (durumsuz, sadece zamandan hesaplanır)
            for (int i = 0; i < 40; i++)
            {
                float fx = Mathf.Repeat(i * 0.137f + Mathf.Sin(t * 0.3f + i) * 0.02f, 1f);
                float fy = 1f - Mathf.Repeat(t * (0.02f + (i % 5) * 0.012f) + i * 0.173f, 1f);
                float a = 0.25f + 0.45f * Mathf.Sin(t * 2.5f + i * 1.7f) * Mathf.Sin(t * 2.5f + i * 1.7f);
                Color c = i % 3 == 0 ? new Color(0.8f, 0.4f, 1f, a) : i % 3 == 1 ? new Color(0.4f, 0.95f, 1f, a) : new Color(1f, 0.5f, 0.3f, a);
                float sz = (2f + (i % 3)) * sc;
                LdrUI.Fill(new Rect(fx * W, fy * H, sz, sz), c);
            }
            LdrUI.Scanlines(t, sc);
        }

        kBaslik.fontSize = LdrUI.Px(40 * sc);
        LdrUI.Text(new Rect(0, 24 * sc, W, 60 * sc), "KARAKTERİNİ SEÇ", kBaslik, new Color(0.92f, 0.2f, 0.3f), 2f);
        kKucuk.fontSize = LdrUI.Px(14 * sc);
        LdrUI.Text(new Rect(0, 86 * sc, W, 24 * sc), "YAŞA.  ÖL.  TEKRARLA.", kKucuk, new Color(0.78f, 0.68f, 0.95f), 1f);

        float kw = Mathf.Min(300f * sc, W * 0.3f), kh2 = Mathf.Min(470f * sc, H * 0.78f);
        // en uzun açıklama satırı en küçük piksel boyutunda bile sığsın diye kartı gerekirse genişlet
        {
            int enUzun = 0;
            for (int q = 0; q < 2; q++) foreach (var ln in Anlatim[q].Split('\n')) enUzun = Mathf.Max(enUzun, ln.Length);
            float gerek = enUzun * 16f * LdrUI.CharW + 28f;
            kw = Mathf.Min(Mathf.Max(kw, gerek), W * 0.4f);
        }
        float aralik = 70f * sc;
        string[] adlar = { "LIZ", "JAN" };
        int mevcut = LdrIlerleme.Karakter;
        for (int i = 0; i < 2; i++)
        {
            float x = W * 0.5f + (i == 0 ? -kw - aralik * 0.5f : aralik * 0.5f);
            Rect kr3 = new Rect(x, H * 0.5f - kh2 * 0.5f + 28f * sc, kw, kh2);
            bool ustte = kr3.Contains(Event.current.mousePosition);
            Color vurgu = KarakterRengi[i];
            if (cizim)
            {
                LdrUI.Panel(kr3, ustte ? vurgu : new Color(vurgu.r * 0.7f, vurgu.g * 0.7f, vurgu.b * 0.7f), sc, 0.94f, ustte ? 1.6f : 0.8f);
                float sallan = Mathf.Sin(t * 1.7f + i * 1.9f) * 7f * sc;
                float ps = Mathf.Min(kw * 0.72f, kh2 * 0.42f);
                Vector2 pc = new Vector2(kr3.center.x, kr3.y + ps * 0.55f);
                LdrUI.Light(pc, ps * (ustte ? 0.95f : 0.75f), new Color(vurgu.r, vurgu.g, vurgu.b, ustte ? 0.38f : 0.22f));
                LdrUI.Light(new Vector2(pc.x, pc.y + ps * 0.46f), ps * 0.4f, new Color(0f, 0f, 0f, 0.6f));   // zemin gölgesi
                var tex = Onizleme(i);
                if (tex != null) GUI.DrawTexture(new Rect(kr3.center.x - ps * 0.5f, kr3.y + 14f * sc + sallan - (ustte ? 6f * sc : 0f), ps, ps), tex, ScaleMode.ScaleToFit, true);
                kAd.fontSize = LdrUI.Px(30 * sc);
                LdrUI.Text(new Rect(kr3.x, kr3.y + ps + 22f * sc, kw, 40f * sc), adlar[i], kAd, Color.Lerp(vurgu, Color.white, 0.6f), 2f);
                var satirlar = Anlatim[i].Split('\n');
                float ustY = kr3.y + ps + 62f * sc;
                float altY = kr3.yMax - 52f * sc - 6f * sc;
                float bosluk = Mathf.Max(20f, altY - ustY);
                int lfs = LdrUI.Px(13 * sc);
                while (lfs > 12 && lfs * 1.4f * satirlar.Length > bosluk) lfs = lfs > 16 ? lfs - 8 : 12;
                kKucuk.fontSize = lfs;
                float lh = Mathf.Min(lfs * 1.5f, bosluk / satirlar.Length);
                for (int k = 0; k < satirlar.Length; k++)
                    LdrUI.Text(new Rect(kr3.x + 10f * sc, ustY + k * lh, kw - 20f * sc, lh), satirlar[k], kKucuk, new Color(0.78f, 0.72f, 0.9f), 0f);
                // SEÇ şeridi
                Rect br = new Rect(kr3.x + 18f * sc, kr3.yMax - 52f * sc, kw - 36f * sc, 36f * sc);
                LdrUI.Fill(br, ustte ? new Color(vurgu.r * 0.55f, vurgu.g * 0.55f, vurgu.b * 0.55f, 1f) : new Color(vurgu.r * 0.2f, vurgu.g * 0.2f, vurgu.b * 0.2f, 1f));
                LdrUI.Frame(br, vurgu, 2f);
                kDugme.fontSize = LdrUI.Px(16 * sc);
                LdrUI.Text(br, i == mevcut ? "SEÇİLİ  >" : "SEÇ  >", kDugme, Color.white, 1f);
            }
            var ev = Event.current;
            bool tus = ev.type == EventType.KeyDown && (ev.keyCode == (i == 0 ? KeyCode.Alpha1 : KeyCode.Alpha2) || ev.keyCode == (i == 0 ? KeyCode.Keypad1 : KeyCode.Keypad2) || (ev.keyCode == KeyCode.Return && i == mevcut));
            if ((ev.type == EventType.MouseDown && ev.button == 0 && kr3.Contains(ev.mousePosition)) || tus)
            { ev.Use(); KarakterSec(i); return; }
        }

        // GERİ düğmesi (yalnızca tıklayarak; Esc çalışmaz):karakter hiç seçilmediyse bölüm listesine, seçiliyse silah pazarına döner
        {
            float gw = 200f * sc, gh = 46f * sc;
            Rect gb = new Rect(48f * sc, H - gh - 34f * sc, gw, gh);
            bool ust = gb.Contains(Event.current.mousePosition);
            if (cizim)
            {
                LdrUI.Fill(gb, ust ? new Color(0.2f, 0.08f, 0.34f, 1f) : new Color(0.1f, 0.04f, 0.18f, 1f));
                LdrUI.Frame(gb, ust ? LdrUI.Purple : new Color(0.5f, 0.3f, 0.8f), 2f);
                pEtiket.fontSize = LdrUI.Px(9 * sc);
                LdrUI.Text(gb, "◄ GERİ", pEtiket, LdrUI.Bone, 1f);
            }
            if (Tikla(gb))
            {
                if (oyunIci) { oyunIci = false; Gecerli = Sayfa.Kapali; Time.timeScale = 1f; }
                else if (karakterSecildi) { Gecerli = Sayfa.Pazar; PazarHazirla(); }
                else Gecerli = Sayfa.Ana;   // ilk girişte GERİ → OYNA / BÖLÜM SEÇ ... ana menüsü
                return;
            }
        }
    }

    // ================================================================ SİLAH PAZARI
    void PazarEkrani(float sc)
    {
        float t = Time.unscaledTime;
        float W = Screen.width, H = Screen.height, cx = W * 0.5f;
        bool cizim = Event.current.type == EventType.Repaint;
        var liste = Liste;
        if (cizim)
        {
            LdrUI.Fill(new Rect(0, 0, W, H), new Color(0.016f, 0.005f, 0.032f, 1f));
            LdrUI.Light(new Vector2(cx, H * 0.45f), H * 0.95f, new Color(0.32f, 0.08f, 0.5f, 0.2f));
            LdrUI.Light(new Vector2(W * 0.12f, H * 0.9f), H * 0.5f, new Color(0.6f, 0.07f, 0.18f, 0.13f));
            LdrUI.Scanlines(t, sc);
        }

        pBaslik.fontSize = LdrUI.Px(34 * sc);
        LdrUI.Text(new Rect(0, 20 * sc, W, 50 * sc), "SİLAH PAZARI", pBaslik, new Color(0.9f, 0.2f, 0.3f), 2f);
        pKucuk.fontSize = LdrUI.Px(10 * sc);
        LdrUI.Text(new Rect(0, 72 * sc, W, 20 * sc), "YAŞAM #" + (LiveDieRepeat.Deaths + 1) + "    ÖLÜM " + LiveDieRepeat.Deaths + "    RUH " + LdrIlerleme.ToplamRuh, pKucuk, new Color(0.8f, 0.7f, 0.96f), 1f);
        LdrUI.Text(new Rect(0, 94 * sc, W, 20 * sc), "Her ölüm ve topladığın her ruh yeni silahların kilidini açar.", pKucuk, new Color(0.6f, 0.55f, 0.78f), 1f);

        int n = liste.Count;
        float pad = 14f * sc;
        // en uzun silah adı en küçük piksel boyutunda (16) sığacak kadar geniş kart: dar ekranda sütun azalır, satır artar
        int enUzun = 0;
        for (int q = 0; q < n; q++) enUzun = Mathf.Max(enUzun, liste[q].ad.Length);
        float enAzKw = enUzun * 16f * LdrUI.CharW + 16f;
        int sutun = Mathf.Min(n, Mathf.Max(3, Mathf.FloorToInt(W / (214f * sc))));
        int sigan = Mathf.Max(2, Mathf.FloorToInt((W - 80f * sc) / (enAzKw + pad)));
        sutun = Mathf.Min(sutun, sigan);
        int satir = Mathf.CeilToInt(n / (float)sutun);
        float kw = Mathf.Min(Mathf.Max(200f * sc, enAzKw), (W - 80f * sc) / sutun - pad);
        float kh2 = Mathf.Max(172f * sc, Mathf.Min(210f * sc, (H - 290f * sc) / satir - pad));
        float gx = cx - (sutun * (kw + pad)) * 0.5f + pad * 0.5f;
        float gy = 132f * sc;
        var gorulen = LdrIlerleme.GorulenSilahlar();

        for (int i = 0; i < n; i++)
        {
            var e = liste[i];
            int sut = i % sutun, sat = i / sutun;
            Rect kr3 = new Rect(gx + sut * (kw + pad), gy + sat * (kh2 + pad), kw, kh2);
            bool acik = e.Acik;
            bool sec = i == secili && acik;
            bool ustte = kr3.Contains(Event.current.mousePosition);
            Color wc = e.renk;
            Color nadirRenk; string nadir = e.Nadirlik(out nadirRenk);
            Color altin = new Color(1f, 0.82f, 0.28f);
            Color sakin = new Color(0.72f, 0.64f, 0.92f);
            if (cizim)
            {
                Color vurgu = acik ? (sec ? altin : LdrUI.Purple) : new Color(0.3f, 0.25f, 0.38f);
                LdrUI.Panel(kr3, sec ? altin : (ustte && acik ? vurgu : new Color(vurgu.r * 0.6f, vurgu.g * 0.6f, vurgu.b * 0.6f)), sc, 0.95f, sec ? 1.6f : (ustte && acik ? 0.8f : 0f));
                if (sec) LdrUI.Light(new Vector2(kr3.center.x, kr3.y + kh2 * 0.3f), kw * (0.7f + 0.04f * Mathf.Sin(t * 3f)), new Color(1f, 0.75f, 0.2f, 0.16f));
                // nadirlik etiketi
                pEtiket.fontSize = LdrUI.Px(8 * sc);
                if (sec) pEtiket.alignment = TextAnchor.MiddleLeft;    // sağdaki etiketle çakışmasın
                LdrUI.Text(new Rect(kr3.x + 10 * sc, kr3.y + 9 * sc, kw - 20 * sc, 16 * sc), acik ? (sec ? "SEÇİLİ" : nadir) : "KİLİTLİ", pEtiket, acik ? (sec ? altin : sakin) : new Color(0.5f, 0.45f, 0.6f), 1f);
                pEtiket.alignment = TextAnchor.MiddleCenter;

                var tex = LdrResim.Tex(e.cizim);
                if (tex != null)
                {
                    float aw = kw - 30f * sc, ah = kh2 * 0.3f;
                    float oran = tex.width / (float)tex.height;
                    float w = Mathf.Min(aw, ah * oran), h = w / oran;
                    float sallan = sec ? Mathf.Sin(t * 2.2f) * 3f * sc : 0f;
                    GUI.color = acik ? Color.Lerp(Color.white, wc, 0.3f) : new Color(0.1f, 0.08f, 0.14f, 1f);
                    GUI.DrawTexture(new Rect(kr3.center.x - w * 0.5f, kr3.y + 30f * sc + (ah - h) * 0.5f + sallan, w, h), tex, ScaleMode.ScaleToFit, true);
                    GUI.color = Color.white;
                }
                pAd.fontSize = LdrUI.Px(10 * sc);
                { var gc = new GUIContent(e.ad); while (pAd.fontSize > 16 && pAd.CalcSize(gc).x > kw - 14 * sc) pAd.fontSize = LdrUI.StepDown(pAd.fontSize); }
                float adY = kr3.y + kh2 * 0.46f;
                LdrUI.Text(new Rect(kr3.x + 4, adY, kw - 8, 22 * sc), e.ad, pAd, acik ? (sec ? altin : LdrUI.Bone) : new Color(0.55f, 0.5f, 0.62f), 1f);
                pKucuk.fontSize = LdrUI.Px(8 * sc);
                if (acik)
                {
                    float hasar = e.mermi.hasar;
                    float tempo = e.Tempo;
                    pEtiket.alignment = TextAnchor.MiddleLeft;
                    float cubukKayma = Mathf.Max(15f * sc, pEtiket.fontSize * 1.3f);
                    float sx = kr3.x + 14 * sc, sw = kw - 28 * sc, sy = kr3.y + kh2 * 0.62f, rh = Mathf.Max(26f * sc, cubukKayma + 8f);
                    var etiket = new Color(0.82f, 0.76f, 0.96f);
                    LdrUI.Text(new Rect(sx, sy, sw, 12 * sc), "HASAR", pEtiket, etiket, 0f);
                    pEtiket.alignment = TextAnchor.MiddleRight;
                    LdrUI.Text(new Rect(sx, sy, sw, 12 * sc), hasar.ToString("0"), pEtiket, Color.white, 0f);
                    Cubuk(new Rect(sx, sy + cubukKayma, sw, 5 * sc), hasar / 16f, new Color(0.75f, 0.5f, 1f));
                    pEtiket.alignment = TextAnchor.MiddleLeft;
                    LdrUI.Text(new Rect(sx, sy + rh, sw, 12 * sc), "TEMPO", pEtiket, etiket, 0f);
                    pEtiket.alignment = TextAnchor.MiddleRight;
                    LdrUI.Text(new Rect(sx, sy + rh, sw, 12 * sc), tempo.ToString("0.0"), pEtiket, Color.white, 0f);
                    pEtiket.alignment = TextAnchor.MiddleCenter;
                    Cubuk(new Rect(sx, sy + rh + cubukKayma, sw, 5 * sc), tempo / 4.2f, new Color(0.55f, 0.6f, 0.85f));
                    if (!gorulen.Contains(e.ad) && e.gerekenOlum + e.gerekenRuh > 0)
                        LdrUI.Text(new Rect(kr3.xMax - 62 * sc, kr3.y + 8 * sc, 56 * sc, 14 * sc), "YENİ!", pEtiket, LdrUI.Bone, 1f);
                }
                else
                {
                    LdrUI.Text(new Rect(kr3.x + 8, kr3.y + kh2 * 0.64f, kw - 16, kh2 * 0.34f), e.Sart, pKucuk, new Color(1f, 0.55f, 0.4f), 1f);
                    // kilit
                    float lw = 18f * sc, lh = 14f * sc, lx = kr3.center.x - lw * 0.5f, ly = kr3.y + kh2 * 0.3f;
                    LdrUI.Frame(new Rect(lx + 3f * sc, ly - 10f * sc, lw - 6f * sc, 12f * sc), new Color(0.75f, 0.6f, 0.2f), 3f * sc);
                    LdrUI.Fill(new Rect(lx, ly, lw, lh), new Color(0.75f, 0.6f, 0.2f));
                }
            }
            if (acik && Tikla(kr3)) secili = i;
        }

        // KARAKTERİ DEĞİŞTİR (geri)
        {
            float gw = 310f * sc, gh = 46f * sc;
            Rect gb = new Rect(Mathf.Max(gx, 48f * sc), H - 34f * sc - 52f * sc * 0.5f - gh * 0.5f, gw, gh);
            bool ust2 = gb.Contains(Event.current.mousePosition);
            if (cizim)
            {
                LdrUI.Fill(gb, ust2 ? new Color(0.2f, 0.08f, 0.34f, 1f) : new Color(0.1f, 0.04f, 0.18f, 1f));
                LdrUI.Frame(gb, ust2 ? LdrUI.Purple : new Color(0.5f, 0.3f, 0.8f), 2f);
                pEtiket.fontSize = LdrUI.Px(9 * sc);
                LdrUI.Text(gb, "◄ KARAKTERİ DEĞİŞTİR", pEtiket, LdrUI.Bone, 1f);
            }
            if (Tikla(gb)) { Gecerli = Sayfa.Karakter; return; }
        }

        // YENİDEN DOĞ
        float bw = 300f * sc, bh = 52f * sc;
        Rect bb = new Rect(cx - bw * 0.5f, H - bh - 34f * sc, bw, bh);
        bool ust3 = bb.Contains(Event.current.mousePosition);
        if (cizim)
        {
            LdrUI.Light(bb.center, bw * 0.8f, new Color(0.9f, 0.15f, 0.25f, ust3 ? 0.4f : 0.2f + 0.06f * Mathf.Sin(t * 3f)));
            LdrUI.Fill(bb, ust3 ? new Color(0.5f, 0.07f, 0.15f, 1f) : new Color(0.28f, 0.04f, 0.1f, 1f));
            LdrUI.Frame(bb, new Color(1f, 0.3f, 0.4f), 2f);
            pBaslik.fontSize = LdrUI.Px(18 * sc);
            LdrUI.Text(bb, "YENİDEN DOĞ", pBaslik, Color.white, 1f);
        }
        var ev2 = Event.current;
        if (Tikla(bb) || (ev2.type == EventType.KeyDown && (ev2.keyCode == KeyCode.Return || ev2.keyCode == KeyCode.KeypadEnter)))
        {
            if (ev2.type == EventType.KeyDown) ev2.Use();
            var secilen = liste[Mathf.Clamp(secili, 0, liste.Count - 1)];
            if (!secilen.Acik) secilen = liste.Find(x => x.Acik);
            LdrIlerleme.SeciliSilah = secilen.ad;
            LdrIlerleme.SilahlariGorduIsaretle();
            OyunaGec();
        }
    }

    static void Cubuk(Rect r, float v, Color c)
    {
        LdrUI.Fill(r, new Color(0.1f, 0.06f, 0.16f, 1f));
        LdrUI.Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), c);
    }
}
