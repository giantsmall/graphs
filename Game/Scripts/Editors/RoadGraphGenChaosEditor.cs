using UnityEditor;
using UnityEngine;
using Assets.Game.Scripts.Gen.GraphGenerator;

#if UNITY_EDITOR

namespace Assets.Game.Scripts.Editors
{
    [CustomEditor(typeof(RoadGraphGenChaos))]
    public class RoadGraphGenChaosEditor : Editor
    {
        static bool changed = false;
        public override void OnInspectorGUI()
        {
            var gg = (RoadGraphGenChaos)target;
            changed = false;


            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear seeds")) { RoadGraphGenChaos.ClearSeeds(); }
            if (GUILayout.Button("Regen prev")) { gg.GeneratePrev(); changed = true; }
            if (GUILayout.Button("Generate")) { gg.GenerateNext(); changed = true; }
            GUILayout.EndHorizontal();
            if (changed)
                SceneView.RepaintAll();
        }
    }
}
#endif