using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

/// <summary>Moves this GameObject around a target using the horizontal axis of a controller stick.</summary>
public class OrbitAroundTarget : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private InputActionProperty stickAction;
    [SerializeField] private float orbitSpeed = 90f;
    [SerializeField, Range(0f, 1f)] private float deadzone = 0.15f;
    [SerializeField] private bool rotateWithOrbit = true;
    [Tooltip("Orbited and rotated together with this GameObject (e.g. the level root), so only the target appears to turn.")]
    [SerializeField] private Transform[] orbitTogether;
    [Tooltip("Falls back to the XR device API (left controller primary 2D axis) when the action delivers no input.")]
    [SerializeField] private bool useXRDeviceFallback = true;
    [Tooltip("While orbiting, how often (seconds) to look for rigidbodies that were spawned or destroyed.")]
    [SerializeField, Min(0.05f)] private float bodyRefreshInterval = 0.5f;

    private struct CachedBody
    {
        public Rigidbody Body;
        public XRGrabInteractable Grab;
    }

    private XRInputDevice leftController;

    // Rigidbodies below this transform (index 0) and below each orbitTogether entry (index 1..n).
    private readonly List<List<CachedBody>> cachedBodies = new();
    private readonly List<Rigidbody> bodyBuffer = new();
    private int lastOrbitFrame = -10;
    private float nextBodyRefreshTime;

    private void OnEnable()
    {
        if (stickAction.action != null)
            stickAction.action.Enable();
    }

    private void OnDisable()
    {
        if (stickAction.action != null)
            stickAction.action.Disable();
    }

    private void Update()
    {
        if (target == null)
            return;

        float horizontal = ReadHorizontal();
        if (Mathf.Abs(horizontal) <= deadzone)
            return;

        ApplyInput(horizontal);
    }

    /// <summary>
    /// Orbits for one frame as if the stick were pushed to <paramref name="horizontal"/> (-1 = left, 1 = right).
    /// Call every frame for continuous orbiting (e.g. from an ArrowButton).
    /// </summary>
    public void ApplyInput(float horizontal)
    {
        if (target == null)
            return;

        // A new orbit gesture (nothing orbited last frame) or the refresh interval elapsed:
        // rebuild the rigidbody cache instead of searching the hierarchy every frame.
        int frame = Time.frameCount;
        if (frame - lastOrbitFrame > 1 || Time.time >= nextBodyRefreshTime)
            RefreshBodies();
        lastOrbitFrame = frame;

        float angle = Mathf.Clamp(horizontal, -1f, 1f) * orbitSpeed * Time.deltaTime;
        Quaternion orbit = Quaternion.AngleAxis(angle, Vector3.up);

        Orbit(transform, orbit, rotateWithOrbit);
        OrbitDynamicBodies(cachedBodies[0], orbit);
        if (orbitTogether == null)
            return;

        // Always rotate carried objects, otherwise the background shifts relative to the player.
        for (int i = 0; i < orbitTogether.Length; i++)
        {
            Transform carried = orbitTogether[i];
            if (carried == null)
                continue;

            Orbit(carried, orbit, true);
            OrbitDynamicBodies(cachedBodies[i + 1], orbit);
        }
    }

    private void RefreshBodies()
    {
        nextBodyRefreshTime = Time.time + bodyRefreshInterval;

        int rootCount = 1 + (orbitTogether?.Length ?? 0);
        while (cachedBodies.Count < rootCount)
            cachedBodies.Add(new List<CachedBody>());

        FillBodies(transform, cachedBodies[0]);
        for (int i = 1; i < rootCount; i++)
            FillBodies(orbitTogether[i - 1], cachedBodies[i]);
    }

    private void FillBodies(Transform root, List<CachedBody> result)
    {
        result.Clear();
        if (root == null)
            return;

        root.GetComponentsInChildren(bodyBuffer); // reuses the buffer, no garbage
        foreach (Rigidbody body in bodyBuffer)
        {
            body.TryGetComponent(out XRGrabInteractable grab);
            result.Add(new CachedBody { Body = body, Grab = grab });
        }
        bodyBuffer.Clear();
    }

    private void Orbit(Transform moved, Quaternion orbit, bool rotate)
    {
        Vector3 offset = moved.position - target.position;
        moved.position = target.position + orbit * offset;
        if (rotate)
            moved.rotation = orbit * moved.rotation;
    }

    // Non-kinematic rigidbodies ignore parent movement (physics writes their pose back),
    // so they have to be orbited through the physics state, including their velocity.
    private void OrbitDynamicBodies(List<CachedBody> bodies, Quaternion orbit)
    {
        foreach (CachedBody cached in bodies)
        {
            Rigidbody body = cached.Body;
            // Destroyed, disabled or moved out of the level since the last refresh.
            if (body == null || !body.gameObject.activeInHierarchy)
                continue;
            if (body.isKinematic || (cached.Grab != null && cached.Grab.isSelected))
                continue;

            Vector3 newPosition = target.position + orbit * (body.position - target.position);
            Quaternion newRotation = orbit * body.rotation;
            body.position = newPosition;
            body.rotation = newRotation;
            body.transform.SetPositionAndRotation(newPosition, newRotation);
            body.linearVelocity = orbit * body.linearVelocity;
            body.angularVelocity = orbit * body.angularVelocity;
        }
    }

    private float ReadHorizontal()
    {
        float value = 0f;
        InputAction action = stickAction.action;
        if (action != null && action.activeControl != null)
        {
            value = action.activeControl.valueType == typeof(Vector2)
                ? action.ReadValue<Vector2>().x
                : action.ReadValue<float>();
        }

        if (Mathf.Abs(value) > deadzone || !useXRDeviceFallback)
            return value;

        if (!leftController.isValid)
            leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        if (leftController.isValid && leftController.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 stick))
            return stick.x;

        return value;
    }
}
