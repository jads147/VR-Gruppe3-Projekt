using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HouseBuilder))]
public class HouseBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Build House"))
        {
            foreach (Object selected in targets)
                ((HouseBuilder)selected).BuildHouse();
        }

        if (GUILayout.Button("Clear House"))
        {
            foreach (Object selected in targets)
                ((HouseBuilder)selected).ClearHouse();
        }
    }
}
