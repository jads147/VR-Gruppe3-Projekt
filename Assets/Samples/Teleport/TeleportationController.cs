using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using static UnityEngine.Rendering.HableCurve;


[Serializable]
public struct RayCurveSettings
{
    [Range(1, 50)] public float maxRayDistance;
    [Range(30, 90)] public float maxRayAngle;
    [Range(0.1f, 20)] public float initialVelocity;
    [Range(10, 200)] public int segments;
    [Range(0.1f, 50)] public float maxHeightChange;
}

[RequireComponent(typeof(LineRenderer))]
public class TeleportationController : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Gradient validColor;
    [SerializeField] private Gradient invalidColor;
    [SerializeField] private InputActionReference teleportAction;
    [SerializeField] private RayCurveSettings rayCurveSettings;
    [SerializeField] private GameObject teleportReticlePrefab;
    [SerializeField] private XROrigin xrOrigin;

    private bool toolActive = false;
    private Vector3 acceleration = new(0, -9.81f, 0);
    private Vector3[] arcPoints;
    private int hitIndex = -1;
    private bool canTeleport = false;
    private RaycastHit? hitInfo;
    private GameObject reticleInstance;

    private void Awake()
    {
        
        teleportAction.action.canceled += (t) =>
        {
            TryTeleport();
            toolActive = false;
        };

        arcPoints = new Vector3[rayCurveSettings.segments + 1];
        reticleInstance = Instantiate(teleportReticlePrefab);
        reticleInstance.SetActive(false);
    }

    private void OnEnable()
    {
        teleportAction.action.performed += ActivateTeleport;
        teleportAction.action.canceled += CancelTeleport;
    }

    private void OnDisable()
    {
        teleportAction.action.performed -= ActivateTeleport;
        teleportAction.action.canceled -= CancelTeleport;
    }

    private void ActivateTeleport(InputAction.CallbackContext context)
    {
        toolActive = true;
        lineRenderer.enabled = true;
        reticleInstance.SetActive(true);
    }

    private void CancelTeleport(InputAction.CallbackContext context)
    {
        TryTeleport();
        toolActive = false;
        lineRenderer.enabled = false;
        reticleInstance.SetActive(false);
    }

    private void Update()
    {
       
        if (!toolActive) return;

        if(Vector3.Angle(transform.forward, -Vector3.up) > rayCurveSettings.maxRayAngle + 90)
        {
            canTeleport = false;

            lineRenderer.colorGradient = invalidColor;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, transform.position + transform.forward * 0.075f);
            return;
        }

        CalculateArcPoints();
        hitInfo = GetHit();

        lineRenderer.positionCount = arcPoints.Length;
        if (hitInfo == null)
        {            
            lineRenderer.SetPositions(arcPoints);
        }
        else
        {
            lineRenderer.SetPositions(arcPoints);
            lineRenderer.SetPosition(hitIndex + 1, hitInfo.Value.point);
            lineRenderer.positionCount = hitIndex + 2;
            reticleInstance.transform.position = hitInfo.Value.point;
        }

        lineRenderer.colorGradient = canTeleport ? validColor : invalidColor;
        reticleInstance.SetActive(canTeleport);
    }

    private RaycastHit? GetHit()
    {
        for (int i = 0; i < rayCurveSettings.segments; i++)
        {
            Physics.Raycast(arcPoints[i], arcPoints[i + 1] - arcPoints[i], out RaycastHit hitInfo, Vector3.Distance(arcPoints[i], arcPoints[i + 1]));

            if(hitInfo.collider != null)
            {
                hitIndex = i;
                canTeleport = hitInfo.collider.GetComponent<TeleportTarget>();
                return hitInfo;
            }
        }

        hitIndex = -1;
        canTeleport = false;
        return null;
    }

    private void TryTeleport()
    {
        if(!toolActive || !canTeleport) return;

        xrOrigin.transform.position = ((RaycastHit)hitInfo).point + new Vector3(xrOrigin.transform.position.x - xrOrigin.Camera.transform.position.x, 0, xrOrigin.transform.position.z - xrOrigin.Camera.transform.position.z);
    }

    public void CalculateArcPoints()
    {
        float timeStep = GetStepSize();

        Vector3 startPosition = transform.position;

        for (int i = 0; i <= rayCurveSettings.segments; i++)
        {
            float t = i * timeStep;
            Vector3 point = startPosition + rayCurveSettings.initialVelocity * transform.forward * t + 0.5f * acceleration * t * t;
            arcPoints[i] = point;
        }        
    }

    public float GetStepSize()
    {
        var p = rayCurveSettings.initialVelocity * transform.forward.y / (0.5f * acceleration.y);
        var q = rayCurveSettings.maxHeightChange / (0.5f * acceleration.y);
        var flightTimeUntilMaxHeightChange = -p / 2 + Mathf.Sqrt(p * p / 4 - q);

        return Mathf.Min(flightTimeUntilMaxHeightChange / rayCurveSettings.segments, rayCurveSettings.maxRayDistance / (rayCurveSettings.segments * rayCurveSettings.initialVelocity));
    }
}
