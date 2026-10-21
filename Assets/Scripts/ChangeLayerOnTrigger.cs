using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class ChangeLayerOnTrigger : MonoBehaviour
{
    [SerializeField] private string targetLayerName = "Default";

    private int targetLayer;

    private void Awake()
    {
        targetLayer = LayerMask.NameToLayer(targetLayerName);
        if (targetLayer < 0)
            Debug.LogError($"ChangeLayerOnTrigger: Layer '{targetLayerName}' does not exist.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (targetLayer < 0)
            return;

        // Use the Rigidbody root when present, so compound colliders change as one object.
        Transform objectRoot = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform;

        // The item stops following the orbiting setup as soon as it reaches this trigger.
        if (objectRoot.parent != null)
            objectRoot.SetParent(null, true);

        SetLayerRecursively(objectRoot, targetLayer);
    }

    private static void SetLayerRecursively(Transform current, int layer)
    {
        current.gameObject.layer = layer;
        for (int i = 0; i < current.childCount; i++)
            SetLayerRecursively(current.GetChild(i), layer);
    }
}
