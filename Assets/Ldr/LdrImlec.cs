using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NİŞAN İMLECİ: tüm ekranlarda (menüler, oyun) görünen, kan kırmızısı, nabız gibi atan imleç.
/// Arkasında soluk bir kan izi bırakır. Kendi kendine kurulur.
/// </summary>
public class LdrImlec : MonoBehaviour
{
    [Tooltip("İmleç boyutu (piksel)")]
    public float size = 56f;
    [Tooltip("İz uzunluğu (saniye)")]
    public float trailTime = 0.4f;

    Texture2D tex;
    float pressTime = -10f;

    struct Dot { public Vector2 pos; public float time; }
    readonly List<Dot> trail = new List<Dot>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("LdrImlec");
        DontDestroyOnLoad(go);
        go.AddComponent<LdrImlec>();
    }

    void Awake()
    {
        tex = BuildTexture();
    }

    void Update()
    {
        Cursor.visible = false;

    }

    // 31x31 piksel nişan: halka + 4 çentik + orta nokta, siyah kenarlıklı
    static Texture2D BuildTexture()
    {
        const int N = 31, C = 15;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        t.hideFlags = HideFlags.HideAndDontSave;
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;

        var red = new Color32(214, 26, 38, 255);
        var dark = new Color32(8, 0, 2, 230);
        var clear = new Color32(0, 0, 0, 0);
        var px = new Color32[N * N];

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                int dx = x - C, dy = y - C;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                int ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);

                bool ring = d >= 8.5f && d <= 10.5f;
                bool ringOutline = (d >= 7.5f && d < 8.5f) || (d > 10.5f && d <= 11.5f);
                bool tickV = ax == 0 && ay >= 12 && ay <= 15;
                bool tickH = ay == 0 && ax >= 12 && ax <= 15;
                bool tickOutline = (ax <= 1 && ay >= 11 && ay <= 15 && !tickV) || (ay <= 1 && ax >= 11 && ax <= 15 && !tickH);
                bool dot = d <= 1.5f;
                bool dotOutline = d > 1.5f && d <= 2.5f;

                Color32 c = clear;
                if (ring || tickV || tickH || dot) c = red;
                else if (ringOutline || tickOutline || dotOutline) c = dark;
                px[y * N + x] = c;
            }
        }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    void OnGUI()
    {
        GUI.depth = -10000;                       // her şeyin üstünde
        var e = Event.current;
        if (e.type == EventType.MouseDown) pressTime = Time.unscaledTime;
        if (e.type != EventType.Repaint) return;

        Vector2 m = e.mousePosition;
        float now = Time.unscaledTime;

        // iz
        if (trail.Count == 0 || (trail[trail.Count - 1].pos - m).sqrMagnitude > 25f)
            trail.Add(new Dot { pos = m, time = now });
        while (trail.Count > 0 && now - trail[0].time > trailTime) trail.RemoveAt(0);

        var old = GUI.color;
        for (int i = 0; i < trail.Count; i++)
        {
            float k = 1f - (now - trail[i].time) / trailTime;      // 1 -> 0
            float s = 3f + 7f * k;
            GUI.color = new Color(0.55f, 0.02f, 0.05f, 0.55f * k);
            GUI.DrawTexture(new Rect(trail[i].pos.x - s / 2f, trail[i].pos.y - s / 2f, s, s), Texture2D.whiteTexture);
        }

        // imleç: nabız + tıklama + ara sıra titreme
        float pulse = 1f + 0.07f * Mathf.Sin(now * 4.5f);
        float press = Mathf.Clamp01(1f - (now - pressTime) / 0.15f);
        float sz = size * pulse * (1f - 0.22f * press);
        float flicker = Mathf.PerlinNoise(now * 11f, 3f) > 0.92f ? 0.45f : 1f;
        GUI.color = new Color(1f, 1f - 0.7f * press, 1f - 0.7f * press, flicker);
        GUI.DrawTexture(new Rect(m.x - sz / 2f, m.y - sz / 2f, sz, sz), tex, ScaleMode.StretchToFill, true);
        GUI.color = old;
    }
}
