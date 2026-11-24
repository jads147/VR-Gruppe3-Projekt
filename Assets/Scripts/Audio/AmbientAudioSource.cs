using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Spielt eine Playlist aus mehreren Sounds nacheinander ab.
/// Jeder Eintrag hat eigene Lautstärke, Wiederholungsanzahl und Ein-/Ausblendzeiten.
/// Mit "crossfade" überlappen Ausblenden des alten und Einblenden des neuen Sounds
/// (dafür wird zur Laufzeit eine zweite AudioSource mit denselben Einstellungen angelegt).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AmbientAudioSource : MonoBehaviour
{
    [Serializable]
    public class Track
    {
        public AudioClip clip;

        [Tooltip("Lautstärke dieses Sounds (wird mit der Gesamtlautstärke multipliziert).")]
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Wie oft der Sound hintereinander abgespielt wird. 0 = endlos (bis Next() oder Stop()).")]
        [Min(0)] public int loopCount = 1;

        [Tooltip("Einblendzeit in Sekunden.")]
        [Min(0f)] public float fadeIn = 2f;

        [Tooltip("Ausblendzeit in Sekunden (beginnt vor dem Ende des letzten Durchlaufs).")]
        [Min(0f)] public float fadeOut = 2f;

        [Tooltip("Startet an einer zufälligen Stelle im Clip (der erste Durchlauf ist dann kürzer).")]
        public bool randomStartTime = false;
    }

    [Header("Playlist")]
    [SerializeField] private Track[] tracks = new Track[0];

    [Tooltip("Gesamtlautstärke für alle Sounds.")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;

    [Tooltip("Startet automatisch beim Szenenstart.")]
    [SerializeField] private bool playOnStart = true;

    [Tooltip("Reihenfolge bei jedem Durchgang der Playlist mischen.")]
    [SerializeField] private bool shuffle = false;

    [Tooltip("Nach dem letzten Sound wieder von vorne beginnen.")]
    [SerializeField] private bool repeatPlaylist = true;

    [Tooltip("Ausblenden des alten und Einblenden des neuen Sounds überlappen. " +
             "Aus = erst komplett ausblenden, dann den nächsten einblenden.")]
    [SerializeField] private bool crossfade = true;

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

    // Alte Felder aus der Ein-Clip-Version. Werden beim Laden in "tracks" übernommen.
    // Können entfernt werden, sobald alle Szenen/Prefabs einmal neu gespeichert wurden.
    [SerializeField, HideInInspector] private AudioClip clip;
    [SerializeField, HideInInspector] private bool randomStartTime = true;
    [SerializeField, HideInInspector] private float fadeInDuration = 2f;

    private readonly AudioSource[] sources = new AudioSource[2];
    private readonly Coroutine[] fades = new Coroutine[2];
    private Coroutine playlistRoutine;
    private int activeSource = -1;
    private Track currentTrack;
    private bool skipRequested;

    /// <summary>Index des aktuell spielenden Eintrags in der Playlist, -1 wenn nichts spielt.</summary>
    public int CurrentTrackIndex { get; private set; } = -1;

    public bool IsPlaying => playlistRoutine != null;

    private void Awake()
    {
        MigrateLegacyClip();

        sources[0] = GetComponent<AudioSource>();
        sources[1] = gameObject.AddComponent<AudioSource>();
        sources[1].hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
        ApplySettings();
    }

    private void Start()
    {
        if (playOnStart) Play();
    }

    private void ApplySettings()
    {
        AudioSource primary = sources[0];
        if (primary == null) return;

        primary.playOnAwake = false;
        primary.spatialBlend = spatialBlend;
        primary.minDistance = minDistance;
        primary.maxDistance = maxDistance;
        primary.rolloffMode = rolloffMode;
        primary.dopplerLevel = 0f;

        AudioSource secondary = sources[1];
        if (secondary == null) return;

        // Zweite Quelle übernimmt alles von der ersten, damit beide gleich klingen.
        secondary.playOnAwake = false;
        secondary.outputAudioMixerGroup = primary.outputAudioMixerGroup;
        secondary.priority = primary.priority;
        secondary.pitch = primary.pitch;
        secondary.panStereo = primary.panStereo;
        secondary.spatialBlend = primary.spatialBlend;
        secondary.reverbZoneMix = primary.reverbZoneMix;
        secondary.spread = primary.spread;
        secondary.dopplerLevel = primary.dopplerLevel;
        secondary.minDistance = primary.minDistance;
        secondary.maxDistance = primary.maxDistance;
        secondary.rolloffMode = primary.rolloffMode;
        secondary.bypassEffects = primary.bypassEffects;
        secondary.bypassListenerEffects = primary.bypassListenerEffects;
        secondary.bypassReverbZones = primary.bypassReverbZones;
        secondary.mute = primary.mute;
        if (primary.rolloffMode == AudioRolloffMode.Custom)
            secondary.SetCustomCurve(AudioSourceCurveType.CustomRolloff,
                primary.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
    }

    // ---------------------------------------------------------------- API

    /// <summary>Startet die Playlist von vorne (bzw. neu gemischt).</summary>
    public void Play()
    {
        if (playlistRoutine != null) StopCoroutine(playlistRoutine);
        playlistRoutine = StartCoroutine(PlaylistRoutine());
    }

    /// <summary>Blendet alles aus und stoppt die Playlist.</summary>
    public void Stop(float fadeOutDuration = 1f)
    {
        if (playlistRoutine != null)
        {
            StopCoroutine(playlistRoutine);
            playlistRoutine = null;
        }

        for (int i = 0; i < sources.Length; i++)
            if (sources[i] != null && sources[i].isPlaying)
                FadeTo(i, 0f, fadeOutDuration, stopWhenDone: true);

        currentTrack = null;
        CurrentTrackIndex = -1;
    }

    /// <summary>Blendet den aktuellen Sound aus und springt zum nächsten.</summary>
    public void Next()
    {
        if (playlistRoutine == null) Play();
        else skipRequested = true;
    }

    // ---------------------------------------------------------------- Playlist

    private IEnumerator PlaylistRoutine()
    {
        int lastIndex = -1;

        while (true)
        {
            int[] order = BuildOrder(lastIndex);
            bool anyPlayed = false;

            foreach (int index in order)
            {
                Track track = tracks[index];
                if (track == null || track.clip == null) continue;

                anyPlayed = true;
                lastIndex = index;
                CurrentTrackIndex = index;
                yield return PlayTrack(track);
            }

            if (!anyPlayed)
            {
                Debug.LogWarning($"[{name}] AmbientAudioSource: Keine AudioClips in der Playlist.", this);
                break;
            }
            if (!repeatPlaylist) break;
        }

        currentTrack = null;
        CurrentTrackIndex = -1;
        playlistRoutine = null;
    }

    private int[] BuildOrder(int avoidFirst)
    {
        int count = tracks != null ? tracks.Length : 0;
        int[] order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;

        if (shuffle && count > 1)
        {
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            // Nicht denselben Sound zweimal direkt hintereinander beim Neustart der Runde.
            if (order[0] == avoidFirst)
            {
                int swap = Random.Range(1, count);
                (order[0], order[swap]) = (order[swap], order[0]);
            }
        }
        return order;
    }

    /// <summary>
    /// Spielt einen Eintrag ab. Kehrt zurück, sobald der nächste Sound starten soll:
    /// mit Crossfade beim Beginn des Ausblendens, sonst nachdem ausgeblendet wurde.
    /// </summary>
    private IEnumerator PlayTrack(Track track)
    {
        skipRequested = false;
        currentTrack = track;

        int srcIndex = AcquireSource();
        activeSource = srcIndex;
        AudioSource src = sources[srcIndex];
        AudioClip trackClip = track.clip;

        bool endless = track.loopCount <= 0;

        StopFade(srcIndex);
        src.Stop();
        src.clip = trackClip;
        src.volume = 0f;
        src.loop = endless || track.loopCount > 1;
        src.timeSamples = track.randomStartTime ? Random.Range(0, trackClip.samples) : 0;
        src.Play();

        // Ein-/Ausblenden darf zusammen nicht länger dauern als der Sound selbst.
        float fadeIn = track.fadeIn;
        float fadeOut = track.fadeOut;
        if (!endless)
        {
            float totalLength = trackClip.length * track.loopCount
                                - (float)src.timeSamples / trackClip.frequency;
            float fadeSum = fadeIn + fadeOut;
            if (fadeSum > totalLength && fadeSum > 0f)
            {
                float scale = totalLength / fadeSum;
                fadeIn *= scale;
                fadeOut *= scale;
            }
        }

        FadeTo(srcIndex, TargetVolume(track), fadeIn);

        int completedLoops = 0;
        int lastSamples = src.timeSamples;
        float fadeOutDuration = fadeOut;

        while (true)
        {
            if (skipRequested) break;
            if (!src.isPlaying) { fadeOutDuration = 0f; break; } // letzter Durchlauf zu Ende

            int samples = src.timeSamples;
            if (samples < lastSamples)
            {
                completedLoops++;
                // Ab dem letzten Durchlauf nicht mehr loopen, die Quelle endet dann von selbst.
                if (!endless && completedLoops >= track.loopCount - 1) src.loop = false;
            }
            lastSamples = samples;

            if (!src.loop && fadeOut > 0f)
            {
                float remaining = (float)(trackClip.samples - samples) / trackClip.frequency;
                if (remaining <= fadeOut)
                {
                    fadeOutDuration = remaining;
                    break;
                }
            }

            yield return null;
        }

        skipRequested = false;
        FadeTo(srcIndex, 0f, fadeOutDuration, stopWhenDone: true);

        if (!crossfade)
            while (src.isPlaying) yield return null;
    }

    private float TargetVolume(Track track) => volume * track.volume;

    /// <summary>Nimmt eine freie Quelle, sonst die leisere (die gerade ausblendet).</summary>
    private int AcquireSource()
    {
        if (sources[1] == null) return 0;
        if (!sources[0].isPlaying) return 0;
        if (!sources[1].isPlaying) return 1;
        return sources[0].volume <= sources[1].volume ? 0 : 1;
    }

    // ---------------------------------------------------------------- Fading

    private void FadeTo(int srcIndex, float target, float duration, bool stopWhenDone = false)
    {
        StopFade(srcIndex);
        fades[srcIndex] = StartCoroutine(Fade(srcIndex, target, duration, stopWhenDone));
    }

    private void StopFade(int srcIndex)
    {
        if (fades[srcIndex] == null) return;
        StopCoroutine(fades[srcIndex]);
        fades[srcIndex] = null;
    }

    private IEnumerator Fade(int srcIndex, float target, float duration, bool stopWhenDone)
    {
        AudioSource src = sources[srcIndex];

        if (duration > 0f)
        {
            float start = src.volume;
            float t = 0f;
            while (t < duration)
            {
                // Unscaled, damit Fades auch bei Time.timeScale = 0 weiterlaufen (Audio tut das auch).
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
        }
        src.volume = target;

        if (stopWhenDone) src.Stop();
        fades[srcIndex] = null;
    }

    // ---------------------------------------------------------------- Editor

    private void MigrateLegacyClip()
    {
        if (clip == null) return;

        if (tracks == null || tracks.Length == 0)
        {
            tracks = new[]
            {
                new Track
                {
                    clip = clip,
                    volume = 1f,
                    loopCount = 0, // altes Verhalten: endlos loopen
                    fadeIn = fadeInDuration,
                    fadeOut = 2f,
                    randomStartTime = randomStartTime,
                }
            };
        }
        clip = null;
    }

    private void OnValidate()
    {
        MigrateLegacyClip();

        if (sources[0] == null) sources[0] = GetComponent<AudioSource>();
        if (sources[0] == null) return;
        ApplySettings();

        // Lautstärke live im Inspector anpassbar, solange gerade nicht geblendet wird.
        if (Application.isPlaying && currentTrack != null && activeSource >= 0
            && fades[activeSource] == null && sources[activeSource].isPlaying)
            sources[activeSource].volume = TargetVolume(currentTrack);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, minDistance);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, maxDistance);
    }
}
