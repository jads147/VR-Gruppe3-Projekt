using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Throwable grappling hook. Grab it and throw it: a rope connects it to the hand that threw it.
/// If it hits a Jenga block hard enough, it sticks to it. The rope then behaves like an elastic band between hand
/// and hook: moving the hand away stretches it and pulls the block towards the hand.
/// The pull has 2 degrees of freedom: the block moves freely on the world X/Z plane, never up/down
/// (optionally its height and rotation are locked completely while it is pulled).
/// If the rope tension exceeds <see cref="breakForce"/> (e.g. a load-bearing block that doesn't move), it snaps.
/// Otherwise the rope stays attached (also after a miss, or once the block is out) until the trigger of the hand
/// that threw the hook is pressed. After one pull the hook is used up (optional).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public sealed class JengaHook : MonoBehaviour
{
    private enum State { Idle, Flying, Stuck, Spent }

    /// <summary>Controller buttons that can release the rope (several can be combined).</summary>
    [System.Flags]
    public enum ReleaseButtons
    {
        None = 0,
        Trigger = 1 << 0,
        Grip = 1 << 1,
        PrimaryButton = 1 << 2,   // A / X
        SecondaryButton = 1 << 3, // B / Y
        ThumbstickClick = 1 << 4,
        XriActivate = 1 << 5,     // the Activate input of the throwing interactor (as set up in the rig)
    }

    [Header("Sticking")]
    [SerializeField] private LayerMask blockLayers;
    [Tooltip("Minimum impact speed (m/s) for the hook to stick. Slower touches just bounce off.")]
    [SerializeField, Min(0f)] private float minStickSpeed = 1f;

    [Header("Rope Strength")]
    [Tooltip("How much the rope withstands (Newton). If the rope tension gets higher, it snaps. " +
             "A Jenga block weighs ~1 kg (≈10 N): low values = only loose blocks can be pulled out.")]
    [SerializeField, Min(0.1f)] private float breakForce = 40f;
    [Tooltip("Rope stiffness (Newton per meter of stretch). Higher = the block follows the hand more directly, " +
             "and the rope snaps after less stretch (snap stretch = Break Force / Stiffness).")]
    [SerializeField, Min(1f)] private float ropeStiffness = 300f;
    [Tooltip("Damps the block's movement relative to the hand (Newton per m/s), so it doesn't overshoot.")]
    [SerializeField, Min(0f)] private float ropeDamping = 10f;
    [Tooltip("The rope reels in automatically when the hand comes closer, so it never hangs slack while stuck.")]
    [SerializeField] private bool autoReel = true;

    [Header("Pulling (2 DOF: X/Z only)")]
    [Tooltip("While pulled, the block's height and rotation are frozen: it can only slide on X/Z. " +
             "Off = only the pull force is horizontal; gravity and collisions still move the block freely.")]
    [SerializeField] private bool lockBlockHeight = true;
    [Tooltip("Once the block has moved its full length or width out of its place, the height/rotation lock is " +
             "removed so it can fall out of the tower. The rope stays attached.")]
    [SerializeField] private bool unlockWhenOut = true;

    [Header("Rope")]
    [Tooltip("Which controller button(s) release the rope. Several can be selected.")]
    [SerializeField] private ReleaseButtons releaseButtons = ReleaseButtons.Trigger;
    [Tooltip("Off = only the buttons of the hand that threw the hook count. On = either hand.")]
    [SerializeField] private bool releaseWithEitherHand = false;
    [Tooltip("Optional. An additional Input Action that also releases the rope (works with any hand).")]
    [FormerlySerializedAs("releaseButton")]
    [SerializeField] private InputActionReference customReleaseAction;
    [Tooltip("The rope tears if the hook gets further away from the hand than this (meters).")]
    [SerializeField, Min(1f)] private float maxRopeLength = 30f;

    [Header("After Use")]
    [Tooltip("Destroy the hook after it has pulled a block (or snapped). Misses don't count.")]
    [SerializeField] private bool destroyAfterUse = true;
    [SerializeField, Min(0f)] private float destroyDelay = 2f;

    [Header("Line")]
    [Tooltip("Where the rope is attached to the hook (e.g. an empty child at the eye ring). " +
             "Leave empty to use the hook's pivot.")]
    [SerializeField] private Transform ropeAnchor;
    [SerializeField, Min(0.001f)] private float lineWidth = 0.012f;
    [SerializeField] private Color slackColor = new Color(0.85f, 0.65f, 0.35f);
    [SerializeField] private Color tenseColor = new Color(1f, 0.25f, 0.15f);
    [Tooltip("Sag of the loose rope, as a fraction of its length.")]
    [SerializeField, Range(0f, 0.3f)] private float slackSag = 0.05f;
    [Tooltip("Rope stretch (meters) at which the rope is pulled completely straight.")]
    [SerializeField, Min(0.001f)] private float tautStretch = 0.02f;
    [SerializeField, Range(2, 64)] private int lineSegments = 24;
    [Tooltip("Optional. Leave empty to use URP Unlit.")]
    [SerializeField] private Material lineMaterial;

    private XRGrabInteractable grab;
    private Rigidbody body;
    private Collider[] colliders;
    private bool[] colliderWasTrigger;
    private LineRenderer line;
    private Material lineInstance;

    private State state = State.Idle;
    private Transform hand;
    private XRBaseInputInteractor handInput;
    private InteractorHandedness handSide;
    private InputAction leftReleaseAction;
    private InputAction rightReleaseAction;
    private float destroyAt = float.PositiveInfinity;

    // Stuck state.
    private Rigidbody target;
    private FixedJoint joint;
    private float storedMass;
    private RigidbodyConstraints storedConstraints;
    private Vector3 localCenter;
    private Vector3 startCenter;
    private Quaternion startRotation;
    private Vector3 blockSize;
    private int verticalAxis;
    private float ropeLength;
    private Vector3 lastHandPosition;
    private Vector3 handVelocity;
    private bool heightUnlocked;

    private void Reset()
    {
        blockLayers = LayerMask.GetMask("Jenga");
    }

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        colliderWasTrigger = new bool[colliders.Length];
        CreateLine();
        leftReleaseAction = CreateReleaseAction("LeftHand");
        rightReleaseAction = CreateReleaseAction("RightHand");
    }

    private void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnThrown);
        Application.onBeforeRender += UpdateLine;
        leftReleaseAction?.Enable();
        rightReleaseAction?.Enable();
        if (customReleaseAction != null && customReleaseAction.action != null)
            customReleaseAction.action.Enable();
    }

    private void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnThrown);
        Application.onBeforeRender -= UpdateLine;
        leftReleaseAction?.Disable();
        rightReleaseAction?.Disable();
        Unstick();
        DropRope();
        if (line != null)
            line.enabled = false;
    }

    private void OnDestroy()
    {
        if (lineInstance != null)
            Destroy(lineInstance);
        leftReleaseAction?.Dispose();
        rightReleaseAction?.Dispose();
    }

    // ---------------------------------------------------------------- throw / grab

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // Grabbing it again (e.g. with the far ray) frees it from the block.
        bool wasStuck = state == State.Stuck;
        Unstick();
        DropRope();
        if (wasStuck)
            Spend();
        else if (state != State.Spent)
            state = State.Idle;
    }

    private void OnThrown(SelectExitEventArgs args)
    {
        if (args.isCanceled || state == State.Spent)
            return;

        hand = args.interactorObject.transform;
        handInput = args.interactorObject as XRBaseInputInteractor;
        handSide = args.interactorObject is XRBaseInteractor interactor
            ? interactor.handedness
            : InteractorHandedness.None;
        state = State.Flying;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (state != State.Flying)
            return;

        Rigidbody other = collision.rigidbody;
        bool isBlock = other != null && !other.isKinematic &&
                       (blockLayers.value & (1 << other.gameObject.layer)) != 0;

        // Anything else (or too soft): the hook just lies there, the rope stays until the trigger is pressed.
        if (isBlock && collision.relativeVelocity.magnitude >= minStickSpeed)
            Stick(other);
    }

    // ---------------------------------------------------------------- sticking

    private void Stick(Rigidbody block)
    {
        if (hand == null || !TryGetLocalBounds(block.transform, out Bounds bounds))
            return;

        Transform blockTransform = block.transform;
        target = block;
        localCenter = bounds.center;
        startCenter = blockTransform.TransformPoint(bounds.center);
        startRotation = blockTransform.rotation;
        blockSize = Vector3.Scale(bounds.size, Abs(blockTransform.lossyScale));
        verticalAxis = MostAlignedAxis(blockTransform.InverseTransformDirection(Vector3.up));

        ropeLength = Vector3.Distance(hand.position, RopeEnd);
        lastHandPosition = hand.position;
        handVelocity = Vector3.zero;
        heightUnlocked = false;

        // Ride on the block: almost weightless, and its colliders don't push the neighbours.
        // (useGravity is left alone: XRGrabInteractable restores it on release if the hook is grabbed again.)
        storedMass = body.mass;
        body.mass = Mathf.Max(0.001f, block.mass * 0.01f);
        body.linearVelocity = block.linearVelocity;
        body.angularVelocity = Vector3.zero;
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderWasTrigger[i] = colliders[i].isTrigger;
            colliders[i].isTrigger = true;
        }

        // 2 DOF: the block may only slide on X/Z while the rope holds it.
        storedConstraints = block.constraints;
        if (lockBlockHeight)
        {
            block.constraints = storedConstraints | RigidbodyConstraints.FreezePositionY |
                                RigidbodyConstraints.FreezeRotation;
            block.linearVelocity = Vector3.ProjectOnPlane(block.linearVelocity, Vector3.up);
            block.angularVelocity = Vector3.zero;
        }

        joint = gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = block;
        joint.enableCollision = false;

        state = State.Stuck;
        block.WakeUp();
    }

    private void Unstick()
    {
        if (target == null && joint == null)
            return;

        if (joint != null)
            Destroy(joint);
        joint = null;

        if (target != null)
        {
            target.constraints = storedConstraints;
            target.WakeUp();
        }
        target = null;

        body.mass = storedMass;
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].isTrigger = colliderWasTrigger[i];
    }

    private void DropRope()
    {
        hand = null;
        handInput = null;
        if (state == State.Flying)
            state = State.Idle;
    }

    // Used once: free the block, drop the rope and (optionally) remove the hook after a moment.
    private void FinishPull()
    {
        Unstick();
        DropRope();
        Spend();
    }

    private void Spend()
    {
        state = destroyAfterUse ? State.Spent : State.Idle;
        if (destroyAfterUse)
            destroyAt = Time.time + destroyDelay;
    }

    private void Update()
    {
        if (Time.time >= destroyAt && !grab.isSelected)
            Destroy(gameObject);

        if (hand != null && ReleasePressed())
        {
            // Stuck: the pull is over (hook used up). Not stuck (miss): just drop the rope, the hook stays usable.
            if (state == State.Stuck)
                FinishPull();
            else
                DropRope();
        }
    }

    private bool ReleasePressed()
    {
        // Unknown hand (e.g. simulator without handedness): accept both controllers.
        bool left = releaseWithEitherHand || handSide != InteractorHandedness.Right;
        bool right = releaseWithEitherHand || handSide != InteractorHandedness.Left;

        if (left && leftReleaseAction != null && leftReleaseAction.WasPerformedThisFrame())
            return true;
        if (right && rightReleaseAction != null && rightReleaseAction.WasPerformedThisFrame())
            return true;

        if ((releaseButtons & ReleaseButtons.XriActivate) != 0 && handInput != null &&
            handInput.activateInput != null && handInput.activateInput.ReadWasPerformedThisFrame())
            return true;

        return customReleaseAction != null && customReleaseAction.action != null &&
               customReleaseAction.action.WasPerformedThisFrame();
    }

    /// <summary>A button action bound to the selected controller buttons of one hand (by XR usage).</summary>
    private InputAction CreateReleaseAction(string hand)
    {
        var action = new InputAction($"HookRelease{hand}", InputActionType.Button);
        AddUsage(ReleaseButtons.Trigger, "TriggerButton");
        AddUsage(ReleaseButtons.Grip, "GripButton");
        AddUsage(ReleaseButtons.PrimaryButton, "PrimaryButton");
        AddUsage(ReleaseButtons.SecondaryButton, "SecondaryButton");
        AddUsage(ReleaseButtons.ThumbstickClick, "Primary2DAxisClick");

        if (action.bindings.Count > 0)
            return action;
        action.Dispose();
        return null;

        void AddUsage(ReleaseButtons button, string usage)
        {
            if ((releaseButtons & button) != 0)
                action.AddBinding($"<XRController>{{{hand}}}/{{{usage}}}");
        }
    }

    // ---------------------------------------------------------------- pulling

    private void FixedUpdate()
    {
        if (state == State.Flying)
        {
            UpdateFlight();
            return;
        }

        if (state != State.Stuck)
            return;

        if (target == null || !target.gameObject.activeInHierarchy || joint == null || hand == null)
        {
            FinishPull();
            return;
        }

        // Hand velocity (smoothed: the hand pose updates per frame, not per physics step).
        Vector3 handPosition = hand.position;
        Vector3 rawVelocity = (handPosition - lastHandPosition) / Time.fixedDeltaTime;
        handVelocity = Vector3.Lerp(handVelocity, rawVelocity, 0.3f);
        lastHandPosition = handPosition;

        Vector3 toHand = handPosition - RopeEnd;
        float distance = toHand.magnitude;
        if (autoReel && distance < ropeLength)
            ropeLength = distance;

        float stretch = distance - ropeLength;
        float tension = ropeStiffness * Mathf.Max(0f, stretch);

        // The rope can't take more: it snaps.
        if (tension > breakForce)
        {
            FinishPull();
            return;
        }

        // Out of the tower: let it fall/tilt freely from now on, but keep it on the rope.
        if (unlockWhenOut && !heightUnlocked && IsOut())
        {
            heightUnlocked = true;
            target.constraints = storedConstraints;
            target.WakeUp();
        }

        if (stretch <= 0f)
            return;

        // 2 DOF: only the horizontal (X/Z) part of the rope pull acts on the block.
        Vector3 flat = Vector3.ProjectOnPlane(toHand, Vector3.up);
        if (flat.sqrMagnitude < 1e-6f)
            return;
        Vector3 pullDirection = flat.normalized;

        float relativeSpeed = Vector3.Dot(target.linearVelocity - handVelocity, pullDirection);
        float force = Mathf.Clamp(tension * (flat.magnitude / distance) - ropeDamping * relativeSpeed,
            0f, breakForce);
        if (force > 0f)
            target.AddForce(pullDirection * force, ForceMode.Force);
    }

    private void UpdateFlight()
    {
        if (hand == null || Vector3.Distance(hand.position, RopeEnd) > maxRopeLength)
            DropRope();
    }

    /// <summary>
    /// True once the block has moved its full length or width (along its own horizontal axes) away from where it
    /// was, or (with free height) has dropped by half its height, i.e. fallen out of the tower.
    /// Measured from where it was when the hook stuck.
    /// </summary>
    private bool IsOut()
    {
        Vector3 center = target.transform.TransformPoint(localCenter);
        Vector3 travel = Quaternion.Inverse(startRotation) * (center - startCenter);

        for (int i = 0; i < 3; i++)
        {
            if (i != verticalAxis && Mathf.Abs(travel[i]) >= blockSize[i])
                return true;
        }

        return !lockBlockHeight && startCenter.y - center.y > blockSize[verticalAxis] * 0.5f;
    }

    /// <summary>World position where the rope is attached to the hook.</summary>
    private Vector3 RopeEnd => ropeAnchor != null ? ropeAnchor.position : transform.position;

    // ---------------------------------------------------------------- rope visual

    [BeforeRenderOrder(XRInteractionUpdateOrder.k_BeforeRenderOrder + 1)]
    private void UpdateLine()
    {
        if (line == null)
            return;

        bool visible = hand != null && (state == State.Flying || state == State.Stuck);
        line.enabled = visible;
        if (!visible)
            return;

        Vector3 start = hand.position;
        Vector3 end = RopeEnd;
        float length = Vector3.Distance(start, end);

        float taut = 0f;
        float tension = 0f;
        if (state == State.Stuck && target != null)
        {
            float stretch = length - ropeLength;
            taut = Mathf.Clamp01(stretch / tautStretch);
            tension = Mathf.Clamp01(ropeStiffness * stretch / breakForce);
        }

        // Loose rope hangs down; pulled, it gets straight, then thinner and redder towards snapping.
        float sag = slackSag * length * (1f - taut);
        line.widthMultiplier = lineWidth * Mathf.Lerp(1f, 0.45f, tension);
        if (lineInstance != null)
            lineInstance.color = Color.Lerp(slackColor, tenseColor, tension);

        // Quadratic Bezier; the control point is twice the sag so the curve's middle hangs by 'sag'.
        Vector3 control = (start + end) * 0.5f + Vector3.down * (sag * 2f);
        line.positionCount = lineSegments + 1;
        for (int i = 0; i <= lineSegments; i++)
        {
            float t = i / (float)lineSegments;
            float u = 1f - t;
            line.SetPosition(i, u * u * start + 2f * u * t * control + t * t * end);
        }
    }

    private void CreateLine()
    {
        var lineObject = new GameObject("HookRope");
        lineObject.transform.SetParent(transform, false);

        line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = 0;
        line.enabled = false;

        if (lineMaterial != null)
        {
            lineInstance = new Material(lineMaterial);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("JengaHook: URP Unlit shader not found. Assign a Line Material.", this);
                return;
            }
            lineInstance = new Material(shader);
        }

        lineInstance.name = "HookRope (Instance)";
        line.sharedMaterial = lineInstance;
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Bounds of all non-trigger box colliders of the block, in the block's local space.</summary>
    private static bool TryGetLocalBounds(Transform block, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        foreach (BoxCollider box in block.GetComponentsInChildren<BoxCollider>())
        {
            if (!box.enabled || box.isTrigger)
                continue;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = box.center + Vector3.Scale(box.size * 0.5f, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                Vector3 point = block.InverseTransformPoint(box.transform.TransformPoint(local));

                if (hasBounds)
                {
                    bounds.Encapsulate(point);
                }
                else
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
            }
        }

        if (!hasBounds && block.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
        {
            bounds = meshFilter.sharedMesh.bounds;
            hasBounds = true;
        }

        return hasBounds;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    /// <summary>Index (0-2) of the local axis most parallel to <paramref name="localDirection"/>.</summary>
    private static int MostAlignedAxis(Vector3 localDirection)
    {
        Vector3 a = Abs(localDirection);
        return a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
    }
}
