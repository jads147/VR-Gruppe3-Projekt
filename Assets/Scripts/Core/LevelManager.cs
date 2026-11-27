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
///
/// Freischalten: Ein Level ist erst spielbar, wenn in den Leveln davor die Max-Punkte (bzw. "Required Score")
/// erreicht und in die Bestenliste eingetragen wurden. Gilt als Bestwert der Platz 1 der Bestenliste – wird
/// die Bestenliste gelöscht, sind die Level also wieder gesperrt.
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

        [Tooltip("Punkte, die man in diesem Level erreichen muss, damit das nächste Level freigeschaltet wird. " +
                 "0 = automatisch: Summe aller positiven Punkte-Blöcke (Point_OBJ) im Level-Objekt (= Max-Punkte).")]
        [Min(0)] public int requiredScore;

        [NonSerialized] private int m_AutoMaxScore;
        [NonSerialized] private bool m_AutoMaxScoreCached;

        public string Id => !string.IsNullOrWhiteSpace(id) ? id.Trim() : root != null ? root.name : Leaderboard.DefaultLevelId;
        public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : Id;

        /// <summary>Punkte, die man zum Freischalten des nächsten Levels braucht (Standard: Max-Punkte des Levels).</summary>
        public int RequiredScore => requiredScore > 0 ? requiredScore : MaxScore;

        /// <summary>Max-Punkte des Levels: Summe aller positiven Punkte-Blöcke (Point_OBJ) im Level-Objekt.</summary>
        public int MaxScore
        {
            get
            {
                // Die Vorlage ändert sich zur Laufzeit nicht (gespielt wird mit einer Kopie) -> einmal zählen reicht.
                if (!m_AutoMaxScoreCached || !Application.isPlaying)
                {
                    m_AutoMaxScore = CountMaxScore(root);
                    m_AutoMaxScoreCached = Application.isPlaying;
                }
                return m_AutoMaxScore;
            }
        }

        /// <summary>Bester eingetragener Score dieses Levels (0, falls noch nie gespielt).</summary>
        public int BestScore
        {
            get
            {
                var entries = Leaderboard.GetEntries(Id);
                return entries.Count > 0 ? entries[0].score : 0;
            }
        }

        /// <summary>True, wenn in diesem Level schon ein Score mit mindestens <see cref="RequiredScore"/> eingetragen wurde.</summary>
        public bool IsCompleted
        {
            get
            {
                var entries = Leaderboard.GetEntries(Id);
                return entries.Count > 0 && entries[0].score >= RequiredScore;
            }
        }

        private static int CountMaxScore(GameObject levelRoot)
        {
            if (levelRoot == null)
                return 0;

            int sum = 0;
            foreach (var pointBlock in levelRoot.GetComponentsInChildren<Point_OBJ>(true))
            {
                if (pointBlock.Points > 0)
                    sum += pointBlock.Points;
            }
            return sum;
        }
    }

    [SerializeField] private List<Level> levels = new();

    [Tooltip("Zum Testen: alle Level sind spielbar, egal welche Punkte erreicht wurden.")]
    [SerializeField] private bool unlockAllLevels = false;

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

    /// <summary>True, wenn das gewählte Level gespielt werden darf.</summary>
    public bool IsSelectedUnlocked => IsUnlocked(SelectedIndex);

    /// <summary>
    /// Ein Level ist freigeschaltet, wenn in ALLEN Leveln davor die nötigen Punkte (Standard: Max-Punkte)
    /// erreicht und eingetragen wurden. Das erste Level ist immer frei.
    /// </summary>
    public bool IsUnlocked(int index)
    {
        if (index < 0 || index >= levels.Count)
            return false;
        if (unlockAllLevels)
            return true;

        for (int i = 0; i < index; i++)
        {
            if (!levels[i].IsCompleted)
                return false;
        }
        return true;
    }

    /// <summary>Das Level vor dem angegebenen (dessen Punkte zum Freischalten fehlen), sonst null.</summary>
    public Level GetPreviousLevel(int index) => index > 0 && index <= levels.Count ? levels[index - 1] : null;

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

        if (!IsSelectedUnlocked)
        {
            Debug.Log($"[LevelManager] Level \"{SelectedLevel.DisplayName}\" ist noch gesperrt.", this);
            return;
        }

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
