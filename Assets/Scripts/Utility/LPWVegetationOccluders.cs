using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sends the occluders (e.g. the Jenga blocks) as oriented boxes to the "Level Space" Low Poly Wind shaders.
/// Plant pixels that cover a block on screen (or stick into it) are discarded, with a dithered soft edge,
/// so the blocks always stay visible behind the vegetation.
/// Blocks that are hidden behind other geometry (checked with raycasts) do not clear any vegetation.
/// Only one active instance is supported (the values are global shader properties).
/// </summary>
public class LPWVegetationOccluders : MonoBehaviour
{
    private const int MaxOccluders = 64; // must match the array size in the shader

    private static readonly int SphereId = Shader.PropertyToID("_LPWOccluderSphere");
    private static readonly int Row0Id = Shader.PropertyToID("_LPWOccluderRow0");
    private static readonly int Row1Id = Shader.PropertyToID("_LPWOccluderRow1");
    private static readonly int Row2Id = Shader.PropertyToID("_LPWOccluderRow2");
    private static readonly int BoxId = Shader.PropertyToID("_LPWOccluderBox");
    private static readonly int VisibilityId = Shader.PropertyToID("_LPWOccluderVisibility");
    private static readonly int CountId = Shader.PropertyToID("_LPWOccluderCount");
    private static readonly int EnabledId = Shader.PropertyToID("_LPWOccluderEnabled");
    private static readonly int ViewerId = Shader.PropertyToID("_LPWViewerPosition");
    private static readonly int ShadowsId = Shader.PropertyToID("_LPWOccluderShadows");

    // Sample points for the visibility raycasts: center first, then the 8 corners (in half-extent units).
    private static readonly Vector3[] VisibilitySamples =
    {
        Vector3.zero,
        new(1f, 1f, 1f), new(-1f, 1f, 1f), new(1f, -1f, 1f), new(-1f, -1f, 1f),
        new(1f, 1f, -1f), new(-1f, 1f, -1f), new(1f, -1f, -1f), new(-1f, -1f, -1f)
    };

    private struct OccluderBox
    {
        public Vector4 Sphere;
        public Vector4 Row0;
        public Vector4 Row1;
        public Vector4 Row2;
        public Vector4 Box;
        public Vector4 Visibility;
    }

    [Header("Occluders")]
    [Tooltip("Renderers on these layers keep the vegetation in front of them hidden.")]
    [SerializeField] private LayerMask occluderLayers;
    [Tooltip("Only search below this transform (e.g. 'Generated Blocks'). Empty = whole scene.")]
    [SerializeField] private Transform searchRoot;
    [Tooltip("How often (seconds) to look for new or removed occluders.")]
    [SerializeField, Min(0.1f)] private float refreshInterval = 1f;

    [Header("Viewer")]
    [Tooltip("Viewer for the visibility and shadow checks. Empty = Camera.main (the XR center eye).")]
    [SerializeField] private Transform viewer;

    [Header("Visibility")]
    [Tooltip("Only blocks that the viewer can actually see clear the vegetation in front of them.")]
    [SerializeField] private bool requireVisibility = true;
    [Tooltip("Colliders on these layers can hide a block from the viewer (terrain, island, other blocks...).")]
    [SerializeField] private LayerMask visibilityBlockers;
    [Tooltip("How fast (per second) a block fades in/out when it becomes visible or hidden. 0 = instantly.")]
    [SerializeField, Min(0f)] private float visibilityFadeSpeed = 6f;
    [Tooltip("Draw the visibility rays in the Scene view (green = block visible, red = blocked).")]
    [SerializeField] private bool debugRays;

    [Header("Shadows")]
    [Tooltip("Also remove the shadows of plant parts that are hidden in front of a block.")]
    [SerializeField] private bool occludeShadows = true;

    [Header("Shape")]
    [Tooltip("Grows (or shrinks, negative) every box by this many world units on each side.")]
    [SerializeField] private float padding;
    [Tooltip("Width of the dithered soft edge around each block, relative to the block's average half size " +
             "(1 = one half block). 0 = hard edge.")]
    [SerializeField, Range(0f, 3f)] private float softness = 0.5f;

