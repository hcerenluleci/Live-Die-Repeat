using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum LdrEsyaTuru { Boguruten, Uzum, Kalkan, Cephane, Ates, Kasa }

// =====================================================================================
// YARDIM EŞYALARI: mor böğürtlen (12 sn güç), can üzümü (+1 kalp), gölge kalkanı (8 sn dokunulmazlık),
// cephane (8 sn sınırsız), silah kasası (20 sn pompalı), ateş topu (F, en çok 6). Temizlenen oda ve kırılan fıçılardan düşer.
// =====================================================================================
/// <summary>
/// Yardım eşyaları:
///   Mor böğürtlen: 12 sn güç (hasar x2, daha sık ateş)      Can üzümü: +1 can
///   Gölge kalkanı: 8 sn dokunulmazlık                          Cephane: şarjör dolar, 8 sn sınırsız
///   Ateş topu: [F] ile fırlat, patlar (en çok 6)                Silah kasası: 20 sn pompalı
/// Temizlenen odadan (%65) ve kırılan fıçıdan (%50) düşer. Çizimler 16x16, kodla üretilir.
/// </summary>
public class LdrEsya : MonoBehaviour
{
    static readonly Dictionary<LdrEsyaTuru, Sprite> cizimler = new Dictionary<LdrEsyaTuru, Sprite>();

    public LdrEsyaTuru tur;
    Vector3 taban;
    float zaman;

    public static Color Renk(LdrEsyaTuru t)
    {
        switch (t)
        {
            case LdrEsyaTuru.Boguruten: return new Color(0.75f, 0.3f, 1f);
            case LdrEsyaTuru.Uzum: return new Color(1f, 0.25f, 0.3f);
            case LdrEsyaTuru.Kalkan: return new Color(0.3f, 0.9f, 1f);
            case LdrEsyaTuru.Cephane: return new Color(1f, 0.6f, 0.2f);
            case LdrEsyaTuru.Ates: return new Color(1f, 0.45f, 0.12f);
            default: return new Color(1f, 0.8f, 0.3f);
        }
    }

    public static LdrEsyaTuru Sec()
    {
        var o = LdrOyun.Ornek;
        bool yarali = o != null && o.yasayan != null && o.yasayan.Can.Simdiki < o.yasayan.Can.azami;
        // mermi azsa ya da bittiyse cephane düşme şansı artar
        float azlik = 0.18f;
        if (o != null && o.yasayan != null)
        {
            if (o.yasayan.Silah.Yedekte) azlik = 0.9f;
            else if (o.yasayan.Silah.MermiOrani < 0.2f) azlik = 0.5f;
            else if (o.yasayan.Silah.MermiOrani < 0.45f) azlik = 0.32f;
        }
        float wAtes = 0.11f, wCephane = azlik, wBogurten = 0.22f, wUzum = yarali ? 0.2f : 0f, wKalkan = 0.12f, wKasa = 0.08f;
        float r = Random.value * (wAtes + wCephane + wBogurten + wUzum + wKalkan + wKasa);
        if ((r -= wAtes) < 0f) return LdrEsyaTuru.Ates;
        if ((r -= wCephane) < 0f) return LdrEsyaTuru.Cephane;
        if ((r -= wBogurten) < 0f) return LdrEsyaTuru.Boguruten;
        if ((r -= wUzum) < 0f) return LdrEsyaTuru.Uzum;
        if ((r -= wKalkan) < 0f) return LdrEsyaTuru.Kalkan;
        return LdrEsyaTuru.Kasa;
    }

    /// <summary>Konuma rastgele bir eşya bırakır.</summary>
    public static LdrEsya Dusur(Vector2 konum, Transform ebeveyn) { return Olustur(Sec(), konum, ebeveyn); }

