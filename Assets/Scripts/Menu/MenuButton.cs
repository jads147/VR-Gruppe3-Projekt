using UnityEngine;

/// <summary>
/// 3D-Menü-Button: Beim Hover Entered fährt die Kappe nach unten und das Menü wird
/// ein-/ausgeschaltet. Beim Hover Exited fährt die Kappe wieder hoch.
/// Setup siehe <see cref="PressableButton"/>. "Menu" = Menü-Objekt in der Szene
/// (oder das Prefab, dann wird es beim Start instanziert).
/// </summary>
public class MenuButton : PressableButton
{
    [Header("Menü")]
    [Tooltip("Menü, das ein-/ausgeschaltet wird. Szenen-Objekt oder Prefab.")]
    [SerializeField] GameObject menu;

    [Tooltip("Ist das Menü beim Start sichtbar?")]
    [SerializeField] bool menuVisibleOnStart = false;

    [Tooltip("Mindestzeit zwischen zwei Toggles, damit ein zitternder Ray das Menü nicht flackern lässt.")]
    [SerializeField] float toggleCooldown = 0.5f;

    float m_LastToggleTime = float.NegativeInfinity;

    protected override void Awake()
    {
        base.Awake();

        // Prefab-Asset statt Szenen-Objekt zugewiesen? Dann eine Instanz erzeugen.
        if (menu != null && !menu.scene.IsValid())
            menu = Instantiate(menu);

        if (menu != null)
            menu.SetActive(menuVisibleOnStart);
    }

    protected override void OnPressed() => ToggleMenu();

    /// <summary>Kann auch direkt aufgerufen werden (z.B. von einem UI-Button oder Controller-Input).</summary>
    public void ToggleMenu()
    {
        if (menu == null || Time.unscaledTime - m_LastToggleTime < toggleCooldown)
            return;

        m_LastToggleTime = Time.unscaledTime;
        menu.SetActive(!menu.activeSelf);
    }
}
