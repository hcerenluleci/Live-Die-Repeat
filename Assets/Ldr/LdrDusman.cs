using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum LdrDusmanDurumu { Uyuyor, Uyari, Kovala, Saldir, Sendeler, Oldu }

// =====================================================================================
// DERS GEREKSİNİMİ: OYUN YAPAY ZEKASI (Finite State Machine)  -  Ali Çağrı Sepet
// Durumlar: Uyuyor -> Uyari (0,55 sn irkilme) -> Kovala <-> Saldir ; vurulunca Sendeler (0,16 sn) ; can bitince Oldu.
// Uyanma: oyuncu 40 birim içine gelince. Ateş edenler sadece oyuncuyu görürken (görüş çizgisi) ateş eder.
// Yol: oyuncuya giden yol masa/duvarla kapalıysa A* (LdrYol) ile etrafından dolaşır.
// Her durum Animator'daki (DusmanAnim) aynı adlı animasyonu oynatır: anim.Int("Durum", ...).
// 5 tür: Sızıntı, Kızıl Sızıntı, Ağıtçı, Bakıcı (dönen 8'li mermi halkası), Sürünen (atılır ve ateş eder). Can = taban x (1 + 0,18 x derinlik).
// =====================================================================================
/// <summary>
/// Düşman. Beş tür, takımın kendi yaratık çizimleri (LdrYaratikSayfa): Sızıntı, Kızıl Sızıntı, Ağıtçı, Bakıcı, Sürünen.
/// Sonlu durum makinesi: Uyuyor -> Uyarı (0,55 sn irkilme) -> Kovala <-> Saldır; vurulunca kısa Sendele; can bitince ölüm patlaması.
/// Can = türün temel canı x derinlik çarpanı (CanBul); puan = başlangıç canı x ZİNCİR; vurulunca başının üstünde can çubuğu görünür.
///   Sızıntı: tek mermi atan kovalayıcı        Kızıl Sızıntı: üçlü yelpaze atan ağır yaratık
///   Ağıtçı : hızlı, zikzak yapan yakın saldırgan   Bakıcı  : mesafe koruyup dönen mermi halkası atan
///   Sürünen: yakın saldırgan, ara sıra üstüne atılır
/// </summary>
public class LdrDusman : MonoBehaviour
{
    public const int Sizinti = 0, KizilSizinti = 1, Agitci = 2, Bakici = 3, Surunen = 4;   // sırasıyla Sızıntı, Kızıl Sızıntı, Ağıtçı, Bakıcı, Sürünen
    public static readonly string[] Adlar = { "Sizinti", "KizilSizinti", "Agitci", "Bakici", "Surunen" };

    enum Dav { Atesci, Yelpaze, Hizli, Halka, Atilan }

    public static readonly List<LdrDusman> Hepsi = new List<LdrDusman>();

    // tür başına ayarlar
    static readonly float[] Boy = { 2.1f, 2.9f, 2.3f, 2.0f, 2.5f };              // görünür boy (dünya birimi), 32x32 yaratık karesi bu boya ölçeklenir
    static readonly float[] Yaricap = { 0.50f, 0.72f, 0.55f, 0.48f, 0.60f };   // çarpışma yarıçapı
    static readonly float[] Hizlar = { 2.3f, 1.6f, 3.6f, 1.9f, 2.6f };
    // temel can: başlangıç tabancası (5 hasar) ile Sızıntı 4 mermide, Kızıl Sızıntı 6 mermide düşer
    static readonly float[] TemelCan = { 18f, 30f, 14f, 22f, 26f };
    const float DerinlikCarpani = 0.18f;   // her bölümde canlar %18 artar
    static readonly string[] SilahAnahtari = { "tabanca", "pompali", null, "sarmal", null };   // LdrSilahTablosu.Dusman

    static float CanBul(int tur, int derinlik)
    {
        return Mathf.Round(TemelCan[tur] * (1f + DerinlikCarpani * Mathf.Max(0, derinlik)));
    }
    static readonly Dav[] Davranis = { Dav.Atesci, Dav.Yelpaze, Dav.Hizli, Dav.Halka, Dav.Atilan };
    static readonly float[] Menzil = { 8f, 9f, 0f, 10f, 0f };                    // 0 = yakın saldırgan
    // göz ışığı renkleri (Sızıntı, Kızıl Sızıntı, Ağıtçı, Bakıcı, Sürünen)
    static readonly Color[] Parilti =
    {
        new Color(1f, 0.85f, 0.25f), new Color(1f, 0.45f, 0.15f), new Color(1f, 0.15f, 0.2f),
        new Color(1f, 0.7f, 0.15f), new Color(0.9f, 0.95f, 0.7f),
    };

    public int tur = Sizinti;
    public float hiz = 2.2f;
    public float uyanmaMesafesi = 40f;     // bu mesafede uyanır ve kovalar (görüş çizgisi aranmaz)
    public float dokunmaHasari = 8f;       // temas hasarı
    public float saldiriAraligi = 0.6f;    // iki temas hasarı arası (sn)

    public LdrCan Can { get; private set; }
    public LdrDusmanDurumu Durum { get; private set; }