    public static LdrEsya Olustur(LdrEsyaTuru tur, Vector2 konum, Transform ebeveyn)
    {
        var go = new GameObject("Esya_" + tur);
        if (ebeveyn != null) go.transform.SetParent(ebeveyn, false);
        go.transform.position = new Vector3(konum.x, konum.y, 0f);
        go.transform.localScale = Vector3.one * 1.25f;
        var e = go.AddComponent<LdrEsya>();
        e.tur = tur;
        e.taban = go.transform.position;
        e.zaman = Random.value * 6f;
        LdrGorsel.Ciz(go.transform, "Cizim", Cizim(tur), Vector3.zero, 6, LdrGorsel.Isiksiz());

        var lg = new GameObject("Isik");
        lg.transform.SetParent(go.transform, false);
        var l = lg.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = Renk(tur);
        l.intensity = 0.9f;
        l.pointLightOuterRadius = 2.6f;
        l.pointLightInnerRadius = 0.2f;
        l.falloffIntensity = 0.5f;
        return e;
    }

    void Update()
    {
        zaman += Time.deltaTime;
        transform.position = taban + new Vector3(0f, Mathf.Sin(zaman * 3f) * 0.08f, 0f);
        var o = LdrOyun.Ornek;
        if (o == null || o.yasayan == null || o.yasayan.Can.Olu) return;
        if (Vector2.Distance((Vector2)o.yasayan.transform.position + Vector2.up * 0.2f, taban) < 1.1f)
        {
            Topla(o.yasayan);
            Destroy(gameObject);
        }
    }

    void Topla(LdrYasayan y)
    {
        var b = y.Buflar;
        Color c = Renk(tur);
        LdrSes.Cal("sfx_pickup", 0.7f);
        LdrVfx.Burst(transform.position, c, Color.white, 14, 3f, 0.12f, 0.6f, 0f, 0, 14);
        switch (tur)
        {
            case LdrEsyaTuru.Boguruten:
                b.Guc = 12f;
                LdrHud.Duyur("MOR BÖĞÜRTLEN!  12 sn GÜÇ: hasar x2, daha hızlı atış", c);
                break;
            case LdrEsyaTuru.Uzum:
                y.Can.Iyiles(1);
                LdrHud.Duyur("CAN ÜZÜMÜ!  +%20 CAN", c);
                break;
            case LdrEsyaTuru.Kalkan:
                b.Kalkan = 8f;
                LdrHud.Duyur("GÖLGE KALKANI!  8 sn dokunulmazsın", c);
                break;
            case LdrEsyaTuru.Cephane:
                y.Silah.HemenDoldur();
                b.Sinirsiz = 8f;
                LdrHud.Duyur("CEPHANE!  şarjör doldu, 8 sn sınırsız", c);
                break;
            case LdrEsyaTuru.Ates:
                b.AtesTopu = Mathf.Min(6, b.AtesTopu + 3);
                LdrHud.Duyur("ATEŞ TOPU!  [F] ile fırlat  (" + b.AtesTopu + ")", c);
                break;
            default:
                b.Pompali = 20f;
                y.Silah.PompaliAyarla(true);
                LdrHud.Duyur("SİLAH KASASI!  20 sn: POMPALI", c);
                break;
        }
    }

    // ---------------------------------------------------------------- çizimler (16x16, bize ait)

