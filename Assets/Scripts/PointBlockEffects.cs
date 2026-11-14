using UnityEngine;

/// Spawns the particles, floating number and sound when a point block is scored.
/// Gained points: bright, everything moves up (flash, sparkle fountain, shockwave ring, glitter, chime).
/// Lost points: dark, everything moves down or inwards (red flash, smoke, falling embers,
/// collapsing ring, low buzz). Both burst the block into wood splinters.
public static class PointBlockEffects
{
    private struct Burst
    {
        public string name;
        public int count;
        public float lifetime;
        public float speedMin, speedMax;   // in block lengths per second
        public float size;                 // in block lengths
        public float gravity;
        public Color startColor, endColor;
        public AnimationCurve sizeOverLife;
        public bool stretched;
        public ParticleSystemShapeType shape;
        public float radius;               // in block lengths
        public Vector3 boxSize;            // world units, for Box shapes
        public float coneAngle;
        public Quaternion rotation;
    }

    private static AudioClip generatedChime;
    private static AudioClip generatedBuzz;

    /// center/size/rotation describe the block's box in world space.
    public static void Spawn(PointEffectSettings settings, int points, Vector3 center, Vector3 size,
        Quaternion rotation)
    {
        bool positive = points >= 0;
        float blockLength = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float scale = blockLength * settings.effectScale;

        if (settings.particleMaterial != null)
        {
            var root = new GameObject(positive ? "Point Gain VFX" : "Point Loss VFX");
            root.transform.position = center;
            CreateSplinters(root.transform, settings, size, rotation, scale);
            if (positive)
                CreatePositive(root.transform, settings, scale);
            else
                CreateNegative(root.transform, settings, scale);
            Object.Destroy(root, 3f);
        }
        else
        {
            Debug.LogWarning("PointBlockEffects: assign a particle material in PointEffectSettings.", settings);
        }

        if (settings.showNumber)
            FloatingScoreText.Create(center + Vector3.up * (0.45f * scale), points, scale, settings);

        if (settings.playSound)
            PlaySound(settings, positive, center);
    }

    private static void CreateSplinters(Transform root, PointEffectSettings s, Vector3 size,
        Quaternion rotation, float scale)
    {
        if (s.splinterCount <= 0)
            return;
        Color dark = s.woodColor * 0.7f;
        dark.a = 1f;
        Emit(root, s.particleMaterial, scale, new Burst
        {
            name = "Wood splinters", count = s.splinterCount, lifetime = 0.9f,
            speedMin = 0.25f, speedMax = 0.75f, size = 0.035f, gravity = 1f,
            startColor = s.woodColor, endColor = dark,
            sizeOverLife = AnimationCurve.Linear(0f, 1f, 1f, 0.3f), stretched = true,
            shape = ParticleSystemShapeType.Box, boxSize = size, rotation = rotation,
        });
    }

