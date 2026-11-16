#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Yalnızca editörde çalışan fragman kayıt aracı (oyunun parçası değildir, derlenmiş oyuna girmez).
/// Ldr > Fragman kaydı menüsüyle başlar: menüleri gezer, oyunu kendi oynar (otomatik pilot), sandık açar,
/// boss'a girer ve sonunda ölür. Her sahneyi 30 fps sabit zamanla JPG kareler halinde
/// proje kökündeki FragmanKayit klasörüne yazar (her dosya: [4 bayt uzunluk][JPG] ...).
/// </summary>
public class LdrFragman : MonoBehaviour
{
    const string Bayrak = "LDR_Fragman";
    const int Fps = 30;

    [MenuItem("Ldr/Fragman kaydı (oyunu kendi oynatır)")]
    static void Baslat()
    {
        SessionState.SetBool(Bayrak, true);
        EditorApplication.isPlaying = true;
    }

    /// <summary>Büyük kayıt dosyalarını ~300 MB'lık parçalara böler (aktarım kolaylığı için).</summary>
    [MenuItem("Ldr/Fragman dosyalarını parçala")]
    static void Parcala()
    {
        string kl = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "FragmanKayit"));
        foreach (var f in Directory.GetFiles(kl, "*.bin"))
        {
            if (f.Contains("_p") || new FileInfo(f).Length < 350000000L) continue;
            using (var g = File.OpenRead(f))
            {
                int parca = 0;
                long yazilan = 0;
                FileStream c = null;
                var u = new byte[4];
                while (g.Read(u, 0, 4) == 4)
                {
                    int n = System.BitConverter.ToInt32(u, 0);
                    var b = new byte[n];
                    int okunan = 0;
                    while (okunan < n) { int r = g.Read(b, okunan, n - okunan); if (r <= 0) break; okunan += r; }
                    if (c == null || yazilan > 300000000L)
                    {
                        if (c != null) c.Dispose();
                        c = File.Create(f.Substring(0, f.Length - 4) + "_p" + parca++ + ".bin");
                        yazilan = 0;
                    }
                    c.Write(u, 0, 4);
                    c.Write(b, 0, n);
                    yazilan += n + 4;
                }
                if (c != null) c.Dispose();
                Debug.Log("[Fragman] " + Path.GetFileName(f) + " -> " + parca + " parça");
            }
        }
    }

    [MenuItem("Ldr/Fragman kaydı - yalnızca ölüm sahnesi")]
    static void BaslatOlum()
    {
        SessionState.SetBool(Bayrak, true);
        SessionState.SetBool(Bayrak + "_olum", true);
        EditorApplication.isPlaying = true;
    }

    static bool yalnizOlum;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Kur()
    {
        if (!SessionState.GetBool(Bayrak, false)) return;
        SessionState.SetBool(Bayrak, false);
        yalnizOlum = SessionState.GetBool(Bayrak + "_olum", false);
        SessionState.SetBool(Bayrak + "_olum", false);
        var go = new GameObject("Fragman");
        DontDestroyOnLoad(go);
        go.AddComponent<LdrFragman>();
    }

    // ---------------------------------------------------------------- kayıt
    string klasor;
    FileStream akis;
    string akisAdi;
    int kare;
    Texture2D tampon;

    void Kaydet(string ad)
    {
        Durdur();
        akisAdi = ad;
        akis = new FileStream(Path.Combine(klasor, ad + ".bin"), FileMode.Create, FileAccess.Write);
        kare = 0;
    }

    void Durdur()
    {
        if (akis == null) return;
        akis.Dispose();
        akis = null;
        Debug.Log("[Fragman] " + akisAdi + ": " + kare + " kare");
    }

    IEnumerator Yakala()
    {
        var son = new WaitForEndOfFrame();
        while (true)
        {
            yield return son;
            if (akis == null) continue;
            int w = Screen.width, h = Screen.height;
            if (tampon == null || tampon.width != w || tampon.height != h)
            {
                if (tampon != null) Destroy(tampon);
                tampon = new Texture2D(w, h, TextureFormat.RGB24, false);
            }
            tampon.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);   // ekranın son hali (oyun + arayüz)
            tampon.Apply(false);
            byte[] b = tampon.EncodeToJPG(92);
            akis.Write(System.BitConverter.GetBytes(b.Length), 0, 4);
            akis.Write(b, 0, b.Length);
            kare++;
        }
    }

    static IEnumerator Sn(float saniye)
    {
        int n = Mathf.RoundToInt(saniye * Fps);
        for (int i = 0; i < n; i++) yield return null;
    }

    // ---------------------------------------------------------------- senaryo
    void Start()
    {
        klasor = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "FragmanKayit"));
        Directory.CreateDirectory(klasor);
        Time.captureFramerate = Fps;
        LdrIlerleme.HepsiAcik = true;      // pazarda ve oyunda farklı silahları göstermek için (yalnızca bu oturum)
        StartCoroutine(Yakala());
        StartCoroutine(Senaryo());
    }

    void OnDestroy()
    {
        Durdur();
        PilotKapat();
        Time.captureFramerate = 0;
        LdrIlerleme.HepsiAcik = false;
    }

    static void MenuCagir(string yontem)
    {
        var m = typeof(LdrMenu).GetMethod(yontem, BindingFlags.Instance | BindingFlags.NonPublic);
        m.Invoke(LdrMenu.Ornek, null);
    }

    static void MenuAlan(string ad, object deger)
    {
        typeof(LdrMenu).GetField(ad, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, deger);
    }

    IEnumerator Senaryo()
    {
        yield return Sn(1.5f);                      // menü ve közler otursun

        if (yalnizOlum)
        {
            yield return Oyna(1, "08_olum_giris", 14f);
            yield return Olum("09_olum");
            Durdur();
            EditorApplication.isPlaying = false;
            yield break;
        }

        Kaydet("01_menu");
        yield return Sn(7f);
        LdrMenu.Gecerli = LdrMenu.Sayfa.Karakter;
        Kaydet("02_karakter");
        yield return Sn(4f);
        LdrMenu.Gecerli = LdrMenu.Sayfa.Pazar;
        Kaydet("03_pazar");
        yield return Sn(5f);
        Durdur();

        yield return Oyna(0, "04_bolum1", 80f);
        yield return Oyna(2, "05_bolum3", 50f);
        yield return Boss("06_boss", 26f);
        yield return Olum("07_olum");

        Durdur();
        Debug.Log("[Fragman] Bitti: " + klasor);
        EditorApplication.isPlaying = false;
    }

    IEnumerator SahneBekle()
    {
        var eski = LdrOyun.Ornek;
        while (LdrOyun.Ornek == null || LdrOyun.Ornek == eski || LdrOyun.Ornek.yasayan == null || LdrOyun.Ornek.kat == null) yield return null;
    }

    IEnumerator Oyna(int bolum, string ad, float sure)
    {
        PilotKapat();
        LdrIlerleme.Karakter = 0;
        MenuAlan("karakterSecildi", true);
        MenuAlan("secilenBolum", bolum);
        MenuCagir("OyunaGec");
        Kaydet(ad);
        yield return SahneBekle();
        silahSirasi = bolum == 0 ? 0 : 3;
        yield return Sn(3.4f);                      // "BÖLÜM n" yazısı ve kararma
        PilotAc(true);
        yield return Sn(sure);
    }

    IEnumerator Boss(string ad, float sure)
    {
        var o = LdrOyun.Ornek;
        var hedef = o.kat.CikisOdasi;
        PilotAc(true);
        dolasma = false;                            // boss odasından çıkmasın
        o.kat.Isinla(hedef.EnYakinZemin(hedef.merkez + new Vector2(0f, -3f)), hedef);
        yield return Sn(0.6f);
        SilahTak("GECE TABANCASI");
        Kaydet(ad);
        yield return Sn(sure);
    }

    IEnumerator Olum(string ad)
    {
        Kaydet(ad);
        PilotKapat();                               // durur, ateş etmez, takla atmaz; tanrı modu da kapanır
        yield return Sn(1.2f);
        var y = LdrOyun.Ornek.yasayan;
        int deneme = 0;
        var bf = BindingFlags.Instance | BindingFlags.NonPublic;
        while (!y.Can.Olu && deneme++ < Fps * 10)
        {
            // koruma kaynaklarını sıfırla: vuruş sonrası koruma, takla, gölge kalkanı
            typeof(LdrCan).GetField("korumaKalan", bf).SetValue(y.Can, 0f);
            typeof(LdrYasayan).GetField("taklaZaman", bf).SetValue(y, 0f);
            if (y.Buflar != null) y.Buflar.Kalkan = 0f;
            if (deneme % 30 == 1) Debug.Log("[Fragman] ölüm denemesi: korumali=" + y.Can.Korumali + " can=" + y.Can.Simdiki + " donuk=" + y.Donuk);
            Vector2 kaynak = hedefDusman != null ? (Vector2)hedefDusman.transform.position : (Vector2)y.transform.position + Vector2.up;
            y.Can.Hasar(y.Can.Simdiki + 1f, kaynak);
            yield return null;
        }
        yield return Sn(9f);                        // ÖLDÜN ekranı + yeniden doğuş pazarı
    }

    // ---------------------------------------------------------------- otomatik pilot
    bool pilot, tanri, dolasma = true, ateslesin = true;
    bool taklaIstek, etkIstek;
    Vector2 yon;
    Vector2? nisan;
    LdrDusman hedefDusman;
    readonly List<Vector2> yol = new List<Vector2>();
    float yolSayac, taklaSayac, bosSayac, takilmaSayac, kacisSayac;
    Vector2 sonKonum, kacisYon;
    int silahSirasi;
    readonly HashSet<Object> kullanilan = new HashSet<Object>();
    static readonly string[] SilahGosterimi = { "SÜRGÜN REVOLVERİ", "TIRNAK", "GECE TABANCASI", "HAYALET IŞINI", "GÖZCÜ NİŞANCI", "RUH TOPU", "ROKETATAR" };

    void PilotAc(bool tanriModu)
    {
        pilot = true; tanri = tanriModu; dolasma = true; ateslesin = true;
        LdrGiris.PilotYon = () => yon;
        LdrGiris.PilotAtes = () => ateslesin && hedefDusman != null && GorusVar();
        LdrGiris.PilotTakla = () => { bool b = taklaIstek; taklaIstek = false; return b; };
        LdrGiris.PilotEtkilesim = () => { bool b = etkIstek; etkIstek = false; return b; };
        LdrGiris.PilotHedef = () => nisan;
    }

    void PilotKapat()
    {
        pilot = false;
        yon = Vector2.zero;
        LdrGiris.PilotYon = () => Vector2.zero;
        LdrGiris.PilotAtes = () => false;
        LdrGiris.PilotTakla = () => false;
        LdrGiris.PilotEtkilesim = () => false;
        LdrGiris.PilotHedef = () => nisan;
    }

    void SilahTak(string ad)
    {
        var o = LdrOyun.Ornek;
        if (o == null || o.yasayan == null) return;
        var t = LdrSilahTablosu.Oyuncu.Find(s => s.ad == ad);
        if (t != null) o.yasayan.Silah.SilahSec(t);
    }

    bool GorusVar()
    {
        var o = LdrOyun.Ornek;
        if (o == null || hedefDusman == null) return false;
        Vector2 a = o.yasayan.transform.position, b = hedefDusman.transform.position;
        return !Physics2D.Linecast(a, b, LayerMask.GetMask("Duvar"));
    }

    void Update()
    {
        if (!pilot) return;
        var o = LdrOyun.Ornek;
        if (o == null || o.yasayan == null || o.kat == null) return;
        var y = o.yasayan;
        var kat = o.kat;
        var oda = kat.Gecerli;
        float dt = Time.deltaTime;
        Vector2 p = y.transform.position;

        if (tanri && !y.Can.Olu && y.Can.Oran < 0.55f) y.Can.Doldur();
        if (y.Can.Olu || kat.Gecis || oda == null) { yon = Vector2.zero; return; }

        // en yakın canlı düşman
        hedefDusman = null;
        float en = float.MaxValue;
        foreach (var d in LdrDusman.Hepsi)
        {
            if (d == null || d.Can == null || d.Can.Olu || !oda.Icinde(d.transform.position)) continue;
            float m = ((Vector2)d.transform.position - p).sqrMagnitude;
            if (m < en) { en = m; hedefDusman = d; }
        }

        Vector2 hedefNokta = p;
        nisan = null;
        bool hareket = true;

        if (hedefDusman != null)
        {
            bosSayac = 0f;
            Vector2 h = hedefDusman.transform.position;
            nisan = h + new Vector2(0f, 0.3f);
            float uzak = Mathf.Sqrt(en);
            Vector2 disari = uzak > 0.01f ? (p - h) / uzak : Vector2.right;
            if (uzak < 3.2f) hedefNokta = p + disari * 3f;                              // çok yakın: geri çekil
            else hedefNokta = h + (Vector2)(Quaternion.Euler(0f, 0f, 28f) * disari) * 6f;   // çevresinde dön
            taklaSayac += dt;
            if (taklaSayac > 2.6f && (uzak < 4.5f || Random.value < 0.01f)) { taklaIstek = true; taklaSayac = 0f; }
            // yakındaki masayı devir (siper)
            if (o.Ipucu is LdrMasa masa && !kullanilan.Contains(masa)) { kullanilan.Add(masa); etkIstek = true; }
        }
        else
        {
            // sandık, kaçış parçası, eşya; yoksa sıradaki odaya geç
            Component ilgi = null;
            float ie = float.MaxValue;
            foreach (var s in FindObjectsByType<LdrSandik>(FindObjectsSortMode.None))
            {
                if (!s.Hazir || !oda.Icinde(s.transform.position)) continue;
                float m = ((Vector2)s.transform.position - p).sqrMagnitude;
                if (m < ie) { ie = m; ilgi = s; }
            }
            if (ilgi == null)
                foreach (var s in FindObjectsByType<LdrParcaNesnesi>(FindObjectsSortMode.None))
                    if (oda.Icinde(s.transform.position)) { ilgi = s; break; }
            if (ilgi == null)
                foreach (var s in FindObjectsByType<LdrEsya>(FindObjectsSortMode.None))
                {
                    if (!oda.Icinde(s.transform.position)) continue;
                    float m = ((Vector2)s.transform.position - p).sqrMagnitude;
                    if (m < ie) { ie = m; ilgi = s; }
                }

            if (ilgi != null && bosSayac < 8f)
            {
                bosSayac += dt;
                Vector2 hp = ilgi.transform.position;
                if (ilgi is LdrSandik sd)
                {
                    hp = (Vector2)sd.transform.position + new Vector2(0f, -0.9f);
                    nisan = sd.Konum;
                    if (Vector2.Distance(p, hp) < 1.3f && ReferenceEquals(o.Ipucu, sd)) etkIstek = true;
                }
                hedefNokta = hp;
            }
            else
            {
                hareket = false;
                bosSayac += dt;
                if (bosSayac > 1.4f && dolasma) { bosSayac = 0f; SonrakiOda(kat, p); }
            }
        }

        if (!hareket) { yon = Vector2.zero; return; }

        // takıldıysa kısa süre rastgele yöne kaç
        if (kacisSayac > 0f) { kacisSayac -= dt; yon = kacisYon; return; }
        takilmaSayac = (p - sonKonum).sqrMagnitude < 0.0004f && (hedefNokta - p).sqrMagnitude > 1f ? takilmaSayac + dt : 0f;
        sonKonum = p;
        if (takilmaSayac > 0.6f) { takilmaSayac = 0f; kacisSayac = 0.35f; kacisYon = Random.insideUnitCircle.normalized; }

        hedefNokta = oda.EnYakinZemin(hedefNokta);
        yolSayac -= dt;
        if (yolSayac <= 0f) { yolSayac = 0.25f; if (!LdrYol.Bul(oda, p, hedefNokta, 0.45f, yol)) yol.Clear(); }
        while (yol.Count > 0 && Vector2.Distance(yol[0], p) < 0.35f) yol.RemoveAt(0);
        Vector2 ara = yol.Count > 0 ? yol[0] : hedefNokta;
        Vector2 fark = ara - p;
        yon = fark.sqrMagnitude > 0.04f ? fark.normalized : Vector2.zero;
        if (nisan == null && yon != Vector2.zero) nisan = p + yon * 4f;
    }

    void SonrakiOda(LdrKat kat, Vector2 p)
    {
        LdrOda sec = null;
        float en = float.MaxValue;
        foreach (var r in kat.odalar)
        {
            if (r == kat.Gecerli || r == kat.CikisOdasi) continue;
            bool ilginc = (r.tur == LdrOdaTuru.Savas && !r.temiz) || (r.tur == LdrOdaTuru.Sandik && !r.ziyaret);
            if (!ilginc) continue;
            float m = (r.merkez - p).sqrMagnitude;
            if (m < en) { en = m; sec = r; }
        }
        if (sec == null) return;
        yol.Clear();
        kat.Isinla(sec.EnYakinZemin(sec.merkez), sec, () =>
        {
            SilahTak(SilahGosterimi[silahSirasi % SilahGosterimi.Length]);
            silahSirasi++;
        });
    }
}
#endif
