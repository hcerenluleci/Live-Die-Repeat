using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mini harita (M tuşu): sağ üstte yuvarlak "ritüel gözü". Oyuncunun çevresini gösterir; odalar kendi minimap karolarıyla (düzensiz kenarlı) çizilir;
/// bulunduğun oda mor, boss odası kırmızı (atıp tutan), temizlenen oda camgöbeği, görülmüş oda soluk mor, görülmemiş oda koyu kırmızıdır.
/// M tuşu: küçük harita -> BÜYÜK HARİTA (kattaki tüm odalar, görülmemişler soluk) -> kapalı. Altında "temiz oda sayısı" yazar.
/// </summary>
public class LdrHarita : MonoBehaviour
{
    [Header("Görünüm")]
    [SerializeField] float kutuBoyu = 224f;               // 720 piksellik ekran yüksekliğine göre
    [SerializeField] float yenilemeAraligi = 0.15f;

    const int N = 112;                                    // harita dokusu (piksel-sanat görünümü için düşük çözünürlük)

    struct OdaCizim { public float cx, cy, yx, yy, r; public int tur; }

    // tur: 0 görülmemiş, 1 görülmüş, 2 temiz, 3 şu an, 4 boss
    const int Gorulmemis = 0, Gorulmus = 1, Temiz = 2, SuAn = 3, Boss = 4;

    LdrOyun oyun;
    Texture2D harita, parlama;
    readonly Color32[] piksel = new Color32[N * N];
    readonly List<OdaCizim> liste = new List<OdaCizim>();
    float boyaSayac, olcek, ortaX, ortaY;
    bool acik = true;
    bool buyuk;                                           // M ile açılan tam harita
    float yar = 38f;                                      // şu an haritanın gösterdiği dünya yarıçapı
    GUIStyle etiket;

    void Awake()
    {
        oyun = GetComponent<LdrOyun>();
    }

    void OnDestroy()
    {
        if (harita != null) Destroy(harita);
        if (parlama != null) Destroy(parlama);
    }

    void Update()
    {
        if (LdrGiris.HaritaBasildi() && !LdrMenu.Acik && !LdrDuraklat.Acik)
        {
            if (!acik) { acik = true; buyuk = false; }
            else if (!buyuk) buyuk = true;
            else { buyuk = false; acik = false; }
            boyaSayac = 0f;
        }
    }

