using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [SerializeField] GameObject spawnItem;
    [SerializeField] Transform spawnPoint;
    [SerializeField] private Transform orbitParent;

    public void SpawnItem()
    {
        if (spawnItem == null || spawnPoint == null)
        {
            Debug.LogError("Assign both Spawn Item and Spawn Point.", this);
            return;
        }

        // Keep the item's world pose while making it follow the orbiting AR setup.
        Instantiate(spawnItem, spawnPoint.position, spawnPoint.rotation, orbitParent);
    }
}