    Rigidbody2D rb;
    SpriteRenderer govdeSr;
    Transform govde;
    Light2D isik;
    Transform canCubuguKok, canCubuguDolgu;
    float cubukGenislik;
    Sprite[] kareler;
    Vector2 itme, yonCache, sonKonum;
    float saldiriBekleme, kirmizi, faz, yonYenile, yuruyus, zaman, ateBekleme, atilmaKalan, atilmaBekleme, halkaAci;
    float uyariKalan, belirmeKalan, belirmeSure;
    Collider2D carpisma;
    LdrAnimKopru anim;                      // Animator (DusmanAnim): FSM durumlarının her biri ayrı animasyon

    // ---- boss (can 260 + 80 x derinlik, boss silahı, 6-8 sn ateş / 1,5-2,5 sn dinlenme)
    public bool boss;
    string bossSilah;
    Color ton = Color.white;
    float olcek = 1f;
    bool atesDonemi;
    float donemKalan, bossAtesBekle;
    int bossSarjor;
    float R { get { return Yaricap[tur] * olcek; } }
    float MenzilDeger { get { return boss ? 12f : Menzil[tur]; } }
    const float UyariSure = 0.55f, SendelemeSure = 0.16f;
    LdrDusmanDurumu devam = LdrDusmanDurumu.Kovala;
    Vector2 atilmaYon;
    int duvarMaskesi;
    int engelMaskesi;                       // duvar + masa + heykel: yol bulurken bunlardan kaçınılır (mermiyi sadece duvar durdurur)
    static readonly RaycastHit2D[] sonuc = new RaycastHit2D[8];
    Vector2 takilmaKonum;
    readonly List<Vector2> yol = new List<Vector2>();     // A* yolu (kalan ara noktalar)
    float yolZaman = 99f;
    Vector2 yolHedef;
    float takilma, kacisKalan;
    Vector2 kacisYon;

    static readonly Sprite[][] onbellek = new Sprite[5][];

