using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Hand-held lasso that pulls single Jenga blocks out of a structure.
/// Hold the item, aim at a block and fire (FireTrigger -> Toggle(), the optional fire button, or XRI Activate):
/// the rope sticks to the hit point. Pulling the hand back (along the rope) pulls the block straight out of the
/// face that was hit: an end face pulls it lengthwise, a long side pulls it sideways. The force only acts along
/// that one block axis, never up/down (top/bottom hits use the nearest side instead).
/// A short hand pull moves the block out completely, whatever its size (see handPullToRemove).
/// Toggle again / let go of the button to release.
/// </summary>
[RequireComponent(typeof(XRBaseInteractable))]
public sealed class JengaLasso : MonoBehaviour
{
    [Header("Aiming")]
    [Tooltip("Where the rope starts. Its forward (blue) axis is the aim direction.")]
    [SerializeField] private Transform tip;
    [SerializeField, Min(0.1f)] private float maxRange = 40f;
    [SerializeField] private LayerMask blockLayers;
    [SerializeField] private bool showAimLine = true;

    [Header("Firing")]
    [Tooltip("Optional button (e.g. primaryButton / A or X). Hold it while holding the lasso = rope attached, " +
             "let go = released. Leave empty if you use a FireTrigger with Toggle() instead.")]
    [SerializeField] private InputActionReference fireButton;
    [Tooltip("Also fire on XRI Activate. Turn off if grabbing and activating are on the same button (trigger).")]
    [SerializeField] private bool useActivateEvent = false;

    [Header("Pulling")]
    [Tooltip("How far (meters) the hand has to be pulled back to move a block completely out " +
             "(its length when pulled lengthwise, its width when pulled sideways). " +
             "Independent of the block size, so big blocks don't need huge arm movements.")]
    [SerializeField, Min(0.05f)] private float handPullToRemove = 0.5f;
    [Tooltip("Pull acceleration per meter the block lags behind its target position (m/s² per m). " +
             "Independent of block mass.")]
    [SerializeField, Min(0f)] private float stiffness = 80f;
    [Tooltip("Brakes the block's outward speed, so it follows the hand instead of shooting out.")]
    [SerializeField, Min(0f)] private float damping = 10f;
    [Tooltip("Upper limit of the pull acceleration (m/s²). Higher = load-bearing blocks come out more easily.")]
    [SerializeField, Min(0f)] private float maxAcceleration = 60f;
    [Tooltip("Only for hits on the top/bottom face: the nearest side is used, unless the hit is closer to the " +
             "middle than this (fraction of half the size); then the side facing the lasso is used.")]
    [SerializeField, Range(0f, 1f)] private float sideHitThreshold = 0.2f;
    [Tooltip("Let go automatically once the block has moved its full size along the pull direction, " +
             "so it can fall freely.")]
    [SerializeField] private bool releaseWhenOut = true;
    [Tooltip("The rope snaps if the hand is pulled this much (meters) further than the block can follow " +
             "(e.g. a stuck, load-bearing block).")]
    [SerializeField, Min(0.05f)] private float breakStretch = 0.8f;

    [Header("Line")]
    [SerializeField, Min(0.001f)] private float lineWidth = 0.012f;
    [SerializeField] private Color slackColor = new Color(0.85f, 0.65f, 0.35f);
    [SerializeField] private Color tenseColor = new Color(1f, 0.25f, 0.15f);
    [SerializeField] private Color aimColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color aimValidColor = new Color(0.4f, 1f, 0.4f);
    [Tooltip("Sag of the loose rope, as a fraction of its length.")]
    [SerializeField, Range(0f, 0.3f)] private float slackSag = 0.05f;
    [Tooltip("Hand lead (meters) at which the rope is pulled completely straight.")]
    [SerializeField, Min(0.01f)] private float tautDistance = 0.08f;
    [SerializeField, Range(2, 64)] private int lineSegments = 24;
    [Tooltip("Optional. Leave empty to use URP Unlit.")]
    [SerializeField] private Material lineMaterial;

