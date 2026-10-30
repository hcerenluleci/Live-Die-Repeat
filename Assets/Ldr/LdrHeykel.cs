using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ruh heykeli. Kaideli, kanatlı, göğsünde parlayan kalp tutan taş melek (canavara benzemesin diye yeni çizim); yanına gidince bir kez kısa bir nefes verir (+1 can) ve söner.
/// 32x40 çizim bize ait.
/// </summary>
public class LdrHeykel : MonoBehaviour
{
    const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAACAAAAAoCAYAAACfKfiZAAAC9UlEQVR4nO1XTUgbQRT+VgRLziK1hCCrLKGHUEJOxfPiKRYvgR5KLxUrQUQMFHorFAoREQmJ" +
        "6EU8FOxBYvAQFumxhyKLeChhSUNYQisipaegp+0hzLg/M7OzSWwv/S7ZyczO+95735t5qyACmmXHIc/TaTsw/91M0OeZZUWR2VNqEQDsP//hAMDLj48UAFDj" +
        "Gce/ptU5U8ja2dlJKRLSBNwkZECIDpUAcOf5ZrkEALCsNp3b2doAcBcJGYwMYtyPpdV1z7qhEggz3i8JKQKyxvshMSq1IwOl7V0AgD6nw6gb9DkqpMXCi4Jb" +
        "hARRxMhdIAqfm8RJ7RRaciZgnAUWISYBNZ5x1GQK+pwOTZuiXmraFNaW8x4SrDLcLJc871hWG0bdQKtxESARWQMiIRLxsdLCQ6AK3N77QbwnRiyrjULicYX+" +
        "12jCajSZadDndKjJVCC1HgLEeH5lUdqDov3tteza/MpigAQlIPLcjfmbWEU0v6dnhfP+SIxEMQ4Axw+61OPCSq73ez1G518ZtdCIuEmMRA07EVjhegwwbWrc" +
        "TUIGJB3cu91fhjtbG4Hymv/yE3iWBqomjp9O9oTZaAIAPterWFpdD5ShH6MA98RyDACadheZ+ZtYpQjQEBfHb1GomiiO30JzvbinZyvT9SpdxzsDgIjXsTv/" +
        "JBLF8VvPGJDTQSiBVudMaTUu6KXTL0rbu1zvhQT+FkJvK1ELxgIRIdATIiC+Ff95BP4T4PYD7rFs/oGeBrTkjOdAIpBqSNR4xpFtPqNibTkv15Cc1E7vhQAL" +
        "whTQDsdVWv1AVI7MCLQ6Z4oazziaNtW3UT/poZ+EC7GccCyLvgkcdQ89hsk4KoRdMSk7ngbepN9iOm1jwczhg/l++ATCNHDUPcSCmcNR99DzcQLICzc0BRuf" +
        "3oWS4OH3RDgJ9neB6yR8mJhA7fwgdCOW8Uv7irsvgac07vMUJPCfhgENEOF9/VXHpX2F7JMXAxutnR9w9wlEQE2mBjYogr89+wPdkIF8OLk/YQAAAABJRU5E" +
        "rkJggg==";

    static Sprite sprite;
    Light2D isik;
    SpriteRenderer sr;
    bool kullanildi;
    float faz;

    static Sprite Cizim()
    {
        if (sprite != null) return sprite;
        var t = new Texture2D(32, 40, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        t.LoadImage(System.Convert.FromBase64String(Png));
        t.filterMode = FilterMode.Point;
        sprite = Sprite.Create(t, new Rect(0, 0, 32, 40), new Vector2(0.5f, 0.5f), 16f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    public static LdrHeykel Kur(Transform ebeveyn, Vector2 dunya)
    {
        var go = new GameObject("Heykel");
        go.transform.SetParent(ebeveyn, false);
        go.transform.position = new Vector3(dunya.x, dunya.y, 0f);
        int katman = LayerMask.NameToLayer("Duvar");
        if (katman >= 0) go.layer = katman;
        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(1.8f, 1.0f);       // taş kaide: oyuncu ve canavarlar içinden geçemez
        bc.offset = new Vector2(0f, -0.75f);
        var h = go.AddComponent<LdrHeykel>();
        h.faz = Random.value * 6.28f;
        h.sr = LdrGorsel.Ciz(go.transform, "Govde", Cizim(), Vector3.zero, 7, LdrGorsel.Isikli());
        var lg = new GameObject("HeykelIsigi");
        lg.transform.SetParent(go.transform, false);
        lg.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        h.isik = lg.AddComponent<Light2D>();
        h.isik.lightType = Light2D.LightType.Point;
        h.isik.color = new Color(0.8f, 0.55f, 1f);
        h.isik.intensity = 0.9f;
        h.isik.pointLightOuterRadius = 3.5f;
        h.isik.falloffIntensity = 0.6f;
        return h;
    }

    void OnEnable() { LdrSira.Kaydet(GetComponent<Collider2D>()); }
    void OnDisable() { LdrSira.Sil(GetComponent<Collider2D>()); }

    void Update()
    {
        var o = LdrOyun.Ornek;
        if (kullanildi) return;
        isik.intensity = 0.7f + 0.35f * Mathf.Sin(Time.time * 2f + faz);
        if (o == null || o.yasayan == null || o.yasayan.Can.Olu) return;
        if (Vector2.Distance(o.yasayan.transform.position, transform.position) < 1.9f)
        {
            kullanildi = true;
            o.yasayan.Can.Iyiles(1);
            isik.intensity = 0.15f;
            isik.color = new Color(0.4f, 0.3f, 0.5f);
            sr.color = new Color(0.7f, 0.7f, 0.8f, 1f);
            LdrVfx.Burst(transform.position + Vector3.up * 0.4f, new Color(0.85f, 0.65f, 1f), Color.white, 18, 2.5f, 0.14f, 1f, -0.2f, 0, 14);
            LdrHud.Duyur("RUH HEYKELİ SANA KISA BİR NEFES VERDİ  +CAN", new Color(0.85f, 0.65f, 1f));
        }
    }
}
