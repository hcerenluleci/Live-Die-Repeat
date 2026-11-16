using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// =====================================================================================
// GİRDİ: oyunun geri kalanı klavye/fareyi doğrudan okumaz, buradan alır (WASD, SPACE, R, E, F, T, M, B, ESC, fare).
// Böylece tuş değişikliği ya da yeni girdi sistemi tek dosyayı etkiler.
// =====================================================================================
/// <summary>
/// Girdi katmanı. Oyunun geri kalanı klavye/fareyi doğrudan okumaz, buradan alır;
/// böylece eski ve yeni girdi sistemi arasında geçmek tek dosyayı ilgilendirir.
/// </summary>
public static class LdrGiris
{
#if UNITY_EDITOR
    // Yalnızca editörde: fragman kayıt aracı (LdrFragman) oyunu kendi oynatırken girdiyi buradan verir.
    // Normal oyunda hepsi null kalır ve hiçbir etkisi olmaz.
    public static System.Func<Vector2> PilotYon;
    public static System.Func<bool> PilotAtes, PilotTakla, PilotEtkilesim;
    public static System.Func<Vector2?> PilotHedef;   // nişan alınan dünya noktası
#endif

    /// <summary>WASD / ok tuşları. Uzunluğu en fazla 1.</summary>
    public static Vector2 Yon()
    {
#if UNITY_EDITOR
        if (PilotYon != null) return PilotYon();
#endif
        Vector2 v;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = Keyboard.current;
        if (k == null) return Vector2.zero;
        v = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                        (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
#else
        v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        return Vector2.ClampMagnitude(v, 1f);
    }

    public static bool TaklaBasildi()
    {
#if UNITY_EDITOR
        if (PilotTakla != null) return PilotTakla();
#endif
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    public static bool DoldurBasildi()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    public static bool EtkilesimBasildi()
    {
#if UNITY_EDITOR
        if (PilotEtkilesim != null) return PilotEtkilesim();
#endif
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    /// <summary>ESC: duraklatma menüsünü açar / kapatır.</summary>
    public static bool DuraklatBasildi()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    /// <summary>M: mini haritayı açar / kapatır.</summary>
    public static bool HaritaBasildi()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.M);
#endif
    }

    /// <summary>B: ışık seviyesini değiştirir (aydınlık / orta / karanlık).</summary>
    public static bool IsikBasildi()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.B);
#endif
    }

    public static bool IsinlanBasildi()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.T);
#endif
    }

    /// <summary>F1..F4 deneme tuşları. Yalnızca Unity editöründe kullanılır (LdrOyun içinde #if UNITY_EDITOR).</summary>
    public static bool TestTusu(int n)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = Keyboard.current;
        if (k == null) return false;
        switch (n)
        {
            case 1: return k.f1Key.wasPressedThisFrame;
            case 2: return k.f2Key.wasPressedThisFrame;
            case 3: return k.f3Key.wasPressedThisFrame;
            case 4: return k.f4Key.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.F1 + (n - 1));
#endif
    }

    /// <summary>Ateş topu fırlatma tuşu (F).</summary>
    public static bool AtesTopuBasildi()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F);
#endif
    }

    public static bool AtesBasili()
    {
#if UNITY_EDITOR
        if (PilotAtes != null) return PilotAtes();
#endif
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return (Mouse.current != null && Mouse.current.leftButton.isPressed) || (Keyboard.current != null && Keyboard.current.gKey.isPressed);
#else
#if UNITY_EDITOR
        return Input.GetMouseButton(0) || Input.GetKey(KeyCode.G);   // G: yalnızca editörde test için yedek ateş tuşu
#else
        return Input.GetMouseButton(0);
#endif
#endif
    }

    public static Vector2 FareEkran()
    {
#if UNITY_EDITOR
        if (PilotHedef != null)
        {
            var h = PilotHedef();
            var k = Camera.main;
            if (h.HasValue && k != null) { Vector3 e = k.WorldToScreenPoint(new Vector3(h.Value.x, h.Value.y, 0f)); return new Vector2(e.x, e.y); }
        }
#endif
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    /// <summary>Farenin dünya konumu (2D, ortografik kamera).</summary>
    public static Vector2 FareDunya(Camera kam)
    {
        if (kam == null) return Vector2.zero;
        Vector2 m = FareEkran();
        Vector3 w = kam.ScreenToWorldPoint(new Vector3(m.x, m.y, Mathf.Abs(kam.transform.position.z)));
        return new Vector2(w.x, w.y);
    }
}
