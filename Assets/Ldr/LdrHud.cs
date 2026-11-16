using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// DERS GEREKSİNİMİ: HUD (Heads-Up Display) - oyun boyunca hep görünür
// Gösterir: can (5 kan damlası), RUH + ZİNCİR plakası, silah ve mermi plakası, [T] ışınlanma ve [F] ateş topu
// plakaları, güç süreleri, ekrana süzülen duyurular ("ODA TEMİZLENDİ" gibi) ve bölüm başlığı.
// Ruh ve zincir değişince LdrPuan.Degisti olayıyla haber alır, plakayı kısa bir nabızla günceller.
// =====================================================================================
/// <summary>
/// Ekran göstergeleri: sol üstte RUH + ZİNCİR plakası, altta ortada silah plakası ve
/// üstünde 5 kan damlası (can), sol altta [T] / [F] plakaları, güç çubukları, yukarı süzülen bildirimler, bölüm başlığı,
/// etkileşim ipucu, kararma/parlama perdesi ve son ekran. "ÖLDÜN" ekranını LiveDieRepeat çizer.
/// </summary>
public class LdrHud : MonoBehaviour
{
    // ---- dışarıdan kullanılan durum
    public static float Karartma;       // 0..1 siyah perde
    public static Color KarartmaRenk = Color.black;   // perdenin rengi (bölüm girişinde siyah, açılırken kan kırmızısı)
    public static string OrtaYazi;      // perdenin ortasındaki beyaz yazı (bölüm girişi: "BÖLÜM 1" / ad)
    public static float Flas;           // 0..1 mor parlama (kendiliğinden söner)
    public static string SonYazi, SonAlt;
    public static Color SonRenk = Color.white;

    // ---- bildirimler (yukarı süzülen şeritler)
    class Bildirim { public string yazi; public Color renk; public float t; }
    static readonly List<Bildirim> bildirimler = new List<Bildirim>();
    static string baslik;
    static float baslikZaman = 99f;

    // Ekranın ortasında yukarı süzülen renkli bir bildirim gösterir (örn. "KAÇIŞ PARÇASI BULUNDU!").
    public static void Duyur(string yazi, Color renk)
    {
        if (bildirimler.Count >= 4) bildirimler.RemoveAt(0);
        bildirimler.Add(new Bildirim { yazi = yazi, renk = renk });
    }

    // Yeni bölüme girince büyük bölüm başlığını gösterir.
    public static void Baslik(string b) { baslik = b; baslikZaman = 0f; }

    public static void Sifirla()
    {
        Karartma = 0f; KarartmaRenk = Color.black; OrtaYazi = null; Flas = 0f; SonYazi = null; SonAlt = null;
        bildirimler.Clear(); baslik = null; baslikZaman = 99f;
    }

    // ----
    LdrYasayan yasayan;
    Texture2D damlaDoku;
    GUIStyle sol, orta, sag, piksel, kucuk;
    float gosterilenRuh, ruhNabzi, vurusNabzi;
    readonly List<KeyValuePair<string, float>> guclar = new List<KeyValuePair<string, float>>();
    static readonly Color Mor = new Color(0.62f, 0.3f, 0.9f);

    public static LdrHud Kur(LdrYasayan y)
    {
        var go = new GameObject("LdrHud");
        var h = go.AddComponent<LdrHud>();
        h.yasayan = y;
        h.damlaDoku = DamlaDokusu();
        y.Can.Hasarlandi += (c, k) => { h.vurusNabzi = 1f; };
        LdrPuan.Degisti += h.PuanDegisti;
        return h;
    }

    long oncekiRuh;
    void PuanDegisti()
    {
        if (LdrPuan.Ruh > oncekiRuh) ruhNabzi = 1f;
        oncekiRuh = LdrPuan.Ruh;
    }

    void OnDestroy() { LdrPuan.Degisti -= PuanDegisti; }

