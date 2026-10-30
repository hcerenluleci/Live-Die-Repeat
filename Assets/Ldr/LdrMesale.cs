using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Meşale: boynuzlu kafatası askısı, 3 kareli alev ve titreyen renkli ışık.
/// Duvar meşalesi 1,4x1,9, ayakta duran 0,85x2,1, küçük 0,9x0,9; alev her tipte kafatasının tepesinden çıkar. Işık: yarıçap 6, titreme 0,35-0,65.
/// </summary>
public class LdrMesale : MonoBehaviour
{
    static readonly Color[] Palet =
    {
        new Color(0.66f, 0.30f, 1.00f),   // mor
        new Color(1.00f, 0.25f, 0.70f),   // macenta
        new Color(0.25f, 0.88f, 1.00f),   // camgöbeği
    };

    SpriteRenderer alev, tutucuSr;
    float taban;
    Light2D isik;
    Sprite[] kareler;
    float tohum;
    Color ton;

    /// <param name="tip">'a' duvar (siyah), 'b' ayakta, 'c' kahverengi; başka bir şey: duvar</param>
    public static LdrMesale Kur(Transform ebeveyn, Vector3 konum, char tip)
    {
        var go = new GameObject("Mesale");
        go.transform.SetParent(ebeveyn, false);
        go.transform.position = konum;
        var m = go.AddComponent<LdrMesale>();
        m.tohum = Random.value * 10f;
        var palet = LdrTema.Mesale;
        m.ton = palet[Mathf.Abs(Mathf.RoundToInt(konum.x * 7.3f + konum.y * 3.1f)) % palet.Length];

        Vector2 boyut, pivot;
        switch (tip)
        {
            case 'b': boyut = new Vector2(0.85f, 2.1f); pivot = new Vector2(0.5f, 0f); break;
            case 'c': boyut = new Vector2(0.9f, 0.9f); pivot = new Vector2(0.5f, 0.5f); break;
            default: boyut = new Vector2(1.4f, 1.9f); pivot = new Vector2(0.5f, 0f); break;
        }
        // kafatası çizimi kare (32x32); yükseklik = büyük kenar. Alev, duvar meşalesindeki gibi her zaman kafanın tepesinden çıkar
        // (alev boyun %77'sinden çıkar). Boyuna göre oranlanır, böylece ayakta duranlarda havada asılı kalmaz.
        float yuk = Mathf.Max(boyut.x, boyut.y);
        float taban = -yuk * pivot.y;
        Vector3 alevYeri = new Vector3(0.03f * yuk / 2f, taban + 0.77f * yuk, 0f);
        float isikY = taban + 0.235f * yuk;

        var tutucu = LdrResim.Fit("skull_holder", boyut, pivot, true, 1f);
        m.taban = konum.y + taban;
        if (tutucu != null) m.tutucuSr = LdrGorsel.Ciz(go.transform, "Tutucu", tutucu, new Vector3(0f, tip == 'b' || tip == 'c' ? 0f : -0.025f, 0f), 5, LdrGorsel.Isikli());

        // alev: 8x10 piksellik üç kare, 0,85 x 1,55 birime oturtulur
        m.kareler = new Sprite[3];
        for (int i = 0; i < 3; i++) m.kareler[i] = LdrResim.Fit("flame_" + i, new Vector2(0.85f, 1.55f), new Vector2(0.5f, 0.1f), true, 0.9f);
        if (m.kareler[0] != null)
        {
            m.alev = LdrGorsel.Ciz(go.transform, "Alev", m.kareler[0], alevYeri, 6, LdrGorsel.Isiksiz());
            m.alev.color = m.ton;
        }

        // ayakta duran meşalelerin dibi katıdır (oyuncu ve canavarlar üstünden geçemez)
        if (tip == 'b' || tip == 'c')
        {
            int katman = LayerMask.NameToLayer("Duvar");
            if (katman >= 0) go.layer = katman;
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(0.9f, 0.5f);
            bc.offset = new Vector2(0f, taban + 0.25f);
            LdrSira.Kaydet(bc);
        }

        var lgo = new GameObject("Isik");
        lgo.transform.SetParent(go.transform, false);
        lgo.transform.localPosition = new Vector3(0f, isikY, 0f);
        m.isik = lgo.AddComponent<Light2D>();
        m.isik.lightType = Light2D.LightType.Point;
        m.isik.color = m.ton;
        m.isik.intensity = 0.6f;
        m.isik.pointLightOuterRadius = 6f;
        m.isik.pointLightInnerRadius = 0.4f;
        m.isik.falloffIntensity = 0.5f;
        return m;
    }

    void OnEnable() { LdrSira.Kaydet(GetComponent<Collider2D>()); }
    void OnDisable() { LdrSira.Sil(GetComponent<Collider2D>()); }

    void Update()
    {
        float t = Time.time + tohum;
        if (alev != null && kareler != null && kareler[0] != null) alev.sprite = kareler[(int)(t * 8f) % kareler.Length];
        if (isik != null) isik.intensity = Mathf.Lerp(0.35f, 0.65f, Mathf.PerlinNoise(t * 2.6f, tohum));
    }
}
