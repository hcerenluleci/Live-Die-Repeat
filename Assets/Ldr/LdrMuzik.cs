using UnityEngine;

// =====================================================================================
// DERS GEREKSİNİMİ: SES  -  müzik
// İki döngü: music_horror_loop (menü ve keşif), music_battle_loop (oda kilitliyken savaş). Aralarında yumuşak geçiş yapılır.
// =====================================================================================
/// <summary>
/// Müzik. İki döngü (Assets/Resources: music_horror_loop = menü ve sakin anlar, music_battle_loop = oda savaşı)
/// birbirine yumuşakça geçer. Ses seviyesi duraklatma menüsündeki MÜZİK ayarından gelir (LdrSes.MuzikSeviyesi).
/// Sahneler arasında yaşar; kendi kendine kurulur.
/// </summary>
public class LdrMuzik : MonoBehaviour
{
    [Header("Seviyeler (0..1, ayar çarpanından önce)")]
    [SerializeField, Range(0f, 1f)] float sakinSes = 0.5f;
    [SerializeField, Range(0f, 1f)] float savasSes = 0.5f;
    [SerializeField, Tooltip("Saniyede ne kadar hızlı geçiş (1 = bir saniyede tam geçiş)")] float gecisHizi = 0.8f;

    AudioSource sakin, savas;
    float savasAgirlik;                 // 0 = sakin müzik, 1 = savaş müziği

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Kur()
    {
        var go = new GameObject("LdrMuzik");
        DontDestroyOnLoad(go);
        go.AddComponent<LdrMuzik>();
    }

    AudioSource KaynakYap(string klipAdi)
    {
        var k = gameObject.AddComponent<AudioSource>();
        k.loop = true;
        k.playOnAwake = false;
        k.spatialBlend = 0f;
        k.ignoreListenerPause = true;
        k.clip = Resources.Load<AudioClip>(klipAdi);
        k.volume = 0f;
        if (k.clip != null) k.Play();
        return k;
    }

    void Awake()
    {
        sakin = KaynakYap("music_horror_loop");
        savas = KaynakYap("music_battle_loop");
    }

    void Update()
    {
        var oyun = LdrOyun.Ornek;
        bool savasVar = oyun != null && oyun.SavasVar && !LdrMenu.Acik;
        savasAgirlik = Mathf.MoveTowards(savasAgirlik, savasVar ? 1f : 0f, gecisHizi * Time.unscaledDeltaTime);

        float ayar = LdrSes.MuzikCarpani;
        sakin.volume = Mathf.Min(1f, sakinSes * ayar * (1f - savasAgirlik));
        savas.volume = Mathf.Min(1f, savasSes * ayar * savasAgirlik);
        if (sakin.clip != null && !sakin.isPlaying) sakin.Play();
        if (savas.clip != null && !savas.isPlaying) savas.Play();
    }
}
