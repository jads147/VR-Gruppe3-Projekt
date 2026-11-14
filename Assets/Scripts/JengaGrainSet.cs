using UnityEngine;

/// Pairs of baked wood textures (side grain + end grain) for the Jenga block mesh.
/// Each pair was baked from the same 3D grain, so its side and end lines meet at the edges.
[CreateAssetMenu(fileName = "JengaGrainSet", menuName = "Jenga/Grain Set")]
public class JengaGrainSet : ScriptableObject
{
    [System.Serializable]
    public struct Variant
    {
        public Texture2D side;
        public Texture2D end;
    }

    [SerializeField] private Variant[] variants = new Variant[0];

    public int Count => variants.Length;
    public Variant this[int index] => variants[index];
}
