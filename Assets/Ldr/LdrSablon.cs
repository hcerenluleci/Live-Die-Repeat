using System.Collections.Generic;
using UnityEngine;

/// <summary>Şablondaki bir kapı yeri: yön, kenardaki konum ve kullanılmazsa duvarla doldurulacak boşluk (dahil sınırlar).</summary>
public class LdrKapiVeri
{
    public Vector2Int konum;
    public int yon;                   // 0 kuzey, 1 doğu, 2 güney, 3 batı
    public Vector2Int kapatAlt, kapatUst;
    public bool bagli, kullanilamaz;

    public LdrKapiVeri Kopya()
    {
        return new LdrKapiVeri { konum = konum, yon = yon, kapatAlt = kapatAlt, kapatUst = kapatUst };
    }
}

public class LdrEsyaVeri
{
    public char tur;                  // B tahta fıçı, G altın fıçı, H yatay masa, V dikey masa, a/b/c meşale, y sandık noktası
    public Vector2 konum;
    public float olcek;
}

/// <summary>
/// Oda şablonu. Veri LdrSablonVerisi'nden okunur (ekibin zindan üreticisi Araclar/zindan.py ile üretilir):
/// zemin, duvar (çarpışma) ve harita hücreleri, kapılar, düşman doğma noktaları ve eşyalar.
/// </summary>
public class LdrSablon
{
    public string ad;
    public char tur;                  // E giriş, S küçük, M orta, L büyük, H sunak (sandık), B taht (çıkış), N geçit (kuzey-güney), W geçit (doğu-batı)
    public Vector2Int alt, ust;       // dahil
    public LdrKapiVeri[] kapilar;
    public Vector2[] doguma;
    public LdrEsyaVeri[] esyalar;
    public byte[] hucre;              // satır satır, alttan üste; bit1 zemin, 4 katı, 8 harita

    public int Gen { get { return ust.x - alt.x + 1; } }
    public int Yuk { get { return ust.y - alt.y + 1; } }

    static List<LdrSablon> hepsi;
    public static List<LdrSablon> Hepsi { get { if (hepsi == null) Yukle(); return hepsi; } }

    public static LdrSablon Bul(string ad)
    {
        foreach (var s in Hepsi) if (s.ad == ad) return s;
        return null;
    }

    static void Yukle()
    {
        hepsi = new List<LdrSablon>();
        foreach (var satir in LdrSablonVerisi.Odalar)
        {
            var p = satir.Split('|');
            var s = new LdrSablon();
            s.ad = p[0];
            s.tur = p[1][0];
            var b = p[2].Split(',');
            s.alt = new Vector2Int(int.Parse(b[0]), int.Parse(b[1]));
            s.ust = new Vector2Int(int.Parse(b[2]), int.Parse(b[3]));

            var kl = new List<LdrKapiVeri>();
            foreach (var d in p[3].Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var q = d.Split(',');
                var k = new LdrKapiVeri();
                k.konum = new Vector2Int(int.Parse(q[0]), int.Parse(q[1]));
                k.yon = q[2] == "N" ? 0 : q[2] == "E" ? 1 : q[2] == "S" ? 2 : 3;
                k.kapatAlt = new Vector2Int(int.Parse(q[3]), int.Parse(q[4]));
                k.kapatUst = new Vector2Int(int.Parse(q[5]), int.Parse(q[6]));
                kl.Add(k);
            }
            s.kapilar = kl.ToArray();

            var dl = new List<Vector2>();
            foreach (var d in p[4].Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var q = d.Split(',');
                dl.Add(new Vector2(float.Parse(q[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture)));
            }
            s.doguma = dl.ToArray();

            var el = new List<LdrEsyaVeri>();
            foreach (var d in p[5].Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var q = d.Split(',');
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                el.Add(new LdrEsyaVeri { tur = q[0][0], konum = new Vector2(float.Parse(q[1], ci), float.Parse(q[2], ci)), olcek = float.Parse(q[3], ci) });
            }
            s.esyalar = el.ToArray();

            // hücreler (RLE: harf = değer, ardındaki sayı = tekrar)
            var h = new byte[s.Gen * s.Yuk];
            int n = 0, i = 0;
            string r = p[6];
            while (i < r.Length && n < h.Length)
            {
                byte v = (byte)(r[i] - 'A');
                i++;
                int say = 0;
                while (i < r.Length && r[i] >= '0' && r[i] <= '9') { say = say * 10 + (r[i] - '0'); i++; }
                if (say == 0) say = 1;
                for (int j = 0; j < say && n < h.Length; j++) h[n++] = v;
            }
            s.hucre = h;
            hepsi.Add(s);
        }
    }
}

/// <summary>Yerleştirilmiş oda (zindan düğümü): şablon + dünyadaki konumu.</summary>
public class LdrYerOda
{
    public int dugum, ebeveyn;
    public char dugumTuru;            // E C S M L H B
    public LdrSablon sablon;
    public Vector2Int alt, ust;       // yerleştirilmiş sınırlar (dahil)
    public LdrKapiVeri[] kapilar;
    public bool konumlandi;

    /// <summary>Şablon koordinatından dünya koordinatına kayma.</summary>
    public Vector2Int Kayma { get { return alt - sablon.alt; } }
}

/// <summary>
/// Zindan kurucu: harita grafiğindeki her düğüm için, ebeveyn odanın boş bir kapısına karşı yönde kapısı olan bir şablon seçer
/// ve iki kapı uç uca gelecek şekilde, başka odalarla çakışmadan yerleştirir. Olmazsa başka kapı / şablon / grafik dener.
/// </summary>
public static class LdrZindan
{
    static readonly Vector2Int[] Ayar = { new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(1, 0) };   // kuzey, doğu, güney, batı kapısına yaslanan oda kayması

