using System.Collections;
using UnityEngine;

/// <summary>
/// Startet automatisch, sobald das Objekt erscheint (gespawnt oder aktiviert wird):
/// aktiviert zuerst das erste Objekt und nach einer einstellbaren Verzögerung das zweite.
/// StartTutorial() ist zusätzlich public, falls man es manuell neu starten will.
/// </summary>
public class TutorialSequence : MonoBehaviour
{
    [SerializeField] private GameObject firstObject;
    [SerializeField] private GameObject secondObject;
    [SerializeField, Min(0f)] private float delay = 2f;
    [Tooltip("Sekunden nach dem Aktivieren des zweiten Objekts, bis sich das Tutorial selbst zerstört.")]
    [SerializeField, Min(0f)] private float destroyDelay = 10f;

    private Coroutine running;

    private void OnEnable()
    {
        StartTutorial();
    }

    public void StartTutorial()
    {
        if (running != null)
            StopCoroutine(running);

        running = StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        if (secondObject != null)
            secondObject.SetActive(false);

        if (firstObject != null)
            firstObject.SetActive(true);

        yield return new WaitForSeconds(delay);

        if (secondObject != null)
            secondObject.SetActive(true);

        yield return new WaitForSeconds(destroyDelay);

        running = null;
        Destroy(gameObject);
    }
}
