using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Animator denetleyicilerini ve kliplerini üretir (Assets/Resources/Animasyon):
///   OyuncuAnim : Durus, Yuru, Takla, Olu            (parametreler: Hareket, Takla, Olu)
///   DusmanAnim : Uyuyor, Uyari, Kovala, Saldir, Sendeler, Oldu   (parametre: Durum = LdrDusmanDurumu)
/// Klipler LdrAnimKopru alanlarını (kare, olcekX, olcekY, egim) canlandırır.
/// Proje açılırken denetleyiciler yoksa kendiliğinden kurulur; menüden (Ldr > Animator'ları Kur) yeniden kurulabilir.
/// </summary>
[InitializeOnLoad]
public static class LdrAnimasyonKur
{
    const string Klasor = "Assets/Resources/Animasyon";

    static LdrAnimasyonKur()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Klasor + "/OyuncuAnim.controller") || !File.Exists(Klasor + "/DusmanAnim.controller")) Kur();
        };
    }

    [MenuItem("Ldr/Animator'ları Kur")]
    public static void Kur()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Klasor)) AssetDatabase.CreateFolder("Assets/Resources", "Animasyon");
        OyuncuKur();
        DusmanKur();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Ldr] Animator denetleyicileri kuruldu: " + Klasor);
    }

    // ---------------------------------------------------------------- yardımcılar

    static AnimationClip Klip(string ad, float uzunluk, bool dongu, params (string alan, Keyframe[] anahtarlar)[] egriler)
    {
        string yol = Klasor + "/" + ad + ".anim";
        AssetDatabase.DeleteAsset(yol);
        var c = new AnimationClip { name = ad, frameRate = 60f };
        foreach (var e in egriler)
            AnimationUtility.SetEditorCurve(c, EditorCurveBinding.FloatCurve("", typeof(LdrAnimKopru), e.alan), new AnimationCurve(e.anahtarlar));
        var ayar = AnimationUtility.GetAnimationClipSettings(c);
        ayar.loopTime = dongu;
        ayar.stopTime = uzunluk;
        AnimationUtility.SetAnimationClipSettings(c, ayar);
        AssetDatabase.CreateAsset(c, yol);
        return c;
    }

    static Keyframe[] Sabit(float deger, float uzunluk) { return new[] { new Keyframe(0f, deger), new Keyframe(uzunluk, deger) }; }

    /// <summary>a'dan b'ye doğrusal.</summary>
    static Keyframe[] Iki(float a, float b, float uzunluk)
    {
        float e = (b - a) / uzunluk;
        return new[] { new Keyframe(0f, a, e, e), new Keyframe(uzunluk, b, e, e) };
    }

    /// <summary>a -> b -> a (yarı yolda tepe), yumuşak.</summary>
    static Keyframe[] Nabiz(float a, float b, float uzunluk)
    {
        return new[] { new Keyframe(0f, a, 0f, 0f), new Keyframe(uzunluk * 0.5f, b, 0f, 0f), new Keyframe(uzunluk, a, 0f, 0f) };
    }

    /// <summary>İki kareli yürüyüş: ilk yarı 0, ikinci yarı 1 (basamaklı).</summary>
    static Keyframe[] AdimKareleri(float uzunluk)
    {
        float y = uzunluk * 0.5f, e = 0.0005f;
        return new[] { new Keyframe(0f, 0f), new Keyframe(y - e, 0f), new Keyframe(y, 1f), new Keyframe(uzunluk - e, 1f), new Keyframe(uzunluk, 0f) };
    }

    static AnimatorState Durum(AnimatorStateMachine sm, string ad, Motion klip, Vector3 konum)
    {
        var s = sm.AddState(ad, konum);
        s.motion = klip;
        s.writeDefaultValues = false;
        return s;
    }

    static void Gecis(AnimatorState a, AnimatorState b, string param, AnimatorConditionMode mod, float esik = 0f)
    {
        var t = a.AddTransition(b);
        t.hasExitTime = false; t.duration = 0f; t.hasFixedDuration = true;
        t.AddCondition(mod, esik, param);
    }

    static void HerYerden(AnimatorStateMachine sm, AnimatorState b, string param, AnimatorConditionMode mod, float esik = 0f)
    {
        var t = sm.AddAnyStateTransition(b);
        t.hasExitTime = false; t.duration = 0f; t.hasFixedDuration = true; t.canTransitionToSelf = false;
        t.AddCondition(mod, esik, param);
    }

    // ---------------------------------------------------------------- oyuncu

    static void OyuncuKur()
    {
        const float takla = 0.36f;   // LdrYasayan.taklaSuresi ile aynı
        var durus = Klip("Oyuncu_Durus", 1.6f, true,
            ("kare", Sabit(0f, 1.6f)), ("olcekX", Sabit(1f, 1.6f)), ("olcekY", Nabiz(1f, 1.015f, 1.6f)), ("egim", Sabit(0f, 1.6f)));
        var yuru = Klip("Oyuncu_Yuru", 0.36f, true,
            ("kare", AdimKareleri(0.36f)), ("olcekX", Sabit(1f, 0.36f)), ("olcekY", Sabit(1f, 0.36f)), ("egim", Sabit(0f, 0.36f)));
        var taklaK = Klip("Oyuncu_Takla", takla, false,
            ("kare", Sabit(0f, takla)), ("olcekX", Sabit(1f, takla)), ("olcekY", Sabit(0.82f, takla)), ("egim", Iki(0f, 360f, takla)));
        var olu = Klip("Oyuncu_Olu", 0.25f, false,
            ("kare", Sabit(0f, 0.25f)), ("olcekX", Sabit(1f, 0.25f)), ("olcekY", Sabit(1f, 0.25f)), ("egim", Iki(0f, 90f, 0.25f)));

        string yol = Klasor + "/OyuncuAnim.controller";
        AssetDatabase.DeleteAsset(yol);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(yol);
        ac.AddParameter("Hareket", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Takla", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Olu", AnimatorControllerParameterType.Bool);
        var sm = ac.layers[0].stateMachine;
        var sDurus = Durum(sm, "Durus", durus, new Vector3(300, 0));
        var sYuru = Durum(sm, "Yuru", yuru, new Vector3(300, 120));
        var sTakla = Durum(sm, "Takla", taklaK, new Vector3(560, 60));
        var sOlu = Durum(sm, "Olu", olu, new Vector3(560, 200));
        sm.defaultState = sDurus;

        Gecis(sDurus, sYuru, "Hareket", AnimatorConditionMode.If);
        Gecis(sYuru, sDurus, "Hareket", AnimatorConditionMode.IfNot);
        Gecis(sTakla, sDurus, "Takla", AnimatorConditionMode.IfNot);
        Gecis(sOlu, sDurus, "Olu", AnimatorConditionMode.IfNot);
        HerYerden(sm, sOlu, "Olu", AnimatorConditionMode.If);
        HerYerden(sm, sTakla, "Takla", AnimatorConditionMode.If);
        EditorUtility.SetDirty(ac);
    }

    // ---------------------------------------------------------------- düşman (FSM durumlarıyla bire bir)

    static void DusmanKur()
    {
        var uyuyor = Klip("Dusman_Uyuyor", 2.6f, true,
            ("kare", Sabit(0f, 2.6f)), ("olcekX", Nabiz(1f, 0.96f, 2.6f)), ("olcekY", Nabiz(1f, 1.04f, 2.6f)), ("egim", Sabit(0f, 2.6f)));
        var uyari = Klip("Dusman_Uyari", 0.55f, false,
            ("kare", Sabit(1f, 0.55f)), ("olcekX", Nabiz(1f, 1.2f, 0.55f)), ("olcekY", Nabiz(1f, 1.2f, 0.55f)), ("egim", Sabit(0f, 0.55f)));
        var kovala = Klip("Dusman_Kovala", 0.34f, true,
            ("kare", AdimKareleri(0.34f)), ("olcekX", Nabiz(1f, 0.96f, 0.34f)), ("olcekY", Nabiz(1f, 1.04f, 0.34f)), ("egim", Sabit(0f, 0.34f)));
        var saldir = Klip("Dusman_Saldir", 0.6f, true,
            ("kare", Sabit(0f, 0.6f)), ("olcekX", Nabiz(1f, 1.06f, 0.6f)), ("olcekY", Nabiz(1f, 0.95f, 0.6f)), ("egim", Sabit(0f, 0.6f)));
        var sendeler = Klip("Dusman_Sendeler", 0.16f, false,
            ("kare", Sabit(1f, 0.16f)), ("olcekX", Sabit(0.9f, 0.16f)), ("olcekY", Sabit(0.9f, 0.16f)), ("egim", Nabiz(0f, 8f, 0.16f)));
        var oldu = Klip("Dusman_Oldu", 0.3f, false,
            ("kare", Sabit(1f, 0.3f)), ("olcekX", Iki(1f, 1.4f, 0.3f)), ("olcekY", Iki(1f, 0f, 0.3f)), ("egim", Sabit(0f, 0.3f)));

        string yol = Klasor + "/DusmanAnim.controller";
        AssetDatabase.DeleteAsset(yol);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(yol);
        ac.AddParameter("Durum", AnimatorControllerParameterType.Int);
        var sm = ac.layers[0].stateMachine;
        // LdrDusmanDurumu sırası: Uyuyor 0, Uyari 1, Kovala 2, Saldir 3, Sendeler 4, Oldu 5
        var klipler = new[] { uyuyor, uyari, kovala, saldir, sendeler, oldu };
        var adlar = new[] { "Uyuyor", "Uyari", "Kovala", "Saldir", "Sendeler", "Oldu" };
        for (int i = 0; i < klipler.Length; i++)
        {
            var s = Durum(sm, adlar[i], klipler[i], new Vector3(300 + (i % 2) * 260, i / 2 * 110));
            if (i == 0) sm.defaultState = s;
            HerYerden(sm, s, "Durum", AnimatorConditionMode.Equals, i);
        }
        EditorUtility.SetDirty(ac);
    }
}
