using UnityEngine;

/// <summary>
/// Setzt die laufende Runde zurück, OHNE die Szene neu zu laden (Musik läuft weiter):
///  - offene Namenseingabe schließen (ohne Eintrag),
///  - gespielte Level-Kopie, gespawnte Items und ein laufendes Tutorial löschen,
///  - Start/Tutorial/Level-Auswahl wieder freigeben,
///  - Score und Timer auf 0.
///
/// Wird von "Reload Game" (<see cref="SceneLoader.ReloadGame"/>) und nach dem Eintragen des Scores
/// (<see cref="RoundEnder"/>) aufgerufen.
/// </summary>
public static class RoundReset
{
    /// <param name="gameStarter">Optional. Leer = in der Szene suchen.</param>
    public static void ResetRound(GameStarter gameStarter = null)
    {
        var roundEnder = Object.FindAnyObjectByType<RoundEnder>(FindObjectsInactive.Include);
        if (roundEnder != null)
            roundEnder.Abort();

        if (gameStarter == null)
            gameStarter = Object.FindAnyObjectByType<GameStarter>(FindObjectsInactive.Include);
        if (gameStarter != null)
            gameStarter.ResetForNextRound();

        if (LevelManager.Current != null)
            LevelManager.Current.EndLevel();

        ItemSpawner.DestroyAllSpawned();

        foreach (var tutorial in Object.FindObjectsByType<TutorialSpawner>(FindObjectsInactive.Include))
            tutorial.Clear();

        // Zuletzt: beendet die Runde -> RoundVisibility blendet die Menü-Buttons wieder ein.
        if (GameManager.Instance != null)
            GameManager.Instance.ResetGame();
    }
}
