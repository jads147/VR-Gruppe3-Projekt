using System;
using UnityEngine;

public partial class JengaStructureBuilder
{
    [Serializable]
    public class PyramidSettings
    {
        [Tooltip("Cells along X in the bottom step. A cell is one square Jenga layer (see Blocks Per Layer).")]
        [Min(1)] public int baseCellsX = 4;
        [Tooltip("Cells along Z in the bottom step. 1 = flat pyramid wall.")]
        [Min(1)] public int baseCellsZ = 4;
        [Tooltip("Jenga layers per step. Each step is one cell smaller per direction, " +
                 "so every upper cell rests on the corners of four cells below.")]
        [Min(1)] public int layersPerStep = 2;
    }

    private void BuildPyramid()
    {
        int steps = Mathf.Max(pyramid.baseCellsX, pyramid.baseCellsZ);
        heightLayers = steps * pyramid.layersPerStep;

        // Each cell is one square layer, so neighbouring cells touch and upper cells rest on the corners below.
        GetLayerLayout(out int count, out float spacing, out float cell);
        float pitch = cell + gap;

        int layer = 0;
        for (int step = 0; step < steps; step++)
        {
            // The narrow side stays at 1 cell and forms a ridge until the wide side is done too.
            int cellsX = Mathf.Max(1, pyramid.baseCellsX - step);
            int cellsZ = Mathf.Max(1, pyramid.baseCellsZ - step);

            for (int stepLayer = 0; stepLayer < pyramid.layersPerStep; stepLayer++, layer++)
            {
                // All cells of a layer share the direction, so joints stay aligned across the layer.
                float yaw = layer % 2 * 90f;
                for (int x = 0; x < cellsX; x++)
                {
                    for (int z = 0; z < cellsZ; z++)
                    {
                        var center = new Vector3(
                            (x - (cellsX - 1) * 0.5f) * pitch,
                            LayerY(layer),
                            (z - (cellsZ - 1) * 0.5f) * pitch);
                        PlaceLayer(center, yaw, count, spacing, layer);
                    }
                }
            }
        }
    }
}
