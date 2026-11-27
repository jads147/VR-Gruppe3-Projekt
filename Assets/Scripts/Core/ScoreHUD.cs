using TMPro;
using UnityEngine;

/// <summary>
/// Zeigt den Score an. Während eine Runde läuft, zusätzlich die Max-Punkte des Levels ("Score: 30/50").
/// </summary>
public class ScoreHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;

    [Tooltip("Während einer Runde. {0} = Score, {1} = Max-Punkte des Levels.")]
    [SerializeField] private string roundFormat = "Score: {0}/{1}";

    [Tooltip("Ohne laufende Runde (z.B. Tutorial) oder ohne Level. {0} = Score.")]
    [SerializeField] private string format = "Score: {0}";

    private int m_LastScore = int.MinValue;
    private int m_LastMax = int.MinValue;

    private void Update()
    {
        var gameManager = GameManager.Instance;
        if (gameManager == null || scoreText == null)
            return;

        int score = gameManager.Score;
        var level = LevelManager.Current != null && LevelManager.Current.HasStarted
            ? LevelManager.Current.SelectedLevel
            : null;
        int max = level != null ? level.MaxScore : -1;

        // Text nur bei Änderung neu setzen (kein String-Müll pro Frame).
        if (score == m_LastScore && max == m_LastMax)
            return;

        m_LastScore = score;
        m_LastMax = max;
        scoreText.text = max > 0 ? string.Format(roundFormat, score, max) : string.Format(format, score);
    }
}
