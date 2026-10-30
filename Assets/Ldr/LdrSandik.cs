using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sandık. İki yerde bulunur: sunak (sandık) odası ve temizlenen savaş odaları (oda büyüdükçe çıkma şansı artar).
/// Mühürlü olan (mor ışık, süzülen kıvılcımlar) kaçış parçası saklar; diğerleri 1-3 yardım eşyası verir:
/// silah kasası, cephane, can üzümü. E ile açılır. Çizim: LdrResim "sandik_sayfa" (4 kare, kapalıdan açığa).
/// </summary>
public class LdrSandik : MonoBehaviour, ILdrEtkilesim
{
    // sandık: 4 kare (kapalı ... açık), 2 x 2 birim; sıcak renk tonu; çarpışma kutusu 1,90 x 1,50
    static readonly Color Ton = new Color(1f, 0.8f, 0.6f);
    static Sprite[] kareler;
    const float KareSuresi = 0.07f;

    int derinlik, madde;
    bool muhurlu, acik, beliriyor;
    SpriteRenderer sr;
    float acilmaSuresi = -1f;
    Light2D isik;
    ParticleSystem kivilcim;

    public Vector2 Konum { get { return (Vector2)transform.position + new Vector2(0f, 0.8f); } }
    public string Yazi1 { get { return "[E]  SANDIĞI AÇ"; } }
    public string Yazi2 { get { return muhurlu ? "MÜHÜRLÜ  -  KAÇIŞ PARÇASI" : ""; } }
    public bool Hazir { get { return !acik && !beliriyor; } }

    /// <param name="muhurlu">kaçış parçası saklar</param>
    /// <param name="madde">mühürlü değilse kaç yardım eşyası verir (0-3)</param>
    /// <param name="belir">sandık birkaç saniyede parlayan noktalardan oluşarak belirir (LdrBelirme)</param>
    public static LdrSandik Kur(Transform ebeveyn, Vector2 dunya, int derinlik, bool muhurlu, int madde, bool belir = false)
    {
        var go = new GameObject("Sandik");
        go.transform.SetParent(ebeveyn, false);
        go.transform.position = new Vector3(dunya.x, dunya.y, 0f);
        int katman = LayerMask.NameToLayer("Duvar");
        if (katman >= 0) go.layer = katman;
        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(1.9f, 1.5f);
        bc.offset = new Vector2(0f, 0.75f);         // pivot altta

        var s = go.AddComponent<LdrSandik>();
        s.derinlik = derinlik;
        s.muhurlu = muhurlu;
        s.madde = madde;
        s.sr = LdrGorsel.Ciz(go.transform, "Govde", Kareler()[0], Vector3.zero, 6, LdrGorsel.Isikli());
        s.sr.color = Ton;

        var lg = new GameObject("Isik");
        lg.transform.SetParent(go.transform, false);
        lg.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        s.isik = lg.AddComponent<Light2D>();
        s.isik.lightType = Light2D.LightType.Point;
        s.isik.color = s.muhurlu ? new Color(0.75f, 0.35f, 1f) : new Color(1f, 0.75f, 0.4f);
        s.isik.intensity = s.muhurlu ? 1.3f : 0.7f;
        s.isik.pointLightOuterRadius = s.muhurlu ? 3.6f : 2.4f;
        s.isik.pointLightInnerRadius = 0.2f;

        if (belir)
        {
            s.beliriyor = true;
            LdrBelirme.Baslat(s.sr, new Color(0.8f, 0.45f, 1f), 2.5f, () => { if (s != null) s.beliriyor = false; });
        }
        if (s.muhurlu) s.kivilcim = LdrVfx.Motes(go.transform, new Color(0.85f, 0.55f, 1f), 9f, 0.55f, 0.4f, 0, 14);
        return s;
    }

    void OnEnable() { LdrEtkilesim.Kaydet(this); LdrSira.Kaydet(GetComponent<Collider2D>()); }
    void OnDisable() { LdrEtkilesim.Sil(this); LdrSira.Sil(GetComponent<Collider2D>()); }

