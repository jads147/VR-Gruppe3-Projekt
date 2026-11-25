using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Throwable grappling hook. Grab it and throw it: a rope connects it to the hand that threw it.
/// If it hits a Jenga block hard enough, it sticks to it. Pulling the hand back (along the rope) then pulls the
/// block straight out of the face that was hit (end face = lengthwise, long side = sideways). The force only acts
/// along that one block axis, never up/down. Load-bearing blocks resist; pulling too hard snaps the rope.
/// After one successful pull the hook is used up (optional).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public sealed class JengaHook : MonoBehaviour
{
    private enum State { Idle, Flying, Stuck, Spent }

    [Header("Sticking")]
    [SerializeField] private LayerMask blockLayers;
    [Tooltip("Minimum impact speed (m/s) for the hook to stick. Slower touches just bounce off.")]
    [SerializeField, Min(0f)] private float minStickSpeed = 1f;
    [Tooltip("Only for hits on the top/bottom face: the nearest side is used, unless the hit is closer to the " +
             "middle than this (fraction of half the size); then the side facing the player is used.")]
    [SerializeField, Range(0f, 1f)] private float sideHitThreshold = 0.2f;

    [Header("Pulling")]
    [Tooltip("How far (meters) the hand has to be pulled back to move a block completely out " +
             "(its length when pulled lengthwise, its width when pulled sideways).")]
    [SerializeField, Min(0.05f)] private float handPullToRemove = 0.6f;
    [Tooltip("Pull acceleration per meter the block lags behind its target position (m/s² per m).")]
    [SerializeField, Min(0f)] private float stiffness = 40f;
    [Tooltip("Brakes the block's outward speed, so it follows the hand instead of shooting out.")]
    [SerializeField, Min(0f)] private float damping = 8f;
    [Tooltip("Upper limit of the pull acceleration (m/s²). Low = load-bearing blocks stay stuck and the rope snaps.")]
    [SerializeField, Min(0f)] private float maxAcceleration = 20f;
    [Tooltip("Let go automatically once the block has moved its full size along the pull direction.")]
    [SerializeField] private bool releaseWhenOut = true;
    [Tooltip("The rope snaps if the hand is pulled this much (meters) further than the block can follow.")]
    [SerializeField, Min(0.05f)] private float breakStretch = 0.5f;

    [Header("Rope")]
    [Tooltip("The rope tears if the hook gets further away from the hand than this (meters).")]
    [SerializeField, Min(1f)] private float maxRopeLength = 30f;
    [Tooltip("After a miss (hit something that is not a block), the rope is dropped after this many seconds.")]
    [SerializeField, Min(0f)] private float missRopeTime = 1.5f;
    [Tooltip("The rope is dropped if the hook has been flying this long without hitting anything.")]
    [SerializeField, Min(0.5f)] private float maxFlightTime = 5f;

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
    [Tooltip("Hand lead (meters) at which the rope is pulled completely straight.")]
    [SerializeField, Min(0.01f)] private float tautDistance = 0.08f;
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
    private float flightTime;
    private float missTime = -1f;
    private float destroyAt = float.PositiveInfinity;

    // Stuck state. Pull axis and center are stored in the block's local space, so they follow it.
    private Rigidbody target;
    private FixedJoint joint;
    private float storedMass;
    private Vector3 localCenter;
    private Vector3 localPullAxis;
    private Vector3 handStart;
    private Vector3 ropeStartDirection;
    private Vector3 startCenter;
    private Vector3 startDirection;
    private float sizeAlongPull;

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
    }

    private void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnThrown);
        Application.onBeforeRender += UpdateLine;
    }

    private void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnThrown);
        Application.onBeforeRender -= UpdateLine;
        Unstick();
        DropRope();
        if (line != null)
            line.enabled = false;
    }

    private void OnDestroy()
    {
        if (lineInstance != null)
            Destroy(lineInstance);
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
        state = State.Flying;
        flightTime = 0f;
        missTime = -1f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (state != State.Flying)
            return;

        Rigidbody other = collision.rigidbody;
        bool isBlock = other != null && !other.isKinematic &&
                       (blockLayers.value & (1 << other.gameObject.layer)) != 0;

        if (isBlock && collision.relativeVelocity.magnitude >= minStickSpeed && collision.contactCount > 0)
        {
            Stick(other, collision.GetContact(0).point);
            return;
        }

        // Hit something else (or too softly): keep the slack rope a moment, then drop it.
        if (missTime < 0f)
            missTime = 0f;
    }

    // ---------------------------------------------------------------- sticking

    private void Stick(Rigidbody block, Vector3 point)
    {
        if (hand == null || !TryGetLocalBounds(block.transform, out Bounds bounds))
            return;

        Transform blockTransform = block.transform;
        Vector3 size = Vector3.Scale(bounds.size, Abs(blockTransform.lossyScale));
        Vector3 worldCenter = blockTransform.TransformPoint(bounds.center);

        // The face that was hit decides the pull axis; the axis pointing (most) up/down is never used.
        int verticalAxis = MostAlignedAxis(blockTransform.InverseTransformDirection(Vector3.up));
        int axis = ClosestSideAxis(blockTransform, worldCenter, size, point, -1, out float offset, out _);
        float sign = Mathf.Sign(offset);

        if (axis == verticalAxis)
        {
            axis = ClosestSideAxis(blockTransform, worldCenter, size, point, verticalAxis, out offset,
                out float ratio);
            sign = ratio > sideHitThreshold
                ? Mathf.Sign(offset)
                : Mathf.Sign(blockTransform.InverseTransformDirection(hand.position - worldCenter)[axis]);
        }

        Vector3 localAxis = Vector3.zero;
        localAxis[axis] = sign;

        target = block;
        localCenter = bounds.center;
        localPullAxis = localAxis;
        handStart = hand.position;
        ropeStartDirection = (hand.position - point).normalized;
        startCenter = worldCenter;
        startDirection = blockTransform.TransformDirection(localAxis).normalized;
        sizeAlongPull = size[axis];

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
        target = null;

        body.mass = storedMass;
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].isTrigger = colliderWasTrigger[i];
    }

    private void DropRope()
    {
        hand = null;
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

        GetPullState(out float handPull, out float travel, out float handLead);

        // Hand pulled much further than the block could follow (stuck block): the rope snaps.
        if (handLead > breakStretch || (releaseWhenOut && travel >= sizeAlongPull))
        {
            FinishPull();
            return;
        }

        // Where the block should be: the hand pull, scaled so handPullToRemove = the block's size along the axis.
        float error = handPull * PullGain - travel;
        if (error <= 0f)
            return;

        Vector3 pullDirection = target.transform.TransformDirection(localPullAxis).normalized;
        float outwardSpeed = Vector3.Dot(target.linearVelocity, pullDirection);
        // Never negative: the rope can only pull, never push the block back in.
        float acceleration = Mathf.Clamp(stiffness * error - damping * outwardSpeed, 0f, maxAcceleration);
        if (acceleration > 0f)
            target.AddForce(pullDirection * acceleration, ForceMode.Acceleration);
    }

    private void UpdateFlight()
    {
        flightTime += Time.fixedDeltaTime;
        if (missTime >= 0f)
            missTime += Time.fixedDeltaTime;

        bool tooFar = hand == null || Vector3.Distance(hand.position, RopeEnd) > maxRopeLength;
        if (tooFar || flightTime > maxFlightTime || missTime > missRopeTime)
            DropRope();
    }

    private float PullGain => sizeAlongPull / handPullToRemove;

    /// <summary>World position where the rope is attached to the hook.</summary>
    private Vector3 RopeEnd => ropeAnchor != null ? ropeAnchor.position : transform.position;

    /// <summary>
    /// handPull: how far the hand moved back along the rope since sticking (m).
    /// travel: how far the block moved out along its pull axis (m).
    /// handLead: how much further the hand is pulled than the block has followed, in hand meters.
    /// </summary>
    private void GetPullState(out float handPull, out float travel, out float handLead)
    {
        handPull = Vector3.Dot(hand.position - handStart, ropeStartDirection);
        Vector3 center = target.transform.TransformPoint(localCenter);
        travel = Vector3.Dot(center - startCenter, startDirection);
        handLead = handPull - travel / PullGain;
    }

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
            GetPullState(out _, out _, out float handLead);
            taut = Mathf.Clamp01(handLead / tautDistance);
            tension = Mathf.Clamp01(handLead / breakStretch);
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

    /// <summary>
    /// The axis whose face is nearest to <paramref name="point"/> (skipping <paramref name="skipAxis"/>).
    /// <paramref name="offset"/> is the signed distance from the center along it, <paramref name="ratio"/> that
    /// distance relative to half the size (0 = middle, 1 = on the face).
    /// </summary>
    private static int ClosestSideAxis(Transform block, Vector3 center, Vector3 size, Vector3 point, int skipAxis,
        out float offset, out float ratio)
    {
        int best = 0;
        offset = 0f;
        ratio = -1f;
        for (int i = 0; i < 3; i++)
        {
            if (i == skipAxis)
                continue;

            Vector3 localAxis = Vector3.zero;
            localAxis[i] = 1f;
            float axisOffset = Vector3.Dot(point - center, block.TransformDirection(localAxis).normalized);
            float axisRatio = Mathf.Abs(axisOffset) / Mathf.Max(1e-4f, size[i] * 0.5f);
            if (axisRatio <= ratio)
                continue;

            best = i;
            offset = axisOffset;
            ratio = axisRatio;
        }
        return best;
    }
}
