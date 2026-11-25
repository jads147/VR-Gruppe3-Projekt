using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Sperrt alle <see cref="XRGrabInteractable"/>s in der Szene (Blöcke, Items, Kanonen), z.B. solange der
/// Rundenende-Screen offen ist. So kann man bei angehaltener Zeit nicht weiterbauen.
///
/// Lock() deaktiviert alle gerade aktiven Grab-Interactables (ein gehaltenes Objekt wird dabei losgelassen),
/// Unlock() schaltet genau diese wieder ein. Interactables, die schon vorher aus waren, bleiben aus.
/// </summary>
public static class GrabLock
{
    static readonly List<XRGrabInteractable> s_Disabled = new();

    public static bool IsLocked { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_Disabled.Clear();
        IsLocked = false;
    }

    public static void Lock()
    {
        if (IsLocked)
            return;
        IsLocked = true;

        foreach (var grab in Object.FindObjectsByType<XRGrabInteractable>())
        {
            if (!grab.enabled)
                continue;
            grab.enabled = false; // OnDisable beendet auch eine laufende Auswahl (Objekt fällt aus der Hand)
            s_Disabled.Add(grab);
        }
    }

    public static void Unlock()
    {
        if (!IsLocked)
            return;
        IsLocked = false;

        foreach (var grab in s_Disabled)
        {
            if (grab != null) // inzwischen zerstört (z.B. Level-Kopie nach dem Eintragen)
                grab.enabled = true;
        }
        s_Disabled.Clear();
    }
}
