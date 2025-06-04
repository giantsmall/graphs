using Assets.Game.Scripts.Gen;
using Assets.Game.Scripts.Gen.WorldGen;
using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(ParcelGenerator))]
public class ParcelGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var modelGen = (ParcelGenerator)target;
        if (GUILayout.Button("Generate"))
        {
            modelGen.Generate();
        }
    }
}
