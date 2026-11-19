using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Startet das Spiel: Score zurücksetzen, Tutorial beenden + sperren, Jenga-Turm aktivieren.
/// StartGame() über das OnClick-Event des Start-Buttons aufrufen.
///
/// Der Zustand lebt nur in der Szene: Erst ein Reload Level (SceneLoader.LoadScene)
/// setzt alles zurück, dann ist das Tutorial wieder verfügbar.
/// </summary>
public class GameStarter : MonoBehaviour
{
    [Tooltip("Der Jenga-Turm (bzw. das Level-Objekt), der beim Start aktiviert wird.")]
    [SerializeField] private GameObject jengaTower;

    [Tooltip("Spawner des Tutorials. Wird beim Start gesperrt und ein laufendes Tutorial entfernt.")]
    [SerializeField] private TutorialSpawner tutorialSpawner;

    [Tooltip("Buttons, die nach dem Start nicht mehr klickbar sein sollen (z.B. Tutorial- und Start-Button).")]
    [SerializeField] private Selectable[] disableOnStart;

    [Tooltip("Zusätzliche Aktionen beim Start (optional).")]
    [SerializeField] private UnityEvent onGameStarted;

    public bool HasStarted { get; private set; }

    public void StartGame()
    {
        if (HasStarted)
            return;

        HasStarted = true;

        // Tutorial weg und sperren, damit keine Tutorial-Blöcke mehr erscheinen / Punkte geben.
        if (tutorialSpawner != null)
            tutorialSpawner.Lock();

        foreach (var selectable in disableOnStart)
        {
            if (selectable != null)
                selectable.interactable = false;
        }

        // Punkte aus dem Tutorial verwerfen.
        if (GameManager.Instance != null)
            GameManager.Instance.ResetGame();

        if (jengaTower != null)
            jengaTower.SetActive(true);

        onGameStarted?.Invoke();
    }
}
