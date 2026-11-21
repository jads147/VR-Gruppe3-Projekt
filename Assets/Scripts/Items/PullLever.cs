using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Hebel, den man in VR greifen und um EINE Achse (Standard: lokale X-Achse) runterziehen kann.
/// Ab einer bestimmten Auslenkung wird "On Pulled" einmal ausgelöst. Nach dem Loslassen fährt der
/// Hebel langsam in die Ruheposition zurück.
///
/// Setup:
///  - Empty "LeverPivot" genau auf den Drehpunkt (Achse am Mount) setzen, den Hebel (z.B. SM_Lever)
///    als Child darunter. (Wenn der Pivot von SM_Lever schon am Drehpunkt liegt, geht auch SM_Lever direkt.)
///  - Auf LeverPivot: dieses Script + XR Simple Interactable (wird automatisch hinzugefügt).
///  - Collider (Box/Capsule) auf den Griff des Hebels (Child) – der Interactable findet Child-Collider selbst.
///  - KEIN Rigidbody / XR Grab Interactable nötig, die Drehung wird komplett hier berechnet.
///  - Max Angle negativ setzen, falls der Hebel in die falsche Richtung geht.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class PullLever : MonoBehaviour
{
    [Header("Referenzen")]
    [Tooltip("Das Transform, das gedreht wird. Leer = dieses GameObject.")]
    [SerializeField] Transform pivot;

    [Header("Achse & Winkel")]
    [Tooltip("Drehachse lokal zum Pivot. (1,0,0) = X-Achse.")]
    [SerializeField] Vector3 localAxis = Vector3.right;

    [Tooltip("Maximale Auslenkung in Grad. Negativ = andere Richtung.")]
    [SerializeField] float maxAngle = 60f;

    [Tooltip("Ab welchem Anteil von Max Angle 'On Pulled' ausgelöst wird (0.9 = 90%).")]
    [Range(0f, 1f)] [SerializeField] float triggerAt = 0.9f;

    [Tooltip("Unter diesem Anteil wird der Hebel wieder 'scharf' geschaltet (verhindert Mehrfachauslösung).")]
    [Range(0f, 1f)] [SerializeField] float rearmAt = 0.1f;

    [Header("Zurückfahren")]
    [Tooltip("Rückstell-Geschwindigkeit in Grad pro Sekunde.")]
    [SerializeField] float returnSpeed = 40f;

    [Tooltip("Wartezeit nach dem Loslassen, bevor der Hebel zurückfährt (Sekunden).")]
    [SerializeField] float returnDelay = 0f;

    [Header("Events")]
    [Tooltip("Wird einmal pro Zug ausgelöst, wenn Trigger At erreicht ist.")]
    public UnityEvent onPulled;

    [Tooltip("Wird ausgelöst, wenn der Hebel wieder ganz in Ruheposition ist.")]
    public UnityEvent onReturned;

    /// <summary>Aktueller Winkel in Grad (0 = Ruhe).</summary>
    public float Angle => m_Angle;

    /// <summary>0 = Ruhe, 1 = ganz unten.</summary>
    public float Progress => Mathf.Approximately(maxAngle, 0f) ? 0f : m_Angle / maxAngle;

    public bool IsGrabbed => m_Interactor != null;

    XRSimpleInteractable m_Interactable;
    IXRSelectInteractor m_Interactor;
    Quaternion m_RestRotation;
    Vector3 m_GrabDir;
    float m_GrabAngle;
    float m_Angle;
    float m_ReleaseTime;
    bool m_Armed = true;

    Vector3 WorldAxis => pivot.rotation * localAxis.normalized;

    void Awake()
    {
        m_Interactable = GetComponent<XRSimpleInteractable>();
        if (pivot == null)
            pivot = transform;
        m_RestRotation = pivot.localRotation;
    }

    void OnEnable()
    {
        m_Interactable.selectEntered.AddListener(OnSelectEntered);
        m_Interactable.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
        m_Interactable.selectExited.RemoveListener(OnSelectExited);
        m_Interactor = null;
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        m_Interactor = args.interactorObject;
        // Relativ zum Greifpunkt rechnen, damit der Hebel beim Anfassen nicht springt
        m_GrabDir = DirectionToHand();
        m_GrabAngle = m_Angle;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (args.interactorObject != m_Interactor)
            return;
        m_Interactor = null;
        m_ReleaseTime = Time.time;
    }

    void Update()
    {
        if (IsGrabbed)
        {
            Vector3 dir = DirectionToHand();
            if (dir != Vector3.zero && m_GrabDir != Vector3.zero)
            {
                float delta = Vector3.SignedAngle(m_GrabDir, dir, WorldAxis);
                SetAngle(m_GrabAngle + delta);
            }
        }
        else if (m_Angle != 0f && Time.time >= m_ReleaseTime + returnDelay)
        {
            SetAngle(Mathf.MoveTowards(m_Angle, 0f, returnSpeed * Time.deltaTime));
            if (m_Angle == 0f)
                onReturned.Invoke();
        }
    }

    /// <summary>Richtung Pivot -> Hand, projiziert auf die Drehebene (alles außerhalb der Achse wird ignoriert).</summary>
    Vector3 DirectionToHand()
    {
        Vector3 hand = m_Interactor.GetAttachTransform(m_Interactable).position;
        Vector3 dir = Vector3.ProjectOnPlane(hand - pivot.position, WorldAxis);
        return dir.sqrMagnitude < 1e-6f ? Vector3.zero : dir;
    }

    void SetAngle(float angle)
    {
        m_Angle = Mathf.Clamp(angle, Mathf.Min(0f, maxAngle), Mathf.Max(0f, maxAngle));
        pivot.localRotation = m_RestRotation * Quaternion.AngleAxis(m_Angle, localAxis.normalized);

        float p = Progress;
        if (m_Armed && p >= triggerAt)
        {
            m_Armed = false;
            onPulled.Invoke();
        }
        else if (!m_Armed && p <= rearmAt)
        {
            m_Armed = true;
        }
    }

    void OnDrawGizmosSelected()
    {
        Transform t = pivot != null ? pivot : transform;
        Vector3 axis = t.rotation * localAxis.normalized;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(t.position - axis * 0.1f, t.position + axis * 0.1f);
    }
}
