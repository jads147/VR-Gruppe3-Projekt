using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
 
// Plays a sound when the object is grabbed and, optionally, when it is thrown.
// Put it on any object with an XRGrabInteractable (items, balls, the cannon).
[RequireComponent(typeof(XRBaseInteractable))]
public sealed class GrabSound : MonoBehaviour
{
    [Header("Grab")]
    [SerializeField] private AudioClip grabClip;
    [SerializeField, Range(0f, 1f)] private float grabVolume = 1f;
 
    [Header("Throw (optional)")]
    [Tooltip("Played on release, but only if the object is moving faster than the threshold (a real throw, not just letting go).")]
    [SerializeField] private AudioClip throwClip;
    [SerializeField, Range(0f, 1f)] private float throwVolume = 1f;
    [SerializeField, Min(0f)] private float throwSpeedThreshold = 1.5f;
 
    [Header("Variation")]
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.08f;
 
    private XRBaseInteractable interactable;
    private Rigidbody body;
    private AudioSource source;
 
    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        body = GetComponent<Rigidbody>();
 
    
        if (!TryGetComponent(out source))
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
        }
    }
 
    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
    }
 
    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnGrab);
        interactable.selectExited.RemoveListener(OnRelease);
    }
 
    private void OnGrab(SelectEnterEventArgs args) => Play(grabClip, grabVolume);
 
    private void OnRelease(SelectExitEventArgs args)
    {
        
        if (throwClip == null || body == null || args.isCanceled || !isActiveAndEnabled)
            return;
        StartCoroutine(CheckThrow());
    }
 
    private IEnumerator CheckThrow()
    {
        yield return new WaitForFixedUpdate();
        if (body != null && body.linearVelocity.magnitude >= throwSpeedThreshold)
            Play(throwClip, throwVolume);
    }
 
    private void Play(AudioClip clip, float volume)
    {
        if (clip == null)
            return;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clip, volume);
    }
}

