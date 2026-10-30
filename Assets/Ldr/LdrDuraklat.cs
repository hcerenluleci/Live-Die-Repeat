using UnityEngine;

/// <summary>
/// Duraklatma menüsü (ESC). Yerleşim: başlık, ölüm / ruh satırı, önce DEVAM ET ve ANA MENÜ (en sık kullanılanlar),
/// sonra MÜZİK ve SES EFEKTLERİ ses satırları, en altta ayrık duran OYUNDAN ÇIK (yanlışlıkla basılmasın diye).
/// Açıkken zaman durur (timeScale 0). Yalnızca oyun sırasında açılır; menü, ölüm ekranı veya geçiş sırasında açılmaz.
/// </summary>
public class LdrDuraklat : MonoBehaviour
{
    public static bool Acik { get; private set; }

    LdrOyun oyun;
    bool tetikBirakBekle;                                // DEVAM ET tıklaması ateş sayılmasın
    GUIStyle stBaslik, stYazi, stDugme;

    void Awake()
    {
        oyun = GetComponent<LdrOyun>();
        Acik = false;
    }

    void OnDestroy()
    {
        Acik = false;
    }

    bool Acilabilir()
    {
        var y = oyun != null ? oyun.yasayan : null;
        return y != null && !y.Donuk && !y.Can.Olu && !LdrMenu.Acik && !LiveDieRepeat.Dying;
    }

    void Update()
    {
        if (tetikBirakBekle && !LdrGiris.AtesBasili())
        {
            tetikBirakBekle = false;
            if (oyun != null && oyun.yasayan != null && !oyun.yasayan.Can.Olu) oyun.yasayan.Donuk = false;
        }
        if (!LdrGiris.DuraklatBasildi()) return;
        if (Acik) Devam();
        else if (Acilabilir()) Ac();
    }

    void Ac()
    {
        Acik = true;
        Time.timeScale = 0f;
        oyun.yasayan.Donuk = true;                       // duraklatma tıklaması ateş etmesin
    }

    void Devam()
    {
        Acik = false;
        Time.timeScale = 1f;
        tetikBirakBekle = true;                          // fare/tetik bırakılınca oyuncu serbest kalır
    }

    // ---------------------------------------------------------------- çizim
    void Stiller()
    {
        if (stBaslik != null) return;
        stBaslik = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
        stYazi = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
        stDugme = LdrUI.MakeStyle(TextAnchor.MiddleCenter);
    }

    static int Sigdir(float istenen, string yazi, float bosluk)
    {
        int boyut = LdrUI.Px(istenen);
        while (boyut > 16 && boyut * LdrUI.CharW * yazi.Length > bosluk) boyut = LdrUI.StepDown(boyut);
        return boyut;
    }

    static bool Tiklandi(Rect r)
    {
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { e.Use(); return true; }
        return false;
    }

    bool Dugme(Rect r, string yazi, float sc, Color vurgu)
    {
        bool uzerinde = r.Contains(Event.current.mousePosition);
        if (Event.current.type == EventType.Repaint)
        {
            Color koyu = new Color(vurgu.r * 0.12f, vurgu.g * 0.12f, vurgu.b * 0.14f, 0.92f);
            Color orta = new Color(vurgu.r * 0.34f, vurgu.g * 0.34f, vurgu.b * 0.38f, 0.97f);
            LdrUI.Fill(r, uzerinde ? orta : koyu);
            LdrUI.Frame(r, uzerinde ? vurgu : new Color(vurgu.r * 0.65f, vurgu.g * 0.65f, vurgu.b * 0.65f), 2f);
            LdrUI.Fill(new Rect(r.x, r.y, 6f * sc, r.height), uzerinde ? LdrUI.Blood : new Color(0.45f, 0.1f, 0.2f));
            stDugme.fontSize = Sigdir(18 * sc, yazi, r.width - 40 * sc);
            LdrUI.Text(r, yazi, stDugme, uzerinde ? Color.white : new Color(0.88f, 0.82f, 0.96f), 1f);
        }
        return Tiklandi(r);
    }

