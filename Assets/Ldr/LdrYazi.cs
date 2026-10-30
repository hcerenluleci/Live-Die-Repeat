using UnityEngine;

/// <summary>Özgün piksel font (LDR Pixel, bu projede çizildi, Türkçe harfler dahil). Assets/Resources/Fonts/LDRPixel.ttf</summary>
public static class LdrYazi
{
    static Font font; static TMPro.TMP_FontAsset asset; static bool tried;
    public static float K { get { return Font != null ? 0.6f : 1f; } }
    public static Font Font
    {
        get
        {
            if (!tried)
            {
                tried = true;
                try { font = Resources.Load<Font>("Fonts/LDRPixel"); } catch (System.Exception) { }
            }
            return font;
        }
    }
    public static TMPro.TMP_FontAsset Asset
    {
        get
        {
            if (asset == null && Font != null)
            {
                try
                {
                    asset = TMPro.TMP_FontAsset.CreateFontAsset(font);
                    if (asset != null) asset.hideFlags = HideFlags.HideAndDontSave;
                }
                catch (System.Exception e) { Debug.LogWarning("Font oluşturulamadı: " + e.Message); }
            }
            return asset;
        }
    }
}