    static float Yumusat(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static float YuvarlakKutu(float px, float py, OdaCizim o)
    {
        float qx = Mathf.Abs(px - o.cx) - (o.yx - o.r);
        float qy = Mathf.Abs(py - o.cy) - (o.yy - o.r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - o.r;
    }

    void DokuHazirla()
    {
        if (harita == null)
        {
            harita = new Texture2D(N, N, TextureFormat.RGBA32, false);
            harita.filterMode = FilterMode.Point;
            harita.wrapMode = TextureWrapMode.Clamp;
            harita.hideFlags = HideFlags.HideAndDontSave;
        }
        if (parlama == null)
        {
            const int g = 64;
            parlama = new Texture2D(g, g, TextureFormat.RGBA32, false);
            parlama.filterMode = FilterMode.Bilinear;
            parlama.wrapMode = TextureWrapMode.Clamp;
            parlama.hideFlags = HideFlags.HideAndDontSave;
            var px = new Color32[g * g];
            for (int y = 0; y < g; y++)
                for (int x = 0; x < g; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(g * 0.5f, g * 0.5f)) / (g * 0.5f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    px[y * g + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            parlama.SetPixels32(px);
            parlama.Apply();
        }
    }

    const float Yaricap = 38f;                           // haritanın gösterdiği dünya yarıçapı (birim)
    readonly int[] kimlik = new int[N * N];               // piksel -> oda sırası (-1: boş)
    readonly int[] durum = new int[64];                   // oda sırası -> tur (-1: gösterme)

    int Kimlik(int x, int y) { return (x < 0 || y < 0 || x >= N || y >= N) ? -1 : kimlik[y * N + x]; }

    void Boya(LdrKat kat)
    {
        Vector2 oy = oyun.yasayan.transform.position;
        ortaX = oy.x; ortaY = oy.y;
        float R = N * 0.5f - 1f;
        yar = Yaricap;
        if (buyuk)
        {
            // tüm kat sığsın: katın merkezi ve köşegen yarıçapı
            Rect sn = kat.Sinir;
            ortaX = sn.center.x; ortaY = sn.center.y;
            yar = Mathf.Sqrt(sn.width * sn.width + sn.height * sn.height) * 0.5f + 3f;
        }
        olcek = R / yar;

        // hangi odalar gösterilir: girilmiş olanlar + onlara bağlı komşular; boss odası boss kapısı açılınca
        int sayi = Mathf.Min(kat.odalar.Count, durum.Length);
        for (int i = 0; i < sayi; i++) durum[i] = -1;
        for (int i = 0; i < sayi; i++)
        {
            var o = kat.odalar[i];
            if (!o.ziyaret) continue;
            durum[i] = o == kat.Gecerli ? SuAn : o.tur == LdrOdaTuru.Cikis ? Boss : (o.temiz && o.dalga != null) ? Temiz : Gorulmus;
        }
        for (int i = 0; i < sayi; i++)
        {
            var o = kat.odalar[i];
            if (!o.ziyaret) continue;
            int ebeveyn = o.yer.ebeveyn;
            if (ebeveyn >= 0 && ebeveyn < sayi && durum[ebeveyn] < 0) durum[ebeveyn] = kat.odalar[ebeveyn].tur == LdrOdaTuru.Cikis ? Boss : Gorulmemis;
            for (int j = 0; j < sayi; j++)
                if (kat.odalar[j].yer.ebeveyn == i && durum[j] < 0) durum[j] = kat.odalar[j].tur == LdrOdaTuru.Cikis ? Boss : Gorulmemis;
        }
        if (buyuk)
            for (int i = 0; i < sayi; i++)
                if (durum[i] < 0) durum[i] = kat.odalar[i].tur == LdrOdaTuru.Cikis ? Boss : Gorulmemis;
        if (kat.BossAcik && kat.CikisOdasi != null)
        {
            int ci = kat.odalar.IndexOf(kat.CikisOdasi);
            if (ci >= 0 && ci < sayi) durum[ci] = Boss;
        }

        // piksel -> oda (odanın kendi harita hücreleri; düzensiz kenarlar)
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                kimlik[y * N + x] = -1;
                float dx = x + 0.5f - N * 0.5f, dy = y + 0.5f - N * 0.5f;
                if (dx * dx + dy * dy > (R + 1f) * (R + 1f)) continue;
                var dunya = new Vector2(ortaX + dx / olcek, ortaY + dy / olcek);
                for (int i = 0; i < sayi; i++)
                {
                    if (durum[i] < 0) continue;
                    var o = kat.odalar[i];
                    if (o.sinir.Contains(dunya) && o.MinimapVar(dunya)) { kimlik[y * N + x] = i; break; }
                }
            }

        float t = Time.unscaledTime;
        float nabiz = 0.5f + 0.5f * Mathf.Sin(t * 3f);
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = x + 0.5f - N * 0.5f, dy = y + 0.5f - N * 0.5f;
                float rad = Mathf.Sqrt(dx * dx + dy * dy);
                if (rad > R + 1f) { piksel[y * N + x] = new Color32(0, 0, 0, 0); continue; }

                // zemin: koyu mor, hafif damar gürültüsü
                float n1 = Mathf.PerlinNoise(x * 0.09f + 11f, y * 0.09f + 3f);
                float n2 = Mathf.PerlinNoise(x * 0.28f, y * 0.28f + 7f);
                float damar = Mathf.Clamp01(1f - Mathf.Abs(n1 - 0.5f) * 9f);
                Color col = new Color(0.035f, 0.012f, 0.06f) + new Color(0.05f, 0.015f, 0.08f) * (0.5f * n2) + new Color(0.10f, 0.02f, 0.14f) * (0.35f * damar);

                // odalar
                int id = kimlik[y * N + x];
                int tr = -1; bool kenarPiksel = false; bool yakin = false;
                if (id >= 0)
                {
                    tr = durum[id];
                
                    kenarPiksel = Kimlik(x - 1, y) != id || Kimlik(x + 1, y) != id || Kimlik(x, y - 1) != id || Kimlik(x, y + 1) != id;
                }
                else
                {
                    yakin = Kimlik(x - 1, y) >= 0 || Kimlik(x + 1, y) >= 0 || Kimlik(x, y - 1) >= 0 || Kimlik(x, y + 1) >= 0
                         || Kimlik(x - 2, y) >= 0 || Kimlik(x + 2, y) >= 0 || Kimlik(x, y - 2) >= 0 || Kimlik(x, y + 2) >= 0;
                    if (yakin)
                    {
                        // dışa sızan hafif ışık: en yakın komşunun durumu
                        int kid = Mathf.Max(Mathf.Max(Kimlik(x - 1, y), Kimlik(x + 1, y)), Mathf.Max(Kimlik(x, y - 1), Kimlik(x, y + 1)));
                        if (kid < 0) kid = Mathf.Max(Mathf.Max(Kimlik(x - 2, y), Kimlik(x + 2, y)), Mathf.Max(Kimlik(x, y - 2), Kimlik(x, y + 2)));
                        tr = kid >= 0 ? durum[kid] : -1;
                    }
                }
                if (tr >= 0)
                {
                    Color temel, kenar;
                    switch (tr)
                    {
                        case SuAn: temel = new Color(0.42f, 0.14f, 0.70f); kenar = new Color(0.85f, 0.55f, 1f); break;
                        case Boss: temel = Color.Lerp(new Color(0.40f, 0.02f, 0.06f), new Color(0.65f, 0.05f, 0.10f), nabiz); kenar = new Color(1f, 0.30f, 0.30f); break;
                        case Temiz: temel = new Color(0.07f, 0.32f, 0.38f); kenar = new Color(0.35f, 0.90f, 0.95f); break;
                        case Gorulmus: temel = new Color(0.22f, 0.09f, 0.34f); kenar = new Color(0.60f, 0.35f, 0.85f); break;
                        default: temel = new Color(0.12f, 0.03f, 0.07f); kenar = new Color(0.40f, 0.10f, 0.18f); break;
                    }
                    if (id >= 0) col = kenarPiksel ? kenar : temel * (0.85f + 0.3f * n2);
                    else col += kenar * 0.22f;
                }

                // kenara doğru kararma
                float vinyet = Yumusat(R * 0.62f, R - 3f, rad);
                col *= 1f - 0.65f * vinyet;

                // dış halka: mor çerçeve + rün işaretleri
                float aci = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * 24f;
                float cizgi = Mathf.Abs(aci - Mathf.Round(aci));
                bool buyuk = ((int)Mathf.Round(aci) % 2) == 0;
                if (rad > R - 5f)
                {
                    float bant = Yumusat(R - 5f, R - 4f, rad) * (1f - Yumusat(R - 2.2f, R - 1.2f, rad));
                    Color halka = new Color(0.40f, 0.10f, 0.62f);
                    if (cizgi < (buyuk ? 0.22f : 0.12f) && rad < R - (buyuk ? 9f : 7f)) halka = new Color(0.9f, 0.6f, 1f);
                    col = Color.Lerp(col, halka, bant);
                    if (rad > R - 1.2f) col = Color.Lerp(col, new Color(0.01f, 0f, 0.02f), Yumusat(R - 1.2f, R, rad));
                }
                else if (rad > R - 9f && buyuk && cizgi < 0.22f)
                {
                    col = Color.Lerp(col, new Color(0.9f, 0.6f, 1f), 0.9f);
                }

                float alfa = 1f - Yumusat(R - 0.5f, R + 1f, rad);
                piksel[y * N + x] = new Color(Mathf.Clamp01(col.r), Mathf.Clamp01(col.g), Mathf.Clamp01(col.b), alfa * 0.96f);
            }
        }
        harita.SetPixels32(piksel);
        harita.Apply(false);
    }

    void OnGUI()
    {
        if (!acik || Event.current.type != EventType.Repaint) return;
        if (LdrMenu.Acik || LdrDuraklat.Acik || LiveDieRepeat.Dying || Time.timeScale < 0.001f || !string.IsNullOrEmpty(LdrHud.OrtaYazi)) return;
        if (oyun == null || oyun.kat == null || oyun.kat.odalar.Count == 0 || oyun.yasayan == null) return;

        DokuHazirla();
        boyaSayac -= Time.unscaledDeltaTime;
        if (boyaSayac <= 0f) { boyaSayac = yenilemeAraligi; Boya(oyun.kat); }

        GUI.depth = -5000;
        float sc = Screen.height / 720f;
        float boy = kutuBoyu * sc;
        if (buyuk) boy = Mathf.Min(Screen.height * 0.66f, Screen.width * 0.5f);      // alttaki silah paneline binmesin
        float ox = buyuk ? (Screen.width - boy) * 0.5f : Screen.width - boy - 18f * sc;
        float oy = buyuk ? 56f * sc : 112f * sc;

        var eski = GUI.color;
        float nabiz = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f);
        GUI.color = new Color(0.55f, 0.15f, 0.9f, 0.35f + 0.15f * nabiz);
        GUI.DrawTexture(new Rect(ox - 22f * sc, oy - 22f * sc, boy + 44f * sc, boy + 44f * sc), parlama);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(ox, oy, boy, boy), harita);

