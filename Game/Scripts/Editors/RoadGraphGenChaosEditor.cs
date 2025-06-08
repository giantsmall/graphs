using UnityEditor;
using UnityEngine;
using Assets.Game.Scripts.Gen.GraphGenerator;

#if UNITY_EDITOR
namespace Assets.Game.Scripts.Editors
{
    [CustomEditor(typeof(RoadGraphGenChaosNew))]
    public class RoadGraphGenChaosEditor : Editor
    {
        static bool changed = false;
        public override void OnInspectorGUI()
        {
            changed = false;
            var gg = (RoadGraphGenChaosNew)target;

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
            gg.OuterVoronoiSize = EditorGUILayout.FloatField(nameof(gg.OuterVoronoiSize), gg.OuterVoronoiSize);
            gg.InnerVoronoiSize = EditorGUILayout.FloatField(nameof(gg.InnerVoronoiSize), gg.InnerVoronoiSize);
            gg.minPtsDistToNoTWield = EditorGUILayout.FloatField(nameof(gg.minPtsDistToNoTWield), gg.minPtsDistToNoTWield);

            GUILayout.BeginHorizontal();
            RoadGraphGenChaosNew.FixedRandom = EditorGUILayout.Toggle(nameof(RoadGraphGenChaosNew.FixedRandom), RoadGraphGenChaosNew.FixedRandom);
            RoadGraphGenChaosNew.Randomize = EditorGUILayout.Toggle(nameof(RoadGraphGenChaosNew.Randomize), RoadGraphGenChaosNew.Randomize);
            GUILayout.EndHorizontal();
            RoadGraphGenChaosNew.Seed = EditorGUILayout.TextField(nameof(RoadGraphGenChaosNew.Seed), RoadGraphGenChaosNew.Seed);

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