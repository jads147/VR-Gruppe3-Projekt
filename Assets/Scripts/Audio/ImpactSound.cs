using UnityEngine;
 
// Plays a sound when this object hits something. Louder hits = louder sound.
// Put it on the prefab of anything that should make a sound on impact:
// the cannon projectile, balls, Jenga blocks, point cubes, house cubes.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class ImpactSound : MonoBehaviour
{
    [Header("Clips")]
    [Tooltip("One is picked at random per hit. Several similar clips sound more natural than one.")]
    [SerializeField] private AudioClip[] clips;
 
    [Header("Volume by impact speed")]
    [Tooltip("Hits slower than this make no sound (avoids noise from sliding or resting objects).")]
    [SerializeField, Min(0f)] private float minImpactSpeed = 0.4f;
    [Tooltip("Hits at or above this speed play at full volume.")]
    [SerializeField, Min(0.01f)] private float maxImpactSpeed = 6f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 1f;
 
    [Header("Spam protection")]
    [Tooltip("Minimum time between two sounds from this object.")]
    [SerializeField, Min(0f)] private float cooldown = 0.1f;
    [Tooltip("No sounds this long after the object appears, so a freshly built tower settles silently.")]
    [SerializeField, Min(0f)] private float silentAfterSpawn = 1f;
    [Tooltip("Max impact sounds per frame across ALL objects. Keeps a collapsing tower from playing 50 sounds at once.")]
    [SerializeField, Min(1)] private int maxSoundsPerFrame = 4;
 
    [Header("3D sound")]
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.1f;
    [SerializeField, Min(0f)] private float minDistance = 0.5f;
    [SerializeField, Min(0.1f)] private float maxDistance = 15f;
 
    private static int soundsThisFrame;
    private static int countedFrame = -1;
 
    private static int idCounter;
    private int id;
 
    private float nextAllowedTime;
 
    private void Awake()
    {
        id = idCounter++;
        nextAllowedTime = Time.time + silentAfterSpawn;
    }
 
    private void OnCollisionEnter(Collision collision)
    {
        if (clips == null || clips.Length == 0 || Time.time < nextAllowedTime)
            return;
 
        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed)
            return;
 
        // Block hits block: both get OnCollisionEnter. Only one of them plays, otherwise it sounds doubled.
        ImpactSound other = collision.rigidbody != null ? collision.rigidbody.GetComponent<ImpactSound>() : null;
        if (other != null && other.id < id)
            return;
 
        if (countedFrame != Time.frameCount)
        {
            countedFrame = Time.frameCount;
            soundsThisFrame = 0;
        }
        if (soundsThisFrame >= maxSoundsPerFrame)
            return;
        soundsThisFrame++;
 
        nextAllowedTime = Time.time + cooldown;
 
        float t = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed);
        Vector3 position = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        PlayAt(clips[Random.Range(0, clips.Length)], position, t * maxVolume);
    }
 
    // Uses a short-lived separate object, so the sound keeps playing even if this object
    // is destroyed right after the hit (e.g. exploding balls, projectiles with a lifetime).
    private void PlayAt(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null || volume <= 0f)
            return;
 
        var soundObject = new GameObject("ImpactSound");
        soundObject.transform.position = position;
 
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.Play();
 
        Destroy(soundObject, clip.length / Mathf.Max(0.01f, source.pitch) + 0.1f);
    }
}