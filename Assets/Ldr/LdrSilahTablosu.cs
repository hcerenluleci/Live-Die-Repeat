using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mermi tanımı: hasar, hız, menzil, sapma, bir atıştaki mermi sayısı ve iz.
/// Inspector'da bu alanlar düzenli başlıklarla görünür.
/// </summary>
[System.Serializable]
public class LdrMermiTanimi
{
    [Tooltip("Tek mermi hasarı (can birimi: Liz 100, Jan 70)")]
    public int hasar = 5;
    [Tooltip("En düşük ve en yüksek mermi hızı (birim/sn)")]
    public Vector2 hiz = new Vector2(14f, 14f);
    [Tooltip("Mermi uçuş menzili (birim)")]
    public float menzil = 12f;
    [Tooltip("Sapma açısı aralığı (derece): en az, en çok")]
    public Vector2 sapma = new Vector2(2f, 5f);
    [Tooltip("Bir ateşte çıkan mermi sayısı aralığı (pompalı gibi)")]
    public Vector2Int adet = new Vector2Int(1, 1);
    [Tooltip("Çoklu mermiler arası bekleme aralığı (sn)")]
    public Vector2 adetAraligi = Vector2.zero;
    [Tooltip("Arkasında iz bırakır mı")]
    public bool iz;
    public float izSuresi;
    [Tooltip("Mermi ışık/renk kimliği")]
    public Color renk = new Color(1f, 0.85f, 0.3f);
}

/// <summary>
/// Bir silahın tüm ayarı (oyuncu silahları ve düşman silahları aynı sınıfı kullanır).
/// </summary>
[System.Serializable]
public class LdrSilahTanimi
{
    [Header("Kimlik")]
    [Tooltip("Dosya/arama adı parçası, ör. soluk")]
    public string anahtar;
    [Tooltip("Ekranda görünen ad")]
    public string ad;
    [Tooltip("LdrResim çizim adı, ör. gun_pistol")]
    public string cizim;
    [Tooltip("Silah rengi (pazar, HUD)")]
    public Color renk = Color.white;

    [Header("Açılma koşulu (yalnızca oyuncu silahları)")]
    public int gerekenOlum;
    public long gerekenRuh;

    [Header("Ateş")]
    [Tooltip("Ardışık iki atış arası süre (sn)")]
    public float atesAraligi = 0.25f;
    [Tooltip("Basılı tutunca ilk atıştan önce bekleme (sn)")]
    public float onSarj;
    public float doldurmaSuresi = 1.5f;

    [Header("Mermi kapasitesi")]
    public int sarjor = 9;
    [Tooltip("Şarjör dahil toplam mermi (sınırsızsa göz ardı edilir)")]
    public int toplamMermi;
    public bool sinirsizMermi;
    public bool sinirsizSarjor;

    [Header("Mermi")]
    public LdrMermiTanimi mermi = new LdrMermiTanimi();

    [Header("Silahın namlu noktası (silah çiziminde)")]
    public Vector2 namlu = new Vector2(1.1f, 0.3f);

    /// <summary>Oyuncu silahı mı (yoksa düşman silahı)</summary>
    [HideInInspector] public bool oyuncuSilahi;

    public bool Acik
    {
        get
        {
            return LdrIlerleme.HepsiAcik || (LiveDieRepeat.Deaths >= gerekenOlum && LdrIlerleme.ToplamRuh >= gerekenRuh);
        }
    }

    public string Sart
    {
        get
        {
            if (gerekenOlum > 0 && gerekenRuh > 0) return gerekenOlum + " KEZ ÖL ve " + gerekenRuh + " RUH TOPLA";
            if (gerekenOlum > 0) return gerekenOlum + " KEZ ÖL  (" + Mathf.Min(LiveDieRepeat.Deaths, gerekenOlum) + "/" + gerekenOlum + ")";
            return gerekenRuh + " RUH TOPLA  (" + System.Math.Min(LdrIlerleme.ToplamRuh, gerekenRuh) + "/" + gerekenRuh + ")";
        }
    }

    /// <summary>SIRADAN / NADİR / DESTANSI / EFSANEVİ</summary>
    public string Nadirlik(out Color renkOut)
    {
        float s = gerekenRuh + gerekenOlum * 150f;
        if (s < 1f) { renkOut = new Color(0.75f, 0.75f, 0.85f); return "SIRADAN"; }
        if (s < 800f) { renkOut = new Color(0.4f, 0.7f, 1f); return "NADİR"; }
        if (s < 2500f) { renkOut = new Color(0.78f, 0.4f, 1f); return "DESTANSI"; }
        renkOut = new Color(1f, 0.8f, 0.3f); return "EFSANEVİ";
    }

    /// <summary>Saniyedeki atış sayısı (pazarda TEMPO çubuğu)</summary>
    public float Tempo { get { return atesAraligi > 0.001f ? 1f / atesAraligi : 0f; } }
}

