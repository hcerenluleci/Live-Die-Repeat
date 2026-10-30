using UnityEngine;

/// <summary>
/// ÖZGÜN PARTICLE SYSTEM EFEKTLERİ (kodla üretilir, ek dosya gerekmez).
///  - Burst: tek seferlik patlama (düşman ölümü, vuruş kıvılcımı, kaçış parçası bulunması)
///  - Motes: sürekli süzülen ruh kıvılcımları (mühürlü sandık, kaçış kristali)
/// </summary>
public static class LdrVfx
{
    static Material mat;

    static Material Mat()
    {
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh != null) mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }
        return mat;
    }

    static void Setup(ParticleSystemRenderer r, int layerId, int order)
    {
        var m = Mat(); if (m != null) r.sharedMaterial = m;
        r.sortingLayerID = layerId; r.sortingOrder = order;
    }

    /// <summary>Tek seferlik patlama. gravity &gt; 0 aşağı çeker, &lt; 0 yukarı süzülür.</summary>
    public static void Burst(Vector3 pos, Color c1, Color c2, int count, float speed, float size, float life, float gravity, int layerId, int order)
    {
        var go = new GameObject("VfxBurst");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(count, 8);
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.15f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
        Setup(go.GetComponent<ParticleSystemRenderer>(), layerId, order);
        ps.Emit(count);
        Object.Destroy(go, life + 1.5f);
    }

    /// <summary>Sürekli süzülen kıvılcımlar; parent ile birlikte yok olur.</summary>
    public static ParticleSystem Motes(Transform parent, Color c, float rate, float radius, float rise, int layerId, int order)
    {
        var go = new GameObject("VfxMotes");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startColor = c;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 48;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(rise); vel.z = new ParticleSystem.MinMaxCurve(0f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.5f, 1f), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        Setup(go.GetComponent<ParticleSystemRenderer>(), layerId, order);
        return ps;
    }
}
