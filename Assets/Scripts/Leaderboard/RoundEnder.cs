using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Beendet eine Runde: Der aktuelle Score wird eingefroren, die Namens-Tastatur geöffnet und nach
/// Bestätigung in die <see cref="Leaderboard"/> eingetragen. Danach wird der Score zurückgesetzt.
///
/// EndRound() über das OnClick-Event eines "Runde beenden"-Buttons aufrufen
/// (oder von jedem anderen Trigger, z.B. Timer oder "Turm umgefallen").
///
/// Runde beenden geht nur, während eine Runde läuft (nach "Start Game"). Nach dem Eintragen wird die
/// gespielte Level-Kopie entfernt und der GameStarter zurückgesetzt – die nächste Runde kann ohne
/// Neuladen der Szene direkt gestartet werden.
/// </summary>
public class RoundEnder : MonoBehaviour, IMenuOverlay
{
    [Header("Referenzen")]
    [Tooltip("Panel mit der Namens-Tastatur. Wird beim Start versteckt und bei EndRound() gezeigt.")]
    [SerializeField] GameObject namePanel;

    [SerializeField] NameKeyboard keyboard;

    [Tooltip("Optional: zeigt 'Score: 120 · Time: 1:23.4 · Rank 3'.")]
    [SerializeField] TMP_Text scoreInfoText;

    [Tooltip("Wird nach dem Eintragen für die nächste Runde zurückgesetzt. Leer = in der Szene suchen.")]
    [SerializeField] GameStarter gameStarter;

    [Header("Platzierung")]
    [Tooltip("Aus = Panel erscheint dort, wo es in der Szene steht. " +
             "An = Panel wird beim Öffnen vor die Kamera des Spielers gesetzt.")]
    [SerializeField] bool placeInFrontOfCamera = false;

    [Tooltip("Abstand zur Kamera in Metern.")]
    [SerializeField, Min(0.3f)] float distance = 1.2f;

    [Tooltip("Höhe relativ zur Augenhöhe in Metern (negativ = etwas tiefer).")]
    [SerializeField] float heightOffset = -0.2f;

    [Tooltip("Breite des Panels in Metern.")]
    [SerializeField, Min(0.1f)] float panelWidth = 1.1f;

    [Header("Events")]
    [Tooltip("Tastatur wurde geöffnet (z.B. Menü ausblenden).")]
    public UnityEvent onRoundEnded;

    [Tooltip("Score wurde eingetragen. Die Runde ist dann schon zurückgesetzt – hier KEINE Szene neu laden.")]
    public UnityEvent onScoreSubmitted;

    [Tooltip("Eingabe wurde abgebrochen, die Runde läuft weiter.")]
    public UnityEvent onCancelled;

    int m_FinalScore;
    float m_FinalTime;
    string m_LevelId;
    bool m_TimerWasRunning;

    public bool IsEnteringName { get; private set; }

    /// <summary>Platz des zuletzt eingetragenen Scores (0 = noch nichts eingetragen).</summary>
    public int LastRank { get; private set; }

    bool m_Initialized;

    void Awake()
    {
        Initialize();

        if (namePanel != null && !IsEnteringName)
            namePanel.SetActive(false);
    }

    // Wird auch aus EndRound() aufgerufen, falls das GameObject in der Szene deaktiviert ist
    // (dann läuft Awake erst beim Aktivieren).
    void Initialize()
    {
        if (m_Initialized)
            return;
        m_Initialized = true;

        if (keyboard == null && namePanel != null)
            keyboard = namePanel.GetComponentInChildren<NameKeyboard>(true);

        if (keyboard != null)
        {
            keyboard.onSubmitted.AddListener(Submit);
            keyboard.onCancelled.AddListener(Cancel);
        }

        MenuOverlays.Register(this);
    }

    void OnDestroy()
    {
        MenuOverlays.Unregister(this);

        if (keyboard != null)
        {
            keyboard.onSubmitted.RemoveListener(Submit);
            keyboard.onCancelled.RemoveListener(Cancel);
        }
    }

    // MenuButton schließt die Namenseingabe wie "Cancel" – die Runde läuft weiter.
    bool IMenuOverlay.IsOverlayOpen => IsEnteringName;
    void IMenuOverlay.CloseOverlay() => Cancel();