/// <summary>
/// Silah tablosu: 9 oyuncu silahı ve düşman silahları. Denge ekibin kendi tablosudur:
/// temel birim "saniyedeki hasar" (DPS). Başlangıç tabancası ~18 DPS; açılan silahlar farklı oyun tarzı sunar
/// (yakın/uzak, patlayıcı, saçma), toplam güçleri 20-35 DPS bandında tutulur. Düşman canları buna göre ayarlanır (LdrDusman).
/// </summary>
public static class LdrSilahTablosu
{
    static List<LdrSilahTanimi> oyuncu;
    static Dictionary<string, LdrSilahTanimi> dusman;

    static Color AltinMermi = new Color(1f, 0.82f, 0.25f);
    static Color MaviMermi = new Color(0.35f, 0.65f, 1f);
    static Color SariMermi = new Color(1f, 0.95f, 0.3f);
    static Color YesilMermi = new Color(0.4f, 1f, 0.45f);
    static Color KirmiziMermi = new Color(1f, 0.2f, 0.22f);
    static Color RoketMermi = new Color(1f, 0.55f, 0.15f);
    static Color MorMermi = new Color(0.75f, 0.35f, 1f);

    static LdrMermiTanimi M(int hasar, float hizMin, float hizMax, float menzil, float sapmaMin, float sapmaMax, Color renk,
        int adetMin = 1, int adetMax = 1, float aralikMax = 0f, bool iz = false, float izSure = 0f)
    {
        return new LdrMermiTanimi
        {
            hasar = hasar, hiz = new Vector2(hizMin, hizMax), menzil = menzil, sapma = new Vector2(sapmaMin, sapmaMax),
            adet = new Vector2Int(adetMin, adetMax), adetAraligi = new Vector2(0f, aralikMax), iz = iz, izSuresi = izSure, renk = renk
        };
    }

    static LdrSilahTanimi S(string anahtar, string ad, string cizim, Color renk, int olum, long ruh, float aralik, float onSarj, float doldur,
        int sarjor, int toplam, bool sinirsizMermi, bool sinirsizSarjor, LdrMermiTanimi mermi, Vector2 namlu, bool oyuncuSilahi = true)
    {
        return new LdrSilahTanimi
        {
            anahtar = anahtar, ad = ad, cizim = cizim, renk = renk, gerekenOlum = olum, gerekenRuh = ruh,
            atesAraligi = aralik, onSarj = onSarj, doldurmaSuresi = doldur, sarjor = sarjor, toplamMermi = toplam,
            sinirsizMermi = sinirsizMermi, sinirsizSarjor = sinirsizSarjor, mermi = mermi, namlu = namlu, oyuncuSilahi = oyuncuSilahi
        };
    }

    /// <summary>Pazar sırası: (gerekenOlum*400 + gerekenRuh/5) küçükten büyüğe.</summary>
    public static List<LdrSilahTanimi> Oyuncu
    {
        get
        {
            if (oyuncu != null) return oyuncu;
            var l = new List<LdrSilahTanimi>
            {
                // anahtar, ad, çizim, renk, ölüm şartı, ruh şartı, atış aralığı, ön şarj, doldurma, şarjör, toplam mermi, sınırsız mermi, sınırsız şarjör
                S("soluk", "SOLUK TABANCASI", "gun_pistol", new Color(0.85f, 0.85f, 0.95f), 0, 0, 0.28f, 0f, 1.4f, 8, 0, true, false,
                    M(5, 16, 16, 13, 1.5f, 3.5f, AltinMermi), new Vector2(1.12f, 0.28f)),
                S("surgun", "SÜRGÜN REVOLVERİ", "gun_revolver", new Color(1f, 0.72f, 0.28f), 1, 0, 0.42f, 0f, 2f, 6, 48, false, false,
                    M(10, 14, 14, 15, 0.5f, 2f, AltinMermi), new Vector2(1.1f, 0.3f)),
                S("tirnak", "TIRNAK", "gun_smg", new Color(0.85f, 0.7f, 1f), 0, 400, 0.1f, 0f, 1.8f, 28, 140, false, false,
                    M(3, 19, 19, 11, 3f, 6f, YesilMermi), new Vector2(1.2f, 0.35f)),
                S("gozcu", "GÖZCÜ NİŞANCI", "gun_sniper", new Color(0.4f, 0.92f, 1f), 0, 1800, 0.85f, 0f, 2.2f, 5, 30, false, false,
                    M(24, 30, 30, 24, 0f, 0.8f, YesilMermi), new Vector2(1.5f, 0.3f)),
                S("hayalet", "HAYALET IŞINI", "gun_laser", new Color(0.65f, 0.85f, 1f), 6, 0, 0.36f, 0.1f, 2.6f, 10, 60, false, false,
                    M(11, 60, 60, 16, 0.3f, 0.8f, MaviMermi, 1, 1, 0f, true, 0.08f), new Vector2(1.2f, 0.3f)),
                S("ruh", "RUH TOPU", "gun_laser", new Color(0.45f, 1f, 0.6f), 0, 4000, 0.3f, 0.08f, 1.2f, 8, 0, true, false,
                    M(7, 40, 40, 15, 0.5f, 1.2f, MaviMermi, 1, 1, 0f, true, 0.06f), new Vector2(1.2f, 0.3f)),
                S("roket", "ROKETATAR", "gun_rifle", new Color(0.85f, 0.7f, 1f), 3, 0, 1.2f, 0f, 2.8f, 3, 18, false, false,
                    M(32, 9, 11, 26, 0.5f, 1.5f, RoketMermi, 1, 1, 0f, true, 0.15f), new Vector2(1.3f, 0.3f)),
                S("gece", "GECE TABANCASI", "gun_rifle", new Color(0.75f, 0.45f, 1f), 2, 0, 0.2f, 0f, 2.4f, 14, 70, false, false,
                    M(5, 15, 15, 13, 1.5f, 4f, SariMermi), new Vector2(1.2f, 0.3f)),
                S("golge", "GÖLGE TÜFEĞİ", "gun_rifle", new Color(0.85f, 0.7f, 1f), 3, 0, 0.55f, 0f, 1.6f, 5, 40, false, false,
                    M(4, 10, 15, 9f, 1f, 26f, SariMermi, 5, 7, 0.01f), new Vector2(1.3f, 0.3f)),
            };
            oyuncu = StabilSirala(l);   // eşit anahtarlı silahlar tablo sırasını korur
            return oyuncu;
        }
    }

