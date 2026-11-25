using System;
using UnityEngine;

public partial class JengaStructureBuilder
{
    [Serializable]
    public class SpiralSettings
    {
        [Min(1)] public int layers = 24;
        [Tooltip("Rotation from one layer to the next in degrees. 90 = normal Jenga tower.")]
        [Range(-90f, 90f)] public float anglePerLayer = 20f;
        [Tooltip("Random extra rotation per layer in degrees.")]
        [Range(0f, 15f)] public float angleJitter = 0f;
        [Tooltip("Radius (in block widths) on which the layer centers circle around the middle. " +
                 "0 = straight tower, bigger = corkscrew (less stable).")]
        [Min(0f)] public float helixRadius = 0f;
    }

    private void BuildSpiral()
    {
        heightLayers = spiral.layers;
        float radius = spiral.helixRadius * blockWidth;
        float yaw = 0f;
        GetLayerLayout(out int count, out float spacing, out _);

        for (int layer = 0; layer < spiral.layers; layer++)
        {
            float rad = yaw * Mathf.Deg2Rad;
            var center = new Vector3(Mathf.Cos(rad) * radius, LayerY(layer), -Mathf.Sin(rad) * radius);
            PlaceLayer(center, yaw, count, spacing, layer);

            yaw += spiral.anglePerLayer + RandomRange(-spiral.angleJitter, spiral.angleJitter);
        }
    }
}