    public void EndRound()
    {
        if (IsEnteringName || GameManager.Instance == null)
            return;

        if (!GameManager.Instance.IsRoundActive)
        {
            Debug.Log("[RoundEnder] Es läuft keine Runde (erst \"Start Game\" drücken).", this);
            return;
        }

        Initialize();
        var gameManager = GameManager.Instance;
        m_FinalScore = gameManager.Score;
        m_FinalTime = gameManager.ElapsedTime;
        m_LevelId = LevelManager.CurrentLevelId;
        // Zeit beim Tippen nicht mitzählen; bei "Cancel" läuft sie weiter.
        m_TimerWasRunning = gameManager.IsTimerRunning;
        gameManager.PauseTimer();
        IsEnteringName = true;

        // Ist "Rundenende" (oder ein Eltern-Objekt des Panels) deaktiviert, wäre das Panel unsichtbar.
        ActivateHierarchy(transform);
        if (namePanel != null && namePanel.transform.parent != null)
            ActivateHierarchy(namePanel.transform.parent);

        if (scoreInfoText != null)
            scoreInfoText.text = $"Score: {m_FinalScore}  ·  Time: {Leaderboard.FormatTime(m_FinalTime)}  ·  " +
                                 $"Rank {Leaderboard.GetRank(m_LevelId, m_FinalScore, m_FinalTime)}";

        if (keyboard != null)
            keyboard.Clear();
        if (namePanel != null)
        {
            if (placeInFrontOfCamera)
                PlacePanelInFrontOfCamera();
            namePanel.SetActive(true);
        }

        Debug.Log($"[RoundEnder] Runde beendet (Level {m_LevelId}, Score {m_FinalScore}, " +
                  $"Zeit {Leaderboard.FormatTime(m_FinalTime)}) – Namenseingabe geöffnet.", this);
        // Normales Menü ausblenden, damit es nicht hinter der Tastatur stört.
        MenuOverlays.NotifyOpened(namePanel != null ? namePanel : gameObject);
        onRoundEnded?.Invoke();
    }

    static void ActivateHierarchy(Transform t)
    {
        for (; t != null && !t.gameObject.activeInHierarchy; t = t.parent)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
        }
    }

    void PlacePanelInFrontOfCamera()
    {
        Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("[RoundEnder] Keine Kamera gefunden – Panel bleibt an seiner Position.", this);
            return;
        }

        Transform panel = namePanel.transform;
        Transform head = cam.transform;

        // Aus der (rotierenden) Level-Hierarchie lösen, damit das Panel beim Orbiten stehen bleibt.
        panel.SetParent(null, true);

        // Nur waagerecht vor den Spieler, damit das Panel nicht schräg im Raum hängt.
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f)
            forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
        forward.Normalize();

        panel.SetPositionAndRotation(
            head.position + forward * distance + Vector3.up * heightOffset,
            Quaternion.LookRotation(forward, Vector3.up));

        if (panel is RectTransform rect && rect.rect.width > 0f)
            panel.localScale = Vector3.one * (panelWidth / rect.rect.width);
    }

    void Submit(string playerName)
    {
        if (!IsEnteringName)
            return;

        IsEnteringName = false;
        LastRank = Leaderboard.Submit(m_LevelId, playerName, m_FinalScore, m_FinalTime);

        if (namePanel != null)
            namePanel.SetActive(false);

        // Bereit für die nächste Runde: Level-Kopie + Items weg, Score/Timer auf 0 – ohne Szenen-Reload.
        RoundReset.ResetRound(gameStarter);

        onScoreSubmitted?.Invoke();
    }

    /// <summary>Schließt eine offene Namenseingabe ohne Eintrag und ohne den Timer fortzusetzen (z.B. bei "Reload Game").</summary>
    public void Abort()
    {
        if (!IsEnteringName)
            return;

        IsEnteringName = false;
        if (namePanel != null)
            namePanel.SetActive(false);
    }

    void Cancel()
    {
        if (!IsEnteringName)
            return;

        IsEnteringName = false;
        if (m_TimerWasRunning && GameManager.Instance != null)
            GameManager.Instance.ResumeTimer();
        if (namePanel != null)
            namePanel.SetActive(false);

        onCancelled?.Invoke();
    }
}
