using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(JengaStructureBuilder)), CanEditMultipleObjects]
public class JengaStructureBuilderEditor : Editor
{
    // Settings field per StructureType, in the same order as the enum.
    private static readonly string[] SettingsFields = { "tower", "pyramid", "skyline", "spiral" };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty typeProperty = serializedObject.FindProperty("structureType");
        SerializedProperty useCustomSize = serializedObject.FindProperty("useCustomBlockSize");
        bool showSizeFields = useCustomSize.boolValue || useCustomSize.hasMultipleDifferentValues;
        SerializedProperty iterator = serializedObject.GetIterator();
        for (bool enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
        {
            if (iterator.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(iterator);
                EditorGUILayout.PropertyField(typeProperty);
                continue;
            }

            if (iterator.name == "structureType" || System.Array.IndexOf(SettingsFields, iterator.name) >= 0)
                continue;

            // Mass scaling stays visible: Block Scale changes the volume too.
            bool isSizeField = iterator.name == "customBlockSize";
            if (isSizeField && !showSizeFields)
                continue;

            using (new EditorGUI.IndentLevelScope(isSizeField ? 1 : 0))
                EditorGUILayout.PropertyField(iterator, true);

            if (iterator.name == "customBlockSize")
                DrawPrefabSize(iterator);
        }

        // Only the settings of the selected type.
        if (!typeProperty.hasMultipleDifferentValues &&
            typeProperty.enumValueIndex >= 0 && typeProperty.enumValueIndex < SettingsFields.Length)
        {
            SerializedProperty settings = serializedObject.FindProperty(SettingsFields[typeProperty.enumValueIndex]);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(typeProperty.enumDisplayNames[typeProperty.enumValueIndex] + " Settings",
                EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                SerializedProperty end = settings.GetEndProperty();
                SerializedProperty child = settings.Copy();
                for (bool enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
                    EditorGUILayout.PropertyField(child, true);
            }
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Build"))
        {
            foreach (Object selected in targets)
                ((JengaStructureBuilder)selected).Build();
        }

        if (GUILayout.Button("Clear"))
        {
            foreach (Object selected in targets)
                ((JengaStructureBuilder)selected).Clear();
        }

        // Block count helps to keep the physics load in check (Quest).
        if (targets.Length == 1)
        {
            DrawLayerInfo();

            Transform parent = ((JengaStructureBuilder)target).BlocksParent;
            if (parent != null)
                EditorGUILayout.HelpBox($"Blocks: {parent.childCount}", MessageType.None);
        }
    }

    /// <summary>Shows how a layer is laid out with the current size and warns about proportions that won't stack well.</summary>
    private void DrawLayerInfo()
    {
        var builder = (JengaStructureBuilder)target;
        if (!builder.TryGetLayerInfo(out Vector3 size, out int count, out float gapBetween))
            return;

        bool auto = serializedObject.FindProperty("blocksPerLayer").intValue == 0;
        EditorGUILayout.HelpBox(
            $"Block L {size.x:0.###}  H {size.y:0.###}  W {size.z:0.###}\n" +
            $"Layer: {count} block(s) side by side{(auto ? " (auto)" : "")}, gap between blocks {gapBetween:0.###}",
            MessageType.None);

        float length = size.x, height = size.y, width = size.z;
        if (count == 1)
            EditorGUILayout.HelpBox("The block is not clearly longer than wide, so every layer is a single block " +
                "(the structure becomes a stack of crosses).", MessageType.Info);
        if (count * width > length * 1.05f)
            EditorGUILayout.HelpBox("The layer is wider than one block is long: the ends of the turned layer above " +
                "hang over. Use fewer blocks per layer (0 = auto).", MessageType.Warning);
        else if (count > 1 && gapBetween > width * 0.5f)
            EditorGUILayout.HelpBox("Big gaps between the blocks of a layer. Use more blocks per layer (0 = auto).",
                MessageType.Warning);
        if (height > width)
            EditorGUILayout.HelpBox("The blocks are higher than wide, so they stand on their narrow side. " +
                "Structures will be wobbly.", MessageType.Warning);
    }

    /// <summary>Shows the prefab's own size (length x height x width) with a button to copy it into the custom size.</summary>
    private void DrawPrefabSize(SerializedProperty customSize)
    {
        if (targets.Length != 1 || !((JengaStructureBuilder)target).TryGetPrefabBlockSize(out Vector3 size))
            return;

        using (new EditorGUI.IndentLevelScope())
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Prefab Size",
                $"L {size.x:0.###}  H {size.y:0.###}  W {size.z:0.###}", EditorStyles.miniLabel);
            if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(50f)))
                customSize.vector3Value = size;
        }
    }
}
