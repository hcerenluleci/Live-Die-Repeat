using UnityEngine;

/// <summary>
/// Ortak arayüz araç kutusu: tüm ekranlar (menü, karakter seçimi, silah pazarı, HUD) aynı mor-kırmızı temayı
/// ve aynı piksel fontu buradan kullanır. Allocation'sız, tek seferde üretilen dokular.
/// </summary>
public static class LdrUI
{
    public static readonly Color Purple = new Color(0.62f, 0.28f, 0.95f);
    public static readonly Color Blood = new Color(0.85f, 0.12f, 0.2f);
    public static readonly Color Ice = new Color(0.35f, 0.9f, 1f);
    public static readonly Color Ink = new Color(0.04f, 0.015f, 0.07f, 0.92f);
    public static readonly Color Bone = new Color(0.93f, 0.88f, 0.98f);

    static Texture2D glow, white;
    public static Texture2D Glow
    {
        get
        {
            if (glow == null)
            {
                glow = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                var p = new Color32[64 * 64];
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                    float a = Mathf.Clamp01(1f - d); a *= a;
                    p[y * 64 + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                glow.SetPixels32(p); glow.Apply();
            }
            return glow;
        }
    }
    public static Texture2D White { get { if (white == null) white = Texture2D.whiteTexture; return white; } }

    /// <summary>Özgün piksel font (5x7) bir harf 6 piksel yer kaplar: bir karakterin genişliği = boyut x CharW.</summary>
    public const float CharW = 0.75f;

    /// <summary>Piksel font boyutu: her zaman 8'in katı ve en az 16 (16, 24, 32...). 8'in katı olmayan boyutta piksel font bulanır/bozulur.</summary>
    public static int Px(float size)
    {
        float v = size * 1.25f;
        return Mathf.Max(16, Mathf.RoundToInt(v / 8f) * 8);
    }

    /// <summary>Bir boyut küçült: 32 -> 24 -> 16 (16'nın altına inmez).</summary>
    public static int StepDown(int fs)
    {
        return fs > 16 ? fs - 8 : 16;
    }

    public static GUIStyle MakeStyle(TextAnchor anchor, bool wrap = false)
    {
        var s = new GUIStyle(GUI.skin.label) { alignment = anchor, wordWrap = wrap, richText = false, clipping = TextClipping.Overflow };
        if (LdrYazi.Font != null) { s.font = LdrYazi.Font; s.fontStyle = FontStyle.Normal; }
        return s;
    }

    public static void Fill(Rect r, Color c)
    {
        var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, White); GUI.color = o;
    }

    public static void Frame(Rect r, Color c, float w)
    {
        Fill(new Rect(r.x, r.y, r.width, w), c); Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
        Fill(new Rect(r.x, r.y, w, r.height), c); Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
    }

    public static void Light(Vector2 c, float radius, Color col)
    {
        var o = GUI.color; GUI.color = col;
        GUI.DrawTexture(new Rect(c.x - radius, c.y - radius, radius * 2f, radius * 2f), Glow);
        GUI.color = o;
    }

    /// <summary>Köşe süslemeli panel: iç dolgu + ince çerçeve + parlak köşe parantezleri.</summary>
    public static void Panel(Rect r, Color accent, float sc, float fillAlpha = 0.92f, float glowAmount = 0f)
    {
        if (glowAmount > 0f) Light(r.center, Mathf.Max(r.width, r.height) * 0.7f, new Color(accent.r, accent.g, accent.b, 0.16f * glowAmount));
        Fill(r, new Color(0.045f, 0.02f, 0.08f, fillAlpha));
        Frame(r, new Color(accent.r, accent.g, accent.b, 0.55f), Mathf.Max(1f, 1.5f * sc));
        float L = 14f * sc, T = Mathf.Max(2f, 3f * sc);
        Color a = accent;
        Fill(new Rect(r.x - 1, r.y - 1, L, T), a); Fill(new Rect(r.x - 1, r.y - 1, T, L), a);
        Fill(new Rect(r.xMax - L + 1, r.y - 1, L, T), a); Fill(new Rect(r.xMax - T + 1, r.y - 1, T, L), a);
        Fill(new Rect(r.x - 1, r.yMax - T + 1, L, T), a); Fill(new Rect(r.x - 1, r.yMax - L + 1, T, L), a);
        Fill(new Rect(r.xMax - L + 1, r.yMax - T + 1, L, T), a); Fill(new Rect(r.xMax - T + 1, r.yMax - L + 1, T, L), a);
    }

    /// <summary>Tek yönlü sert gölge: iki kopya yerine tek kopya, böylece yazı "kalın/bulanık" görünmez.</summary>
    public static void Text(Rect r, string s, GUIStyle st, Color c, float shadow = 1f)
    {
        // Kutu yazıdan kısaysa harflerin üst/alt pikselleri (noktalar, çengeller, nokta işaretleri) kesilir: kutuyu büyüt
        float needH = st.fontSize * 1.5f;
        if (st.fontSize > 0 && r.height < needH)
        {
            float d = needH - r.height;
            int row = (int)st.alignment / 3; // 0 üst, 1 orta, 2 alt
            if (row == 1) r.y -= d * 0.5f; else if (row == 2) r.y -= d;
            r.height = needH;
        }
        var o = st.normal.textColor;
        if (shadow > 0f)
        {
            st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.85f);
            GUI.Label(new Rect(r.x + shadow, r.y + shadow, r.width, r.height), s, st);
        }
        st.normal.textColor = c;
        GUI.Label(r, s, st);
        st.normal.textColor = o;
    }

    /// <summary>Parlayan sıvı gibi yavaşça kayan gölge çizgisi (tarama çizgisi) – ucuz CRT havası.</summary>
    public static void Scanlines(float t, float sc)
    {
        float h = Screen.height;
        float y = Mathf.Repeat(t * 40f * sc, h);
        Fill(new Rect(0, y, Screen.width, 2f * sc), new Color(1f, 1f, 1f, 0.025f));
    }
}
