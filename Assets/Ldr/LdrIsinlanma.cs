using UnityEngine;
using UnityEngine.Rendering.Universal;

// =====================================================================================
// YETENEK: T ile başlangıç odasına ışınlanma. 1,4 sn şarj, 30 sn bekleme; başlangıca 4 birimden yakınsan çalışmaz.
// =====================================================================================
/// <summary>
/// T ile başlangıç noktasına ışınlanma (bizim özgün tasarım). 1,4 sn şarj olur, sonra katın başlangıç odasına gider;
/// 30 sn bekleme süresi vardır. Zaten başlangıç noktasının 4 birim yakınındaysan işe yaramaz.
/// </summary>
public class LdrIsinlanma : MonoBehaviour
{
    public const float SarjSuresi = 1.4f;
    public const float BeklemeSuresi = 30f;
    public const float EnAzMesafe = 4f;

    public bool Sarjda { get; private set; }
    public float SarjIlerleme { get { return Mathf.Clamp01(sarj / SarjSuresi); } }
    public float Kalan { get; private set; }          // bekleme süresinden kalan saniye

    float sarj;
    Light2D sarjIsigi;

    void Update()
    {
        var o = LdrOyun.Ornek;
        if (o == null || o.kat == null || o.yasayan == null) return;
        var y = o.yasayan;
        if (Kalan > 0f) Kalan -= Time.deltaTime;

        bool engel = y.Donuk || y.Can.Olu || o.kat.Gecis;

        if (Sarjda)
        {
            if (engel) { Iptal(); return; }
            sarj += Time.deltaTime;
            if (sarjIsigi != null)
            {
                float k = SarjIlerleme;
                sarjIsigi.intensity = 0.4f + 1.8f * k;
                sarjIsigi.pointLightOuterRadius = 2f + 3f * k;
            }
            if (sarj >= SarjSuresi) Yap(o);
            return;
        }

        if (engel || !LdrGiris.IsinlanBasildi()) return;

        if (Kalan > 0f)
        {
            LdrHud.Duyur("IŞINLANMA  " + Mathf.CeilToInt(Kalan) + " SN SONRA HAZIR", new Color(0.7f, 0.65f, 0.85f));
            return;
        }
        var hedef = o.kat.Baslangic;
        if (o.kat.Gecerli == hedef && Vector2.Distance(y.transform.position, hedef.baslangic) < EnAzMesafe)
        {
            LdrHud.Duyur("ZATEN BAŞLANGIÇTASIN", new Color(0.7f, 0.65f, 0.85f));
            return;
        }

        Sarjda = true;
        sarj = 0f;
        var go = new GameObject("IsinlanmaIsigi");
        go.transform.SetParent(y.transform, false);
        sarjIsigi = go.AddComponent<Light2D>();
        sarjIsigi.lightType = Light2D.LightType.Point;
        sarjIsigi.color = new Color(0.7f, 0.35f, 1f);
        sarjIsigi.intensity = 0.4f;
        sarjIsigi.pointLightOuterRadius = 2f;
    }

    void Iptal()
    {
        Sarjda = false;
        sarj = 0f;
        if (sarjIsigi != null) Destroy(sarjIsigi.gameObject);
    }

    void Yap(LdrOyun o)
    {
        Vector3 eskiKonum = o.yasayan.transform.position;
        Iptal();
        Kalan = BeklemeSuresi;
        LdrSonenIsik.Olustur(eskiKonum, 0.6f);
        LdrVfx.Burst(eskiKonum, new Color(0.7f, 0.35f, 1f), Color.white, 36, 6f, 0.22f, 0.8f, 0f, 0, 40);
        var hedef = o.kat.Baslangic;
        o.kat.Isinla(hedef.baslangic, hedef, () =>
        {
            Vector3 yeni = o.yasayan.transform.position;
            LdrSonenIsik.Olustur(yeni, 0.6f);
            LdrVfx.Burst(yeni, new Color(0.7f, 0.35f, 1f), Color.white, 36, 6f, 0.22f, 0.8f, 0f, 0, 40);
            LdrHud.Flas = 0.5f;
        });
    }
}

/// <summary>Işınlanma anında beliren ve hızla sönen mor ışık patlaması.</summary>
public class LdrSonenIsik : MonoBehaviour
{
    Light2D isik;
    float sure, gecen, ilk;

    public static LdrSonenIsik Olustur(Vector3 konum, float sure)
    {
        var go = new GameObject("SonenIsik");
        go.transform.position = konum;
        var s = go.AddComponent<LdrSonenIsik>();
        s.sure = sure;
        s.isik = go.AddComponent<Light2D>();
        s.isik.lightType = Light2D.LightType.Point;
        s.isik.color = new Color(0.7f, 0.35f, 1f);
        s.isik.intensity = s.ilk = 2.5f;
        s.isik.pointLightOuterRadius = 9f;
        s.isik.pointLightInnerRadius = 0.5f;
        return s;
    }

    void Update()
    {
        gecen += Time.deltaTime;
        isik.intensity = ilk * Mathf.Clamp01(1f - gecen / sure);
        if (gecen >= sure) Destroy(gameObject);
    }
}
