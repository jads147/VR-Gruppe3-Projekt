using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Blendet den Ray (Linie + Reticle) nur ein, wenn der Ray in die NÄHE von UI zeigt.
/// Der Ray wird mit der Ebene jedes World-Space-Canvas geschnitten; liegt der Schnittpunkt
/// höchstens "Proximity" Meter neben einem UI-Element, wird der Ray sichtbar.
/// Auf dasselbe GameObject wie den XR Ray Interactor legen.
/// </summary>
[RequireComponent(typeof(XRRayInteractor))]
public class RayVisibilityByLayer : MonoBehaviour
{
    public enum TargetMode
    {
        InteractiveElements, // Buttons, Slider, Toggles, ... (alle Selectables)
        WholeCanvas          // die gesamte Canvas-Fläche
    }

    [Tooltip("Layer der Canvases, bei denen der Ray sichtbar werden soll (z.B. UI).")]
    [SerializeField] LayerMask visibleLayers = 1 << 5; // 5 = UI

    [Tooltip("Worauf sich die Nähe bezieht.")]
    [SerializeField] TargetMode targetMode = TargetMode.InteractiveElements;

    [Tooltip("Wie weit (in Metern) der Ray neben dem UI-Element vorbeizeigen darf und trotzdem sichtbar ist.")]
    [SerializeField] float proximity = 0.3f;

    [Tooltip("Maximale Länge des Rays in Metern. 0 = Max Raycast Distance des Ray Interactors verwenden.")]
    [SerializeField] float maxRayLength = 0f;

    [Tooltip("Wie oft (Sekunden) nach neuen Canvases/Buttons gesucht wird.")]
    [SerializeField] float refreshInterval = 1f;

    XRRayInteractor m_Ray;
    XRInteractorLineVisual m_LineVisual;
    LineRenderer m_LineRenderer;

    readonly List<RectTransform> m_Targets = new List<RectTransform>();
    float m_NextRefresh;

    void Awake()
    {
        m_Ray = GetComponent<XRRayInteractor>();
        m_LineVisual = GetComponent<XRInteractorLineVisual>();
        m_LineRenderer = GetComponent<LineRenderer>();
    }

    void Update()
    {
        if (Time.unscaledTime >= m_NextRefresh)
        {
            RefreshTargets();
            m_NextRefresh = Time.unscaledTime + refreshInterval;
        }

        SetVisible(IsRayNearTarget());
    }

    void SetVisible(bool show)
    {
        if (m_LineVisual != null)
        {
            if (m_LineVisual.enabled != show)
                m_LineVisual.enabled = show; // OnDisable schaltet auch LineRenderer + Reticle aus
        }
        else if (m_LineRenderer != null)
        {
            m_LineRenderer.enabled = show;
        }
    }

    void RefreshTargets()
    {
        m_Targets.Clear();
        var canvases = FindObjectsByType<Canvas>();
        foreach (var canvas in canvases)
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.WorldSpace)
                continue;
            if ((visibleLayers.value & (1 << canvas.gameObject.layer)) == 0)
                continue;

            if (targetMode == TargetMode.WholeCanvas)
            {
                m_Targets.Add((RectTransform)canvas.transform);
            }
            else
            {
                foreach (var selectable in canvas.GetComponentsInChildren<Selectable>(true))
                    m_Targets.Add((RectTransform)selectable.transform);
            }
        }
    }

    bool IsRayNearTarget()
    {
        Transform originT = m_Ray.rayOriginTransform != null ? m_Ray.rayOriginTransform : transform;
        var ray = new Ray(originT.position, originT.forward);
        float length = maxRayLength > 0f ? maxRayLength : m_Ray.maxRaycastDistance;

        foreach (var rt in m_Targets)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy)
                continue;

            // Ray mit der Ebene des UI-Elements schneiden
            var plane = new Plane(rt.forward, rt.position);
            if (!plane.Raycast(ray, out float enter) || enter > length)
                continue;

            Vector3 hitPoint = ray.GetPoint(enter);

            // Nächster Punkt auf dem Rechteck des Elements
            Vector3 local = rt.InverseTransformPoint(hitPoint);
            Rect r = rt.rect;
            var clamped = new Vector3(Mathf.Clamp(local.x, r.xMin, r.xMax),
                                      Mathf.Clamp(local.y, r.yMin, r.yMax), 0f);
            Vector3 closest = rt.TransformPoint(clamped);

            if (Vector3.Distance(hitPoint, closest) <= proximity)
                return true;
        }

        return false;
    }
}
