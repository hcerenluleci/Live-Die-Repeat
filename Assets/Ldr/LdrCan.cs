using System;
using UnityEngine;

// =====================================================================================
// CAN: oyuncu ve düşmanda aynı bileşen. Hasar alınca koruma süresi başlar; can 0 olunca Oldu olayı tetiklenir.
// HUD'da 5 kalp gösterilir: 1 kalp = canın %20'si.
// =====================================================================================
/// <summary>
/// Can. Oyuncuda ve düşmanda aynı bileşen kullanılır. Değerler "can puanı"dır
/// (Liz 100, Jan 70, düşmanlar 14-580); mermi hasarı doğrudan bu puandan düşer.
/// takim: 0 = yaşayan (oyuncu), 1 = düşman. Mermiler kendi takımına hasar vermez.
/// </summary>
public class LdrCan : MonoBehaviour
{
    [Header("Ayar")]
    [Tooltip("0 = oyuncu, 1 = düşman")]
    public int takim;
    [Tooltip("En yüksek can puanı")]
    public float azami = 100f;
    [Tooltip("Hasar yedikten sonra dokunulmazlık süresi (Liz 2,5 sn, Jan 3,5 sn)")]
    public float korumaSuresi;

    [Header("Anlık durum (yalnızca görmek için)")]
    [SerializeField] float simdiki;
    [SerializeField] bool olu;
    [SerializeField] float korumaKalan;

    /// <summary>Bir "birim" tam canın beşte biridir (HUD'daki 5 kan damlasının biri).</summary>
    public const float BirimOrani = 0.2f;

    public float Simdiki { get { return simdiki; } }
    public bool Olu { get { return olu; } }
    public float KorumaKalan { get { return korumaKalan; } }
    public float Oran { get { return azami > 0f ? Mathf.Clamp01(simdiki / azami) : 0f; } }
    public Func<bool> ekKoruma;              // ör. oyuncu taklada dokunulmaz

    public Action<LdrCan, Vector2> Hasarlandi;   // (bu can, hasarın geldiği konum)
    public Action<LdrCan> Oldu;
    public Action<LdrCan, float> Iyilesti;

    public bool Korumali { get { return korumaKalan > 0f || (ekKoruma != null && ekKoruma()); } }

    public void Kur(int takim, float azami, float korumaSuresi)
    {
        this.takim = takim;
        this.azami = azami;
        this.korumaSuresi = korumaSuresi;
        Doldur();
    }

    public void Doldur()
    {
        simdiki = azami;
        olu = false;
        korumaKalan = 0f;
    }

    /// <summary>Hasar uygular. Gerçekten hasar işlendiyse true döner.</summary>
    public bool Hasar(float miktar, Vector2 kaynak)
    {
        if (olu || Korumali || miktar <= 0f) return false;
        simdiki = Mathf.Max(0f, simdiki - miktar);
        korumaKalan = korumaSuresi;
        if (Hasarlandi != null) Hasarlandi(this, kaynak);
        if (simdiki <= 0f)
        {
            olu = true;
            if (Oldu != null) Oldu(this);
        }
        return true;
    }

    /// <summary>Yüzde olarak iyileştirir.</summary>
    public void IyilesYuzde(float yuzde)
    {
        if (olu) return;
        float once = simdiki;
        simdiki = Mathf.Min(azami, simdiki + azami * yuzde / 100f);
        if (Iyilesti != null && simdiki > once) Iyilesti(this, simdiki - once);
    }

    /// <summary>Birim olarak iyileştirir: 1 birim = %20.</summary>
    public void Iyiles(int birim) { IyilesYuzde(birim * BirimOrani * 100f); }

    void Update()
    {
        if (korumaKalan > 0f) korumaKalan -= Time.deltaTime;
    }
}
