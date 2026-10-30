using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Derinlik sırası. Canavarlar masa/heykel gibi cisimlerin ÜSTÜNE çıkmış gibi görünmemeli; bunu sağlayan
///  1) fizik katmanı matrisi (Dusman x Esya/Duvar/MermiGecer çarpışır) onları cisimlerin içine sokmaz, 2) yol bulma (A*) etrafından dolaştırır.
/// Burada ek olarak çizim sırası düzeltilir: bir cismin ARKASINDA (daha yukarıda) duran karakter cismin altında çizilir,
/// önünde duran üstünde. Böylece cismin tepesi karakterin gövdesini örter, "üstüne çıkmış" gibi durmaz.
/// Cisimler (masa, heykel, ayakta meşale, sandık) kendi çarpışma kutusunu buraya kaydeder.
/// </summary>
public static class LdrSira
{
    public const int Geri = -6;      // arkada kalan karakterin çizim sırasından çıkarılan değer
    static readonly List<Collider2D> engeller = new List<Collider2D>();

    public static void Kaydet(Collider2D c) { if (c != null && !engeller.Contains(c)) engeller.Add(c); }
    public static void Sil(Collider2D c) { engeller.Remove(c); }

    /// <summary>Ayak noktası bir cismin arkasında mı (cismin ortasından yukarıda ve cismin gövde yüksekliği içinde).</summary>
    public static bool Arkada(Vector2 ayak)
    {
        for (int i = engeller.Count - 1; i >= 0; i--)
        {
            var c = engeller[i];
            if (c == null) { engeller.RemoveAt(i); continue; }
            if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
            Bounds b = c.bounds;
            if (ayak.x < b.min.x - 0.7f || ayak.x > b.max.x + 0.7f) continue;
            if (ayak.y > b.center.y && ayak.y < b.max.y + 1.6f) return true;
        }
        return false;
    }
}

/// <summary>Karakterin (oyuncu / canavar) tüm çizimlerinin sırasını, bir cismin arkasındaysa geriye alır.</summary>
public class LdrSiraliGorunum : MonoBehaviour
{
    public float ayakKaydirma = -0.8f;       // karakter merkezinden ayağa uzaklık

    SpriteRenderer[] srler;
    int[] temel;
    bool geride;
    float gecikme = 0.2f;

    void Hazirla()
    {
        var hepsi = GetComponentsInChildren<SpriteRenderer>(true);
        var liste = new List<SpriteRenderer>();
        foreach (var s in hepsi) if (s.sortingOrder < 18) liste.Add(s);      // can çubuğu (20+) hariç
        srler = liste.ToArray();
        temel = new int[srler.Length];
        for (int i = 0; i < srler.Length; i++) temel[i] = srler[i].sortingOrder;
    }

    void LateUpdate()
    {
        gecikme -= Time.deltaTime;
        if (gecikme > 0f) return;
        if (srler == null) { Hazirla(); }
        Vector2 ayak = (Vector2)transform.position + new Vector2(0f, ayakKaydirma);
        bool g = LdrSira.Arkada(ayak);
        if (g == geride) return;
        geride = g;
        for (int i = 0; i < srler.Length; i++)
            if (srler[i] != null) srler[i].sortingOrder = temel[i] + (g ? LdrSira.Geri : 0);
    }
}
