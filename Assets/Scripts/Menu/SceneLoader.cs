using UnityEngine;

[CreateAssetMenu(fileName = "SceneLoader", menuName = "Scriptable Objects/SceneLoader")]
public class SceneLoader : ScriptableObject
{
    /// <summary>Lädt eine Szene komplett neu (unterbricht z.B. die Musik).</summary>
    public void LoadScene(string sceneName)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        GameManager.Instance.ResetGame();
    }

    /// <summary>
    /// "Reload Game" ohne Szenenwechsel: löscht Level-Kopie und gespawnte Objekte und setzt Score/Timer zurück.
    /// Siehe <see cref="RoundReset"/>.
    /// </summary>
    public void ReloadGame()
    {
        RoundReset.ResetRound();
    }
}