    private readonly List<Renderer> occluders = new();
    private readonly List<OccluderBox> boxes = new();
    private readonly Dictionary<Renderer, float> visibilityByOccluder = new();
    private readonly Vector4[] sphereData = new Vector4[MaxOccluders];
    private readonly Vector4[] row0 = new Vector4[MaxOccluders];
    private readonly Vector4[] row1 = new Vector4[MaxOccluders];
    private readonly Vector4[] row2 = new Vector4[MaxOccluders];
    private readonly Vector4[] boxData = new Vector4[MaxOccluders];
    private readonly Vector4[] visibilityData = new Vector4[MaxOccluders];
    private float nextRefreshTime;

    private void Reset()
    {
        occluderLayers = LayerMask.GetMask("Jenga");
        visibilityBlockers = LayerMask.GetMask("Level", "Collider", "Jenga");
    }

    private void OnEnable()
    {
        // Components added before this field existed have an empty mask, which would make every block "visible".
        if (visibilityBlockers.value == 0)
        {
            visibilityBlockers = LayerMask.GetMask("Level", "Collider", "Jenga");
            Debug.LogWarning($"{name}: 'Visibility Blockers' was empty, using Level, Collider and Jenga.", this);
        }

        RefreshOccluders();
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(EnabledId, 0f);
        Shader.SetGlobalFloat(ShadowsId, 0f);
    }

    private void LateUpdate()
    {
        if (Time.time >= nextRefreshTime)
            RefreshOccluders();

        Transform viewerTransform = viewer;
        if (viewerTransform == null && Camera.main != null)
            viewerTransform = Camera.main.transform;

        // The level is moved via transforms (OrbitAroundTarget) and auto sync is off,
        // so bring the colliders up to date before raycasting against them.
        if (requireVisibility)
            Physics.SyncTransforms();

        CollectBoxes(viewerTransform);

        int count = Mathf.Min(boxes.Count, MaxOccluders);
        for (int i = 0; i < count; i++)
        {
            sphereData[i] = boxes[i].Sphere;
            row0[i] = boxes[i].Row0;
            row1[i] = boxes[i].Row1;
            row2[i] = boxes[i].Row2;
            boxData[i] = boxes[i].Box;
            visibilityData[i] = boxes[i].Visibility;
        }

        Shader.SetGlobalVectorArray(SphereId, sphereData);
        Shader.SetGlobalVectorArray(Row0Id, row0);
        Shader.SetGlobalVectorArray(Row1Id, row1);
        Shader.SetGlobalVectorArray(Row2Id, row2);
        Shader.SetGlobalVectorArray(BoxId, boxData);
        Shader.SetGlobalVectorArray(VisibilityId, visibilityData);
        Shader.SetGlobalFloat(CountId, count);
        Shader.SetGlobalFloat(EnabledId, 1f);

        bool shadowsActive = occludeShadows && viewerTransform != null;
        Shader.SetGlobalFloat(ShadowsId, shadowsActive ? 1f : 0f);
        if (shadowsActive)
            Shader.SetGlobalVector(ViewerId, viewerTransform.position);
    }

    private void CollectBoxes(Transform viewerTransform)
    {
        boxes.Clear();

        foreach (Renderer occluder in occluders)
        {
            // Blocks can be destroyed, disabled or moved to another layer at runtime.
            if (occluder == null || !occluder.enabled || !occluder.gameObject.activeInHierarchy)
                continue;
            if ((occluderLayers.value & (1 << occluder.gameObject.layer)) == 0)
                continue;

            boxes.Add(CreateBox(occluder, viewerTransform));
        }

        // More occluders than the shader supports: keep the ones closest to the viewer.
        if (boxes.Count > MaxOccluders && viewerTransform != null)
        {
            Vector3 viewerPosition = viewerTransform.position;
            boxes.Sort((a, b) =>
                ((Vector3)a.Sphere - viewerPosition).sqrMagnitude.CompareTo(((Vector3)b.Sphere - viewerPosition).sqrMagnitude));
        }
    }

