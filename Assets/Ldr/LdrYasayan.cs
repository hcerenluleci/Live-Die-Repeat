using UnityEngine;

/// <summary>
/// Oyuncu ("Yaşayan"). Liz veya Jan olarak seçilir (LdrKostum çizimleri).
/// WASD hareket, SPACE ile takla (kısa dokunulmazlık), fareye doğru bakış ve silah çizimi.
/// </summary>
public class LdrYasayan : MonoBehaviour
{
    [Header("Hareket")]
    public float hiz = 5.5f;

    [Header("Takla")]
    public float taklaSuresi = 0.36f;
    public float taklaCarpani = 2.4f;
    public float taklaBekleme = 0.7f;

    public bool Takla { get { return taklaZaman > 0f; } }
    public bool Dokunulmaz { get { return Takla || (Buflar != null && Buflar.Kalkan > 0f); } }
    public Vector2 BakisYonu { get; private set; }
    public LdrCan Can { get; private set; }
    public LdrSilah Silah { get; private set; }
    public LdrBuflar Buflar { get; private set; }
    public bool Donuk { get; set; }          // ölüm ekranında kontrol kapalı

    Rigidbody2D rb;
    Transform govde, silah;
    SpriteRenderer govdeSr, silahSr;
    Sprite durus, adim;
    Camera kam;
    Vector2 girdi, taklaYon;
    Vector2 itme;
    float adimSayaci, taklaZaman, taklaBekleyen;
    int taklaIsaret = 1;
    const float Olcek = 1.8f;    // oyuncu çiziminin dünya boyu (birim)
    LdrAnimKopru anim;           // Animator (OyuncuAnim): Durus / Yuru / Takla / Olu
    // fener ve hale ışıkları açık renkli çizimleri (Liz) beyaza yakar; bu yüzden çizimler biraz kısılır
    static readonly Color IsikKisma = new Color(0.72f, 0.72f, 0.72f, 1f);

    // karakter farkı: Liz dayanıklı, Jan kırılgan ama vurulunca daha uzun dokunulmaz kalır
    public const float LizCan = 100f, LizKoruma = 2.5f;
    public const float JanCan = 70f, JanKoruma = 3.5f;

    public static LdrYasayan Olustur(Vector2 konum)
    {
        var go = new GameObject("Yasayan");
        try { go.tag = "Player"; } catch (UnityException) { }
        int katman = LayerMask.NameToLayer("Oyuncu");
        if (katman >= 0) go.layer = katman;
        go.transform.position = new Vector3(konum.x, konum.y, 0f);
        var y = go.AddComponent<LdrYasayan>();
        y.Kur();
        return y;
    }

    void Kur()
    {
        BakisYonu = Vector2.down;
        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var kutu = gameObject.AddComponent<BoxCollider2D>();
        kutu.size = new Vector2(0.9f, 0.6f);            // yalnızca ayaklar çarpışır (kafa duvarın önüne taşabilir)
        kutu.offset = new Vector2(0f, -0.55f);           // ayak hizası (çizim merkezde, boy 1,8)

        int skin = LdrKostum.SelectedSkin;      // 0 = Liz, 1 = Jan
        Can = gameObject.AddComponent<LdrCan>();
        Can.Kur(0, skin == 0 ? LizCan : JanCan, skin == 0 ? LizKoruma : JanKoruma);
        Can.ekKoruma = () => Dokunulmaz;
        Can.Hasarlandi += (c, kaynak) => { itme = ((Vector2)transform.position - kaynak).normalized * 9f; LdrSes.Cal("sfx_player_hurt", 0.7f); LdrKamera.Salla(0.08f); };
        Silah = gameObject.AddComponent<LdrSilah>();
        Buflar = gameObject.AddComponent<LdrBuflar>();

        durus = LdrKostum.SkinSprite(skin, false, 16f / Olcek);
        adim = LdrKostum.SkinSprite(skin, true, 16f / Olcek);

        var mat = LdrGorsel.Isikli();
        gameObject.AddComponent<LdrSiraliGorunum>().ayakKaydirma = -0.88f;
        var golge = LdrGorsel.Ciz(transform, "Golge", LdrGorsel.GolgeSprite(), new Vector3(0f, -0.88f, 0f), 9, LdrGorsel.Isiksiz());
        golge.color = new Color(1f, 1f, 1f, 0.9f);

        govdeSr = LdrGorsel.Ciz(transform, "Govde", durus, Vector3.zero, 10, mat);
        govde = govdeSr.transform;
        anim = LdrAnimKopru.Ekle(gameObject, "OyuncuAnim");

        silahSr = LdrGorsel.Ciz(transform, "Silah", LdrResim.Get(Silah.Tanim.cizim, 16f), new Vector3(0.73f, -0.22f, 0f), 11, mat);
        silah = silahSr.transform;
        silahSr.color = IsikKisma;
        Silah.SilahDegisti += t => { if (silahSr != null) silahSr.sprite = LdrResim.Get(t.cizim, 16f); };
    }

