using UnityEngine;

/// <summary>
/// Bölüm teması (ortam rengi). Bölüm seçim ekranındaki renkler: 1 mor, 2 yeşil, 3 kehribar, 4 kırmızı, 5 camgöbeği.
/// Zemin ve duvar karolarının tonu, ekran renk süzgeci, köşe kararması ve kafatası meşalelerinin (lav) alev renkleri bölüme göre değişir.
/// </summary>
public static class LdrTema
{
    public static int Bolum { get { return Mathf.Clamp(LdrOyun.Derinlik, 0, 4); } }

    static readonly float[] Ton = { 0.72f, 0.34f, 0.09f, 0.0f, 0.52f };      // karo sanatı mor (ton ~0,72): hedef ton
    static readonly Color[] FiltreRenk =
    {
        new Color(0.92f, 0.86f, 1f), new Color(0.84f, 1f, 0.87f), new Color(1f, 0.93f, 0.80f), new Color(1f, 0.83f, 0.83f), new Color(0.83f, 0.95f, 1f),
    };
    static readonly Color[] VinyetRenk =
    {
        new Color(0.06f, 0f, 0.12f), new Color(0f, 0.10f, 0.04f), new Color(0.12f, 0.07f, 0f), new Color(0.14f, 0f, 0.03f), new Color(0f, 0.07f, 0.14f),
    };
    static readonly Color[][] MesaleRenk =
    {
        new[] { new Color(0.66f, 0.30f, 1f), new Color(1f, 0.25f, 0.70f), new Color(0.25f, 0.88f, 1f) },       // mor, macenta, camgöbeği
        new[] { new Color(0.30f, 1f, 0.35f), new Color(0.60f, 1f, 0.20f), new Color(0.20f, 0.95f, 0.70f) },   // yeşil tonları
        new[] { new Color(1f, 0.72f, 0.20f), new Color(1f, 0.50f, 0.10f), new Color(1f, 0.88f, 0.40f) },      // kehribar tonları
        new[] { new Color(1f, 0.22f, 0.18f), new Color(1f, 0.45f, 0.12f), new Color(0.95f, 0.10f, 0.35f) },   // lav kırmızısı
        new[] { new Color(0.25f, 0.90f, 1f), new Color(0.30f, 0.60f, 1f), new Color(0.45f, 1f, 0.85f) },      // buz mavisi
    };

    public static Color Filtre { get { return FiltreRenk[Bolum]; } }
    public static Color Vinyet { get { return VinyetRenk[Bolum]; } }
    public static Color[] Mesale { get { return MesaleRenk[Bolum]; } }

    /// <summary>Karo pikselinin tonunu bölüm rengine kaydırır (gri tonlar değişmez).</summary>
    public static Color32 Boya(Color32 c)
    {
        int b = Bolum;
        if (b == 0 || c.a == 0) return c;
        float h, s, v;
        Color.RGBToHSV(c, out h, out s, out v);
        if (s < 0.10f) return c;
        h = Mathf.Repeat(h + (Ton[b] - Ton[0]), 1f);
        Color32 r = Color.HSVToRGB(h, s, v);
        r.a = c.a;
        return r;
    }
}