    /// <summary>[-] ▮▮▮▮▮▯▯▯▯▯ [+] biçiminde ses satırı; yeni değeri döndürür.</summary>
    int SesSatiri(float x, float y, float w, float sc, string baslik, int deger)
    {
        bool boya = Event.current.type == EventType.Repaint;
        if (boya)
        {
            stYazi.fontSize = LdrUI.Px(10 * sc);
            stYazi.alignment = TextAnchor.MiddleLeft;
            LdrUI.Text(new Rect(x, y, w, 18 * sc), baslik, stYazi, new Color(0.8f, 0.72f, 0.96f), 1f);
            stYazi.alignment = TextAnchor.MiddleCenter;
        }
        float by = y + 24 * sc, bs = 30 * sc;
        var eksi = new Rect(x, by, bs, bs);
        var arti = new Rect(x + w - bs, by, bs, bs);
        float cubukX = x + bs + 10 * sc, cubukW = w - 2 * bs - 20 * sc;
        if (boya)
        {
            LdrUI.Fill(eksi, new Color(0.14f, 0.05f, 0.24f, 1f)); LdrUI.Frame(eksi, LdrUI.Purple, 2f);
            LdrUI.Fill(arti, new Color(0.14f, 0.05f, 0.24f, 1f)); LdrUI.Frame(arti, LdrUI.Purple, 2f);
            stDugme.fontSize = LdrUI.Px(16 * sc);
            LdrUI.Text(eksi, "-", stDugme, LdrUI.Bone, 0f);
            LdrUI.Text(arti, "+", stDugme, LdrUI.Bone, 0f);
            const int kutu = 10;
            float kw = cubukW / kutu;
            for (int i = 0; i < kutu; i++)
            {
                bool dolu = deger * kutu > i * LdrSes.SesBasamagi;
                LdrUI.Fill(new Rect(cubukX + i * kw + 2, by + 4 * sc, kw - 4, bs - 8 * sc),
                              dolu ? new Color(0.7f, 0.4f, 1f, 1f) : new Color(0.14f, 0.08f, 0.22f, 1f));
            }
        }
        if (Tiklandi(eksi)) deger--;
        if (Tiklandi(arti)) deger++;
        return Mathf.Clamp(deger, 0, LdrSes.SesBasamagi);
    }

    void OnGUI()
    {
        if (!Acik) return;
        Stiller();
        GUI.depth = -300;
        float sc = Screen.height / 720f, W = Screen.width, H = Screen.height, t = Time.unscaledTime;
        bool boya = Event.current.type == EventType.Repaint;

        if (boya)
        {
            LdrUI.Fill(new Rect(0, 0, W, H), new Color(0.008f, 0f, 0.02f, 0.8f));
            LdrUI.Light(new Vector2(W * 0.5f, H * 0.45f), H * 0.9f, new Color(0.4f, 0.08f, 0.55f, 0.2f));
            LdrUI.Light(new Vector2(W * 0.5f, H * 1.0f), H * 0.7f, new Color(0.75f, 0.05f, 0.16f, 0.16f));
            LdrUI.Scanlines(t, sc);
        }

        float pw = 480f * sc, ph = 500f * sc;
        var panel = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f, pw, ph);
        if (boya) LdrUI.Panel(panel, LdrUI.Purple, sc, 0.95f, 1f);

        float ix = panel.x + 36 * sc, iw = panel.width - 72 * sc;
        if (boya)
        {
            float titre = Mathf.PerlinNoise(t * 7f, 3.3f) > 0.86f ? 0.55f : 1f;
            stBaslik.fontSize = Sigdir(30 * sc, "DURAKLATILDI", iw);
            LdrUI.Text(new Rect(panel.x, panel.y + 26 * sc, panel.width, 44 * sc), "DURAKLATILDI", stBaslik,
                          new Color(0.92f * titre, 0.18f * titre, 0.3f * titre), 2f);
            stYazi.fontSize = LdrUI.Px(8 * sc);
            LdrUI.Text(new Rect(panel.x, panel.y + 74 * sc, panel.width, 18 * sc),
                          "ÖLÜM " + LiveDieRepeat.Deaths + "     RUH " + LdrIlerleme.ToplamRuh, stYazi, new Color(0.7f, 0.62f, 0.9f), 1f);
            LdrUI.Fill(new Rect(ix, panel.y + 104 * sc, iw, 2f), new Color(0.62f, 0.28f, 0.95f, 0.45f));
        }

        float y = panel.y + 120 * sc;
        float dh = 48f * sc;
        if (Dugme(new Rect(ix, y, iw, dh), "DEVAM ET", sc, LdrUI.Purple)) Devam();
        if (Dugme(new Rect(ix, y + dh + 14 * sc, iw, dh), "ANA MENÜ", sc, LdrUI.Purple))
        {
            Acik = false;
            PlayerPrefs.Save();
            LdrMenu.AnaMenuyeDon();
            return;
        }

        float sy = y + 2 * (dh + 14 * sc) + 6 * sc;
        if (boya) LdrUI.Fill(new Rect(ix, sy - 4 * sc, iw, 2f), new Color(0.62f, 0.28f, 0.95f, 0.3f));
        LdrSes.MuzikSeviyesi = SesSatiri(ix, sy, iw, sc, "MÜZİK", LdrSes.MuzikSeviyesi);
        int onceki = LdrSes.EfektSeviyesi;
        LdrSes.EfektSeviyesi = SesSatiri(ix, sy + 70 * sc, iw, sc, "SES EFEKTLERİ", onceki);
        if (LdrSes.EfektSeviyesi != onceki) LdrSes.Cal("sfx_ui_click", 0.6f);

        if (Dugme(new Rect(ix, sy + 150 * sc, iw, dh), "OYUNDAN ÇIK", sc, LdrUI.Blood))
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        if (boya)
        {
            stYazi.fontSize = LdrUI.Px(8 * sc);
            LdrUI.Text(new Rect(panel.x, panel.yMax - 34 * sc, panel.width, 18 * sc), "ESC: DEVAM ET", stYazi, new Color(0.55f, 0.5f, 0.72f), 1f);
        }
    }
}
