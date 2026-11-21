using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public enum SlotSymbol { Seven, Cherries, Bar, Bell, Dollar, Lemon, Diamond, Star }

/// <summary>
/// Slotmaschine mit 3 Walzen à 8 Symbolen (45° pro Symbol).
/// Spin() dreht alle Walzen, sie stoppen nacheinander auf einem zufälligen Symbol,
/// danach wird die Gewinnlinie ausgewertet.
///
/// Setup:
///  - Script auf das Root der Slotmaschine legen.
///  - Reels: SM_Reel_1, SM_Reel_2, SM_Reel_3 (in dieser Reihenfolge) zuweisen.
///    Die Walzen müssen im Mittelpunkt gepivotet sein und in Ruhe die 7 auf der Gewinnlinie zeigen.
///  - Am PullLever bei "On Pulled" -> SlotMachine.Spin verknüpfen.
///  - Läuft die Walze falsch herum (falsches Symbol landet auf der Linie): "Invert Direction" anhaken.
///  - Zum Testen ohne VR: Rechtsklick auf die Komponente -> "Spin".
/// </summary>
public class SlotMachine : MonoBehaviour
{
    [Serializable]
    public struct Payout
    {
        public SlotSymbol symbol;
        [Tooltip("Punkte für 3 gleiche Symbole.")]
        public int threeOfAKind;
    }

    // Reihenfolge auf den Walzen: Index 0 = auf der Gewinnlinie, dann nach oben.
    static readonly SlotSymbol[][] Strips =
    {
        new[] { SlotSymbol.Seven, SlotSymbol.Cherries, SlotSymbol.Bar, SlotSymbol.Bell, SlotSymbol.Dollar, SlotSymbol.Lemon, SlotSymbol.Diamond, SlotSymbol.Star },
        new[] { SlotSymbol.Seven, SlotSymbol.Bell, SlotSymbol.Star, SlotSymbol.Cherries, SlotSymbol.Lemon, SlotSymbol.Bar, SlotSymbol.Dollar, SlotSymbol.Diamond },
        new[] { SlotSymbol.Seven, SlotSymbol.Diamond, SlotSymbol.Lemon, SlotSymbol.Star, SlotSymbol.Bar, SlotSymbol.Cherries, SlotSymbol.Bell, SlotSymbol.Dollar },
    };

    [Header("Walzen")]
    [Tooltip("SM_Reel_1, SM_Reel_2, SM_Reel_3")]
    [SerializeField] Transform[] reels = new Transform[3];

    [Tooltip("Drehachse lokal zur Walze. (1,0,0) = X-Achse.")]
    [SerializeField] Vector3 localAxis = Vector3.right;

    [Tooltip("Anhaken, wenn die Walzen in die falsche Richtung laufen.")]
    [SerializeField] bool invertDirection;

    [Tooltip("Winkel pro Symbol in Grad.")]
    [SerializeField] float symbolAngle = 45f;

    [Header("Spin")]
    [Tooltip("Dauer, bis die erste Walze stoppt (Sekunden).")]
    [SerializeField] float spinDuration = 2f;

    [Tooltip("Jede weitere Walze stoppt so viel später (Sekunden).")]
    [SerializeField] float reelStopDelay = 0.5f;

    [Tooltip("Volle Umdrehungen, die jede Walze mindestens macht.")]
    [SerializeField] int extraSpins = 3;

    [Tooltip("Kleines Überschwingen beim Stoppen (Grad). 0 = aus.")]
    [SerializeField] float bounceAngle = 4f;

    [SerializeField] float bounceDuration = 0.15f;

    [Header("Gewinne")]
    [SerializeField] Payout[] payouts =
    {
        new Payout { symbol = SlotSymbol.Seven,    threeOfAKind = 100 },
        new Payout { symbol = SlotSymbol.Diamond,  threeOfAKind = 75 },
        new Payout { symbol = SlotSymbol.Star,     threeOfAKind = 50 },
        new Payout { symbol = SlotSymbol.Dollar,   threeOfAKind = 40 },
        new Payout { symbol = SlotSymbol.Bar,      threeOfAKind = 30 },
        new Payout { symbol = SlotSymbol.Bell,     threeOfAKind = 20 },
        new Payout { symbol = SlotSymbol.Cherries, threeOfAKind = 15 },
        new Payout { symbol = SlotSymbol.Lemon,    threeOfAKind = 10 },
    };

    [Tooltip("Punkte für genau 2 Kirschen auf der Linie.")]
    [SerializeField] int twoCherries = 5;

    [Tooltip("Punkte für genau 1 Kirsche auf der Linie.")]
    [SerializeField] int oneCherry = 2;

