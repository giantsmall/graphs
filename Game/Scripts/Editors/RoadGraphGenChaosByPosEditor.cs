using UnityEditor;
using UnityEngine;
using Assets.Game.Scripts.Gen.GraphGenerator;

#if UNITY_EDITOR
namespace Assets.Game.Scripts.Editors
{
    [CustomEditor(typeof(RoadGraphGenChaosByPos))]
    public class RoadGraphGenChaosByPosEditor : Editor
    {
        static bool changed = false;
        public override void OnInspectorGUI()
        {
            changed = false;
            var gg = (RoadGraphGenChaosByPos)target;

            //minWidth = 1f, maxWidth = 2f, minDepth = 1f, maxDepth = 2f;
            GUILayout.BeginHorizontal();
            gg.minLotWidth = EditorGUILayout.FloatField(nameof(gg.minLotWidth), gg.minLotWidth);
            gg.maxLotWidth = EditorGUILayout.FloatField(nameof(gg.maxLotWidth), gg.maxLotWidth);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            gg.minLotDepth = EditorGUILayout.FloatField(nameof(gg.minLotDepth), gg.minLotDepth);
            gg.maxLotDepth = EditorGUILayout.FloatField(nameof(gg.maxLotDepth), gg.maxLotDepth);
            GUILayout.EndHorizontal();
            
            //gg.DrawSpanningTree = HandleToggle(nameof(gg.DrawSpanningTree), gg.DrawSpanningTree);
            //gg.DrawTriangles = HandleToggle(nameof(gg.DrawTriangles), gg.DrawTriangles);
            //gg.DrawBoundaries = HandleToggle(nameof(gg.DrawBoundaries), gg.DrawBoundaries);
            gg.MapSize = EditorGUILayout.IntField(nameof(gg.MapSize), gg.MapSize);

            //gg.DrawDistricts = HandleToggle(nameof(gg.DrawDistricts), gg.DrawDistricts);
            //gg.DrawBlocks = HandleToggle(nameof(gg.DrawBlocks), gg.DrawBlocks);
            //gg.DrawMainRoads = HandleToggle(nameof(gg.DrawMainRoads), gg.DrawMainRoads);
            //gg.InsertIntersections = HandleToggle(nameof(gg.InsertIntersections), gg.InsertIntersections);

            //gg.MinEdgeLength = EditorGUILayout.FloatField(nameof(gg.MinEdgeLength), gg.MinEdgeLength);
            gg.OuterVoronoiSize = EditorGUILayout.FloatField(nameof(gg.OuterVoronoiSize), gg.OuterVoronoiSize);
            gg.InnerVoronoiSize = EditorGUILayout.FloatField(nameof(gg.InnerVoronoiSize), gg.InnerVoronoiSize);
            gg.InnerCirDistrictCount = EditorGUILayout.IntField(nameof(gg.InnerCirDistrictCount), gg.InnerCirDistrictCount);
            gg.mainRoadsCount = EditorGUILayout.IntField(nameof(gg.mainRoadsCount), gg.mainRoadsCount);
            
            gg.FlattenTriangles = EditorGUILayout.Toggle(nameof(gg.FlattenTriangles), gg.FlattenTriangles);

            gg.PolishInnerCircle = EditorGUILayout.Toggle(nameof(gg.PolishInnerCircle), gg.PolishInnerCircle);
            gg.CreateLots = EditorGUILayout.Toggle(nameof(gg.CreateLots), gg.CreateLots);
            gg.DealWithShortBlockEdges = EditorGUILayout.Toggle(nameof(gg.DealWithShortBlockEdges), gg.DealWithShortBlockEdges);
            gg.RoadWidth = EditorGUILayout.FloatField(nameof(gg.RoadWidth), gg.RoadWidth);
            gg.MinBlockAreaToPutShortEdgesAway = EditorGUILayout.FloatField(nameof(gg.MinBlockAreaToPutShortEdgesAway), gg.MinBlockAreaToPutShortEdgesAway);

            GUILayout.Box("Wall settings", GUILayout.ExpandWidth(true));
            gg.BuildWall = EditorGUILayout.Toggle(nameof(gg.BuildWall), gg.BuildWall);
            if (gg.BuildWall)
            {
                gg.WallThickness = EditorGUILayout.FloatField(nameof(gg.WallThickness), gg.WallThickness);
                GUILayout.Box("Moat settings", GUILayout.ExpandWidth(true));
                gg.BuildMoat = EditorGUILayout.Toggle(nameof(gg.BuildMoat), gg.BuildMoat);
                if(gg.BuildMoat)
                {                    
                    gg.MoatDistFromWall = EditorGUILayout.FloatField(nameof(gg.MoatDistFromWall), gg.MoatDistFromWall);
                    gg.MoatWidth = EditorGUILayout.FloatField(nameof(gg.MoatWidth), gg.MoatWidth);
                    gg.MoatDistToRoad = EditorGUILayout.FloatField(nameof(gg.MoatDistFromWall), gg.MoatDistToRoad);
                }
            }

            GUILayout.Box("Seed settings", GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            RoadGraphGenChaosByPos.FixedRandom = EditorGUILayout.Toggle(nameof(RoadGraphGenChaosByPos.FixedRandom), RoadGraphGenChaosByPos.FixedRandom);
            RoadGraphGenChaosByPos.Randomize = EditorGUILayout.Toggle(nameof(RoadGraphGenChaosByPos.Randomize), RoadGraphGenChaosByPos.Randomize);
            GUILayout.EndHorizontal();
            RoadGraphGenChaosByPos.Seed = EditorGUILayout.TextField(nameof(RoadGraphGenChaosByPos.Seed), RoadGraphGenChaosByPos.Seed);

            GUILayout.TextArea("68e7c8ae-9808-4335-ab18-2a4de60b7804 <- ");
            GUILayout.TextArea("12295635-5138-4239-998d-afb6b62d1131 <- some twists");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear seeds")) { RoadGraphGenChaosByPos.ClearSeeds(); }
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