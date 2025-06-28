// ---- Assets/Scripts/Dungeon/Editor/DungeonGeneratorEditor.cs ----------
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DungeonGenerator))]
public class DungeonGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DungeonGenerator gen = (DungeonGenerator)target;
        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Dungeon"))
        {
            Undo.RegisterCompleteObjectUndo(gen, "Generate Dungeon");
            gen.Generate();
        }
        if (GUILayout.Button("Clear Dungeon"))
        {
            Undo.RegisterCompleteObjectUndo(gen, "Clear Dungeon");
            gen.Clear();
        }
    }
}
