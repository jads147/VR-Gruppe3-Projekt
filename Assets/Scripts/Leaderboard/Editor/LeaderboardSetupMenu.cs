using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Menüeinträge zum Anlegen und Verwalten der Bestenliste:
///  - GameObject > Bestenliste > ...   legt fertig verdrahtete UI-Objekte in der Szene an
///  - Tools > Bestenliste > ...        Datei öffnen / Einträge löschen
/// </summary>
static class LeaderboardSetupMenu
{
    const string GameObjectMenu = "GameObject/Bestenliste/";
    const string ToolsMenu = "Tools/Bestenliste/";

    // Gleicher Maßstab wie der MenuCanvas (800x600 bei 0.005).
    const float CanvasScale = 0.005f;

    static readonly Color PanelColor = new(0.08f, 0.09f, 0.12f, 0.92f);
    static readonly Color FieldColor = new(0.18f, 0.2f, 0.25f, 1f);
    static readonly Color ConfirmColor = new(0.55f, 0.9f, 0.55f, 1f);
    static readonly Color CancelColor = new(0.95f, 0.6f, 0.6f, 1f);

    // ---------------------------------------------------------------- Tools

    [MenuItem(ToolsMenu + "Speicherort öffnen")]
    static void RevealFile()
    {
        string path = Leaderboard.FilePath;
        EditorUtility.RevealInFinder(File.Exists(path) ? path : Path.GetDirectoryName(path));
    }

    [MenuItem(ToolsMenu + "Alle Einträge löschen (alle Level)")]
    static void ClearEntries()
    {
        if (EditorUtility.DisplayDialog("Bestenliste löschen",
                $"Alle {Leaderboard.TotalCount} Einträge aller Level endgültig löschen?\n\n{Leaderboard.FilePath}",
                "Löschen", "Abbrechen"))
        {
            Leaderboard.ClearAll();
        }
    }

    [MenuItem(ToolsMenu + "Testeintrag hinzufügen (gewähltes Level)")]
    static void AddTestEntry()
    {
        // Im Play Mode das gewählte Level, sonst das erste Level des LevelManagers der offenen Szene.
        string levelId = LevelManager.CurrentLevelId;
        if (!Application.isPlaying)
        {
            var manager = Object.FindAnyObjectByType<LevelManager>(FindObjectsInactive.Include);
            if (manager != null && manager.Levels.Count > 0)
                levelId = manager.Levels[0].Id;
        }

        string[] names = { "Anna", "Ben", "Clara", "David", "Emma", "Finn", "Greta", "Hannes" };
        int rank = Leaderboard.Submit(levelId, names[Random.Range(0, names.Length)],
            Random.Range(1, 10) * 10, Random.Range(20f, 180f));
        Debug.Log($"[Leaderboard] Testeintrag in Level \"{levelId}\" auf Platz {rank}.");
    }

    // ---------------------------------------------------------------- Anzeige

    [MenuItem(GameObjectMenu + "Anzeige (World Space)", false, 10)]
    static void CreateDisplay(MenuCommand command)
    {
        var canvas = CreateWorldCanvas("Bestenliste", new Vector2(600, 800), false, command.context as GameObject);

        var title = CreateText("Titel", canvas.transform, "Leaderboard", 64, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -130), new Vector2(-30, -30));

        var list = CreateText("Liste", canvas.transform, "1.<pos=12%>Name<pos=58%>0:00.0<pos=82%>0", 38, TextAlignmentOptions.TopLeft);
        list.lineSpacing = 20;
        Place(list.rectTransform, Vector2.zero, Vector2.one, new Vector2(50, 40), new Vector2(-40, -160));

        var display = canvas.AddComponent<LeaderboardDisplay>();
        SetRef(display, "listText", list);
        SetRef(display, "titleText", title);

