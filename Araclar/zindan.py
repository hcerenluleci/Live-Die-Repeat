# LIVE-DIE-REPEAT oda şablonu üreticisi (ekibin kendi zindan tasarımı).
# Çıktı: Assets/Ldr/LdrSablonVerisi.cs
# Hücre bitleri: 1 zemin, 4 katı, 8 harita. Duvar = 13, zemin = 9, dış = 0.
import random, math, sys
from collections import deque

ZEMIN, DUVAR, BOS = 9, 13, 0
KUZEY_DUVAR = 3          # kuzey duvarı kalınlığı (derinlik hissi)

class Oda:
    def __init__(s, ad, tur, gen, yuk):
        s.ad, s.tur, s.W, s.H = ad, tur, gen, yuk
        s.h = [[BOS] * gen for _ in range(yuk)]
        s.kapilar = []   # (x, y, yon, kx0, ky0, kx1, ky1)
        s.dogma = []
        s.esya = []
    def al(s, x, y):
        return s.h[y][x] if 0 <= x < s.W and 0 <= y < s.H else BOS
    def koy(s, x, y, v):
        if 0 <= x < s.W and 0 <= y < s.H: s.h[y][x] = v
    def dikdortgen(s, x0, y0, x1, y1, v):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1): s.koy(x, y, v)
    def zemin_mi(s, x, y): return s.al(x, y) == ZEMIN

