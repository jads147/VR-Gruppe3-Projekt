using System.Collections.Generic;
using UnityEngine;

/// Gives each Jenga block a random wood grain from a JengaGrainSet and an optional
/// random 180-degree flip, so blocks in the tower don't all look the same.
/// The block's own material settings (tint, metallic, ...) are kept; only the textures change.
[DisallowMultipleComponent]
public class BlockGrainRandomizer : MonoBehaviour
{
    [SerializeField] private JengaGrainSet grainSet;
    [SerializeField] private Renderer targetRenderer;
    [SerializeField, Min(0)] private int sideMaterialIndex = 0;
    [SerializeField, Min(0)] private int endMaterialIndex = 1;
    [Tooltip("Randomly turn the block 180 degrees around one of its axes. The box shape stays identical, " +
             "but which end and side face outward changes, which multiplies the visible variations by 4.")]
    [SerializeField] private bool randomFlip = true;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    // One material per (original material, texture), shared by all blocks that rolled the same variant.
    private static readonly Dictionary<(Material, Texture), Material> MaterialCache =
        new Dictionary<(Material, Texture), Material>();

    private static readonly Vector3[] FlipAxes = { Vector3.right, Vector3.up, Vector3.forward };

    private void Reset()
    {
        targetRenderer = GetComponentInChildren<Renderer>();
    }

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer == null)
            return;

        if (grainSet != null && grainSet.Count > 0)
            ApplyVariant(grainSet[Random.Range(0, grainSet.Count)]);

        if (randomFlip)
            ApplyRandomFlip();
    }

    private void ApplyVariant(JengaGrainSet.Variant variant)
    {
        Material[] materials = targetRenderer.sharedMaterials;
        SwapTexture(materials, sideMaterialIndex, variant.side);
        SwapTexture(materials, endMaterialIndex, variant.end);
        targetRenderer.sharedMaterials = materials;
    }

    private static void SwapTexture(Material[] materials, int index, Texture texture)
    {
        if (texture == null || index >= materials.Length || materials[index] == null)
            return;

        Material original = materials[index];
        if (original.HasProperty(BaseMapId) && original.GetTexture(BaseMapId) == texture)
            return;

        var key = (original, texture);
        // A cached material can be destroyed between play sessions when domain reload is off.
        if (!MaterialCache.TryGetValue(key, out Material variantMaterial) || variantMaterial == null)
        {
            variantMaterial = new Material(original) { name = $"{original.name} ({texture.name})" };
            if (variantMaterial.HasProperty(BaseMapId))
                variantMaterial.SetTexture(BaseMapId, texture);
            if (variantMaterial.HasProperty(MainTexId))
                variantMaterial.SetTexture(MainTexId, texture);
            MaterialCache[key] = variantMaterial;
        }

        materials[index] = variantMaterial;
    }

    private void ApplyRandomFlip()
    {
        // 0 = keep, 1..3 = 180 degrees around the block's local X, Y or Z axis.
        int choice = Random.Range(0, 4);
        if (choice == 0)
            return;

        // Rotate around the visual centre so the block stays exactly where it was placed.
        Vector3 pivot = targetRenderer.bounds.center;
        Vector3 axis = targetRenderer.transform.TransformDirection(FlipAxes[choice - 1]);
        transform.RotateAround(pivot, axis, 180f);
    }
}
