using System.Collections.Generic;
using UnityEngine;

public class HouseBuilder : MonoBehaviour
{
    public enum WallSide { Front, Back, Left, Right }

    [System.Serializable]
    public class Opening
    {
        public WallSide wall = WallSide.Front;
        [Tooltip("Offset of the opening center from the wall center along the wall (m).")]
        public float offset = 0f;
        [Min(0.05f)] public float width = 1.2f;
        [Tooltip("Height of the opening's lower edge above the floor (m). 0 = door.")]
        [Min(0f)] public float bottom = 0f;
        [Min(0.05f)] public float height = 2.1f;
    }

    [Header("Prefabs")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private GameObject pointCubePrefab;
    [SerializeField] private GameObject negativPointCubePrefab;
    [SerializeField, Range(0f, 1f)] private float pointCubeChance = 0.15f;
    [SerializeField, Range(0f, 1f)] private float negativPointCubeChance = 0.1f;
    [Tooltip("Bottom rows that only use the plain Cube (they touch the ground and would score instantly).")]
    [SerializeField, Min(0)] private int pointFreeBottomRows = 1;
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int seed = 12345;

    [Header("Build")]
    [SerializeField] private Transform blocksParent;
    [SerializeField] private bool buildWhenPlayStarts = false;

    [Header("House (outer size, pivot = floor center)")]
    [SerializeField, Min(1f)] private float width = 6f;
    [SerializeField, Min(1f)] private float depth = 5f;
    [SerializeField, Min(0.3f)] private float wallHeight = 2.8f;

    [Header("Blocks (target sizes, adjusted so they fit)")]
    [SerializeField, Min(0.1f)] private float brickLength = 1.2f;
    [SerializeField, Min(0.05f)] private float brickHeight = 0.35f;
    [SerializeField, Min(0.05f)] private float wallThickness = 0.5f;
    [SerializeField, Min(0f)] private float gap = 0.002f;

    [Header("Openings (doors / windows)")]
    [Tooltip("How far a lintel block reaches past each side of an opening.")]
    [SerializeField, Min(0f)] private float lintelOverlap = 0.3f;
    [SerializeField] private List<Opening> openings = new List<Opening>
    {
        new Opening { wall = WallSide.Front, offset = 0f, width = 1.2f, bottom = 0f, height = 2.1f },
        new Opening { wall = WallSide.Back, offset = -1.5f, width = 1.2f, bottom = 0.7f, height = 1.05f },
        new Opening { wall = WallSide.Back, offset = 1.5f, width = 1.2f, bottom = 0.7f, height = 1.05f },
        new Opening { wall = WallSide.Left, offset = 0f, width = 1.2f, bottom = 0.7f, height = 1.05f },
        new Opening { wall = WallSide.Right, offset = 0f, width = 1.2f, bottom = 0.7f, height = 1.05f },
    };

    [Header("Roof (stepped gable)")]
    [SerializeField] private bool buildRoof = true;
    [Tooltip("Horizontal inset per layer. Smaller = steeper roof.")]
    [SerializeField, Min(0.05f)] private float roofStep = 0.5f;
    [SerializeField, Min(0f)] private float roofOverhang = 0.3f;

    [Header("Physics")]
    [Tooltip("Scale each block's Rigidbody mass by its volume relative to the prefab.")]
    [SerializeField] private bool scaleMassWithVolume = true;

    private System.Random random;
    private float rowHeight;
    private int wallRows;

    private void Start()
    {
        if (buildWhenPlayStarts)
            BuildHouse();
    }

    public void BuildHouse()
    {
        if (cubePrefab == null)
        {
            Debug.LogError("Assign at least the Cube prefab to the house builder.", this);
            return;
        }

        EnsureParent();
        ClearHouse();

        random = new System.Random(randomSeed ? System.Environment.TickCount : seed);

        // Fit the row height so the walls end exactly at wallHeight.
        wallRows = Mathf.Max(1, Mathf.RoundToInt(wallHeight / brickHeight));
        rowHeight = wallHeight / wallRows;

        float t = Mathf.Min(wallThickness, Mathf.Min(width, depth) * 0.25f);
        float halfW = width * 0.5f;
        float halfD = depth * 0.5f;

        for (int row = 0; row < wallRows; row++)
        {
            // Corners interlock: front/back and side walls take turns running through the corner.
            bool even = row % 2 == 0;
            float frontHalf = even ? halfW : halfW - t;
            float sideHalf = even ? halfD - t : halfD;

            BuildWallRow(WallSide.Front, row, -frontHalf, frontHalf, t);
            BuildWallRow(WallSide.Back, row, -frontHalf, frontHalf, t);
            BuildWallRow(WallSide.Left, row, -sideHalf, sideHalf, t);
            BuildWallRow(WallSide.Right, row, -sideHalf, sideHalf, t);
        }

        if (buildRoof)
            BuildRoof(t);
    }

    public void ClearHouse()
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

    private void BuildRoof(float t)
    {
        float halfW = width * 0.5f;
        float plankLength = depth + 2f * roofOverhang;

        for (int layer = 0; ; layer++)
        {
            float outerHalf = halfW - layer * roofStep;
            float y = (wallRows + layer + 0.5f) * rowHeight;

            // Close the roof with one ridge plank once the remaining width is small.
            if (outerHalf * 2f <= roofStep * 3f)
            {
                if (outerHalf > 0.01f)
                {
                    PlaceBlock(new Vector3(0f, y, 0f), Quaternion.identity,
                        new Vector3(outerHalf * 2f - gap, rowHeight, plankLength));
                }
                break;
            }

            // Gable walls shrink each layer and carry the roof planks at their ends.
            float gableHalf = outerHalf - roofStep;
            BuildWallRow(WallSide.Front, wallRows + layer, -gableHalf, gableHalf, t);
            BuildWallRow(WallSide.Back, wallRows + layer, -gableHalf, gableHalf, t);

            float plankX = outerHalf - roofStep * 0.5f;
            Vector3 plankSize = new Vector3(roofStep - gap, rowHeight, plankLength);
            PlaceBlock(new Vector3(-plankX, y, 0f), Quaternion.identity, plankSize);
            PlaceBlock(new Vector3(plankX, y, 0f), Quaternion.identity, plankSize);
        }
    }

    private void BuildWallRow(WallSide side, int row, float spanMin, float spanMax, float t)
    {
        if (spanMax - spanMin < 0.01f)
            return;

        List<Vector2> blocked = new List<Vector2>();
        List<Vector2> lintels = new List<Vector2>();

        foreach (Opening opening in openings)
        {
            if (opening == null || opening.wall != side)
                continue;

            GetOpeningRows(opening, out int firstRow, out int endRow);
            float halfWidth = opening.width * 0.5f;

            if (row >= firstRow && row < endRow)
                blocked.Add(new Vector2(opening.offset - halfWidth, opening.offset + halfWidth));
            else if (row == endRow)
                lintels.Add(new Vector2(opening.offset - halfWidth - lintelOverlap,
                    opening.offset + halfWidth + lintelOverlap));
        }

        // Lintels are single blocks bridging the opening below them.
        lintels = Union(lintels, spanMin, spanMax);
        foreach (Vector2 cut in blocked)
            SubtractInterval(lintels, cut.x, cut.y);

        List<Vector2> free = new List<Vector2> { new Vector2(spanMin, spanMax) };
        foreach (Vector2 cut in blocked)
            SubtractInterval(free, cut.x, cut.y);
        foreach (Vector2 cut in lintels)
            SubtractInterval(free, cut.x, cut.y);

        List<Vector2> pieces = new List<Vector2>(lintels);
        foreach (Vector2 segment in free)
            SplitSegment(segment.x, segment.y, row % 2 == 1, pieces);

        GetWallFrame(side, t, out Vector3 wallCenter, out Quaternion wallRotation);
        float y = (row + 0.5f) * rowHeight;

        foreach (Vector2 piece in pieces)
        {
            float length = piece.y - piece.x;
            if (length < 0.02f)
                continue;

            Vector3 center = wallCenter + wallRotation * new Vector3((piece.x + piece.y) * 0.5f, 0f, 0f);
            center.y = y;
            PlaceBlock(center, wallRotation, new Vector3(Mathf.Max(0.01f, length - gap), rowHeight, t),
                row >= pointFreeBottomRows);
        }
    }

    private void SplitSegment(float a, float b, bool halfOffset, List<Vector2> pieces)
    {
        // Equal bricks per segment, every other row starts and ends with a half brick (running bond).
        float length = b - a;
        int count = Mathf.Max(1, Mathf.RoundToInt(length / brickLength));
        float brick = length / count;

        if (halfOffset && brick * 0.5f >= brickLength * 0.3f)
        {
            pieces.Add(new Vector2(a, a + brick * 0.5f));
            for (int i = 0; i < count - 1; i++)
            {
                float start = a + brick * (i + 0.5f);
                pieces.Add(new Vector2(start, start + brick));
            }
            pieces.Add(new Vector2(b - brick * 0.5f, b));
            return;
        }

        for (int i = 0; i < count; i++)
            pieces.Add(new Vector2(a + brick * i, a + brick * (i + 1)));
    }

    private void GetOpeningRows(Opening opening, out int firstRow, out int endRow)
    {
        // Openings snap to whole block rows.
        firstRow = Mathf.Max(0, Mathf.RoundToInt(opening.bottom / rowHeight));
        endRow = firstRow + Mathf.Max(1, Mathf.RoundToInt(opening.height / rowHeight));
    }

    private void GetWallFrame(WallSide side, float t, out Vector3 center, out Quaternion rotation)
    {
        float halfW = width * 0.5f;
        float halfD = depth * 0.5f;

        switch (side)
        {
            case WallSide.Front:
                center = new Vector3(0f, 0f, -halfD + t * 0.5f);
                rotation = Quaternion.identity;
                break;
            case WallSide.Back:
                center = new Vector3(0f, 0f, halfD - t * 0.5f);
                rotation = Quaternion.identity;
                break;
            case WallSide.Left:
                center = new Vector3(-halfW + t * 0.5f, 0f, 0f);
                rotation = Quaternion.Euler(0f, -90f, 0f);
                break;
            default:
                center = new Vector3(halfW - t * 0.5f, 0f, 0f);
                rotation = Quaternion.Euler(0f, -90f, 0f);
                break;
        }
    }

    private void PlaceBlock(Vector3 localCenter, Quaternion localRotation, Vector3 size,
        bool allowPointCubes = true)
    {
        GameObject prefab = PickPrefab(allowPointCubes);
        Vector3 position = transform.position + transform.rotation * localCenter;
        Quaternion rotation = transform.rotation * localRotation;

        GameObject instance;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, gameObject.scene);
            if (instance == null)
                return;
            instance.transform.SetParent(blocksParent, true);
            instance.transform.SetPositionAndRotation(position, rotation);
            UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Build House");
        }
        else
#endif
        {
            instance = Instantiate(prefab, position, rotation, blocksParent);
        }

