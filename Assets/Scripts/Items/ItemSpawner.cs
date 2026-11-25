using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D-Button, der beim Drücken (Hover Entered) ein Item spawnt – z.B. Bombe, Hufeisen oder Ball.
/// Die Kappe fährt dabei runter wie bei allen <see cref="PressableButton"/>s (Setup siehe dort).
/// SpawnItem() kann weiterhin auch direkt per Event aufgerufen werden.
/// </summary>
public class ItemSpawner : PressableButton
{
    [Header("Spawn")]
    [SerializeField] GameObject spawnItem;
    [SerializeField] Transform spawnPoint;
    [SerializeField] private Transform orbitParent;

    [Header("Sound (optional)")]
    [SerializeField] private AudioClip spawnSound;
    [SerializeField, Range(0f, 1f)] private float spawnVolume = 1f;

    int m_LastSpawnFrame = -1;

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

    protected override void OnPressed() => SpawnItem();

    public void SpawnItem()
    {
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

        // Plays as a temporary 3D sound at the spawn point, so it comes from the right direction in VR.
        if (spawnSound != null)
            AudioSource.PlayClipAtPoint(spawnSound, spawnPoint.position, spawnVolume);
    }
}