def sekil_kur(o, maske):
    """maske(x,y) -> True ise oda içi. Kenar hücreler duvar, kuzey duvarı kalın."""
    W, H = o.W, o.H
    ic = [[maske(x, y) for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if not ic[y][x]: continue
            # zemin: iç hücre, sol/sağ/alt komşuları içeride, üstte KUZEY_DUVAR kadar içeride
            z = all(0 <= x + dx < W and 0 <= y + dy < H and ic[y + dy][x + dx] for dx, dy in ((-1, 0), (1, 0), (0, -1), (-1, -1), (1, -1)))
            for k in range(1, KUZEY_DUVAR + 1):
                if not (y + k < H and ic[y + k][x] and (x - 1 >= 0 and ic[y + k][x - 1]) and (x + 1 < W and ic[y + k][x + 1])): z = False
            o.h[y][x] = ZEMIN if z else DUVAR

def kapi_ac(o, yon, merkez):
    """yon: N E S W. merkez: kapının ortası (x ya da y). Boşluk 3 hücre."""
    W, H = o.W, o.H
    if yon == 'N':
        x = merkez
        y = H - 1
        while y >= 0 and o.al(x, y) == BOS: y -= 1
        assert y == H - 1, (o.ad, 'kuzey kapısı kenarda değil')
        yy = y
        while yy >= 0 and o.al(x, yy) != ZEMIN: yy -= 1
        o.dikdortgen(x - 1, yy + 1, x + 1, y, ZEMIN)
        o.kapilar.append((x, y, 'N', x - 1, yy + 1, x + 1, y))
    elif yon == 'S':
        x = merkez
        assert o.al(x, 0) != BOS, (o.ad, 'güney kapısı kenarda değil')
        yy = 0
        while o.al(x, yy) != ZEMIN: yy += 1
        o.dikdortgen(x - 1, 0, x + 1, yy - 1, ZEMIN)
        o.kapilar.append((x, 0, 'S', x - 1, 0, x + 1, yy - 1))
    elif yon == 'E':
        y = merkez
        assert o.al(W - 1, y) != BOS, (o.ad, 'doğu kapısı kenarda değil')
        xx = W - 1
        while o.al(xx, y) != ZEMIN: xx -= 1
        o.dikdortgen(xx + 1, y - 1, W - 1, y + 1, ZEMIN)
        o.kapilar.append((W - 1, y, 'E', xx + 1, y - 1, W - 1, y + 1))
    else:
        y = merkez
        assert o.al(0, y) != BOS, (o.ad, 'batı kapısı kenarda değil')
        xx = 0
        while o.al(xx, y) != ZEMIN: xx += 1
        o.dikdortgen(0, y - 1, xx - 1, y + 1, ZEMIN)
        o.kapilar.append((0, y, 'W', 0, y - 1, xx - 1, y + 1))

def dort_kapi(o, kx=None, dy=None):
    kx = o.W // 2 if kx is None else kx
    dy = (1 + (o.H - 1 - KUZEY_DUVAR)) // 2 if dy is None else dy
    kapi_ac(o, 'N', kx); kapi_ac(o, 'S', kx); kapi_ac(o, 'E', dy); kapi_ac(o, 'W', dy)

def blok(o, x0, y0, w, h):
    """Oda içine katı blok (sütun, bölme). Üst tarafı duvar görünümlü."""
    o.dikdortgen(x0, y0, x0 + w - 1, y0 + h - 1, DUVAR)

# ---------------------------------------------------------------- yardımcı: erişilebilirlik ve yerleşim

def erisim(o):
    bas = [(k[0], k[1]) for k in o.kapilar]
    gor = set([bas[0]]); q = deque([bas[0]])
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n not in gor and o.zemin_mi(*n): gor.add(n); q.append(n)
    for b in bas: assert b in gor, (o.ad, 'kapıya ulaşılamıyor', b)
    toplam = sum(1 for y in range(o.H) for x in range(o.W) if o.zemin_mi(x, y))
    assert len(gor) == toplam, (o.ad, 'kopuk zemin', len(gor), toplam)
    return gor

def acik(o, x, y, pay):
    for dx in range(-pay, pay + 1):
        for dy in range(-pay, pay + 1):
            if not o.zemin_mi(x + dx, y + dy): return False
    return True

def kapiya_uzak(o, x, y, d):
    return all(math.hypot(x - k[0], y - k[1]) >= d for k in o.kapilar)

def dogma_noktalari(o, adet, rng):
    sec = []
    for pay, ara in ((2, 6), (1, 5), (1, 4)):
        aday = [(x, y) for y in range(o.H) for x in range(o.W) if acik(o, x, y, pay) and kapiya_uzak(o, x, y, 5)]
        rng.shuffle(aday)
        for p in aday:
            if len(sec) >= adet: break
            if all(math.hypot(p[0] - q[0], p[1] - q[1]) >= ara for q in sec): sec.append(p)
    assert len(sec) >= max(2, adet - 2), (o.ad, 'doğma noktası az', len(sec))
    o.dogma = [(x + 0.5, y + 0.5) for x, y in sec]

def duvar_mesaleleri(o, aralik=9):
    """Kuzey duvarının yüzüne meşale: zeminin hemen üstündeki duvar sırası."""
    for x in range(2, o.W - 2):
        y = o.H - 1
        while y > 0 and not o.zemin_mi(x, y): y -= 1
        if y <= 0: continue
        if any(o.al(xx, yy) != DUVAR for xx in (x - 1, x, x + 1) for yy in (y + 1, y + 2, y + 3)): continue
        if not (o.zemin_mi(x - 1, y) and o.zemin_mi(x + 1, y)): continue
        if not kapiya_uzak(o, x, o.H - 1, 4): continue
        if any(abs(e[1] - (x + 0.5)) < aralik for e in o.esya if e[0] == 'a'): continue
        o.esya.append(('a', x + 0.5, y + 2.0, 1.0))

def kose_ficilari(o, rng, sans=0.7):
    koseler = []
    for y in range(o.H):
        for x in range(o.W):
            if not o.zemin_mi(x, y): continue
            yatay = (not o.zemin_mi(x - 1, y)) or (not o.zemin_mi(x + 1, y))
            dikey = (not o.zemin_mi(x, y - 1)) or (not o.zemin_mi(x, y + 1))
            if yatay and dikey and kapiya_uzak(o, x, y, 5): koseler.append((x, y))
    for x, y in koseler:
        if rng.random() > sans: continue
        if any(math.hypot(x + 0.5 - e[1], y + 0.5 - e[2]) < 3 for e in o.esya): continue
        n = rng.choice((1, 2, 2, 3))
        for i in range(n):
            px = x + 0.5 + (0.7 if not o.zemin_mi(x - 1, y) else -0.7) * (i % 2) * (1 if i else 0)
            py = y + 0.35 + (0.65 if not o.zemin_mi(x, y - 1) else -0.65) * (i // 2) + 0.15 * i
            px = max(1.2, min(o.W - 1.2, px))
            if not (o.zemin_mi(int(px), int(py)) and o.zemin_mi(int(px), int(py + 0.6))): continue
            altin = rng.random() < 0.12
            o.esya.append(('G' if altin else 'B', round(px, 2), round(py, 2), round(rng.uniform(1.45, 1.6) if altin else rng.uniform(0.86, 1.08), 2)))

def ayakta_mesale(o, x, y):
    o.esya.append(('b', x + 0.5, y + 0.1, 1.0))

def masa(o, x, y, dikey):
    o.esya.append(('V' if dikey else 'H', x + 0.5, y + 0.5, 1.0))

# ---------------------------------------------------------------- şablonlar

def dikdortgen_oda(ad, tur, W, H):
    o = Oda(ad, tur, W, H)
    sekil_kur(o, lambda x, y: True)
    return o

def hac_oda(ad, tur, W, H, kol):
    """Haç biçimli: köşelerden kol x kol kesik."""
    o = Oda(ad, tur, W, H)
    def m(x, y):
        kx = x < kol or x >= W - kol
        ky = y < kol or y >= H - kol
        return not (kx and ky)
    sekil_kur(o, m)
    return o

def pahli_oda(ad, tur, W, H, pah):
    """Köşeleri çapraz kesik (sekizgene yakın)."""
    o = Oda(ad, tur, W, H)
    def m(x, y):
        dx = min(x, W - 1 - x); dy = min(y, H - 1 - y)
        return dx + dy >= pah
    sekil_kur(o, m)
    return o

def uret_hepsi():
    rng = random.Random(4411)
    odalar = []

    # --- girişler
    o = dikdortgen_oda('Giris_Avlu', 'E', 22, 18); dort_kapi(o)
    for (x, y) in ((6, 5), (15, 5), (6, 10), (15, 10)): ayakta_mesale(o, x, y)
    duvar_mesaleleri(o, 7); dogma_noktalari(o, 4, rng); odalar.append(o)

    o = hac_oda('Giris_Hac', 'E', 23, 21, 6); dort_kapi(o, 11, 8)
    ayakta_mesale(o, 9, 8); ayakta_mesale(o, 13, 8)
    duvar_mesaleleri(o, 6); dogma_noktalari(o, 4, rng); odalar.append(o)

    # --- küçük savaş odaları (S)
    o = dikdortgen_oda('Hucre_Islak', 'S', 18, 15); dort_kapi(o)
    duvar_mesaleleri(o); kose_ficilari(o, rng); dogma_noktalari(o, 4, rng); odalar.append(o)

    o = dikdortgen_oda('Hucre_Sutun', 'S', 21, 17); dort_kapi(o)
    blok(o, 9, 6, 3, 3); ayakta_mesale(o, 5, 4); ayakta_mesale(o, 15, 4)
    duvar_mesaleleri(o); kose_ficilari(o, rng, 0.5); dogma_noktalari(o, 5, rng); odalar.append(o)

    o = dikdortgen_oda('Hucre_Bolme', 'S', 19, 19); dort_kapi(o, 9, 7)
    blok(o, 3, 11, 5, 1); blok(o, 11, 11, 5, 1)
    duvar_mesaleleri(o); kose_ficilari(o, rng); dogma_noktalari(o, 5, rng); odalar.append(o)

    o = hac_oda('Hucre_Hac', 'S', 21, 21, 6); dort_kapi(o, 10, 8)
    duvar_mesaleleri(o, 6); kose_ficilari(o, rng, 0.6); dogma_noktalari(o, 4, rng); odalar.append(o)

    # --- orta savaş odaları (M)
    o = dikdortgen_oda('Salon_DortSutun', 'M', 27, 21); dort_kapi(o)
    for (x, y) in ((6, 5), (19, 5), (6, 12), (19, 12)): blok(o, x, y, 2, 2)
    duvar_mesaleleri(o); kose_ficilari(o, rng); dogma_noktalari(o, 6, rng); odalar.append(o)

    o = dikdortgen_oda('Salon_Ziyafet', 'M', 29, 22); dort_kapi(o)
    masa(o, 10, 9, False); masa(o, 18, 9, False); masa(o, 14, 13, True)
    ayakta_mesale(o, 4, 15); ayakta_mesale(o, 24, 15)
    duvar_mesaleleri(o); kose_ficilari(o, rng); dogma_noktalari(o, 7, rng); odalar.append(o)

    o = pahli_oda('Salon_Pahli', 'M', 25, 25, 5); dort_kapi(o, 12, 10)
    blok(o, 11, 9, 3, 3); masa(o, 6, 10, True); masa(o, 18, 10, True)
    duvar_mesaleleri(o, 7); kose_ficilari(o, rng, 0.6); dogma_noktalari(o, 6, rng); odalar.append(o)

    o = dikdortgen_oda('Salon_Koridorlu', 'M', 31, 20); dort_kapi(o)
    blok(o, 5, 11, 8, 1); blok(o, 18, 11, 8, 1); blok(o, 5, 5, 8, 1); blok(o, 18, 5, 8, 1)
    duvar_mesaleleri(o); kose_ficilari(o, rng, 0.8); dogma_noktalari(o, 6, rng); odalar.append(o)

    # --- büyük savaş odaları (L)
    o = dikdortgen_oda('Mahzen_Sutunlu', 'L', 37, 29); dort_kapi(o)
    for x in (8, 17, 26):
        for y in (7, 17): blok(o, x, y, 2, 2)
    masa(o, 13, 12, False); masa(o, 23, 12, False)
    duvar_mesaleleri(o); kose_ficilari(o, rng); dogma_noktalari(o, 9, rng); odalar.append(o)

    o = dikdortgen_oda('Mahzen_Halka', 'L', 35, 31); dort_kapi(o)
    # ortada boşluklu kare halka
    blok(o, 10, 9, 15, 1); blok(o, 10, 20, 15, 1); blok(o, 10, 9, 1, 12); blok(o, 24, 9, 1, 12)
    o.dikdortgen(16, 9, 18, 9, ZEMIN); o.dikdortgen(16, 20, 18, 20, ZEMIN)
    o.dikdortgen(10, 14, 10, 15, ZEMIN); o.dikdortgen(24, 14, 24, 15, ZEMIN)
    ayakta_mesale(o, 17, 14)
    duvar_mesaleleri(o); kose_ficilari(o, rng); dogma_noktalari(o, 9, rng); odalar.append(o)

    o = pahli_oda('Mahzen_Genis', 'L', 41, 27, 6); dort_kapi(o, 20, 11)
    blok(o, 13, 6, 1, 9); blok(o, 27, 6, 1, 9)
    masa(o, 20, 16, False); masa(o, 7, 11, True); masa(o, 33, 11, True)
    duvar_mesaleleri(o); kose_ficilari(o, rng, 0.8); dogma_noktalari(o, 10, rng); odalar.append(o)

    # --- sandık odaları (H)
    o = dikdortgen_oda('Sunak_Kucuk', 'H', 15, 15); dort_kapi(o, 7, 5)
    o.esya.append(('y', 7.5, 7.0, 1.0)); ayakta_mesale(o, 3, 8); ayakta_mesale(o, 11, 8)
    duvar_mesaleleri(o, 6); dogma_noktalari(o, 2, rng); odalar.append(o)

    o = hac_oda('Sunak_Hac', 'H', 17, 17, 4); dort_kapi(o, 8, 6)
    o.esya.append(('y', 8.5, 7.0, 1.0)); duvar_mesaleleri(o, 6); dogma_noktalari(o, 2, rng); odalar.append(o)

    # --- çıkış (boss) odaları (B)
    o = dikdortgen_oda('Taht_Arena', 'B', 39, 33); dort_kapi(o)
    for (x, y) in ((7, 6), (30, 6), (7, 22), (30, 22)): blok(o, x, y, 2, 2)
    for (x, y) in ((12, 15), (26, 15)): ayakta_mesale(o, x, y)
    duvar_mesaleleri(o); dogma_noktalari(o, 4, rng); odalar.append(o)

    o = pahli_oda('Taht_Sekizgen', 'B', 35, 35, 8); dort_kapi(o, 17, 15)
    for (x, y) in ((10, 9), (23, 9), (10, 22), (23, 22)): blok(o, x, y, 2, 2)
    duvar_mesaleleri(o); dogma_noktalari(o, 4, rng); odalar.append(o)

    # --- koridorlar
    for ad, L in (('Gecit_Dikey_Kisa', 6), ('Gecit_Dikey', 9), ('Gecit_Dikey_Uzun', 13)):
        o = Oda(ad, 'N', 5, L)
        for y in range(L):
            o.h[y] = [DUVAR, ZEMIN, ZEMIN, ZEMIN, DUVAR]
        o.kapilar = [(2, L - 1, 'N', 1, L - 1, 3, L - 1), (2, 0, 'S', 1, 0, 3, 0)]
        o.dogma = [(2.5, L / 2)]
        if L >= 9: o.esya.append(('B', 1.6, round(L * 0.45, 2), 0.9))
        odalar.append(o)
    for ad, L in (('Gecit_Yatay_Kisa', 7), ('Gecit_Yatay', 10), ('Gecit_Yatay_Uzun', 14)):
        o = Oda(ad, 'W', L, 7)
        o.h[0] = [DUVAR] * L
        for y in (1, 2, 3): o.h[y] = [ZEMIN] * L
        for y in (4, 5, 6): o.h[y] = [DUVAR] * L
        o.kapilar = [(0, 2, 'W', 0, 1, 0, 3), (L - 1, 2, 'E', L - 1, 1, L - 1, 3)]
        o.dogma = [(L / 2, 2.5)]
        if L >= 10: o.esya.append(('a', L / 2, 5.0, 1.0))
        odalar.append(o)

    for o in odalar:
        if o.tur not in 'NW': erisim(o)
        # eşyalar zeminde mi
        for e in o.esya:
            if e[0] == 'a': continue
            assert o.zemin_mi(int(e[1]), int(e[2])), (o.ad, 'eşya zemin dışında', e)
    return odalar

# ---------------------------------------------------------------- harita grafikleri

def graf_uret(rng, derinlik):
    """E kök; odalar arasında hep koridor; bir sandık (H), en uzaktaki yaprak boss (B)."""
    hedef = {1: (6, 7), 2: (8, 9), 3: (10, 11)}[derinlik]
    oda_say = rng.randint(*hedef)
    boy_havuz = {1: 'SSSSM', 2: 'SSMMML', 3: 'SMMMLLL'}[derinlik]
    dugum = [('E', -1)]
    oda_idx = [0]; derin = {0: 0}; cocuk = {0: 0}
    for i in range(oda_say):
        adaylar = [r for r in oda_idx if cocuk[r] < (3 if r == 0 else 2)]
        ebeveyn = rng.choice(adaylar)
        cocuk[ebeveyn] += 1
        dugum.append(('C', ebeveyn)); c = len(dugum) - 1
        dugum.append((rng.choice(boy_havuz), c)); r = len(dugum) - 1
        oda_idx.append(r); derin[r] = derin[ebeveyn] + 1; cocuk[r] = 0
    yapraklar = [r for r in oda_idx if r != 0 and cocuk[r] == 0]
    boss = max(yapraklar, key=lambda r: (derin[r], r))
    dugum[boss] = ('B', dugum[boss][1])
    digerleri = [r for r in yapraklar if r != boss] or [r for r in oda_idx if r not in (0, boss)]
    sandik = rng.choice(digerleri)
    dugum[sandik] = ('H', dugum[sandik][1])
    return ','.join(t + str(p) for t, p in dugum)

# ---------------------------------------------------------------- yerleştirme benzetimi (LdrZindan ile aynı kurallar)

AYAR = {'N': (0, -1), 'E': (-1, 0), 'S': (0, 1), 'W': (1, 0)}
KARSI = {'N': 'S', 'S': 'N', 'E': 'W', 'W': 'E'}

def benzet(graf, sablonlar, rng):
    dug = [(t[0], int(t[1:])) for t in graf.split(',')]
    for dis in range(40):
        for ic in range(200):
            yer = []
            ok = True
            for i, (tur, eb) in enumerate(dug):
                if tur == 'E':
                    s = rng.choice([x for x in sablonlar if x.tur == 'E'])
                    yer.append([s, (0, 0), (s.W - 1, s.H - 1), [[k, False] for k in s.kapilar]]); continue
                e = yer[eb]
                yerlesti = False
                while not yerlesti:
                    ad = [k for k in e[3] if not k[1]]
                    if not ad: ok = False; break
                    kp = rng.choice(ad)
                    yon = kp[0][2]
                    if tur == 'C': cs = [x for x in sablonlar if x.tur == ('N' if yon in 'NS' else 'W')]
                    else: cs = [x for x in sablonlar if x.tur == tur]
                    s = rng.choice(cs)
                    kar = [k for k in s.kapilar if k[2] == KARSI[yon]]
                    if not kar: kp[1] = True; continue
                    k = kar[0]
                    px, py = e[1][0] + kp[0][0], e[1][1] + kp[0][1]
                    a = AYAR[k[2]]
                    alt = (px + a[0] - k[0], py + a[1] - k[1]); ust = (alt[0] + s.W - 1, alt[1] + s.H - 1)
                    if any(max(alt[0], d[1][0]) <= min(ust[0], d[2][0]) and max(alt[1], d[1][1]) <= min(ust[1], d[2][1]) for d in yer):
                        kp[1] = True; continue
                    kp[1] = True
                    kk = [[q, q is k] for q in s.kapilar]
                    yer.append([s, alt, ust, kk]); yerlesti = True
                if not ok: break
            if ok: return dis * 200 + ic + 1
    return None

# ---------------------------------------------------------------- C# çıktısı

def rle(o):
    out = []
    duz = [o.h[y][x] for y in range(o.H) for x in range(o.W)]
    i = 0
    while i < len(duz):
        j = i
        while j < len(duz) and duz[j] == duz[i]: j += 1
        n = j - i
        out.append(chr(65 + duz[i]) + (str(n) if n > 1 else ''))
        i = j
    return ''.join(out)

def satir(o):
    kap = ';'.join(f'{x},{y},{d},{a},{b},{c},{e}' for x, y, d, a, b, c, e in o.kapilar)
    dg = ';'.join(f'{x:.1f},{y:.1f}' for x, y in o.dogma)
    es = ';'.join(f'{t},{x:.2f},{y:.2f},{s:.2f}' for t, x, y, s in o.esya)
    return f'{o.ad}|{o.tur}|0,0,{o.W - 1},{o.H - 1}|{kap}|{dg}|{es}|{rle(o)}'

def onizle(o):
    ch = {ZEMIN: '.', DUVAR: '#', BOS: ' '}
    g = [[ch[o.h[y][x]] for x in range(o.W)] for y in range(o.H)]
    for e in o.esya:
        x, y = int(e[1]), int(e[2])
        if 0 <= y < o.H and 0 <= x < o.W: g[y][x] = e[0]
    for x, y in o.dogma: g[int(y)][int(x)] = '*'
    return '\n'.join(''.join(r) for r in reversed(g))

if __name__ == '__main__':
    odalar = uret_hepsi()
    rng = random.Random(2026)
    graflar = []
    for d in (1, 2, 3):
        n = 0
        while n < 5:
            g = graf_uret(rng, d)
            graflar.append(f'{d}_{n + 1}:{g}'); n += 1
    seviyeler = [
        ['Giris_Avlu', 'Giris_Hac', 'Hucre_Islak', 'Hucre_Sutun', 'Hucre_Bolme', 'Hucre_Hac', 'Salon_DortSutun', 'Salon_Ziyafet',
         'Sunak_Kucuk', 'Sunak_Hac', 'Taht_Arena'],
        ['Giris_Avlu', 'Giris_Hac', 'Hucre_Sutun', 'Hucre_Bolme', 'Hucre_Hac', 'Salon_DortSutun', 'Salon_Ziyafet', 'Salon_Pahli',
         'Salon_Koridorlu', 'Mahzen_Sutunlu', 'Mahzen_Halka', 'Sunak_Kucuk', 'Sunak_Hac', 'Taht_Arena', 'Taht_Sekizgen'],
        ['Giris_Avlu', 'Giris_Hac', 'Hucre_Islak', 'Hucre_Sutun', 'Hucre_Bolme', 'Hucre_Hac', 'Salon_DortSutun', 'Salon_Ziyafet',
         'Salon_Pahli', 'Salon_Koridorlu', 'Mahzen_Sutunlu', 'Mahzen_Halka', 'Mahzen_Genis', 'Sunak_Kucuk', 'Sunak_Hac', 'Taht_Sekizgen'],
    ]
    koridor = ['Gecit_Dikey_Kisa', 'Gecit_Dikey', 'Gecit_Dikey_Uzun', 'Gecit_Yatay_Kisa', 'Gecit_Yatay', 'Gecit_Yatay_Uzun']
    seviyeler = [s + koridor for s in seviyeler]
    ad = {o.ad: o for o in odalar}
    # benzetim
    srng = random.Random(7)
    for g in graflar:
        d = int(g[0]) - 1
        sab = [ad[n] for n in seviyeler[d]]
        deneme = [benzet(g.split(':')[1], sab, srng) for _ in range(30)]
        basarisiz = deneme.count(None)
        print(g.split(':')[0], 'başarısız', basarisiz, '/30', 'ort. deneme', sum(x for x in deneme if x) / max(1, 30 - basarisiz))
    if '--onizle' in sys.argv:
        for o in odalar: print('\n' + o.ad, o.tur, o.W, 'x', o.H); print(onizle(o))
    with open(sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith('--') else 'LdrSablonVerisi.cs', 'w', encoding='utf-8') as f:
        f.write('// Oda şablonları, harita grafikleri ve bölüm listeleri. Ekibin zindan üreticisiyle (Araclar/zindan.py) üretildi.\n')
        f.write('public static class LdrSablonVerisi\n{\n')
        f.write('    // ad|tür|alt_x,alt_y,üst_x,üst_y|kapılar(x,y,yön,kapatX0,kapatY0,kapatX1,kapatY1)|doğma noktaları|eşyalar(tür,x,y,ölçek)|hücreler(RLE, bit1 zemin,4 katı,8 harita)\n')
        f.write('    public static readonly string[] Odalar =\n    {\n')
        for o in odalar: f.write(f'        "{satir(o)}",\n')
        f.write('    };\n')
        f.write('    // bölüm_sıra: düğümler (tür + ebeveyn sırası). E giriş, C geçit, S küçük, M orta, L büyük, H sunak (sandık), B taht (çıkış)\n')
        f.write('    public static readonly string[] Graflar =\n    {\n')
        for g in graflar: f.write(f'        "{g}",\n')
        f.write('    };\n')
        f.write('    // derinlik (0..2): o derinlikte kullanılabilen şablonlar\n')
        f.write('    public static readonly string[][] Seviyeler =\n    {\n')
        for s in seviyeler: f.write('        new[] { ' + ', '.join(f'"{n}"' for n in s) + ' },\n')
        f.write('    };\n}\n')
