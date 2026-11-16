# LIVE-DIE-REPEAT

**YAŞA. ÖL. TEKRARLA.**
*Zindan seni hatırlar. Sen onu hatırlayacak mısın?*

Karanlık bir zindanda geçen, piksel sanat bir **korku nişancısı** (top-down shooter). Ölüm burada son değil, ilerlemenin kendisidir: öldükçe topladığın ruhlar yeni silahların kilidini açar, bulduğun kaçış parçaları yaşamlar arasında kalıcıdır.

Üniversite oyun geliştirme dersi grup projesi (Grup 11). Unity 6 (6000.3.24f1), 2D URP.

**Ekip:** Hülya Ceren LÜLECİ · Ali Çağrı SEPET

## Oyun döngüsü

1. **YAŞA:** Odaya gir, canavarları vur, ruh topla (art arda öldürmek çarpan verir).
2. **ÖL:** Ölüm sayacı artar, "ÖLDÜN" ekranı gelir.
3. **TEKRARLA:** Silah Pazarı'nda ruhlarınla kilitli silahları aç, karakterini seç (Liz / Jan), yeniden doğ.

**Gerçek çıkış:** 4 bölümün her birinde yalnızca **mühürlü bir sandıktan** kaçış parçası çıkar. 4 parçayla son bölümü bitirirsen gerçek kaçış sonunu görürsün; eksikse çıkış mühürlü kalır ve döngü devam eder.

## Kontroller

| Tuş | İşlev |
| --- | --- |
| `W` `A` `S` `D` | Hareket |
| Fare | Nişan / ateş (sol tık) |
| `R` | Şarjör değiştir |
| `E` | Sandık aç, masa devir |
| `Space` | Yuvarlanma (takla) |
| `T` | Doğuş noktasına ışınlan |
| `F` | Ateş topu |
| `B` | El feneri |
| `M` | Harita |
| `ESC` | Duraklat |

## Mekanikler ve sistemler

- Ölüm-ruh döngüsü, ruh zinciri, kalıcı ilerleme ve **Silah Pazarı**
- **Mühürlü sandık** ve kalıcı **kaçış parçaları**
- **Doğuşa ışınlan**, **ateş topu**, **ruh heykeli** (iyileşme), **el feneri**
- **Düşman yapay zekâsı:** sonlu durum makinesi (Uyuyor, Uyarı, Kovala, Saldır, Sendele, Öldü), 5 yaratık türü ve patron savaşları
- **Animator:** `OyuncuAnim` (Durus, Yuru, Takla, Olu) ve `DusmanAnim` (FSM'in 6 durumu, her biri ayrı animasyon)
- Parçacık efektleri (ölüm, vuruş, ışınlanma, sandık), 2D ışıklar, kamera sarsıntısı, halüsinasyon efekti
- Tamamen Türkçe, piksel yazı tipli arayüz: ana menü, duraklatma menüsü, HUD

### Kim hangi mekaniği yaptı

| Hülya Ceren Lüleci | Ali Çağrı Sepet |
| --- | --- |
| Ölüm döngüsü ve kalıcı ilerleme (`LiveDieRepeat.cs`, `LdrIlerleme.cs`) | Düşman yapay zekâsı, FSM + A* (`LdrDusman.cs`, `LdrYol.cs`) |
| Ruh zinciri çarpanı (`LdrPuan`, `LdrMermi.cs`) | Devrilebilir masa siperi (`LdrMasa.cs`) |
| Mühürlü sandık ve kaçış parçaları (`LdrSandik.cs`, `LdrParca.cs`) | Takla / yuvarlanma (`LdrYasayan.cs`) |

## Proje yapısı

- `Assets/Ldr/`: oyunun çekirdeği (`LdrOyun` sahne yöneticisi, `LdrKat`/`LdrOda`/`LdrSablon` zindan üretimi, `LdrYasayan` oyuncu, `LdrDusman` düşman FSM'i, `LdrSilah`/`LdrSilahTablosu`, `LdrHud`, `LdrMenu`, `LdrDuraklat`, `LdrHarita`, `LdrFener`, `LdrEfekt`, `LdrMuzik`, `LdrSes` ...).
- `Assets/LiveDieRepeat.cs`: ölüm sayacı, ÖLDÜN ekranı ve yeniden doğuş döngüsü.
- `Assets/Ldr/LdrResim.cs`, `LdrKostum.cs`, `LdrYaratikSayfa.cs`, `LdrUI.cs`, `LdrYazi.cs`: koda gömülü piksel çizimler, karakterler, yaratıklar, ortak arayüz araçları ve piksel yazı tipi (LDR Pixel).
- `Assets/Ldr/LdrSablonVerisi.cs`: oda şablonları, harita grafikleri ve bölüm listeleri. Elle düzenlenmez; `Araclar/zindan.py` ile üretilir.
- `Assets/Ldr/LdrAnimKopru.cs` ve `Assets/Resources/Animasyon/`: Animator köprüsü, denetleyiciler ve klipler.
- `Assets/Resources/`: seslerimiz, yazı tipimiz ve animasyonlar.
- `Araclar/zindan.py`: zindan tasarım aracımız. Odaları (biçim, duvar, kapı, sütun, eşya, doğma noktaları) ve kat grafiklerini üretir, her odanın kapılarına ulaşılabildiğini ve her grafiğin oyundaki yerleştirme kurallarıyla kurulabildiğini sınar. Çalıştırma: `python Araclar/zindan.py Assets/Ldr/LdrSablonVerisi.cs --onizle`
- `Assets/Ldr/Editor/`: editör araçları. **Ldr > Animator'ları Kur** animasyon denetleyicilerini, **Ldr > LdrOyun sahnesini kur ve aç** sahneyi yeniden üretir, **Ldr > Derle** menüsü itch.io için Windows ve WebGL paketlerini `Builds/` klasörüne hazırlar.
- `Assets/Editor/`: geliştirme sırasında kullanılan geçici bakım araçları (teslim öncesi silinecek).

## Çalıştırma

Unity Hub ile projeyi **6000.3.24f1** sürümüyle aç, `Assets/_Scenes/LdrOyun` sahnesini aç ve Play'e bas.

## Kaynaklar

- Geliştirmede, hocanın izniyle bir yapay zekâ asistanından (Claude) yardım alındı. Tasarım kararları, testler ve son sürüm ekibe aittir; her üye kendi mekaniklerini açıklayabilir.

- Oyunun kodu, piksel çizimleri, sesleri, müzikleri, yazı tipi, oda tasarımları ve denge değerleri Grup 11 tarafından bu proje için üretilmiştir.
- Unity paketleri (URP, 2D Sprite/Tilemap, uGUI ve TextMesh Pro kaynakları; TextMesh Pro'nun varsayılan LiberationSans yazı tipi OFL lisanslıdır) Unity Package Manager üzerinden kendi lisanslarıyla kullanılır.

## Lisans

Telif Hakkı © 2026 Hülya Ceren Lüleci. Tüm hakları saklıdır. Kaynak kod ve içerik açık kaynak değildir; ayrıntılar için `LICENSE` dosyasına bakın.
