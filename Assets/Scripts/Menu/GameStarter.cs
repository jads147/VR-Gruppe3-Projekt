using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Startet das Spiel: Score zurücksetzen, Tutorial beenden + sperren, gespawnte Items entfernen,
/// gewähltes Level starten, Timer starten.
/// StartGame() über das OnClick-Event des Start-Buttons aufrufen.
///
/// Nach dem Eintragen des Scores ruft der <see cref="RoundEnder"/> ResetForNextRound() auf –
/// dann kann ohne Neuladen der Szene direkt die nächste Runde gestartet werden.
/// </summary>
public class GameStarter : MonoBehaviour
{
    [Tooltip("Der Jenga-Turm, der beim Start aktiviert wird. Wird ignoriert, sobald ein LevelManager " +
             "mit Leveln in der Szene ist – dann startet das gewählte Level.")]
    [SerializeField] private GameObject jengaTower;

    [Tooltip("Spawner des Tutorials. Wird beim Start gesperrt und ein laufendes Tutorial entfernt.")]
    [SerializeField] private TutorialSpawner tutorialSpawner;

    [Tooltip("Buttons, die während der Runde nicht klickbar sein sollen (z.B. Tutorial- und Start-Button).")]
    [SerializeField] private Selectable[] disableOnStart;

    [Tooltip("Zusätzliche Aktionen beim Start (optional).")]
    [SerializeField] private UnityEvent onGameStarted;

    [Tooltip("Zusätzliche Aktionen, wenn die Runde eingetragen wurde und die nächste starten kann (optional).")]
    [SerializeField] private UnityEvent onReadyForNextRound;

    public bool HasStarted { get; private set; }

    public void StartGame()
    {
        if (HasStarted)
            return;

        HasStarted = true;

        // Tutorial weg und sperren, damit keine Tutorial-Blöcke mehr erscheinen / Punkte geben.
        if (tutorialSpawner != null)
            tutorialSpawner.Lock();

        SetInteractable(false);

        // Im Menü/Tutorial gespawnte Items entfernen, sonst könnte man sie ansammeln und mit in die Runde nehmen.
        ItemSpawner.DestroyAllSpawned();

        // Punkte aus dem Tutorial verwerfen, Runde + Timer starten.
        if (GameManager.Instance != null)
            GameManager.Instance.StartRound();

        // Mit LevelManager wird das gewählte Level gestartet, sonst wie bisher der Jenga-Turm.
        var levels = LevelManager.Current;
        if (levels != null && levels.Levels.Count > 0)
            levels.StartSelectedLevel();
        else if (jengaTower != null)
            jengaTower.SetActive(true);

        onGameStarted?.Invoke();
    }

    /// <summary>Macht Start, Tutorial und Level-Auswahl wieder benutzbar.</summary>
    public void ResetForNextRound()
    {
        if (!HasStarted)
            return;

        HasStarted = false;

        if (tutorialSpawner != null)
            tutorialSpawner.Unlock();

        SetInteractable(true);
        onReadyForNextRound?.Invoke();
    }

    private void SetInteractable(bool interactable)
    {
        foreach (var selectable in disableOnStart)
        {
            if (selectable != null)
                selectable.interactable = interactable;
        }
    }
}
