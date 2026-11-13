using UnityEngine;

[CreateAssetMenu(fileName = "SceneLoader", menuName = "Scriptable Objects/SceneLoader")]
public class SceneLoader : ScriptableObject
{
    public void LoadScene(string sceneName)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        GameManager.Instance.ResetGame();
    }
}
