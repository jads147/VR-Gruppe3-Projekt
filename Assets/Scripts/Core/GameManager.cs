using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>
    /// Rundenzustand hat sich geändert (Runde gestartet, erste Punkte, zurückgesetzt).
    /// Statisch, damit Szenen-Objekte sich auch abonnieren können, bevor die Instanz existiert.
    /// </summary>
    public static event Action StateChanged;

    public int Score { get; set; }

    /// <summary>Laufzeit der aktuellen Runde in Sekunden.</summary>
    public float ElapsedTime { get; private set; }

    public bool IsTimerRunning { get; private set; }

    /// <summary>True von "Start Game" bis die Runde eingetragen (oder die Szene neu geladen) wurde.</summary>
    public bool IsRoundActive { get; private set; }

    /// <summary>True, sobald in der laufenden Runde Punkte gemacht oder verloren wurden.</summary>
    public bool HasScoredThisRound { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        StateChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
        if (Instance != null)
            return;

        var gameManagerObject = new GameObject(nameof(GameManager));
        gameManagerObject.AddComponent<GameManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (IsTimerRunning)
            ElapsedTime += Time.deltaTime;
    }

    public void AddPoints(int points)
    {
        Score += points;

        if (IsRoundActive && !HasScoredThisRound)
        {
            HasScoredThisRound = true;
            StateChanged?.Invoke();
        }
    }

    /// <summary>Startet eine neue Runde: Score/Zeit auf 0, Timer läuft.</summary>
    public void StartRound()
    {
        Score = 0;
        HasScoredThisRound = false;
        IsRoundActive = true;
        StartTimer();
        StateChanged?.Invoke();
    }

    /// <summary>Startet den Timer bei 0.</summary>
    public void StartTimer()
    {
        ElapsedTime = 0f;
        IsTimerRunning = true;
    }

    /// <summary>Hält den Timer an (z.B. während der Namenseingabe).</summary>
    public void PauseTimer()
    {
        IsTimerRunning = false;
    }

    /// <summary>Lässt einen angehaltenen Timer weiterlaufen.</summary>
    public void ResumeTimer()
    {
        IsTimerRunning = true;
    }

    /// <summary>Beendet die Runde und setzt Score und Zeit zurück.</summary>
    public void ResetGame()
    {
        Score = 0;
        ElapsedTime = 0f;
        IsTimerRunning = false;
        IsRoundActive = false;
        HasScoredThisRound = false;
        StateChanged?.Invoke();
    }

}