    void Update()
    {
        if (kam == null) kam = Camera.main;
        girdi = (Donuk || Can.Olu) ? Vector2.zero : LdrGiris.Yon();

        // bakış: fareye doğru
        Vector2 fare = LdrGiris.FareDunya(kam);
        Vector2 fark = fare - (Vector2)transform.position;
        if (fark.sqrMagnitude > 0.01f) BakisYonu = fark.normalized;

        if (Donuk || Can.Olu) return;

        if (taklaBekleyen > 0f) taklaBekleyen -= Time.deltaTime;

        // ateş ve yeniden doldurma (takla sırasında ateş yok)
        if (!Takla)
        {
            if (LdrGiris.DoldurBasildi()) Silah.Doldur();
            if (LdrGiris.AtesBasili())
                Silah.AtesEt((Vector2)transform.position + BakisYonu * 1.35f + new Vector2(0f, -0.22f), BakisYonu);
        }

        if (Takla)
        {
            taklaZaman -= Time.deltaTime;
        }
        else if (taklaBekleyen <= 0f && LdrGiris.TaklaBasildi())
        {
            taklaYon = girdi.sqrMagnitude > 0.01f ? girdi.normalized : BakisYonu;
            taklaIsaret = taklaYon.x >= 0f ? -1 : 1;
            taklaZaman = taklaSuresi;
            taklaBekleyen = taklaBekleme;
        }
    }

    void FixedUpdate()
    {
        Vector2 v = Takla ? taklaYon * hiz * taklaCarpani : girdi * hiz;
        rb.linearVelocity = v + itme;
        itme = Vector2.MoveTowards(itme, Vector2.zero, 40f * Time.fixedDeltaTime);
    }

    /// <summary>Oyuncuyu hemen bir konuma yerleştirir (oda geçişi, ışınlanma).</summary>
    public void Konumla(Vector2 konum)
    {
        transform.position = new Vector3(konum.x, konum.y, 0f);
        rb.position = konum;
        rb.linearVelocity = Vector2.zero;
        itme = Vector2.zero;
    }

    /// <summary>Oyun sırasında karakter değiştirilince (menüden) çizimleri yeni seçime göre yeniler.</summary>
    public void GorunumuYenile()
    {
        int skin = LdrKostum.SelectedSkin;      // 0 = Liz, 1 = Jan
        durus = LdrKostum.SkinSprite(skin, false, 16f / Olcek);
        adim = LdrKostum.SkinSprite(skin, true, 16f / Olcek);
        if (govdeSr != null) govdeSr.sprite = durus;
        Can.azami = skin == 0 ? LizCan : JanCan;
        Can.korumaSuresi = skin == 0 ? LizKoruma : JanKoruma;
        if (!Can.Olu) Can.Doldur();     // karakter değişince can dolu başlar
    }

    /// <summary>Dışarıdan itme (düşman vuruşu).</summary>
    public void It(Vector2 v) { itme = v; }

    /// <summary>Ölüm sonrası yeniden doğuş: konum, can ve mermi sıfırlanır.</summary>
    public void YenidenDogus(Vector2 konum)
    {
        transform.position = new Vector3(konum.x, konum.y, 0f);
        rb.linearVelocity = Vector2.zero;
        itme = Vector2.zero;
        taklaZaman = 0f;
        Can.Doldur();
        LdrPuan.Sifirla();
        Silah.Sifirla();
        Buflar.Sifirla();
        Donuk = false;
    }

    void LateUpdate()
    {
        bool hareket = girdi.sqrMagnitude > 0.01f && !Takla;
        if (anim != null)
        {
            // hangi animasyonun oynayacağına Animator karar verir; köprüdeki değerler gövdeye uygulanır
            anim.Bool("Hareket", hareket);
            anim.Bool("Takla", Takla);
            anim.Bool("Olu", Can.Olu);
            govdeSr.sprite = anim.Kare == 1 ? adim : durus;
        }
        else
        {
            // Animator yoksa: kodla iki kareli yürüyüş
            if (hareket) adimSayaci += Time.deltaTime; else adimSayaci = 0f;
            govdeSr.sprite = ((int)(adimSayaci / 0.18f) % 2 == 1) ? adim : durus;
        }

        // yüzünü fareye çevir
        govdeSr.flipX = BakisYonu.x < 0f;

        // hasar korumasındayken yanıp söner
        bool yanip = Can.KorumaKalan > 0f && ((int)(Time.time * 16f) % 2 == 0);
        govdeSr.color = yanip ? new Color(IsikKisma.r, IsikKisma.g, IsikKisma.b, 0.35f) : IsikKisma;

        // ölü: yere yatmış
        if (Can.Olu)
        {
            float yatis = anim != null ? anim.egim : 90f;
            govde.localRotation = Quaternion.Euler(0f, 0f, govdeSr.flipX ? -yatis : yatis);
            govde.localScale = anim != null ? anim.Olcek : Vector3.one;
            silahSr.enabled = false;
            return;
        }

        // takla: dönen ve hafif ezilen gövde
        if (Takla)
        {
            float aci = anim != null ? anim.egim : 360f * (1f - Mathf.Clamp01(taklaZaman / taklaSuresi));
            govde.localRotation = Quaternion.Euler(0f, 0f, taklaIsaret * aci);
            govde.localScale = anim != null ? anim.Olcek : new Vector3(1f, 0.82f, 1f);
            silahSr.enabled = false;
        }
        else
        {
            govde.localRotation = Quaternion.identity;
            govde.localScale = anim != null ? anim.Olcek : Vector3.one;
            silahSr.enabled = true;
            float aci = Mathf.Atan2(BakisYonu.y, BakisYonu.x) * Mathf.Rad2Deg;
            float tepme = 0.18f * Silah.GeriTepme;
            silah.localPosition = new Vector3(BakisYonu.x * (0.55f - tepme), BakisYonu.y * (0.55f - tepme) - 0.15f, 0f);
            silah.localRotation = Quaternion.Euler(0f, 0f, aci);
            silahSr.flipY = BakisYonu.x < 0f;
        }
    }
}
