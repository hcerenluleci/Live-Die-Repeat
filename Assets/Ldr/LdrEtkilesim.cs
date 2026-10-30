using System.Collections.Generic;
using UnityEngine;

/// <summary>E tuşuyla kullanılan şeyler (sandık, çıkış kapısı) bu arayüzü uygular.</summary>
public interface ILdrEtkilesim
{
    Vector2 Konum { get; }
    string Yazi1 { get; }          // ana satır: "[E]  SANDIĞI AÇ"
    string Yazi2 { get; }          // üstteki küçük satır (boş olabilir)
    bool Hazir { get; }
    void Etkilesim(LdrYasayan oyuncu);
}

/// <summary>Sahnedeki etkileşim noktalarının kaydı ve en yakınını bulma.</summary>
public static class LdrEtkilesim
{
    static readonly List<ILdrEtkilesim> liste = new List<ILdrEtkilesim>();

    public static void Kaydet(ILdrEtkilesim e) { if (!liste.Contains(e)) liste.Add(e); }
    public static void Sil(ILdrEtkilesim e) { liste.Remove(e); }
    public static void Temizle() { liste.Clear(); }

    public static ILdrEtkilesim EnYakin(Vector2 konum, float menzil)
    {
        ILdrEtkilesim en = null;
        float ed = menzil;
        for (int i = 0; i < liste.Count; i++)
        {
            var e = liste[i];
            if (e == null || !e.Hazir) continue;
            float d = Vector2.Distance(konum, e.Konum);
            if (d < ed) { ed = d; en = e; }
        }
        return en;
    }
}