    private XRBaseInteractable interactable;
    private Rigidbody ownBody;
    private readonly RaycastHit[] aimHits = new RaycastHit[32];
    private LineRenderer line;
    private Material lineInstance;

    // Current target. Anchor and axis are stored in the block's local space, so they follow it.
    private Rigidbody target;
    private Vector3 localAnchor;
    private Vector3 localPullAxis;
    private Vector3 tipStart;
    private Vector3 ropeStartDirection;
    private Vector3 startCenter;
    private Vector3 startDirection;
    private float sizeAlongPull;
    private Vector3 localCenter;

    public bool IsAttached => target != null;

    private void Reset()
    {
        blockLayers = LayerMask.GetMask("Jenga");
    }

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        ownBody = GetComponentInParent<Rigidbody>();
        if (tip == null)
        {
            Debug.LogWarning("JengaLasso: Tip is not assigned, using the lasso's own transform.", this);
            tip = transform;
        }
        CreateLine();
    }

    private void OnEnable()
    {
        interactable.activated.AddListener(OnActivated);
        interactable.deactivated.AddListener(OnDeactivated);
        interactable.selectExited.AddListener(OnReleasedFromHand);
        Application.onBeforeRender += UpdateLine;

        if (fireButton != null && fireButton.action != null)
        {
            fireButton.action.performed += OnFireButtonPressed;
            fireButton.action.canceled += OnFireButtonReleased;
            fireButton.action.Enable();
        }
    }

    private void OnDisable()
    {
        interactable.activated.RemoveListener(OnActivated);
        interactable.deactivated.RemoveListener(OnDeactivated);
        interactable.selectExited.RemoveListener(OnReleasedFromHand);
        Application.onBeforeRender -= UpdateLine;

        if (fireButton != null && fireButton.action != null)
        {
            fireButton.action.performed -= OnFireButtonPressed;
            fireButton.action.canceled -= OnFireButtonReleased;
        }

        Detach();
        if (line != null)
            line.enabled = false;
    }

    private void OnDestroy()
    {
        if (lineInstance != null)
            Destroy(lineInstance);
    }

    // XRI "Activate" while the lasso is held (only useful if grabbing is NOT on the same button).
    private void OnActivated(ActivateEventArgs args)
    {
        if (useActivateEvent)
            Fire();
    }

    private void OnDeactivated(DeactivateEventArgs args)
    {
        if (useActivateEvent)
            Release();
    }

    private void OnReleasedFromHand(SelectExitEventArgs args) => Release();

    // Optional extra button (e.g. A/X): hold = rope attached, let go = released.
    private void OnFireButtonPressed(InputAction.CallbackContext context)
    {
        if (interactable.isSelected)
            Fire();
    }

    private void OnFireButtonReleased(InputAction.CallbackContext context) => Release();

    /// <summary>Throws the rope at the aimed block. Void, so it can also be linked in UnityEvents.</summary>
    public void Fire() => TryAttach();

    /// <summary>Lets go of the block.</summary>
    public void Release() => Detach();

    /// <summary>
    /// Not attached: fire. Attached: let go. Link this to a FireTrigger's Hover Entered (like the PotatoCannon),
    /// so one touch attaches the rope and the next touch releases it.
    /// </summary>
    public void Toggle()
    {
        if (IsAttached)
            Release();
        else
            Fire();
    }

    // ---------------------------------------------------------------- attaching

    // Finds the nearest movable block along the aim ray. Everything else (portal, walls, the lasso itself,
    // static or kinematic objects) is looked through instead of stopping the ray.
    private bool Aim(out RaycastHit hit)
    {
        hit = default;
        int count = Physics.RaycastNonAlloc(tip.position, tip.forward, aimHits, maxRange, blockLayers,
            QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = aimHits[i];
            Rigidbody body = candidate.rigidbody;
            if (body == null || body.isKinematic || body == ownBody || candidate.distance >= nearest)
                continue;
            if ((blockLayers.value & (1 << body.gameObject.layer)) == 0)
                continue;

            nearest = candidate.distance;
            hit = candidate;
        }

        return nearest < float.MaxValue;
    }

    public bool TryAttach()
    {
        if (!Aim(out RaycastHit hit))
            return false;

        Rigidbody body = hit.rigidbody;
        if (!TryGetLocalBounds(body.transform, out Bounds bounds))
            return false;

        Transform blockTransform = body.transform;
        Vector3 size = Vector3.Scale(bounds.size, Abs(blockTransform.lossyScale));
        Vector3 worldCenter = blockTransform.TransformPoint(bounds.center);

        // The local axis that points (most) up/down is never pulled along.
        int verticalAxis = MostAlignedAxis(blockTransform.InverseTransformDirection(Vector3.up), -1);

        // Pull along the axis of the face that was hit (end face = lengthwise, long side = sideways).
        int axis = MostAlignedAxis(blockTransform.InverseTransformDirection(hit.normal), -1);
        float sign = Mathf.Sign(LocalComponent(blockTransform, axis, hit.normal));

        if (axis == verticalAxis)
        {
            // Top/bottom face: use the horizontal axis whose side is closest to the hit point.
            axis = ClosestSideAxis(blockTransform, worldCenter, size, hit.point, verticalAxis, out float offset,
                out float ratio);
            sign = ratio > sideHitThreshold
                ? Mathf.Sign(offset)
                : Mathf.Sign(LocalComponent(blockTransform, axis, tip.position - worldCenter));
        }

        Vector3 localAxis = Vector3.zero;
        localAxis[axis] = 1f;

        target = body;
        localCenter = bounds.center;
        localAnchor = blockTransform.InverseTransformPoint(hit.point);
        localPullAxis = localAxis * sign;
        tipStart = tip.position;
        ropeStartDirection = (tip.position - hit.point).normalized;
        startCenter = worldCenter;
        startDirection = blockTransform.TransformDirection(localPullAxis).normalized;
        sizeAlongPull = size[axis];
        body.WakeUp();
        return true;
    }

    public void Detach()
    {
        target = null;
    }

    // ---------------------------------------------------------------- pulling

    private void FixedUpdate()
    {
        if (target == null)
            return;
        if (!target.gameObject.activeInHierarchy)
        {
            Detach();
            return;
        }

        GetPullState(out float handPull, out float travel, out float handLead);

        // Hand pulled much further than the block could follow (stuck block): the rope snaps.
        if (handLead > breakStretch)
        {
            Detach();
            return;
        }

        if (releaseWhenOut && travel >= sizeAlongPull)
        {
            Detach();
            return;
        }

        // Where the block should be: the hand pull, scaled up so handPullToRemove = one block length.
        float error = handPull * PullGain - travel;
        if (error <= 0f)
            return;

        Vector3 pullDirection = target.transform.TransformDirection(localPullAxis).normalized;
        float outwardSpeed = Vector3.Dot(target.linearVelocity, pullDirection);
        float acceleration = stiffness * error - damping * outwardSpeed;
        // Never negative: the rope can only pull, never push the block back in.
        acceleration = Mathf.Clamp(acceleration, 0f, maxAcceleration);
        if (acceleration <= 0f)
            return;

        // Applied at the center of mass and only along the axis: no torque, no sideways or vertical push.
        target.AddForce(pullDirection * acceleration, ForceMode.Acceleration);
    }

    private float PullGain => sizeAlongPull / handPullToRemove;

    /// <summary>
    /// handPull: how far the hand moved back along the rope since attaching (m).
    /// travel: how far the block moved out along its pull axis (m).
    /// handLead: how much further the hand is pulled than the block has followed, in hand meters.
    /// </summary>
    private void GetPullState(out float handPull, out float travel, out float handLead)
    {
        handPull = Vector3.Dot(tip.position - tipStart, ropeStartDirection);
        Vector3 center = target.transform.TransformPoint(localCenter);
        travel = Vector3.Dot(center - startCenter, startDirection);
        handLead = handPull - travel / PullGain;
    }

    // ---------------------------------------------------------------- line visual

    // Same order as the cannon preview: right after XRI moved the held item to the rendered hand pose.
    [BeforeRenderOrder(XRInteractionUpdateOrder.k_BeforeRenderOrder + 1)]
    private void UpdateLine()
    {
        if (line == null)
            return;

        if (target != null && target.gameObject.activeInHierarchy)
        {
            DrawRope();
            return;
        }

        if (showAimLine && interactable.isSelected)
        {
            bool valid = Aim(out RaycastHit hit);
            Vector3 end = valid ? hit.point : tip.position + tip.forward * maxRange;
            DrawStraight(tip.position, end, valid ? aimValidColor : aimColor, lineWidth * 0.5f);
            return;
        }

        line.enabled = false;
    }

    private void DrawRope()
    {
        Vector3 start = tip.position;
        Vector3 end = target.transform.TransformPoint(localAnchor);
        float length = Vector3.Distance(start, end);
        GetPullState(out _, out _, out float handLead);

        // Loose rope hangs down in a curve; pulled, it gets straight, then thinner and redder towards snapping.
        float taut = Mathf.Clamp01(handLead / tautDistance);
        float sag = slackSag * length * (1f - taut);
        float tension = Mathf.Clamp01(handLead / breakStretch);

        line.enabled = true;
        line.widthMultiplier = lineWidth * Mathf.Lerp(1f, 0.45f, tension);
        SetColor(Color.Lerp(slackColor, tenseColor, tension));

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

    private void DrawStraight(Vector3 start, Vector3 end, Color color, float width)
    {
        line.enabled = true;
        line.widthMultiplier = width;
        SetColor(color);
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void SetColor(Color color)
    {
        if (lineInstance != null)
            lineInstance.color = color;
    }

    private void CreateLine()
    {
        var lineObject = new GameObject("LassoLine");
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
                Debug.LogError("JengaLasso: URP Unlit shader not found. Assign a Line Material.", this);
                return;
            }
            lineInstance = new Material(shader);
        }

        lineInstance.name = "LassoLine (Instance)";
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

    /// <summary>Index (0-2) of the local axis most parallel to <paramref name="localDirection"/>, skipping one.</summary>
    private static int MostAlignedAxis(Vector3 localDirection, int skipAxis)
    {
        int best = -1;
        float bestValue = -1f;
        for (int i = 0; i < 3; i++)
        {
            float value = Mathf.Abs(localDirection[i]);
            if (i == skipAxis || value <= bestValue)
                continue;
            best = i;
            bestValue = value;
        }
        return best;
    }

    /// <summary>Component of a world direction along one of the block's local axes.</summary>
    private static float LocalComponent(Transform block, int axis, Vector3 worldDirection)
    {
        return block.InverseTransformDirection(worldDirection)[axis];
    }

    /// <summary>
    /// The horizontal axis whose side face is nearest to <paramref name="point"/>.
    /// <paramref name="offset"/> is the signed distance from the center along it, <paramref name="ratio"/> that
    /// distance relative to half the size (0 = middle, 1 = on the face).
    /// </summary>
    private static int ClosestSideAxis(Transform block, Vector3 center, Vector3 size, Vector3 point, int skipAxis,
        out float offset, out float ratio)
    {
        int best = -1;
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

    private void OnDrawGizmosSelected()
    {
        Transform origin = tip != null ? tip : transform;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin.position, origin.position + origin.forward * maxRange);
    }
}