    [Tooltip("Gewinn automatisch zum GameManager-Score addieren.")]
    [SerializeField] bool addToScore = true;

    [Header("Audio (optional)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip reelStopClip;
    [SerializeField] AudioClip winClip;
    [SerializeField] AudioClip loseClip;

    [Header("Events")]
    public UnityEvent onSpinStarted;
    [Tooltip("Wird mit dem Gewinn (Punkte) aufgerufen.")]
    public UnityEvent<int> onWin;
    public UnityEvent onLose;

    public bool IsSpinning { get; private set; }

    /// <summary>Symbole auf der Gewinnlinie nach dem letzten Spin.</summary>
    public SlotSymbol[] LastResult { get; private set; } = new SlotSymbol[3];

    Quaternion[] m_RestRotations;
    float[] m_Angles;
    int m_ReelsRunning;

    void Awake()
    {
        if (reels.Length > Strips.Length)
            Debug.LogWarning($"{name}: Es gibt nur {Strips.Length} Walzen-Belegungen, überzählige Reels werden ignoriert.", this);

        m_RestRotations = new Quaternion[reels.Length];
        m_Angles = new float[reels.Length];
        for (int i = 0; i < reels.Length; i++)
        {
            if (reels[i] != null)
                m_RestRotations[i] = reels[i].localRotation;
        }
    }

    [ContextMenu("Spin")]
    public void Spin()
    {
        if (IsSpinning || !Application.isPlaying)
            return;
        StartCoroutine(SpinRoutine());
    }

    IEnumerator SpinRoutine()
    {
        IsSpinning = true;
        onSpinStarted.Invoke();

        int count = Mathf.Min(reels.Length, Strips.Length);
        var result = new SlotSymbol[count];
        m_ReelsRunning = count;

        for (int i = 0; i < count; i++)
        {
            int index = UnityEngine.Random.Range(0, Strips[i].Length);
            result[i] = Strips[i][index];
            StartCoroutine(SpinReel(i, index, spinDuration + i * reelStopDelay));
        }

        yield return new WaitUntil(() => m_ReelsRunning == 0);

        LastResult = result;
        int win = Evaluate(result);
        Debug.Log($"{name}: {string.Join(" | ", result)} -> {win} Punkte", this);

        if (win > 0)
        {
            if (addToScore && GameManager.Instance != null)
                GameManager.Instance.AddPoints(win);
            PlayClip(winClip);
            onWin.Invoke(win);
        }
        else
        {
            PlayClip(loseClip);
            onLose.Invoke();
        }

        IsSpinning = false;
    }

    IEnumerator SpinReel(int i, int targetIndex, float duration)
    {
        float start = m_Angles[i];
        // Kürzester Weg vorwärts zum Zielsymbol + volle Extra-Umdrehungen
        float delta = Mathf.Repeat(targetIndex * symbolAngle - Mathf.Repeat(start, 360f), 360f);
        float end = start + extraSpins * 360f + delta;
        float overshoot = end + bounceAngle;

        // Schnell starten, weich abbremsen (Ease-Out Cubic)
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / duration, 3f);
            ApplyAngle(i, Mathf.LerpUnclamped(start, overshoot, k));
            yield return null;
        }

        // Kleines Zurückfedern aufs Symbol
        for (float t = 0f; t < bounceDuration; t += Time.deltaTime)
        {
            ApplyAngle(i, Mathf.Lerp(overshoot, end, Mathf.SmoothStep(0f, 1f, t / bounceDuration)));
            yield return null;
        }

        m_Angles[i] = Mathf.Repeat(end, 360f);
        ApplyAngle(i, m_Angles[i]);
        PlayClip(reelStopClip);
        m_ReelsRunning--;
    }

    void ApplyAngle(int i, float angle)
    {
        if (reels[i] == null)
            return;
        float a = invertDirection ? -angle : angle;
        reels[i].localRotation = m_RestRotations[i] * Quaternion.AngleAxis(a, localAxis.normalized);
    }

    /// <summary>Gewinn für die Symbole auf der Gewinnlinie berechnen.</summary>
    public int Evaluate(SlotSymbol[] line)
    {
        if (line.Length == 0)
            return 0;

        bool allSame = true;
        for (int i = 1; i < line.Length; i++)
            allSame &= line[i] == line[0];

        if (allSame)
        {
            foreach (var p in payouts)
                if (p.symbol == line[0])
                    return p.threeOfAKind;
        }

        int cherries = 0;
        foreach (var s in line)
            if (s == SlotSymbol.Cherries)
                cherries++;

        if (cherries == 2) return twoCherries;
        if (cherries == 1) return oneCherry;
        return 0;
    }

    void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
