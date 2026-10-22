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

    private XRInputDevice leftController;

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

        float angle = horizontal * orbitSpeed * Time.deltaTime;
        Quaternion orbit = Quaternion.AngleAxis(angle, Vector3.up);

        Orbit(transform, orbit, rotateWithOrbit);
        OrbitDynamicBodies(transform, orbit);
        if (orbitTogether == null)
            return;

        // Always rotate carried objects, otherwise the background shifts relative to the player.
        foreach (Transform carried in orbitTogether)
        {
            if (carried == null)
                continue;

            Orbit(carried, orbit, true);
            OrbitDynamicBodies(carried, orbit);
        }
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
    private void OrbitDynamicBodies(Transform root, Quaternion orbit)
    {
        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>())
        {
            if (body.isKinematic || IsHeld(body))
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

    // A held object follows the hand, not the level.
    private static bool IsHeld(Rigidbody body)
    {
        return body.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected;
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
