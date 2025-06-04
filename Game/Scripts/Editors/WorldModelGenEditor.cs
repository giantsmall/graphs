using Assets.Game.Scripts.Gen.WorldGen;
using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(WorldModelGenerator))]
public class WorldModelGenEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var modelGen = (WorldModelGenerator)target;
        modelGen.seed = EditorGUILayout.IntField("Seed", modelGen.seed);
        if (GUILayout.Button("Generate"))
        {
            var model = modelGen.GenerateSector(100, 512, 512, 5);            
        }
    }
}
