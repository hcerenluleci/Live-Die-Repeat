using System;
using UnityEngine;

/// <summary>
/// "Belirme" efekti: nesne parlayan, titreşen noktalardan oluşarak ortaya çıkar. Her piksele rastgele bir eşik verilir;
/// süre ilerledikçe eşiği geçen pikseller asıl rengine döner, eşiğin hemen üstündekiler parlak kıvılcım olarak yanar.
/// Parlaklık nabız gibi 1,2 ile 1,9 arasında salınır. İşlem doğrudan sprite pikselleri üzerinde yapılır (gölgelendirici gerekmez).
/// Bitince asıl sprite, malzeme ve renk geri verilir.
/// </summary>
public class LdrBelirme : MonoBehaviour
{
    SpriteRenderer sr;
    Sprite asil, gecici;
    Material asilMat;
    Color asilRenk;
    Texture2D tex;
    Color32[] kaynak, cikti;
    float[] temel, oynak;
    int w, h;
    float sure, t, titresimSayac, nabiz = NabizEn;
    const float NabizEn = 1.2f, NabizEnCok = 1.9f, NabizHizi = 1.4f, TitresimAraligi = 0.08f;
    bool artiyor = true;
    Color renk;
    Action bitti;

    /// <param name="renk">kıvılcım rengi</param>
    public static LdrBelirme Baslat(SpriteRenderer sr, Color renk, float sure, Action bitince = null)
    {
        if (sr == null || sr.sprite == null) { if (bitince != null) bitince(); return null; }
        var b = sr.gameObject.AddComponent<LdrBelirme>();
        if (!b.Hazirla(sr, renk, sure, bitince)) { Destroy(b); if (bitince != null) bitince(); return null; }
        return b;
    }

    bool Hazirla(SpriteRenderer s, Color r, float sr2, Action bitince)
    {
        sr = s; renk = r; sure = Mathf.Max(0.1f, sr2); bitti = bitince;
        asil = sr.sprite; asilMat = sr.sharedMaterial; asilRenk = sr.color;
        try
        {
            Rect rc = asil.textureRect;
            w = (int)rc.width; h = (int)rc.height;
            var src = asil.texture.GetPixels32();
            int tw = asil.texture.width;
            kaynak = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    kaynak[y * w + x] = src[((int)rc.y + y) * tw + (int)rc.x + x];
        }
        catch (Exception) { return false; }       // doku okunamıyorsa efekt atlanır

        cikti = new Color32[w * h];
        temel = new float[w * h]; oynak = new float[w * h];
        var rng = new System.Random(w * 31 + h * 17 + (int)(transform.position.x * 13f));
        for (int i = 0; i < temel.Length; i++) temel[i] = (float)rng.NextDouble();
        tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        Vector2 pv = new Vector2(asil.pivot.x / w, asil.pivot.y / h);
        gecici = Sprite.Create(tex, new Rect(0, 0, w, h), pv, asil.pixelsPerUnit);
        gecici.hideFlags = HideFlags.HideAndDontSave;
        sr.sharedMaterial = LdrGorsel.Isiksiz();      // parlayan noktalar ışıktan etkilenmesin
        sr.color = Color.white;
        sr.sprite = gecici;
        Isle(0f);
        return true;
    }

    void Update()
    {
        t += Time.deltaTime;
        // parlaklık nabzı
        nabiz += (artiyor ? NabizHizi : -NabizHizi) * Time.deltaTime;
        if (nabiz >= NabizEnCok) { nabiz = NabizEnCok; artiyor = false; }
        if (nabiz <= NabizEn) { nabiz = NabizEn; artiyor = true; }

        // titreşim: eşikler kısa aralıklarla biraz oynatılır, kıvılcımlar kıpırdar
        titresimSayac -= Time.deltaTime;
        if (titresimSayac <= 0f)
        {
            titresimSayac = TitresimAraligi;
            for (int i = 0; i < oynak.Length; i++) oynak[i] = UnityEngine.Random.Range(-0.14f, 0.14f);
        }
        float d = Mathf.Clamp01(t / sure);
        Isle(d);
        if (t >= sure) Bitir();
    }

    void Isle(float d)
    {
        // kıvılcım rengi: nabız çarpanıyla (piksel rengi 1'i aşamadığı için açık tona doğru kayar)
        float p = nabiz;
        for (int i = 0; i < cikti.Length; i++)
        {
            float n = Mathf.Clamp01(temel[i] + oynak[i]);
            byte a = kaynak[i].a;
            if (a > 0 && n < d - 0.10f) { cikti[i] = kaynak[i]; continue; }                 // oluşmuş kısım
            float bant = a > 0 ? 0.08f : 0.26f;                                              // boş alanda daha geniş kıvılcım bandı
            if (n >= d - 0.10f && n < d + bant)
            {
                float k = 0.55f + 0.45f * ((i * 2654435761u >> 7) % 100 / 100f);             // her noktanın parlaklığı farklı
                cikti[i] = new Color(Mathf.Min(1f, renk.r * p * k), Mathf.Min(1f, renk.g * p * k), Mathf.Min(1f, renk.b * p * k), 1f);
                continue;
            }
            cikti[i] = new Color32(0, 0, 0, 0);
        }
        tex.SetPixels32(cikti);
        tex.Apply(false);
    }

    void Bitir()
    {
        if (sr != null) { sr.sprite = asil; sr.sharedMaterial = asilMat; sr.color = asilRenk; }
        if (tex != null) Destroy(tex);
        if (gecici != null) Destroy(gecici);
        if (bitti != null) bitti();
        Destroy(this);
    }

    void OnDestroy()
    {
        if (sr != null && sr.sprite == gecici) { sr.sprite = asil; sr.sharedMaterial = asilMat; sr.color = asilRenk; }
    }
}
