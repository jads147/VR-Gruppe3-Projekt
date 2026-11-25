using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Legt bei jedem "Start Game" frische Exemplare von Items (z.B. der Wurfaxt) an genau die Stelle,
/// an der sie beim Laden der Szene lagen. Das alte Exemplar wird dabei entfernt – egal ob es geworfen,
/// verbraucht oder noch unberührt ist. So gibt es pro Versuch genau einen Wurf.
///
/// Setup: Dieses Skript auf ein Objekt legen, das immer aktiv bleibt (NICHT auf Start-/Menü-Buttons,
/// die während der Runde ausgeblendet werden), und die Items aus der Szene in "Items" ziehen.
/// Beim Laden wird von jedem Item eine deaktivierte Vorlage erzeugt; Änderungen am Item zur Laufzeit
/// (z.B. verbraucht, zerstört) haben also keinen Einfluss auf die nächste Kopie.
/// </summary>
public class RoundItemRespawner : MonoBehaviour
{
    [Tooltip("Items in der Szene, die bei jedem Start Game frisch an ihrer Startposition erscheinen.")]
    [SerializeField] private GameObject[] items;

    private sealed class Entry
    {
        public GameObject template;
        public GameObject current;
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public string name;
    }

    private readonly List<Entry> entries = new();
    private bool wasRoundActive;

    private void Awake()
    {
        foreach (var item in items)
        {
            if (item == null)
                continue;

            Transform t = item.transform;
            var entry = new Entry
            {
                current = item,
                parent = t.parent,
                localPosition = t.localPosition,
                localRotation = t.localRotation,
                localScale = t.localScale,
                name = item.name,
            };

            // Vorlage im deaktivierten Zustand kopieren, damit auf ihr kein Awake/OnEnable läuft
            // (keine Physik, kein Grab-Interactable, keine Selbstzerstörung).
            bool wasActive = item.activeSelf;
            item.SetActive(false);
            entry.template = Instantiate(item, transform);
            entry.template.name = item.name + " (Respawn Template)";
            item.SetActive(wasActive);

            entries.Add(entry);
        }

        wasRoundActive = GameManager.Instance != null && GameManager.Instance.IsRoundActive;
        GameManager.StateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        GameManager.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged()
    {
        bool roundActive = GameManager.Instance != null && GameManager.Instance.IsRoundActive;

        // Nur beim Übergang "keine Runde" -> "Runde läuft", also bei Start Game
        // (StateChanged kommt auch bei den ersten Punkten und beim Zurücksetzen).
        if (roundActive && !wasRoundActive)
            RespawnAll();

        wasRoundActive = roundActive;
    }

    /// <summary>Entfernt alle aktuellen Exemplare und legt frische an die Startpositionen.</summary>
    public void RespawnAll()
    {
        foreach (var entry in entries)
        {
            if (entry.current != null)
            {
                // Sofort deaktivieren (Destroy wirkt erst am Ende des Frames), lässt es auch aus der Hand fallen.
                entry.current.SetActive(false);
                Destroy(entry.current);
            }

            // Vorlage ist deaktiviert -> Kopie ebenfalls; erst Pose setzen, dann aktivieren.
            GameObject copy = Instantiate(entry.template, entry.parent);
            copy.name = entry.name;
            copy.transform.SetLocalPositionAndRotation(entry.localPosition, entry.localRotation);
            copy.transform.localScale = entry.localScale;
            copy.SetActive(true);

            entry.current = copy;
        }
    }
}
