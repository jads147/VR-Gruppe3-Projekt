using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ruft beim Klick <see cref="RoundEnder.EndRound"/> auf. Der RoundEnder wird erst zur Laufzeit in der
/// Szene gesucht – dadurch funktioniert der Button auch in Prefabs (z.B. MenuCanvas), die keine
/// Verweise auf Szenen-Objekte speichern können.
/// </summary>
[RequireComponent(typeof(Button))]
public class EndRoundButton : MonoBehaviour
{
    [Tooltip("Optional. Leer = beim Klick in der Szene suchen.")]
    [SerializeField] RoundEnder roundEnder;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        Debug.Log("[EndRoundButton] Klick", this);

        if (roundEnder == null)
            roundEnder = FindAnyObjectByType<RoundEnder>(FindObjectsInactive.Include);

        if (roundEnder == null)
        {
            Debug.LogWarning("[EndRoundButton] Kein RoundEnder in der Szene gefunden.", this);
            return;
        }

        roundEnder.EndRound();
    }
}
