using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Desenli mermi: bir ateşte tek mermi değil, geometrik bir biçimde dizilmiş toplar çıkar; biçim uçarken kendi etrafında döner.
/// Bakıcı (sarı toplar): Halka, Yıldız ya da Haç. Sürünen (yeşil toplar): Sarmal ya da İkiz Sarmal.
/// Biçimler kodla hesaplanır (aşağıdaki Desen* yöntemleri). Hız 6-10, menzil 26, dönme 95 derece/sn, 0,6 sn şarj.
/// Her topun arkasında kısa, ucu incelen kırmızı bir iz vardır.
/// </summary>
public class LdrKume : MonoBehaviour
{
    // ---- desenler: (x, y, ölçek) üçlüleri, merkez (0,0) çevresinde
    static float[] DesenHalka()
    {
        var l = new List<float>();
        for (int i = 0; i < 12; i++) { float a = i * Mathf.PI * 2f / 12f; l.Add(Mathf.Cos(a) * 1.4f); l.Add(Mathf.Sin(a) * 1.4f); l.Add(1f); }
        l.Add(0f); l.Add(0f); l.Add(1.8f);
        return l.ToArray();
    }

    static float[] DesenYildiz()
    {
        var l = new List<float>();
        for (int k = 0; k < 5; k++)
        {
            float a = k * Mathf.PI * 2f / 5f + Mathf.PI * 0.5f;
            for (int j = 1; j <= 3; j++) { float r = 0.55f * j; l.Add(Mathf.Cos(a) * r); l.Add(Mathf.Sin(a) * r); l.Add(j == 3 ? 1.2f : 1f); }
        }
        l.Add(0f); l.Add(0f); l.Add(1.4f);
        return l.ToArray();
    }

    static float[] DesenHac()
    {
        var l = new List<float>();
        for (int k = 0; k < 4; k++)
        {
            float a = k * Mathf.PI * 0.5f;
            for (int j = 1; j <= 4; j++) { float r = 0.42f * j; l.Add(Mathf.Cos(a) * r); l.Add(Mathf.Sin(a) * r); l.Add(1f); }
        }
        return l.ToArray();
    }

    static float[] DesenSarmal()
    {
        var l = new List<float>();
        for (int i = 0; i < 14; i++) { float a = i * 0.55f, r = 0.25f + i * 0.12f; l.Add(Mathf.Cos(a) * r); l.Add(Mathf.Sin(a) * r); l.Add(1f); }
        return l.ToArray();
    }

    static float[] DesenIkizSarmal()
    {
        var l = new List<float>();
        for (int i = 0; i < 8; i++)
            for (int k = 0; k < 2; k++) { float a = i * 0.7f + k * Mathf.PI, r = 0.3f + i * 0.17f; l.Add(Mathf.Cos(a) * r); l.Add(Mathf.Sin(a) * r); l.Add(1f); }
        return l.ToArray();
    }

    const float SarjSuresi = 0.6f, DonmeHizi = 95f, TopYaricapi = 0.17f;

    class Top { public Transform tr; public SpriteRenderer sr; public Vector3 onceki; }
    readonly List<Top> toplar = new List<Top>();
    Vector2 yon;
    float hiz, kalanMenzil, sarj, hasar;
    Color renk;

    static Sprite sariSpr, yesilSpr;

    /// <param name="sari">true: Bakıcı deseni (sarı), false: Sürünen deseni (yeşil)</param>
    public static LdrKume Ates(Vector2 konum, Vector2 yon, bool sari, float hasar)
    {
        var go = new GameObject(sari ? "GirdapDeseni" : "SarmalDeseni");
        go.transform.position = new Vector3(konum.x, konum.y, 0f);
        var k = go.AddComponent<LdrKume>();
        float[] desen;
        if (sari) { int r = Random.Range(0, 3); desen = r == 0 ? DesenHalka() : r == 1 ? DesenYildiz() : DesenHac(); }
        else desen = Random.value < 0.5f ? DesenSarmal() : DesenIkizSarmal();
        float sapma = Random.Range(0f, 3f) * (Random.value < 0.5f ? -1f : 1f);
        k.yon = (Vector2)(Quaternion.Euler(0f, 0f, sapma) * (Vector3)yon.normalized);
        k.hiz = Random.Range(6f, 10f);
        k.kalanMenzil = 26f;
        k.sarj = SarjSuresi;
        k.hasar = hasar;
        k.renk = sari ? new Color(1f, 0.85f, 0.2f) : new Color(0.4f, 1f, 0.45f);

        var spr = TopSprite(sari);
        var mat = LdrGorsel.Isiksiz();
        for (int i = 0; i + 2 < desen.Length; i += 3)
        {
            var c = new GameObject("Top");
            c.transform.SetParent(go.transform, false);
            c.transform.localPosition = new Vector3(desen[i], desen[i + 1], 0f);
            c.transform.localScale = Vector3.one * desen[i + 2];
            var sr = c.AddComponent<SpriteRenderer>();
            sr.sprite = spr; sr.sortingOrder = 12; sr.sharedMaterial = mat;
            IzEkle(c, mat);
            k.toplar.Add(new Top { tr = c.transform, sr = sr, onceki = c.transform.position });
        }
        return k;
    }

