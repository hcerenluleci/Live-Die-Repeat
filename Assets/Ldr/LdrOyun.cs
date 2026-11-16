using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Oyun kurucusu. "LdrOyun" sahnesinde tek bir nesneye eklenir; katı, oyuncuyu, kamerayı, ışığı ve ekranı kurar.
/// Akış: DERİNLİK 1-4 + SON UYANIŞ. Ölünce LiveDieRepeat "ÖLDÜN" ekranını gösterip sahneyi yeniden yükler (ölüm döngüsü);
/// toplanan kaçış parçaları ölümde silinmez.
/// </summary>
public class LdrOyun : MonoBehaviour
{
    public static LdrOyun Ornek { get; private set; }

    public const int SonDerinlik = 4;                 // 0..3 = DERİNLİK 1..4, 4 = SON UYANIŞ
    public static int Derinlik;                       // sahne yenilense de korunur; ölünce sıfırlanır

    public LdrKat kat;
    public LdrYasayan yasayan;
    public LdrIsinlanma isinlanma;
    public ILdrEtkilesim Ipucu { get; private set; }

    /// <summary>Şu an oda savaşı sürüyor mu (dalga bitmedi, oyuncu yaşıyor)? Müzik savaş döngüsüne geçsin diye kullanılır.</summary>
    public bool SavasVar
    {
        get { return kat != null && kat.Gecerli != null && kat.Gecerli.dalga != null && !kat.Gecerli.temiz && yasayan != null && !yasayan.Can.Olu; }
    }

    LdrKamera kamera;
    bool gecisliyor;
#if UNITY_EDITOR
    bool testHedef;
#endif

    /// <summary>Oyundaki bölüm sırası (0..4): İLK ÖLÜM, HATIRLADIKLARIN, DÖNGÜ SIKILAŞIR, ÇIKIŞ YOK, SON UYANIŞ.</summary>
    public static readonly string[] BolumAdlari = { "İLK ÖLÜM", "HATIRLADIKLARIN", "DÖNGÜ SIKILAŞIR", "ÇIKIŞ YOK", "SON UYANIŞ" };

    public static string DerinlikAdi(int d)
    {
        return BolumAdlari[Mathf.Clamp(d, 0, SonDerinlik)];
    }

    void Awake()
    {
        Ornek = this;
        Time.timeScale = 1f;
        LdrEtkilesim.Temizle();
        LdrHud.Sifirla();
        LdrPuan.Sifirla();

        var kam = Camera.main;
        if (kam == null)
        {
            var kg = new GameObject("Main Camera");
            kg.tag = "MainCamera";
            kam = kg.AddComponent<Camera>();
            kam.clearFlags = CameraClearFlags.SolidColor;
            kam.backgroundColor = Color.black;
            kg.transform.position = new Vector3(0, 0, -10);
            kg.AddComponent<AudioListener>();
        }
        kamera = kam.GetComponent<LdrKamera>();
        if (kamera == null) kamera = kam.gameObject.AddComponent<LdrKamera>();

        bool genelIsikVar = false;
        foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) genelIsikVar = true;
        if (!genelIsikVar)
        {
            var ig = new GameObject("GenelIsik");
            var l = ig.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Global;
            l.intensity = 0.9f;
        }

        Derinlik = Mathf.Clamp(Derinlik, 0, SonDerinlik);
        yasayan = LdrYasayan.Olustur(Vector2.zero);
        kamera.hedef = yasayan.transform;
        isinlanma = gameObject.AddComponent<LdrIsinlanma>();
        gameObject.AddComponent<LdrDuraklat>();
        gameObject.AddComponent<LdrHarita>();
        gameObject.AddComponent<LdrFener>();
        gameObject.AddComponent<LdrEfekt>();
        KatKur(Derinlik);
        StartCoroutine(BolumGirisi(Derinlik));

