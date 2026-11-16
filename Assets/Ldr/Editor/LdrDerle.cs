using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// itch.io için derleme: Ldr > Derle menüsü. Çıktılar proje kökündeki Builds/ klasörüne gider (Git'e girmez).
///   Windows : Builds/Windows/LIVE-DIE-REPEAT.exe  ->  Builds/LIVE-DIE-REPEAT_Windows.zip
///   WebGL   : Builds/WebGL/index.html             ->  Builds/LIVE-DIE-REPEAT_WebGL.zip (itch.io "Run game" için)
/// Fragman kayıt aracı ve deneme tuşları yalnızca editörde derlendiği için oyuna girmez.
/// </summary>
public static class LdrDerle
{
    const string Kok = "Builds";
    static readonly string[] Sahneler = { "Assets/_Scenes/LdrOyun.unity" };

    /// <summary>URP Uyumluluk Modu (editördeki görünümle birebir aynı çizim) için gerekli tanım.</summary>
    static void UyumlulukModu(NamedBuildTarget hedef)
    {
        string d = PlayerSettings.GetScriptingDefineSymbols(hedef);
        if (!d.Contains("URP_COMPATIBILITY_MODE"))
            PlayerSettings.SetScriptingDefineSymbols(hedef, string.IsNullOrEmpty(d) ? "URP_COMPATIBILITY_MODE" : d + ";URP_COMPATIBILITY_MODE");
    }

    static void OrtakAyarlar()
    {
        PlayerSettings.productName = "LIVE-DIE-REPEAT";
        PlayerSettings.companyName = "hcerenluleci";
        var ikon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Ldr/Ikon/LdrIkon.png");
        if (ikon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { ikon }, IconKind.Any);
    }

    [MenuItem("Ldr/Derle/Windows (itch.io zip)")]
    public static void Windows()
    {
        OrtakAyarlar();
        UyumlulukModu(NamedBuildTarget.Standalone);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);   // ek modül gerektirmez
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        string klasor = Path.Combine(Kok, "Windows");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        var r = BuildPipeline.BuildPlayer(Sahneler, Path.Combine(klasor, "LIVE-DIE-REPEAT.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) { Debug.LogError("[Derle] Windows başarısız: " + r.summary.result); return; }
        // itch.io'ya gerekmeyen hata ayıklama klasörünü çıkar
        foreach (var d in Directory.GetDirectories(klasor, "*BurstDebugInformation_DoNotShip")) Directory.Delete(d, true);
        Ziple(klasor, Path.Combine(Kok, "LIVE-DIE-REPEAT_Windows.zip"));
    }

    [MenuItem("Ldr/Derle/WebGL (itch.io tarayıcıda oyna)")]
    public static void WebGL()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            EditorUtility.DisplayDialog("WebGL modülü yok", "Unity Hub > Installs > 6000.3.24f1 > Add modules > Web Build Support kurulmalı.", "Tamam");
            return;
        }
        OrtakAyarlar();
        UyumlulukModu(NamedBuildTarget.WebGL);
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;   // itch.io sunucusunda sıkıştırma başlığı olmadan da açılır
        PlayerSettings.WebGL.dataCaching = true;
        string klasor = Path.Combine(Kok, "WebGL");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        var r = BuildPipeline.BuildPlayer(Sahneler, klasor, BuildTarget.WebGL, BuildOptions.None);
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) { Debug.LogError("[Derle] WebGL başarısız: " + r.summary.result); return; }
        Ziple(klasor, Path.Combine(Kok, "LIVE-DIE-REPEAT_WebGL.zip"));
    }

    static void Ziple(string klasor, string zip)
    {
        if (File.Exists(zip)) File.Delete(zip);
        ZipFile.CreateFromDirectory(klasor, zip, System.IO.Compression.CompressionLevel.Optimal, false);
        Debug.Log("[Derle] Hazır: " + Path.GetFullPath(zip));
        EditorUtility.RevealInFinder(zip);
    }
}
