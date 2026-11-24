using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level-Auswahl im Menü: "&lt;  Level 1  &gt;". Blättert durch die Level des <see cref="LevelManager"/>.
/// Nach "Start Game" sind die Pfeile gesperrt.
///
/// Der LevelManager wird zur Laufzeit gefunden – funktioniert daher auch im MenuCanvas-Prefab.
/// Am einfachsten über GameObject > Bestenliste > Level-Auswahl anlegen.
/// </summary>
public class LevelSelector : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Tooltip("{0} = Name des Levels.")]
    [SerializeField] private string format = "{0}";

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
        UpdateView();
    }

    private void OnDisable()
    {
        LevelManager.SelectionChanged -= UpdateView;
        LevelManager.LevelStarted -= UpdateView;
        LevelManager.LevelEnded -= UpdateView;
    }

    // Falls OnEnable vor dem Awake des LevelManagers lief.
    private void Start() => UpdateView();

    private void UpdateView()
    {
        var manager = LevelManager.Current;
        var level = manager != null ? manager.SelectedLevel : null;

        if (label != null)
            label.text = level != null ? string.Format(format, level.DisplayName) : noLevelsText;

        bool canBrowse = manager != null && !manager.HasStarted && manager.Levels.Count > 1;
        if (previousButton != null)
            previousButton.interactable = canBrowse;
        if (nextButton != null)
            nextButton.interactable = canBrowse;
    }
}
