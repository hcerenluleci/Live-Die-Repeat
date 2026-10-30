using UnityEngine;

/// <summary>
/// Silah. Ayarlar LdrSilahTablosu'ndan gelir: şarjör + yedek mermi, ateş aralığı,
/// ön şarj (basılı tutunca ilk atıştan önce bekleme), sapma, pellet sayısı, yeniden doldurma.
/// Oyuncu pazarda seçtiği silahla başlar (LdrIlerleme.SeciliSilah). "Silah kasası" eşyası kısa süre pompalıya (GÖLGE TÜFEĞİ) çevirir.
/// </summary>
public class LdrSilah : MonoBehaviour
{
    [Header("Çarpanlar (böğürtlen vb. etkiler)")]
    public float hasarCarpani = 1f;          // böğürtlen: 2
    public float hizCarpani = 1f;            // böğürtlen: 0,6 (daha sık ateş)

    public LdrSilahTanimi Tanim { get; private set; }
    public bool Pompali { get; private set; }
    public int Mermi { get; private set; }               // şarjördeki
    public int Yedek { get; private set; }               // şarjör dışındaki
    public int SarjorBoyu { get { return Tanim != null ? Tanim.sarjor : 0; } }
    public bool Sinirsiz { get { return Tanim != null && Tanim.sinirsizMermi; } }
    public bool Dolduruyor { get { return doldurKalan > 0f; } }
    public float DoldurmaIlerleme { get { return Dolduruyor ? 1f - doldurKalan / Mathf.Max(0.01f, Tanim.doldurmaSuresi) : 1f; } }
    public float GeriTepme { get; private set; }         // 1 = az önce ateş edildi, hızla 0'a iner
    /// <summary>Şarjör ve yedek bitti: silah ruhsuz kaldı.</summary>
    public bool Bos { get { return Mermi <= 0 && Yedek <= 0 && !Sinirsiz && !Dolduruyor; } }
    public event System.Action<LdrSilahTanimi> SilahDegisti;
    /// <summary>Oyuncu ateş edince (namlu konumu, yön): namlu patlaması gibi efektler dinler.</summary>
    public static event System.Action<Vector2, Vector2> OyuncuAtesEtti;

    /// <summary>Seçili silahın mermisi bitti, sınırsız mermili SOLUK TABANCA devrede (cephane bulunca asıl silah geri gelir).</summary>
    public bool Yedekte { get; private set; }
    /// <summary>Kalan mermi / toplam (sınırsız silahta 1).</summary>
    public float MermiOrani { get { if (Yedekte) return 0f; if (Tanim == null || Sinirsiz || Tanim.toplamMermi <= 0) return 1f; return Mathf.Clamp01((Mermi + Yedek) / (float)Tanim.toplamMermi); } }

    LdrSilahTanimi secili;                               // pompalı gibi geçici değişimden önceki silah
    float sonAtes = -10f, doldurKalan, onSarjSayac;
    bool tetikteydi;

    void Awake()
    {
        if (Tanim == null) SilahSec(LdrSilahTablosu.OyuncuAdiyla(LdrIlerleme.SeciliSilah));
    }

    /// <summary>Pazarda seçilen silahı takar ve mermisini doldurur.</summary>
    public void SilahSec(LdrSilahTanimi t)
    {
        secili = t;
        Pompali = false;
        Yedekte = false;
        Tak(t);
    }

    void Tak(LdrSilahTanimi t)
    {
        Tanim = t;
        Mermi = t.sarjor;
        Yedek = t.sinirsizMermi ? 0 : Mathf.Max(0, t.toplamMermi - t.sarjor);
        doldurKalan = 0f;
        GeriTepme = 0f;
        onSarjSayac = 0f;
        if (SilahDegisti != null) SilahDegisti(t);
    }

    public void Sifirla()
    {
        if (secili == null) secili = LdrSilahTablosu.OyuncuAdiyla(LdrIlerleme.SeciliSilah);
        Pompali = false;
        Yedekte = false;
        Tak(secili);
    }

    public void Doldur()
    {
        if (Tanim == null || Dolduruyor || Tanim.sinirsizSarjor || Mermi >= Tanim.sarjor) return;
        if (!Sinirsiz && Yedek <= 0) return;
        doldurKalan = Tanim.doldurmaSuresi;
        LdrSes.Cal("sfx_reload", 0.6f);
    }

