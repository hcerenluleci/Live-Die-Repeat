#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Araç: oyun sahnesini (Assets/_Scenes/LdrOyun.unity) sıfırdan kurar, derleme listesine ekler ve açar.</summary>
public static class LdrSahneKur
{
    const string Yol = "Assets/_Scenes/LdrOyun.unity";

    [MenuItem("Ldr/LdrOyun sahnesini kur ve aç")]
    static void Kur()
    {
        if (Application.isPlaying) { Debug.LogWarning("Önce Play'i durdur."); return; }
        EditorSceneManager.SaveOpenScenes();

        var sahne = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var kg = new GameObject("Main Camera");
        kg.tag = "MainCamera";
        var kam = kg.AddComponent<Camera>();
        kam.orthographic = true;
        kam.orthographicSize = 7f;
        kam.clearFlags = CameraClearFlags.SolidColor;
        kam.backgroundColor = Color.black;
        kg.transform.position = new Vector3(0, 0, -10);
        kg.AddComponent<AudioListener>();
        kg.AddComponent<UniversalAdditionalCameraData>();
        kg.AddComponent<LdrKamera>();

        var ig = new GameObject("GenelIsik");
        var isik = ig.AddComponent<Light2D>();
        isik.lightType = Light2D.LightType.Global;
        isik.intensity = 0.9f;

        new GameObject("LdrOyun").AddComponent<LdrOyun>();

        EditorSceneManager.SaveScene(sahne, Yol);

        var liste = EditorBuildSettings.scenes.ToList();
        if (!liste.Any(s => s.path == Yol))
        {
            liste.Add(new EditorBuildSettingsScene(Yol, true));
            EditorBuildSettings.scenes = liste.ToArray();
        }
        Debug.Log("LdrSahneKur: " + Yol + " kuruldu ve açıldı.");
    }
}
#endif