    /// <summary>Düşman mermisi izi: parlak kırmızı baş, koyu kuyruk, 0,16 sn, ucu incelen.</summary>
    public static void IzEkle(GameObject go, Material mat)
    {
        var tr = go.AddComponent<TrailRenderer>();
        tr.sharedMaterial = mat;
        tr.minVertexDistance = 0.04f;
        tr.numCapVertices = 2;
        tr.sortingOrder = 11;
        tr.time = 0.16f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.17f), new Keyframe(1f, 0f));
        Color bas = new Color(1f, 0.25f, 0.2f, 0.9f), kuyruk = new Color(0.5f, 0f, 0.05f, 0f);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(bas, 0f), new GradientColorKey(kuyruk, 1f) },
                  new[] { new GradientAlphaKey(bas.a, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
    }

    /// <summary>7x7 yuvarlak top: koyu kenar, gövde, parlak nokta. Sarı (Bakıcı) ve yeşil (Sürünen).</summary>
    static Sprite TopSprite(bool sari)
    {
        if (sari && sariSpr != null) return sariSpr;
        if (!sari && yesilSpr != null) return yesilSpr;
        string[] sekil =
        {
            "..ooo..",
            ".obbbo.",
            "obhhbbo",
            "obhwbbo",
            "obbbbbo",
            ".obbbo.",
            "..ooo.."
        };
        Color32 o = sari ? new Color32(176, 96, 0, 255) : new Color32(20, 112, 44, 255);
        Color32 b = sari ? new Color32(255, 214, 40, 255) : new Color32(110, 240, 90, 255);
        Color32 hl = sari ? new Color32(255, 240, 150, 255) : new Color32(200, 255, 170, 255);
        Color32 wh = new Color32(255, 255, 235, 255);
        var tex = new Texture2D(7, 7, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[49];
        for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
            {
                char ch = sekil[6 - y][x];
                px[y * 7 + x] = ch == 'o' ? o : ch == 'b' ? b : ch == 'h' ? hl : ch == 'w' ? wh : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, 7, 7), new Vector2(0.5f, 0.5f), 16f);
        s.hideFlags = HideFlags.HideAndDontSave;
        if (sari) sariSpr = s; else yesilSpr = s;
        return s;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (sarj > 0f)
        {
            // şarj: top yerinde bekler, soluk yanıp söner
            sarj -= dt;
            float a = 0.45f + 0.35f * Mathf.Sin(Time.time * 30f);
            for (int i = 0; i < toplar.Count; i++) if (toplar[i].sr != null) toplar[i].sr.color = new Color(1f, 1f, 1f, a);
            if (sarj <= 0f)
                for (int i = 0; i < toplar.Count; i++)
                {
                    if (toplar[i].sr != null) toplar[i].sr.color = Color.white;
                    if (toplar[i].tr != null) toplar[i].onceki = toplar[i].tr.position;
                }
            return;
        }

        float adim = hiz * dt;
        transform.position += (Vector3)(yon * adim);
        transform.Rotate(0f, 0f, DonmeHizi * dt);
        kalanMenzil -= adim;

        for (int i = toplar.Count - 1; i >= 0; i--)
        {
            var top = toplar[i];
            if (top.tr == null) { toplar.RemoveAt(i); continue; }
            Vector3 p = top.tr.position;
            Vector2 fark = p - top.onceki;
            float uzak = fark.magnitude;
            if (uzak > 0.0001f && Carp(top.onceki, fark / uzak, uzak))
            {
                Destroy(top.tr.gameObject);
                toplar.RemoveAt(i);
                continue;
            }
            top.onceki = p;
        }
        if (kalanMenzil <= 0f || toplar.Count == 0) Destroy(gameObject);
    }

    /// <summary>Topun bu karedeki yolunda bir şeye çarptı mı (oyuncu hasar alır, duvar/eşya topu yok eder).</summary>
    bool Carp(Vector2 baslangic, Vector2 yonu, float uzaklik)
    {
        var vurus = Physics2D.CircleCastAll(baslangic, TopYaricapi, yonu, uzaklik);
        for (int j = 0; j < vurus.Length; j++)
        {
            var c = vurus[j].collider;
            if (c == null) continue;
            if (c.isTrigger && c.GetComponent<LdrFici>() == null) continue;
            var masa = c.GetComponent<LdrMasa>();
            if (masa != null && !masa.Devrildi) continue;
            var can = c.GetComponentInParent<LdrCan>();
            if (can != null)
            {
                if (can.takim == 1 || can.Olu) continue;       // düşmanlar ve ölüler topu durdurmaz
                can.Hasar(hasar, vurus[j].point);
            }
            LdrVfx.Burst(vurus[j].point, renk, Color.white, 5, 2.5f, 0.12f, 0.25f, 0f, 0, 40);
            return true;
        }
        return false;
    }
}
