using System;
using UnityEngine;

public partial class JengaStructureBuilder
{
    [Serializable]
    public class SkylineSettings
    {
        [Min(1)] public int countX = 4;
        [Min(1)] public int countZ = 3;
        [Tooltip("Free space between neighbouring towers, in block lengths. " +
                 "Gets raised automatically if towers would overlap because of rotation or jitter.")]
        [Min(0f)] public float spacing = 0.4f;
        [Min(1)] public int minLayers = 6;
        [Min(1)] public int maxLayers = 18;
        [Tooltip("0 = random heights. 1 = towers in the middle are highest (like a city center).")]
        [Range(0f, 1f)] public float centerBias = 0.5f;
        [Tooltip("Chance that a spot stays empty.")]
        [Range(0f, 0.9f)] public float emptyChance = 0.1f;
        [Tooltip("Random rotation of each tower in degrees.")]
        [Range(0f, 45f)] public float maxYaw = 10f;
        [Tooltip("Random position offset of each tower, in block widths.")]
        [Min(0f)] public float positionJitter = 0.2f;
    }

    private void BuildSkyline()
    {
        int minLayers = Mathf.Min(skyline.minLayers, skyline.maxLayers);
        int maxLayers = Mathf.Max(skyline.minLayers, skyline.maxLayers);
        heightLayers = maxLayers;

        GetLayerLayout(out int count, out float layerSpacing, out float cell);
        float jitter = skyline.positionJitter * blockWidth;
        // A square turned by maxYaw needs cell * (cos + sin) of room; jitter can move two neighbours together.
        float yawRad = skyline.maxYaw * Mathf.Deg2Rad;
        float minPitch = cell * (Mathf.Cos(yawRad) + Mathf.Sin(yawRad)) + 2f * jitter + gap;
        float pitch = Mathf.Max(cell + skyline.spacing * blockLength, minPitch);

        float halfX = (skyline.countX - 1) * 0.5f;
        float halfZ = (skyline.countZ - 1) * 0.5f;

        for (int x = 0; x < skyline.countX; x++)
        {
            for (int z = 0; z < skyline.countZ; z++)
            {
                if (random.NextDouble() < skyline.emptyChance)
                    continue;

                // 0 at the corners, 1 in the middle.
                float dx = halfX > 0f ? (x - halfX) / halfX : 0f;
                float dz = halfZ > 0f ? (z - halfZ) / halfZ : 0f;
                float centerFactor = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Sqrt(2f));

                float height01 = Mathf.Lerp((float)random.NextDouble(), centerFactor, skyline.centerBias);
                int layers = Mathf.RoundToInt(Mathf.Lerp(minLayers, maxLayers, height01));

                var basePosition = new Vector3(
                    (x - halfX) * pitch + RandomRange(-jitter, jitter),
                    0f,
                    (z - halfZ) * pitch + RandomRange(-jitter, jitter));
                float yaw = RandomRange(-skyline.maxYaw, skyline.maxYaw);

                BuildJengaStack(basePosition, yaw, layers, count, layerSpacing);
            }
        }
    }
}