    private OccluderBox CreateBox(Renderer occluder, Transform viewerTransform)
    {
        // Oriented box from the renderer's local bounds (the mesh shape in its own space).
        Transform occluderTransform = occluder.transform;
        Bounds localBounds = occluder.localBounds;
        Vector3 lossyScale = occluderTransform.lossyScale;

        Vector3 center = occluderTransform.TransformPoint(localBounds.center);
        Quaternion rotation = occluderTransform.rotation;
        Vector3 meshHalfExtents = Vector3.Scale(localBounds.extents, new Vector3(
            Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));
        Vector3 halfExtents = Vector3.Max(meshHalfExtents + Vector3.one * padding, Vector3.one * 0.0001f);

        float softEdge = (halfExtents.x + halfExtents.y + halfExtents.z) / 3f * softness;
        float boundingRadius = halfExtents.magnitude + softEdge;

        // World -> box space without scale, so distances in the shader stay in world units.
        Matrix4x4 worldToBox = Matrix4x4.TRS(center, rotation, Vector3.one).inverse;

        float visibility = UpdateVisibility(occluder, viewerTransform, center, rotation, meshHalfExtents);

        return new OccluderBox
        {
            Sphere = new Vector4(center.x, center.y, center.z, boundingRadius),
            Row0 = worldToBox.GetRow(0),
            Row1 = worldToBox.GetRow(1),
            Row2 = worldToBox.GetRow(2),
            Box = new Vector4(halfExtents.x, halfExtents.y, halfExtents.z, softEdge),
            Visibility = new Vector4(visibility, 0f, 0f, 0f)
        };
    }

    private float UpdateVisibility(
        Renderer occluder,
        Transform viewerTransform,
        Vector3 center,
        Quaternion rotation,
        Vector3 halfExtents)
    {
        float target = !requireVisibility || viewerTransform == null ||
                       IsVisible(occluder.transform, viewerTransform.position, center, rotation, halfExtents)
            ? 1f
            : 0f;

        if (!visibilityByOccluder.TryGetValue(occluder, out float current))
            current = target;

        current = visibilityFadeSpeed <= 0f
            ? target
            : Mathf.MoveTowards(current, target, visibilityFadeSpeed * Time.deltaTime);

        visibilityByOccluder[occluder] = current;
        return current;
    }

    private bool IsVisible(
        Transform occluderTransform,
        Vector3 viewerPosition,
        Vector3 center,
        Quaternion rotation,
        Vector3 halfExtents)
    {
        // Slightly inside the box, so corner rays do not just graze past the block.
        Vector3 sampleExtents = halfExtents * 0.9f;

        foreach (Vector3 sample in VisibilitySamples)
        {
            Vector3 target = center + rotation * Vector3.Scale(sampleExtents, sample);
            Vector3 toTarget = target - viewerPosition;
            float distance = toTarget.magnitude;
            if (distance <= Mathf.Epsilon)
                return true;

            // Nothing in between, or the first thing hit is the block itself: this part of the block is visible.
            if (!Physics.Raycast(viewerPosition, toTarget / distance, out RaycastHit hit, distance,
                    visibilityBlockers, QueryTriggerInteraction.Ignore))
            {
                DrawDebugRay(viewerPosition, target, true);
                return true;
            }

            if (hit.transform == occluderTransform ||
                hit.transform.IsChildOf(occluderTransform) ||
                occluderTransform.IsChildOf(hit.transform))
            {
                DrawDebugRay(viewerPosition, target, true);
                return true;
            }

            DrawDebugRay(viewerPosition, hit.point, false);
        }

        return false;
    }

    private void DrawDebugRay(Vector3 from, Vector3 to, bool visible)
    {
        if (debugRays)
            Debug.DrawLine(from, to, visible ? Color.green : Color.red);
    }

    private void RefreshOccluders()
    {
        nextRefreshTime = Time.time + refreshInterval;
        occluders.Clear();

        Renderer[] renderers = searchRoot != null
            ? searchRoot.GetComponentsInChildren<Renderer>(true)
            : FindObjectsByType<Renderer>(FindObjectsInactive.Include);

        foreach (Renderer candidate in renderers)
        {
            if ((candidate is MeshRenderer or SkinnedMeshRenderer) &&
                (occluderLayers.value & (1 << candidate.gameObject.layer)) != 0)
            {
                occluders.Add(candidate);
            }
        }

        // Forget the visibility of occluders that no longer exist.
        List<Renderer> removed = null;
        foreach (Renderer known in visibilityByOccluder.Keys)
        {
            if (known == null || !occluders.Contains(known))
                (removed ??= new List<Renderer>()).Add(known);
        }

        if (removed != null)
        {
            foreach (Renderer known in removed)
                visibilityByOccluder.Remove(known);
        }
    }
}