        // Resize the block: prefab mesh/collider size times scale gives the wanted size.
        Vector3 baseSize = GetBaseSize(prefab);
        Vector3 parentScale = blocksParent.lossyScale;
        instance.transform.localScale = new Vector3(
            size.x / (baseSize.x * parentScale.x),
            size.y / (baseSize.y * parentScale.y),
            size.z / (baseSize.z * parentScale.z));

        if (scaleMassWithVolume &&
            instance.TryGetComponent(out Rigidbody body) &&
            prefab.TryGetComponent(out Rigidbody prefabBody))
        {
            Vector3 prefabSize = Vector3.Scale(baseSize, prefab.transform.localScale);
            float prefabVolume = prefabSize.x * prefabSize.y * prefabSize.z;
            if (prefabVolume > 0f)
                body.mass = prefabBody.mass * (size.x * size.y * size.z) / prefabVolume;
        }
    }

    private GameObject PickPrefab(bool allowPointCubes)
    {
        double roll = random.NextDouble();
        if (!allowPointCubes)
            return cubePrefab;
        if (roll < pointCubeChance && pointCubePrefab != null)
            return pointCubePrefab;
        if (roll < pointCubeChance + negativPointCubeChance && negativPointCubePrefab != null)
            return negativPointCubePrefab;
        return cubePrefab;
    }

    private static Vector3 GetBaseSize(GameObject prefab)
    {
        Vector3 size = Vector3.one;
        if (prefab.TryGetComponent(out BoxCollider box))
            size = box.size;
        else if (prefab.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
            size = filter.sharedMesh.bounds.size;

        return new Vector3(
            Mathf.Max(0.0001f, size.x),
            Mathf.Max(0.0001f, size.y),
            Mathf.Max(0.0001f, size.z));
    }

    private void EnsureParent()
    {
        if (blocksParent != null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject parentObject = new GameObject("Generated House");
            UnityEditor.Undo.RegisterCreatedObjectUndo(parentObject, "Create House Parent");
            parentObject.transform.SetParent(transform, false);
            UnityEditor.Undo.RecordObject(this, "Assign House Parent");
            blocksParent = parentObject.transform;
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            return;
        }
#endif
        GameObject runtimeParent = new GameObject("Generated House");
        runtimeParent.transform.SetParent(transform, false);
        blocksParent = runtimeParent.transform;
    }

    private static List<Vector2> Union(List<Vector2> intervals, float min, float max)
    {
        List<Vector2> result = new List<Vector2>();
        intervals.Sort((a, b) => a.x.CompareTo(b.x));

        foreach (Vector2 interval in intervals)
        {
            Vector2 clamped = new Vector2(Mathf.Max(min, interval.x), Mathf.Min(max, interval.y));
            if (clamped.y <= clamped.x)
                continue;

            int last = result.Count - 1;
            if (last >= 0 && clamped.x <= result[last].y)
                result[last] = new Vector2(result[last].x, Mathf.Max(result[last].y, clamped.y));
            else
                result.Add(clamped);
        }

        return result;
    }

    private static void SubtractInterval(List<Vector2> intervals, float a, float b)
    {
        for (int i = intervals.Count - 1; i >= 0; i--)
        {
            Vector2 interval = intervals[i];
            if (b <= interval.x || a >= interval.y)
                continue;

            intervals.RemoveAt(i);
            if (b < interval.y)
                intervals.Insert(i, new Vector2(b, interval.y));
            if (interval.x < a)
                intervals.Insert(i, new Vector2(interval.x, a));
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(0f, wallHeight * 0.5f, 0f), new Vector3(width, wallHeight, depth));

        float t = Mathf.Min(wallThickness, Mathf.Min(width, depth) * 0.25f);
        Gizmos.color = Color.yellow;
        foreach (Opening opening in openings)
        {
            if (opening == null)
                continue;

            GetWallFrame(opening.wall, t, out Vector3 center, out Quaternion rotation);
            Vector3 openingCenter = center + rotation * new Vector3(opening.offset, 0f, 0f);
            openingCenter.y = opening.bottom + opening.height * 0.5f;

            Gizmos.matrix = Matrix4x4.TRS(transform.position + transform.rotation * openingCenter,
                transform.rotation * rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(opening.width, opening.height, t));
        }
    }
}
