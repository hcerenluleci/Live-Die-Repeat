using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ÖZGÜN KARAKTERLER: Liz (kadın) ve Jan (erkek). Takımın çizdiği 16x16 piksel-art kareler kod içinde durur;
/// sprite'lar ilk istendiğinde üretilir. Seçim "LDR_Skin" anahtarıyla saklanır.
/// </summary>
public static class LdrKostum
{
    const string SkinKey = "LDR_Skin";

    // ---------------- ÇİZİMLER (16x16, '.' = saydam) ----------------
    static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
    {
        { 'o', new Color32(28, 22, 32, 255) },
        { 'F', new Color32(214, 184, 160, 255) },
        { 'E', new Color32(18, 8, 10, 255) },
        { 'B', new Color32(52, 36, 32, 255) },
        { 'y', new Color32(190, 150, 88, 255) },
        { 'Y', new Color32(152, 112, 60, 255) },
        { 'd', new Color32(108, 74, 40, 255) },
        { 'P', new Color32(150, 56, 100, 255) },
        { 'p', new Color32(188, 92, 134, 255) },
        { 'q', new Color32(100, 34, 70, 255) },
        { 'm', new Color32(128, 86, 54, 255) },
        { 'M', new Color32(92, 58, 36, 255) },
        { 'R', new Color32(178, 38, 46, 255) },
        { 'r', new Color32(216, 74, 72, 255) },
        { 'g', new Color32(52, 120, 68, 255) }
    };

    static readonly string[] FemaleIdle =
    {
        "....oooooooo....",
        "...oyyYYYYYYo...",
        "..oyYYYYYYYYYo..",
        "..oYYFFFFFFYYo..",
        "..oYdFEFFEFdYo..",
        "..oYdFFFFFFdYo..",
        "..oYYdFFFFdYYo..",
        "..oYYPPPPPPYYo..",
        "..oYpPPPPPPpYo..",
        "..oFpPPPPPPpFo..",
        "...oPPPqqPPPo...",
        "..oPPPPPPPPPPo..",
        "..oPpPPPPPPpPo..",
        "....oFFooFFo....",
        "....oBBooBBo....",
        "....oooooooo...."
    };

    static readonly string[] FemaleStep =
    {
        "....oooooooo....",
        "...oyyYYYYYYo...",
        "..oyYYYYYYYYYo..",
        "..oYYFFFFFFYYo..",
        "..oYdFEFFEFdYo..",
        "..oYdFFFFFFdYo..",
        "..oYYdFFFFdYYo..",
        "..oYYPPPPPPYYo..",
        "..oYpPPPPPPpYo..",
        "..oFpPPPPPPpFo..",
        "...oPPPqqPPPo...",
        "..oPPPPPPPPPPo..",
        "..oPpPPPPPPpPo..",
        "...oFFo..oFFo...",
        "...oBBo..oBBo...",
        "...oooo..oooo..."
    };

    static readonly string[] MaleIdle =
    {
        "....oooooooo....",
        "...ommMMMMMMo...",
        "..omMMMMMMMMMo..",
        "..oMMFFFFFFMMo..",
        "..oMFFEFFEFFMo..",
        "..oMFFFFFFFFMo..",
        "...oFFFFFFFFo...",
        "...oRRFFFFRRo...",
        "..oRRRRRRRRRRo..",
        "..oRrRRRRRRrRo..",
        "..oRrRRBBRRrRo..",
        "..oFRRRRRRRRFo..",
        "...oggggggggo...",
        "....oggooggo....",
        "....oBBooBBo....",
        "....oooooooo...."
    };

    static readonly string[] MaleStep =
    {
        "....oooooooo....",
        "...ommMMMMMMo...",
        "..omMMMMMMMMMo..",
        "..oMMFFFFFFMMo..",
        "..oMFFEFFEFFMo..",
        "..oMFFFFFFFFMo..",
        "...oFFFFFFFFo...",
        "...oRRFFFFRRo...",
        "..oRRRRRRRRRRo..",
        "..oRrRRRRRRrRo..",
        "..oRrRRBBRRrRo..",
        "..oFRRRRRRRRFo..",
        "...oggggggggo...",
        "...oggo..oggo...",
        "...oBBo..oBBo...",
        "...oooo..oooo..."
    };

    // Liz = 0 (kadın), Jan = 1 (erkek).
    static readonly Dictionary<string, Sprite> sharedSprites = new Dictionary<string, Sprite>();
    public static int SelectedSkin { get { return Mathf.Clamp(PlayerPrefs.GetInt(SkinKey, 0), 0, 1); } }
    public static Sprite SkinSprite(int skin, bool step, float ppu = 16f)
    {
        string key = skin + (step ? "s" : "i") + ppu;
        Sprite s;
        if (sharedSprites.TryGetValue(key, out s) && s != null) return s;
        string[] rows = skin == 0 ? (step ? FemaleStep : FemaleIdle) : (step ? MaleStep : MaleIdle);
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave; tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            string row = rows[h - 1 - y];
            for (int x = 0; x < w; x++)
            {
                Color32 col;
                px[y * w + x] = Palette.TryGetValue(row[x], out col) ? col : new Color32(0, 0, 0, 0);
            }
        }
        tex.SetPixels32(px); tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        s.hideFlags = HideFlags.HideAndDontSave;
        sharedSprites[key] = s;
        return s;
    }
}
