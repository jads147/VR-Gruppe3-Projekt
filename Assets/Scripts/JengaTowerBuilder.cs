using UnityEngine;

public class JengaTowerBuilder : MonoBehaviour
{
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private GameObject pointCubePrefab;
    [SerializeField] private GameObject negativPointCubePrefab;
    [SerializeField, Range(0f, 1f)] private float pointCubeChance = 0.15f;
    [SerializeField, Range(0f, 1f)] private float negativPointCubeChance = 0.1f;
    [Tooltip("Bottom layers that only use the plain block (they touch the ground and would score instantly).")]
    [SerializeField, Min(0)] private int pointFreeBottomLayers = 1;
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int seed = 12345;
    [SerializeField] private Transform blocksParent;
    [SerializeField] private bool buildWhenPlayStarts = true;
    [SerializeField, Min(1)] private int layers = 18;
    [SerializeField, Min(0f)] private float gap = 0.001f;

    private void Start()
    {
        if (buildWhenPlayStarts)
            BuildTower();
    }

    public void BuildTower()
    {
        if (blockPrefab == null)
        {
            Debug.LogError("Assign a block prefab to the Jenga tower builder.", this);
            return;
        }

        ClearTower();

        if (blocksParent == null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                GameObject parentObject = new GameObject("Generated Blocks");
                UnityEditor.Undo.RegisterCreatedObjectUndo(parentObject, "Create Tower Blocks Parent");
                parentObject.transform.SetParent(transform, false);
                UnityEditor.Undo.RecordObject(this, "Assign Tower Blocks Parent");
                blocksParent = parentObject.transform;
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
            else
#endif
            {
                GameObject parentObject = new GameObject("Generated Blocks");
                parentObject.transform.SetParent(transform, false);
                blocksParent = parentObject.transform;
            }
        }

        if (!TryGetBlockBounds(out Bounds bounds) ||
            bounds.size.x <= 0f || bounds.size.y <= 0f || bounds.size.z <= 0f)
        {
            Debug.LogError("The block prefab needs a non-trigger Box Collider or a Renderer with non-zero bounds.", this);
            return;
        }

        // Normalize blocks whose long horizontal side is along Z instead of X.
        bool longSideAlongZ = bounds.size.z > bounds.size.x;
        float width = Mathf.Min(bounds.size.x, bounds.size.z);
        float spacing = width + Mathf.Max(0f, gap);
        System.Random random = new System.Random(randomSeed ? System.Environment.TickCount : seed);

        for (int layer = 0; layer < Mathf.Max(1, layers); layer++)
        {
            Quaternion layerRotation = Quaternion.Euler(0f, layer % 2 * 90f, 0f);
            Quaternion blockRotation = transform.rotation * layerRotation *
                Quaternion.Euler(0f, longSideAlongZ ? 90f : 0f, 0f);

            for (int block = 0; block < 3; block++)
            {
                GameObject prefab = PickPrefab(random, layer >= pointFreeBottomLayers);
                Vector3 center = new Vector3(
                    0f,
                    bounds.size.y * (layer + 0.5f),
                    (block - 1) * spacing
                );

                Vector3 worldCenter = transform.position +
                    transform.rotation * (layerRotation * center);

                // Place the bounds center correctly even when the pivot is offset.
                Vector3 blockPosition = worldCenter - blockRotation * bounds.center;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    GameObject instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab,
                        gameObject.scene);
                    if (instance != null)
                    {
                        instance.transform.SetParent(blocksParent, true);
                        UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Build Jenga Tower");
                        instance.transform.SetPositionAndRotation(blockPosition, blockRotation);
                    }
                    continue;
                }
#endif
                Instantiate(prefab, blockPosition, blockRotation, blocksParent);
            }
        }
    }

    public void ClearTower()
    {
        if (blocksParent == null)
            return;

        for (int i = blocksParent.childCount - 1; i >= 0; i--)
        {
            GameObject child = blocksParent.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.DestroyObjectImmediate(child);
                continue;
            }
#endif
            Destroy(child);
        }
    }

    private GameObject PickPrefab(System.Random random, bool allowPointCubes)
    {
        // Point cubes are assumed to have the same size as the block prefab.
        double roll = random.NextDouble();
        if (!allowPointCubes)
            return blockPrefab;
        if (roll < pointCubeChance && pointCubePrefab != null)
            return pointCubePrefab;
        if (roll < pointCubeChance + negativPointCubeChance && negativPointCubePrefab != null)
            return negativPointCubePrefab;
        return blockPrefab;
    }

    private bool TryGetBlockBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        foreach (BoxCollider box in blockPrefab.GetComponentsInChildren<BoxCollider>())
        {
            if (box.enabled && !box.isTrigger)
            {
                EncapsulateBounds(new Bounds(box.center, box.size), box.transform,
                    ref bounds, ref hasBounds);
            }
        }

        if (hasBounds)
            return true;

        foreach (Renderer modelRenderer in blockPrefab.GetComponentsInChildren<Renderer>())
        {
            if (modelRenderer.enabled)
            {
                EncapsulateBounds(modelRenderer.localBounds, modelRenderer.transform,
                    ref bounds, ref hasBounds);
            }
        }

        return hasBounds;
    }

    private void EncapsulateBounds(Bounds source, Transform sourceTransform,
        ref Bounds result, ref bool hasBounds)
    {
        // Child transforms are included; prefab position/rotation are ignored.
        // Apply root scale explicitly so prefab scale is preserved on spawning.
        Matrix4x4 toBlockSpace = Matrix4x4.Scale(blockPrefab.transform.localScale) *
            blockPrefab.transform.worldToLocalMatrix * sourceTransform.localToWorldMatrix;

        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = source.center + Vector3.Scale(source.extents,
                new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));

            point = toBlockSpace.MultiplyPoint3x4(point);
            if (hasBounds)
                result.Encapsulate(point);
            else
            {
                result = new Bounds(point, Vector3.zero);
                hasBounds = true;
            }
        }
    }
}
