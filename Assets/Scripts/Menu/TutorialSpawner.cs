using UnityEngine;

/// <summary>
/// Spawnt ein Prefab (z.B. das Tutorial) als Kind des angegebenen Parent-Objekts.
/// Spawn() ist public und kann z.B. über ein Button-Event getriggert werden.
/// </summary>
public class TutorialSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [Tooltip("Objekt, unter dem das Prefab gespawnt wird. Leer = dieses Objekt.")]
    [SerializeField] private Transform parent;
    [Tooltip("Ein bereits gespawntes Exemplar wird ersetzt, statt mehrere gleichzeitig zu haben.")]
    [SerializeField] private bool replaceExisting = true;

    private GameObject current;

    /// <summary>True, sobald das Spiel gestartet wurde. Wird erst durch einen Szenen-Reload zurückgesetzt.</summary>
    public bool IsLocked { get; private set; }

    /// <summary>Entfernt ein laufendes Tutorial und verhindert weitere Spawns (bis die Szene neu geladen wird).</summary>
    public void Lock()
    {
        IsLocked = true;

        if (current != null)
        {
            Destroy(current);
            current = null;
        }
    }

    public void Spawn()
    {
        if (IsLocked)
            return;

        if (prefab == null)
        {
            Debug.LogWarning($"{name}: Kein Prefab zugewiesen.", this);
            return;
        }

        if (replaceExisting && current != null)
            Destroy(current);

        Transform target = parent != null ? parent : transform;
        current = Instantiate(prefab, target, false);
    }
}
