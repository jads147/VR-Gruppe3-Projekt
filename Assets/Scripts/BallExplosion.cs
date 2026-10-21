using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class BallExplosion : MonoBehaviour
{
    [Header("Collision trigger")]
    [Tooltip("Only collisions with these collider layers trigger the explosion. Other collisions leave the ball armed.")]
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Header("Physics")]
    [SerializeField, Min(0.01f)] private float radius = 2.5f;
    [SerializeField, Min(0f)] private float force = 12f;
    [SerializeField, Min(0f)] private float upwardsModifier = 0.4f;
    [SerializeField] private LayerMask affectedLayers = ~0;
    [Tooltip("Impulse respects mass. VelocityChange gives all bodies the same kick.")]
    [SerializeField] private ForceMode forceMode = ForceMode.Impulse;

    [Header("Procedural particles")]
    [SerializeField] private Material particleMaterial;
    [SerializeField, Min(0.01f)] private float effectScale = 1f;
    [SerializeField, Range(8, 128)] private int sparkCount = 40;
    [SerializeField] private Color hotColor = new Color(1f, 0.85f, 0.25f);
    [SerializeField] private Color fireColor = new Color(1f, 0.22f, 0.04f);
    [SerializeField] private Color smokeColor = new Color(0.22f, 0.14f, 0.28f, 0.65f);

    private bool exploded;

    private void OnCollisionEnter(Collision collision)
    {
        if (exploded || (collisionLayers.value & (1 << collision.collider.gameObject.layer)) == 0)
            return;

        // Set before applying forces: multiple contacts must never trigger twice.
        exploded = true;
        Vector3 center = transform.position;
        Rigidbody ownBody = GetComponent<Rigidbody>();
        var affectedBodies = new HashSet<Rigidbody>();

        foreach (Collider hit in Physics.OverlapSphere(center, radius, affectedLayers,
                     QueryTriggerInteraction.Ignore))
        {
            Rigidbody body = hit.attachedRigidbody;
            if (body == null || body == ownBody || body.isKinematic || !affectedBodies.Add(body))
                continue;

            body.AddExplosionForce(force, center, radius, upwardsModifier, forceMode);
        }

        Vector3 effectPosition = collision.contactCount > 0
            ? collision.GetContact(0).point + collision.GetContact(0).normal * 0.05f
            : center;
        if (particleMaterial != null)
            CreateEffect(effectPosition);
        else
            Debug.LogWarning("BallExplosion: Assign the StylizedExplosion material to see particles.", this);

        // The detached effect survives the ball and cleans itself up independently.
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void CreateEffect(Vector3 position)
    {
        var root = new GameObject("Ball Explosion VFX");
        root.transform.position = position;

        CreateBurst(root.transform, "Flash", 1, 0.18f, 0f, 0.65f, 0f,
            hotColor, fireColor, AnimationCurve.Linear(0f, 0.4f, 1f, 2.2f));
        CreateBurst(root.transform, "Sparks", sparkCount, 0.65f, 4.5f, 0.13f, 0.5f,
            hotColor, fireColor, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        CreateBurst(root.transform, "Smoke puffs", 12, 1.1f, 0.8f, 0.4f, -0.08f,
            smokeColor, smokeColor, AnimationCurve.Linear(0f, 0.3f, 1f, 2f));

        Destroy(root, 2f);
    }

    private void CreateBurst(Transform parent, string effectName, int count, float lifetime,
        float speed, float size, float gravity, Color startColor, Color endColor,
        AnimationCurve sizeCurve)
    {
        var effect = new GameObject(effectName);
        effect.transform.SetParent(parent, false);
        var particles = effect.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = lifetime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f * effectScale, speed * effectScale);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f * effectScale, size * effectScale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;

        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f * effectScale;

        var colors = particles.colorOverLifetime;
        colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 0.6f) },
            new[] {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(startColor.a * 0.8f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
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

        particles.Play();
        particles.Emit(count);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
