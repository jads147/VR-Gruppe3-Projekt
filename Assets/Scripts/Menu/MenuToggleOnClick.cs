using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Togglet beim Klick zusätzlich das Menü des <see cref="MenuButton"/> (z.B. am Tutorial-Button,
/// damit das Menü nach dem Klick verschwindet). Der MenuButton wird zur Laufzeit gesucht –
/// funktioniert daher auch im MenuCanvas-Prefab.
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuToggleOnClick : MonoBehaviour
{
    [Tooltip("Optional. Leer = in der Szene suchen.")]
    [SerializeField] private MenuButton menuButton;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (menuButton == null)
            menuButton = FindAnyObjectByType<MenuButton>(FindObjectsInactive.Include);

        if (menuButton != null)
            menuButton.ToggleMenu();
        else
            Debug.LogWarning("[MenuToggleOnClick] Kein MenuButton in der Szene gefunden.", this);
    }
}
