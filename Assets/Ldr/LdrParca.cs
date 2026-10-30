using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Kaçış parçaları. Dört derinliğin her birinde mühürlü bir sandık bir parça saklar.
/// Parçalar ölümde kaybolmaz (PlayerPrefs "LDR_ShardMask": her derinlik için bir bit); dördü toplanınca son çıkış açılır.
/// </summary>
public static class LdrParca
{
    public const int Gerekli = 4;
    public const string Anahtar = "LDR_ShardMask";

    static Texture2D doku;
    static Sprite sprite;

    public static int Maske { get { return PlayerPrefs.GetInt(Anahtar, 0); } }
    public static bool Var(int derinlik) { return derinlik >= 0 && derinlik < Gerekli && (Maske & (1 << derinlik)) != 0; }

    public static int Sayi()
    {
        int m = Maske, n = 0;
        for (int i = 0; i < Gerekli; i++) if ((m & (1 << i)) != 0) n++;
        return n;
    }

    public static void Ekle(int derinlik)
    {
        if (derinlik < 0 || derinlik >= Gerekli) return;
        PlayerPrefs.SetInt(Anahtar, Maske | (1 << derinlik));
        PlayerPrefs.Save();
    }

    public static void Sifirla()
    {
        PlayerPrefs.SetInt(Anahtar, 0);
        PlayerPrefs.Save();
    }

    /// <summary>16x16 elmas biçimli kristal, kodla çizilir.</summary>
    public static Texture2D Doku()
    {
        if (doku != null) return doku;
        const int n = 16;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        t.hideFlags = HideFlags.HideAndDontSave;
        var px = new Color32[n * n];
        var koyu = new Color(0.32f, 0.1f, 0.62f);
        var acik = new Color(0.85f, 0.65f, 1f);
        var kenar = new Color(0.16f, 0.05f, 0.3f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Abs(x - 7.5f) / 5.2f + Mathf.Abs(y - 7.5f) / 7.4f;
                if (d > 1f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                Color c;
                if (d > 0.82f) c = kenar;
                else c = Color.Lerp(koyu, acik, Mathf.Clamp01((y / 15f) * 0.6f + (1f - d) * 0.5f));
                if (x == 6 && y >= 9 && y <= 12) c = Color.white;
                px[y * n + x] = c;
            }
        t.SetPixels32(px);
        t.Apply();
        doku = t;
        return t;
    }

    public static Sprite Elmas()
    {
        if (sprite != null) return sprite;
        var t = Doku();
        sprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 14f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}

/// <summary>Sandıktan çıkan, yüzen ve yaklaşınca toplanan parça.</summary>
public class LdrParcaNesnesi : MonoBehaviour
{
    int derinlik;
    float zaman;
    Vector3 taban;
    SpriteRenderer sr;
    Light2D isik;
    bool alindi;

    public static LdrParcaNesnesi Olustur(Vector2 konum, int derinlik, Transform ebeveyn)
    {
        var go = new GameObject("KacisParcasi");
        if (ebeveyn != null) go.transform.SetParent(ebeveyn, false);
        go.transform.position = new Vector3(konum.x, konum.y, 0f);
        var p = go.AddComponent<LdrParcaNesnesi>();
        p.derinlik = derinlik;
        p.taban = go.transform.position;
        p.sr = LdrGorsel.Ciz(go.transform, "Elmas", LdrParca.Elmas(), Vector3.zero, 12, LdrGorsel.Isiksiz());

        var lg = new GameObject("Isik");
        lg.transform.SetParent(go.transform, false);
        p.isik = lg.AddComponent<Light2D>();
        p.isik.lightType = Light2D.LightType.Point;
        p.isik.color = new Color(0.75f, 0.35f, 1f);
        p.isik.intensity = 1.4f;
        p.isik.pointLightOuterRadius = 3.6f;
        p.isik.pointLightInnerRadius = 0.2f;

        LdrVfx.Motes(go.transform, new Color(0.85f, 0.55f, 1f), 9f, 0.55f, 0.4f, 0, 14);
        return p;
    }

    void Update()
    {
        if (alindi) return;
        zaman += Time.deltaTime;
        transform.position = taban + new Vector3(0f, Mathf.Sin(zaman * 3f) * 0.14f, 0f);
        float nabiz = 1f + 0.08f * Mathf.Sin(zaman * 6f);
        sr.transform.localScale = new Vector3(nabiz, nabiz, 1f);
        isik.intensity = 1.2f + 0.4f * Mathf.Sin(zaman * 5f);

        var o = LdrOyun.Ornek;
        if (zaman < 0.6f || o == null || o.yasayan == null || o.yasayan.Can.Olu) return;
        if (Vector2.Distance(o.yasayan.transform.position, transform.position) < 1.15f) Al();
    }

    void Al()
    {
        alindi = true;
        LdrParca.Ekle(derinlik);
        LdrVfx.Burst(transform.position, new Color(0.85f, 0.55f, 1f), Color.white, 30, 4.5f, 0.2f, 0.9f, 0f, 0, 40);
        LdrHud.Flas = Mathf.Max(LdrHud.Flas, 0.35f);
        int n = LdrParca.Sayi();
        LdrHud.Duyur("KAÇIŞ PARÇASI BULUNDU!  " + n + "/" + LdrParca.Gerekli, new Color(0.85f, 0.65f, 1f));
        if (n >= LdrParca.Gerekli) LdrHud.Duyur("ÇIKIŞ ANAHTARI TAMAM  -  SON UYANIŞA İN", new Color(1f, 0.9f, 0.5f));
        Destroy(gameObject);
    }
}
