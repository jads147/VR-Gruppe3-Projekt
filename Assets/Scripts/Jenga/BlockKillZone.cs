using UnityEngine;

/// <summary>
/// Deletes blocks that touch the collider on this GameObject (normal collider or trigger).
/// Blocks with a <see cref="Point_OBJ"/> give their points (with the point effect) before they are deleted.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BlockKillZone : MonoBehaviour
{
    [Tooltip("Objects on these layers are deleted when they touch this collider.")]
    [SerializeField] private LayerMask blockLayers;
    [Tooltip("Blocks with a Point_OBJ add their points (and play the point effect) before they are deleted.")]
    [SerializeField] private bool scorePointBlocks = true;

    private void Reset()
    {
        blockLayers = LayerMask.GetMask("Jenga");
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleContact(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    private void HandleContact(Collider other)
    {
        // Use the rigidbody's object, so blocks with child colliders are deleted as a whole.
        GameObject block = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;

        if ((blockLayers.value & (1 << block.layer)) == 0)
            return;

        // Point_OBJ scores once and destroys the block itself.
        if (scorePointBlocks && block.TryGetComponent(out Point_OBJ pointBlock))
        {
            pointBlock.TryScore();
            return;
        }

        block.SetActive(false);
        Destroy(block);
    }
}