        Finish(canvas, "Bestenliste anlegen");
    }

    // ---------------------------------------------------------------- Rundenende + Tastatur

    [MenuItem(GameObjectMenu + "Rundenende mit Tastatur", false, 11)]
    static void CreateRoundEnder(MenuCommand command)
    {
        var root = new GameObject("Rundenende");
        if (command.context is GameObject parent)
            root.transform.SetParent(parent.transform, false);
        else
            PlaceInFrontOfSceneView(root.transform);

        var roundEnder = root.AddComponent<RoundEnder>();

        var canvas = CreateWorldCanvas("Namenseingabe", new Vector2(1000, 720), true, root);

        // Kopfzeile
        var title = CreateText("Titel", canvas.transform, "Round over!", 58, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -95), new Vector2(-20, -20));

        var scoreInfo = CreateText("Score Info", canvas.transform, "Score: 0  ·  Rank 1", 36, TextAlignmentOptions.Center);
        Place(scoreInfo.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -150), new Vector2(-20, -95));

        // Namensfeld
        var field = CreateImage("Namensfeld", canvas.transform, FieldColor);
        Place(field.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-330, -250), new Vector2(330, -165));
        var nameText = CreateText("Name", field.transform, "Enter name_", 50, TextAlignmentOptions.Center);
        Place(nameText.rectTransform, Vector2.zero, Vector2.one, new Vector2(15, 0), new Vector2(-15, 0));

        // Tastenbereich
        var keys = new GameObject("Tasten", typeof(RectTransform), typeof(VerticalLayoutGroup));
        keys.transform.SetParent(canvas.transform, false);
        Place((RectTransform)keys.transform, Vector2.zero, Vector2.one, new Vector2(20, 120), new Vector2(-20, -270));
        var vertical = keys.GetComponent<VerticalLayoutGroup>();
        vertical.spacing = 10;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        var keyTemplate = CreateButton("Tasten-Vorlage", keys.transform, "A", 38, Color.white);
        ((RectTransform)keyTemplate.transform).sizeDelta = new Vector2(72, 70);
        keyTemplate.gameObject.SetActive(false);

        // Untere Reihe
        var bottom = new GameObject("Aktionen", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        bottom.transform.SetParent(canvas.transform, false);
        Place((RectTransform)bottom.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 25), new Vector2(-40, 100));
        var horizontal = bottom.GetComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 16;
        horizontal.childControlWidth = horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = true;

        var cancel = CreateButton("Abbrechen", bottom.transform, "Cancel", 32, CancelColor);
        var backspace = CreateButton("Löschen", bottom.transform, "Delete", 32, Color.white);
        var space = CreateButton("Leerzeichen", bottom.transform, "Space", 32, Color.white);
        var confirm = CreateButton("OK", bottom.transform, "Submit", 32, ConfirmColor);
        confirm.GetComponentInChildren<TMP_Text>().fontStyle = FontStyles.Bold;

        // Verdrahten
        var keyboard = canvas.AddComponent<NameKeyboard>();
        SetRef(keyboard, "keyTemplate", keyTemplate);
        SetRef(keyboard, "keyContainer", keys.transform);
        SetRef(keyboard, "nameText", nameText);
        SetRef(keyboard, "backspaceButton", backspace);
        SetRef(keyboard, "spaceButton", space);
        SetRef(keyboard, "confirmButton", confirm);
        SetRef(keyboard, "cancelButton", cancel);

        SetRef(roundEnder, "namePanel", canvas);
        SetRef(roundEnder, "keyboard", keyboard);
        SetRef(roundEnder, "scoreInfoText", scoreInfo);
        SetRef(roundEnder, "gameStarter", Object.FindAnyObjectByType<GameStarter>(FindObjectsInactive.Include));

        Finish(root, "Rundenende anlegen");
    }

    // ---------------------------------------------------------------- "Runde beenden"-Button

    [MenuItem(GameObjectMenu + "\"Runde beenden\"-Button (im ausgewählten Canvas)", false, 12)]
    static void CreateEndRoundButton()
    {
        var parent = Selection.activeGameObject.transform;
        // Gleicher Stil wie die vorhandenen Menü-Buttons (160x30, Schriftgröße 24).
        var button = CreateButton("EndRound", parent, "Finish Round", 24, Color.white);
        ((RectTransform)button.transform).sizeDelta = new Vector2(160, 30);
        // Sucht den RoundEnder zur Laufzeit -> funktioniert auch, wenn der Button in einem Prefab landet.
        button.gameObject.AddComponent<EndRoundButton>();

        Finish(button.gameObject, "\"Runde beenden\"-Button anlegen");
    }

    [MenuItem(GameObjectMenu + "\"Runde beenden\"-Button (im ausgewählten Canvas)", true)]
    static bool ValidateCreateEndRoundButton() =>
        Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<Canvas>(true) != null;

    // ---------------------------------------------------------------- Level-Auswahl

    [MenuItem(GameObjectMenu + "Level-Auswahl (im ausgewählten Canvas)", false, 13)]
    static void CreateLevelSelector()
    {
        var parent = Selection.activeGameObject.transform;

        // Gleicher Stil wie die vorhandenen Menü-Buttons (Höhe 30, Schriftgröße 24).
        var row = new GameObject("LevelSelect", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        ((RectTransform)row.transform).sizeDelta = new Vector2(240, 30);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var previous = CreateButton("Previous", row.transform, "<", 24, Color.white);
        previous.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;

        var label = CreateText("Level", row.transform, "Level 1", 24, TextAlignmentOptions.Center);
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        var next = CreateButton("Next", row.transform, ">", 24, Color.white);
        next.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;

        var selector = row.AddComponent<LevelSelector>();
        SetRef(selector, "label", label);
        SetRef(selector, "previousButton", previous);
        SetRef(selector, "nextButton", next);

        Finish(row, "Level-Auswahl anlegen");
    }

    [MenuItem(GameObjectMenu + "Level-Auswahl (im ausgewählten Canvas)", true)]
    static bool ValidateCreateLevelSelector() => ValidateCreateEndRoundButton();

    // ---------------------------------------------------------------- Timer-Anzeige

    [MenuItem(GameObjectMenu + "Timer-Anzeige (Kopie des ausgewählten Textes)", false, 14)]
    static void CreateTimerText()
    {
        var source = Selection.activeGameObject.GetComponent<TMP_Text>();
        var copy = Object.Instantiate(source.gameObject, source.transform.parent);
        copy.name = "TimerText";

        // Direkt unter den Original-Text setzen.
        var rect = (RectTransform)copy.transform;
        rect.anchoredPosition -= new Vector2(0, ((RectTransform)source.transform).rect.height);

        // Score-Logik nicht mitkopieren.
        foreach (var scoreHud in copy.GetComponents<ScoreHUD>())
            Object.DestroyImmediate(scoreHud);

        var text = copy.GetComponent<TMP_Text>();
        text.text = "Time: 0:00.0";
        var timer = copy.AddComponent<TimerHUD>();
        SetRef(timer, "timerText", text);

        Finish(copy, "Timer-Anzeige anlegen");
    }

    [MenuItem(GameObjectMenu + "Timer-Anzeige (Kopie des ausgewählten Textes)", true)]
    static bool ValidateCreateTimerText() =>
        Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<TMP_Text>() != null;

    // ---------------------------------------------------------------- Helfer

    static GameObject CreateWorldCanvas(string name, Vector2 size, bool interactive, GameObject parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        if (parent != null)
            go.transform.SetParent(parent.transform, false);
        else
            PlaceInFrontOfSceneView(go.transform);

        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one * CanvasScale;

        if (interactive)
        {
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        var background = CreateImage("Hintergrund", go.transform, PanelColor);
        Place(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        background.raycastTarget = interactive;
        return go;
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Button CreateButton(string name, Transform parent, string label, float fontSize, Color color)
    {
        var go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        });
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;

        var text = go.GetComponentInChildren<TMP_Text>();
        text.text = label;
        text.fontSize = fontSize;
        return go.GetComponent<Button>();
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void SetRef(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void PlaceInFrontOfSceneView(Transform t)
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null)
            return;
        t.position = view.pivot;
        // Canvas aufrecht zur Kamera drehen (nur um die Hochachse).
        Vector3 forward = Vector3.ProjectOnPlane(view.camera.transform.forward, Vector3.up);
        if (forward.sqrMagnitude > 1e-4f)
            t.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    static void Finish(GameObject go, string undoName)
    {
        Undo.RegisterCreatedObjectUndo(go, undoName);
        Selection.activeGameObject = go;
    }
}
