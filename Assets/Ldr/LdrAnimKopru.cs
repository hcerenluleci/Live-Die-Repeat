using UnityEngine;

// =====================================================================================
// DERS GEREKSİNİMİ: ANIMATOR (2 Animator, her birinde en az 3 durum)
// Animator 1: OyuncuAnim  -> Durus, Yuru, Takla, Olu (4 durum)
// Animator 2: DusmanAnim  -> Uyuyor, Uyari, Kovala, Saldir, Sendeler, Oldu (6 durum, düşmanın FSM'i ile birebir)
// Çizimler kodla üretildiği için animasyon klipleri sprite yerine bu bileşenin alanlarını (kare, ölçek, eğim) oynatır;
// oyuncu/düşman kodu her karede bu değerleri gövdeye uygular. Hangi animasyonun oynadığına Animator karar verir.
// Animator dosyaları: Assets/Resources/Animasyon (Unity menüsü: Ldr > Animator'ları Kur).
// =====================================================================================
/// <summary>
/// Animator köprüsü. Oyuncu ve düşman çizimleri kodla üretildiği için animasyon klipleri doğrudan sprite'a değil,
/// bu bileşenin alanlarına yazar: kare (0 / 1: hangi çizim karesi), olcekX / olcekY (gövde ölçeği), egim (gövde açısı, derece).
/// Oyuncu ve düşman kodu her karede bu değerleri okuyup gövdeye uygular. Böylece hangi animasyonun oynadığına
/// Animator'daki durum makinesi karar verir (Animasyon/OyuncuAnim ve Animasyon/DusmanAnim denetleyicileri).
/// Klipler ve denetleyiciler Ldr > Animator'ları Kur menüsüyle (LdrAnimasyonKur) üretilir.
/// </summary>
public class LdrAnimKopru : MonoBehaviour
{
    [Tooltip("0 = ilk kare (duruş / kapalı çene), 1 = ikinci kare (adım / açık çene)")]
    public float kare;
    public float olcekX = 1f;
    public float olcekY = 1f;
    [Tooltip("Gövde açısı (derece)")]
    public float egim;

    public Animator Animator { get; private set; }

    public int Kare { get { return kare >= 0.5f ? 1 : 0; } }
    public Vector3 Olcek { get { return new Vector3(olcekX, olcekY, 1f); } }

    /// <summary>Nesneye Animator + köprü ekler. Denetleyici bulunamazsa null döner; o zaman oyuncu ve düşman kodu kendi yedek çizimini kullanır.</summary>
    public static LdrAnimKopru Ekle(GameObject go, string denetleyici)
    {
        var rac = Resources.Load<RuntimeAnimatorController>("Animasyon/" + denetleyici);
        if (rac == null) return null;
        var k = go.AddComponent<LdrAnimKopru>();
        k.Animator = go.AddComponent<Animator>();
        k.Animator.runtimeAnimatorController = rac;
        k.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return k;
    }

    public void Bool(string ad, bool deger) { if (Animator != null) Animator.SetBool(ad, deger); }
    public void Int(string ad, int deger) { if (Animator != null) Animator.SetInteger(ad, deger); }
}
