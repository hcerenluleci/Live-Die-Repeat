using UnityEngine;

/// <summary>Kamera: oyuncuyu yumuşakça izler, fareye doğru hafifçe kayar, oda dışına taşmaz, vuruşlarda sarsılır.</summary>
[RequireComponent(typeof(Camera))]
public class LdrKamera : MonoBehaviour
{
    public static LdrKamera Ornek { get; private set; }

    public Transform hedef;
    public Rect sinir = new Rect(-100, -100, 200, 200);
    public float boyut = 7f;
    [Range(0f, 0.5f)] public float fareKaymasi = 0.15f;
    public float yumusaklik = 0.12f;

    Camera kam;
    Vector3 hiz, temel;
    float sarsinti;

    void Awake()
    {
        Ornek = this;
        kam = GetComponent<Camera>();
        kam.orthographic = true;
        kam.orthographicSize = boyut;
        temel = transform.position;
    }

    /// <summary>Kısa ekran sarsıntısı (0.1 hafif ... 0.3 güçlü).</summary>
    public static void Salla(float guc)
    {
        if (Ornek != null) Ornek.sarsinti = Mathf.Max(Ornek.sarsinti, guc);
    }

    /// <summary>Oda değişiminde kamerayı yumuşatmadan hemen yerine koyar.</summary>
    public void Atla()
    {
        if (hedef == null) return;
        if (kam == null) kam = GetComponent<Camera>();
        temel = HedefKonum();
        transform.position = temel;
        hiz = Vector3.zero;
    }

    Vector3 HedefKonum()
    {
        Vector2 odak = hedef.position;
        Vector2 fare = LdrGiris.FareDunya(kam);
        odak += Vector2.ClampMagnitude((fare - odak) * fareKaymasi, 1.6f);

        float yarimY = kam.orthographicSize, yarimX = yarimY * kam.aspect;
        odak.x = Sinirla(odak.x, sinir.xMin + yarimX, sinir.xMax - yarimX, sinir.center.x);
        odak.y = Sinirla(odak.y, sinir.yMin + yarimY, sinir.yMax - yarimY, sinir.center.y);

        return new Vector3(odak.x, odak.y, transform.position.z);
    }

    void LateUpdate()
    {
        if (hedef == null) return;
        temel = Vector3.SmoothDamp(temel, HedefKonum(), ref hiz, yumusaklik, 100f, Time.unscaledDeltaTime);
        Vector3 sars = Vector3.zero;
        if (sarsinti > 0.001f)
        {
            sars = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * sarsinti;
            sarsinti = Mathf.MoveTowards(sarsinti, 0f, Time.unscaledDeltaTime * 0.9f);
        }
        transform.position = temel + sars;
    }

    /// <summary>Görüş alanı odadan büyükse ortalar, değilse aralığa sıkıştırır.</summary>
    static float Sinirla(float v, float min, float max, float orta)
    {
        if (min > max) return orta;
        return Mathf.Clamp(v, min, max);
    }
}
