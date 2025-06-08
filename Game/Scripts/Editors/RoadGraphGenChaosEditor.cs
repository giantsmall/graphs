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
            changed = false;
            RoadGraphGenChaos gg = (RoadGraphGenChaos)target;

            GUILayout.BeginHorizontal();
            gg.DrawEdges = HandleToggle(nameof(gg.DrawEdges), gg.DrawEdges);                        
            gg.DrawRemovedEdges = HandleToggle(nameof(gg.DrawRemovedEdges), gg.DrawRemovedEdges);
            GUILayout.EndHorizontal();

            gg.DrawSpanningTree = HandleToggle(nameof(gg.DrawSpanningTree), gg.DrawSpanningTree);
            gg.DrawTriangles = HandleToggle(nameof(gg.DrawTriangles), gg.DrawTriangles);
            gg.DrawBoundaries = HandleToggle(nameof(gg.DrawBoundaries), gg.DrawBoundaries);
            gg.MapSize = EditorGUILayout.IntField(nameof(gg.MapSize), gg.MapSize);

            gg.DrawDistricts = HandleToggle(nameof(gg.DrawDistricts), gg.DrawDistricts);
            gg.DrawBlocks = HandleToggle(nameof(gg.DrawBlocks), gg.DrawBlocks);
            gg.DrawMainRoads = HandleToggle(nameof(gg.DrawMainRoads), gg.DrawMainRoads);
            gg.InsertIntersections = HandleToggle(nameof(gg.InsertIntersections), gg.InsertIntersections);

            gg.MinEdgeLength = EditorGUILayout.FloatField(nameof(gg.MinEdgeLength), gg.MinEdgeLength);
            gg.MinBlockPtsRad = EditorGUILayout.FloatField(nameof(gg.MinBlockPtsRad), gg.MinBlockPtsRad);
            gg.minPtsDistToNoTWield = EditorGUILayout.FloatField(nameof(gg.minPtsDistToNoTWield), gg.minPtsDistToNoTWield);

            GUILayout.BeginHorizontal();
            RoadGraphGenChaos.FixedRandom = EditorGUILayout.Toggle(nameof(RoadGraphGenChaos.FixedRandom), RoadGraphGenChaos.FixedRandom);
            RoadGraphGenChaos.Randomize = EditorGUILayout.Toggle(nameof(RoadGraphGenChaos.Randomize), RoadGraphGenChaos.Randomize);
            GUILayout.EndHorizontal();
            RoadGraphGenChaos.Seed = EditorGUILayout.TextField(nameof(RoadGraphGenChaos.Seed), RoadGraphGenChaos.Seed);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear seeds")) { RoadGraphGenChaos.ClearSeeds(); }
            if (GUILayout.Button("Regen prev")) { gg.GeneratePrev(); changed = true; }
            if (GUILayout.Button("Generate")) { gg.GenerateNext(); changed = true; }
            GUILayout.EndHorizontal();
            if (changed)
                SceneView.RepaintAll();
        }

        bool HandleToggle(string name, bool value)
        {
            var prev = value;
            var result = EditorGUILayout.Toggle(name, value);
            if(prev != result)
            {
                changed = true;
            }
            return result;
        }
    }
}
#endif