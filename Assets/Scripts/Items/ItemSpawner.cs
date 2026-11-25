using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 3D-Button, der beim Drücken (Hover Entered) ein Item spawnt – z.B. Bombe, Hufeisen oder Ball.
/// Die Kappe fährt dabei runter wie bei allen <see cref="PressableButton"/>s (Setup siehe dort).
/// SpawnItem() kann weiterhin auch direkt per Event aufgerufen werden.
///
/// Optional begrenzt "Max Spawns" die Anzahl pro Runde; der Zähler wird bei "Start Game" und beim
/// Rundenende wieder aufgefüllt. "Remaining Text" zeigt die verbleibende Anzahl an.
/// </summary>
public class ItemSpawner : PressableButton
{
    [Header("Spawn")]
    [SerializeField] GameObject spawnItem;
    [SerializeField] Transform spawnPoint;
    [SerializeField] private Transform orbitParent;

    [Header("Anzahl (optional)")]
    [Tooltip("Wie oft pro Runde gespawnt werden kann. 0 = unbegrenzt.")]
    [SerializeField, Min(0)] private int maxSpawns = 0;
    [Tooltip("Zeigt die verbleibende Anzahl an. Wird bei unbegrenzter Anzahl ausgeblendet.")]
    [SerializeField] private TMP_Text remainingText;

    [Header("Sound (optional)")]
    [SerializeField] private AudioClip spawnSound;
    [SerializeField, Range(0f, 1f)] private float spawnVolume = 1f;

    int m_LastSpawnFrame = -1;
    int m_Remaining;
    bool m_WasRoundActive;

    bool IsLimited => maxSpawns > 0;

    /// <summary>Verbleibende Spawns in dieser Runde (bei unbegrenzter Anzahl int.MaxValue).</summary>
    public int Remaining => IsLimited ? m_Remaining : int.MaxValue;

    // Alle gespawnten Items (aller Spawner), damit "Reload Game" sie wieder entfernen kann.
    static readonly List<GameObject> s_Spawned = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_Spawned.Clear();

    /// <summary>Entfernt alle noch vorhandenen gespawnten Items.</summary>
    public static void DestroyAllSpawned()
    {
        foreach (var item in s_Spawned)
        {
            if (item == null)
                continue;
            // Sofort deaktivieren (Destroy wirkt erst am Ende des Frames).
            item.SetActive(false);
            Destroy(item);
        }
        s_Spawned.Clear();
    }

    protected override void Awake()
    {
        base.Awake();
        m_WasRoundActive = GameManager.Instance != null && GameManager.Instance.IsRoundActive;
        ResetCount();
        GameManager.StateChanged += OnRoundStateChanged;
    }

    void OnDestroy()
    {
        GameManager.StateChanged -= OnRoundStateChanged;
    }

    // Bei Start Game und beim Rundenende (Score eingetragen / Reload Game) wieder auffüllen.
    // StateChanged kommt auch bei den ersten Punkten – dann ändert sich IsRoundActive nicht.
    void OnRoundStateChanged()
    {
        bool roundActive = GameManager.Instance != null && GameManager.Instance.IsRoundActive;
        if (roundActive != m_WasRoundActive)
            ResetCount();
        m_WasRoundActive = roundActive;
    }

    /// <summary>Füllt die Anzahl wieder auf "Max Spawns" auf.</summary>
    public void ResetCount()
    {
        m_Remaining = maxSpawns;
        UpdateRemainingText();
    }

    void UpdateRemainingText()
    {
        if (remainingText == null)
            return;

        remainingText.enabled = IsLimited;
        if (IsLimited)
            remainingText.text = m_Remaining.ToString();
    }

    protected override void OnPressed() => SpawnItem();

    public void SpawnItem()
    {
        // Rundenende-Screen offen: neue Items wären greifbar, obwohl alles andere gesperrt ist.
        if (GrabLock.IsLocked)
            return;

        // Kontingent für diese Runde aufgebraucht.
        if (IsLimited && m_Remaining <= 0)
            return;

        // Schutz gegen Doppel-Spawn, falls SpawnItem zusätzlich noch manuell im
        // Hover-Entered-Event verknüpft ist (beide Aufrufe kommen im selben Frame).
        if (m_LastSpawnFrame == Time.frameCount)
            return;
        m_LastSpawnFrame = Time.frameCount;

        if (spawnItem == null || spawnPoint == null)
        {
            Debug.LogError("Assign both Spawn Item and Spawn Point.", this);
            return;
        }

        // Keep the item's world pose while making it follow the orbiting AR setup.
        s_Spawned.RemoveAll(item => item == null);
        s_Spawned.Add(Instantiate(spawnItem, spawnPoint.position, spawnPoint.rotation, orbitParent));

        if (IsLimited)
        {
            m_Remaining--;
            UpdateRemainingText();
        }

        // Plays as a temporary 3D sound at the spawn point, so it comes from the right direction in VR.
        if (spawnSound != null)
            AudioSource.PlayClipAtPoint(spawnSound, spawnPoint.position, spawnVolume);
    }
}