    /// <summary>Kan damlası dokusu (16x22), kodla çizilir: yuvarlak gövde, sivri uç, sol üstte parlak çizgi.</summary>
    static Texture2D DamlaDokusu()
    {
        const int W = 16, H = 22;
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[W * H];
        var icinde = new bool[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            float dx = x + 0.5f - W * 0.5f, dy = y + 0.5f;
            bool ins;
            if (dy < 8.5f) { float ddy = dy - 8.5f, r = 7f; ins = dx * dx + ddy * ddy <= r * r; }
            else { float k = 1f - (dy - 8.5f) / (H - 8.5f); ins = Mathf.Abs(dx) <= 7f * k * k + 0.4f * k; }
            icinde[x, y] = ins;
        }
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            if (!icinde[x, y]) continue;
            bool kenar = x == 0 || y == 0 || x == W - 1 || y == H - 1 || !icinde[x - 1, y] || !icinde[x + 1, y] || !icinde[x, y - 1] || (y + 1 < H && !icinde[x, y + 1]);
            float isik = Mathf.Clamp01(0.55f + 0.06f * (W * 0.5f - x) + 0.01f * y);
            byte v = (byte)(255 * (kenar ? 0.25f : isik));
            px[y * W + x] = new Color32(v, v, v, 255);
        }
        for (int y = 6; y < 12; y++) { px[y * W + 4] = new Color32(255, 255, 255, 255); if (y < 9) px[y * W + 5] = new Color32(255, 255, 255, 255); }
        t.SetPixels32(px); t.Apply();
        return t;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        Flas = Mathf.MoveTowards(Flas, 0f, dt * 2.5f);
        baslikZaman += dt;
        ruhNabzi = Mathf.MoveTowards(ruhNabzi, 0f, dt * 3f);
        vurusNabzi = Mathf.MoveTowards(vurusNabzi, 0f, dt * 2.5f);
        gosterilenRuh = Mathf.MoveTowards(gosterilenRuh, LdrPuan.Ruh, Mathf.Max(40f, Mathf.Abs(LdrPuan.Ruh - gosterilenRuh) * 6f) * dt);
    }

    void StilHazirla()
    {
        if (sol != null) return;
        sol = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        orta = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        sag = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        piksel = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
        kucuk = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
    }

    // ---------------------------------------------------------------- çizim yardımcıları
    static void Golge(Rect r, string s, GUIStyle st, Color c, float off)
    {
        var eski = st.normal.textColor;
        st.normal.textColor = new Color(0, 0, 0, c.a);
        GUI.Label(new Rect(r.x - off, r.y, r.width, r.height), s, st);
        GUI.Label(new Rect(r.x + off, r.y, r.width, r.height), s, st);
        GUI.Label(new Rect(r.x, r.y - off, r.width, r.height), s, st);
        GUI.Label(new Rect(r.x, r.y + off, r.width, r.height), s, st);
        st.normal.textColor = c;
        GUI.Label(r, s, st);
        st.normal.textColor = eski;
    }

    static void Plaka(Rect r, Color kenar)
    {
        LdrUI.Fill(r, new Color(0.04f, 0.015f, 0.08f, 0.82f));
        LdrUI.Frame(r, kenar, 2f);
        LdrUI.Frame(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), new Color(kenar.r, kenar.g, kenar.b, 0.25f), 1f);
        float d = 6f;
        LdrUI.Fill(new Rect(r.x - 2, r.y - 2, d, d), kenar); LdrUI.Fill(new Rect(r.xMax - d + 2, r.y - 2, d, d), kenar);
        LdrUI.Fill(new Rect(r.x - 2, r.yMax - d + 2, d, d), kenar); LdrUI.Fill(new Rect(r.xMax - d + 2, r.yMax - d + 2, d, d), kenar);
    }

    void Damla(Rect r, float dolu, Color c)
    {
        var eski = GUI.color;
        GUI.color = new Color(0.16f, 0.05f, 0.13f, 0.95f);
        GUI.DrawTexture(r, damlaDoku);
        if (dolu > 0.01f)
        {
            float h = r.height * Mathf.Clamp01(dolu);
            GUI.BeginGroup(new Rect(r.x, r.yMax - h, r.width, h));
            GUI.color = c;
            GUI.DrawTexture(new Rect(0, -(r.height - h), r.width, r.height), damlaDoku);
            GUI.EndGroup();
        }
        GUI.color = eski;
    }

    static Color GucRengi(string ad)
    {
        return ad == "GÜÇ" ? new Color(0.75f, 0.3f, 1f) : ad == "KALKAN" ? new Color(0.3f, 0.9f, 1f) : ad == "SINIRSIZ" ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.8f, 0.3f);
    }

    static float GucSuresi(string ad) { return ad == "GÜÇ" ? 12f : ad == "KALKAN" ? 8f : ad == "SINIRSIZ" ? 8f : 20f; }

    void OnGUI()
    {
        if (yasayan == null || LdrMenu.Acik) return;
        StilHazirla();
        bool cizim = Event.current.type == EventType.Repaint;
        if (!cizim) return;
        GUI.depth = -50;

        float W = Screen.width, H = Screen.height;
        float sc = H / 720f;
        float t = Time.unscaledTime;
        var o = LdrOyun.Ornek;
        var can = yasayan.Can;
        var silah = yasayan.Silah;
        bool oluyor = LiveDieRepeat.Dying;

        if (!oluyor && Time.timeScale > 0.001f)
        {
            // ---- sol üst: RUH + ZİNCİR
            Rect sp = new Rect(34 * sc, 26 * sc, 250 * sc, 78 * sc);
            Plaka(sp, Mor);
            float fl = 1f + 0.08f * Mathf.Sin(t * 9f) + ruhNabzi * 0.25f;
            Rect ic = new Rect(sp.x + 14 * sc, sp.y + 12 * sc, 28 * sc * fl, 38 * sc * fl);
            GUI.color = new Color(0.45f, 0.95f, 1f, 0.95f); GUI.DrawTexture(ic, damlaDoku); GUI.color = Color.white;
            sol.fontSize = Mathf.RoundToInt(11 * sc);
            Golge(new Rect(sp.x + 54 * sc, sp.y + 6 * sc, 120 * sc, 16 * sc), "RUH", sol, new Color(0.7f, 0.65f, 0.9f), 1f);
            sol.fontSize = Mathf.RoundToInt((30 + 6 * ruhNabzi) * sc);
            Golge(new Rect(sp.x + 54 * sc, sp.y + 18 * sc, 190 * sc, 34 * sc), Mathf.RoundToInt(gosterilenRuh).ToString("N0"), sol, Color.Lerp(Color.white, new Color(0.5f, 1f, 1f), ruhNabzi), 2f);
            sol.fontSize = Mathf.RoundToInt(11 * sc);
            Golge(new Rect(sp.x + 14 * sc, sp.y + 56 * sc, 60 * sc, 16 * sc), "ZİNCİR", sol, new Color(0.7f, 0.65f, 0.9f), 1f);
            int halka = Mathf.Min(LdrPuan.Zincir, 8);
            for (int i = 0; i < 8; i++)
            {
                Rect lr = new Rect(sp.x + 70 * sc + i * 14 * sc, sp.y + 59 * sc, 11 * sc, 9 * sc);
                bool acik = i < halka;
                Color lc = acik ? Color.Lerp(new Color(0.7f, 0.35f, 1f), new Color(1f, 0.8f, 0.3f), i / 7f) : new Color(0.25f, 0.18f, 0.35f);
                LdrUI.Frame(lr, lc, 2f * Mathf.Max(1f, sc));
                if (acik) LdrUI.Fill(new Rect(lr.x + 3, lr.y + 3, lr.width - 6, lr.height - 6), new Color(lc.r, lc.g, lc.b, 0.5f));
            }
            sol.fontSize = Mathf.RoundToInt(17 * sc);
            Golge(new Rect(sp.xMax - 52 * sc, sp.y + 53 * sc, 48 * sc, 22 * sc), "x" + LdrPuan.Zincir, sol, new Color(1f, 0.85f, 0.35f), 1.5f);

            // ---- alt orta: silah plakası
            float pwid = 410 * sc, phei = 88 * sc;
            Rect pr = new Rect(W * 0.5f - pwid * 0.5f, H - phei - 12 * sc, pwid, phei);
            Plaka(pr, Mor);
            LdrUI.Fill(new Rect(pr.center.x - 5 * sc, pr.y - 5 * sc, 10 * sc, 10 * sc), new Color(0.4f, 0.95f, 1f));
            var td = silah.Tanim;
            if (td != null)
            {
                Rect yuva = new Rect(pr.x + 12 * sc, pr.y + 12 * sc, 112 * sc, phei - 24 * sc);
                LdrUI.Fill(yuva, new Color(0.1f, 0.04f, 0.16f, 1f)); LdrUI.Frame(yuva, new Color(0.4f, 0.2f, 0.6f), 1.5f);
                var tex = LdrResim.Tex(td.cizim);
                if (tex != null)
                {
                    float en = tex.width / (float)tex.height;
                    float gw = Mathf.Min(yuva.width - 14 * sc, (yuva.height - 14 * sc) * en), gh = gw / en;
                    GUI.color = Color.Lerp(Color.white, td.renk, 0.55f);
                    GUI.DrawTexture(new Rect(yuva.center.x - gw * 0.5f, yuva.center.y - gh * 0.5f, gw, gh), tex, ScaleMode.ScaleToFit, true);
                    GUI.color = Color.white;
                }
                float tx = pr.x + 136 * sc, tw = pr.width - 148 * sc;
                sol.fontSize = Mathf.RoundToInt(17 * sc);
                Golge(new Rect(tx, pr.y + 8 * sc, tw, 22 * sc), td.ad + "  [" + (LdrSilahTablosu.Oyuncu.IndexOf(td) + 1) + "]", sol, new Color(0.96f, 0.9f, 1f), 1.5f);
                int kap = Mathf.Max(1, td.sarjor);
                if (!td.sinirsizSarjor && kap <= 24)
                {
                    float pw = Mathf.Min(10 * sc, (tw - 4 * sc) / kap - 2 * sc);
                    for (int i = 0; i < kap; i++)
                        LdrUI.Fill(new Rect(tx + i * (pw + 2 * sc), pr.y + 34 * sc, pw, 14 * sc), i < silah.Mermi ? new Color(1f, 0.78f, 0.3f) : new Color(0.2f, 0.12f, 0.25f));
                }
                string sayi = silah.Sinirsiz ? (td.sinirsizSarjor ? "∞" : silah.Mermi + "  /  ∞") : silah.Mermi + "  /  " + silah.Yedek;
                bool bos = silah.Bos;
                sol.fontSize = Mathf.RoundToInt(21 * sc);
                Color nc = bos ? (Mathf.Sin(t * 14f) > 0 ? new Color(1f, 0.2f, 0.2f) : new Color(0.5f, 0.1f, 0.1f)) : new Color(1f, 0.85f, 0.4f);
                Golge(new Rect(tx, pr.y + 52 * sc, tw * 0.55f, 28 * sc), sayi, sol, nc, 1.5f);
                if (bos)
                {
                    sol.fontSize = Mathf.RoundToInt(13 * sc);
                    Golge(new Rect(tx + tw * 0.45f, pr.y + 54 * sc, tw * 0.55f, 24 * sc), "RUH SIZIYOR...", sol, new Color(1f, 0.4f, 0.35f), 1f);
                }
                else if (silah.Dolduruyor)
                {
                    Rect br = new Rect(tx + tw * 0.5f, pr.y + 60 * sc, tw * 0.5f, 10 * sc);
                    LdrUI.Fill(br, new Color(0.15f, 0.08f, 0.2f));
                    LdrUI.Fill(new Rect(br.x, br.y, br.width * silah.DoldurmaIlerleme, br.height), new Color(0.5f, 0.95f, 1f));
                    sol.fontSize = Mathf.RoundToInt(12 * sc);
                    Golge(new Rect(br.x, br.y - 16 * sc, br.width, 16 * sc), "ŞARJÖR", sol, new Color(0.6f, 0.95f, 1f), 1f);
                }
            }

            // ---- kan damlaları (can)
            float dw = 22 * sc, dh = 29 * sc, ara = 8 * sc;
            float topW = 5 * dw + 4 * ara;
            float oran = can.Oran;
            float dusuk = oran < 0.4f ? 1f : 0f;
            float atis = dusuk > 0f ? 1f + 0.12f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 7f)), 4f) : 1f;
            for (int i = 0; i < 5; i++)
            {
                float dolu = Mathf.Clamp01(oran * 5f - i);
                float k = atis * (1f + vurusNabzi * 0.25f);
                Rect dr = new Rect(W * 0.5f - topW * 0.5f + i * (dw + ara) - (k - 1f) * dw * 0.5f, pr.y - dh - 12 * sc - (k - 1f) * dh * 0.5f, dw * k, dh * k);
                Damla(dr, dolu, Color.Lerp(new Color(0.85f, 0.08f, 0.15f), new Color(1f, 0.55f, 0.6f), vurusNabzi));
            }

            // ---- sol alt: [T] ışınlanma ve [F] ateş topu
            var isin = o != null ? o.isinlanma : null;
            Rect tp = new Rect(34 * sc, H - 84 * sc, 170 * sc, 34 * sc);
            if (isin != null)
            {
                bool hazir = !isin.Sarjda && isin.Kalan <= 0f;
                Color c = isin.Sarjda ? new Color(0.9f, 0.6f, 1f) : (hazir ? new Color(0.4f, 0.95f, 1f) : new Color(0.55f, 0.4f, 0.7f));
                LdrUI.Fill(tp, new Color(0.05f, 0.02f, 0.09f, 0.85f)); LdrUI.Frame(tp, c, 1.5f);
                float f = isin.Sarjda ? Mathf.Clamp01(isin.SarjIlerleme) : (isin.Kalan > 0f ? 1f - isin.Kalan / LdrIsinlanma.BeklemeSuresi : 1f);
                LdrUI.Fill(new Rect(tp.x, tp.yMax - 4 * sc, tp.width * f, 4 * sc), c);
                sol.fontSize = Mathf.RoundToInt(12 * sc);
                string ys = isin.Sarjda ? "IŞINLANIYOR..." : (isin.Kalan > 0f ? "DOĞUŞ  " + Mathf.CeilToInt(isin.Kalan) + "sn" : "[T] DOĞUŞA IŞINLAN");
                Golge(new Rect(tp.x + 8 * sc, tp.y, tp.width - 10 * sc, tp.height - 4 * sc), ys, sol, c, 1f);
            }
            var buf = yasayan.Buflar;
            if (buf != null && buf.AtesTopu > 0)
            {
                Rect fr = new Rect(tp.xMax + 8 * sc, tp.y, 120 * sc, tp.height);
                Color fc = new Color(1f, 0.55f, 0.2f);
                LdrUI.Fill(fr, new Color(0.09f, 0.03f, 0.02f, 0.88f)); LdrUI.Frame(fr, fc, 1.5f);
                sol.fontSize = Mathf.RoundToInt(12 * sc);
                Golge(new Rect(fr.x + 8 * sc, fr.y, fr.width - 10 * sc, fr.height), "[F] ATEŞ x" + buf.AtesTopu, sol, fc, 1f);
            }

            // ---- etkin güçler (alt orta)
            if (buf != null)
            {
                buf.Liste(guclar);
                float bx = W * 0.5f - (guclar.Count * 130f * sc) * 0.5f;
                for (int i = 0; i < guclar.Count; i++)
                {
                    var g = guclar[i];
                    Color gc = GucRengi(g.Key);
                    Rect br = new Rect(bx, H - 215 * sc, 120 * sc, 34 * sc);
                    LdrUI.Fill(br, new Color(0, 0, 0, 0.6f));
                    LdrUI.Fill(new Rect(br.x, br.yMax - 6 * sc, br.width * Mathf.Clamp01(g.Value / GucSuresi(g.Key)), 6 * sc), gc);
                    LdrUI.Frame(br, new Color(gc.r, gc.g, gc.b, 0.9f), 1.5f);
                    orta.fontSize = Mathf.RoundToInt(13 * sc);
                    Golge(new Rect(br.x, br.y, br.width, br.height - 6 * sc), g.Key + "  " + Mathf.CeilToInt(g.Value) + "sn", orta, Color.white, 1f);
                    bx += 130f * sc;
                }
            }

            // ---- etkileşim ipucu
            var ip = o != null ? o.Ipucu : null;
            if (ip != null)
            {
                // küçük, yarı saydam etkileşim ipucu (yazıya göre genişler; arka plan siyah blok değil)
                string y2 = ip.Yazi2;
                bool ikiSatir = !string.IsNullOrEmpty(y2);
                int f1 = LdrUI.Px(13 * sc), f2 = LdrUI.Px(9 * sc);
                float ipW = Mathf.Max(ip.Yazi1.Length * f1 * LdrUI.CharW, ikiSatir ? y2.Length * f2 * LdrUI.CharW : 0f) + 36 * sc;
                float ipH = (ikiSatir ? 50 : 30) * sc;
                var r = new Rect(W * 0.5f - ipW * 0.5f, H * 0.66f, ipW, ipH);
                LdrUI.Panel(r, LdrUI.Purple, sc, 0.28f, 0.25f);
                if (ikiSatir)
                {
                    piksel.fontSize = f2;
                    LdrUI.Text(new Rect(r.x, r.y + 4 * sc, r.width, 18 * sc), y2, piksel, new Color(0.85f, 0.6f, 1f));
                }
                piksel.fontSize = f1;
                LdrUI.Text(new Rect(r.x, r.y + (ikiSatir ? 22 : 4) * sc, r.width, 24 * sc), ip.Yazi1, piksel, LdrUI.Bone);
            }

            // ---- bölüm başlığı (kat başında)
            if (baslik != null && baslikZaman < 3f)
            {
                float a = baslikZaman < 0.5f ? baslikZaman / 0.5f : baslikZaman > 2.2f ? (3f - baslikZaman) / 0.8f : 1f;
                piksel.fontSize = LdrUI.Px(40 * sc);
                LdrUI.Text(new Rect(0, H * 0.28f, W, 72 * sc), baslik, piksel, new Color(0.85f, 0.6f, 1f, Mathf.Clamp01(a)));
            }

            // ---- yukarı süzülen bildirimler
            float dts = Time.unscaledDeltaTime;
            for (int i = bildirimler.Count - 1; i >= 0; i--)
            {
                var p = bildirimler[i];
                p.t += dts;
                if (p.t > 3f) { bildirimler.RemoveAt(i); continue; }
                float a = p.t < 0.3f ? p.t / 0.3f : Mathf.Clamp01((3f - p.t) / 0.8f);
                int pfs = LdrUI.Px(16 * sc);
                piksel.fontSize = pfs;
                float pw3 = p.yazi.Length * pfs * LdrUI.CharW + 48 * sc, ph3 = pfs + 16 * sc;
                Rect pr3 = new Rect((W - pw3) * 0.5f, H * 0.2f - p.t * 14f * sc + i * (ph3 + 6 * sc), pw3, ph3);
                LdrUI.Fill(pr3, new Color(0.04f, 0.015f, 0.08f, 0.82f * a));
                LdrUI.Frame(pr3, new Color(p.renk.r, p.renk.g, p.renk.b, 0.8f * a), Mathf.Max(1f, 1.5f * sc));
                LdrUI.Text(pr3, p.yazi, piksel, new Color(p.renk.r, p.renk.g, p.renk.b, a), 1f);
            }
        }

        // ---- perdeler
        if (Flas > 0f && !oluyor) LdrUI.Fill(new Rect(0, 0, W, H), new Color(0.62f, 0.3f, 1f, Mathf.Clamp01(Flas) * 0.55f));
        if (Karartma > 0f) LdrUI.Fill(new Rect(0, 0, W, H), new Color(KarartmaRenk.r, KarartmaRenk.g, KarartmaRenk.b, Karartma));
        if (!string.IsNullOrEmpty(OrtaYazi))
        {
            piksel.fontSize = LdrUI.Px(36 * sc);
            LdrUI.Text(new Rect(0, H * 0.34f, W, H * 0.32f), OrtaYazi, piksel, Color.white);
        }

        // ---- son ekran
        if (!string.IsNullOrEmpty(SonYazi))
        {
            LdrUI.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.9f));
            piksel.fontSize = LdrUI.Px(40 * sc);
            LdrUI.Text(new Rect(0, H * 0.4f - 40 * sc, W, 80 * sc), SonYazi, piksel, SonRenk);
            if (!string.IsNullOrEmpty(SonAlt))
            {
                piksel.fontSize = LdrUI.Px(18 * sc);
                LdrUI.Text(new Rect(0, H * 0.4f + 50 * sc, W, 40 * sc), SonAlt, piksel, LdrUI.Bone);
            }
        }
    }
}
