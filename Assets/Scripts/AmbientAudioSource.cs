using System.Collections;
using UnityEngine;
 

[RequireComponent(typeof(AudioSource))]
public class AmbientAudioSource : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Der Loop, der abgespielt werden soll.")]
    [SerializeField] private AudioClip clip;
 
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;
 
    [Tooltip("Startet automatisch beim Szenenstart.")]
    [SerializeField] private bool playOnStart = true;
 
    [Tooltip("Startet an einer zufälligen Stelle im Clip, damit mehrere Loops nicht synchron klingen.")]
    [SerializeField] private bool randomStartTime = true;
 
    [Tooltip("Einblendzeit in Sekunden (0 = sofort volle Lautstärke).")]
    [SerializeField] private float fadeInDuration = 2f;
 
    [Header("3D-Sound")]
    [Tooltip("0 = 2D (überall gleich laut), 1 = voll 3D (kommt hörbar vom Objekt).")]
    [Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 1f;
 
    [Tooltip("Bis zu dieser Entfernung ist der Sound in voller Lautstärke.")]
    [SerializeField] private float minDistance = 2f;
 
    [Tooltip("Ab dieser Entfernung wird es nicht mehr leiser (bei Linear: unhörbar).")]
    [SerializeField] private float maxDistance = 25f;
 
    [Tooltip("Linear klingt meist natürlicher für Ambience, Logarithmic ist realistischer.")]
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;
 
    private AudioSource source;
    private Coroutine fadeRoutine;
 
    private void Awake()
    {
        source = GetComponent<AudioSource>();
        ApplySettings();
    }
 
    private void Start()
    {
        if (playOnStart) Play();
    }
 
    private void ApplySettings()
    {
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = rolloffMode;
        source.dopplerLevel = 0f; 
    }
 
    public void Play()
    {
        if (clip == null)
        {
            Debug.LogWarning($"[{name}] AmbientAudioSource: Kein AudioClip zugewiesen.", this);
            return;
        }
 
        if (randomStartTime)
            source.time = Random.Range(0f, clip.length);
 
        source.Play();
        FadeTo(volume, fadeInDuration);
    }
 
    public void Stop(float fadeOutDuration = 1f)
    {
        FadeTo(0f, fadeOutDuration, stopWhenDone: true);
    }
 
    private void FadeTo(float target, float duration, bool stopWhenDone = false)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(target, duration, stopWhenDone));
    }
 
    private IEnumerator Fade(float target, float duration, bool stopWhenDone)
    {
        if (duration <= 0f)
        {
            source.volume = target;
        }
        else
        {
            float start = source.volume;
            if (!stopWhenDone && !source.isPlaying) start = 0f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                source.volume = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            source.volume = target;
        }
 
        if (stopWhenDone) source.Stop();
        fadeRoutine = null;
    }
 
    private void OnValidate()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) return;
        ApplySettings();
        if (Application.isPlaying && fadeRoutine == null) source.volume = volume;
    }
 
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, minDistance);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, maxDistance);
    }
}