    /// <summary>Cephane eşyası: şarjör ve yedek tamamen dolar.</summary>
    public void HemenDoldur()
    {
        if (Yedekte && !Pompali) { Yedekte = false; Tak(secili); LdrHud.Duyur("MERMİ BULDUN  -  " + secili.ad + " GERİ GELDİ", new Color(1f, 0.8f, 0.4f)); return; }
        doldurKalan = 0f;
        Mermi = Tanim.sarjor;
        Yedek = Tanim.sinirsizMermi ? 0 : Mathf.Max(0, Tanim.toplamMermi - Tanim.sarjor);
    }

    public void PompaliAyarla(bool ac)
    {
        if (Pompali == ac) return;
        Pompali = ac;
        if (ac) Yedekte = false;
        Tak(ac ? LdrSilahTablosu.Oyuncu.Find(s => s.anahtar == "golge") : secili);
    }

    /// <summary>Tetik basılıyken her karede çağrılır. Ateş edildiyse true.</summary>
    public bool AtesEt(Vector2 namlu, Vector2 yon)
    {
        if (Tanim == null) return false;
        if (Dolduruyor) return false;
        // ön şarj: tetik basılı tutuldukça dolar
        tetikteydi = true;
        onSarjSayac += Time.deltaTime;
        if (onSarjSayac < Tanim.onSarj) return false;
        if (Time.time - sonAtes < Tanim.atesAraligi * hizCarpani) return false;
        if (Mermi <= 0 && !Tanim.sinirsizSarjor) { Doldur(); return false; }

        sonAtes = Time.time;
        if (!Tanim.sinirsizSarjor) Mermi--;
        GeriTepme = 1f;

        var m = Tanim.mermi;
        int adet = Random.Range(m.adet.x, m.adet.y + 1);
        for (int i = 0; i < adet; i++)
        {
            float sapma = Random.Range(m.sapma.x, Mathf.Max(m.sapma.x, m.sapma.y)) * (Random.value < 0.5f ? -1f : 1f);
            Vector2 d = Quaternion.Euler(0f, 0f, sapma) * (Vector3)yon;
            var mermi = LdrMermi.Ates(namlu, d, m, 0);
            mermi.HasarCarpaniUygula(hasarCarpani);
        }
        LdrSes.Cal("sfx_shoot", adet > 1 ? 0.7f : 0.5f, Random.Range(0.95f, 1.05f));
        if (adet > 1) LdrKamera.Salla(0.05f);
        if (OyuncuAtesEtti != null) OyuncuAtesEtti(namlu, yon);
        if (Mermi <= 0 && !Tanim.sinirsizSarjor) Doldur();
        return true;
    }

    void Update()
    {
        if (Tanim == null) return;
        // mermi bitince oyun kilitlenmesin: sınırsız mermili ilk tabanca (SOLUK TABANCASI) devreye girer
        if (Bos && !Pompali && !Yedekte && secili != null && !secili.sinirsizMermi)
        {
            var soluk = LdrSilahTablosu.Oyuncu.Find(x => x.sinirsizMermi && x.anahtar == "soluk");
            if (soluk != null)
            {
                Yedekte = true;
                Tak(soluk);
                LdrHud.Duyur("MERMİN BİTTİ  -  SOLUK TABANCAYA GEÇTİN  (cephane bul)", new Color(1f, 0.6f, 0.4f));
            }
        }
        if (!tetikteydi) onSarjSayac = 0f;      // tetik bırakıldı: ön şarj sıfırlanır
        tetikteydi = false;

        if (doldurKalan > 0f)
        {
            doldurKalan -= Time.deltaTime;
            if (doldurKalan <= 0f)
            {
                int eksik = Tanim.sarjor - Mermi;
                if (Tanim.sinirsizMermi) Mermi = Tanim.sarjor;
                else { int al = Mathf.Min(eksik, Yedek); Mermi += al; Yedek -= al; }
            }
        }
        GeriTepme = Mathf.MoveTowards(GeriTepme, 0f, Time.deltaTime * 8f);
    }
}
