#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// À placer dans un dossier nommé "Editor" (par exemple Assets/Scripts/Procedural/Editor)
[CustomEditor(typeof(ScatterGenerator))]
public class ScatterGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var g = (ScatterGenerator)target;

        if (GUILayout.Button("Generate")) g.Generate();
        if (GUILayout.Button("Clear")) g.Clear();
        if (GUILayout.Button("Seed aléatoire + Generate"))
        {
            Undo.RecordObject(g, "Changer la seed");
            g.seed = Random.Range(0, 100000);   // ici l'aléa sert seulement à CHOISIR une seed
            g.Generate();
            EditorUtility.SetDirty(g);
        }
    }
}
#endif