using UnityEditor;
using UnityEngine;

// AI GENERATED SCRIPT
[CustomEditor(typeof(MeshPrefabScatter))]
public class MeshPrefabScatterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DrawPrefabDropArea();
        EditorGUILayout.Space();

        MeshPrefabScatter scatter = (MeshPrefabScatter)target;

        if (GUILayout.Button("Generate"))
        {
            Undo.SetCurrentGroupName("Generate Mesh Prefabs");
            int undoGroup = Undo.GetCurrentGroup();

            scatter.Generate();
            if (scatter.GeneratedParent != null)
                Undo.RegisterCreatedObjectUndo(scatter.GeneratedParent.gameObject, "Generate Mesh Prefabs");

            EditorUtility.SetDirty(scatter);
            Undo.CollapseUndoOperations(undoGroup);
        }

        using (new EditorGUI.DisabledScope(scatter.GeneratedParent == null))
        {
            if (GUILayout.Button("Clear"))
            {
                Undo.DestroyObjectImmediate(scatter.GeneratedParent.gameObject);
                EditorUtility.SetDirty(scatter);
            }
        }
    }

    private void OnSceneGUI()
    {
        MeshPrefabScatter scatter = (MeshPrefabScatter)target;
        if (!scatter.LimitDistance)
            return;

        Vector3 center = scatter.DistanceCenterPosition;
        bool horizontal = scatter.HorizontalDistanceOnly;

        DrawRadiusHandle(
            "maximumDistance",
            center,
            scatter.MaximumDistance,
            horizontal,
            Vector3.right,
            new Color(0.2f, 0.8f, 1f, 0.9f));

        DrawRadiusHandle(
            "minimumDistance",
            center,
            scatter.MinimumDistance,
            horizontal,
            Vector3.left,
            new Color(1f, 0.5f, 0.2f, 0.9f));
    }

    private void DrawRadiusHandle(
        string propertyName,
        Vector3 center,
        float radius,
        bool horizontal,
        Vector3 handleDirection,
        Color color)
    {
        Handles.color = color;
        EditorGUI.BeginChangeCheck();

        float newRadius;
        if (horizontal)
        {
            Handles.DrawWireDisc(center, Vector3.up, radius);

            Vector3 handlePosition = center + handleDirection * radius;
            float handleSize = HandleUtility.GetHandleSize(handlePosition) * 0.08f;
            Vector3 movedPosition = Handles.Slider(
                handlePosition,
                handleDirection,
                handleSize,
                Handles.DotHandleCap,
                0f);

            newRadius = Vector3.Dot(movedPosition - center, handleDirection);
        }
        else
        {
            newRadius = Handles.RadiusHandle(Quaternion.identity, center, radius);
        }

        if (!EditorGUI.EndChangeCheck())
            return;

        serializedObject.Update();
        SerializedProperty minimum = serializedObject.FindProperty("minimumDistance");
        SerializedProperty maximum = serializedObject.FindProperty("maximumDistance");
        newRadius = Mathf.Max(0f, newRadius);

        if (propertyName == "minimumDistance")
            minimum.floatValue = Mathf.Min(newRadius, maximum.floatValue);
        else
        {
            maximum.floatValue = newRadius;
            minimum.floatValue = Mathf.Min(minimum.floatValue, newRadius);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPrefabDropArea()
    {
        EditorGUILayout.Space();
        Rect dropArea = GUILayoutUtility.GetRect(
            0f,
            48f,
            GUILayout.ExpandWidth(true));

        GUI.Box(dropArea, "Drop one or multiple prefabs here", EditorStyles.helpBox);

        Event currentEvent = Event.current;
        if (!dropArea.Contains(currentEvent.mousePosition))
            return;

        if (currentEvent.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = HasDraggedPrefabs()
                ? DragAndDropVisualMode.Copy
                : DragAndDropVisualMode.Rejected;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type != EventType.DragPerform || !HasDraggedPrefabs())
            return;

        DragAndDrop.AcceptDrag();
        AddDraggedPrefabs();
        currentEvent.Use();
    }

    private static bool HasDraggedPrefabs()
    {
        foreach (Object draggedObject in DragAndDrop.objectReferences)
        {
            if (draggedObject is GameObject gameObject &&
                PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                return true;
            }
        }

        return false;
    }

    private void AddDraggedPrefabs()
    {
        serializedObject.Update();
        SerializedProperty prefabsProperty = serializedObject.FindProperty("prefabs");

        foreach (Object draggedObject in DragAndDrop.objectReferences)
        {
            if (draggedObject is not GameObject prefab ||
                !PrefabUtility.IsPartOfPrefabAsset(prefab) ||
                ContainsPrefab(prefabsProperty, prefab))
            {
                continue;
            }

            int newIndex = prefabsProperty.arraySize;
            prefabsProperty.InsertArrayElementAtIndex(newIndex);

            SerializedProperty entry = prefabsProperty.GetArrayElementAtIndex(newIndex);
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("weight").floatValue = 1f;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool ContainsPrefab(SerializedProperty prefabsProperty, GameObject prefab)
    {
        for (int i = 0; i < prefabsProperty.arraySize; i++)
        {
            SerializedProperty entry = prefabsProperty.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("prefab").objectReferenceValue == prefab)
                return true;
        }

        return false;
    }
}