    private static void CreatePositive(Transform root, PointEffectSettings s, float scale)
    {
        Color clear = s.positiveColor;
        clear.a = 0f;
        Emit(root, s.particleMaterial, scale, new Burst
        {
            name = "Flash", count = 1, lifetime = 0.25f, size = 0.9f,
            startColor = s.positiveHighlight, endColor = s.positiveColor,
            sizeOverLife = AnimationCurve.Linear(0f, 0.4f, 1f, 1.5f),
            shape = ParticleSystemShapeType.Sphere, radius = 0.01f,
        });
        Emit(root, s.particleMaterial, scale, new Burst
        {
            name = "Sparkle fountain", count = 40, lifetime = 1.1f,
            speedMin = 1.2f, speedMax = 2.2f, size = 0.03f, gravity = 0.7f,
            startColor = s.positiveHighlight, endColor = s.positiveColor,
            sizeOverLife = AnimationCurve.Linear(0f, 1f, 1f, 0.2f), stretched = true,
            shape = ParticleSystemShapeType.Cone, coneAngle = 28f, radius = 0.12f,
            rotation = Quaternion.Euler(-90f, 0f, 0f), // cone points up
        });
        Emit(root, s.particleMaterial, scale, new Burst
        {
            name = "Shockwave ring", count = 36, lifetime = 0.5f,
            speedMin = 1.7f, speedMax = 1.9f, size = 0.07f,
            startColor = s.positiveHighlight, endColor = clear,
            sizeOverLife = AnimationCurve.Linear(0f, 1f, 1f, 0.4f),
            shape = ParticleSystemShapeType.Circle, radius = 0.05f,
            rotation = Quaternion.Euler(90f, 0f, 0f), // horizontal
        });
        Emit(root, s.particleMaterial, scale, new Burst
        {
            name = "Rising glitter", count = 18, lifetime = 1.7f,
            speedMin = 0.05f, speedMax = 0.2f, size = 0.04f, gravity = -0.04f,
            startColor = s.positiveHighlight, endColor = s.positiveColor,
            sizeOverLife = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f),
                new Keyframe(0.6f, 0.6f), new Keyframe(1f, 0f)),
            shape = ParticleSystemShapeType.Sphere, radius = 0.4f,
        });
    }

    private static void CreateNegative(Transform root, PointEffectSettings s, float scale)
    {
        Color darkRed = s.negativeColor * 0.35f;
        darkRed.a = 1f;
        Color clearSmoke = s.negativeSmoke;
        clearSmoke.a = 0f;
        Emit(root, s.particleMaterial, scale, new Burst
        {
            name = "Flash", count = 1, lifetime = 0.2f, size = 0.7f,
            startColor = s.negativeColor, endColor = darkRed,
            sizeOverLife = AnimationCurve.Linear(0f, 0.5f, 1f, 1.2f),
            shape = ParticleSystemShapeType.Sphere, radius = 0.01f,
        });
        Emit(root, s.particleMaterial, scale, new Burst
        {
            name = "Smoke", count = 14, lifetime = 1.3f,
            speedMin = 0.1f, speedMax = 0.3f, size = 0.32f, gravity = -0.02f,
            startColor = s.negativeSmoke, endColor = clearSmoke,
            sizeOverLife = AnimationCurve.Linear(0f, 0.5f, 1f, 1.8f),
            shape = ParticleSystemShapeType.Sphere, radius = 0.25f,
        });
        Emit(root, s.particleMaterial, scale, new Burst
        {
            name = "Falling embers", count = 30, lifetime = 1f,
            speedMin = 0.4f, speedMax = 1f, size = 0.03f, gravity = 1.6f,
            startColor = s.negativeColor, endColor = darkRed,
            sizeOverLife = AnimationCurve.Linear(0f, 1f, 1f, 0.3f), stretched = true,
            shape = ParticleSystemShapeType.Hemisphere, radius = 0.2f,
            rotation = Quaternion.Euler(90f, 0f, 0f), // hemisphere opens downwards
        });
        Emit(root, s.particleMaterial, scale, new Burst
        {
            // Starts wide and is pulled into the centre: the points get "sucked away".
            name = "Collapsing ring", count = 30, lifetime = 0.45f,
            speedMin = -1.7f, speedMax = -1.7f, size = 0.06f,
            startColor = darkRed, endColor = s.negativeColor,
            sizeOverLife = AnimationCurve.Linear(0f, 0.6f, 1f, 1f),
            shape = ParticleSystemShapeType.Circle, radius = 0.8f,
            rotation = Quaternion.Euler(90f, 0f, 0f),
        });
    }

    private static void Emit(Transform parent, Material material, float scale, Burst b)
    {
        var go = new GameObject(b.name);
        go.transform.SetParent(parent, false);
        // An unset Quaternion is all zeros (not identity), and Quaternion == can't detect it.
        bool hasRotation = b.rotation.x != 0f || b.rotation.y != 0f || b.rotation.z != 0f || b.rotation.w != 0f;
        go.transform.rotation = hasRotation ? b.rotation : Quaternion.identity;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = b.lifetime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(b.lifetime * 0.7f, b.lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(b.speedMin * scale, b.speedMax * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(b.size * 0.7f * scale, b.size * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = b.gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(1, b.count);

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = b.shape;
        if (b.shape == ParticleSystemShapeType.Box)
        {
            shape.scale = b.boxSize;
            shape.randomDirectionAmount = 1f; // a box would otherwise shoot everything along its Z axis
        }
        else
        {
            shape.radius = Mathf.Max(0.0001f, b.radius * scale);
            shape.angle = b.coneAngle;
        }

        var colors = ps.colorOverLifetime;
        colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(b.startColor, 0f), new GradientColorKey(b.endColor, 1f) },
            new[] {
                new GradientAlphaKey(b.startColor.a, 0f),
                new GradientAlphaKey(Mathf.Lerp(b.startColor.a, b.endColor.a, 0.4f), 0.6f),
                new GradientAlphaKey(0f, 1f),
            });
        colors.color = gradient;

        var sizes = ps.sizeOverLifetime;
        sizes.enabled = true;
        sizes.size = new ParticleSystem.MinMaxCurve(1f, b.sizeOverLife);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 1f;
        if (b.stretched)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f; // streak length = 0.06 s of travel, scale independent
            renderer.lengthScale = 1.5f;
        }
        else
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        ps.Play();
        ps.Emit(b.count);
    }

    private static void PlaySound(PointEffectSettings s, bool positive, Vector3 position)
    {
        // Unity's == null (not ??=) also catches clips destroyed between play sessions.
        AudioClip clip;
        if (positive)
        {
            if (generatedChime == null)
                generatedChime = MakeChime();
            clip = s.positiveClip != null ? s.positiveClip : generatedChime;
        }
        else
        {
            if (generatedBuzz == null)
                generatedBuzz = MakeBuzz();
            clip = s.negativeClip != null ? s.negativeClip : generatedBuzz;
        }

        var go = new GameObject(positive ? "Point Gain Sound" : "Point Loss Sound");
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = s.volume;
        source.spatialBlend = 1f;
        source.minDistance = s.minDistance;
        source.maxDistance = Mathf.Max(s.minDistance + 0.1f, s.maxDistance);
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.pitch = Random.Range(0.96f, 1.04f);
        source.Play();
        Object.Destroy(go, clip.length / source.pitch + 0.1f);
    }

    private const int SampleRate = 44100;

    // Two bright bell notes going up (C6 -> G6).
    private static AudioClip MakeChime()
    {
        float length = 0.9f;
        var data = new float[(int)(SampleRate * length)];
        AddBell(data, 1046.5f, 0f);
        AddBell(data, 1568f, 0.09f);
        Normalize(data, 0.8f);
        var clip = AudioClip.Create("PointGainChime", data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static void AddBell(float[] data, float frequency, float startTime)
    {
        int start = (int)(startTime * SampleRate);
        for (int i = start; i < data.Length; i++)
        {
            float t = (i - start) / (float)SampleRate;
            float envelope = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 6f);
            float w = 2f * Mathf.PI * frequency * t;
            data[i] += envelope * (Mathf.Sin(w) + 0.35f * Mathf.Sin(2f * w) + 0.12f * Mathf.Sin(3.01f * w));
        }
    }

    // Short descending, slightly gritty "wah" (330 Hz falling towards ~110 Hz).
    private static AudioClip MakeBuzz()
    {
        float length = 0.55f;
        var data = new float[(int)(SampleRate * length)];
        float phase = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float frequency = 110f + 220f * Mathf.Exp(-t * 5f);
            phase += 2f * Mathf.PI * frequency / SampleRate;
            float envelope = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 4.5f);
            float tone = (float)System.Math.Tanh(2.5f * Mathf.Sin(phase)) + 0.3f * Mathf.Sin(0.5f * phase);
            data[i] = envelope * tone;
        }
        Normalize(data, 0.7f);
        var clip = AudioClip.Create("PointLossBuzz", data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static void Normalize(float[] data, float peak)
    {
        float max = 0f;
        foreach (float v in data)
            max = Mathf.Max(max, Mathf.Abs(v));
        if (max <= 0f)
            return;
        float gain = peak / max;
        for (int i = 0; i < data.Length; i++)
            data[i] *= gain;
    }
}
