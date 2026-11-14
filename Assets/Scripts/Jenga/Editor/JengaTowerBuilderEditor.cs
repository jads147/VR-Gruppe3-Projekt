using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(JengaTowerBuilder))]
public class JengaTowerBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Build Tower"))
        {
            foreach (Object selected in targets)
                ((JengaTowerBuilder)selected).BuildTower();
        }

        if (GUILayout.Button("Clear Tower"))
        {
            foreach (Object selected in targets)
                ((JengaTowerBuilder)selected).ClearTower();
        }
    }
}
