using UnityEngine;

/// <summary>
/// Blendet dieses GameObject abhängig vom Rundenzustand (<see cref="GameManager"/>) ein und aus:
///  - <see cref="Mode.HiddenDuringRound"/>: z.B. Start Game, Tutorial, Level-Auswahl –
///    verschwinden bei "Start Game" und kommen nach dem Eintragen des Scores zurück.
///  - <see cref="Mode.AfterFirstPoints"/>: z.B. Finish Round – erscheint erst, wenn in der laufenden
///    Runde Punkte gemacht oder verloren wurden.
///
/// Abonniert in Awake (nicht OnEnable), damit das Objekt sich auch selbst wieder einblenden kann.
/// </summary>
public class RoundVisibility : MonoBehaviour
{
    public enum Mode
    {
        HiddenDuringRound,
        AfterFirstPoints,
    }

    [SerializeField] private Mode mode = Mode.HiddenDuringRound;

    private void Awake()
    {
        GameManager.StateChanged += Apply;
        Apply();
    }

    private void OnDestroy()
    {
        GameManager.StateChanged -= Apply;
    }

    private void Apply()
    {
        var gameManager = GameManager.Instance;
        bool roundActive = gameManager != null && gameManager.IsRoundActive;
        bool scored = gameManager != null && gameManager.HasScoredThisRound;

        bool visible = mode switch
        {
            Mode.HiddenDuringRound => !roundActive,
            Mode.AfterFirstPoints => roundActive && scored,
            _ => true,
        };

        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }
}
