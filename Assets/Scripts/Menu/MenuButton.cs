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

    [Tooltip("Sind Overlays offen (Bestenliste, Namenseingabe), schließt der Button zuerst diese. " +
             "Erst wenn keins mehr offen ist, wird das Menü getoggelt.")]
    [SerializeField] bool closeOverlaysFirst = true;

    [Tooltip("Menü ausblenden, sobald ein Overlay (Bestenliste, Namenseingabe) geöffnet wird.")]
    [SerializeField] bool hideMenuWhenOverlayOpens = true;

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

    protected override void OnEnable()
    {
        base.OnEnable();
        MenuOverlays.Opened += HandleOverlayOpened;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        MenuOverlays.Opened -= HandleOverlayOpened;
    }

    void HandleOverlayOpened(GameObject shownObject)
    {
        if (!hideMenuWhenOverlayOpens || menu == null)
            return;

        // Liegt das Overlay im Menü selbst, würde es mit ausgeblendet -> dann Menü lassen.
        if (shownObject != null && shownObject.transform.IsChildOf(menu.transform))
            return;

        menu.SetActive(false);
    }

    protected override void OnPressed() => ToggleMenu();

    /// <summary>Kann auch direkt aufgerufen werden (z.B. von einem UI-Button oder Controller-Input).</summary>
    public void ToggleMenu()
    {
        if (Time.unscaledTime - m_LastToggleTime < toggleCooldown)
            return;

        m_LastToggleTime = Time.unscaledTime;

        // Overlay offen? Dann schließen und zurück zum normalen Menü.
        if (closeOverlaysFirst && MenuOverlays.CloseAll())
        {
            if (menu != null)
                menu.SetActive(true);
            return;
        }

        if (menu != null)
            menu.SetActive(!menu.activeSelf);
    }
}
