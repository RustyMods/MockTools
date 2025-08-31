using UnityEditor;
using UnityEngine;

namespace MockTools;

[CustomEditor(typeof(SetupMock))]
public class SetupMockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SetupMock script = (SetupMock)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Get ZNetScene Objects"))
        {
            Undo.RecordObject(script, "Get ZNetScene Objects");
            script.FindObjects();
        }

        if (GUILayout.Button("Rename"))
        {
            Undo.RegisterFullObjectHierarchyUndo(script.gameObject, "Rename");
            script.Rename();
        }

        if (GUILayout.Button("Cleanup"))
        {
            Undo.RegisterFullObjectHierarchyUndo(script.gameObject, "Cleanup");
            script.Cleanup();
        }
        
        if (GUILayout.Button("Get Shaders"))
        {
            Undo.RecordObject(script, "Get Shaders");
            script.FindShaders();
        }
        
        if (GUILayout.Button("Replace Shaders"))
        {
            Undo.RegisterFullObjectHierarchyUndo(script.gameObject, "Replace Shaders");
            script.ReplaceShaders();
        }
    }
}