        // oyuncu: parlayan çekirdek + hale
        float k = boy / N;
        Vector2 p = oyun.yasayan.transform.position;
        float ppx = ox + boy * 0.5f + (p.x - ortaX) * olcek * k;
        float ppy = oy + boy * 0.5f - (p.y - ortaY) * olcek * k;
        float yanip = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
        GUI.color = new Color(1f, 0.85f, 1f, 0.55f * yanip);
        GUI.DrawTexture(new Rect(ppx - 14f * sc, ppy - 14f * sc, 28f * sc, 28f * sc), parlama);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(ppx - 2.5f * sc, ppy - 2.5f * sc, 5f * sc, 5f * sc), parlama);
        GUI.color = eski;

        if (etiket == null)
        {
            etiket = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter };
            etiket.normal.textColor = new Color(0.85f, 0.65f, 1f);
        }
        etiket.fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * sc));
        int top = 0, tem = 0;
        foreach (var od in oyun.kat.odalar)
            if (od.tur == LdrOdaTuru.Savas || od.tur == LdrOdaTuru.Cikis) { top++; if (od.temiz) tem++; }
        GUI.Label(new Rect(ox - 20f * sc, oy + boy + 2f * sc, boy + 40f * sc, 18f * sc), (buyuk ? "[M] KAPAT" : "[M] HARİTA") + "   -   TEMİZ ODA " + tem + " / " + top, etiket);
        if (buyuk) GUI.Label(new Rect(ox - 60f * sc, oy + boy + 20f * sc, boy + 120f * sc, 18f * sc), "MOR: BURADASIN   CAMGÖBEĞİ: TEMİZ   KIRMIZI PARLAYAN: BOSS   KOYU: GÖRÜLMEMİŞ", etiket);
    }
}
