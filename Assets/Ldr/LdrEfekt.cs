using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Görüntü ve efekt katmanı:
///  - Korku görüntüsü: vinyet, film greni, kromatik sapma, soluk renk, bloom (URP Volume)
///  - Hasar: kırmızı flaş, sarsıntı, vinyet; düşük canda kalp atışı gibi atan kırmızı vinyet
///  - Silah: namlu patlaması, kıvılcım, kovan
///  - Takla: mor hayalet izi ve toz
///  - Jump scare: ara sıra ve ölümden sonra ekrana çarpan yüz + çığlık (kendi çizdiğimiz yüz, kodla üretilir)
/// "LdrOyun" nesnesine eklenir.
/// </summary>
public class LdrEfekt : MonoBehaviour
{
    [Header("Jump scare")]
    [SerializeField, Tooltip("İki rastgele scare arası en az / en çok süre (oyun saniyesi)")] Vector2 scareAraligi = new Vector2(75f, 160f);
    [SerializeField, Range(0f, 1f), Tooltip("Ölümden sonra scare çıkma ihtimali")] float olumScareIhtimali = 0.7f;

    // ---------------------------------------------------------------- ortak kaynaklar
    static Sprite sprToz, sprYildiz, sprKivilcim, sprKovan, sprYuz;
    static Texture2D beyaz;
    static Material isiksiz;
    static AudioClip cigligi;
    static Volume volume;                                   // sahneler arasında yaşar
    static readonly Stack<Parcacik> havuz = new Stack<Parcacik>();

    LdrOyun oyun;
    LdrYasayan yasayan;
    SpriteRenderer govde;
    Vignette vinyet; ChromaticAberration kromatik; FilmGrain gren; ColorAdjustments renkAyar; Bloom parlama; LensDistortion mercek;
    // HALÜSİNASYON: oyuncu hasar alınca 4 sn sürer.
    // Renk kayması (hue), mercek bozulması, parlama titreşimi, vinyet ve mor-kırmızı renk filtresi dalgası.
    float halu = -1f;
    const float HaluSure = 4f, HaluDongu = 6f;       // dalganın bir tam turu (sn)
    static Gradient haluRenk;
    public static bool HaluAktif { get; private set; }
    AudioSource ses;

    float hasar, kromatikVurus, canOrani = 1f, flasA;
    Color flasRenk = Color.red;
    float scareT = -1f, scareBekleme, rastgeleScareT, olumScareT;
    bool oluyordu, olumScareBekliyor;
    float izSayac, tozSayac;

    // ---------------------------------------------------------------- kurulum
    void Awake()
    {
        oyun = GetComponent<LdrOyun>();
        ses = gameObject.AddComponent<AudioSource>();
        ses.spatialBlend = 0f;
        ses.playOnAwake = false;
        rastgeleScareT = Random.Range(scareAraligi.x, scareAraligi.y);
        KaynaklariHazirla();
        VolumeKur();
        LdrSilah.OyuncuAtesEtti += NamluEfekti;
    }

    void OnDestroy()
    {
        LdrSilah.OyuncuAtesEtti -= NamluEfekti;
        if (yasayan != null) yasayan.Can.Hasarlandi -= Vuruldu;
    }

