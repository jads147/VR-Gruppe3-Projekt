using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class BallImplosion : MonoBehaviour
{
    [Header("Collision trigger")]
    [Tooltip("Only collisions with these collider layers trigger the implosion.")]
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Header("Physics")]
    [SerializeField, Min(0.01f)] private float radius = 3f;
    [SerializeField, Min(0f)] private float force = 12f;
    [SerializeField] private LayerMask affectedLayers = ~0;
    [Tooltip("Impulse respects mass. VelocityChange gives all bodies the same kick.")]
    [SerializeField] private ForceMode forceMode = ForceMode.Impulse;

    [Header("Procedural particles")]
    [SerializeField] private Material particleMaterial;
    [SerializeField, Min(0.01f)] private float effectScale = 1f;
    [SerializeField, Min(0.05f)] private float effectDuration = 0.65f;
    [SerializeField, Range(8, 128)] private int sparkCount = 48;
    [SerializeField] private Color outerColor = new Color(0.45f, 0.15f, 1f);
    [SerializeField] private Color coreColor = new Color(0.25f, 0.9f, 1f);

    private bool imploded;

    private void OnCollisionEnter(Collision collision)
    {
        if (imploded || (collisionLayers.value & (1 << collision.collider.gameObject.layer)) == 0)
            return;

        imploded = true;
        Vector3 center = transform.position;
        Rigidbody ownBody = GetComponent<Rigidbody>();
        var affectedBodies = new HashSet<Rigidbody>();

        foreach (Collider hit in Physics.OverlapSphere(center, radius, affectedLayers,
                     QueryTriggerInteraction.Ignore))
        {
            Rigidbody body = hit.attachedRigidbody;
            if (body == null || body == ownBody || body.isKinematic || !affectedBodies.Add(body))
                continue;

            Vector3 toCenter = center - body.worldCenterOfMass;
            float distance = toCenter.magnitude;
            if (distance < 0.001f)
                continue;

            // One inward kick per body, fading out towards the edge of the radius.
            float falloff = 1f - Mathf.Clamp01(distance / radius);
            body.AddForce(toCenter / distance * force * falloff, forceMode);
        }

        if (particleMaterial != null)
            CreateEffect(center);
        else
            Debug.LogWarning("BallImplosion: Assign the StylizedExplosion material to see particles.", this);

        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void CreateEffect(Vector3 center)
    {
        var root = new GameObject("Ball Implosion VFX");
        root.transform.position = center;
        ParticleSystem sparks = CreateParticles(root.transform, "Inward sparks", sparkCount,
            0.12f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
        sparks.Play();
        for (int i = 0; i < sparkCount; i++)
        {
            Vector3 offset = Random.onUnitSphere * Random.Range(0.7f, 1.4f) * effectScale;
            // Every spark reaches the center at the end of its lifetime.
            var emission = new ParticleSystem.EmitParams
            {
                position = center + offset,
                velocity = -offset / effectDuration,
                startLifetime = effectDuration
            };
            sparks.Emit(emission, 1);
        }

        ParticleSystem core = CreateParticles(root.transform, "Collapsing core", 1,
            1.2f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.35f),
                new Keyframe(1f, 0f)));
        core.Play();
        core.Emit(1);
        Destroy(root, effectDuration + 0.25f);
    }

    private ParticleSystem CreateParticles(Transform parent, string effectName, int count,
        float size, AnimationCurve sizeCurve)
    {
        var effect = new GameObject(effectName);
        effect.transform.SetParent(parent, false);
        var particles = effect.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = effectDuration;
        main.startLifetime = effectDuration;
        main.startSpeed = 0f;
        main.startSize = size * effectScale;
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;

        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;

        var colors = particles.colorOverLifetime;
        colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(outerColor, 0f), new GradientColorKey(coreColor, 0.8f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        colors.color = gradient;
        var sizes = particles.sizeOverLifetime;
        sizes.enabled = true;
        sizes.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 1f;
        return particles;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.45f, 0.15f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
