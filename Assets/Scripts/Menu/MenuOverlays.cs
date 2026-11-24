using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Fenster, das über dem normalen Menü liegt (z.B. Bestenliste, Namenseingabe).
/// Der <see cref="MenuButton"/> schließt offene Overlays zuerst, bevor er das Menü togglet.
/// </summary>
public interface IMenuOverlay
{
    bool IsOverlayOpen { get; }
    void CloseOverlay();
}

/// <summary>Register aller <see cref="IMenuOverlay"/>s in der Szene.</summary>
public static class MenuOverlays
{
    static readonly List<IMenuOverlay> s_Overlays = new();

    /// <summary>
    /// Ein Overlay wurde geöffnet. Parameter = das sichtbare Objekt des Overlays.
    /// Der <see cref="MenuButton"/> blendet daraufhin sein Menü aus.
    /// </summary>
    public static event System.Action<GameObject> Opened;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_Overlays.Clear();
        Opened = null;
    }

    /// <summary>Von Overlays aufrufen, sobald sie sichtbar werden.</summary>
    public static void NotifyOpened(GameObject shownObject) => Opened?.Invoke(shownObject);

    public static void Register(IMenuOverlay overlay)
    {
        if (overlay != null && !s_Overlays.Contains(overlay))
            s_Overlays.Add(overlay);
    }

    public static void Unregister(IMenuOverlay overlay) => s_Overlays.Remove(overlay);

    /// <summary>Ist gerade irgendein Overlay offen?</summary>
    public static bool AnyOpen
    {
        get
        {
            foreach (var overlay in s_Overlays)
            {
                if (IsAlive(overlay) && overlay.IsOverlayOpen)
                    return true;
            }
            return false;
        }
    }

    /// <summary>Schließt alle offenen Overlays. Gibt true zurück, wenn mindestens eins offen war.</summary>
    public static bool CloseAll()
    {
        bool closedAny = false;
        // Kopie, weil Close-Events weitere Overlays öffnen/schließen könnten.
        foreach (var overlay in s_Overlays.ToArray())
        {
            if (!IsAlive(overlay))
            {
                s_Overlays.Remove(overlay);
                continue;
            }

            if (overlay.IsOverlayOpen)
            {
                overlay.CloseOverlay();
                closedAny = true;
            }
        }
        return closedAny;
    }

    // Zerstörte Unity-Objekte sind != null für das Interface, aber == null als UnityEngine.Object.
    static bool IsAlive(IMenuOverlay overlay) => overlay is not Object unityObject || unityObject != null;
}
