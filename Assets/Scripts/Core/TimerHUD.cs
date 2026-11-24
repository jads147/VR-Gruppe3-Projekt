using TMPro;
using UnityEngine;

/// <summary>
/// Zeigt die Rundenzeit aus dem <see cref="GameManager"/> an (wie <see cref="ScoreHUD"/> für den Score).
/// Der Text bleibt unsichtbar, bis "Start Game" den Timer startet.
/// </summary>
public class TimerHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;

    [Tooltip("{0} = Zeit, z.B. \"Time: {0}\".")]
    [SerializeField] private string format = "Time: {0}";

    [Tooltip("Text erst anzeigen, wenn der Timer läuft (bzw. gelaufen ist).")]
    [SerializeField] private bool hideBeforeStart = true;

    private void Reset()
    {
        timerText = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        var gameManager = GameManager.Instance;
        if (gameManager == null || timerText == null)
            return;

        bool visible = !hideBeforeStart || gameManager.IsTimerRunning || gameManager.ElapsedTime > 0f;
        if (timerText.enabled != visible)
            timerText.enabled = visible;

        if (visible)
            timerText.text = string.Format(format, Leaderboard.FormatTime(gameManager.ElapsedTime));
    }
}
