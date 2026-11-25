using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spielt einen Klick-Sound bei JEDEM UI-Button der Szene (Menü, Levelauswahl, Tastatur, ...).
/// Einmal in die Szene legen (z.B. auf das EventSystem) und einen Clip eintragen – die Buttons
/// müssen nicht einzeln eingerichtet werden. Neue Buttons (z.B. die Tasten der Namenseingabe,
/// die erst zur Laufzeit entstehen) werden regelmäßig nachgesucht.
/// Der Sound ist 2D (ohne räumliche Abschwächung), wie bei den Punkte-Sounds.
/// </summary>
public class UIButtonClickSound : MonoBehaviour
{
    [Tooltip("Sound, der beim Klick auf einen Button abgespielt wird.")]
    [SerializeField] AudioClip clickClip;

    [Range(0f, 1f)]
    [SerializeField] float volume = 0.8f;

    [Tooltip("Zufällige Tonhöhen-Abweichung, damit schnelle Klicks nicht identisch klingen. 0 = aus.")]
    [Range(0f, 0.2f)]
    [SerializeField] float pitchVariation = 0.03f;

    [Tooltip("Wie oft (Sekunden) nach neuen Buttons gesucht wird.")]
    [SerializeField] float refreshInterval = 1f;

    AudioSource m_Source;
    readonly HashSet<Button> m_Hooked = new HashSet<Button>();
    float m_NextRefresh;

    void Awake()
    {
        m_Source = gameObject.AddComponent<AudioSource>();
        m_Source.playOnAwake = false;
        m_Source.spatialBlend = 0f;

        if (clickClip == null)
            Debug.LogWarning("[UIButtonClickSound] Kein Click Clip eingetragen.", this);
        else if (clickClip.loadState == AudioDataLoadState.Unloaded)
            clickClip.LoadAudioData(); // "Preload Audio Data" aus -> sonst ist der erste Klick evtl. stumm
    }

    void Update()
    {
        if (Time.unscaledTime < m_NextRefresh)
            return;

        m_NextRefresh = Time.unscaledTime + refreshInterval;
        HookNewButtons();
    }

    void HookNewButtons()
    {
        // Zerstörte Buttons aus der Liste werfen (Unity-null).
        m_Hooked.RemoveWhere(b => b == null);

        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (m_Hooked.Add(button))
                button.onClick.AddListener(PlayClick);
        }
    }

    void OnDestroy()
    {
        foreach (var button in m_Hooked)
        {
            if (button != null)
                button.onClick.RemoveListener(PlayClick);
        }
    }

    /// <summary>Kann auch direkt aufgerufen werden, z.B. von einem 3D-Button.</summary>
    public void PlayClick()
    {
        if (clickClip == null)
            return;

        m_Source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        m_Source.PlayOneShot(clickClip, volume);
    }
}
