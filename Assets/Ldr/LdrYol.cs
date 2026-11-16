using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// A* YOL BULMA  -  Ali Çağrı Sepet
// Izgara: 1x1 hücreler. g = başlangıçtan gelinen yol, h = hedefe kuş uçuşu (sezgisel), en küçük f = g + h açılır.
// Düşman sadece düz yol kapalıyken kullanır (performans için); bulunan yol düşmana ara noktalar olarak verilir.
// =====================================================================================
/// <summary>
/// Yol bulma (A*). Düşman, oyuncuya giden doğrudan yol masa, heykel ya da
/// duvar tarafından kapalıysa 1x1'lik ızgarada A* ile dolanarak giden bir yol bulur. Izgara hücreleri aranırken tembelce
/// (yalnızca gezilen hücreler) fizik motorundan sorulur, bu yüzden devrilip itilen masalar da hesaba katılır.
/// </summary>
public static class LdrYol
{
    static readonly Collider2D[] tampon = new Collider2D[6];
    static ContactFilter2D filtre;
    static bool hazir;
    static readonly Dictionary<int, bool> engel = new Dictionary<int, bool>();
    static readonly Dictionary<int, float> g = new Dictionary<int, float>();
    static readonly Dictionary<int, int> gelen = new Dictionary<int, int>();
    static readonly List<int> acik = new List<int>();
    static readonly HashSet<int> kapali = new HashSet<int>();
    const int EnFazla = 1600;
    static int oMinX, oMinY;             // anahtar = (x - oMinX) * 4096 + (y - oMinY): negatif koordinatlarda da çalışsın
    static int Anahtar(int x, int y) { return (x - oMinX) * 4096 + (y - oMinY); }
    static readonly int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
    static readonly int[] dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

    static bool Dolu(LdrOda oda, int cx, int cy, float yaricap)
    {
        int anahtar = Anahtar(cx, cy);
        bool b;
        if (engel.TryGetValue(anahtar, out b)) return b;
        var p = new Vector2(cx + 0.5f, cy + 0.5f);
        b = !oda.Yuruyebilir(p);
        if (!b)
        {
            if (!hazir)
            {
                filtre = new ContactFilter2D();
                filtre.SetLayerMask(LayerMask.GetMask("Duvar", "MermiGecer", "Esya"));
                filtre.useTriggers = false;
                hazir = true;
            }
            b = Physics2D.OverlapCircle(p, yaricap, filtre, tampon) > 0;
        }
        engel[anahtar] = b;
        return b;
    }

    // A* araması: başlangıçtan hedefe engellere çarpmayan en kısa yolu "yol" listesine doldurur; bulursa true, bulamazsa false döner.
    /// <summary>Başlangıçtan hedefe hücre merkezlerinden geçen bir yol (başlangıç hariç) doldurur. Yol yoksa false.</summary>
    public static bool Bul(LdrOda oda, Vector2 bas, Vector2 hedef, float yaricap, List<Vector2> yol)
    {
        yol.Clear();
        if (oda == null) return false;
        yaricap = Mathf.Min(yaricap * 0.85f, 0.45f);
        int minx = Mathf.FloorToInt(oda.sinir.xMin), miny = Mathf.FloorToInt(oda.sinir.yMin);
        int maxx = Mathf.CeilToInt(oda.sinir.xMax) - 1, maxy = Mathf.CeilToInt(oda.sinir.yMax) - 1;
        oMinX = minx; oMinY = miny;
        int sx = Mathf.Clamp(Mathf.FloorToInt(bas.x), minx, maxx), sy = Mathf.Clamp(Mathf.FloorToInt(bas.y), miny, maxy);
        int hx = Mathf.Clamp(Mathf.FloorToInt(hedef.x), minx, maxx), hy = Mathf.Clamp(Mathf.FloorToInt(hedef.y), miny, maxy);
        if (sx == hx && sy == hy) return false;

        engel.Clear(); g.Clear(); gelen.Clear(); acik.Clear(); kapali.Clear();
        int bas0 = Anahtar(sx, sy), hedef0 = Anahtar(hx, hy);
        g[bas0] = 0f; gelen[bas0] = -1; acik.Add(bas0);
        int tur = 0;
        int bulunan = -1;
        while (acik.Count > 0 && tur++ < EnFazla)
        {
            // en küçük f = g + sezgisel
            int en = 0; float enF = float.MaxValue;
            for (int i = 0; i < acik.Count; i++)
            {
                int k = acik[i];
                float h = Sezgi(k / 4096 + minx, k % 4096 + miny, hx, hy);
                float f = g[k] + h;
                if (f < enF) { enF = f; en = i; }
            }
            int mevcut = acik[en];
            acik.RemoveAt(en);
            if (mevcut == hedef0) { bulunan = mevcut; break; }
            kapali.Add(mevcut);
            int cx = mevcut / 4096 + minx, cy = mevcut % 4096 + miny;
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + dx[d], ny = cy + dy[d];
                if (nx < minx || ny < miny || nx > maxx || ny > maxy) continue;
                int nk = Anahtar(nx, ny);
                if (kapali.Contains(nk)) continue;
                if (nk != hedef0 && Dolu(oda, nx, ny, yaricap)) continue;
                if (d >= 4 && (Dolu(oda, cx + dx[d], cy, yaricap) || Dolu(oda, cx, cy + dy[d], yaricap))) continue;   // köşeden kesme yok
                float ng = g[mevcut] + (d < 4 ? 1f : 1.4142f);
                float eski;
                if (g.TryGetValue(nk, out eski) && eski <= ng) continue;
                g[nk] = ng; gelen[nk] = mevcut;
                if (!acik.Contains(nk)) acik.Add(nk);
            }
        }
        if (bulunan < 0) return false;
        for (int k = bulunan; k != -1 && k != bas0; k = gelen[k]) yol.Add(new Vector2(k / 4096 + minx + 0.5f, k % 4096 + miny + 0.5f));
        yol.Reverse();
        return yol.Count > 0;
    }

    // A*'ın sezgisel (heuristic) değeri: hedefe kalan kuş uçuşu uzaklık.
    static float Sezgi(int x, int y, int hx, int hy)
    {
        float a = Mathf.Abs(x - hx), b = Mathf.Abs(y - hy);
        return (a + b) + (1.4142f - 2f) * Mathf.Min(a, b);
    }
}
