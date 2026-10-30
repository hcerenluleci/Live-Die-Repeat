using UnityEngine;

/// <summary>
/// Çizim yardımcıları. Tüm sprite'lar (zemin, duvar, kapı, silah, oyuncu) bu projeye özgü
/// LdrResim deposundan ve LdrKostum çizimlerinden gelir; burada sadece sahneye yerleştirme işi yapılır.
/// </summary>
public static class LdrGorsel
{
    static Material isikli, isiksiz;
    static Sprite golge;

    /// <summary>Işıktan etkilenen 2D sprite malzemesi.</summary>
    public static Material Isikli()
    {
        if (isikli == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (sh != null) isikli = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }
        return isikli;
    }

    /// <summary>Işıktan etkilenmeyen (parlayan) 2D sprite malzemesi: alev, mermi vb.</summary>
    public static Material Isiksiz()
    {
        if (isiksiz == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh != null) isiksiz = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }
        return isiksiz;
    }

    public static SpriteRenderer Ciz(Transform ebeveyn, string ad, Sprite sprite, Vector3 yerel, int sira, Material malzeme)
    {
        var go = new GameObject(ad);
        go.transform.SetParent(ebeveyn, false);
        go.transform.localPosition = yerel;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sira;
        if (malzeme != null) sr.sharedMaterial = malzeme;
        return sr;
    }

    /// <summary>Oyuncunun ayağının altındaki yumuşak gölge.</summary>
    public static Sprite GolgeSprite()
    {
        if (golge != null) return golge;
        const int w = 16, h = 8;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f);
                float dy = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                float d = dx * dx + dy * dy;
                byte a = d < 0.55f ? (byte)150 : d < 1f ? (byte)70 : (byte)0;
                px[y * w + x] = new Color32(0, 0, 0, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        golge = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        golge.hideFlags = HideFlags.HideAndDontSave;
        return golge;
    }
}
