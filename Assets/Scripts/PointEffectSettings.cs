using UnityEngine;

/// Look and sound of the effect a point block plays when it hits a "Fallen" surface.
/// Sizes are relative to the block's longest side, so the effect fits any scene scale.
[CreateAssetMenu(fileName = "PointEffectSettings", menuName = "Jenga/Point Effect Settings")]
public class PointEffectSettings : ScriptableObject
{
    public const string ResourcesPath = "PointEffectSettings";

    [Header("General")]
    [Tooltip("Soft round particle material, e.g. StylizedExplosion.")]
    public Material particleMaterial;
    [Min(0.01f)] public float effectScale = 1f;
    public Color woodColor = new Color(0.87f, 0.72f, 0.55f);
    [Range(0, 80)] public int splinterCount = 30;

    [Header("Points gained (+)")]
    public Color positiveColor = new Color(1f, 0.78f, 0.2f);
    public Color positiveHighlight = new Color(1f, 0.97f, 0.75f);

    [Header("Points lost (-)")]
    public Color negativeColor = new Color(0.95f, 0.18f, 0.14f);
    public Color negativeSmoke = new Color(0.22f, 0.1f, 0.12f, 0.75f);

    [Header("Floating number")]
    public bool showNumber = true;
    [Tooltip("Height of the digits, relative to the block's longest side.")]
    [Min(0.01f)] public float numberHeight = 0.45f;
    [Min(0.2f)] public float numberDuration = 1.6f;
    public Color positiveNumberColor = new Color(1f, 0.86f, 0.25f);
    public Color negativeNumberColor = new Color(1f, 0.27f, 0.22f);
    public Color numberOutlineColor = new Color(0.12f, 0.06f, 0.02f);

    [Header("Sound")]
    public bool playSound = true;
    [Range(0f, 1f)] public float volume = 0.7f;
    [Min(0f)] public float minDistance = 7f;
    [Min(0.1f)] public float maxDistance = 40f;
    [Tooltip("Leave empty to use the built-in generated chime.")]
    public AudioClip positiveClip;
    [Tooltip("Leave empty to use the built-in generated buzz.")]
    public AudioClip negativeClip;
}
