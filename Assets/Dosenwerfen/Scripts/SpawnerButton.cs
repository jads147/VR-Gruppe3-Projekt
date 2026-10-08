using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System.Collections;


public class SpawnerButton : MonoBehaviour
{
    [SerializeField] GameObject prefabItem;
    [SerializeField] Transform spawnPoint;
    [SerializeField] float timeToRespawn = 3f;
    private bool spawned = false;


    private void SpawnItem()
    {
        Instantiate(prefabItem, spawnPoint.position, Quaternion.identity);
    }
    public void TrySpawn()
    {
        if(!spawned)
        {
            SpawnItem();
            StartCoroutine("CountDownToSpawn", timeToRespawn);
            spawned = true;
            Debug.Log("BallRespawn");
        }
    }

    private IEnumerator CountDownToSpawn(float timeToRespawn)
    {
        float elapsedTime = 0f;

        while(elapsedTime < timeToRespawn) 
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        spawned = false;
    }
}
