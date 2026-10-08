using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

public class SnapTurn : MonoBehaviour
{
    [SerializeField] private float turnAngle = 45f;
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private InputActionProperty turnAction;

    private void OnEnable()
    {
        turnAction.action.Enable();
        turnAction.action.performed += Turn;
    }

    private void OnDisable()
    {
        turnAction.action.performed -= Turn;
    }

    private void Turn(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            float direction = context.ReadValue<Vector2>().x > 0 ? 1 : -1;

            Vector3 prePosition = xrOrigin.Camera.transform.position;
            xrOrigin.transform.Rotate(0, direction * turnAngle, 0);
            Vector3 postPosition = xrOrigin.Camera.transform.position;
            Vector3 delta = postPosition - prePosition;
            xrOrigin.transform.position -= delta;
        }
    }
}