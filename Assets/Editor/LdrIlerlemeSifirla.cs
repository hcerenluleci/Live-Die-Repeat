#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Geçici araç: test sırasında birikmiş kayıtları (ölüm sayacı, toplam ruh, puan tablosu, açılan silahlar,
/// kaçış parçaları, ışık ve ses ayarları, karakter seçimi) sıfırlar. Teslimden önce ya da sıfırdan oynamak için kullan.
/// Play kapalıyken çalıştır.
/// </summary>
public static class LdrIlerlemeSifirla
{
    [MenuItem("Ldr/İlerlemeyi sıfırla (ölüm, ruh, puan, ayarlar)")]
    static void Sifirla()
    {
        if (Application.isPlaying) { Debug.LogWarning("Önce Play'i durdur."); return; }
        if (!EditorUtility.DisplayDialog("İlerlemeyi sıfırla", "Ölüm sayacı, ruhlar, puan tablosu, açılan silahlar, kaçış parçaları ve ayarlar silinecek. Emin misin?", "Sıfırla", "Vazgeç")) return;
        LdrIlerleme.HepsiniSifirla();
        PlayerPrefs.DeleteKey("LDR_Deaths");
        PlayerPrefs.DeleteKey("LDR_Skin");
        PlayerPrefs.DeleteKey("LDR_Light2");
        PlayerPrefs.Save();
        Debug.Log("LdrIlerlemeSifirla: kayıtlar sıfırlandı.");
    }
}
#endif
