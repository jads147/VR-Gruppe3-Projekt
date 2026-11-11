using UnityEngine;

/// <summary>
/// 3D-Pfeil-Button: Solange er gehovert (runtergedrückt) wird, dreht sich der Orbit von
/// <see cref="OrbitAroundTarget"/> nach links oder rechts – genau wie mit dem Stick.
/// Setup siehe <see cref="PressableButton"/>.
/// </summary>
public class ArrowButton : PressableButton
{
    [Header("Orbit")]
    [Tooltip("Der OrbitAroundTarget, der gedreht wird. Leer = in der Szene suchen.")]
    [SerializeField] OrbitAroundTarget orbit;

    [Tooltip("An = nach links orbiten, Aus = nach rechts.")]
    [SerializeField] bool orbitLeft = false;

    [Tooltip("Wie stark der Button 'den Stick drückt' (1 = volle Orbit Speed).")]
    [SerializeField, Range(0.1f, 1f)] float strength = 1f;

    protected override void Awake()
    {
        base.Awake();

        if (orbit == null)
            orbit = FindAnyObjectByType<OrbitAroundTarget>();
    }

    void Update()
    {
        if (IsPressed && orbit != null)
            orbit.ApplyInput((orbitLeft ? -1f : 1f) * strength);
    }
}
