using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Kalıcı ilerleme: toplam ruh, en derin bölüm, seçili karakter/silah, görülen silahlar ve puan tablosu.
/// (Ölüm sayısını LiveDieRepeat tutar, kaçış parçalarını LdrParca.)
/// </summary>
public static class LdrIlerleme
{
    const string RuhAnahtari = "LDR_Souls";
    const string EnDerinAnahtari = "LDR_MaxLevel";
    const string GorulenAnahtari = "LDR_SeenWeapons";
    const string SilahAnahtari = "LDR_Weapon";
    const string KarakterAnahtari = "LDR_Skin";
    const string PuanTablosuAnahtari = "LDR_Scores";

    /// <summary>Yalnızca Unity editöründe geliştirme kolaylığı: tüm silahlar açık. Gerçek ilerlemeyi denemek için false bırak.</summary>
#if UNITY_EDITOR
    public static bool HepsiAcik = false;
#else
    public const bool HepsiAcik = false;
#endif

    public static event System.Action<int> YeniDerinlik;

    // ---------------- ruh
    public static long ToplamRuh
    {
        get
        {
            long v;
            return long.TryParse(PlayerPrefs.GetString(RuhAnahtari, "0"), out v) ? v : 0L;
        }
    }

    public static void RuhEkle(long n)
    {
        if (n <= 0) return;
        PlayerPrefs.SetString(RuhAnahtari, (ToplamRuh + n).ToString());
        PlayerPrefs.Save();
    }

    // ---------------- en derin bölüm (0 tabanlı)
    public static int EnDerin { get { return PlayerPrefs.GetInt(EnDerinAnahtari, 0); } }

    public static void DerinlikBildir(int idx)
    {
        if (idx <= EnDerin) return;
        PlayerPrefs.SetInt(EnDerinAnahtari, idx);
        PlayerPrefs.Save();
        if (YeniDerinlik != null) YeniDerinlik(idx);
    }

    // ---------------- karakter (0 = Liz, 1 = Jan)
    public static int Karakter
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(KarakterAnahtari, 0), 0, 1); }
        set { PlayerPrefs.SetInt(KarakterAnahtari, Mathf.Clamp(value, 0, 1)); PlayerPrefs.Save(); }
    }

    public static string KarakterAdi(int k) { return k == 0 ? "LIZ" : "JAN"; }

    // ---------------- silah
    public static string SeciliSilah
    {
        get { return PlayerPrefs.GetString(SilahAnahtari, ""); }
        set { PlayerPrefs.SetString(SilahAnahtari, value ?? ""); PlayerPrefs.Save(); }
    }

    public static HashSet<string> GorulenSilahlar()
    {
        return new HashSet<string>(PlayerPrefs.GetString(GorulenAnahtari, "").Split('|'));
    }

    public static void SilahlariGorduIsaretle()
    {
        var hepsi = GorulenSilahlar();
        foreach (var s in LdrSilahTablosu.Oyuncu) if (s.Acik) hepsi.Add(s.ad);
        PlayerPrefs.SetString(GorulenAnahtari, string.Join("|", new List<string>(hepsi).ToArray()));
        PlayerPrefs.Save();
    }

    /// <summary>Ölüm ekranı için: henüz görülmemiş ama artık açık olan silahların adları.</summary>
    public static string YeniAcilanlar()
    {
        var gorulen = GorulenSilahlar();
        var yeni = new List<string>();
        foreach (var s in LdrSilahTablosu.Oyuncu) if (s.Acik && !gorulen.Contains(s.ad)) yeni.Add(s.ad);
        return yeni.Count == 0 ? "" : "YENİ SİLAH AÇILDI: " + string.Join(", ", yeni.ToArray());
    }

    // ---------------- puan tablosu (en iyi 100)
    public class Kayit { public string isim, bolum; public long puan; }

    public static List<Kayit> PuanTablosu()
    {
        var l = new List<Kayit>();
        string ham = PlayerPrefs.GetString(PuanTablosuAnahtari, "");
        if (string.IsNullOrEmpty(ham)) return l;
        foreach (var satir in ham.Split('\n'))
        {
            var p = satir.Split('\t');
            long puan;
            if (p.Length == 3 && long.TryParse(p[2], out puan)) l.Add(new Kayit { isim = p[0], bolum = p[1], puan = puan });
        }
        return l;
    }

    public static void PuanEkle(string isim, string bolum, long puan)
    {
        if (puan <= 0) return;
        var l = PuanTablosu();
        l.Add(new Kayit { isim = isim, bolum = bolum, puan = puan });
        l.Sort((a, b) => b.puan.CompareTo(a.puan));
        if (l.Count > 100) l.RemoveRange(100, l.Count - 100);
        var sb = new StringBuilder();
        for (int i = 0; i < l.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(l[i].isim.Replace('\t', ' ').Replace('\n', ' ')).Append('\t').Append(l[i].bolum.Replace('\t', ' ').Replace('\n', ' ')).Append('\t').Append(l[i].puan);
        }
        PlayerPrefs.SetString(PuanTablosuAnahtari, sb.ToString());
        PlayerPrefs.Save();
    }

    public static void HepsiniSifirla()
    {
        PlayerPrefs.DeleteKey(RuhAnahtari);
        PlayerPrefs.DeleteKey(EnDerinAnahtari);
        PlayerPrefs.DeleteKey(GorulenAnahtari);
        PlayerPrefs.DeleteKey(SilahAnahtari);
        PlayerPrefs.DeleteKey(PuanTablosuAnahtari);
        LdrParca.Sifirla();
        PlayerPrefs.Save();
    }
}

/// <summary>
/// Bu yaşamın puanı: ruh (puan) ve zincir (çarpan).
/// Düşman ölünce ruh += düşmanın başlangıç canı x zincir. İsabetli her mermi zinciri 1 artırır;
/// boşa giden mermi 2 düşürür (1..25): dikkatli nişan almak ödüllendirilir.
/// </summary>
public static class LdrPuan
{
    public static long Ruh { get; private set; }
    public static int Zincir { get; private set; }
    public static event System.Action Degisti;

    public static void Sifirla()
    {
        Ruh = 0; Zincir = 1;
        if (Degisti != null) Degisti();
    }

    public static void Kazan(int taban)
    {
        Ruh += (long)taban * Mathf.Max(1, Zincir);
        if (Degisti != null) Degisti();
    }

    public static void ZincirDegistir(bool artir)
    {
        Zincir = Mathf.Clamp(Zincir + (artir ? 1 : -2), 1, 25);
        if (Degisti != null) Degisti();
    }
}