    void Update()
    {
        if (acilmaSuresi >= 0f)
        {
            acilmaSuresi += Time.deltaTime;
            int k = Mathf.Min(Kareler().Length - 1, (int)(acilmaSuresi / KareSuresi));
            sr.sprite = Kareler()[k];
            if (k >= Kareler().Length - 1) acilmaSuresi = -1f;
        }
        if (acik || isik == null) return;
        isik.intensity = (muhurlu ? 1.3f : 0.7f) * (0.85f + 0.15f * Mathf.Sin(Time.time * 4f));
    }

    public void Etkilesim(LdrYasayan oyuncu)
    {
        if (acik) return;
        acik = true;
        LdrSes.Cal("sfx_door", 0.7f);
        acilmaSuresi = 0f;
        Vector3 ust = transform.position + new Vector3(0f, 1.1f, 0f);
        if (muhurlu)
        {
            LdrVfx.Burst(ust, new Color(0.85f, 0.55f, 1f), Color.white, 26, 4f, 0.18f, 0.8f, 0f, 0, 40);
            LdrParcaNesnesi.Olustur((Vector2)transform.position + new Vector2(0f, 1.2f), derinlik, transform.parent);
            if (kivilcim != null) kivilcim.Stop();
        }
        else
        {
            LdrVfx.Burst(ust, new Color(1f, 0.8f, 0.4f), Color.white, 14, 3f, 0.14f, 0.6f, 0f, 0, 40);
            IcerikBirak(madde);
        }
        isik.intensity = 0.5f;
        isik.pointLightOuterRadius = 2f;
    }

    /// <summary>İçerik: 1 madde = rastgele biri; 2 madde = ikili; 3 ve üstü = üçü birden.</summary>
    void IcerikBirak(int n)
    {
        if (n <= 0) { LdrHud.Duyur("SANDIK BOŞ ÇIKTI", new Color(0.8f, 0.75f, 0.9f)); return; }
        bool silah = false, cephane = false, can = false;
        if (n == 1) { int c = Random.Range(0, 3); silah = c == 0; cephane = c == 1; can = c == 2; }
        else if (n == 2) { int c = Random.Range(0, 3); silah = c != 1; cephane = c != 2; can = c != 0; }
        else silah = cephane = can = true;
        var liste = new System.Collections.Generic.List<LdrEsyaTuru>();
        if (silah) liste.Add(LdrEsyaTuru.Kasa);
        if (cephane) liste.Add(LdrEsyaTuru.Cephane);
        if (can) liste.Add(LdrEsyaTuru.Uzum);
        var oyun = LdrOyun.Ornek;
        var oda = oyun != null && oyun.kat != null ? oyun.kat.Gecerli : null;
        float[] dx = { 0f, -1.3f, 1.3f };
        for (int i = 0; i < liste.Count; i++)
        {
            // sandığın önünde (güneyinde), yürünebilir bir yere
            Vector2 p = (Vector2)transform.position + new Vector2(dx[i], -0.9f);
            if (oda != null && !oda.Yuruyebilir(p)) p = (Vector2)transform.position + new Vector2(dx[i], 2.4f);
            if (oda != null && !oda.Yuruyebilir(p)) p = oda.EnYakinZemin(p);
            LdrEsya.Olustur(liste[i], p, transform.parent);
        }
        LdrHud.Duyur(liste.Count == 1 ? "SANDIKTAN BİR YARDIM ÇIKTI" : "SANDIKTAN YARDIM EŞYALARI ÇIKTI", new Color(1f, 0.85f, 0.45f));
    }

    static Sprite[] Kareler()
    {
        if (kareler != null && kareler[0] != null) return kareler;
        var t = LdrResim.Tex("sandik_sayfa");
        kareler = new Sprite[4];
        for (int i = 0; i < 4; i++)
        {
            kareler[i] = Sprite.Create(t, new Rect(i * 32, 0, 32, 32), new Vector2(0.5f, 0f), 16f);
            kareler[i].hideFlags = HideFlags.HideAndDontSave;
        }
        return kareler;
    }
}