    public static List<LdrYerOda> Uret(int derinlik, System.Random rng)
    {
        string onEk = (Mathf.Clamp(derinlik, 0, 2) + 1) + "_";
        var graflar = new List<string>();
        foreach (var g in LdrSablonVerisi.Graflar) if (g.StartsWith(onEk)) graflar.Add(g);
        var sablonlar = new List<LdrSablon>();
        foreach (var ad in LdrSablonVerisi.Seviyeler[Mathf.Clamp(derinlik, 0, 2)])
        {
            var s = LdrSablon.Bul(ad);
            if (s != null) sablonlar.Add(s);
        }

        for (int dis = 0; dis < 40; dis++)
        {
            var graf = Coz(graflar[rng.Next(graflar.Count)]);
            for (int ic = 0; ic < 200; ic++)
            {
                var sonuc = Dene(graf, sablonlar, rng);
                if (sonuc != null) return sonuc;
            }
        }
        // son çare: yalnızca giriş
        return null;
    }

    struct Dugum { public char tur; public int ebeveyn; }

    static List<Dugum> Coz(string graf)
    {
        var l = new List<Dugum>();
        foreach (var t in graf.Substring(graf.IndexOf(':') + 1).Split(','))
            l.Add(new Dugum { tur = t[0], ebeveyn = int.Parse(t.Substring(1)) });
        return l;
    }

    static List<LdrYerOda> Dene(List<Dugum> graf, List<LdrSablon> sablonlar, System.Random rng)
    {
        var yerler = new List<LdrYerOda>();
        for (int i = 0; i < graf.Count; i++)
        {
            var d = graf[i];
            LdrYerOda yer;
            if (d.tur == 'E')
            {
                var s = Rastgele(sablonlar, 'E', rng);
                if (s == null) return null;
                yer = Olustur(i, d, s);
                yer.alt = s.alt; yer.ust = s.ust; yer.konumlandi = true;
                yerler.Add(yer);
                continue;
            }

            var ebeveyn = yerler[d.ebeveyn];
            bool yerlesti = false;
            while (!yerlesti)
            {
                var adaylar = new List<LdrKapiVeri>();
                foreach (var k in ebeveyn.kapilar) if (!k.bagli && !k.kullanilamaz) adaylar.Add(k);
                if (adaylar.Count == 0) return null;
                var kp = adaylar[rng.Next(adaylar.Count)];

                LdrSablon sablon;
                if (d.tur == 'C') sablon = Rastgele(sablonlar, (kp.yon == 0 || kp.yon == 2) ? 'N' : 'W', rng);
                else sablon = Rastgele(sablonlar, d.tur, rng);
                if (sablon == null) return null;

                yer = Olustur(i, d, sablon);
                if (Yerlestir(ebeveyn, kp, yer, yerler)) { yerlesti = true; yerler.Add(yer); }
            }
        }
        return yerler;
    }

    static LdrYerOda Olustur(int i, Dugum d, LdrSablon s)
    {
        var y = new LdrYerOda { dugum = i, ebeveyn = d.ebeveyn, dugumTuru = d.tur, sablon = s };
        y.alt = s.alt; y.ust = s.ust;
        y.kapilar = new LdrKapiVeri[s.kapilar.Length];
        for (int k = 0; k < y.kapilar.Length; k++) y.kapilar[k] = s.kapilar[k].Kopya();
        return y;
    }

    static LdrSablon Rastgele(List<LdrSablon> liste, char tur, System.Random rng)
    {
        var u = new List<LdrSablon>();
        foreach (var s in liste) if (s.tur == tur) u.Add(s);
        return u.Count == 0 ? null : u[rng.Next(u.Count)];
    }

    static bool Yerlestir(LdrYerOda ebeveyn, LdrKapiVeri ebeveynKapi, LdrYerOda oda, List<LdrYerOda> yerler)
    {
        int karsi = (ebeveynKapi.yon + 2) % 4;
        LdrKapiVeri kapi = null;
        foreach (var k in oda.kapilar) if (k.yon == karsi) { kapi = k; break; }
        if (kapi == null) { ebeveynKapi.kullanilamaz = true; return false; }

        Vector2Int ebeveynKapiKonum = ebeveyn.alt + ebeveynKapi.konum - ebeveyn.sablon.alt;
        Vector2Int ayar = Ayar[kapi.yon];
        oda.alt = ebeveynKapiKonum + ayar + oda.sablon.alt - kapi.konum;
        oda.ust = oda.alt + oda.sablon.ust - oda.sablon.alt;

        foreach (var diger in yerler)
            if (Cakisir(oda, diger)) { ebeveynKapi.kullanilamaz = true; return false; }

        ebeveynKapi.bagli = true; ebeveynKapi.kullanilamaz = true;
        kapi.bagli = true; kapi.kullanilamaz = true;
        oda.konumlandi = true;
        return true;
    }

    static bool Cakisir(LdrYerOda a, LdrYerOda b)
    {
        return Mathf.Max(a.alt.x, b.alt.x) <= Mathf.Min(a.ust.x, b.ust.x) && Mathf.Max(a.alt.y, b.alt.y) <= Mathf.Min(a.ust.y, b.ust.y);
    }
}
