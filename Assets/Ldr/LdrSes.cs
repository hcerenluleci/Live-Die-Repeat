using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// DERS GEREKSİNİMİ: SES (menüde en az 1, oyunda en az 2)
// Oyun içi efektler: ateş, şarjör, eşya alma, kapı, masa sürükleme, düşman vuruş/ölüm, oyuncu hasar/ölüm.
// Menü: sfx_ui_click. Seviye duraklatma menüsündeki SES EFEKTLERİ ayarından gelir.
// =====================================================================================
/// <summary>
/// Ses çalar. Sesler Assets/Resources içindeki takımın kendi dosyalarıdır:
/// sfx_shoot, sfx_reload, sfx_pickup, sfx_door, sfx_drag, sfx_enemy_hit, sfx_enemy_death, sfx_player_death, sfx_player_hurt, sfx_ui_click.
/// Tek bir ortak AudioSource üzerinden çalınır; dosya yoksa sessizce geçer.
/// </summary>
public static class LdrSes
{
    static readonly Dictionary<string, AudioClip> onbellek = new Dictionary<string, AudioClip>();
    static AudioSource kaynak;

    // ---- ses seviyeleri (duraklatma menüsünden ayarlanır, PlayerPrefs'te saklanır)
    public const int SesBasamagi = 20;                    // 0..20, varsayılan 10 = tam ses
    const string MuzikAnahtari = "LDR_MuzikSesi", EfektAnahtari = "LDR_EfektSesi";
    static int muzikSeviyesi = -1, efektSeviyesi = -1;

    public static int MuzikSeviyesi
    {
        get { if (muzikSeviyesi < 0) muzikSeviyesi = PlayerPrefs.GetInt(MuzikAnahtari, 10); return muzikSeviyesi; }
        set { muzikSeviyesi = Mathf.Clamp(value, 0, SesBasamagi); PlayerPrefs.SetInt(MuzikAnahtari, muzikSeviyesi); }
    }

    public static int EfektSeviyesi
    {
        get { if (efektSeviyesi < 0) efektSeviyesi = PlayerPrefs.GetInt(EfektAnahtari, 10); return efektSeviyesi; }
        set { efektSeviyesi = Mathf.Clamp(value, 0, SesBasamagi); PlayerPrefs.SetInt(EfektAnahtari, efektSeviyesi); }
    }

    public static float MuzikCarpani { get { return MuzikSeviyesi / 10f; } }
    public static float EfektCarpani { get { return EfektSeviyesi / 10f; } }

    public static AudioClip Yukle(string ad)
    {
        AudioClip c;
        if (onbellek.TryGetValue(ad, out c) && c != null) return c;
        c = Resources.Load<AudioClip>(ad);
        onbellek[ad] = c;
        return c;
    }

    // Resources klasöründen sesi yükler (bir kez, sonra önbellekten) ve verilen ses seviyesi/perdeyle çalar.
    public static void Cal(string ad, float ses = 1f, float perde = 1f)
    {
        var c = Yukle(ad);
        if (c == null) return;
        if (kaynak == null)
        {
            var go = new GameObject("LdrSesKaynagi");
            Object.DontDestroyOnLoad(go);
            kaynak = go.AddComponent<AudioSource>();
            kaynak.playOnAwake = false;
            kaynak.spatialBlend = 0f;
            kaynak.ignoreListenerPause = false;
        }
        kaynak.pitch = perde;
        kaynak.PlayOneShot(c, Mathf.Min(1f, ses * EfektCarpani));
    }

    /// <summary>Aynı sesin çok sık üst üste binmemesi için küçük bekleme.</summary>
    static readonly Dictionary<string, float> son = new Dictionary<string, float>();
    // Aynı ses çok sık tekrar etmesin diye en az "aralik" saniye arayla çalar (örn. masa sürükleme).
    public static void CalSeyrek(string ad, float aralik, float ses = 1f, float perde = 1f)
    {
        float t;
        if (son.TryGetValue(ad, out t) && Time.unscaledTime - t < aralik) return;
        son[ad] = Time.unscaledTime;
        Cal(ad, ses, perde);
    }
}
