using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verwaltet die Level der Szene. Jedes Level ist ein (deaktiviertes) Objekt in der Szene, z.B. ein
/// TowerBuilder mit eigenen Einstellungen. "Start Game" (<see cref="GameStarter"/>) startet das gewählte Level.
///
/// Gespielt wird mit einer KOPIE des Level-Objekts: Das Original bleibt deaktiviert als Vorlage liegen.
/// Nach dem Eintragen des Scores wird die Kopie entfernt (<see cref="EndLevel"/>) – so startet jede Runde
/// mit einem frischen Turm, ohne die Szene neu zu laden.
///
/// "Reload Game" (<see cref="RoundReset"/>) entfernt die Kopie ebenfalls, ohne die Szene neu zu laden.
/// Die Auswahl bleibt auch über ein echtes Neuladen der Szene erhalten. Jede Level-ID hat ihre eigene Bestenliste –
/// die ID daher nach dem ersten Spielen nicht mehr ändern, sonst landen alte Einträge in einer "verwaisten" Liste.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Serializable]
    public class Level
    {
        [Tooltip("Eindeutige ID für die Bestenliste (z.B. \"level1\"). Leer = Name des Objekts. Später nicht mehr ändern!")]
        public string id;

        [Tooltip("Name im Menü und in der Bestenliste (z.B. \"Level 1\"). Leer = ID.")]
        public string displayName;

        [Tooltip("Objekt des Levels in der Szene. Wird beim Start deaktiviert und erst bei Start Game aktiviert.")]
        public GameObject root;

        public string Id => !string.IsNullOrWhiteSpace(id) ? id.Trim() : root != null ? root.name : Leaderboard.DefaultLevelId;
        public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : Id;
    }

    [SerializeField] private List<Level> levels = new();

    // Statisch, damit die Auswahl ein Neuladen der Szene überlebt.
    private static int s_SelectedIndex;

    /// <summary>Der LevelManager der aktuellen Szene (oder null).</summary>
    public static LevelManager Current { get; private set; }

    /// <summary>Ein anderes Level wurde ausgewählt.</summary>
    public static event Action SelectionChanged;

    /// <summary>Das gewählte Level wurde gestartet (Auswahl ist ab jetzt gesperrt).</summary>
    public static event Action LevelStarted;

    /// <summary>Das laufende Level wurde beendet (Auswahl ist wieder frei).</summary>
    public static event Action LevelEnded;

    /// <summary>ID des gewählten Levels – oder <see cref="Leaderboard.DefaultLevelId"/>, wenn es keine Level gibt.</summary>
    public static string CurrentLevelId => Current != null && Current.SelectedLevel != null
        ? Current.SelectedLevel.Id
        : Leaderboard.DefaultLevelId;

    public IReadOnlyList<Level> Levels => levels;
    public int SelectedIndex => levels.Count == 0 ? -1 : Mathf.Clamp(s_SelectedIndex, 0, levels.Count - 1);
    public Level SelectedLevel => levels.Count == 0 ? null : levels[SelectedIndex];
    public bool HasStarted { get; private set; }

    /// <summary>Die gerade gespielte Kopie des Levels (oder null).</summary>
    public GameObject ActiveLevelInstance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_SelectedIndex = 0;
        Current = null;
        SelectionChanged = null;
        LevelStarted = null;
        LevelEnded = null;
    }

    private void Awake()
    {
        Current = this;
        s_SelectedIndex = Mathf.Max(0, SelectedIndex);

        // Alle Level aus, auch wenn eins in der Szene versehentlich aktiv gelassen wurde.
        foreach (var level in levels)
        {
            if (level.root != null)
                level.root.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    /// <summary>Sucht ein Level anhand seiner ID.</summary>
    public Level FindLevel(string levelId)
    {
        foreach (var level in levels)
        {
            if (level.Id == levelId)
                return level;
        }
        return null;
    }

    public void Next() => Select(SelectedIndex + 1);

    public void Previous() => Select(SelectedIndex - 1);

    /// <summary>Wählt ein Level (mit Umlauf am Anfang/Ende). Nach dem Start gesperrt.</summary>
    public void Select(int index)
    {
        if (HasStarted || levels.Count == 0)
            return;

        int wrapped = (index % levels.Count + levels.Count) % levels.Count;
        if (wrapped == s_SelectedIndex)
            return;

        s_SelectedIndex = wrapped;
        SelectionChanged?.Invoke();
    }

    /// <summary>Aktiviert das gewählte Level. Wird von <see cref="GameStarter.StartGame"/> aufgerufen.</summary>
    public void StartSelectedLevel()
    {
        if (HasStarted || SelectedLevel == null)
            return;

        HasStarted = true;

        GameObject template = SelectedLevel.root;
        if (template != null)
        {
            // Kopie an derselben Stelle der Hierarchie (orbitet also z.B. mit ARLevel mit).
            ActiveLevelInstance = Instantiate(template, template.transform.parent);
            ActiveLevelInstance.name = template.name + " (Running)";
            ActiveLevelInstance.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[LevelManager] Level \"{SelectedLevel.DisplayName}\" hat kein Objekt zugewiesen.", this);
        }

        LevelStarted?.Invoke();
    }

    /// <summary>Entfernt die gespielte Kopie und gibt die Level-Auswahl wieder frei.</summary>
    public void EndLevel()
    {
        if (!HasStarted)
            return;

        HasStarted = false;
        if (ActiveLevelInstance != null)
        {
            // Sofort deaktivieren (Destroy wirkt erst am Ende des Frames).
            ActiveLevelInstance.SetActive(false);
            Destroy(ActiveLevelInstance);
            ActiveLevelInstance = null;
        }

        LevelEnded?.Invoke();
    }

    private void OnValidate()
    {
        var seen = new HashSet<string>();
        foreach (var level in levels)
        {
            if (level != null && !seen.Add(level.Id))
                Debug.LogWarning($"[LevelManager] Level-ID \"{level.Id}\" ist doppelt – die Level würden sich eine Bestenliste teilen.", this);
        }
    }
}
