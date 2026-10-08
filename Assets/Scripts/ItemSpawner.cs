using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [SerializeField] GameObject spawnItem;
    [SerializeField] Transform spawnPoint;
    public void SpawnItem()
    {
        Instantiate(spawnItem, spawnPoint.position, Quaternion.identity);
    }
}