        LdrHud.Kur(yasayan);
        yasayan.Can.Oldu += OyuncuOldu;
    }

    void KatKur(int d)
    {
        if (kat != null)
        {
            kat.gameObject.SetActive(false);
            Destroy(kat.gameObject);
        }
        LdrEtkilesim.Temizle();
        foreach (var m in FindObjectsByType<LdrMermi>(FindObjectsSortMode.None)) Destroy(m.gameObject);

        kat = LdrKat.Kur(transform, d, Random.Range(1, 1000000), yasayan, kamera);
        yasayan.Konumla(kat.Baslangic.baslangic);
        kamera.sinir = kat.Sinir;
        kamera.Atla();
        LdrIlerleme.DerinlikBildir(d);
    }

    /// <summary>Çıkış geçidinden çağrılır: bir alt derinliğe iner; son derinlikteyse oyunu bitirir.</summary>
    public void Indi()
    {
        if (gecisliyor || yasayan.Can.Olu) return;
        StartCoroutine(IndiYap());
    }

    IEnumerator IndiYap()
    {
        gecisliyor = true;
        yasayan.Donuk = true;
        yield return Karart(0f, 1f, 0.4f);

        if (Derinlik >= SonDerinlik)
        {
            yield return SonYap();
            yield break;
        }

        Derinlik++;
        yasayan.Can.Iyiles(2);
        yasayan.Silah.Sifirla();
        KatKur(Derinlik);
        yield return null;
        yield return BolumGirisi(Derinlik);
        gecisliyor = false;
    }

    /// <summary>Bölüm girişi: siyah ekranda "BÖLÜM n" + bölüm adı (oyuncu donuk, 1,8 sn), sonra kan kırmızısı perde 1,5 sn'de açılır.</summary>
    IEnumerator BolumGirisi(int d)
    {
        LdrHud.KarartmaRenk = Color.black;
        LdrHud.Karartma = 1f;
        LdrHud.OrtaYazi = "BÖLÜM " + (d + 1) + "\n\n" + DerinlikAdi(d);
        yasayan.Donuk = true;
        while (LdrMenu.Acik) yield return null;      // menü açıksa (ilk açılış) yazı menü kapanınca başlar
        yield return new WaitForSecondsRealtime(1.8f);
        LdrHud.OrtaYazi = null;
        if (!yasayan.Can.Olu) yasayan.Donuk = false;
        LdrHud.KarartmaRenk = new Color(0.55f, 0.02f, 0.08f);
        yield return Karart(1f, 0f, 1.5f);
        LdrHud.KarartmaRenk = Color.black;
    }

    // [Mekanik 3] Son bölümün çıkışına girilince: 4 kaçış parçası tamamsa "KAÇTIN" (oyun kazanıldı),
    // eksikse "ÇIKIŞ MÜHÜRLÜ" yazar ve oyuncu 1. bölüme geri döner (döngü devam eder).
    IEnumerator SonYap()
    {
        YasamiKaydet();
        bool kactin = LdrParca.Sayi() >= LdrParca.Gerekli;   // kazanma koşulu: 4 parçanın hepsi
        if (kactin)
        {
            PlayerPrefs.SetInt("LDR_Escapes", PlayerPrefs.GetInt("LDR_Escapes", 0) + 1);
            LdrParca.Sifirla();
            PlayerPrefs.Save();
            LdrHud.SonYazi = "KAÇTIN";
            LdrHud.SonAlt = "ÖLÜM SAYISI  " + LiveDieRepeat.Deaths + "   -   YAŞA. ÖL. TEKRARLA.";
            LdrHud.SonRenk = new Color(1f, 0.9f, 0.5f);
        }
        else
        {
            LdrHud.SonYazi = "ÇIKIŞ MÜHÜRLÜ";
            LdrHud.SonAlt = LdrParca.Sayi() + " / " + LdrParca.Gerekli + " KAÇIŞ PARÇASI  -  GERİ DÖNÜYORSUN";
            LdrHud.SonRenk = LdrUI.Blood;
        }
        LdrHud.Karartma = 0f;
        yield return new WaitForSecondsRealtime(4f);
        Derinlik = 0;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    static IEnumerator Karart(float a, float b, float sure)
    {
        float t = 0f;
        while (t < sure)
        {
            t += Time.unscaledDeltaTime;
            LdrHud.Karartma = Mathf.Lerp(a, b, Mathf.Clamp01(t / sure));
            yield return null;
        }
        LdrHud.Karartma = b;
    }

    /// <summary>Bu yaşamın ruhunu kalıcı toplama ekler ve puan tablosuna yazar.</summary>
    // [Mekanik 1] Son bölüm bitince bu yaşamda kazanılan ruhu kalıcı toplama ekler ve puan tablosuna yazar.
    void YasamiKaydet()
    {
        LdrIlerleme.RuhEkle(LdrPuan.Ruh);
        LdrIlerleme.PuanEkle(LdrIlerleme.KarakterAdi(LdrIlerleme.Karakter), DerinlikAdi(Derinlik), LdrPuan.Ruh);
    }

    // [Mekanik 1] Oyuncunun canı 0 olunca çağrılır: oyuncuyu dondurur, ölüm sesini çalar,
    // derinliği başa (1. bölüm) alır ve ölümü LiveDieRepeat'e bildirir.
    void OyuncuOldu(LdrCan c)
    {
        yasayan.Donuk = true;
        // ruh ve puan tablosu burada değil, ölüm bildirilince LdrMenu.OlumOldu'da kaydedilir (iki kez sayılmasın)
        LdrSes.Cal("sfx_player_death", 0.8f);
        Derinlik = 0;
        LiveDieRepeat.ReportDeath();
    }

    void Update()
    {
        if (yasayan == null || kat == null) return;

        bool aktif = !yasayan.Donuk && !yasayan.Can.Olu && !kat.Gecis && !gecisliyor;
        Ipucu = aktif ? LdrEtkilesim.EnYakin(yasayan.transform.position, 2.2f) : null;
        if (Ipucu != null && LdrGiris.EtkilesimBasildi()) Ipucu.Etkilesim(yasayan);

#if UNITY_EDITOR
        // deneme tuşları (yalnızca editörde): F1 odayı temizle, F2 tüm parçalar, F3 aşağı in, F4 çıkış odasına git
        if (LdrGiris.TestTusu(1) && kat.Gecerli != null && kat.Gecerli.dalga != null) kat.Gecerli.dalga.cikan = kat.Gecerli.dalga.toplam;
        if (LdrGiris.TestTusu(1) && kat.Gecerli != null)
            foreach (var d in kat.Gecerli.dusmanlar) if (d != null && !d.Can.Olu) d.Can.Hasar(999, d.transform.position);
        if (LdrGiris.TestTusu(2)) { for (int i = 0; i < LdrParca.Gerekli; i++) LdrParca.Ekle(i); LdrHud.Duyur("TÜM PARÇALAR (TEST)", Color.white); }
        if (LdrGiris.TestTusu(3)) Indi();
        if (LdrGiris.TestTusu(4) && kat.CikisOdasi != null)
        {
            testHedef = !testHedef;                       // F4: sırayla çıkış odası / sandık odası
            var hedef = testHedef ? kat.CikisOdasi : kat.SandikOdasi;
            kat.Isinla(hedef.baslangic, hedef);
        }
#endif
    }

    void LateUpdate()
    {
        if (kat != null && kat.Gecerli != null && yasayan != null) kat.Gecerli.Derinlik(yasayan.transform.position.y - 0.5f);
    }
}
