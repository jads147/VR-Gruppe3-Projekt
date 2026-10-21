using UnityEngine;

public class Point_OBJ : MonoBehaviour
{
    [SerializeField] private int points = 10;
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Fallen"))
        {
            GameManager.Instance.AddPoints(points);
            Destroy(gameObject);
        }
    }
}
