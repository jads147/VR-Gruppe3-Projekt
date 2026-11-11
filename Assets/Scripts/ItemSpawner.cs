using UnityEngine;
 
public class ItemSpawner : MonoBehaviour
{
    [SerializeField] GameObject spawnItem;
    [SerializeField] Transform spawnPoint;
    [SerializeField] private Transform orbitParent;
 
    [Header("Sound (optional)")]
    [SerializeField] private AudioClip spawnSound;
    [SerializeField, Range(0f, 1f)] private float spawnVolume = 1f;
 
    public void SpawnItem()
    {
        if (spawnItem == null || spawnPoint == null)
        {
            Debug.LogError("Assign both Spawn Item and Spawn Point.", this);
            return;
        }
 
        // Keep the item's world pose while making it follow the orbiting AR setup.
        Instantiate(spawnItem, spawnPoint.position, spawnPoint.rotation, orbitParent);
 
        // Plays as a temporary 3D sound at the spawn point, so it comes from the right direction in VR.
        if (spawnSound != null)
            AudioSource.PlayClipAtPoint(spawnSound, spawnPoint.position, spawnVolume);
    }
}
