using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fener ve ortam ışığı. Sahneyi karartır; oyuncuya fare yönünü gösteren titrek bir fener, çevresinde bir hale
/// ve süzülen renkli "lav lambası" ışıkları verir. B tuşu üç ışık seviyesi arasında gezer (seçim hatırlanır).
/// Fener menzili her ölümde biraz büyür (hafıza seviyesi): ölüm, ilerlemenin kendisidir.
/// </summary>
public class LdrFener : MonoBehaviour
{
    const string SeviyeAnahtari = "LDR_Light2";

    static readonly string[] SeviyeAdlari = { "Aydınlık", "Orta", "Karanlık" };
    static readonly float[] OrtamIsigi = { 1.15f, 0.90f, 0.55f };
    static readonly float[] FenerMenzili = { 18f, 15f, 12f };

    [Header("Fener")]
    [SerializeField] Color renk = new Color(0.80f, 0.86f, 1f);
    [SerializeField] float siddet = 0.85f;
    [SerializeField] float hafizaBasinaMenzil = 0.5f;
    [SerializeField, Range(10f, 180f)] float acisi = 100f;

    [Header("Oyuncunun etrafındaki hale")]
    [SerializeField] float haleYaricapi = 5f;
    [SerializeField] float haleSiddeti = 0.75f;

    LdrOyun oyun;
    Light2D fener, hale;
    Light2D[] lambalar;
    Camera kamera;
    int seviye;
    float etiketBitis;
    GUIStyle etiketStili;

    void Start()
    {
        oyun = GetComponent<LdrOyun>();
        seviye = Mathf.Clamp(PlayerPrefs.GetInt(SeviyeAnahtari, 0), 0, 2);
        etiketBitis = Time.unscaledTime + 6f;                 // başta ipucunu göster

        fener = IsikYap("Fener");
        fener.color = renk;
        fener.intensity = siddet;
        fener.pointLightInnerRadius = 0.5f;
        fener.pointLightInnerAngle = acisi * 0.4f;
        fener.pointLightOuterAngle = acisi;
        fener.falloffIntensity = 0.35f;

        hale = IsikYap("Hale");
        hale.color = renk;
        hale.intensity = haleSiddeti;
        hale.pointLightInnerRadius = 0.2f;
        hale.pointLightOuterRadius = haleYaricapi;
        hale.pointLightInnerAngle = 360f;
        hale.pointLightOuterAngle = 360f;
        hale.falloffIntensity = 0.45f;

        Color[] renkler =
        {
            new Color(0.62f, 0.25f, 1f), new Color(0.15f, 0.85f, 1f), new Color(1f, 0.18f, 0.35f),
            new Color(0.62f, 0.25f, 1f), new Color(0.15f, 0.85f, 1f),
        };
        lambalar = new Light2D[renkler.Length];
        for (int i = 0; i < renkler.Length; i++)
        {
            var l = IsikYap("LavLambasi" + i);
            l.color = renkler[i];
            l.intensity = 0.6f;
            l.pointLightInnerRadius = 0f;
            l.pointLightOuterRadius = 9f;
            l.falloffIntensity = 0.6f;
            lambalar[i] = l;
        }
        OrtamiUygula();
    }

    Light2D IsikYap(string ad)
    {
        var go = new GameObject(ad);
        go.transform.SetParent(transform, false);
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        return l;
    }

    void OrtamiUygula()
    {
        foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) l.intensity = OrtamIsigi[seviye];
    }

    void SeviyeDegistir()
    {
        seviye = (seviye + 1) % 3;
        PlayerPrefs.SetInt(SeviyeAnahtari, seviye);
        PlayerPrefs.Save();
        OrtamiUygula();
        etiketBitis = Time.unscaledTime + 3f;
    }

    void Update()
    {
        if (LdrGiris.IsikBasildi() && !LdrMenu.Acik) SeviyeDegistir();
        if (oyun == null || oyun.yasayan == null) return;

        Vector3 p = oyun.yasayan.transform.position;
        for (int i = 0; i < lambalar.Length; i++)
        {
            float tt = Time.time * 0.12f + i * 7.3f;
            Vector3 kayma = new Vector3((Mathf.PerlinNoise(tt, i) - 0.5f) * 22f, (Mathf.PerlinNoise(i + 40f, tt) - 0.5f) * 16f, 0f);
            lambalar[i].transform.position = p + kayma;
            lambalar[i].intensity = 0.45f + 0.3f * Mathf.PerlinNoise(tt * 2.5f, i + 90f);
        }
        fener.transform.position = p;
        hale.transform.position = p;

        // fare yönüne dön
        if (kamera == null) kamera = Camera.main;
        if (kamera != null)
        {
            Vector2 yon = LdrGiris.FareDunya(kamera) - (Vector2)p;
            if (yon.sqrMagnitude > 0.0001f)
                fener.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(yon.y, yon.x) * Mathf.Rad2Deg - 90f);
        }

        // hafıza seviyesine göre menzil
        int hafiza = LiveDieRepeat.Instance != null ? LiveDieRepeat.MemoryLevel : 0;
        fener.pointLightOuterRadius = FenerMenzili[seviye] + hafizaBasinaMenzil * hafiza;

        // hafif titreme; seyrek ve kısa bir zayıflama (tamamen sönmez)
        float titre = 0.92f + 0.16f * Mathf.PerlinNoise(Time.time * 9f, 0f);
        if (Mathf.PerlinNoise(Time.time * 0.7f, 5f) > 0.965f) titre *= 0.6f;
        fener.intensity = siddet * titre;
    }

    void OnGUI()
    {
        if (Time.unscaledTime > etiketBitis || LdrMenu.Acik || LdrDuraklat.Acik) return;
        float sc = Screen.height / 720f;
        if (etiketStili == null)
        {
            etiketStili = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            etiketStili.normal.textColor = new Color(0.95f, 0.85f, 0.6f);
        }
        etiketStili.fontSize = Mathf.Max(12, Mathf.RoundToInt(18f * sc));
        GUI.depth = -50;
        GUI.Label(new Rect(Screen.width - 330f * sc, Screen.height - 40f * sc, 320f * sc, 30f * sc), "[B] Işık: " + SeviyeAdlari[seviye], etiketStili);
    }
}
