using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Basis für 3D-Buttons (MenuButton, ArrowButton): Beim Hover Entered fährt die Kappe nach unten,
/// beim Hover Exited wieder hoch. Abgeleitete Klassen reagieren über OnPressed / OnReleased.
///
/// Setup:
///  - Auf das Root-Objekt des Button-Modells legen (die Base).
///  - Dort auch einen XR Simple Interactable + BoxCollider hinzufügen (der Collider gehört auf
///    die Base, NICHT auf die Kappe – sonst wandert er beim Drücken weg und Hover flackert).
///  - "Cap" = die Kappe des Modells.
/// </summary>
public abstract class PressableButton : MonoBehaviour
{
    [Header("Referenzen")]
    [Tooltip("Interactable, dessen Hover-Events genutzt werden. Leer = auf diesem GameObject suchen.")]
    [SerializeField] protected XRBaseInteractable interactable;

    [Tooltip("Die bewegliche Kappe (z.B. Button_Cap / ArrowButton_Cap).")]
    [SerializeField] protected Transform cap;

    [Header("Drück-Animation")]
    [Tooltip("Wie weit die Kappe nach unten fährt (Meter). Modell erlaubt max. 0.005.")]
    [SerializeField] float pressDepth = 0.005f;

    [Tooltip("Drückrichtung automatisch bestimmen: von der Ruheposition der Kappe Richtung Base-Ursprung. " +
             "Funktioniert egal, ob das Modell in Unity Y-up oder Z-up importiert wurde.")]
    [SerializeField] bool autoDetectAxis = true;

    [Tooltip("Richtung, in die die Kappe gedrückt wird (lokal zur Base). Nur wenn Auto Detect Axis aus ist.")]
    [SerializeField] Vector3 pressAxis = Vector3.down;

    [Tooltip("Dauer der Bewegung in Sekunden.")]
    [SerializeField] float pressDuration = 0.08f;

    [Header("Events")]
    [Tooltip("Hover-Events automatisch abonnieren. Aus, wenn du OnHoverEntered/OnHoverExited " +
             "selbst im Inspector verknüpfst (sonst wird doppelt ausgelöst).")]
    [SerializeField] bool autoSubscribe = true;

    /// <summary>True, solange mindestens ein Interactor über dem Button hovert.</summary>
    protected bool IsPressed { get; private set; }

    Vector3 m_CapRestPos;
    Vector3 m_CapPressedPos;
    Coroutine m_Anim;

    protected virtual void Awake()
    {
        if (interactable == null)
            interactable = GetComponent<XRBaseInteractable>();

        if (cap != null)
        {
            m_CapRestPos = cap.localPosition;
            // Die Kappe sitzt "über" dem Base-Ursprung -> nach unten = Richtung Ursprung
            Vector3 axis = autoDetectAxis && m_CapRestPos.sqrMagnitude > 1e-10f
                ? -m_CapRestPos.normalized
                : pressAxis.normalized;
            // Drücktiefe in Metern -> lokale Einheiten des Eltern-Objekts umrechnen (falls skaliert)
            Transform parent = cap.parent;
            Vector3 offset = parent != null
                ? parent.InverseTransformVector(parent.TransformDirection(axis) * pressDepth)
                : axis * pressDepth;
            m_CapPressedPos = m_CapRestPos + offset;
        }
    }

    protected virtual void OnEnable()
    {
        if (autoSubscribe && interactable != null)
        {
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }
    }

    protected virtual void OnDisable()
    {
        if (autoSubscribe && interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        if (IsPressed)
        {
            IsPressed = false;
            if (cap != null)
                cap.localPosition = m_CapRestPos;
            OnReleased();
        }
    }

    public void OnHoverEntered(HoverEnterEventArgs args)
    {
        // Nur beim ersten Interactor reagieren (z.B. wenn beide Hände gleichzeitig zeigen)
        if (IsPressed)
            return;

        IsPressed = true;
        MoveCap(m_CapPressedPos);
        OnPressed();
    }

    public void OnHoverExited(HoverExitEventArgs args)
    {
        // Erst loslassen, wenn kein Interactor mehr hovert
        if (!IsPressed || (interactable != null && interactable.isHovered))
            return;

        IsPressed = false;
        MoveCap(m_CapRestPos);
        OnReleased();
    }

    /// <summary>Wird aufgerufen, wenn der Button runtergedrückt wird.</summary>
    protected virtual void OnPressed() { }

    /// <summary>Wird aufgerufen, wenn der Button wieder hochkommt.</summary>
    protected virtual void OnReleased() { }

    void MoveCap(Vector3 target)
    {
        if (cap == null || !isActiveAndEnabled)
            return;

        if (m_Anim != null)
            StopCoroutine(m_Anim);
        m_Anim = StartCoroutine(AnimateCap(target));
    }

    IEnumerator AnimateCap(Vector3 target)
    {
        Vector3 start = cap.localPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, pressDuration);
            cap.localPosition = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        cap.localPosition = target;
        m_Anim = null;
    }
}