    static List<LdrSilahTanimi> StabilSirala(List<LdrSilahTanimi> kaynak)
    {
        // basit eklemeli sıralama (9 öğe): eşitlikte tablo sırası korunur
        var sonuc = new List<LdrSilahTanimi>();
        foreach (var s in kaynak)
        {
            int k = s.gerekenOlum * 400 + (int)(s.gerekenRuh / 5);
            int i = sonuc.Count;
            while (i > 0)
            {
                var o = sonuc[i - 1];
                int ko = o.gerekenOlum * 400 + (int)(o.gerekenRuh / 5);
                if (ko <= k) break;
                i--;
            }
            sonuc.Insert(i, s);
        }
        return sonuc;
    }

    public static LdrSilahTanimi OyuncuAdiyla(string ad)
    {
        foreach (var s in Oyuncu) if (s.ad == ad) return s;
        return Oyuncu[0];
    }

    /// <summary>Düşman silahları (anahtar: "tabanca", "pompali", "seri", "sarmal", "girdap", "tabancapatron", "pompalipatron", "seripatron").</summary>
    public static LdrSilahTanimi Dusman(string anahtar)
    {
        if (dusman == null)
        {
            dusman = new Dictionary<string, LdrSilahTanimi>();
            dusman["sarmal"] = S("sarmal", "Sarmal Desen", "", Color.green, 0, 0, 2.8f, 0f, 0f, 0, 0, true, true,
                M(4, 6, 10, 26, 0, 3, YesilMermi), new Vector2(0.69f, 0.31f), false);
            dusman["girdap"] = S("girdap", "Girdap Deseni", "", Color.magenta, 0, 0, 3.2f, 0f, 0f, 0, 0, true, true,
                M(4, 6, 10, 26, 0, 3, MorMermi), new Vector2(0.69f, 0.31f), false);
            dusman["seripatron"] = S("seripatron", "Seri Patron", "", Color.red, 0, 0, 0.12f, 0f, 0f, 0, 0, true, true,
                M(7, 17, 17, 24, 2, 5, KirmiziMermi), new Vector2(1.6f, 0.4f), false);
            dusman["tabanca"] = S("tabanca", "Tabanca", "", Color.red, 0, 0, 0.3f, 0f, 1.4f, 8, 0, true, false,
                M(5, 12, 14, 18, 2, 5, KirmiziMermi), new Vector2(1.12f, 0.28f), false);
            dusman["tabancapatron"] = S("tabancapatron", "Tabanca Patron", "", Color.red, 0, 0, 0.22f, 0f, 1f, 12, 0, true, false,
                M(6, 14, 18, 26, 2, 4, KirmiziMermi), new Vector2(1.15f, 0.3f), false);
            dusman["pompali"] = S("pompali", "Pompalı", "", Color.red, 0, 0, 0.5f, 0f, 1.2f, 3, 0, true, false,
                M(4, 8, 11, 16, 1, 26, KirmiziMermi, 5, 7, 0.01f), new Vector2(1.1f, 0.3f), false);
            dusman["pompalipatron"] = S("pompalipatron", "Pompalı Patron", "", Color.red, 0, 0, 0.45f, 0f, 1f, 5, 0, true, false,
                M(6, 9, 13, 22, 1, 22, KirmiziMermi, 6, 8, 0.01f), new Vector2(1.1f, 0.3f), false);
            dusman["seri"] = S("seri", "Seri Ateş", "", Color.red, 0, 0, 0.3f, 0f, 1.8f, 10, 0, true, false,
                M(6, 10, 13, 18, 2, 5, KirmiziMermi), new Vector2(1.2f, 0.3f), false);
        }
        LdrSilahTanimi t;
        return dusman.TryGetValue(anahtar, out t) ? t : dusman["tabanca"];
    }
}
