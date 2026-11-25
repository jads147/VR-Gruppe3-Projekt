using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Throwable that destroys the first Jenga block it touches. No points are added or removed:
/// the block is removed directly, without going through <see cref="Point_OBJ.TryScore"/>.
/// With an <see cref="XRGrabInteractable"/> on the same object it only works after being thrown,
/// so touching blocks while holding it does nothing (optional).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class JengaBlockBreaker : MonoBehaviour
{
    [Header("Breaking")]
    [Tooltip("Objects on these layers count as blocks.")]
    [SerializeField] private LayerMask blockLayers;
    [Tooltip("Only break a block after the item has been thrown (released from the hand). " +
             "Needs an XRGrabInteractable on this object; without one the item is always armed.")]
    [SerializeField] private bool onlyWhenThrown = true;

    [Header("After Use")]
    [Tooltip("Remove the item after it has broken a block.")]
    [SerializeField] private bool destroySelf = true;
    [SerializeField, Min(0f)] private float destroySelfDelay = 0f;

    [Header("Feedback (optional)")]
    [Tooltip("Spawned where the block was (e.g. a particle prefab). Destroyed after Effect Lifetime.")]
    [SerializeField] private GameObject breakEffect;
    [SerializeField, Min(0.1f)] private float effectLifetime = 2f;
    [SerializeField] private AudioClip breakClip;
    [SerializeField, Range(0f, 1f)] private float breakVolume = 1f;

    private XRGrabInteractable grab;
    private bool armed;
    private bool used;

    private void Reset()
    {
        blockLayers = LayerMask.GetMask("Jenga");
    }

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        armed = !onlyWhenThrown || grab == null;
    }

    private void OnEnable()
    {
        if (grab == null)
            return;
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        if (grab == null)
            return;
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (onlyWhenThrown)
            armed = false;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (!args.isCanceled)
            armed = true;

        // Broke a block while held (onlyWhenThrown off): remove it once it is let go.
        if (used && destroySelf)
            Destroy(gameObject, destroySelfDelay);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryBreak(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryBreak(other);
    }

    private void TryBreak(Collider other)
    {
        if (used || !armed)
            return;

        // Use the rigidbody's object, so blocks with child colliders are removed as a whole.
        GameObject block = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if ((blockLayers.value & (1 << block.layer)) == 0)
            return;

        // Set first: several contacts can arrive in the same physics step.
        used = true;
        Vector3 position = other.bounds.center;

        // Deactivate right away so the block can't score through Point_OBJ (or touch anything) before it is gone.
        block.SetActive(false);
        Destroy(block);

        if (breakEffect != null)
            Destroy(Instantiate(breakEffect, position, Quaternion.identity), effectLifetime);
        if (breakClip != null)
            AudioSource.PlayClipAtPoint(breakClip, position, breakVolume);

        if (destroySelf && (grab == null || !grab.isSelected))
            Destroy(gameObject, destroySelfDelay);
    }
}
