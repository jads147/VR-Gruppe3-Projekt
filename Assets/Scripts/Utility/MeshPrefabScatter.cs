using System;
using System.Collections.Generic;
using UnityEngine;

// AI GENERATED SCRIPT
public class MeshPrefabScatter : MonoBehaviour
{
    [Serializable]
    private class PrefabEntry
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
    }

    [Header("Source")]
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private List<PrefabEntry> prefabs = new();
    [SerializeField, Min(0)] private int amount = 100;
    [SerializeField] private int seed = 12345;

    [Header("Placement")]
    [SerializeField, Range(0f, 180f)] private float maximumNormalAngle = 180f;

    [Header("Height (World Space)")]
    [SerializeField] private bool limitHeight;
    [SerializeField] private float minimumHeight;
    [SerializeField] private float maximumHeight = 10f;

    [Header("Distance (World Space)")]
    [SerializeField] private bool limitDistance;
    [Tooltip("Center of the distance check. Uses this object's transform when empty.")]
    [SerializeField] private Transform distanceCenter;
    [SerializeField, Min(0f)] private float minimumDistance;
    [SerializeField, Min(0f)] private float maximumDistance = 25f;
    [Tooltip("Ignore the Y axis when measuring the distance (cylinder instead of sphere).")]
    [SerializeField] private bool horizontalDistanceOnly = true;

    [Header("Rotation")]
    [SerializeField] private bool alignToSurfaceNormal = true;
    [SerializeField] private Vector3 minimumRotation;
    [SerializeField] private Vector3 maximumRotation = new(0f, 360f, 0f);

    [Header("Scale Multiplier")]
    [SerializeField] private bool uniformScale = true;
    [SerializeField] private Vector2 uniformScaleRange = new(0.8f, 1.2f);
    [SerializeField] private Vector3 minimumScale = Vector3.one * 0.8f;
    [SerializeField] private Vector3 maximumScale = Vector3.one * 1.2f;

    [Header("Generation")]
    [SerializeField, Min(1)] private int attemptsPerObject = 20;
    [SerializeField] private Transform generatedParent;

    public Transform GeneratedParent => generatedParent;
    public bool LimitDistance => limitDistance;
    public bool HorizontalDistanceOnly => horizontalDistanceOnly;
    public float MinimumDistance => minimumDistance;
    public float MaximumDistance => maximumDistance;
    public Vector3 DistanceCenterPosition => distanceCenter != null ? distanceCenter.position : transform.position;

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (!TryGetMeshData(out Mesh mesh, out Vector3[] vertices, out int[] triangles))
            return;

        List<PrefabEntry> validPrefabs = prefabs.FindAll(entry =>
            entry != null && entry.prefab != null && entry.weight > 0f);
        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning($"{name}: Add at least one prefab with a weight above zero.", this);
            return;
        }

        float totalPrefabWeight = 0f;
        foreach (PrefabEntry entry in validPrefabs)
            totalPrefabWeight += entry.weight;

        Clear();
        generatedParent = new GameObject($"{name} - Generated").transform;
        generatedParent.SetParent(transform, false);

        Transform meshTransform = meshFilter.transform;
        Vector3 center = DistanceCenterPosition;
        float[] cumulativeAreas = BuildCumulativeTriangleAreas(vertices, triangles, meshTransform, center);
        float totalArea = cumulativeAreas[^1];

        if (totalArea <= Mathf.Epsilon)
        {
            Debug.LogWarning(
                limitDistance
                    ? $"{name}: No part of the mesh lies between the minimum and maximum distance."
                    : $"{name}: The selected mesh has no usable triangle area.",
                this);
            Clear();
            return;
        }

        System.Random random = new(seed);
        int placed = 0;
        int maxAttempts = amount * attemptsPerObject;

        for (int attempt = 0; attempt < maxAttempts && placed < amount; attempt++)
        {
            int triangleIndex = PickTriangle(cumulativeAreas, totalArea, random);
            int vertexIndex = triangleIndex * 3;

            Vector3 a = meshTransform.TransformPoint(vertices[triangles[vertexIndex]]);
            Vector3 b = meshTransform.TransformPoint(vertices[triangles[vertexIndex + 1]]);
            Vector3 c = meshTransform.TransformPoint(vertices[triangles[vertexIndex + 2]]);
            Vector3 position = RandomPointInTriangle(a, b, c, random);

            if (limitHeight && (position.y < minimumHeight || position.y > maximumHeight))
                continue;

            if (limitDistance)
            {
                float distance = GetDistance(position, center);
                if (distance < minimumDistance || distance > maximumDistance)
                    continue;
            }

            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Angle(normal, Vector3.up) > maximumNormalAngle)
                continue;

            Quaternion surfaceRotation = alignToSurfaceNormal
                ? Quaternion.FromToRotation(Vector3.up, normal)
                : Quaternion.identity;
            Quaternion randomRotation = Quaternion.Euler(RandomVector(minimumRotation, maximumRotation, random));

            GameObject prefab = PickPrefab(validPrefabs, totalPrefabWeight, random);
            GameObject instance = Instantiate(prefab, position, surfaceRotation * randomRotation, generatedParent);
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, GetScale(random));
            placed++;
        }

        if (placed < amount)
        {
            Debug.LogWarning(
                $"{name}: Placed {placed} of {amount} objects. Check the height range, distance range and maximum normal angle.",
                this);
        }
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        if (generatedParent == null)
            return;

        if (Application.isPlaying)
            Destroy(generatedParent.gameObject);
        else
            DestroyImmediate(generatedParent.gameObject);

        generatedParent = null;
    }

    private bool TryGetMeshData(out Mesh mesh, out Vector3[] vertices, out int[] triangles)
    {
        mesh = null;
        vertices = null;
        triangles = null;

        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning($"{name}: Assign a MeshFilter with a mesh.", this);
            return false;
        }

        mesh = meshFilter.sharedMesh;

        try
        {
            vertices = mesh.vertices;
            triangles = mesh.triangles;
        }
        catch (UnityException)
        {
            Debug.LogError(
                $"{name}: Mesh '{mesh.name}' is not readable. Enable Read/Write in the model import settings.",
                this);
            return false;
        }

        if (triangles.Length < 3)
        {
            Debug.LogWarning($"{name}: The selected mesh contains no triangles.", this);
            return false;
        }

        return true;
    }

    private float[] BuildCumulativeTriangleAreas(
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangles,
        Transform meshTransform,
        Vector3 center)
    {
        int triangleCount = triangles.Count / 3;
        float[] cumulativeAreas = new float[triangleCount];
        float totalArea = 0f;

        for (int i = 0; i < triangleCount; i++)
        {
            int index = i * 3;
            Vector3 a = meshTransform.TransformPoint(vertices[triangles[index]]);
            Vector3 b = meshTransform.TransformPoint(vertices[triangles[index + 1]]);
            Vector3 c = meshTransform.TransformPoint(vertices[triangles[index + 2]]);

            // Skip triangles that lie completely outside the distance range,
            // so the attempts are not wasted on unreachable areas.
            if (!limitDistance || TriangleOverlapsDistanceRange(a, b, c, center))
                totalArea += Vector3.Cross(b - a, c - a).magnitude * 0.5f;

            cumulativeAreas[i] = totalArea;
        }

        return cumulativeAreas;
    }

    private bool TriangleOverlapsDistanceRange(Vector3 a, Vector3 b, Vector3 c, Vector3 center)
    {
        // Completely inside the minimum distance: the farthest point of a triangle is always a corner.
        float farthestDistance = Mathf.Max(
            GetDistance(a, center),
            Mathf.Max(GetDistance(b, center), GetDistance(c, center)));
        if (farthestDistance < minimumDistance)
            return false;

        // Completely outside the maximum distance (conservative check via the triangle bounds).
        Bounds bounds = new(a, Vector3.zero);
        bounds.Encapsulate(b);
        bounds.Encapsulate(c);

        return GetDistance(bounds.ClosestPoint(center), center) <= maximumDistance;
    }

    private float GetDistance(Vector3 position, Vector3 center)
    {
        Vector3 offset = position - center;
        if (horizontalDistanceOnly)
            offset.y = 0f;

        return offset.magnitude;
    }

    private static int PickTriangle(IReadOnlyList<float> cumulativeAreas, float totalArea, System.Random random)
    {
        float targetArea = (float)random.NextDouble() * totalArea;
        int low = 0;
        int high = cumulativeAreas.Count - 1;

        while (low < high)
        {
            int middle = (low + high) / 2;
            if (targetArea <= cumulativeAreas[middle])
                high = middle;
            else
                low = middle + 1;
        }

        return low;
    }

    private static Vector3 RandomPointInTriangle(Vector3 a, Vector3 b, Vector3 c, System.Random random)
    {
        float u = (float)random.NextDouble();
        float v = (float)random.NextDouble();

        if (u + v > 1f)
        {
            u = 1f - u;
            v = 1f - v;
        }

        return a + u * (b - a) + v * (c - a);
    }

    private static GameObject PickPrefab(
        IReadOnlyList<PrefabEntry> entries,
        float totalWeight,
        System.Random random)
    {
        float targetWeight = (float)random.NextDouble() * totalWeight;

        foreach (PrefabEntry entry in entries)
        {
            targetWeight -= entry.weight;
            if (targetWeight <= 0f)
                return entry.prefab;
        }

        return entries[^1].prefab;
    }

    private Vector3 GetScale(System.Random random)
    {
        if (uniformScale)
        {
            float scale = RandomRange(uniformScaleRange.x, uniformScaleRange.y, random);
            return Vector3.one * scale;
        }

        return RandomVector(minimumScale, maximumScale, random);
    }

    private static Vector3 RandomVector(Vector3 minimum, Vector3 maximum, System.Random random)
    {
        return new Vector3(
            RandomRange(minimum.x, maximum.x, random),
            RandomRange(minimum.y, maximum.y, random),
            RandomRange(minimum.z, maximum.z, random));
    }

    private static float RandomRange(float minimum, float maximum, System.Random random)
    {
        if (minimum > maximum)
            (minimum, maximum) = (maximum, minimum);

        return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }

    private void OnValidate()
    {
        amount = Mathf.Max(0, amount);
        attemptsPerObject = Mathf.Max(1, attemptsPerObject);
        maximumDistance = Mathf.Max(0f, maximumDistance);
        minimumDistance = Mathf.Clamp(minimumDistance, 0f, maximumDistance);

        if (minimumHeight > maximumHeight)
            (minimumHeight, maximumHeight) = (maximumHeight, minimumHeight);

        if (uniformScaleRange.x > uniformScaleRange.y)
            uniformScaleRange = new Vector2(uniformScaleRange.y, uniformScaleRange.x);
    }
}
