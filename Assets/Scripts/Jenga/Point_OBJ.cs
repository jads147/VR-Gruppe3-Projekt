using UnityEngine;

public class Point_OBJ : MonoBehaviour
{
    [SerializeField] private int points = 10;
    [Tooltip("Particles, floating number and sound. Empty = Resources/PointEffectSettings.")]
    [SerializeField] private PointEffectSettings effectSettings;

    private bool scored;

    public int Points => points;

    private void OnCollisionEnter(Collision collision)
    {
        // Several contacts can arrive in the same physics step; score only once.
        if (scored || !collision.gameObject.CompareTag("Fallen"))
            return;

        scored = true;
        GameManager.Instance.AddPoints(points);
        PlayEffect();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void PlayEffect()
    {
        if (effectSettings == null)
            effectSettings = Resources.Load<PointEffectSettings>(PointEffectSettings.ResourcesPath);
        if (effectSettings == null)
            return;

        Vector3 center = transform.position;
        Vector3 size = Vector3.one;
        if (TryGetComponent(out BoxCollider box))
        {
            center = transform.TransformPoint(box.center);
            Vector3 lossy = transform.lossyScale;
            size = new Vector3(Mathf.Abs(box.size.x * lossy.x), Mathf.Abs(box.size.y * lossy.y),
                Mathf.Abs(box.size.z * lossy.z));
        }
        else if (TryGetComponent(out Renderer meshRenderer))
        {
            center = meshRenderer.bounds.center;
            size = meshRenderer.bounds.size;
        }

        PointBlockEffects.Spawn(effectSettings, points, center, size, transform.rotation);
    }
}
