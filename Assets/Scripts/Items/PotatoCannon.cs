using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRBaseInteractable))]
public sealed class PotatoCannon : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] private Rigidbody projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float launchSpeed = 12f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private float projectileLifetime = 8f;

    [Header("Effects (optional)")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private AudioSource fireSound;

    [Header("Trajectory Preview")]
    [SerializeField] private bool showTrajectory = true;
    [SerializeField] private bool onlyWhileHeld = true;
    [SerializeField] private Transform impactMarker;
    [SerializeField] private int maxSteps = 60;
    [SerializeField] private float timeStep = 0.03f;
    [SerializeField] private LayerMask collisionMask = ~0;

    [Header("Trajectory Line")]
    [SerializeField] private Color lineColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private float lineWidth = 0.012f;
    [SerializeField] private float dashLength = 0.08f;
    [SerializeField] private float gapLength = 0.06f;
    [SerializeField] private float dashSpeed = 0.6f;
    [Tooltip("Optional. Leave empty to use URP Unlit. Must be a URP Unlit-style material with _BaseMap.")]
    [SerializeField] private Material lineMaterial;

    private XRBaseInteractable interactable;
    private Collider[] cannonColliders;
    private readonly List<Vector3> points = new List<Vector3>();
    private float nextFireTime;

    private LineRenderer trajectoryLine;
    private Material dashMaterial;
    private Texture2D dashTexture;
    private float dashOffset;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        cannonColliders = GetComponentsInChildren<Collider>();

        if (muzzle == null)
            Debug.LogError("PotatoCannon: Muzzle is not assigned.", this);
        if (projectilePrefab == null)
            Debug.LogError("PotatoCannon: Projectile prefab is not assigned.", this);

        CreateTrajectoryLine();
    }

    private void OnEnable()
    {
        interactable.activated.AddListener(OnActivated);
        Application.onBeforeRender += UpdatePreview;
    }

    private void OnDisable()
    {
        interactable.activated.RemoveListener(OnActivated);
        Application.onBeforeRender -= UpdatePreview;
        SetPreviewVisible(false);
    }

    private void OnDestroy()
    {
        if (dashMaterial != null)
            Destroy(dashMaterial);
        if (dashTexture != null)
            Destroy(dashTexture);
    }

    // Activate = trigger button while the cannon is selected (held).
    private void OnActivated(ActivateEventArgs args) => Fire();

    public void Fire()
    {
        if (Time.time < nextFireTime || projectilePrefab == null || muzzle == null)
            return;
        nextFireTime = Time.time + cooldown;

        Rigidbody projectile = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
        projectile.linearVelocity = muzzle.forward * launchSpeed;

        // The potato spawns inside the barrel, so it must not collide with the cannon itself.
        foreach (Collider projectileCollider in projectile.GetComponentsInChildren<Collider>())
            foreach (Collider cannonCollider in cannonColliders)
                Physics.IgnoreCollision(projectileCollider, cannonCollider);

        Destroy(projectile.gameObject, projectileLifetime);

        if (muzzleFlash != null)
            muzzleFlash.Play();
        if (fireSound != null)
            fireSound.Play();
    }

    // Runs right after XRI moves the held cannon to the final hand pose (also onBeforeRender),
    // so the line uses the muzzle pose that is actually rendered and doesn't trail behind.
    [BeforeRenderOrder(XRInteractionUpdateOrder.k_BeforeRenderOrder + 1)]
    private void UpdatePreview()
    {
        bool visible = showTrajectory && muzzle != null && (!onlyWhileHeld || interactable.isSelected);
        SetPreviewVisible(visible);
        if (!visible)
            return;

        bool hit = SimulateTrajectory(out Vector3 hitPoint, out Vector3 hitNormal);

        trajectoryLine.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            trajectoryLine.SetPosition(i, points[i]);
        AnimateDashes();

        if (impactMarker != null)
        {
            impactMarker.gameObject.SetActive(hit);
            if (hit)
                impactMarker.SetPositionAndRotation(hitPoint + hitNormal * 0.005f, Quaternion.LookRotation(hitNormal));
        }
    }

    // Steps the flight path with the same gravity and drag as the projectile and stops at the first hit.
    private bool SimulateTrajectory(out Vector3 hitPoint, out Vector3 hitNormal)
    {
        points.Clear();
        Vector3 position = muzzle.position;
        Vector3 velocity = muzzle.forward * launchSpeed;
        float drag = projectilePrefab != null ? projectilePrefab.linearDamping : 0f;
        points.Add(position);

        for (int i = 0; i < maxSteps; i++)
        {
            velocity += Physics.gravity * timeStep;
            velocity *= Mathf.Clamp01(1f - drag * timeStep);
            Vector3 next = position + velocity * timeStep;

            Vector3 segment = next - position;
            if (Physics.Raycast(position, segment.normalized, out RaycastHit hit, segment.magnitude, collisionMask, QueryTriggerInteraction.Ignore)
                && !IsCannonCollider(hit.collider))
            {
                points.Add(hit.point);
                hitPoint = hit.point;
                hitNormal = hit.normal;
                return true;
            }

            points.Add(next);
            position = next;
        }

        hitPoint = default;
        hitNormal = Vector3.up;
        return false;
    }

    private void CreateTrajectoryLine()
    {
        var lineObject = new GameObject("TrajectoryLine");
        lineObject.transform.SetParent(transform, false);

        trajectoryLine = lineObject.AddComponent<LineRenderer>();
        trajectoryLine.useWorldSpace = true;
        trajectoryLine.alignment = LineAlignment.View;
        trajectoryLine.textureMode = LineTextureMode.Tile;
        trajectoryLine.widthMultiplier = lineWidth;
        trajectoryLine.numCapVertices = 2;
        trajectoryLine.shadowCastingMode = ShadowCastingMode.Off;
        trajectoryLine.receiveShadows = false;
        trajectoryLine.startColor = trajectoryLine.endColor = Color.white;
        trajectoryLine.positionCount = 0;
        trajectoryLine.enabled = false;

        // One texture tile = one dash + one gap; the alpha cutoff turns the gaps into holes.
        const int width = 64;
        int dashPixels = Mathf.Clamp(Mathf.RoundToInt(width * dashLength / (dashLength + gapLength)), 1, width - 1);
        dashTexture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
        {
            name = "TrajectoryDashes",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point
        };
        for (int x = 0; x < width; x++)
            dashTexture.SetPixel(x, 0, x < dashPixels ? Color.white : Color.clear);
        dashTexture.Apply();

        if (lineMaterial != null)
        {
            dashMaterial = new Material(lineMaterial);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("PotatoCannon: URP Unlit shader not found. Assign a Line Material.", this);
                return;
            }
            dashMaterial = new Material(shader);
            dashMaterial.SetFloat("_AlphaClip", 1f);
            dashMaterial.SetFloat("_Cutoff", 0.5f);
            dashMaterial.EnableKeyword("_ALPHATEST_ON");
            dashMaterial.renderQueue = (int)RenderQueue.AlphaTest;
        }

        dashMaterial.name = "TrajectoryLine (Instance)";
        dashMaterial.mainTexture = dashTexture;
        dashMaterial.color = lineColor;
        dashMaterial.mainTextureScale = new Vector2(1f / (dashLength + gapLength), 1f);
        trajectoryLine.sharedMaterial = dashMaterial;
    }

    // Shifting the texture offset backwards makes the dashes travel from the muzzle towards the target.
    private void AnimateDashes()
    {
        if (dashMaterial == null)
            return;

        dashOffset = Mathf.Repeat(dashOffset - dashSpeed * Time.deltaTime / (dashLength + gapLength), 1f);
        dashMaterial.mainTextureOffset = new Vector2(dashOffset, 0f);
    }

    private bool IsCannonCollider(Collider other)
    {
        foreach (Collider own in cannonColliders)
            if (own == other)
                return true;
        return false;
    }

    private void SetPreviewVisible(bool visible)
    {
        if (trajectoryLine != null)
            trajectoryLine.enabled = visible;
        if (impactMarker != null && !visible)
            impactMarker.gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (muzzle == null)
            return;

        // Rough editor preview of the flight path (gravity only).
        Gizmos.color = Color.yellow;
        Vector3 position = muzzle.position;
        Vector3 velocity = muzzle.forward * launchSpeed;
        for (int i = 0; i < maxSteps; i++)
        {
            Vector3 next = position + velocity * timeStep + 0.5f * timeStep * timeStep * Physics.gravity;
            velocity += Physics.gravity * timeStep;
            Gizmos.DrawLine(position, next);
            position = next;
        }
    }
}
