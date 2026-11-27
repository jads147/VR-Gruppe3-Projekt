using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level-Auswahl im Menü: "&lt;  Level 1  &gt;". Blättert durch die Level des <see cref="LevelManager"/>.
/// Nach "Start Game" sind die Pfeile gesperrt.
///
/// Gesperrte Level (Max-Punkte im Level davor fehlen noch) kann man ansehen, aber nicht starten –
/// sie werden mit "Locked Format" angezeigt, optional mit Hinweistext und Schloss-Symbol.
///
/// Der LevelManager wird zur Laufzeit gefunden – funktioniert daher auch im MenuCanvas-Prefab.
/// Am einfachsten über GameObject > Bestenliste > Level-Auswahl anlegen.
/// </summary>
public class LevelSelector : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Tooltip("Freigeschaltetes Level. {0} = Name, {1} = Bestwert, {2} = Max-Punkte des Levels.")]
    [SerializeField] private string format = "{0}";

    [Tooltip("Gesperrtes Level. {0} = Name, {1} = Bestwert im Level davor, {2} = benötigte Punkte im Level davor, " +
             "{3} = Name des Levels davor.")]
    [SerializeField] private string lockedFormat = "{0} (Locked)";

    [Header("Optional: Hinweis bei gesperrtem Level")]
    [Tooltip("Eigener Text für den Hinweis. Wird nur bei gesperrtem Level eingeblendet.")]
    [SerializeField] private TMP_Text lockedHint;

    [Tooltip("{0} = Bestwert im Level davor, {1} = benötigte Punkte, {2} = Name des Levels davor.")]
    [SerializeField] private string lockedHintFormat = "Get all {1} points in {2} to unlock ({0}/{1})";

    [Tooltip("Z.B. ein Schloss-Symbol. Wird nur bei gesperrtem Level eingeblendet.")]
    [SerializeField] private GameObject lockedIndicator;

    [Tooltip("Text, wenn es keinen LevelManager / keine Level gibt.")]
    [SerializeField] private string noLevelsText = "-";

    private void Awake()
    {
        if (previousButton != null)
            previousButton.onClick.AddListener(() => LevelManager.Current?.Previous());
        if (nextButton != null)
            nextButton.onClick.AddListener(() => LevelManager.Current?.Next());
    }

    private void OnEnable()
    {
        LevelManager.SelectionChanged += UpdateView;
        LevelManager.LevelStarted += UpdateView;
        LevelManager.LevelEnded += UpdateView;
        // Neuer Eintrag in der Bestenliste kann das nächste Level freischalten.
        Leaderboard.Changed += UpdateView;
        UpdateView();
    }

    private void OnDisable()
    {
        LevelManager.SelectionChanged -= UpdateView;
        LevelManager.LevelStarted -= UpdateView;
        LevelManager.LevelEnded -= UpdateView;
        Leaderboard.Changed -= UpdateView;
    }

    // Falls OnEnable vor dem Awake des LevelManagers lief.
    private void Start() => UpdateView();

    private void UpdateView()
    {
        var manager = LevelManager.Current;
        var level = manager != null ? manager.SelectedLevel : null;
        bool locked = level != null && !manager.IsSelectedUnlocked;
        var previous = locked ? manager.GetPreviousLevel(manager.SelectedIndex) : null;

        if (label != null)
        {
            if (level == null)
                label.text = noLevelsText;
            else if (locked && previous != null)
                label.text = string.Format(lockedFormat, level.DisplayName, previous.BestScore,
                    previous.RequiredScore, previous.DisplayName);
            else
                label.text = string.Format(format, level.DisplayName, level.BestScore, level.MaxScore);
        }

        if (lockedHint != null)
        {
            lockedHint.gameObject.SetActive(locked && previous != null);
            if (locked && previous != null)
                lockedHint.text = string.Format(lockedHintFormat, previous.BestScore, previous.RequiredScore,
                    previous.DisplayName);
        }

        if (lockedIndicator != null)
            lockedIndicator.SetActive(locked);

        bool canBrowse = manager != null && !manager.HasStarted && manager.Levels.Count > 1;
        if (previousButton != null)
            previousButton.interactable = canBrowse;
        if (nextButton != null)
            nextButton.interactable = canBrowse;
    }
}
