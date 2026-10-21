using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
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
        Vector3 offset = transform.position - target.position;
        Quaternion orbit = Quaternion.AngleAxis(angle, Vector3.up);

        transform.position = target.position + orbit * offset;
        if (rotateWithOrbit)
            transform.rotation = orbit * transform.rotation;
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