    /// <summary>Sayfa: 64x160, her satır bir tür, iki kare (32x32). Üst satır Kafatası.</summary>
    static Sprite[] KareleriHazirla(int tur)
    {
        if (onbellek[tur] != null && onbellek[tur][0] != null) return onbellek[tur];
        var sayfa = LdrYaratikSayfa.Sayfa();
        float ppu = 32f / Boy[tur];
        var k = new Sprite[2];
        for (int f = 0; f < 2; f++)
        {
            // her kare kendi küçük dokusuna kopyalanır: ortak sayfadan kesilen kareye komşu karenin kenar çizgileri sızmaz
            // (canavarın başında "can barı gibi cızırtı" bundandı)
            var kt = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            kt.SetPixels(sayfa.GetPixels(f * 32, (4 - tur) * 32, 32, 32));
            kt.Apply();
            k[f] = Sprite.Create(kt, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), ppu);
            k[f].hideFlags = HideFlags.HideAndDontSave;
        }
        onbellek[tur] = k;
        return k;
    }

    /// <summary>Boss: Kızıl Sızıntı (Boss 2) veya Sürünen (Boss 1) çizimi büyütülür. silah: tabancapatron, pompalipatron, seripatron.</summary>
    public static LdrDusman BossOlustur(Vector2 konum, int tur, string silah, Color ton)
    {
        tur = Mathf.Clamp(tur, 0, 4);
        var go = new GameObject("Boss_" + Adlar[tur]);
        go.transform.position = new Vector3(konum.x, konum.y, 0f);
        int katman = LayerMask.NameToLayer("Dusman");
        if (katman >= 0) go.layer = katman;
        var d = go.AddComponent<LdrDusman>();
        d.tur = tur;
        d.boss = true;
        d.bossSilah = silah;
        d.ton = ton;
        d.olcek = tur == KizilSizinti ? 1.5f : 1.85f;   // boss, türünün büyütülmüş hâlidir
        go.transform.localScale = new Vector3(d.olcek, d.olcek, 1f);
        d.Kur();
        return d;
    }

    public static LdrDusman Olustur(Vector2 konum, int tur = Sizinti)
    {
        tur = Mathf.Clamp(tur, 0, 4);
        var go = new GameObject(Adlar[tur]);
        go.transform.position = new Vector3(konum.x, konum.y, 0f);
        int katman = LayerMask.NameToLayer("Dusman");
        if (katman >= 0) go.layer = katman;
        var d = go.AddComponent<LdrDusman>();
        d.tur = tur;
        d.Kur();
        return d;
    }

    void Kur()
    {
        duvarMaskesi = LayerMask.GetMask("Duvar");
        engelMaskesi = LayerMask.GetMask("Duvar", "MermiGecer", "Esya");
        hiz = Hizlar[tur];

        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var daire = gameObject.AddComponent<CircleCollider2D>();
        daire.radius = Yaricap[tur];
        daire.offset = new Vector2(0f, -0.1f);
        carpisma = daire;
        belirmeSure = belirmeKalan = Random.Range(1.6f, 2.4f);   // belirirken çarpışma ve yapay zekâ kapalı
        daire.enabled = false;

        Can = gameObject.AddComponent<LdrCan>();
        Can.Kur(1, boss ? 260f + 80f * Mathf.Max(0, LdrOyun.Derinlik) : CanBul(tur, LdrOyun.Derinlik), 0f);
        Can.Hasarlandi += Vuruldu;
        Can.Oldu += Oldu;

        float boy = Boy[tur];
        var golge = LdrGorsel.Ciz(transform, "Golge", LdrGorsel.GolgeSprite(), new Vector3(0f, -boy * 0.36f, 0f), 8, LdrGorsel.Isiksiz());
        golge.color = new Color(1f, 1f, 1f, 0.85f);
        golge.transform.localScale = new Vector3(boy * 0.65f, boy * 0.65f, 1f);

        CanCubuguKur(boy);
        var sira = gameObject.AddComponent<LdrSiraliGorunum>();
        sira.ayakKaydirma = -boy * 0.36f * transform.localScale.y;
        kareler = KareleriHazirla(tur);
        govdeSr = LdrGorsel.Ciz(transform, "Govde", kareler[0], Vector3.zero, 9, LdrGorsel.Isikli());
        govde = govdeSr.transform;
        string gc = boss ? (bossSilah != null && bossSilah.Contains("pompali") ? "gun_shotgun" : bossSilah != null && bossSilah.Contains("seri") ? "gun_smg" : "gun_pistol") : SilahCizim[tur];
        if (gc != null)
        {
            var spr = LdrResim.Get(gc, 16f);
            if (spr != null)
            {
                silahSr = LdrGorsel.Ciz(transform, "ElSilahi", spr, Vector3.zero, 10, LdrGorsel.Isikli());
                silahTr = silahSr.transform;
                silahTr.localScale = Vector3.one * (boss ? 1.1f : 0.85f);
            }
        }
        govdeSr.color = new Color(ton.r, ton.g, ton.b, 0f);
        anim = LdrAnimKopru.Ekle(gameObject, "DusmanAnim");

        // göz ışığı: oyuncu yaklaştıkça parlar
        var lg = new GameObject("GozIsigi");
        lg.transform.SetParent(transform, false);
        isik = lg.AddComponent<Light2D>();
        isik.lightType = Light2D.LightType.Point;
        isik.color = Parilti[tur];
        isik.intensity = 0.7f;
        isik.pointLightOuterRadius = tur == KizilSizinti ? 3.4f : 2.3f;
        isik.pointLightInnerRadius = 0.1f;
        isik.falloffIntensity = 0.7f;

        faz = Random.value * 6.28f;
        sonKonum = transform.position;
        ateBekleme = Random.Range(0.8f, 1.8f);
        atilmaBekleme = Random.Range(1.5f, 3.5f);
        Durum = LdrDusmanDurumu.Uyuyor;
    }

    static Sprite beyaz;
    static Sprite Beyaz()
    {
        if (beyaz == null)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, Color.white); t.Apply();
            beyaz = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            beyaz.hideFlags = HideFlags.HideAndDontSave;
        }
        return beyaz;
    }

    /// <summary>Can çubuğu: koyu mor çerçeve, siyaha yakın zemin, kan kırmızısı dolgu. Düşman hasar alınca başının üstünde görünür.</summary>
    void CanCubuguKur(float boy)
    {
        cubukGenislik = Mathf.Max(1.1f, boy * 0.7f);
        var kok = new GameObject("CanCubugu");
        kok.transform.SetParent(transform, false);
        kok.transform.localPosition = new Vector3(-cubukGenislik * 0.5f, boy * 0.55f + 0.15f, 0f);
        canCubuguKok = kok.transform;
        var mat = LdrGorsel.Isiksiz();
        var cerceve = LdrGorsel.Ciz(kok.transform, "Cerceve", Beyaz(), new Vector3(-0.05f, 0f, 0f), 20, mat);
        cerceve.color = new Color(0.36f, 0.12f, 0.46f, 1f);
        cerceve.transform.localScale = new Vector3(cubukGenislik + 0.1f, 0.19f, 1f);
        var zemin = LdrGorsel.Ciz(kok.transform, "Zemin", Beyaz(), Vector3.zero, 21, mat);
        zemin.color = new Color(0.1f, 0.02f, 0.12f, 1f);
        zemin.transform.localScale = new Vector3(cubukGenislik, 0.13f, 1f);
        var dolgu = LdrGorsel.Ciz(kok.transform, "Dolgu", Beyaz(), Vector3.zero, 22, mat);
        dolgu.color = new Color(0.92f, 0.16f, 0.3f, 1f);
        dolgu.transform.localScale = new Vector3(cubukGenislik, 0.13f, 1f);
        canCubuguDolgu = dolgu.transform;
        kok.SetActive(false);
    }

    void CanCubuguGuncelle()
    {
        if (canCubuguKok == null) return;
        bool goster = !Can.Olu && Can.Oran < 0.999f;
        if (canCubuguKok.gameObject.activeSelf != goster) canCubuguKok.gameObject.SetActive(goster);
        if (goster) canCubuguDolgu.localScale = new Vector3(cubukGenislik * Can.Oran, 0.13f, 1f);
    }

    void OnEnable() { Hepsi.Add(this); }
    void OnDisable() { Hepsi.Remove(this); }

    // FSM: yeni duruma geçer ve o durumun zamanlayıcısını sıfırlar.
    void Gir(LdrDusmanDurumu d)
    {
        if (Durum == d || Durum == LdrDusmanDurumu.Oldu) return;
        var onceki = Durum;
        Durum = d;
        switch (d)
        {
            case LdrDusmanDurumu.Uyari:
                uyariKalan = UyariSure;
                LdrKamera.Salla(0.05f);
                break;
            case LdrDusmanDurumu.Sendeler:
                uyariKalan = SendelemeSure;
                devam = (onceki == LdrDusmanDurumu.Sendeler || onceki == LdrDusmanDurumu.Uyuyor || onceki == LdrDusmanDurumu.Uyari) ? LdrDusmanDurumu.Kovala : onceki;
                break;
        }
    }

    // FSM'in kalbi: her karede o anki duruma göre ne yapılacağına karar verir ve Animator'a durumu yazar.
    void Update()
    {
        if (Can.Olu) return;
        var oyuncu = LdrOyun.Ornek != null ? LdrOyun.Ornek.yasayan : null;
        if (oyuncu == null) return;

        // belirme: yavaşça görünür olur, bitince çarpışma açılır
        if (belirmeKalan > 0f)
        {
            belirmeKalan -= Time.deltaTime;
            float a = 1f - Mathf.Clamp01(belirmeKalan / belirmeSure);
            govdeSr.color = new Color(ton.r, ton.g, ton.b, a * a);
            if (belirmeKalan <= 0f) { govdeSr.color = ton; carpisma.enabled = true; }
            return;
        }

        zaman += Time.deltaTime;
        Vector2 fark = (Vector2)oyuncu.transform.position - (Vector2)transform.position;
        float mesafe = fark.magnitude;
        bool oyuncuHayatta = !oyuncu.Can.Olu;

        // ---- durum makinesi
        switch (Durum)
        {
            case LdrDusmanDurumu.Uyuyor:
                if (oyuncuHayatta && mesafe < uyanmaMesafesi) Gir(LdrDusmanDurumu.Uyari);
                break;
            case LdrDusmanDurumu.Uyari:
                uyariKalan -= Time.deltaTime;
                if (uyariKalan <= 0f) Gir(LdrDusmanDurumu.Kovala);
                break;
            case LdrDusmanDurumu.Kovala:
                if (MenzilDeger > 0.5f && mesafe <= MenzilDeger * 0.85f && Gorus(fark, mesafe)) Gir(LdrDusmanDurumu.Saldir);
                break;
            case LdrDusmanDurumu.Saldir:
                if (mesafe > MenzilDeger * 1.15f) { Gir(LdrDusmanDurumu.Kovala); break; }
                if (oyuncuHayatta) AtesGuncelle(fark, mesafe);
                break;
            case LdrDusmanDurumu.Sendeler:
                uyariKalan -= Time.deltaTime;
                if (uyariKalan <= 0f) Gir(devam);
                break;
        }
        if (Durum == LdrDusmanDurumu.Kovala && Davranis[tur] == Dav.Atilan && oyuncuHayatta) AtilmaGuncelle(fark);
        // yakın dövüşen türler de (Ağıtçı: SMG, Sürünen: yeşil top) kovalarken menzile girince ateş eder
        if (!boss && Durum == LdrDusmanDurumu.Kovala && (Davranis[tur] == Dav.Hizli || Davranis[tur] == Dav.Atilan) && oyuncuHayatta && mesafe <= AtesMenzili) AtesGuncelle(fark, mesafe);

        // ---- görünüm
        bool hareketli = ((Vector2)transform.position - sonKonum).sqrMagnitude > 0.000001f;
        sonKonum = transform.position;
        if (hareketli) yuruyus += Time.deltaTime;
        float hizliFaz = Durum != LdrDusmanDurumu.Uyuyor ? 6f : 2.4f;
        faz += Time.deltaTime * hizliFaz;
        float tt = Time.time + faz;
        float titre = Mathf.PerlinNoise(tt * 6f, faz) > 0.86f ? 1.06f : 1f;
        if (anim != null)
        {
            // FSM durumu Animator'a verilir; kare, ölçek ve eğim o durumun klibinden gelir
            anim.Int("Durum", (int)Durum);
            int k = (Durum == LdrDusmanDurumu.Kovala && !hareketli) ? 0 : anim.Kare;
            if (Durum == LdrDusmanDurumu.Saldir && ateBekleme < 0.3f) k = 1;     // ateş hazırlığı: çene açık
            govdeSr.sprite = kareler[k];
            govde.localScale = new Vector3(anim.olcekX * titre, anim.olcekY, 1f);
            govde.localRotation = Quaternion.Euler(0f, 0f, anim.egim);
        }
        else
        {
            govdeSr.sprite = kareler[hareketli ? ((int)(yuruyus * 6f) % 2) : 0];
            // ateş hazırlığı: çene açık kare
            if (Durum == LdrDusmanDurumu.Saldir && ateBekleme < 0.3f) govdeSr.sprite = kareler[1];
            float nefes = 0.04f * Mathf.Sin(tt * 3.1f);
            float sis = 1f;
            if (Durum == LdrDusmanDurumu.Uyari) sis = 1f + 0.2f * Mathf.Sin((1f - Mathf.Clamp01(uyariKalan / UyariSure)) * Mathf.PI);   // irkilme: şişer
            else if (Durum == LdrDusmanDurumu.Sendeler) sis = 0.9f;                                                                      // vurulunca büzülür
            govde.localScale = new Vector3((1f - nefes) * titre * sis, (1f + nefes) * sis, 1f);
        }
        govdeSr.flipX = oyuncu.transform.position.x < transform.position.x;   // oyuncuya dön
        if (silahTr != null)
        {
            // silah oyuncuya doğru çevrilir; sola bakarken ters çevrilir
            Vector2 d = fark.sqrMagnitude > 0.0001f ? fark.normalized : Vector2.right;
            silahTr.localPosition = new Vector3(d.x * 0.5f, d.y * 0.4f - 0.18f, 0f);
            silahTr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            silahSr.flipY = d.x < 0f;
            silahSr.enabled = Durum != LdrDusmanDurumu.Uyuyor;
            silahSr.color = govdeSr.color;
        }

        float yakin = Mathf.Clamp01(1f - mesafe / 7f);
        float parlama = (Durum == LdrDusmanDurumu.Uyari ? 2.2f * Mathf.Clamp01(uyariKalan / UyariSure) : 0f)
                      + (Durum == LdrDusmanDurumu.Saldir && ateBekleme < 0.3f ? 1.4f : 0f);
        isik.intensity = (0.55f + 0.9f * yakin) * (0.85f + 0.15f * Mathf.Sin(tt * 9f)) * (titre > 1f ? 1.6f : 1f) + parlama;

        CanCubuguGuncelle();
        if (kirmizi > 0f) kirmizi -= Time.deltaTime;
        govdeSr.color = kirmizi > 0f ? new Color(1f, 0.25f, 0.25f, 1f) : ton;

        // ---- dokununca hasar (uyurken, irkilirken ve sendelerken yok)
        if (!oyuncuHayatta || Durum == LdrDusmanDurumu.Uyuyor || Durum == LdrDusmanDurumu.Uyari || Durum == LdrDusmanDurumu.Sendeler) return;
        if (saldiriBekleme > 0f) saldiriBekleme -= Time.deltaTime;
        else if (mesafe < R + 0.5f)
        {
            if (oyuncu.Can.Hasar(dokunmaHasari, transform.position))
            {
                oyuncu.It(fark.normalized * 9f);
                saldiriBekleme = saldiriAraligi;
            }
        }
    }

    void FixedUpdate()
    {
        if (Can.Olu) { rb.linearVelocity = Vector2.zero; return; }
        var oyuncu = LdrOyun.Ornek != null ? LdrOyun.Ornek.yasayan : null;
        Vector2 v = Vector2.zero;
        bool aktif = Durum == LdrDusmanDurumu.Kovala || Durum == LdrDusmanDurumu.Saldir;
        if (aktif && oyuncu != null && !oyuncu.Can.Olu)
        {
            Vector2 fark = (Vector2)oyuncu.transform.position - (Vector2)transform.position;
            yonYenile -= Time.fixedDeltaTime;
            if (yonYenile <= 0f) { yonCache = Yon(fark); yonYenile = 0.08f; }
            Vector2 dik = new Vector2(-yonCache.y, yonCache.x);

            if (atilmaKalan > 0f) v = atilmaYon * hiz * 2.8f;
            else if (Durum == LdrDusmanDurumu.Saldir)
            {
                float d = fark.magnitude;
                if (Davranis[tur] == Dav.Halka)
                {
                    // mesafeyi korur: çok yakınsa geri çekilir, uzaksa yaklaşır, aralarda yan yan süzülür
                    if (d < 5f)
                    {
                        // arkası duvar/masaysa duvara yaslanıp kalmasın: yana süzülür
                        Vector2 geri = -yonCache;
                        v = YolKapali(geri, R - 0.02f, 0.9f) ? dik * Mathf.Sign(Mathf.Sin(zaman * 0.7f + faz) + 0.001f) * hiz * 0.8f : geri * hiz * 0.9f;
                    }
                    else if (d > 8f) v = yonCache * hiz * 0.7f;
                    else v = dik * Mathf.Sign(Mathf.Sin(zaman * 0.7f + faz)) * hiz * 0.45f;
                }
                else v = yonCache * hiz * 0.5f + dik * Mathf.Sin(zaman * 2f + faz) * hiz * 0.25f;
            }
            else
            {
                v = yonCache * hiz;
                if (Davranis[tur] == Dav.Hizli) v += dik * Mathf.Sin(zaman * 5f + faz) * hiz * 0.7f;       // zikzak
            }
        }
        // takılma: gitmek istediği yöne ilerleyemiyorsa kısa süre yana kayar
        if (aktif && atilmaKalan <= 0f)
        {
            if (kacisKalan > 0f) { kacisKalan -= Time.fixedDeltaTime; v = kacisYon * hiz; }
            else
            {
                float gidilen = ((Vector2)transform.position - takilmaKonum).magnitude;
                if (v.magnitude > 0.2f && gidilen < v.magnitude * Time.fixedDeltaTime * 0.25f) takilma += Time.fixedDeltaTime; else takilma = 0f;
                if (takilma > 0.4f)
                {
                    takilma = 0f; kacisKalan = 0.6f;
                    Vector2 yn = v.normalized;
                    kacisYon = new Vector2(-yn.y, yn.x) * (Random.value < 0.5f ? 1f : -1f);
                }
            }
        }
        takilmaKonum = transform.position;
        rb.linearVelocity = v + itme;
        itme = Vector2.MoveTowards(itme, Vector2.zero, 30f * Time.fixedDeltaTime);
    }

    // Oyuncuya düz giden yol bir engelle kapalı mı? Kapalıysa A* yol bulmaya geçilir.
    bool YolKapali(Vector2 a, float r, float mesafe)
    {
        var cf = new ContactFilter2D();
        cf.SetLayerMask(engelMaskesi);
        cf.useTriggers = false;
        int n = Physics2D.CircleCast(transform.position, r, a, cf, sonuc, mesafe);
        for (int i = 0; i < n; i++)
        {
            var k = sonuc[i].collider;
            if (k == null || k.gameObject == gameObject || k.transform.IsChildOf(transform)) continue;
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- saldırılar

    // ---- ateş döngüsü: bekleme -> ateş dönemi -> bekleme ... Dönem boyunca silahın hızıyla ateş eder, şarjör bitince doldurur.
    // Sürünen dışındakiler yalnızca oyuncuyu görüyorsa (arada duvar yoksa) ateş eder.
    // Tür başına (bekleme, ateş süresi): ritimler birbirinden ayrışsın diye seçildi; ağır yaratıklar seyrek ama uzun ateş eder.
    static readonly Vector2[] BeklemeAraligi = { new Vector2(1.2f, 1.8f), new Vector2(1.6f, 2.6f), new Vector2(0.9f, 1.4f), new Vector2(2f, 3.5f), new Vector2(1.5f, 2.5f) };
    static readonly Vector2[] AtesSuresi = { new Vector2(3f, 4.5f), new Vector2(3.5f, 5f), new Vector2(4f, 6f), new Vector2(2f, 3f), new Vector2(5f, 7f) };
    static readonly string[] AtesSilahi = { "tabanca", "pompali", "seri", "girdap", "sarmal" };
    static readonly string[] SilahCizim = { "gun_pistol", "gun_shotgun", "gun_smg", null, null };    // elde görünen silah (Bakıcı ve Sürünen toplarını gövdesinden atar)
    const float AtesMenzili = 9f;      // yakın dövüşenler (Ağıtçı, Sürünen) bu mesafeden itibaren de ateş eder
    SpriteRenderer silahSr;
    Transform silahTr;
    /// <summary>Düşman ateş sıklığı çarpanı (1 = tablodaki hız). Büyütmek ateşi yavaşlatır.</summary>
    const float AtesYavaslik = 1f;
    bool atesiyor;
    float atesDonemKalan = -1f;
    int sarjorKalan = -1;

    void AtesGuncelle(Vector2 fark, float mesafe)
    {
        if (boss) { BossAtesi(fark, mesafe); return; }
        float dt = Time.deltaTime;
        if (atesDonemKalan < 0f)
        {
            // ilk bekleme: oyuncuyu görmeye başladığı andan itibaren
            atesDonemKalan = Random.Range(BeklemeAraligi[tur].x, BeklemeAraligi[tur].y);
            atesiyor = false;
        }
        atesDonemKalan -= dt;
        if (atesDonemKalan <= 0f)
        {
            atesiyor = !atesiyor;
            atesDonemKalan = atesiyor ? Random.Range(AtesSuresi[tur].x, AtesSuresi[tur].y) : Random.Range(BeklemeAraligi[tur].x, BeklemeAraligi[tur].y);
            ateBekleme = 0f;
        }
        if (!atesiyor) { ateBekleme = Mathf.Max(ateBekleme, 0.5f); return; }

        ateBekleme -= dt;
        if (ateBekleme > 0f) return;
        if (Davranis[tur] != Dav.Atilan && !Gorus(fark, mesafe)) { ateBekleme = 0.1f; return; }

        var t = LdrSilahTablosu.Dusman(AtesSilahi[tur]);
        if (!t.sinirsizSarjor)
        {
            if (sarjorKalan < 0) sarjorKalan = t.sarjor;
            if (sarjorKalan <= 0) { sarjorKalan = t.sarjor; ateBekleme = t.doldurmaSuresi * AtesYavaslik; return; }
            sarjorKalan--;
        }
        ateBekleme = t.atesAraligi * AtesYavaslik;
        Vector2 yon = fark.normalized;
        Vector2 namlu = (Vector2)transform.position + yon * (R + 0.25f);
        if (silahTr != null) namlu = (Vector2)silahTr.position + yon * 0.55f;      // mermi silahın ucundan çıkar
        string silahAdi = AtesSilahi[tur];
        if (silahAdi == "girdap" || silahAdi == "sarmal")
        {
            // desenli atış: tek mermi değil, dönen top deseni (Bakıcı sarı, Sürünen yeşil)
            LdrKume.Ates(namlu, yon, silahAdi == "girdap", t.mermi.hasar);
            LdrSes.CalSeyrek("sfx_shoot", 0.07f, 0.45f, 0.6f);
            return;
        }
        switch (Davranis[tur])
        {
            case Dav.Atesci:
            case Dav.Yelpaze:
            case Dav.Hizli:
            case Dav.Atilan:
            {
                // silahın kendi mermi sayısı ve sapması (pompalıda 6-8 saçma)
                int adet = Random.Range(t.mermi.adet.x, t.mermi.adet.y + 1);
                for (int i = 0; i < adet; i++)
                {
                    float sapma = Random.Range(t.mermi.sapma.x, Mathf.Max(t.mermi.sapma.x, t.mermi.sapma.y)) * (Random.value < 0.5f ? -1f : 1f);
                    LdrMermi.Ates(namlu, Dondur(yon, sapma), t.mermi, 1);
                }
                LdrSes.CalSeyrek("sfx_shoot", 0.07f, adet > 1 ? 0.5f : 0.3f, 0.75f);
                if (adet > 1) LdrKamera.Salla(0.03f);
                break;
            }
            case Dav.Halka:
                for (int i = 0; i < 8; i++) Ates((Vector2)transform.position + Dondur(Vector2.right, halkaAci + i * 45f) * (Yaricap[tur] + 0.2f), Dondur(Vector2.right, halkaAci + i * 45f));
                halkaAci += 15f;
                break;
        }
    }

    /// <summary>Boss ateşi: 6-8 sn ateş dönemi, 1,5-2,5 sn dinlenme; silah tablosundaki hız, şarjör ve mermi sayısıyla.</summary>
    void BossAtesi(Vector2 fark, float mesafe)
    {
        donemKalan -= Time.deltaTime;
        if (donemKalan <= 0f)
        {
            atesDonemi = !atesDonemi;
            donemKalan = atesDonemi ? Random.Range(6f, 8f) : Random.Range(1.5f, 2.5f);
        }
        if (!atesDonemi) return;
        bossAtesBekle -= Time.deltaTime;
        if (bossAtesBekle > 0f) return;
        if (!Gorus(fark, mesafe)) { bossAtesBekle = 0.2f; return; }
        var t = LdrSilahTablosu.Dusman(bossSilah ?? "tabancapatron");
        if (!t.sinirsizSarjor)
        {
            if (bossSarjor <= 0) { bossSarjor = t.sarjor; bossAtesBekle = t.doldurmaSuresi; return; }
            bossSarjor--;
        }
        bossAtesBekle = t.atesAraligi;
        Vector2 yon = fark.normalized;
        Vector2 namlu = (Vector2)transform.position + yon * (R + 0.3f);
        if (silahTr != null) namlu = (Vector2)silahTr.position + yon * 0.8f;
        int adet = Random.Range(t.mermi.adet.x, t.mermi.adet.y + 1);
        for (int i = 0; i < adet; i++)
        {
            float sapma = Random.Range(t.mermi.sapma.x, Mathf.Max(t.mermi.sapma.x, t.mermi.sapma.y)) * (Random.value < 0.5f ? -1f : 1f);
            LdrMermi.Ates(namlu, Dondur(yon, sapma), t.mermi, 1);
        }
        LdrSes.CalSeyrek("sfx_shoot", 0.07f, adet > 1 ? 0.6f : 0.35f, 0.7f);
        if (adet > 1) LdrKamera.Salla(0.04f);
    }

    static Vector2 Dondur(Vector2 v, float derece) { return Quaternion.Euler(0f, 0f, derece) * (Vector3)v; }

    void Ates(Vector2 konum, Vector2 yon)
    {
        var t = LdrSilahTablosu.Dusman(SilahAnahtari[tur] ?? "tabanca").mermi;
        LdrMermi.Ates(konum, yon, t, 1);
        LdrSes.CalSeyrek("sfx_shoot", 0.08f, 0.3f, 0.8f);
    }

    /// <summary>Sürünen: ara sıra durup (ışık parlar) oyuncunun üstüne atılır.</summary>
    void AtilmaGuncelle(Vector2 fark)
    {
        if (atilmaKalan > 0f) { atilmaKalan -= Time.deltaTime; return; }
        atilmaBekleme -= Time.deltaTime;
        if (atilmaBekleme <= 0f && fark.magnitude < 6f && fark.magnitude > 1.5f)
        {
            atilmaYon = fark.normalized;
            atilmaKalan = 0.45f;
            atilmaBekleme = Random.Range(2.8f, 4.2f);
            LdrKamera.Salla(0.03f);
        }
    }

    // Oyuncuyu görüyor mu? Arada duvar/masa var mı diye ışın (Raycast) atar.
    bool Gorus(Vector2 fark, float mesafe)
    {
        if (duvarMaskesi == 0) return true;
        return Physics2D.Raycast(transform.position, fark / Mathf.Max(mesafe, 0.001f), mesafe, duvarMaskesi).collider == null;
    }

    /// <summary>
    /// Oyuncuya doğru yön. Doğrudan yol açıksa düz gider; masa, heykel ya da duvar kapatıyorsa A* ile (LdrYol)
    /// dolanır. Yol bulunamazsa yedi açı denemesine döner.
    /// </summary>
    Vector2 Yon(Vector2 fark)
    {
        Vector2 d = fark.normalized;
        if (engelMaskesi == 0) return d;
        float r = R - 0.02f;
        float mes = Mathf.Min(fark.magnitude, 14f);
        if (!YolKapali(d, r, mes)) { yol.Clear(); return d; }

        var o = LdrOyun.Ornek;
        var oda = o != null && o.kat != null ? o.kat.Gecerli : null;
        Vector2 ben = transform.position, oy = ben + fark;
        yolZaman += 0.08f;
        if (oda != null && (yol.Count == 0 || yolZaman > 0.6f || (oy - yolHedef).sqrMagnitude > 4f))
        {
            yolZaman = 0f; yolHedef = oy;
            LdrYol.Bul(oda, ben, oy, R, yol);
        }
        if (yol.Count > 0)
        {
            // yakın noktaları atla; bir sonraki nokta doğrudan görünüyorsa arayı kısalt
            while (yol.Count > 1 && ((yol[0] - ben).sqrMagnitude < 0.36f || !YolKapali((yol[1] - ben).normalized, r, Mathf.Min((yol[1] - ben).magnitude, 3f)))) yol.RemoveAt(0);
            Vector2 h = yol[0] - ben;
            if (h.sqrMagnitude < 0.09f && yol.Count == 1) { yol.Clear(); return d; }
            if (h.sqrMagnitude > 0.0001f) return h.normalized;
        }
        float[] acilar = { 0f, 40f, -40f, 80f, -80f, 120f, -120f };
        for (int i = 0; i < acilar.Length; i++)
        {
            Vector2 a = Quaternion.Euler(0f, 0f, acilar[i]) * (Vector3)d;
            if (!YolKapali(a, r, 0.9f)) return a;
        }
        return d;
    }

    // ---------------------------------------------------------------- hasar ve ölüm

    // Hasar alınca: kısa sendeleme durumuna geçer; uyuyorsa uyanır.
    void Vuruldu(LdrCan c, Vector2 kaynak)
    {
        kirmizi = 0.1f;
        LdrSes.CalSeyrek("sfx_enemy_hit", 0.05f, 0.5f);
        Vector2 uzak = (Vector2)transform.position - kaynak;
        if (uzak.sqrMagnitude < 0.0001f) uzak = Random.insideUnitCircle;
        itme = uzak.normalized * (boss ? 0.8f : (tur == KizilSizinti ? 2.5f : 5f));
        atilmaKalan = 0f;
        if (Can.Olu || Can.Simdiki <= 0) return;
        LdrVfx.Burst(transform.position, Color.Lerp(Parilti[tur], Color.white, 0.5f), new Color(0.6f, 0.05f, 0.1f), 7, 2.6f, 0.1f, 0.45f, 0.5f, 0, 14);
        if (!boss) Gir(LdrDusmanDurumu.Sendeler);
    }

    // Can bitince: ölüm animasyonu, ruh kazancı (LdrPuan.Kazan = can x zincir), ölüm patlaması ve ses.
    void Oldu(LdrCan c)
    {
        Durum = LdrDusmanDurumu.Oldu;
        LdrPuan.Kazan(Mathf.RoundToInt(Can.azami));     // puan = başlangıç canı x ZİNCİR
        LdrSes.Cal("sfx_enemy_death", 0.7f);
        foreach (var k in GetComponents<Collider2D>()) k.enabled = false;
        rb.linearVelocity = Vector2.zero;
        if (isik != null) isik.enabled = false;
        if (canCubuguKok != null) canCubuguKok.gameObject.SetActive(false);
        // ölüm sahnesi: kan parçacıkları fırlar, ardından ruh yukarı süzülür
        Vector3 p = transform.position;
        Color p1 = Parilti[tur];
        LdrVfx.Burst(p, new Color(0.55f, 0.02f, 0.08f), Color.Lerp(p1, new Color(0.4f, 0.02f, 0.06f), 0.4f), 30, 4.2f, 0.17f, 0.9f, 0.9f, 0, 14);
        LdrVfx.Burst(p + Vector3.up * 0.2f, Color.white, p1, 12, 0.7f, 0.12f, 1.8f, -0.25f, 0, 15);
        LdrKamera.Salla(tur == KizilSizinti ? 0.2f : 0.1f);
        StartCoroutine(Sol());
    }

    System.Collections.IEnumerator Sol()
    {
        float t = 0f;
        Vector3 b = govde.localScale;
        if (anim != null) anim.Int("Durum", (int)LdrDusmanDurumu.Oldu);
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float k = t / 0.3f;
            // ezilme Animator'daki "Oldu" klibinden gelir; Animator yoksa kodla
            govde.localScale = anim != null ? new Vector3(b.x * anim.olcekX, b.y * anim.olcekY, 1f) : new Vector3(b.x * (1f + k * 0.4f), b.y * (1f - k), 1f);
            govdeSr.color = new Color(1f, 0.3f, 0.3f, 1f - k);
            yield return null;
        }
        Destroy(gameObject);
    }
}
