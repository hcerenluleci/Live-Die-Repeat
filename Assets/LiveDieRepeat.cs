using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// YAŞA - ÖL - TEKRARLA döngüsü.
/// Kendi kendine kurulur: sahneye elle eklemen gerekmez.
///  - Oyuncu ölünce (yok olunca / kapanınca) ölüm sayacını artırır ve kaydeder.
///  - Kırmızı "ÖLDÜN" ekranı gösterip sahneyi baştan başlatır.
///  - Her ölüm "Hafıza" seviyesini artırır; LdrFener bunu fener menzili için kullanır
///    (ne kadar çok ölürsen zindanı o kadar iyi hatırlarsın = ışığın büyür).
/// </summary>
// =====================================================================================
// MEKANİK 1: ÖLÜM DÖNGÜSÜ VE KALICI İLERLEME  (Hülya Ceren Lüleci)
// Akış: oyuncunun canı 0 olur -> LdrOyun.OyuncuOldu() -> LiveDieRepeat.ReportDeath()
//       -> RegisterDeath(): ölüm sayısı +1, PlayerPrefs'e kaydedilir, OnDeath olayı tetiklenir
//       -> LdrMenu.OlumOldu(): bu yaşamın ruhu toplam ruha eklenir, puan tablosuna yazılır
//       -> 2,5 sn "ÖLDÜN" ekranı -> Restart(): sahne baştan yüklenir (zindan yeniden kurulur).
// Kalıcı kalanlar (oyun kapansa bile): ölüm sayısı, toplam ruh, açılan silahlar, en derin bölüm, kaçış parçaları.
// =====================================================================================
public class LiveDieRepeat : MonoBehaviour
{
    const string DeathKey = "LDR_Deaths";          // ölüm sayısının PlayerPrefs'teki anahtarı (kalıcı kayıt)
    const string GameSceneKeyword = "MainGame";   // MainGameScene içinde çalışır
    const int MaxMemory = 10;                      // fener en fazla 10 ölüme kadar büyür

    public static LiveDieRepeat Instance { get; private set; }
    public static int Deaths { get; private set; }
    public static int MemoryLevel => Mathf.Min(Deaths, MaxMemory);   // "hafıza": ölüm sayısı (en çok 10); LdrFener ışık menzilini buna göre büyütür

    [Header("Ayarlar")]
    [Tooltip("Oyuncu nesnesinin adında geçen yazı (Hierarchy'de 'Yasayan(Clone)' görünüyor)")]
    public string playerNameContains = "Yasayan";
    [Tooltip("Oyuncu bu kadar saniye yoksa ölmüş sayılır")]
    public float deathDebounce = 0.75f;
    [Tooltip("Ölüm ekranının süresi")]
    public float restartDelay = 2.5f;

    public static bool Dying { get { return Instance != null && Instance.dying; } }
    public static event System.Action OnDeath;     // ölüm anında haber verilir (LdrMenu bunu dinleyip ruhu kaydeder)

    GameObject player;
    bool seenPlayer;
    float missingTime;
    float scanTimer;
    bool dying;
    float dyingTimer;
    GUIStyle hudStyle, bigStyle, smallStyle;

    // Oyun açılırken kendini otomatik oluşturur; sahneye elle eklemeye gerek yok.
    // DontDestroyOnLoad: sahne yeniden yüklense de bu nesne (ve ölüm sayısı) yok olmaz.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("LiveDieRepeat");
        DontDestroyOnLoad(go);
        go.AddComponent<LiveDieRepeat>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Deaths = PlayerPrefs.GetInt(DeathKey, 0);   // kayıtlı ölüm sayısını oku (ilk açılışta 0)
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    static bool IsGameScene()
    {
        string n = SceneManager.GetActiveScene().name;
        return n.Contains(GameSceneKeyword) || n == "LdrOyun";
    }

    // Her yeni sahnede (yeniden doğuşta) ölüm durumunu sıfırlar, oyun hızını normale döndürür.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        player = null;
        seenPlayer = false;
        missingTime = 0f;
        dying = false;
        dyingTimer = 0f;
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Otomatik algılama çalışmazsa oyuncunun ölüm fonksiyonundan şunu çağır:
    /// LiveDieRepeat.ReportDeath();
    /// </summary>
    public static void ReportDeath()
    {
        if (Instance != null) Instance.RegisterDeath();
    }