    static Texture2D YeniDoku(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    static Sprite SpriteYap(Texture2D t, float ppu)
    {
        t.Apply();
        var s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), ppu);
        s.hideFlags = HideFlags.HideAndDontSave;
        return s;
    }

    static void KaynaklariHazirla()
    {
        if (sprToz != null && sprYildiz != null && sprKivilcim != null && sprKovan != null && sprYuz != null && beyaz != null && isiksiz != null) return;

        beyaz = YeniDoku(1, 1);
        beyaz.SetPixel(0, 0, Color.white);
        beyaz.Apply();

        // yumuşak toz bulutu (12x12)
        var tp = YeniDoku(12, 12);
        for (int y = 0; y < 12; y++)
            for (int x = 0; x < 12; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(6, 6)) / 6f;
                float n = Mathf.PerlinNoise(x * 0.5f, y * 0.5f);
                float a = Mathf.Clamp01(1f - d - (n - 0.5f) * 0.5f);
                tp.SetPixel(x, y, new Color(1, 1, 1, a > 0.25f ? Mathf.Clamp01(a * 1.3f) : 0f));
            }
        sprToz = SpriteYap(tp, 12f);

        // namlu patlaması: 8 kollu sivri yıldız
        const int S = 17;
        var ts = YeniDoku(S, S);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x - 8f, dy = y - 8f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float uc = Mathf.Abs(Mathf.Cos(ang * 4f));
                float erisim = Mathf.Lerp(2.2f, 8.2f, Mathf.Pow(uc, 5f));
                ts.SetPixel(x, y, r < 2.4f ? Color.white : (r < erisim ? new Color(0.85f, 0.55f, 1f, 1f) : new Color(0, 0, 0, 0)));
            }
        sprYildiz = SpriteYap(ts, 16f);

        // kıvılcım: küçük elmas
        var tk = YeniDoku(5, 5);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                tk.SetPixel(x, y, (Mathf.Abs(x - 2) + Mathf.Abs(y - 2)) <= 2 ? Color.white : new Color(0, 0, 0, 0));
        sprKivilcim = SpriteYap(tk, 16f);

        // kovan
        var tc = YeniDoku(4, 2);
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 4; x++)
                tc.SetPixel(x, y, x == 3 ? new Color(0.65f, 0.4f, 0.15f) : new Color(0.95f, 0.72f, 0.28f));
        sprKovan = SpriteYap(tc, 16f);

        sprYuz = YuzCiz();

        var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        isiksiz = new Material(sh);
        isiksiz.hideFlags = HideFlags.HideAndDontSave;
    }

    /// <summary>Jump scare yüzü: kafatası, kıpkırmızı gözler, dişli ağız (64x64, kodla çizilir).</summary>
    static Sprite YuzCiz()
    {
        const int W = 64, H = 64;
        var t = YeniDoku(W, H);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                Color c = new Color(0.02f, 0f, 0.03f, 1f);
                float hx = (px - 32f) / 22f, hy = (py - 34f) / 27f;
                float kafa = hx * hx + hy * hy;
                if (py < 24f)
                {
                    float gx = (px - 32f) / (14f + (py - 6f) * 0.5f);
                    kafa = gx * gx + hy * hy * 0.3f;
                }
                bool kafaIci = kafa < 1f;
                if (kafaIci)
                {
                    float n = Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                    float golge = 0.62f + 0.28f * n - 0.22f * Mathf.Clamp01(Mathf.Abs(hx));
                    c = new Color(0.86f * golge, 0.82f * golge, 0.74f * golge, 1f);
                    float cat = Mathf.PerlinNoise(x * 0.18f + 9f, y * 0.18f);                 // çatlak
                    if (Mathf.Abs(cat - 0.5f) < 0.018f) c = new Color(0.12f, 0.05f, 0.05f, 1f);
                }
                for (int e = 0; e < 2; e++)                                                      // gözler
                {
                    float ex = e == 0 ? 22.5f : 41.5f, ey = 37f;
                    float dx = (px - ex) / 8f, dy = (py - ey) / 9.5f;
                    if (dx * dx + dy * dy < 1f)
                    {
                        c = new Color(0.01f, 0f, 0.01f, 1f);
                        float pd = (px - ex) * (px - ex) + (py - (ey - 1f)) * (py - (ey - 1f));
                        if (pd < 7f) c = new Color(1f, 0.05f, 0.05f, 1f);
                        if (pd < 2f) c = new Color(1f, 0.9f, 0.8f, 1f);
                    }
                }
                if (py > 24f && py < 31f && Mathf.Abs(px - 32f) < (31f - py) * 0.7f + 0.5f && Mathf.Abs(px - 32f) < 4f)
                    c = new Color(0.05f, 0.01f, 0.02f, 1f);                                      // burun deliği
                float mx = (px - 32f) / 15f, my = (py - 14f) / 7f;                               // ağız
                if (mx * mx + my * my < 1f)
                {
                    c = new Color(0.02f, 0f, 0.02f, 1f);
                    bool dis = ((int)px) % 4 < 2 && (py > 15.5f || py < 12.5f);
                    if (dis && Mathf.Abs(mx) < 0.92f) c = new Color(0.9f, 0.88f, 0.8f, 1f);
                }
                if (kafaIci && px > 20f && px < 24f && py < 30f && py > 10f + 6f * Mathf.PerlinNoise(x * 0.2f, 3f))
                    c = Color.Lerp(c, new Color(0.6f, 0.02f, 0.04f, 1f), 0.9f);                  // sızan kan
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, (kafaIci || c.r > 0.15f) ? 1f : 0f));
            }
        return SpriteYap(t, 16f);
    }

    /// <summary>Jump scare çığlığı: testere dişi + gürültü + alçak gümbürtü, sert bozulmuş.</summary>
    static AudioClip CigligiUret()
    {
        const int sr = 22050;
        int n = (int)(sr * 1.25f);
        var veri = new float[n];
        float faz = 0f, faz2 = 0f, alcak = 0f;
        var rnd = new System.Random(77);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float zarf = t < 0.02f ? t / 0.02f : Mathf.Exp(-(t - 0.02f) * 1.8f);
            float f = Mathf.Lerp(820f, 2500f, Mathf.Clamp01(t / 0.5f)) * (1f + 0.04f * Mathf.Sin(t * 70f));
            faz += f / sr; faz2 += (f * 1.51f) / sr;
            float s = (faz % 1f) * 2f - 1f;
            float s2 = Mathf.Sin(faz2 * Mathf.PI * 2f);
            float gurultu = (float)(rnd.NextDouble() * 2.0 - 1.0);
            alcak += (gurultu - alcak) * 0.35f;
            float gumbur = t < 0.35f ? Mathf.Sin(2f * Mathf.PI * (70f - t * 90f) * t) * Mathf.Exp(-t * 7f) * 1.4f : 0f;
            float v = (s * 0.45f + s2 * 0.3f + alcak * 0.5f) * zarf + gumbur;
            veri[i] = (float)System.Math.Tanh(v * 2.2f) * 0.9f;
        }
        var klip = AudioClip.Create("ldr_scare", n, 1, sr, false);
        klip.SetData(veri, 0);
        klip.hideFlags = HideFlags.HideAndDontSave;
        return klip;
    }

    // ---------------------------------------------------------------- görüntü (URP Volume)
    void VolumeKur()
    {
        var kam = Camera.main;
        if (kam != null)
        {
            var ud = kam.GetComponent<UniversalAdditionalCameraData>();
            if (ud != null && !ud.renderPostProcessing) ud.renderPostProcessing = true;
        }
        if (volume == null)
        {
            var go = new GameObject("LdrGoruntuHacmi");
            DontDestroyOnLoad(go);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.weight = 1f;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            prof.hideFlags = HideFlags.HideAndDontSave;
            var v = prof.Add<Vignette>(true);
            v.intensity.Override(0.30f); v.smoothness.Override(0.55f); v.rounded.Override(true); v.color.Override(LdrTema.Vinyet);
            var k = prof.Add<ChromaticAberration>(true);
            k.intensity.Override(0.12f);
            var g = prof.Add<FilmGrain>(true);
            g.type.Override(FilmGrainLookup.Thin2); g.intensity.Override(0.25f); g.response.Override(0.8f);
            var r = prof.Add<ColorAdjustments>(true);
            r.saturation.Override(4f); r.contrast.Override(14f); r.colorFilter.Override(LdrTema.Filtre); r.postExposure.Override(0.25f);
            var ml = prof.Add<LensDistortion>(true);
            ml.intensity.Override(0f); ml.scale.Override(1f);
            var b = prof.Add<Bloom>(true);
            b.threshold.Override(0.85f); b.intensity.Override(0.7f); b.scatter.Override(0.55f); b.tint.Override(new Color(0.85f, 0.7f, 1f));
            volume.sharedProfile = prof;
        }
        var p = volume.sharedProfile;
        p.TryGet(out vinyet); p.TryGet(out kromatik); p.TryGet(out gren); p.TryGet(out renkAyar); p.TryGet(out parlama); p.TryGet(out mercek);
    }

    void GoruntuyuGuncelle()
    {
        if (vinyet == null || kromatik == null || gren == null || renkAyar == null) return;
        float dusuk = canOrani < 0.4f ? (0.4f - canOrani) / 0.4f : 0f;                         // düşük can
        float atis = dusuk > 0f ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * (4f + 4f * dusuk))), 6f) : 0f;
        vinyet.intensity.value = 0.30f + 0.25f * hasar + 0.2f * dusuk + 0.2f * atis + (scareT >= 0f ? 0.25f : 0f);
        vinyet.color.value = Color.Lerp(LdrTema.Vinyet, new Color(0.55f, 0f, 0.04f), Mathf.Clamp01(hasar + dusuk * 0.8f + atis * 0.4f));
        kromatik.intensity.value = Mathf.Clamp01(0.12f + kromatikVurus * 0.7f + 0.06f * Mathf.Sin(Time.unscaledTime * 0.7f));
        gren.intensity.value = 0.25f + 0.25f * hasar;
        renkAyar.saturation.value = 4f - 30f * dusuk;
        HaluUygula();
    }

    static Gradient HaluGradyani()
    {
        if (haluRenk != null) return haluRenk;
        // oyunun paleti: gece moru -> morötesi -> kan kırmızısı -> soluk et rengi -> hastalıklı yeşil -> gece moru
        var g = new Gradient();
        g.SetKeys(new[]
        {
            new GradientColorKey(new Color(0.22f, 0.12f, 0.34f), 0f),
            new GradientColorKey(new Color(0.55f, 0.2f, 0.95f), 0.2f),
            new GradientColorKey(new Color(0.9f, 0.12f, 0.22f), 0.42f),
            new GradientColorKey(new Color(1f, 0.82f, 0.74f), 0.58f),
            new GradientColorKey(new Color(0.42f, 0.85f, 0.38f), 0.78f),
            new GradientColorKey(new Color(0.22f, 0.12f, 0.34f), 1f),
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        haluRenk = g;
        return g;
    }

    void HaluUygula()
    {
        HaluAktif = halu >= 0f;
        if (halu < 0f)
        {
            if (mercek != null) { mercek.intensity.value = 0f; mercek.scale.value = 1f; }
            if (parlama != null) parlama.intensity.value = 0.7f;
            renkAyar.hueShift.value = 0f;
            renkAyar.colorFilter.value = LdrTema.Filtre;
            return;
        }
        float t = Time.time;
        float yuzde = Mathf.Clamp01(Mathf.Sin(t / HaluDongu * Mathf.PI * 2f) * 0.5f + 0.5f);
        Color c = HaluGradyani().Evaluate(yuzde);
        // renk görüntü filtresine uygulanır (ekran tamamen kararmasın diye alt sınır)
        Color f = new Color(Mathf.Max(c.r, 0.4f), Mathf.Max(c.g, 0.4f), Mathf.Max(c.b, 0.4f), 1f);
        renkAyar.colorFilter.value = Color.Lerp(LdrTema.Filtre, f, 0.8f);
        vinyet.intensity.value = Mathf.Max(vinyet.intensity.value, 0.4f + yuzde * 0.65f);
        if (mercek != null)
        {
            mercek.intensity.value = Mathf.PingPong(t * 0.5f, 1f) - 0.5f;
            mercek.scale.value = 1f - Mathf.PingPong(t * 0.1f, 0.2f);
        }
        if (parlama != null) parlama.intensity.value = 2f + Mathf.PingPong(t, 2f);
        renkAyar.hueShift.value = Mathf.PingPong(t * 40f, 180f) - 90f;
    }

    // ---------------------------------------------------------------- parçacıklar
    class Parcacik : MonoBehaviour
    {
        public SpriteRenderer sr;
        public float omur, t, surtunme, donme, s0, s1, yercekimi;
        public Vector3 hiz;
        public Color c0, c1;

        void Update()
        {
            t += Time.deltaTime;
            if (t >= omur) { gameObject.SetActive(false); havuz.Push(this); return; }
            float k = t / omur;
            hiz.y -= yercekimi * Time.deltaTime;
            hiz *= Mathf.Max(0f, 1f - surtunme * Time.deltaTime);
            transform.position += hiz * Time.deltaTime;
            transform.Rotate(0, 0, donme * Time.deltaTime);
            float s = Mathf.Lerp(s0, s1, k);
            transform.localScale = new Vector3(s, s, 1f);
            sr.color = Color.Lerp(c0, c1, k);
        }
    }

    static Parcacik Dogur(Sprite sp, Vector3 konum, float aci, float s0, float s1, Color c0, Color c1, float omur,
                          Vector3 hiz, float surtunme, float donme, float yercekimi, int katmanId, int sira)
    {
        KaynaklariHazirla();
        Parcacik p = null;
        while (havuz.Count > 0 && p == null) p = havuz.Pop();      // sahne yenilenince havuzdakiler yok edilmiş olabilir
        if (p == null)
        {
            var go = new GameObject("Efekt");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = isiksiz;
            p = go.AddComponent<Parcacik>();
            p.sr = sr;
        }
        p.gameObject.SetActive(true);
        p.sr.sprite = sp;
        p.sr.flipX = false;
        p.sr.sortingLayerID = katmanId;
        p.sr.sortingOrder = sira;
        p.transform.position = konum;
        p.transform.rotation = Quaternion.Euler(0, 0, aci);
        p.transform.localScale = new Vector3(s0, s0, 1f);
        p.t = 0f; p.omur = omur; p.s0 = s0; p.s1 = s1; p.c0 = c0; p.c1 = c1;
        p.hiz = hiz; p.surtunme = surtunme; p.donme = donme; p.yercekimi = yercekimi;
        return p;
    }

    // ---------------------------------------------------------------- olaylar
    void Bagla()
    {
        if (oyun == null || oyun.yasayan == null || oyun.yasayan == yasayan) return;
        yasayan = oyun.yasayan;
        yasayan.Can.Hasarlandi += Vuruldu;
        foreach (var sr in yasayan.GetComponentsInChildren<SpriteRenderer>())
            if (sr.gameObject.name == "Govde") govde = sr;
    }

    void Vuruldu(LdrCan can, Vector2 kaynak)
    {
        hasar = 1f; kromatikVurus = 1f;
        if (halu < 0f) halu = 0f;       // vurulunca halüsinasyon başlar; sürerken yenisi başlamaz
        flasA = Mathf.Max(flasA, 0.45f); flasRenk = new Color(0.8f, 0.02f, 0.05f);
        LdrKamera.Salla(0.22f);
    }

    void NamluEfekti(Vector2 namlu, Vector2 yon)
    {
        if (govde == null) return;
        int katman = govde.sortingLayerID, sira = govde.sortingOrder + 3;
        float aci = Mathf.Atan2(yon.y, yon.x) * Mathf.Rad2Deg;
        Vector3 uc = new Vector3(namlu.x, namlu.y, 0f);
        Dogur(sprYildiz, uc, aci, 0.9f, 1.5f, new Color(1f, 0.95f, 1f, 1f), new Color(0.7f, 0.25f, 1f, 0f), 0.08f, Vector3.zero, 0f, 0f, 0f, katman, sira);
        Dogur(sprToz, uc + (Vector3)(yon * 0.2f), aci, 0.35f, 1.1f, new Color(0.8f, 0.6f, 1f, 0.5f), new Color(0.3f, 0.1f, 0.5f, 0f), 0.22f, (Vector3)(yon * 1.2f), 3f, 0f, 0f, katman, sira - 1);
        for (int i = 0; i < 4; i++)
        {
            float a = (aci + Random.Range(-28f, 28f)) * Mathf.Deg2Rad;
            Vector3 v = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * Random.Range(3.5f, 7f);
            Dogur(sprKivilcim, uc, 0, 0.7f, 0.1f, new Color(1f, 0.85f, 1f, 1f), new Color(0.6f, 0.2f, 1f, 0f), Random.Range(0.12f, 0.24f), v, 4f, 0f, 0f, katman, sira);
        }
        Vector3 yan = new Vector3(-yon.y, yon.x, 0f);
        Vector3 kv = yan * Random.Range(1.5f, 2.6f) + (Vector3)(yon * -0.4f) + new Vector3(0, 1.2f, 0);
        Dogur(sprKovan, yasayan.transform.position + (Vector3)(yon * 0.35f), Random.Range(0f, 360f), 1f, 1f, Color.white, new Color(1, 1, 1, 0f), 0.6f, kv, 1.5f, Random.Range(-720f, 720f), 6f, katman, sira - 2);
        kromatikVurus = Mathf.Max(kromatikVurus, 0.15f);
    }

    void TaklaEfekti()
    {
        if (govde == null || yasayan == null) return;
        int katman = govde.sortingLayerID, sira = govde.sortingOrder;
        izSayac -= Time.deltaTime;
        if (izSayac <= 0f)
        {
            izSayac = 0.045f;
            var iz = Dogur(govde.sprite, yasayan.transform.position, 0f, 1.2f, 1.2f, new Color(0.7f, 0.4f, 1f, 0.6f), new Color(0.3f, 0.1f, 0.5f, 0f), 0.3f, Vector3.zero, 0f, 0f, 0f, katman, sira - 1);
            iz.sr.flipX = govde.flipX;
            iz.transform.rotation = govde.transform.rotation;
        }
        tozSayac -= Time.deltaTime;
        if (tozSayac <= 0f)
        {
            tozSayac = 0.07f;
            Dogur(sprToz, yasayan.transform.position + new Vector3(0f, -0.55f, 0f), Random.Range(0f, 360f), 0.3f, 0.9f,
                  new Color(0.7f, 0.6f, 0.85f, 0.5f), new Color(0.4f, 0.3f, 0.6f, 0f), 0.35f, new Vector3(Random.Range(-0.5f, 0.5f), 0.4f, 0f), 2f, 0f, 0f, katman, sira - 2);
        }
    }

    // ---------------------------------------------------------------- jump scare
    void ScareBaslat()
    {
        if (cigligi == null) cigligi = CigligiUret();
        scareT = 0f;
        scareBekleme = 20f;
        flasA = 1f; flasRenk = new Color(0.95f, 0.1f, 0.1f);
        LdrKamera.Salla(0.6f);
        kromatikVurus = 1.6f; hasar = 1f;
        ses.pitch = 1f;
        ses.PlayOneShot(cigligi, 0.9f * Mathf.Min(1f, LdrSes.EfektCarpani));
        // canı yeterliyse biraz can kaybı (1 birim)
        if (yasayan != null && !yasayan.Can.Olu && yasayan.Can.Oran > 0.45f) yasayan.Can.Hasar(LdrCan.BirimOrani * yasayan.Can.azami, yasayan.transform.position);
    }

    void Update()
    {
        Bagla();
        float udt = Time.unscaledDeltaTime;
        scareBekleme -= udt;

        bool oluyor = LiveDieRepeat.Dying;
        if (oluyor && !oluyordu) { olumScareT = 1.3f; olumScareBekliyor = Random.value < olumScareIhtimali; }
        oluyordu = oluyor;
        if (olumScareBekliyor)
        {
            olumScareT -= udt;
            if (olumScareT <= 0f) { olumScareBekliyor = false; scareBekleme = 0f; ScareBaslat(); }
        }
        if (!oluyor && yasayan != null && !yasayan.Can.Olu && !LdrMenu.Acik && Time.timeScale > 0.1f)
        {
            rastgeleScareT -= Time.deltaTime;
            if (rastgeleScareT <= 0f)
            {
                rastgeleScareT = Random.Range(scareAraligi.x, scareAraligi.y);
                if (scareBekleme <= 0f && scareT < 0f) ScareBaslat();
            }
        }

        if (yasayan != null)
        {
            canOrani = yasayan.Can.Oran;
            if (yasayan.Takla && Time.timeScale > 0.01f) TaklaEfekti();
        }
        hasar = Mathf.MoveTowards(hasar, 0f, udt * 2.2f);
        kromatikVurus = Mathf.MoveTowards(kromatikVurus, 0f, udt * 2.5f);
        flasA = Mathf.MoveTowards(flasA, 0f, udt * 1.6f);
        if (scareT >= 0f) { scareT += udt; if (scareT > 1.5f) scareT = -1f; }

        if (halu >= 0f) { halu += udt; if (halu >= HaluSure) halu = -1f; }
        GoruntuyuGuncelle();
    }

    // ---------------------------------------------------------------- ekran katmanı (flaş + yüz)
    void OnGUI()
    {
        if (beyaz == null) return;
        GUI.depth = -9000;
        var eski = GUI.color;
        if (flasA > 0.01f)
        {
            GUI.color = new Color(flasRenk.r, flasRenk.g, flasRenk.b, flasA);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), beyaz);
        }
        if (scareT >= 0f && sprYuz != null)
        {
            // 0..0.08 sn sert flaş, sonra yüz büyür ve titrer, 1.1 sn'den sonra söner
            float t = scareT;
            float a = t < 0.08f ? t / 0.08f : Mathf.Clamp01((1.15f - t) / 0.35f);
            float olcek = Mathf.Lerp(0.55f, 1.05f, Mathf.Clamp01(t / 0.35f));
            float boy = Mathf.Min(Screen.width, Screen.height) * 1.1f * olcek;
            float jx = Random.Range(-1f, 1f) * 14f * (1f - Mathf.Clamp01(t / 1.1f)) + Random.Range(-3f, 3f);
            float jy = Random.Range(-1f, 1f) * 14f * (1f - Mathf.Clamp01(t / 1.1f)) + Random.Range(-3f, 3f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f * a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), beyaz);
            GUI.color = new Color(1f, 0.85f + Random.Range(-0.1f, 0.1f), 0.85f, a);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - boy * 0.5f + jx, Screen.height * 0.5f - boy * 0.5f + jy, boy, boy), sprYuz.texture, ScaleMode.ScaleToFit, true);
        }
        GUI.color = eski;
    }
}
