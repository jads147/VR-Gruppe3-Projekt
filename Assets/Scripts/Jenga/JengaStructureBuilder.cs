using UnityEngine;

/// <summary>
/// Generates Jenga structures. The structure is picked with <see cref="StructureType"/>; every type has its own
/// settings and its own file (JengaStructureBuilder.&lt;Type&gt;.cs). This file holds what all types share:
/// prefabs, point-block distribution, seed, placing and clearing blocks.
///
/// Adding a new type: add an enum value, a partial file with a [Serializable] settings class + Build method,
/// a case in <see cref="Build"/> and the settings field name in JengaStructureBuilderEditor.
/// </summary>
public partial class JengaStructureBuilder : MonoBehaviour
{
    public enum StructureType { Tower, Pyramid, Skyline, SpiralTower }

    [SerializeField] private StructureType structureType = StructureType.Pyramid;

    [Header("Prefabs")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private GameObject pointCubePrefab;
    [SerializeField] private GameObject negativPointCubePrefab;

    [Header("Block Size")]
    [Tooltip("Uniform scale of every block; proportions stay the same. 1 = normal size, 0.5 = half size. " +
             "Applies to the prefab size, or to Custom Block Size if that is on.")]
    [SerializeField, Min(0.01f)] private float blockScale = 1f;
    [Tooltip("Scale every block (plain, plus and minus) to Custom Block Size instead of using the prefab's size.")]
    [SerializeField] private bool useCustomBlockSize = false;
    [Tooltip("Length (X), height (Y) and width (Z) of one block, in the builder's local units " +
             "(meters if the builder is not scaled).")]
    [SerializeField] private Vector3 customBlockSize = Vector3.zero;
    [Tooltip("Scale each block's Rigidbody mass with its volume, so bigger blocks are heavier.")]
    [SerializeField] private bool scaleMassWithVolume = true;

    [Header("Point Blocks")]
    [SerializeField, Range(0f, 1f)] private float pointCubeChance = 0.15f;
    [SerializeField, Range(0f, 1f)] private float negativPointCubeChance = 0.1f;
    [Tooltip("Bottom layers that only use the plain block (they touch the ground and would score instantly).")]
    [SerializeField, Min(0)] private int pointFreeBottomLayers = 1;
    [Tooltip("0 = point blocks spread evenly. 1 = plus blocks mostly at the top, minus blocks mostly at the bottom " +
             "(the average amount stays the same).")]
    [SerializeField, Range(0f, 1f)] private float heightBias = 0f;

    [Header("Build")]
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int seed = 12345;
    [SerializeField] private Transform blocksParent;
    [SerializeField] private bool buildWhenPlayStarts = false;
    [SerializeField, Min(0f)] private float gap = 0.001f;
    [Tooltip("Blocks side by side in one layer. 0 = automatic: as many as fit the block length, " +
             "so every layer is (nearly) square and the turned layer above rests on it completely.")]
    [SerializeField, Range(0, 9)] private int blocksPerLayer = 0;

    // One settings block per type. The editor only shows the one of the selected type.
    [SerializeField] private TowerSettings tower = new TowerSettings();
    [SerializeField] private PyramidSettings pyramid = new PyramidSettings();
    [SerializeField] private SkylineSettings skyline = new SkylineSettings();
    [SerializeField] private SpiralSettings spiral = new SpiralSettings();

    private System.Random random;
    private Bounds blockBounds;
    private bool longSideAlongZ;
    private float blockLength;
    private float blockWidth;
    private float blockHeight;
    // Custom size: scale per prefab-local axis and the resulting mass multiplier.
    private bool resizeBlocks;
    private Vector3 blockScaleFactor = Vector3.one;
    private float massFactor = 1f;
    // Highest layer count of the structure, used to normalize heights for heightBias.
    private int heightLayers = 1;

    public StructureType Type
    {
        get => structureType;
        set => structureType = value;
    }

    public Transform BlocksParent => blocksParent;

    private void Start()
    {
        if (buildWhenPlayStarts)
            Build();
    }

    public void Build()
    {
        if (blockPrefab == null)
        {
            Debug.LogError("Assign a block prefab to the Jenga structure builder.", this);
            return;
        }

        if (!PrepareBlockSize())
        {
            Debug.LogError("The block prefab needs a non-trigger Box Collider or a Renderer with non-zero bounds.", this);
            return;
        }

#if UNITY_EDITOR
        int undoGroup = UnityEditor.Undo.GetCurrentGroup();
        UnityEditor.Undo.SetCurrentGroupName($"Build Jenga {structureType}");
#endif

        EnsureParent();
        Clear();

        random = new System.Random(randomSeed ? System.Environment.TickCount : seed);
        heightLayers = 1;

        switch (structureType)
        {
            case StructureType.Tower: BuildTower(); break;
            case StructureType.Pyramid: BuildPyramid(); break;
            case StructureType.Skyline: BuildSkyline(); break;
            case StructureType.SpiralTower: BuildSpiral(); break;
        }

#if UNITY_EDITOR
        UnityEditor.Undo.CollapseUndoOperations(undoGroup);
#endif
    }

    public void Clear()
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

    // ---------------------------------------------------------------- shared building helpers

    /// <summary>
    /// Layout of one layer for the current block size: how many blocks lie side by side, their center distance
    /// and the side length of the (square) layer. If the blocks don't fill the block length exactly, they are
    /// spread a little, so the turned layer above has support under both of its ends.
    /// </summary>
    private void GetLayerLayout(out int count, out float spacing, out float footprint)
    {
        count = blocksPerLayer > 0
            ? blocksPerLayer
            : Mathf.Max(1, Mathf.RoundToInt((blockLength + gap) / (blockWidth + gap)));
        float packed = count * blockWidth + (count - 1) * gap;
        footprint = Mathf.Max(blockLength, packed);
        spacing = count > 1 ? (footprint - blockWidth) / (count - 1) : 0f;
    }

    /// <summary>
    /// Reads the block size from the prefab and applies the custom size. Sets <see cref="blockBounds"/>,
    /// <see cref="blockLength"/>, <see cref="blockWidth"/> and <see cref="blockHeight"/>.
    /// </summary>
    private bool PrepareBlockSize()
    {
        if (blockPrefab == null || !TryGetBlockBounds(out blockBounds) ||
            blockBounds.size.x <= 0f || blockBounds.size.y <= 0f || blockBounds.size.z <= 0f)
            return false;

        // Normalize blocks whose long horizontal side is along Z instead of X.
        longSideAlongZ = blockBounds.size.z > blockBounds.size.x;
        ApplyCustomBlockSize();
        blockLength = longSideAlongZ ? blockBounds.size.z : blockBounds.size.x;
        blockWidth = longSideAlongZ ? blockBounds.size.x : blockBounds.size.z;
        blockHeight = blockBounds.size.y;
        return true;
    }

    /// <summary>
    /// For the inspector: the layer layout the current settings produce.
    /// <paramref name="blockSize"/> is (length, height, width), <paramref name="gapBetweenBlocks"/> the free space
    /// between neighbouring blocks of one layer.
    /// </summary>
    public bool TryGetLayerInfo(out Vector3 blockSize, out int count, out float gapBetweenBlocks)
    {
        blockSize = Vector3.zero;
        count = 0;
        gapBetweenBlocks = 0f;
        if (!PrepareBlockSize())
            return false;

        GetLayerLayout(out count, out float spacing, out _);
        blockSize = new Vector3(blockLength, blockHeight, blockWidth);
        gapBetweenBlocks = count > 1 ? spacing - blockWidth : 0f;
        return true;
    }

    /// <summary>Center height of a layer above the builder's pivot.</summary>
    private float LayerY(int layer) => blockHeight * (layer + 0.5f);

    /// <summary>
    /// Classic Jenga stack: <paramref name="layers"/> layers of <paramref name="blocksPerLayer"/> parallel blocks,
    /// each layer turned 90 degrees.
    /// </summary>
    private void BuildJengaStack(Vector3 localBase, float yaw, int layers, int blocksPerLayer, float spacing)
    {
        for (int layer = 0; layer < layers; layer++)
        {
            Vector3 center = localBase + Vector3.up * LayerY(layer);
            PlaceLayer(center, yaw + layer % 2 * 90f, blocksPerLayer, spacing, layer);
        }
    }

    /// <summary>
    /// Places <paramref name="count"/> parallel blocks side by side, centered on <paramref name="localCenter"/>.
    /// At yaw 0 the blocks' long side points along local X and they are lined up along local Z.
    /// </summary>
    private void PlaceLayer(Vector3 localCenter, float yaw, int count, float spacing, int layer)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        for (int i = 0; i < count; i++)
        {
            float offset = (i - (count - 1) * 0.5f) * spacing;
            PlaceBlock(localCenter + rotation * new Vector3(0f, 0f, offset), rotation, layer);
        }
    }

