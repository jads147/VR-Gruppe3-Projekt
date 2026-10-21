using UnityEngine;

public class JengaTowerBuilder : MonoBehaviour
{
    [SerializeField] private GameObject blockPrefab;
    [SerializeField, Min(1)] private int layers = 18;
    [SerializeField, Min(0f)] private float gap = 0.001f;

    private void Start()
    {
        if (blockPrefab == null)
        {
            Debug.LogError("Assign a block prefab to the Jenga tower builder.", this);
            return;
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

        for (int layer = 0; layer < Mathf.Max(1, layers); layer++)
        {
            Quaternion layerRotation = Quaternion.Euler(0f, layer % 2 * 90f, 0f);
            Quaternion blockRotation = transform.rotation * layerRotation *
                Quaternion.Euler(0f, longSideAlongZ ? 90f : 0f, 0f);

            for (int block = 0; block < 3; block++)
            {
                Vector3 center = new Vector3(
                    0f,
                    bounds.size.y * (layer + 0.5f),
                    (block - 1) * spacing
                );

                Vector3 worldCenter = transform.position +
                    transform.rotation * (layerRotation * center);

                // Place the bounds center correctly even when the pivot is offset.
                Instantiate(blockPrefab,
                    worldCenter - blockRotation * bounds.center,
                    blockRotation);
            }
        }
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