    // Her kare: ölüm ekranındaysak süreyi say ve bitince yeniden başlat.
    // Değilsek oyuncuyu takip et; oyuncu 0,75 sn ortadan kaybolursa (yedek yol) ölüm say.
    void Update()
    {
        if (!IsGameScene()) return;

        if (dying)
        {
            dyingTimer += Time.unscaledDeltaTime;
            if (dyingTimer >= restartDelay) Restart();
            return;
        }

        if (player == null || !player.activeInHierarchy)
        {
            if (seenPlayer)
            {
                missingTime += Time.unscaledDeltaTime;
                if (missingTime >= deathDebounce) { RegisterDeath(); return; }
            }

            scanTimer -= Time.unscaledDeltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = 0.25f;
                var found = FindPlayer();
                if (found != null) { player = found; seenPlayer = true; missingTime = 0f; }
            }
        }
        else
        {
            missingTime = 0f;
        }
    }

    GameObject FindPlayer()
    {
        try
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) return tagged;
        }
        catch (UnityException) { /* "Player" etiketi tanımlı değil */ }

        var pc = FindFirstObjectByType<LdrYasayan>();   // yeni çekirdeğin oyuncu bileşeni
        if (pc != null) return pc.gameObject;

        if (!string.IsNullOrEmpty(playerNameContains))
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.gameObject.activeInHierarchy && t.name.Contains(playerNameContains))
                    return t.gameObject;
        }
        return null;
    }

    // ÖLÜMÜ KAYDET: sayacı 1 artırır, diğer sistemlere haber verir, PlayerPrefs'e yazar.
    // "dying" bayrağı aynı ölümün iki kez sayılmasını engeller.
    void RegisterDeath()
    {
        if (dying) return;
        dying = true;
        dyingTimer = 0f;
        Deaths++;
        try { if (OnDeath != null) OnDeath(); } catch (System.Exception e) { Debug.LogWarning(e.Message); }
        PlayerPrefs.SetInt(DeathKey, Deaths);       // kalıcı kayıt: oyun kapansa da ölüm sayısı kalır
        PlayerPrefs.Save();
    }

    // YENİDEN DOĞ: aynı sahneyi baştan yükler; zindan yeni bir düzenle tekrar kurulur.
    void Restart()
    {
        dying = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ---------- Basit arayüz (OnGUI, ek paket gerektirmez) ----------
    void EnsureStyles()
    {
        if (hudStyle != null) return;
        // pikselli yazı tipi (LdrUI ile aynı): menü, HUD ve ölüm ekranı tek stilde
        hudStyle = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
        bigStyle = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
        smallStyle = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
    }

    void OnGUI()
    {
        if (!IsGameScene()) return;
        EnsureStyles();
        GUI.depth = dying ? -300 : 0;   // ölüm ekranı her şeyin üstünde
        bool rep = Event.current.type == EventType.Repaint;
        float sc = Screen.height / 720f, W = Screen.width, H = Screen.height;

        if (rep)
        {
            // sağ üst: ölüm sayacı rozeti
            Rect hr = new Rect(W - 212 * sc - 34 * sc, 26 * sc, 212 * sc, 30 * sc);
            LdrUI.Fill(hr, new Color(0.14f, 0.02f, 0.05f, 0.88f));
            LdrUI.Frame(hr, new Color(0.75f, 0.12f, 0.2f), 2f);
            hudStyle.fontSize = LdrUI.Px(10 * sc);
            LdrUI.Text(hr, "ÖLÜM  " + Deaths, hudStyle, new Color(1f, 0.28f, 0.34f), 1f);
        }

        // Ölüm ekranı: kırmızı karartma, titreyen "ÖLDÜN" yazısı ve kaçıncı ölüm olduğu.
        if (dying && rep)
        {
            float t = dyingTimer;
            float a = Mathf.Clamp01(t / 1.0f) * 0.9f;
            LdrUI.Fill(new Rect(0, 0, W, H), new Color(0.12f, 0f, 0.02f, a));
            LdrUI.Light(new Vector2(W * 0.5f, H * 0.45f), H * 0.8f, new Color(0.8f, 0.05f, 0.12f, 0.25f * Mathf.Clamp01(t)));
            LdrUI.Scanlines(Time.unscaledTime, sc);
            float fade = Mathf.Clamp01(t / 1.2f);
            float flick = Mathf.PerlinNoise(Time.unscaledTime * 9f, 1.7f) > 0.85f ? 0.5f : 1f;   // titreyen yazı
            bigStyle.fontSize = LdrUI.Px(56 * sc);
            LdrUI.Text(new Rect(0, H * 0.34f, W, 90 * sc), "ÖLDÜN", bigStyle, new Color(0.95f * flick, 0.12f * flick, 0.2f * flick, fade), 2f);
            smallStyle.fontSize = LdrUI.Px(12 * sc);
            LdrUI.Text(new Rect(0, H * 0.34f + 96 * sc, W, 28 * sc),
                "ÖLÜM #" + Deaths + "   YAŞA. ÖL. TEKRARLA.", smallStyle, new Color(0.85f, 0.75f, 0.95f, fade), 1f);
        }
    }
}
