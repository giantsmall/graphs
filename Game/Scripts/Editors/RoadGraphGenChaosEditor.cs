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
            gg.minLotWidth = EditorGUILayout.FloatField(nameof(gg.minLotWidth), gg.minLotWidth);
            gg.maxLotWidth = EditorGUILayout.FloatField(nameof(gg.maxLotWidth), gg.maxLotWidth);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            gg.minLotDepth = EditorGUILayout.FloatField(nameof(gg.minLotDepth), gg.minLotDepth);
            gg.maxLotDepth = EditorGUILayout.FloatField(nameof(gg.maxLotDepth), gg.maxLotDepth);
            GUILayout.EndHorizontal();

            gg.OuterVoronoiSize = EditorGUILayout.FloatField(nameof(gg.OuterVoronoiSize), gg.OuterVoronoiSize);
            gg.InnerVoronoiSize = EditorGUILayout.FloatField(nameof(gg.InnerVoronoiSize), gg.InnerVoronoiSize);
            gg.ResolveShortEdges = EditorGUILayout.Toggle(nameof(gg.ResolveShortEdges), gg.ResolveShortEdges);

            gg.BuildRoads = EditorGUILayout.Toggle(nameof(gg.BuildRoads), gg.BuildRoads);
            if (gg.BuildRoads)
            {
                gg.RoadWidth = EditorGUILayout.FloatField(nameof(gg.RoadWidth), gg.RoadWidth);
            }

            GUILayout.Box("Wall settings", GUILayout.ExpandWidth(true));
            gg.BuildWall = EditorGUILayout.Toggle(nameof(gg.BuildWall), gg.BuildWall);
            if (gg.BuildWall)
            {
                gg.WallThickness = EditorGUILayout.FloatField(nameof(gg.WallThickness), gg.WallThickness);
                GUILayout.Box("Moat settings", GUILayout.ExpandWidth(true));
                gg.BuildMoat = EditorGUILayout.Toggle(nameof(gg.BuildMoat), gg.BuildMoat);
                if (gg.BuildMoat)
                {
                    gg.MoatDistFromWall = EditorGUILayout.FloatField(nameof(gg.MoatDistFromWall), gg.MoatDistFromWall);
                    gg.MoatWidth = EditorGUILayout.FloatField(nameof(gg.MoatWidth), gg.MoatWidth);
                    gg.MoatDistToRoad = EditorGUILayout.FloatField(nameof(gg.MoatDistFromWall), gg.MoatDistToRoad);
                }
            }
            gg.CreateLots = EditorGUILayout.Toggle(nameof(gg.CreateLots), gg.CreateLots);

            GUILayout.Box("Seed settings", GUILayout.ExpandWidth(true));
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
    }
}
#endif