    static void Top(Color32[] px, int w, float cx, float cy, float r, Color baz)
    {
        for (int y = 0; y < w; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > r) continue;
                float isik = Mathf.Clamp01(0.55f + 0.5f * (-dx * 0.6f + dy * 0.8f) / r);
                Color c = baz * (0.55f + 0.7f * isik);
                if (d > r - 1f) c = baz * 0.35f;
                if (dx < -r * 0.2f && dy > r * 0.25f && dx > -r * 0.7f && dy < r * 0.75f) c = Color.Lerp(c, Color.white, 0.55f);
                c.a = 1f;
                px[y * w + x] = c;
            }
    }

    // cephane kutusu (zeytin yeşili, pirinç mermili) ve silah kasası (koyu sert çanta, altın köşe, kırmızı kilit): 16x16 elle çizim
    const string AtesSayfaPng = "iVBORw0KGgoAAAANSUhEUgAAAEgAAAANCAYAAAAKeCiYAAABGUlEQVR4nGNgGAV4ASMlmi8ZCf1HF9M7944iMweb+SzYNP6/FPWfUW8ZXodcMhL6rxvFgynOwPCfGE8QsoNS8wnZQaz5JMXGWV1IiLOyMjDADQ+1QShYfYSBgYGB4fKyLxTFNIrjB9h8oiw5qyv038ibHSEgwopquHQtA8PTZhRLGEseETQb3dxzW38yMDAwMBjFozmeDPNhkWl8GRGQcPtIcD+KZuPL7xj/V0hi5EuGAF0483LWGUjqQTYcBpAswRXLKIGCZC61zIe7H2b2hsso/iDVfCaYnNFSDwasgTNEwP9LUf9hqYaagG5ZDFuBOaSyGDFgtJAm1xIkQKnjB5v5FLUnqNGQI9RWocR8YtpalJg/CkYBYQAASyoJLYnjwSgAAAAASUVORK5CYII=";
    const string AtesIkonPng = "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAqUlEQVR4nOWSoQ7CMBRFTwkSNUf4hk4Mg5jHzszwJRh+ZwaDwaCnK1bDV1TNoIpoVuiytppwkyf6Xu/pTVv4eYnUcKgKK08bv9bdSKlM4FllzW3te9+wJGCoCgt8zG0dgLKAUhnhT9tdXEWUTjDXQor1vGHPWwvuwiQ9cHTGa4/uxjwAgEYiG6B/etDSC8QBADeNur/YayNQJrotAEzxxeGR/B9RTYA/0xuizjex1z0RzgAAAABJRU5ErkJggg==";
    static Sprite[] atesKareleri;

    /// <summary>Uçan ateş topu: 3 karelik elle çizilmiş top + alev kuyruğu (sağa bakar; baş %73 noktasında).</summary>
    public static Sprite[] AtesKareleri()
    {
        if (atesKareleri != null && atesKareleri[0] != null) return atesKareleri;
        var t = new Texture2D(72, 13, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        t.LoadImage(System.Convert.FromBase64String(AtesSayfaPng));
        t.filterMode = FilterMode.Point;
        atesKareleri = new Sprite[3];
        for (int i = 0; i < 3; i++)
        {
            atesKareleri[i] = Sprite.Create(t, new Rect(i * 24, 0, 24, 13), new Vector2(0.73f, 0.5f), 16f);
            atesKareleri[i].hideFlags = HideFlags.HideAndDontSave;
        }
        return atesKareleri;
    }

    const string CephanePng = "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAt0lEQVR4nGNgGHTgwbSo/4TYyIAJXeDFjUcMJ/Js/uNj4zWAVIBigISA0H+NKBGGb3JvGBgYGBhwsanqAkZk20nR+OLDO0YGBgYGFmTBiCo7ojSvaDsEZ8MNqJuQxrDj0AFSHMHAwIDmBRU7KQYGBgaGNWUyDAY+pxgubDGD0wwMDAwhXU8YGBgYGO4cegb3As5AhGmCGUKUC8yC1Ihy9ql1t7AH4rNH74gyABmgGPDkDGZCoTkAAFbnTHk2JyRUAAAAAElFTkSuQmCC";
    const string KasaPng = "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAA1klEQVR4nGNgoBAwYhOUEBD6X9fWhyLWVFXE8OLDO6zqsRpAjBjcBbgkCYEXH94xssA4V9bpMZSscCNKY0/ELgadoEsMDAwMDCzoEn4NIng1b2p4g8KHe0FJz4ko22Hg3qV9DC8+vGNkwqfoqJUUw4wv9/AaBPfCk0e3MCQvQzVjk8MwQEZODVMWagA2uXsfnqAaAANzZk2Cs1PS8nCKwQA8DDY1vGHY1PAGrgBZIbIYTB0MwGPhyjo9BqusX5jewAKOTWNj0Am6xPDiwztGilMiOfqoCwAYC1EycAkv/AAAAABJRU5ErkJggg==";

    static Sprite Hazir(LdrEsyaTuru k, string png)
    {
        var t = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        t.LoadImage(System.Convert.FromBase64String(png));
        t.filterMode = FilterMode.Point;
        var s = Sprite.Create(t, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        s.hideFlags = HideFlags.HideAndDontSave;
        cizimler[k] = s;
        return s;
    }

    public static Sprite Cizim(LdrEsyaTuru k)
    {
        Sprite s;
        if (cizimler.TryGetValue(k, out s) && s != null) return s;
        const int W = 16;
        var t = new Texture2D(W, W, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; t.hideFlags = HideFlags.HideAndDontSave;
        var px = new Color32[W * W];
        switch (k)
        {
            case LdrEsyaTuru.Boguruten:
            {
                Color p = new Color(0.52f, 0.14f, 0.72f);
                Top(px, W, 5.5f, 5.5f, 3.4f, p); Top(px, W, 10.5f, 5.5f, 3.4f, p);
                Top(px, W, 8f, 9f, 3.6f, p * 1.1f); Top(px, W, 4.2f, 9.6f, 3f, p * 0.95f); Top(px, W, 11.8f, 9.6f, 3f, p * 0.95f);
                Top(px, W, 8f, 13f, 2.0f, new Color(0.25f, 0.65f, 0.25f));
                break;
            }
            case LdrEsyaTuru.Uzum:
            {
                Color r = new Color(0.85f, 0.1f, 0.18f);
                Top(px, W, 4.5f, 10f, 3f, r); Top(px, W, 8f, 10.5f, 3f, r * 1.05f); Top(px, W, 11.5f, 10f, 3f, r);
                Top(px, W, 6.2f, 6.5f, 3f, r * 0.95f); Top(px, W, 9.8f, 6.5f, 3f, r * 0.95f);
                Top(px, W, 8f, 3.2f, 2.8f, r * 0.9f);
                Top(px, W, 8f, 14.2f, 1.6f, new Color(0.3f, 0.55f, 0.2f));
                break;
            }
            case LdrEsyaTuru.Kalkan:
            {
                for (int y = 0; y < W; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float dx = x + 0.5f - 8f, dy = y + 0.5f - 8f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d >= 7f) continue;
                        bool halka = Mathf.Abs(d - 5.6f) < 1f;
                        Color c = halka ? new Color(0.5f, 1f, 1f) : Color.Lerp(new Color(0.05f, 0.25f, 0.4f), new Color(0.2f, 0.7f, 0.9f), 1f - d / 7f);
                        if (Mathf.Abs(dx) + Mathf.Abs(dy) < 2.6f) c = Color.white;
                        c.a = 1f; px[y * W + x] = c;
                    }
                break;
            }
            case LdrEsyaTuru.Ates:
                return Hazir(k, AtesIkonPng);
            case LdrEsyaTuru.Cephane:
                return Hazir(k, CephanePng);
            default:
                return Hazir(k, KasaPng);
        }
        t.SetPixels32(px);
        t.Apply();
        s = Sprite.Create(t, new Rect(0, 0, W, W), new Vector2(0.5f, 0.5f), 16f);
        s.hideFlags = HideFlags.HideAndDontSave;
        cizimler[k] = s;
        return s;
    }
}

/// <summary>
/// Oyuncunun geçici güçleri (böğürtlen, kalkan, sınırsız mermi, pompalı) ve ateş topu hakkı. Ateş topu [F] ile fırlatılır.
/// </summary>
public class LdrBuflar : MonoBehaviour
{
    public float Guc, Kalkan, Sinirsiz, Pompali;
    public int AtesTopu;

    LdrYasayan y;
    Transform halka;
    SpriteRenderer halkaSr;
    static Sprite halkaSprite;

    // ateş topları
    class Top { public Transform tr; public SpriteRenderer sr; public Vector2 yon; public float omur; public Light2D isik; public float kare, iz; }
    class Patlama { public Transform tr; public SpriteRenderer sr; public float t; }
    readonly List<Top> toplar = new List<Top>();
    readonly List<Patlama> patlamalar = new List<Patlama>();

    void Awake() { y = GetComponent<LdrYasayan>(); }

    public void Sifirla()
    {
        Guc = Kalkan = Sinirsiz = Pompali = 0f;
        AtesTopu = 0;
        if (y != null) { y.Silah.hasarCarpani = 1f; y.Silah.hizCarpani = 1f; y.Silah.PompaliAyarla(false); }
        if (halka != null) halka.gameObject.SetActive(false);
    }

    /// <summary>HUD için: etkin güçlerin (ad, kalan, toplam, renk) listesi.</summary>
    public void Liste(List<KeyValuePair<string, float>> hedef)
    {
        hedef.Clear();
        if (Guc > 0f) hedef.Add(new KeyValuePair<string, float>("GÜÇ", Guc));
        if (Kalkan > 0f) hedef.Add(new KeyValuePair<string, float>("KALKAN", Kalkan));
        if (Sinirsiz > 0f) hedef.Add(new KeyValuePair<string, float>("SINIRSIZ", Sinirsiz));
        if (Pompali > 0f) hedef.Add(new KeyValuePair<string, float>("POMPALI", Pompali));
    }

    void Update()
    {
        if (y == null) return;
        float dt = Time.deltaTime;
        if (Guc > 0f) Guc -= dt;
        if (Kalkan > 0f) Kalkan -= dt;
        if (Sinirsiz > 0f) { Sinirsiz -= dt; y.Silah.HemenDoldur(); }
        if (Pompali > 0f) { Pompali -= dt; if (Pompali <= 0f) y.Silah.PompaliAyarla(false); }

        y.Silah.hasarCarpani = Guc > 0f ? 2f : 1f;
        y.Silah.hizCarpani = Guc > 0f ? 0.6f : 1f;

        // kalkan halkası
        if (Kalkan > 0f)
        {
            if (halka == null)
            {
                halkaSr = LdrGorsel.Ciz(transform, "KalkanHalka", HalkaSprite(), Vector3.zero, 14, LdrGorsel.Isiksiz());
                halka = halkaSr.transform;
            }
            halka.gameObject.SetActive(true);
            float k = 0.9f + 0.1f * Mathf.Sin(Time.time * 8f);
            halka.localScale = new Vector3(1.5f * k, 1.5f * k, 1f);
            halkaSr.color = new Color(0.4f, 0.95f, 1f, Kalkan < 2f ? (Mathf.Sin(Time.time * 20f) > 0 ? 0.9f : 0.2f) : 0.85f);
        }
        else if (halka != null && halka.gameObject.activeSelf) halka.gameObject.SetActive(false);

        AtesTopuGuncelle(dt);
    }

    // ---------------------------------------------------------------- ateş topu

    void AtesTopuGuncelle(float dt)
    {
        if (!y.Donuk && !y.Can.Olu && AtesTopu > 0 && LdrGiris.AtesTopuBasildi())
        {
            var kam = Camera.main;
            Vector2 o = (Vector2)transform.position + Vector2.up * 0.3f;
            Vector2 yon = y.BakisYonu;
            AtesTopu--;
            var g = new GameObject("AtesTopu");
            g.transform.position = o;
            // uçan ateş topu: takımın meşale alevi çizimi (3 kare), hareket yönünün tersine kuyruk bırakan kor gibi; dönmez
            var sr = LdrGorsel.Ciz(g.transform, "Cizim", LdrEsya.AtesKareleri()[0], Vector3.zero, 15, LdrGorsel.Isiksiz());
            g.transform.localScale = Vector3.one * 1.1f;
            g.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(yon.y, yon.x) * Mathf.Rad2Deg);
            var lg = new GameObject("Isik");
            lg.transform.SetParent(g.transform, false);
            var l = lg.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point; l.color = new Color(1f, 0.5f, 0.15f); l.intensity = 1.6f; l.pointLightOuterRadius = 5f;
            toplar.Add(new Top { tr = g.transform, sr = sr, yon = yon, omur = 0.8f, isik = l });
        }

        for (int i = toplar.Count - 1; i >= 0; i--)
        {
            var t = toplar[i];
            if (t.tr == null) { toplar.RemoveAt(i); continue; }
            t.omur -= dt;
            Vector2 p = t.tr.position;
            float adim = 12f * dt;
            bool vurdu = t.omur <= 0f;
            if (!vurdu)
            {
                var h = Physics2D.CircleCastAll(p, 0.35f, t.yon, adim);
                for (int c = 0; c < h.Length; c++)
                {
                    var col = h[c].collider;
                    if (col == null || col.isTrigger) continue;
                    var can = col.GetComponentInParent<LdrCan>();
                    if (can != null && can.takim == 0) continue;
                    vurdu = true; break;
                }
            }
            if (vurdu) { Patlat(p); Destroy(t.tr.gameObject); toplar.RemoveAt(i); continue; }
            t.tr.position = p + t.yon * adim;
            t.kare += dt * 14f;
            t.sr.sprite = LdrEsya.AtesKareleri()[(int)t.kare % 3];
            t.iz -= dt;
            if (t.iz <= 0f)
            {
                t.iz = 0.04f;
                LdrVfx.Burst(t.tr.position, new Color(1f, 0.6f, 0.15f), new Color(0.9f, 0.2f, 0.05f), 2, 0.8f, 0.1f, 0.35f, 0f, 0, 14);
            }
            t.isik.intensity = 1.2f + 0.5f * Mathf.PerlinNoise(Time.time * 20f, i);
        }

        for (int i = patlamalar.Count - 1; i >= 0; i--)
        {
            var b = patlamalar[i];
            if (b.tr == null) { patlamalar.RemoveAt(i); continue; }
            b.t += dt;
            float p = b.t / 0.45f;
            if (p >= 1f) { Destroy(b.tr.gameObject); patlamalar.RemoveAt(i); continue; }
            b.tr.localScale = Vector3.one * Mathf.Lerp(0.8f, 5.6f, 1f - (1f - p) * (1f - p));
            b.sr.color = new Color(1f, 0.55f - 0.3f * p, 0.15f, 1f - p);
        }
    }

    void Patlat(Vector2 p)
    {
        var cs = Physics2D.OverlapCircleAll(p, 2.8f);
        var yapildi = new HashSet<LdrCan>();
        for (int i = 0; i < cs.Length; i++)
        {
            var can = cs[i].GetComponentInParent<LdrCan>();
            if (can == null || can.takim == 0 || !yapildi.Add(can)) continue;
            can.Hasar(5, p);
        }
        var g = new GameObject("AtesPatlamasi");
        g.transform.position = p;
        var sr = LdrGorsel.Ciz(g.transform, "Halka", HalkaSprite(), Vector3.zero, 15, LdrGorsel.Isiksiz());
        var l = g.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point; l.color = new Color(1f, 0.5f, 0.12f); l.intensity = 2.8f; l.pointLightOuterRadius = 7f;
        patlamalar.Add(new Patlama { tr = g.transform, sr = sr });
        Destroy(g, 0.6f);
        LdrVfx.Burst(p, new Color(1f, 0.7f, 0.2f), new Color(0.9f, 0.2f, 0.05f), 30, 6f, 0.2f, 0.7f, 0.3f, 0, 14);
        LdrKamera.Salla(0.12f);
    }

    static Sprite HalkaSprite()
    {
        if (halkaSprite != null) return halkaSprite;
        const int n = 32;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * n + x] = (d < 15f && d > 12.5f) ? new Color32(255, 255, 255, 255) : (d <= 12.5f && d > 10f ? new Color32(255, 255, 255, 70) : new Color32(0, 0, 0, 0));
            }
        t.SetPixels32(px); t.Apply();
        halkaSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 16f);
        halkaSprite.hideFlags = HideFlags.HideAndDontSave;
        return halkaSprite;
    }
}
