using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System.Collections;


public class Spawner : MonoBehaviour
{
    [SerializeField] GameObject prefabItem;
    [SerializeField] float timeToRespawn = 3f;
    private GameObject item;
    private bool spawned = false;


    private void Start()
    {
        SpawnItem();
    }
    void SpawnItem()
    {
        item = Instantiate(prefabItem, transform.position, Quaternion.identity);
    }
    private void OnTriggerExit(Collider other)
    {
        if(other.gameObject == item.gameObject && !spawned)
        {
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
        SpawnItem();
        spawned = false;
    }
}
