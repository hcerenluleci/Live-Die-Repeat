using UnityEngine;

/// <summary>
/// Yaratık çizimleri: ekibin kendi yaratık sayfası.
/// 64x160 sayfa: her satır bir tür (2 kare, 32x32). Sıra: Sızıntı (Ooze), Kızıl Sızıntı (OozeKing), Ağıtçı (Wraith), Bakıcı (Watcher), Sürünen (Shambler).
/// </summary>
public static class LdrYaratikSayfa
{
    const string Png =
        "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" +
        "Aq4DL8Odod25AAAAAElFTkSuQmCC";

    static Texture2D sayfa;

    public static Texture2D Sayfa()
    {
        if (sayfa == null)
        {
            sayfa = new Texture2D(64, 160, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            sayfa.LoadImage(System.Convert.FromBase64String(Png));
            sayfa.filterMode = FilterMode.Point;
        }
        return sayfa;
    }
}