    /// <summary>Places one block with its bounds center at <paramref name="localCenter"/> (builder space).</summary>
    private void PlaceBlock(Vector3 localCenter, Quaternion localRotation, int layer)
    {
        float height01 = heightLayers <= 1 ? 0f : Mathf.Clamp01(layer / (float)(heightLayers - 1));
        GameObject prefab = PickPrefab(layer >= pointFreeBottomLayers, height01);

        Quaternion blockRotation = localRotation * Quaternion.Euler(0f, longSideAlongZ ? 90f : 0f, 0f);
        // Place the bounds center correctly even when the pivot is offset.
        Vector3 localPivot = localCenter - blockRotation * blockBounds.center;
        Vector3 position = transform.TransformPoint(localPivot);
        Quaternion rotation = transform.rotation * blockRotation;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, gameObject.scene);
            if (instance == null)
                return;
            instance.transform.SetParent(blocksParent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            ResizeBlock(instance);
            UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Build Jenga Structure");
            return;
        }
#endif
        ResizeBlock(Instantiate(prefab, position, rotation, blocksParent));
    }

    /// <summary>
    /// With a custom block size: computes the scale per prefab axis and rescales <see cref="blockBounds"/>,
    /// so all shapes automatically work with the new size.
    /// </summary>
    private void ApplyCustomBlockSize()
    {
        blockScaleFactor = Vector3.one;
        massFactor = 1f;
        float scale = Mathf.Max(0.01f, blockScale);
        resizeBlocks = useCustomBlockSize || !Mathf.Approximately(scale, 1f);
        if (!resizeBlocks)
            return;

        Vector3 target = blockBounds.size;
        if (useCustomBlockSize)
        {
            Vector3 size = Vector3.Max(customBlockSize, Vector3.one * 0.001f);
            // customBlockSize is (length, height, width); map it onto the prefab's own axes.
            target = longSideAlongZ ? new Vector3(size.z, size.y, size.x) : size;
        }
        target *= scale;

        blockScaleFactor = new Vector3(
            target.x / blockBounds.size.x,
            target.y / blockBounds.size.y,
            target.z / blockBounds.size.z);
        massFactor = blockScaleFactor.x * blockScaleFactor.y * blockScaleFactor.z;
        blockBounds = new Bounds(Vector3.Scale(blockBounds.center, blockScaleFactor), target);
    }

    private void ResizeBlock(GameObject instance)
    {
        if (!resizeBlocks || instance == null)
            return;

        // Multiply instead of overwrite, so plus/minus prefabs with their own root scale still match the block.
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, blockScaleFactor);
        if (scaleMassWithVolume && instance.TryGetComponent(out Rigidbody body))
            body.mass *= massFactor;
    }

    /// <summary>Size of the block prefab as (length, height, width), or false if it has no usable bounds.</summary>
    public bool TryGetPrefabBlockSize(out Vector3 size)
    {
        size = Vector3.zero;
        if (blockPrefab == null || !TryGetBlockBounds(out Bounds bounds))
            return false;

        Vector3 s = bounds.size;
        size = s.z > s.x ? new Vector3(s.z, s.y, s.x) : s;
        return size.x > 0f && size.y > 0f && size.z > 0f;
    }

    private void OnValidate()
    {
        // Start from the prefab's size the first time the custom size is switched on.
        if (useCustomBlockSize && customBlockSize == Vector3.zero && TryGetPrefabBlockSize(out Vector3 size))
            customBlockSize = size;
    }

    private GameObject PickPrefab(bool allowPointCubes, float height01)
    {
        double roll = random.NextDouble();
        if (!allowPointCubes)
            return blockPrefab;

        // Shift plus blocks up and minus blocks down; the mean over the height stays the same.
        float plus = pointCubeChance * Mathf.Lerp(1f, 2f * height01, heightBias);
        float minus = negativPointCubeChance * Mathf.Lerp(1f, 2f * (1f - height01), heightBias);

        if (roll < plus && pointCubePrefab != null)
            return pointCubePrefab;
        if (roll < plus + minus && negativPointCubePrefab != null)
            return negativPointCubePrefab;
        return blockPrefab;
    }

    private float RandomRange(float min, float max) => min + (float)random.NextDouble() * (max - min);

    // ---------------------------------------------------------------- setup

    private void EnsureParent()
    {
        if (blocksParent != null)
            return;

        var parentObject = new GameObject("Generated Blocks");
        parentObject.transform.SetParent(transform, false);
        blocksParent = parentObject.transform;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RegisterCreatedObjectUndo(parentObject, "Create Blocks Parent");
            UnityEditor.Undo.RecordObject(this, "Assign Blocks Parent");
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }
#endif
    }

    private bool TryGetBlockBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        foreach (BoxCollider box in blockPrefab.GetComponentsInChildren<BoxCollider>())
        {
            if (box.enabled && !box.isTrigger)
                EncapsulateBounds(new Bounds(box.center, box.size), box.transform, ref bounds, ref hasBounds);
        }

        if (hasBounds)
            return true;

        foreach (Renderer modelRenderer in blockPrefab.GetComponentsInChildren<Renderer>())
        {
            if (modelRenderer.enabled)
                EncapsulateBounds(modelRenderer.localBounds, modelRenderer.transform, ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private void EncapsulateBounds(Bounds source, Transform sourceTransform, ref Bounds result, ref bool hasBounds)
    {
        // Child transforms are included; prefab position/rotation are ignored, root scale is kept.
        Matrix4x4 toBlockSpace = Matrix4x4.Scale(blockPrefab.transform.localScale) *
            blockPrefab.transform.worldToLocalMatrix * sourceTransform.localToWorldMatrix;

        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = source.center + Vector3.Scale(source.extents, new Vector3(
                (corner & 1) == 0 ? -1f : 1f,
                (corner & 2) == 0 ? -1f : 1f,
                (corner & 4) == 0 ? -1f : 1f));

            point = toBlockSpace.MultiplyPoint3x4(point);
            if (hasBounds)
            {
                result.Encapsulate(point);
            }
            else
            {
                result = new Bounds(point, Vector3.zero);
                hasBounds = true;
            }
        }
    }
}
