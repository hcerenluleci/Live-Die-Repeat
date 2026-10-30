using UnityEngine;

/// <summary>
/// Mermi. Her karede küçük bir daire taramasıyla ilerler (hızlı mermi duvardan geçmesin).
/// Menzili aşınca kaybolur; oyuncu mermisi düşmana değerse ZİNCİR artar, başka bir şeye değerse veya menzil dışına
/// çıkarsa azalır (LdrPuan). İz bırakan mermiler (lazer, roket) arkasında soluk bir iz çizer.
/// </summary>
public class LdrMermi : MonoBehaviour
{
    Vector2 yon;
    float hiz, kalanMenzil, hasar;
    int takim;
    bool iz;
    float izSuresi, izBekleme;
    Color renk = Color.white;
    bool bitti;
    TrailRenderer izRenderi;

    /// <summary>Tanımdan ateş eder (silah tablosu).</summary>
    public static LdrMermi Ates(Vector2 konum, Vector2 yon, LdrMermiTanimi t, int takim)
    {
        float hiz = Random.Range(t.hiz.x, Mathf.Max(t.hiz.x, t.hiz.y));
        var m = Olustur(konum, yon, hiz, t.hasar, takim, t.menzil);
        m.iz = t.iz;
        m.izSuresi = t.izSuresi;
        m.renk = t.renk;
        return m;
    }

    /// <summary>Kısa çağrı biçimi (eşya/tuzak): hız ve ömürden menzil üretir.</summary>
    public static LdrMermi Ates(Vector2 konum, Vector2 yon, float hiz, float hasar, int takim, float omur = 1.6f)
    {
        return Olustur(konum, yon, hiz, hasar, takim, hiz * omur);
    }

    static LdrMermi Olustur(Vector2 konum, Vector2 yon, float hiz, float hasar, int takim, float menzil)
    {
        var go = new GameObject(takim == 0 ? "OyuncuMermisi" : "DusmanMermisi");
        go.transform.position = new Vector3(konum.x, konum.y, 0f);
        float aci = Mathf.Atan2(yon.y, yon.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0f, 0f, aci);

        var m = go.AddComponent<LdrMermi>();
        m.yon = yon.normalized;
        m.hiz = hiz;
        m.hasar = hasar;
        m.takim = takim;
        m.kalanMenzil = menzil;

        var spr = LdrResim.Get(takim == 0 ? "bullet_player" : "bullet_enemy", 16f);
        LdrGorsel.Ciz(go.transform, "Cizim", spr, Vector3.zero, 12, LdrGorsel.Isiksiz());
        m.izRenderi = Iz(go, takim != 0);
        return m;
    }

    /// <summary>Mermi izi: her mermi arkasında 0,16 sn'lik, ucu incelen ışıklı iz bırakır.
    /// Düşman mermisi kırmızı (1, 0,25, 0,2), oyuncu mermisi mor-beyaz (0,75, 0,6, 1).</summary>
    static TrailRenderer Iz(GameObject go, bool dusman)
    {
        var tr = go.AddComponent<TrailRenderer>();
        tr.sharedMaterial = LdrGorsel.Isiksiz();
        tr.minVertexDistance = 0.04f;
        tr.numCapVertices = 2;
        tr.sortingOrder = 11;
        tr.time = 0.16f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.17f), new Keyframe(1f, 0f));
        Color bas = dusman ? new Color(1f, 0.25f, 0.2f, 0.9f) : new Color(0.75f, 0.6f, 1f, 0.9f);
        Color son = dusman ? new Color(0.5f, 0f, 0.05f, 0f) : new Color(0.3f, 0.1f, 0.8f, 0f);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(bas, 0f), new GradientColorKey(son, 1f) },
                  new[] { new GradientAlphaKey(bas.a, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
        return tr;
    }

    public void HasarCarpaniUygula(float c) { hasar *= c; }

    void Update()
    {
        if (bitti) return;
        float adim = hiz * Time.deltaTime;
        if (adim > kalanMenzil) adim = Mathf.Max(0f, kalanMenzil);
        Vector2 p = transform.position;
        var vurus = Physics2D.CircleCastAll(p, 0.1f, yon, adim);   // yakından uzağa sıralı
        for (int i = 0; i < vurus.Length; i++)
        {
            var c = vurus[i].collider;
            if (c == null) continue;
            if (c.isTrigger && c.GetComponent<LdrFici>() == null) continue;   // fıçı tetik alanıdır ama mermiyi karşılar
            var masa = c.GetComponent<LdrMasa>();
            if (masa != null && !masa.Devrildi) continue;                       // devrilmemiş masadan mermi geçer (siper olması için devrilmeli)
            var can = c.GetComponentInParent<LdrCan>();
            if (can != null)
            {
                if (can.takim == takim || can.Olu) continue;         // kendi takımı ve ölüler mermiyi durdurmaz
                can.Hasar(hasar, p);
                if (takim == 0) LdrPuan.ZincirDegistir(true);
            }
            else if (takim == 0) LdrPuan.ZincirDegistir(false);       // oyuncu mermisi duvara/eşyaya çarptı
            Kivilcim(vurus[i].point);
            Bitir();
            return;
        }
        transform.position = p + yon * adim;
        kalanMenzil -= adim;
        if (iz) IzBirak();
        if (kalanMenzil <= 0.001f)
        {
            if (takim == 0) LdrPuan.ZincirDegistir(false);            // menzil dışı: boşa gitti
            Bitir();
        }
    }

    void Bitir()
    {
        bitti = true;
        Destroy(gameObject);
    }

    void IzBirak()
    {
        izBekleme -= Time.deltaTime;
        if (izBekleme > 0f) return;
        izBekleme = 0.02f;
        var spr = LdrResim.Get("bullet_player", 16f);
        if (spr == null) return;
        var go = new GameObject("MermiIzi");
        go.transform.position = transform.position;
        go.transform.rotation = transform.rotation;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr;
        sr.sortingOrder = 11;
        sr.sharedMaterial = LdrGorsel.Isiksiz();
        sr.color = new Color(renk.r, renk.g, renk.b, 0.55f);
        Destroy(go, Mathf.Max(0.05f, izSuresi));
    }

    /// <summary>Çarpma noktasında çok kısa bir parıltı.</summary>
    static void Kivilcim(Vector2 nokta)
    {
        var spr = LdrResim.Get("flame_0", 16f);
        if (spr == null) return;
        var go = new GameObject("Kivilcim");
        go.transform.position = new Vector3(nokta.x, nokta.y, 0f);
        go.transform.localScale = Vector3.one * 0.45f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr;
        sr.sortingOrder = 13;
        sr.sharedMaterial = LdrGorsel.Isiksiz();
        sr.color = new Color(1f, 0.85f, 0.5f, 1f);
        Destroy(go, 0.09f);
    }
}
