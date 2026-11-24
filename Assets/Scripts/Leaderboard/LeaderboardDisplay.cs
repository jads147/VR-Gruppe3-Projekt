using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Zeigt die Bestenliste eines Levels in einem TMP-Text an und aktualisiert sich automatisch bei neuen
/// Einträgen und beim Wechsel des Levels (<see cref="LevelManager"/>).
/// Der zuletzt eingetragene Score wird farbig hervorgehoben – liegt er außerhalb der angezeigten
/// Plätze, wird er zusätzlich unten angehängt.
/// </summary>
public class LeaderboardDisplay : MonoBehaviour, IMenuOverlay
{
    [Tooltip("Text, in den die Liste geschrieben wird.")]
    [SerializeField] TMP_Text listText;

    [Tooltip("Optional: Überschrift, zeigt den Namen des Levels.")]
    [SerializeField] TMP_Text titleText;

    [Tooltip("{0} = Name des Levels.")]
    [SerializeField] string titleFormat = "Leaderboard - {0}";

    [Tooltip("Überschrift, wenn es keine Level gibt.")]
    [SerializeField] string titleWithoutLevel = "Leaderboard";

    [Tooltip("Leer = Bestenliste des im Menü gewählten Levels. Sonst feste Level-ID (z.B. für eine Tafel pro Level).")]
    [SerializeField] string levelId;

    [Tooltip("Wie viele Plätze angezeigt werden.")]
    [SerializeField, Min(1)] int shownEntries = 10;

    [Tooltip("Spaltenüberschriften (# Name Time Score) anzeigen.")]
    [SerializeField] bool showHeader = true;

    [Tooltip("Zeit-Spalte anzeigen.")]
    [SerializeField] bool showTime = true;

    [Tooltip("Farbe für den zuletzt eingetragenen Score.")]
    [SerializeField] Color highlightColor = new(1f, 0.82f, 0.2f);

    [Tooltip("Text, wenn noch niemand eingetragen ist.")]
    [SerializeField] string emptyText = "No entries yet";

    [Header("Ein-/Ausblenden")]
    [Tooltip("Objekt, das von Show/Hide/Toggle ein- und ausgeschaltet wird. Leer = dieses GameObject.")]
    [SerializeField] GameObject visibleRoot;

    [Tooltip("Der MenuButton blendet die Bestenliste aus, bevor er das Menü togglet. " +
             "Aus, wenn sie dauerhaft sichtbar sein soll (z.B. als Tafel im Level).")]
    [SerializeField] bool closeWithMenuButton = true;

    // Spaltenpositionen
    const string NameColumn = "<pos=12%>";
    const string TimeColumn = "<pos=58%>";
    const string ScoreColumn = "<pos=82%>";

    readonly StringBuilder m_Builder = new();

    GameObject Root => visibleRoot != null ? visibleRoot : gameObject;

    /// <summary>Level, dessen Bestenliste gerade angezeigt wird.</summary>
    public string ShownLevelId => string.IsNullOrWhiteSpace(levelId) ? LevelManager.CurrentLevelId : levelId.Trim();

    /// <summary>Ist die Bestenliste gerade sichtbar?</summary>
    public bool IsVisible => Root.activeInHierarchy;

    /// <summary>Blendet die Bestenliste ein und aktualisiert sie.</summary>
    public void Show() => SetVisible(true);

    /// <summary>Blendet die Bestenliste aus.</summary>
    public void Hide() => SetVisible(false);

    /// <summary>Wechselt zwischen ein- und ausgeblendet.</summary>
    public void Toggle() => SetVisible(!Root.activeSelf);

    /// <summary>Für UnityEvents mit bool-Parameter (z.B. Toggle-UI).</summary>
    public void SetVisible(bool visible)
    {
        Root.SetActive(visible);
        // Liegt der Text außerhalb des ausgeblendeten Objekts, läuft OnEnable nicht -> selbst aktualisieren.
        if (visible)
        {
            Refresh();
            // Normales Menü ausblenden (nicht bei einer dauerhaften Tafel).
            if (closeWithMenuButton)
                MenuOverlays.NotifyOpened(Root);
        }
    }

    bool IMenuOverlay.IsOverlayOpen => closeWithMenuButton && IsVisible;
    void IMenuOverlay.CloseOverlay() => Hide();

    void Reset()
    {
        listText = GetComponentInChildren<TMP_Text>();
    }

    // Awake/OnDestroy statt OnEnable/OnDisable: bleibt registriert, auch während sie ausgeblendet ist.
    void Awake() => MenuOverlays.Register(this);

    void OnDestroy() => MenuOverlays.Unregister(this);

    void OnEnable()
    {
        Leaderboard.Changed += Refresh;
        LevelManager.SelectionChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        Leaderboard.Changed -= Refresh;
        LevelManager.SelectionChanged -= Refresh;
    }

    // Falls OnEnable vor dem Awake des LevelManagers lief.
    void Start() => Refresh();

    public void Refresh()
    {
        string shownLevel = ShownLevelId;
        UpdateTitle(shownLevel);

        if (listText == null)
            return;

        var entries = Leaderboard.GetEntries(shownLevel);
        if (entries.Count == 0)
        {
            listText.text = emptyText;
            return;
        }

        m_Builder.Clear();
        if (showHeader)
        {
            m_Builder.Append("<alpha=#88>#").Append(NameColumn).Append("Name");
            if (showTime)
                m_Builder.Append(TimeColumn).Append("Time");
            m_Builder.Append(ScoreColumn).Append("Score<alpha=#FF>\n");
        }

        string highlightHex = ColorUtility.ToHtmlStringRGB(highlightColor);
        var last = Leaderboard.LastSubmittedLevelId == shownLevel ? Leaderboard.LastSubmitted : null;
        bool lastShown = false;

        int count = Mathf.Min(shownEntries, entries.Count);
        for (int i = 0; i < count; i++)
        {
            bool isLast = entries[i] == last;
            lastShown |= isLast;
            AppendLine(i + 1, entries[i], isLast ? highlightHex : null);
        }

        // Eigener Platz liegt unterhalb der Top X? Trotzdem zeigen.
        if (last != null && !lastShown)
        {
            int rank = Leaderboard.GetRank(shownLevel, last);
            if (rank > 0)
            {
                m_Builder.Append("<align=center>...</align>\n");
                AppendLine(rank, last, highlightHex);
            }
        }

        listText.text = m_Builder.ToString().TrimEnd('\n');
    }

    void UpdateTitle(string shownLevel)
    {
        if (titleText == null)
            return;

        var manager = LevelManager.Current;
        var level = manager != null ? manager.FindLevel(shownLevel) : null;
        titleText.text = level != null ? string.Format(titleFormat, level.DisplayName) : titleWithoutLevel;
    }

    void AppendLine(int rank, LeaderboardEntry entry, string colorHex)
    {
        if (colorHex != null)
            m_Builder.Append("<color=#").Append(colorHex).Append('>');

        m_Builder.Append(rank).Append('.');
        m_Builder.Append(NameColumn).Append("<noparse>").Append(entry.name).Append("</noparse>");
        if (showTime)
            m_Builder.Append(TimeColumn).Append(entry.FormattedTime);
        m_Builder.Append(ScoreColumn).Append(entry.score);

        if (colorHex != null)
            m_Builder.Append("</color>");
        m_Builder.Append('\n');
    }
}
