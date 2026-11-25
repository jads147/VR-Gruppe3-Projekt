using System;
using UnityEngine;

public partial class JengaStructureBuilder
{
    [Serializable]
    public class TowerSettings
    {
        [Min(1)] public int layers = 18;
    }

    private void BuildTower()
    {
        heightLayers = tower.layers;
        GetLayerLayout(out int count, out float spacing, out _);
        BuildJengaStack(Vector3.zero, 0f, tower.layers, count, spacing);
    }
}
