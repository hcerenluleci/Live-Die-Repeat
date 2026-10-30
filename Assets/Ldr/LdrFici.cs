using UnityEngine;

/// <summary>
/// Fıçı: 1 can (tek mermide kırılır), çarpışma alanı küçük bir TETİK kutusu
/// (1,00 x 0,45, fıçının dibinde; yürünerek geçilir ama mermiyi karşılar), boyut her fıçıda 0,9 - 1,15 arası.
/// Kırılınca %50 şansla yardım eşyası düşürür. Tarafsızdır (takım 2): oyuncu da düşman da vurabilir.
/// </summary>
public class LdrFici : MonoBehaviour
{
    const float KutuGenislik = 1.0f, KutuYukseklik = 0.45f;
    const float DusurmeSansi = 0.5f;

    SpriteRenderer sr;
    Sprite saglam, kirik;
    bool altin;
    float kirmizi;
    LdrCan can;
    const float TemasHasari = 1f;   // oyuncu üstünden geçince ya da devrik masa çarpınca (fıçının canı 1: kırılır)

    public static LdrFici Kur(Transform ebeveyn, Vector2 dunya, bool altin, float ozelOlcek = -1f)
    {
        var go = new GameObject(altin ? "AltinFici" : "Fici");
        go.transform.SetParent(ebeveyn, false);
        go.transform.position = new Vector3(dunya.x, dunya.y, 0f);
        float olcek = ozelOlcek > 0f ? ozelOlcek : (altin ? 1.5f : Random.Range(0.9f, 1.15f));          // şablon ölçek vermediyse
        go.transform.localScale = new Vector3(olcek, olcek, 1f);
        int katman = LayerMask.NameToLayer("Esya");
        if (katman >= 0) go.layer = katman;

        string ad = altin ? "barrel_gold_" : "barrel_wood_";
        var f = go.AddComponent<LdrFici>();
        f.altin = altin;
        // çizim boyutu (ölçek 1'de): tahta fıçı 1,8 x 1,8 birim, pivot %10 altta; altın fıçı küçük çizilir, şablon ölçeğiyle büyür
        Vector2 boyut = altin ? new Vector2(0.9f, 1.1f) : new Vector2(1.8f, 1.8f);
        f.saglam = LdrResim.Fit(ad + "ok", boyut, new Vector2(0.5f, altin ? 0f : 0.1f), false, 1f);
        f.kirik = f.saglam != null ? LdrResim.Get(ad + "broken", f.saglam.pixelsPerUnit, new Vector2(0.5f, 0.3f)) : null;   // enkaz: fıçıyla aynı piksel boyutu, yerde yatar
        f.sr = LdrGorsel.Ciz(go.transform, "Govde", f.saglam, Vector3.zero, 6, LdrGorsel.Isikli());

        // çarpışma kutusu: fıçının dibi (ölçek içinde)
        var bc = go.AddComponent<BoxCollider2D>();
        bc.isTrigger = true;
        bc.size = altin ? new Vector2(0.75f, 0.35f) : new Vector2(KutuGenislik, KutuYukseklik);
        bc.offset = altin ? new Vector2(0f, 0.18f) : new Vector2(0f, 0.23f);

        var can = go.AddComponent<LdrCan>();
        f.can = can;
        can.Kur(2, 1, 0f);
        can.Hasarlandi += (c, k) => { f.kirmizi = 0.08f; };
        can.Oldu += f.Kirildi;
        return f;
    }

    /// <summary>Temas: oyuncu fıçının üstünden geçerse ya da devrik masa fıçıya çarparsa fıçı kırılır.</summary>
    void OnTriggerStay2D(Collider2D o)
    {
        if (can == null || can.Olu) return;
        bool oyuncu = o.GetComponentInParent<LdrYasayan>() != null;
        var masa = o.GetComponent<LdrMasa>();
        if (oyuncu || (masa != null && masa.Devrildi)) can.Hasar(TemasHasari, o.transform.position);
    }

    void Update()
    {
        if (kirmizi > 0f) { kirmizi -= Time.deltaTime; sr.color = new Color(1f, 0.5f, 0.5f); }
        else sr.color = Color.white;
    }

    void Kirildi(LdrCan c)
    {
        foreach (var k in GetComponents<Collider2D>()) k.enabled = false;
        if (kirik != null) sr.sprite = kirik;
        sr.sortingOrder = 3;
        sr.color = Color.white;
        Color a = altin ? new Color(1f, 0.85f, 0.3f) : new Color(0.6f, 0.4f, 0.22f);
        LdrVfx.Burst(transform.position, a, new Color(0.3f, 0.18f, 0.1f), 14, 3.2f, 0.12f, 0.6f, 0.8f, 0, 14);
        LdrKamera.Salla(0.04f);
        LdrSes.Cal("sfx_enemy_hit", 0.55f, 0.7f);
        if (Random.value < DusurmeSansi) LdrEsya.Dusur(transform.position, transform.parent);
        enabled = false;
    }
}
