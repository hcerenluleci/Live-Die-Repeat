using UnityEngine;

/// <summary>
/// İtilebilir tahta masa (siper).
///  - Yatay masa 2,90 x 1,00, dikey masa 1,00 x 2,10 çarpışma kutusu; önce yerinden oynamaz (çok ağır), mermi içinden geçer.
///  - [E] ile oyuncunun durduğu tarafa göre devrilir (5 karelik devrilme çizimi): hafifler, itilebilir olur, mermiyi durdurur.
///  - Sönüm 30, dönme kilitli; oda sınırının dışına çıkamaz; sürüklenirken ses çalar.
/// </summary>
public class LdrMasa : MonoBehaviour, ILdrEtkilesim
{
    const float KutleSabit = 50000f, KutleDevrik = 6f;
    const float KareSuresi = 0.07f;

    public bool Devrildi { get; private set; }

    Rigidbody2D rb;
    BoxCollider2D kutu;
    SpriteRenderer sr;
    LdrOda oda;
    Vector3 onceki;
    bool dikey;
    Sprite[] devrilme;
    float devrilmeSuresi = -1f;

    public Vector2 Konum { get { return (Vector2)transform.position + new Vector2(0f, kutu != null ? kutu.offset.y : 0.5f); } }
    public string Yazi1 { get { return "[E]  MASAYI DEVİR"; } }
    public string Yazi2 { get { return ""; } }
    public bool Hazir { get { return !Devrildi; } }

    public static LdrMasa Kur(LdrOda oda, Vector2 dunya, bool dikey)
    {
        var go = new GameObject(dikey ? "MasaDikey" : "MasaYatay");
        go.transform.SetParent(oda.env != null ? oda.env : oda.kok, false);
        go.transform.position = new Vector3(dunya.x, dunya.y, 0f);
        int katman = LayerMask.NameToLayer("MermiGecer");
        if (katman >= 0) go.layer = katman;

        var m = go.AddComponent<LdrMasa>();
        m.oda = oda;
        m.dikey = dikey;
        m.kutu = go.AddComponent<BoxCollider2D>();
        m.kutu.size = dikey ? new Vector2(1.0f, 2.1f) : new Vector2(2.9f, 1.0f);
        m.kutu.offset = dikey ? new Vector2(0f, 1.05f) : new Vector2(0f, 0.5f);

        LdrSira.Kaydet(m.kutu);
        m.rb = go.AddComponent<Rigidbody2D>();
        m.rb.bodyType = RigidbodyType2D.Dynamic;
        m.rb.mass = KutleSabit;
        m.rb.linearDamping = 30f;
        m.rb.angularDamping = 0.05f;
        m.rb.gravityScale = 0f;
        m.rb.freezeRotation = true;
        m.rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        m.rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var ilk = Kareler(dikey ? "masa_dikey_asagi" : "masa_yatay_asagi", dikey ? 22 : 48)[0];
        m.sr = LdrGorsel.Ciz(go.transform, "Govde", ilk, Vector3.zero, 6, LdrGorsel.Isikli());
        m.onceki = go.transform.position;
        return m;
    }

    void OnEnable() { LdrEtkilesim.Kaydet(this); LdrSira.Kaydet(kutu); }
    void OnDisable() { LdrEtkilesim.Sil(this); LdrSira.Sil(kutu); }

    public void Etkilesim(LdrYasayan oyuncu)
    {
        if (Devrildi) return;
        Devrildi = true;
        rb.mass = KutleDevrik;
        int katman = LayerMask.NameToLayer("Esya");
        if (katman >= 0) gameObject.layer = katman;

        // devrilme yönü: oyuncuya en yakın kenarın tersine
        Bounds b = kutu.bounds;
        Vector3 p = b.ClosestPoint(oyuncu.transform.position);
        bool sola = false;
        string yon;
        if (Mathf.Approximately(p.x, b.max.x)) { yon = "sag"; sola = true; }        // oyuncu sağda: sola devrilir (sağ çiziminin aynası)
        else if (Mathf.Approximately(p.x, b.min.x)) yon = "sag";                     // oyuncu solda: sağa devrilir
        else if (Mathf.Approximately(p.y, b.min.y)) yon = "yukari";
        else yon = "asagi";
        string ad = (dikey ? "masa_dikey_" : "masa_yatay_") + yon;
        devrilme = Kareler(ad, dikey ? 22 : 48);
        sr.flipX = sola;
        devrilmeSuresi = 0f;
        LdrSes.Cal("sfx_door", 0.7f, 1.25f);
    }

    void Update()
    {
        if (devrilmeSuresi < 0f || devrilme == null) return;
        devrilmeSuresi += Time.deltaTime;
        int k = Mathf.Min(devrilme.Length - 1, (int)(devrilmeSuresi / KareSuresi));
        sr.sprite = devrilme[k];
        if (k >= devrilme.Length - 1) devrilmeSuresi = -1f;
    }

    void FixedUpdate()
    {
        if (!Devrildi) return;
        // oda sınırının dışına çıkamaz
        Bounds b = kutu.bounds;
        Rect z = oda.zemin;
        if (b.min.x <= z.xMin || b.max.x >= z.xMax || b.min.y <= z.yMin || b.max.y >= z.yMax)
        {
            transform.position = onceki;
            rb.linearVelocity = Vector2.zero;
        }
        else onceki = transform.position;
        if (rb.linearVelocity.sqrMagnitude > 0.0001f && Time.frameCount % 10 == 0)
            LdrSes.Cal("sfx_drag", 0.35f, 1.2f);
    }

    // ---- çizim: masa sayfaları (LdrResim masa_*: 5 kare, 16 px/birim, pivot altta)
    static readonly System.Collections.Generic.Dictionary<string, Sprite[]> onbellek = new System.Collections.Generic.Dictionary<string, Sprite[]>();

    static Sprite[] Kareler(string ad, int kareGenislik)
    {
        Sprite[] k;
        if (onbellek.TryGetValue(ad, out k) && k[0] != null) return k;
        var t = LdrResim.Tex(ad);
        int n = t.width / kareGenislik;
        k = new Sprite[n];
        for (int i = 0; i < n; i++)
        {
            k[i] = Sprite.Create(t, new Rect(i * kareGenislik, 0, kareGenislik, t.height), new Vector2(0.5f, 0f), 16f);
            k[i].hideFlags = HideFlags.HideAndDontSave;
        }
        onbellek[ad] = k;
        return k;
    }
}
