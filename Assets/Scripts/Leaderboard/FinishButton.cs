using UnityEngine;

/// <summary>
/// 3D-Finish-Button: Beim Drücken (Hover Entered) fährt die Kappe nach unten und die Runde wird über
/// <see cref="RoundEnder.EndRound"/> beendet – Score einfrieren, Namenseingabe öffnen, in die Bestenliste
/// eintragen. Das physische Gegenstück zum UI-<see cref="EndRoundButton"/>.
/// Setup siehe <see cref="PressableButton"/> (Modell: FinishButton.fbx, Cap = FinishButton_Cap).
/// </summary>
public class FinishButton : PressableButton
{
    [Header("Runde beenden")]
    [Tooltip("Optional. Leer = beim Drücken in der Szene suchen (auch inaktive Objekte).")]
    [SerializeField] RoundEnder roundEnder;

    [Tooltip("Mindestzeit zwischen zwei Auslösungen, damit ein zitternder Ray nicht mehrfach auslöst.")]
    [SerializeField] float cooldown = 0.5f;

    float m_LastPressTime = float.NegativeInfinity;

    protected override void OnPressed() => EndRound();

    /// <summary>Kann auch direkt aufgerufen werden (z.B. von einem UnityEvent).</summary>
    public void EndRound()
    {
        if (Time.unscaledTime - m_LastPressTime < cooldown)
            return;

        m_LastPressTime = Time.unscaledTime;

        if (roundEnder == null)
            roundEnder = FindAnyObjectByType<RoundEnder>(FindObjectsInactive.Include);

        if (roundEnder == null)
        {
            Debug.LogWarning("[FinishButton] Kein RoundEnder in der Szene gefunden.", this);
            return;
        }

        roundEnder.EndRound();
    }
}
