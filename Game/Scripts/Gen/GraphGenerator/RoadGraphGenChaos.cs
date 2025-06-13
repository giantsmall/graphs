using Assets.Game.Scripts.Editors;
using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Gen.Utils;
using Assets.Game.Scripts.Utility;
using ClipperLib;
using Delaunay;
using NUnit.Framework;
using SharpGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Assets.Game.Scripts.Gen.GraphGenerator
{
    //rozszerzyæ to o np. wykrywanie wysp (oddzielnych komponentów grafu), samotnych wierzcho³ków albo topologiczne „dziury”.
    //Seed with very small triangle or strange wields: 68e7c8ae-9808-4335-ab18-2a4de60b7804
    //See with strange wields      : ff89438a-e1d3-422c-b6e9-aa88191b2c12
    public class RoadGraphGenChaos : MonoBehaviour
    {
        #region hide
        Voronoi voronoi = new Voronoi(new List<Vector2>(), new Rect());
        List<Polygon> badLots = new List<Polygon>();
        List<PtWSgmnts> badPoints = new List<PtWSgmnts>();

        public bool DrawTriangles = true;
        public bool DrawSpanningTree = true;
        public bool DrawEdges = true;
        public bool DrawRemovedEdges = true;
        public bool DrawBoundaries = true;
        public bool DrawDistricts = true;
        public bool DrawMainRoads = true;
        public bool DrawBlocks = true;
        public bool DrawLots = true;
        List<Polygon> districtFaces = new();
        List<Polygon> blocks = new();
        List<Polygon> allLots = new();
        List<Vector2> gatesCandidates = new();
        Polygon outerWall = new();
        Polygon innerMoat = new();
        Polygon outerMoat = new();
        Polygon roadAroundMoatInner = new();
        Polygon roadAroundMoatOuter = new();
        Polygon innerCircle = new();
        List<LineSegment> allEdges = new List<LineSegment>();
        List<Vector2> mainRoadDirs = new();

        public float minLotWidth = 1f, maxLotWidth = 2f, minLotDepth = 1f, maxLotDepth = 2f;
        public float MinBlockAreaToPutShortEdgesAway = 4f;
        public static float maxStreetLen = 3f;
        static List<int> seedValues = new List<int>();
        static int currentSeed = 0;
        public int mainRoadsCount = 3;

        public bool DealWithShortBlockEdges = true;
        public bool CreateLots = false;

        public float MinEdgeLength = 1.5f;
        public float minPtsDistToNoTWield = 1f;
        public int MainRoadsCount { get; internal set; } = 3;
        #endregion 
        public int InnerCirDistrictCount = 6;
        public float InnerVoronoiSize = 5f;
        public float OuterVoronoiSize = 10f;
        public int InnerCirclePopulation = 600;
        public float PopulationOutsideInnerCircle = 400;
        public bool PolishInnerCircle = true;
        public float WallThickness = 0.2f;
        public float RoadWidth = 0.2f;
        public float MoatWidth = 0.2f;
        public float MoatDistFromWall = 0.3f;
        public float MoatDistToRoad = 0.3f;

        public bool FlattenTriangles = false;
        public int MapSize = 100;
        public static bool FixedRandom { get; internal set; } = true;
        public static bool Randomize { get; internal set; } = false;
        static System.Random rnd = new System.Random();
        public static string Seed { get; internal set; } = "68e7c8ae-9808-4335-ab18-2a4de60b7804";

        public bool BuildMoat = false;
        public bool BuildWall = false;

        static SettlementModel s = new SettlementModel(new PtWSgmnts());
        void Start()
        {
        }

        #region works
        public static void ClearLog()
        {
#if UNITY_EDITOR
            var assembly = Assembly.GetAssembly(typeof(UnityEditor.Editor));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
#endif
        }

        public static void ClearSeeds()
        {
            seedValues.Clear();
        }

        public void GeneratePrev()
        {
            if (seedValues.Any())
            {
                currentSeed = Math.Max(currentSeed - 1, 0);
                Randomize = false;
                Generate();
            }
        }

        public void GenerateNext()
        {
            if (Randomize)
            {
                Seed = Guid.NewGuid().ToString();
                MD5 md5Hasher = MD5.Create();
                var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(Seed));
                var intValue = BitConverter.ToInt32(hashed, 0);

                seedValues.Add(intValue);
                currentSeed = seedValues.Count - 1;
            }

            if (!seedValues.Any())
            {
                MD5 md5Hasher = MD5.Create();
                var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(Seed));
                var intValue = BitConverter.ToInt32(hashed, 0);

                seedValues.Add(intValue);
                currentSeed = seedValues.Count - 1;
            }
            rnd = new System.Random(seedValues.Last());
            Generate();
        }
        #endregion

        void Generate()
        {
            #region clear
            GraphHelpers.intersectingSegments.Clear();
            innerCircle.Clear();
            badPoints.Clear();
            badLots.Clear();
            gatesCandidates.Clear();
            allEdges.Clear();
            blocks.Clear();
            districtFaces.Clear();
            allLots.Clear();
            InnerCirclePopulation = 600;
            outerWall.Points.Clear();
            innerMoat.Points.Clear();
            outerMoat.Points.Clear();
            roadAroundMoatInner.Points.Clear();
            roadAroundMoatOuter.Points.Clear();
            mainRoadDirs.Clear();
            PtWSgmnts.ResetCount();
            LineSegment.ResetCount();
            Polygon.ResetCount();

            ClearLog();
            #endregion

            var center = new Vector2(MapSize / 2f, MapSize / 2f);
            center += new Vector2(rnd.NextFloat(-5, 5), rnd.NextFloat(-5, 5));
            s = new SettlementModel(new PtWSgmnts(center));
            var innerCircleRadius = Mathf.Sqrt(InnerCirclePopulation);
            var outerCircleRadius = Mathf.Sqrt(InnerCirclePopulation + PopulationOutsideInnerCircle * 2);
            s.OuterCircle = new(GraphHelpers.CreateCircle(outerCircleRadius, rnd, maxStreetLen, s.center));
            s.InnerCircle = new(GraphHelpers.CreateCircle(innerCircleRadius, rnd, maxStreetLen, s.center));

            districtFaces = SplitOuterAreaIntoPolygons(new Polygon(s.OuterCircle), OuterVoronoiSize, .3f);
            districtFaces = districtFaces.OrderBy(d => d.FindCenter().DistanceTo(s.center)).ToList();

            var innerCircleCycles = districtFaces.OrderBy(d => d.FindCenter().DistanceTo(s.center)).Where(d => d.FindCenter().DistanceTo(s.center) < innerCircleRadius * .8f).ToList();
            innerCircleCycles = innerCircleCycles.Take(InnerCirDistrictCount).ToList();
            var closestCount = innerCircleCycles.Count;
            districtFaces = districtFaces.Where(f => !innerCircleCycles.Contains(f)).ToList();
            var outerDistricts = districtFaces.Except(innerCircleCycles).ToList();

            innerCircle = PolygonMerge.MergeAdjacentPolygons(innerCircleCycles);
            var neighbours = IdentifyNeighbours(innerCircle, outerDistricts);
            //Polygon.DrawCenters(neighbours);
            //Polygon.DrawParentCountPerPt(neighbours);

            districtFaces.Add(innerCircle);
            innerCircle = PolygonMerge.IncludeDeepNeighbours(innerCircle, outerDistricts);

            //RemoveDuplicatePoints
            //create innerCircle
            Debug.Log(districtFaces.Count);
        }

        public static List<Polygon> IdentifyNeighbours(Polygon needsNeighbours, List<Polygon> polys)
        {
            return polys.Where(b => b.Points.Any(p => needsNeighbours.ContainsCheckpointPos(p))).ToList();
        }


        List<Polygon> SplitOuterAreaIntoPolygons(Polygon outerPolygon, float polySize, float offset = 0)
        {
            List<Polygon> facesExtracted = new List<Polygon>();
            var outerCircle = s.OuterCircle;
            var center = outerPolygon.FindCenter();

            var rect = outerPolygon.GetRectangleCircumscribedInPolygon(offset);
            var pts = PoissonDiscSampler2D.GeneratePoints(rnd, polySize, rect);

            pts = PoissonDiscSampler2D.GeneratePoints(rnd, polySize, rect);
            return GenerateVoronoiPolygons(pts, rect);
        }

        public List<Polygon> GenerateVoronoiPolygons(List<Vector2> points, Rect bounds)
        {
            // Konwersja do typu wymaganego przez bibliotekê
            var delaunayPoints = points.Select(p => new Vertex(p.x, p.y)).ToList();

            // Tworzenie diagramu Delaunay i generowanie Voronoi
            voronoi = new Voronoi(points, bounds);
            var triangulation = voronoi.DelaunayTriangulation();

            var polygons = new List<Polygon>();
            List<PtWSgmnts> totalPoints = new List<PtWSgmnts>();
            List<PtWSgmnts> totalPointsNotDistinct = new List<PtWSgmnts>();
            foreach (var region in voronoi.Regions())
            {
                var poly = new Polygon();
                poly.AddCheckPoints(region
                    .Select((v, i) => new PtWSgmnts { pos = v })
                    .ToArray());
                polygons.Add(poly);
                totalPointsNotDistinct.AddRange(poly.Points);
                poly.ReplacePointsWithSamePos(totalPoints);
                totalPoints.AddRange(poly.Points);
                totalPoints = totalPoints.Distinct(new PointsComparer(true)).ToList();
            }

            Debug.Log($"Created polygons: {polygons.Count}");
            Debug.Log($"Total points: {totalPoints.Count}, not distinct: {totalPointsNotDistinct.Count}");
            return polygons;
        }

        private void DrawFace(Polygon face, Color color, Vector2? shift = null)
        {
            Gizmos.color = color;
            var shiftVal = shift.HasValue ? shift.Value : Vector2.zero;
            for (int i = 0; i < face.Points.Count; i++)
            {
                var a = face[i].pos;
                var b = face.Points.Neighbour(i, 1).pos;
                Gizmos.DrawLine(a + shiftVal, b + shiftVal);
                Gizmos.DrawSphere(a + shiftVal, 0.1f);
            }
        }

        public void OnDrawGizmos()
        {
            var shift150_150 = new Vector2(150, -150f);
            var shift150_0 = new Vector2(0, 0f);

            foreach(var face in districtFaces)
            {                
                Color c = Color.black;
                DrawFace(face, c, shift150_0);
                //DrawFace(districtFaces[i].points, c, shift150_150);
                shift150_150 += new Vector2(.1f, -.1f);
            }

            DrawFace(innerCircle, Color.white);
        }
    }
}
