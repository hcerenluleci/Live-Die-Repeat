using UnityEngine;
using UnityEngine.Rendering.Universal;

// =====================================================================================
// ÇIKIŞ: boss odası temizlenince belirir; E ile bir alt bölüme inilir.
// Son bölümde 4 kaçış parçası tamamsa oyun kazanılır ("KAÇTIN"), değilse çıkış mühürlü kalır.
// =====================================================================================
/// <summary>
/// Çıkış geçidi. Çıkış odasındaki düşmanlar bitince belirir; E ile bir alt derinliğe inilir.
/// Son derinlikte (SON UYANIŞ) dört kaçış parçası toplanmışsa oyun biter, yoksa çıkış mühürlü kalır.
/// </summary>
public class LdrCikis : MonoBehaviour, ILdrEtkilesim
{
    static Sprite sprite;
    Transform donen;
    Light2D isik;

    public Vector2 Konum { get { return transform.position; } }
    public bool Hazir { get { return true; } }

    public string Yazi1
    {
        get { return LdrOyun.Derinlik >= LdrOyun.SonDerinlik ? "[E]  ÇIKIŞA GİR" : "[E]  AŞAĞI İN"; }
    }

    public string Yazi2
    {
        get
        {
            if (LdrOyun.Derinlik >= LdrOyun.SonDerinlik)
                return LdrParca.Sayi() >= LdrParca.Gerekli ? "KAÇIŞ ANAHTARI TAMAM" : "MÜHÜRLÜ  -  " + LdrParca.Sayi() + "/" + LdrParca.Gerekli + " PARÇA";
            return "SONRAKİ  -  " + LdrOyun.DerinlikAdi(LdrOyun.Derinlik + 1);
        }
    }

    public static LdrCikis Kur(Transform ebeveyn, Vector2 dunya)
    {
        var go = new GameObject("Cikis");
        go.transform.SetParent(ebeveyn, false);
        go.transform.position = new Vector3(dunya.x, dunya.y, 0f);
        var c = go.AddComponent<LdrCikis>();
        var sr = LdrGorsel.Ciz(go.transform, "Girdap", Girdap(), Vector3.zero, 4, LdrGorsel.Isiksiz());
        c.donen = sr.transform;

        var lg = new GameObject("Isik");
        lg.transform.SetParent(go.transform, false);
        c.isik = lg.AddComponent<Light2D>();
        c.isik.lightType = Light2D.LightType.Point;
        c.isik.color = new Color(0.62f, 0.28f, 1f);
        c.isik.intensity = 1.6f;
        c.isik.pointLightOuterRadius = 6f;
        c.isik.pointLightInnerRadius = 0.4f;

        LdrVfx.Motes(go.transform, new Color(0.8f, 0.5f, 1f), 12f, 1.0f, 0.5f, 0, 14);
        LdrVfx.Burst(go.transform.position, new Color(0.7f, 0.4f, 1f), Color.white, 30, 5f, 0.2f, 0.9f, 0f, 0, 40);
        return c;
    }

    void OnEnable() { LdrEtkilesim.Kaydet(this); }
    void OnDisable() { LdrEtkilesim.Sil(this); }

    void Update()
    {
        if (donen != null) donen.Rotate(0f, 0f, -45f * Time.deltaTime);
        if (isik != null) isik.intensity = 1.5f + 0.3f * Mathf.Sin(Time.time * 3f);
    }

    public void Etkilesim(LdrYasayan oyuncu)
    {
        if (LdrOyun.Ornek != null) LdrOyun.Ornek.Indi();
    }

    /// <summary>32x32 dönen girdap: koyu merkez, mor kollar ve parlak kenar.</summary>
    static Sprite Girdap()
    {
        if (sprite != null) return sprite;
        const int n = 32;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        t.hideFlags = HideFlags.HideAndDontSave;
        var px = new Color32[n * n];
        var ic = new Color(0.12f, 0.02f, 0.25f);
        var dis = new Color(0.7f, 0.35f, 1f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - 15.5f) / 16f, dy = (y - 15.5f) / 16f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 1f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                Color c;
                if (r < 0.3f) c = new Color(0.03f, 0f, 0.07f);
                else if (r > 0.9f) c = new Color(0.62f, 0.3f, 1f);
                else
                {
                    float ang = Mathf.Atan2(dy, dx);
                    float kol = 0.5f + 0.5f * Mathf.Sin(ang * 3f + r * 9f);
                    c = Color.Lerp(ic, dis, kol * (0.35f + r * 0.65f));
                }
                px[y * n + x] = c;
            }
        t.SetPixels32(px);
        t.Apply();
        sprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 12f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
