using Assets.Game.Scripts.Editors;
using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Utility;
using ClipperLib;
using Delaunay;
using NUnit.Framework;
using SharpGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Game.Scripts.Gen.GraphGenerator
{
    //rozszerzyæ to o np. wykrywanie wysp (oddzielnych komponentów grafu), samotnych wierzcho³ków albo topologiczne „dziury”.
    //Seed with very small triangle or strange wields: 68e7c8ae-9808-4335-ab18-2a4de60b7804
    //See with strange wields      : ff89438a-e1d3-422c-b6e9-aa88191b2c12
    public class RoadGraphGenChaos : MonoBehaviour
    {
        public static float maxStreetLen = 3f;
        static List<int> seedValues = new List<int>();
        static int currentSeed = 0;
        public bool DrawTriangles = true;
        public bool DrawSpanningTree = true;
        public bool DrawEdges = true;
        public bool DrawRemovedEdges = true;
        public bool DrawBoundaries = true;
        public bool DrawDistricts = true;
        public bool DrawMainRoads = true;
        public bool DrawBlocks = true;
        public bool DrawLots = true;
        public bool InsertIntersections = true;

        public float MinEdgeLength = 2f;
        public float MinBlockPtsRad = 6f;
        public float minPtsDistToNoTWield = 1f;

        public int Population = 600;
        public float PopRatioOutsideWalls { get; internal set; } = 40;
        public int MainRoadsCount { get; internal set; } = 3;
        public int MapSize = 100;
        public static bool FixedRandom { get; internal set; } = true;
        public static bool Randomize { get; internal set; } = false;
        static System.Random rnd = new System.Random();
        public static string Seed { get; internal set; } = "68e7c8ae-9808-4335-ab18-2a4de60b7804";

        static SettlementModel s = new SettlementModel(new PtWSgmnts());
        void Start()
        {
            s = new SettlementModel(new PtWSgmnts());
        }

        public static void ClearLog()
        {
#if UNITY_EDITOR
            var assembly = Assembly.GetAssembly(typeof(UnityEditor.Editor));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
#endif
        }

        #region Works
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

        void Generate()
        {
            PtWSgmnts.ResetCount();
            LineSegment.ResetCount();
            ClearLog();
            Population = 600;

            var center = new Vector2(MapSize / 2f, MapSize / 2f);
            center += new Vector2(rnd.NextFloat(-5, 5), rnd.NextFloat(-5, 5));
            s = new SettlementModel(new PtWSgmnts(center));
            //CreateMainRoadsAndInitiallyMerge();

            //CreateInnerCircle();
            //AddRoadToFillGaps();
            //OrderRoadsAndGateByAngle();

            //DefineDistrictsWithinInnerCircle();
            //SplitDistrictsIntoBlocks();
            SplitInnerCirleIntoBlocks();

            AddVoronoisOutputToLists();
        }

        internal static System.Random GetRandom()
        {
            return rnd;
        }

        void GenerateMainRoadDirections()
        {
            var min = -100;
            var zero = 0;
            var hundred = 100;
            var max = 200;
            var roadDirs = new List<Vector2>();

            roadDirs.Add(new Vector2(min, min));
            roadDirs.Add(new Vector2(min, zero));
            roadDirs.Add(new Vector2(min, hundred));
            roadDirs.Add(new Vector2(min, max));

            roadDirs.Add(new Vector2(zero, min));
            roadDirs.Add(new Vector2(zero, max));

            roadDirs.Add(new Vector2(hundred, min));
            roadDirs.Add(new Vector2(hundred, max));

            roadDirs.Add(new Vector2(max, min));
            roadDirs.Add(new Vector2(max, zero));
            roadDirs.Add(new Vector2(max, hundred));
            roadDirs.Add(new Vector2(max, max));

            s.roadDirs = roadDirs.TakeRandom(rnd, MainRoadsCount);
        }
        void CreateMainRoadsAndInitiallyMerge()
        {
            GenerateMainRoadDirections();
            //roads
            var center = new PtWSgmnts(s.center);
            foreach (var dir in s.roadDirs)
            {
                var segment = new Street(center, new PtWSgmnts(dir));
                s.mainRoads.Add(segment);
                s.notJoinedRoads.Add(segment);
            }

            for (int i = 0; i < s.mainRoads.Count; i++)
            {
                var closeRoad = s.mainRoads.FirstOrDefault(mr => Vector2.Distance(mr.p1.pos, s.mainRoads[i].p1.pos) <= 100);
                if (closeRoad != null && closeRoad.Id != s.mainRoads[i].Id && !closeRoad.Joined)
                {
                    var posOnCloseRoad = Vector2.Lerp(closeRoad.p0.pos, closeRoad.p1.pos, rnd.NormalFloat(.2f, .5f));
                    s.notJoinedRoads.Remove(s.mainRoads[i]);
                    var intersection = new PtWSgmnts(posOnCloseRoad);
                    s.mainRoads[i] = new Street(intersection, s.mainRoads[i].p1);
                    s.mainRoads[i].Joined = true;
                    closeRoad.points.Insert(1, intersection);
                }
            }
        }

        void CreateInnerCircle()
        {
            var wallRadius = Mathf.Sqrt(Population * (1 - PopRatioOutsideWalls / 100f));

            var rnd = GetRandom();
            var innerCircleVctrs = new List<Vector2>();
            var flattenAngle = rnd.NextFloat(360);
            var swellAngle = rnd.NextFloat(360);
            var wallLength = wallRadius * 2 * Mathf.PI;
            var lineMaxLength = maxStreetLen * 2; //5f
            var pointsCount = Mathf.Round(wallLength / lineMaxLength);
            var angle = 360 / pointsCount;
            var pts = new List<Vector2>(); ;
            var currAngle = rnd.NextFloat(angle);
            for (int i = 0; i < pointsCount; i++)
            {
                var point = s.center + new Vector2(wallRadius, 0) + rnd.NextVector2(.15f);
                point = point.RotateAroundPivot(s.center, currAngle);
                pts.Add(point);
                currAngle += angle;
            }
            s.innerCircleStreet = new(pts.Select(p => new PtWSgmnts(p)).ToList());
            for (int i = 0; i < s.innerCircleStreet.points.Count; i++)
            {
                var curr = s.innerCircleStreet.points[i];
                var next = s.innerCircleStreet.points.Neighbour(i, 1);
                var prev = s.innerCircleStreet.points.Neighbour(i, -1);
            }

            s.innerCircleStreet.points.Add(s.innerCircleStreet.points[0]);
            foreach (var road in s.notJoinedRoads)
            {
                BuildIntersectionWithInnerCircle(road);
            }
        }

        PtWSgmnts BuildIntersectionWithInnerCircle(LineSegment mainRoad)
        {
            var innerPts = s.innerCircleStreet.points;
            Vector2? intersVector = null;
            var insertIndex = -1;
            for (int i = 0; i < innerPts.Count - 1; i++)
            {
                intersVector = VectorIntersect.GetIntersectionPoint(innerPts[i].pos, innerPts[i + 1].pos, mainRoad.p0.pos, mainRoad.p1.pos);
                if (intersVector != null)
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex < 0)
            {
                intersVector = VectorIntersect.GetIntersectionPoint(innerPts[0].pos, innerPts.Last().pos, mainRoad.p0.pos, mainRoad.p1.pos);
                if (intersVector != null)
                {
                    insertIndex = 0;
                }
            }

            var distToEdge = Vector2.Distance(innerPts[insertIndex].pos, intersVector.Value)
               / Vector2.Distance(innerPts[insertIndex].pos, innerPts[insertIndex.WrapIndex(1, innerPts)].pos);

            PtWSgmnts intersPoint = null;
            if (distToEdge > .85f)
            {
                intersPoint = innerPts[insertIndex];
                intersPoint.IntersectsWIthMainRoad = true;
                intersVector = innerPts[insertIndex].pos;

            }
            else if (distToEdge <= .15f)
            {
                intersPoint = innerPts[insertIndex.WrapIndex(1, innerPts)];
                intersPoint.IntersectsWIthMainRoad = true;
                intersVector = intersPoint.pos;
            }
            else
            {
                intersPoint = new PtWSgmnts(intersVector.Value) { IntersectsWIthMainRoad = true };
                s.innerCircleStreet.points.Insert(insertIndex + 1, intersPoint);
            }
            mainRoad.InsertInnerCircleIntersection(intersPoint, 1);
            return intersPoint;
        }

        void AddRoadToFillGaps()
        {
            if (s.mainRoads.Count >= 5)
                return;

            var rnd = GetRandom();
            int roadsToMake = Mathf.Min(5 - s.mainRoads.Count, 2);
            var points = s.innerCircleStreet.points;

            while (roadsToMake > 0)
            {
                var indexes = points.Where(p => p.IntersectsWIthMainRoad).Select(p => points.IndexOf(p)).ToList();
                indexes.Sort();
                var indexesDiff = new List<int>();
                for (int i = 0; i < indexes.Count - 1; i++)
                {
                    indexesDiff.Add(Mathf.Abs(indexes[i + 1] - indexes[i]));
                }
                indexesDiff.Add(points.Count - indexes.Last() + indexes.First() + 1);

                var maxDiff = indexesDiff.Max();
                if (maxDiff <= points.Count / 3)
                    break;

                var maxDiffIndex = indexesDiff.IndexOf(maxDiff);
                var index1 = indexes[maxDiffIndex];

                var avgIndex = index1.WrapIndex(maxDiff / 2 + rnd.Next(-1, 1), points);
                var newRoadEnd = points[avgIndex];
                var newRoad = new Street(new PtWSgmnts(s.center), newRoadEnd);
                //newRoad.InsertCheckpoint(points[avgIndex], newRoad.points.Count - 1);

                s.mainRoads.Add(newRoad);
                s.notJoinedRoads.Add(newRoad);
                newRoadEnd.IntersectsWIthMainRoad = true;
                newRoadEnd.AddMainPath(newRoad);
                newRoad.PointInnerCircleInters(newRoadEnd);
                roadsToMake--;
            }
        }
        void SplitAndDistortRoads()
        {
            foreach (var road in s.notJoinedRoads)
            {
                road.SplitAndDistort(maxStreetLen);
            }
            foreach (var road in s.mainRoads.Where(r => r.Joined))
            {
                road.SplitAndDistort(maxStreetLen);
            }
        }
        #endregion
        [Obsolete]
        void DefineDistrictsWithinInnerCircle()
        {
            for (int i = 0; i < s.notJoinedRoads.Count - 1; i++)
            {
                DefineInitialDistrict(s.notJoinedRoads[i], s.notJoinedRoads[i + 1]);
            }
            DefineInitialDistrict(s.notJoinedRoads.Last(), s.notJoinedRoads[0], true);
        }
        [Obsolete]
        void DefineInitialDistrict(Street r1, Street r2, bool draw = false)
        {
            //var road1InnerPoints = r1.GetPointsUntilInnerCircle();
            //var road2InnerPoints = r2.GetPointsUntilInnerCircle(false);
            //var innerCirclePoints = s.innerCircleStreet.points.TakeLesserRangeWrapped(r1.InnerCircleInters, r2.InnerCircleInters, false).Reversed();
            var streets = new List<Street>() { r1, s.innerCircleStreet, r2 };
            var d = new District(streets);
            s.InnerDistricts.Add(d);

            if (d.points.Count < 3)
                Debug.LogWarning($"District points: = {d.points.Count}");
        }


        void OrderRoadsAndGateByAngle()
        {
            s.mainRoads = s.mainRoads.OrderBy(r => s.innerCircleStreet.points.IndexOf(r.InnerCircleInters)).ToList();
            s.notJoinedRoads = s.notJoinedRoads.OrderBy(r => s.innerCircleStreet.points.IndexOf(r.InnerCircleInters)).ToList();
        }

        void RemoveDuplicatesOf(LineSegment ls, bool confirm0Duplicates = false)
        {
            allEdges = allEdges.OrderBy(e => e.Id).ToList();
            var duplicates = allEdges.Where(e => e.EdgeIds.Contains(ls.p0.Id) && e.EdgeIds.Contains(ls.p1.Id) && e.Id != ls.Id).ToList();

            if (confirm0Duplicates || duplicates.Any())
            {
                Debug.Log($"Removed {duplicates.Count} duplicates of edge ({ls.Id}):{ls.p0.Id} <-> {ls.p1.Id}");
            }
            if (duplicates.Any())
            {
                allEdges.RemoveList(duplicates);
            }
        }


        void RemoveDuplicates()
        {
            allEdges = allEdges.OrderBy(e => e.Id).ToList();
            for (int i = 0; i < allEdges.Count; i++)
            {
                RemoveDuplicatesOf(allEdges[i]);
            }
        }

        void GetCoveringLines(LineSegment ls)
        {
            for (int i = 0; i < allEdges.Count; i++)
            {
                if (allEdges[i].Id != ls.Id)
                {
                    var edgeI = allEdges[i];
                    var e1P0One2 = ParcelGenerator.IsPointOnSegment(edgeI.p0.pos, (Vector2)ls.p0.pos, (Vector2)ls.p1.pos);
                    var e1P1OnE2 = ParcelGenerator.IsPointOnSegment(edgeI.p1.pos, (Vector2)ls.p0.pos, (Vector2)ls.p1.pos);
                    var e2P0One1 = ParcelGenerator.IsPointOnSegment((Vector2)ls.p0.pos, edgeI.p0.pos, edgeI.p1.pos);
                    var e2P1OnE1 = ParcelGenerator.IsPointOnSegment((Vector2)ls.p1.pos, edgeI.p1.pos, edgeI.p1.pos);
                    if ((e1P0One2 && e1P1OnE2) || (e2P0One1 && e2P1OnE1))
                    {
                        
                    }
                }
            }

            
        }

        void GetCoveringLines(List<LineSegment> lines)
        {
            foreach(var line in lines)
            {
                GetCoveringLines(line);
            }
        }

        float EPSILON = 0.001f;
        public bool isPointOnLine(PtWSgmnts linePointA, PtWSgmnts linePointB, Point point)
        {
            float m = (linePointB.pos.y - linePointA.pos.y) / (linePointB.pos.x - linePointA.pos.x);
            float b = linePointA.pos.y - m * linePointA.pos.x;
            return Math.Abs(point.pos.y - (m * point.pos.x + b)) < EPSILON;
        }
        List<PtWSgmnts> lonelyPts = new();
        Dictionary<District, List<LineSegment>> edgesDict = new();
        Dictionary<District, List<PtWSgmnts>> nodeDict = new();
        List<PtWSgmnts> allNodes = new List<PtWSgmnts>();
        List<LineSegment> allEdges = new List<LineSegment>();

        List<PtWSgmnts> IdentifyNeighboursWIthtoutEdges(PtWSgmnts pt, Color color)
        {
            var existingEdges = allEdges.Where(e => e.EdgeIds.Contains(pt.Id)).ToList();
            var p1NeighbourdsWithEdges = existingEdges.SelectMany(e => e.EdgePoints).Distinct(new PointsComparer(true)).Where(p => p.Id != pt.Id).ToList();
            var PtsWithoutEdges = pt.Neighbours.Except(p1NeighbourdsWithEdges).ToList();

            //foreach (var loosePoint in PtsWithoutEdges)
            //{
            //    Debug.DrawRay(loosePoint.pos, Vector2.up, color);
            //}
            //foreach (var edge in existingEdges)
            //{
            //    break;
            //    Debug.DrawLine(edge.p0.pos, edge.p1.pos, color);
            //}
            //pt.Neighbours = pt.Neighbours.Except(PtsWithoutEdges).ToList();
            Debug.Log($"Removed {PtsWithoutEdges.Count} neighbours for point {pt.Id}. New neighbours count: {pt.Neighbours.Count}");
            return PtsWithoutEdges;
        }

         List<LineSegment> GetExistingEdgesOnly(PtWSgmnts pt, Color color)
        {
            var existingEdges = allEdges.Where(e => e.EdgeIds.Contains(pt.Id)).ToList();
            return existingEdges;
        }


        //2e9c1b25-90c6-4969-b4ad-7d891a15a2cf


        void AddInnerCircleEdges(List<LineSegment> edges, Street innerCircle)
        {

        }

        void SplitInnerCirleIntoBlocks()
        {
            intersections.Clear();
            duplicatedNodes.Clear();
            MissingEdges.Clear();
            facesExtracted.Clear();
            allEdges.Clear();
            allNodes.Clear();
            lonelyPts.Clear();
            deadEnds.Clear();
            LoseEdges.Clear();
            ZeroLenEdges.Clear();
            overlappingEdges.Clear();
            distVEdges.Clear();
            districtDiagrams.Clear();
            ptsInserts.Clear();
            closePoints.Clear();


            var innerCircle = s.innerCircleStreet;
            var center = s.innerCircleStreet.FindCenter();

            var rect = innerCircle.GetRectangleCircumscribedInPolygon(.1f);
            var pts = PoissonDiscSampler2D.GeneratePoints(rnd, MinBlockPtsRad, rect);

            Voronoi v = new Voronoi(pts, rect);
            var it = 100;
            while (v.DelaunayTriangulation().Count < 4 && it > 0)
            {
                if (rect.size == Vector2.zero)
                {
                    Debug.LogWarning("PoissonDisc rect size = 0!!!");
                    return;
                }
                v = new Voronoi(pts, rect);
                if (it-- < 0)
                {
                    pts = PoissonDiscSampler2D.GeneratePoints(rnd, MinBlockPtsRad, rect);
                    v = new Voronoi(pts, rect);
                }
            }
            districtDiagrams.Add(v);

            var edges = v.VoronoiDiagram();
            JoinSamePosPtsAndRemoveEmptyLines(edges);
            AddInnerCircleEdges(edges, innerCircle);
            


            //IdentifyDistrictIntersections();
            //RemoveOutsideEdges(d, edges);

            //JoinSamePosPtsAndRemoveEmptyLines(edges);
            //RemoveLoseEdges(d, edges);
            //WieldPtsThatAreTooClose(d, edges, center, minPtsDistToNoTWield);
        }


        void SplitDistrictsIntoBlocks()
        {
            intersections.Clear();
            duplicatedNodes.Clear();
            MissingEdges.Clear();
            facesExtracted.Clear();
            allEdges.Clear();
            allNodes.Clear();
            lonelyPts.Clear();
            deadEnds.Clear();
            LoseEdges.Clear();
            ZeroLenEdges.Clear();
            overlappingEdges.Clear();
            distVEdges.Clear();
            districtDiagrams.Clear();
            ptsInserts.Clear();
            closePoints.Clear();

            foreach (var d in s.InnerDistricts)
            {
                var center = d.FindCenter();
                var rect = d.GetRectangleCircumscribedInPolygon();
                var pts = PoissonDiscSampler2D.GeneratePoints(rnd, MinBlockPtsRad, rect);

                Voronoi v = new Voronoi(pts, rect);
                var it = 100;
                while (v.DelaunayTriangulation().Count < 4 && it > 0)
                {
                    if(rect.size == Vector2.zero)
                    {
                        Debug.LogWarning("PoissonDisc rect size = 0!!!");
                        return;
                    }
                    v = new Voronoi(pts, rect);
                    if(it-- < 0)
                    {
                        pts = PoissonDiscSampler2D.GeneratePoints(rnd, MinBlockPtsRad, rect);
                        v = new Voronoi(pts, rect);
                    }
                }
                districtDiagrams.Add(v);

                var edges = v.VoronoiDiagram();
                JoinSamePosPtsAndRemoveEmptyLines(edges);

                //IdentifyDistrictIntersections(d, edges, center);
                //RemoveOutsideEdges(s.OuterCircle, edges);

                //JoinSamePosPtsAndRemoveEmptyLines(edges);
                //RemoveLoseEdges(d, edges);
                //WieldPtsThatAreTooClose(d, edges, center, minPtsDistToNoTWield);
                edgesDict.Add(d, edges);
            }

            AddIdentifiedIntersectionsToStreets();

            foreach (var d in s.InnerDistricts)
            {
                d.RefreshPtsFromRoads();
                invalidTriangles.Clear();
                AddDistrictEdges(d, edgesDict[d], s.InnerDistricts.IndexOf(d));
                JoinSamePosPtsAndRemoveEmptyLines(edgesDict[d]);
                //GetRidOfSeparatePointsTooClose
                //ToEdges(edgesDict[d], minPtsDistToNoTWield);                
                RemoveEdgesWIthPointsOutside(d, edgesDict[d]);
                allEdges.AddRange(edgesDict[d]);
            }

            IdentifyCoveringEdges();
            JoinSamePosPtsAndRemoveEmptyLines(allEdges);
            RemoveDuplicates();



            {
                //foreach (var d in s.InnerDistricts)
                //{
                //    var newPts = d.CheckForDistrictEdgePoints(s);
                //    d.ReorderPointsByAngle();
                //}

                //foreach (var d in s.InnerDistricts)
                //{
                //    var allPoints = edgesDict[d].SelectMany(e => e.EdgePoints).ToList();
                //    var lonelyPtsTmp = allPoints
                //        .Where(p => allPoints.Count(p2 => Vector2.Distance(p.pos, p2.pos) < .2f) < 2)
                //        .Distinct(new PointsComparer())
                //        .ToList();
                //    AddDistrictEdges(d, edgesDict[d], s.InnerDistricts.IndexOf(d));
                //    edgesDict[d].ForEach(e => e.p0.AddNeighbour(e.p1));
                //    RemoveLoseEdges(d, edgesDict[d]);
                //    RemoveOutsideEdges(d, edgesDict[d]);
                //    allPoints = edgesDict[d].SelectMany(e => e.EdgePoints).ToList();
                //    lonelyPtsTmp = allPoints
                //        .Where(p => allPoints.Count(p2 => Vector2.Distance(p.pos, p2.pos) < .2f) < 2)
                //        .Distinct(new PointsComparer())
                //        .ToList();
                //    lonelyPts.AddRange(lonelyPtsTmp);
                //    foreach (var pt in d.points)
                //    {
                //        bool usedInEdge = edgesDict[d].Any(e => e.p0.Id == pt.Id || e.p1.Id == pt.Id);
                //        if (!usedInEdge)
                //        {
                //            Debug.LogWarning($"Point {pt.Id} is not used in any edge in district {d.Id}");
                //        }
                //    }
                //    if (lonelyPtsTmp.Any())
                //    {
                //        var foundPts = new List<bool>();
                //        foreach (var lPt in lonelyPtsTmp)
                //        {
                //            d.ContainsCheckpoint(lPt);
                //        }
                //    }                
                //}

                //missing edges: 19898a70-ecf9-4cf7-931b-8c28d99d6f09
                //invalid edges: dc3f8a70-7215-4977-b867-be9d98b213dc
            }
            var allDistEdges = new List<LineSegment>();
            //foreach (var d in s.InnerDistricts)
            //{
            //    nodeDict.Add(d, new List<PtWSgmnts>());
            //    foreach (var edge in edgesDict[d])
            //    {
            //        allDistEdges.Add(edge);
            //        if (!allNodes.Contains(edge.p0))
            //        {
            //            allNodes.Add(edge.p0);
            //        }
            //        if (!allNodes.Contains(edge.p1))
            //        {
            //            allNodes.Add(edge.p1);
            //        }

            //        if (!nodeDict[d].Contains(edge.p0))
            //        {
            //            nodeDict[d].Add(edge.p0);
            //        }
            //        if (!nodeDict[d].Contains(edge.p1))
            //        {
            //            nodeDict[d].Add(edge.p1);
            //        }
            //    }
            //}
            foreach (var edge in allEdges)
            {
                if (!allNodes.Contains(edge.p0))
                {
                    allNodes.Add(edge.p0);
                }
                if (!allNodes.Contains(edge.p1))
                {
                    allNodes.Add(edge.p1);
                }
            }

            

            var allNodesCount = allNodes.Count;
            allNodes = allNodes.Distinct(new PointsComparer(true)).ToList();
            Debug.Log($"All nodes count before: {allNodesCount}, after:{allNodes.Count}");
            var neighbourCount = allNodes.Select(n => n.Neighbours.Count).ToList();
            var avgNei = neighbourCount.Average();
            var min = neighbourCount.Min();
            var max = neighbourCount.Max();
            
            Debug.Log($"Neighbourds data: {avgNei}, {min}, {max}");
            var nodesWithNoNeightbours = allNodes.Where(n => n.Neighbours.Count <= 1).ToList();
            Debug.Log($"Lonely nodes: {nodesWithNoNeightbours.Count}");
            IdentifyCoveringEdges();
            var lonelyEdges = allEdges.Where(e => e.ContainsAnyEdgePointId(nodesWithNoNeightbours.ToArray())).ToList();
            //foreach (var lEdge in lonelyEdges)
            for(int i = 0; i < lonelyEdges.Count; i++)
            {
                var lEdge = lonelyEdges[i];
                Debug.Log(lEdge.p0.Neighbours.Count);
                Debug.Log(lEdge.p1.Neighbours.Count);
                //Debug.DrawLine(lEdge.p0.pos, lEdge.p1.pos, Color.red);
                Debug.Log($"Deleting edge at {i}/{lonelyEdges.Count - 1}");
                lEdge.p0.AddNeighbours(lEdge.p1);

                //allEdges.Remove(lEdge);
            }                        


            var strEdge = allEdges.FirstOrDefault(e => e.EdgePoints.Select(e => e.Id).Contains(301) && e.EdgePoints.Select(e => e.Id).Contains(302));
            var removedEdges = new List<LineSegment>();
            if (strEdge != null)
            {                
                Debug.LogWarning($"GOOOOOT IT:D!!!!: id = {strEdge.Id} p0 neigh: {strEdge.p0.Neighbours.Count}, p1 neigh: {strEdge.p1.Neighbours.Count}, p0.id ={strEdge.p0.Id}, p1.id = {strEdge.p1.Id}");    
                var shift = new Vector2(.2f,  .2f);
                //foreach(var neigh in strEdge.p0.Neighbours)
                //{
                //    Debug.DrawLine(strEdge.p0.pos + shift, neigh.pos + shift, Color.magenta);
                //}

                Debug.DrawRay(strEdge.p1.pos, Vector2.up * 3, Color.red);
                Color[] colors = new Color[] { Color.red, Color.green, Color.gray, Color.yellow, Color.black, Color.magenta };
                int index = 0;


                //shift = new Vector2(-.2f, -.2f);
                var p1Edges = GetExistingEdgesOnly(strEdge.p1, Color.yellow);
                var ptsNoEdges = IdentifyNeighboursWIthtoutEdges(strEdge.p1, Color.yellow);
                //Debug.DrawRay(strEdge.p1.pos, Vector2.up * 3, Color.black);

                foreach (var neigh in ptsNoEdges)
                {
                    Debug.DrawLine(strEdge.p1.pos + shift, neigh.pos + shift, colors[index++]);
                    shift += new Vector2(.1f, .1f);
                }

                //var p1NeighbourdsWithoutEdge = p1Edges.SelectMany(e => e.EdgePoints).Distinct(new PointsComparer(true)).Where(p => p.Id != strEdge.p1.Id).ToList();
                //strEdge.p1.Neighbours = strEdge.p1.Neighbours.Except(p1NeighbourdsWithoutEdge).ToList();

                //Debug.Log($"{p1Edges[2].EdgeIds.Join(',')}, {p1Edges[3].EdgeIds.Join(',')}"); 
                index = 0;
                foreach (var neigh in p1Edges)
                {
                    if (index == 0)
                    {
                        Debug.Log($"Point 0 (red) in innercircle for edge id {neigh.Id}: " + s.innerCircleStreet.ContainsPoint(neigh.TheOtherPoint(strEdge.p1)));
                    }
                    if (index == 1)
                    {
                        //Remove not existing neighbours
                        var theOtherPt = neigh.TheOtherPoint(strEdge.p1); 
                        var existingEdges = GetExistingEdgesOnly(theOtherPt, Color.red);

                        var testedEdge = existingEdges[2];
                        var pointsOnEdge = allNodes.Where(n => n.Id != testedEdge.Id && ParcelGenerator.IsPointOnSegment(n.pos, testedEdge.p0.pos, testedEdge.p1.pos) && !testedEdge.EdgeIds.Contains(n.Id)).ToList();
                        if(pointsOnEdge.Any())
                        {                            
                            Debug.Log($"POints on EDGE {testedEdge.Id}!!!: {pointsOnEdge.Select(p => p.Id).Join(',')}");
                            //Debug.DrawLine(testedEdge.p0.pos, testedEdge.p1.pos, Color.green);
                        }

                        foreach(var edge in existingEdges)
                        {
                            //Debug.DrawLine(edge.p0.pos, edge.p1.pos, Color.red);
                        }

                    }
                    if (index == 2)
                    {
                        RemoveDuplicatesOf(neigh, true);
                    }
                    var color = colors[index];
                    index = (index + 1) % colors.Length;
                    Debug.Log($"Edge {neigh.Id}, color {index}, length: {neigh.CalculateLength()}");
                    //Debug.DrawLine(strEdge.p1.pos + shift, neigh.TheOtherPoint(strEdge.p1).pos + shift, color);
                    shift += new Vector2(-.2f, -.2f);
                }
                Debug.Log(string.Join(',', p1Edges.Select(e => e.Id)));

                shift = new Vector2(.1f, .1f);
                //Debug.DrawLine(strEdge.p0.pos, strEdge.p1.pos, Color.cyan);
                //allEdges.Remove(strEdge);
                Debug.LogWarning($"EEEEEND:D!!!!: id = {strEdge.Id} p0 neigh: {strEdge.p0.Neighbours.Count}, p1 neigh: {strEdge.p1.Neighbours.Count}, p0.id ={strEdge.p0.Id}, p1.id = {strEdge.p1.Id}");
            }

            
            var sh = new Vector2(.3f,  .3f);
            //Debug.DrawLine(strEdge.p0.pos + sh, strEdge.p1.pos + sh, Color.cyan);
            foreach (var removedEdge in removedEdges)
            {
                //Debug.DrawLine(removedEdge.p0.pos + sh, removedEdge.p1.pos + sh, Color.red);
            }

            ReportSummary();
            {
                //kupa zbêdnych krawêdzi: 2d2f1dbb-4797-4913-b10a-2d2eb95d7f7a
                DetectGraphCycles2.AnalyzeEdgeSanity(ref allEdges);                
                //DetectGraphCycles2.ValidateCycle = true;
                //rawCycles = DetectGraphCycles.FindMinimalCycles(allEdges);
                //allCycles = DetectGraphCycles.TrySplitInvalidCycles(allEdges, rawCycles);
                //Debug.Log($"Liczba cykli: {allCycles.Count}, surowych: {rawCycles.Count}");
                //if (allCycles.Any() || rawCycles.Any())
                //{
                //    InvalidEdges = DetectGraphCycles2.AnalyzeInvalidEdges(allEdges, allCycles);
                //    Debug.LogWarning($"Invalid edges: {InvalidEdges.Count}");

                //    var nodes = allNodes.Distinct(new PointsComparer(true)).ToList();
                //    var diffs = allNodes.Except(nodes).ToList();

                //    if (nodes.Count < allNodes.Count)
                //    {
                //        Debug.LogWarning($"Duplicated node positions found: {diffs.Count}: {string.Join(',', diffs.Select(d => d.Id))}");
                //    }

                //    var shift = new Vector2(.2f, .2f);
                //    foreach (var diff in diffs)
                //    {
                //        Debug.DrawRay(diff.pos, Vector2.up + shift, Color.magenta);
                //        shift += new Vector2(.2f, .2f);
                //    }
                //}
            }
            //facesExtracted = FaceExtractor3.ExtractMinimalCoveringFacesIterative(allEdges, 50);
            //facesExtracted = FaceExtractor2.ExtractFaces(allEdges);
            facesExtracted = FaceExtractor.ExtractMinimalCoveringFacesIterative(allEdges, 50);
            Debug.Log($"Faces extracted: {facesExtracted.Count}");
            var missingEdgesStr = DetectGraphCycles2.AnalyzeCycleCoverage(allEdges, facesExtracted, "FACES");
        }

        List<PtWSgmnts> allPtsIdMatters = new();
        List<PtWSgmnts> allPtsIdNotMatters = new();
        List<Tuple<LineSegment, LineSegment>> coveringSegments = new();
        public void IdentifyCoveringEdges()
        {
            int zeroLen = 0;
            int idMatch = 0;
            int posMatch = 0;
            int edgesRemoved = 0;
            do
            {
                edgesRemoved = 0;
                for (int i = 0; i < allEdges.Count; i++)
                {
                    for (int j = 0; j < allEdges.Count; j++)
                    {
                        if (i != j)
                        {
                            var edgeI = allEdges[i];
                            var edgeJ = allEdges[j];
                            if (AreSegmentsParallel(edgeI, edgeJ))
                            {
                                var e1P0One2 = ParcelGenerator.IsPointOnSegment(edgeI.p0.pos, edgeJ.p0.pos, edgeJ.p1.pos);
                                var e1P1OnE2 = ParcelGenerator.IsPointOnSegment(edgeI.p1.pos, edgeJ.p0.pos, edgeJ.p1.pos);
                                var e2P0One1 = ParcelGenerator.IsPointOnSegment(edgeJ.p0.pos, edgeI.p0.pos, edgeI.p1.pos);
                                var e2P1OnE1 = ParcelGenerator.IsPointOnSegment(edgeJ.p1.pos, edgeI.p1.pos, edgeI.p1.pos);

                                if ((e1P0One2 && e1P1OnE2) || (e2P0One1 && e2P1OnE1))
                                {
                                    var ptIdMatch = edgeI.ContainsAnyEdgePointId(edgeJ.EdgePoints);
                                    var ptPosMatch = edgeI.EdgePoints.Where(ep => (ep.Id != edgeJ.p0.Id && ep.pos.DistanceTo(edgeJ.p0.pos) < EPSILON)
                                                || (ep.Id != edgeJ.p1.Id && ep.pos.DistanceTo(edgeJ.p1.pos) < EPSILON)).ToList();
                                   
                                    if (edgeI.CalculateLength() < EPSILON && edgeJ.CalculateLength() > EPSILON)
                                    {
                                        edgesRemoved++;
                                        allEdges.Remove(edgeI);
                                        i--;
                                    }
                                    else if (edgeJ.CalculateLength() < EPSILON && edgeI.CalculateLength() > EPSILON)
                                    {
                                        edgesRemoved++;
                                        allEdges.Remove(edgeJ);
                                        j--;
                                    }
                                    else
                                    {
                                       
                                        if (ptPosMatch.Any())
                                        {
                                            posMatch++;
                                        }

                                        if (ptIdMatch)
                                        {
                                            idMatch++;
                                        }

                                        coveringSegments.Add(new Tuple<LineSegment, LineSegment>(edgeI, edgeJ));
                                    }
                                }
                            }
                        }
                    }
                }
                var lengths = coveringSegments.Select(c => c.Item1.CalculateLength().ToString() + " " + c.Item2.CalculateLength());
                Debug.LogWarning($"Covering segments found {coveringSegments.Count}. Lengths = {string.Join('-', lengths)}. idMatch = {idMatch}, posmatch = {posMatch}, zerolen = {zeroLen}, Edges removed: {edgesRemoved}");
            }
            while (edgesRemoved > 0);
        }

        public static bool AreSegmentsParallel(LineSegment l1, LineSegment l2, float tolerance = 1e-6f)
        {
            return AreSegmentsParallel(l1.p0.pos, l1.p1.pos, l2.p0.pos, l2.p1.pos, tolerance);
        }

        public static bool AreSegmentsParallel(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, float tolerance = 1e-6f)
        {
            Vector2 dir1 = a2 - a1;
            Vector2 dir2 = b2 - b1;

            float cross = dir1.x * dir2.y - dir1.y * dir2.x;
            return Mathf.Abs(cross) < tolerance;
        }

        void ReportSummary()
        {
            var message = $"//////////////////=> Close points found {closePoints.Count}";
            Debug.Log(message);
        }

        List<LineSegment[]> invalidTriangles = new List<LineSegment[]>();
        List<LineSegment[]> height0Triangles = new List<LineSegment[]>();

        List<Tuple<LineSegment, PtWSgmnts>> closePoints = new List<Tuple<LineSegment, PtWSgmnts>>();
        void GetRidOfSeparatePointsTooCloseToEdges(List<LineSegment> edges, float minTriangleHeight)
        {
            var allPts = edges.SelectMany(e => e.EdgePoints).Distinct(new PointsComparer(true)).ToList();
            foreach (var pt in allPts)
            {
                var edgesByDist = edges.Where(e => !e.ContainsAnyEdgePointId(pt)).OrderBy(e => ParcelGenerator.PerpendicularDistance(e, pt));
                foreach(var closeEdge in edgesByDist)
                {
                    var distToP0 = closeEdge.p0.DistanceTo(pt);
                    var distToP1 = closeEdge.p1.DistanceTo(pt);                    
                    var totalDists = distToP0 + distToP1;
                    var edgeLen = closeEdge.CalculateLength();
                    var dist = ParcelGenerator.PerpendicularDistance(closeEdge, pt);
                    if (dist < minTriangleHeight && totalDists < edgeLen && distToP0 < edgeLen && distToP0 < edgeLen)
                    {
                        closePoints.Add(new Tuple<LineSegment, PtWSgmnts>(closeEdge, pt));
                    }
                }
            }
        }
        
        void GetRidOfFlatTrianglesNotCool(District d, float minTriangleHeight)
        {
            invalidTriangles.Clear();
            height0Triangles.Clear();
            var edges = edgesDict[d];
            var anyTriangles = edges.Where(e => e.p0.Neighbours.Any(n => n.Neighbours.Contains(e.p0)) || e.p1.Neighbours.Any(n => n.Neighbours.Contains(e.p1))).ToList();
            List<Tuple<LineSegment, LineSegment, LineSegment>> triangles = new();
            var lenList = new List<float>();
            var totalLens = new List<float>();
            foreach (var e in anyTriangles)
            {
                var e2 = edges.FirstOrDefault(ed => ed.ContainsAnyEdgePointId(e.p0, e.p1));
                var e3 = edges.FirstOrDefault(ed => ed.ContainsAnyEdgePointId(e.p0, e.p1) && ed.ContainsAnyEdgePointId(e2.p1, e2.p0));

                var triangleEdges = new List<LineSegment>();
                if(e2 != null && e3 != null)
                {
                    triangleEdges.Add(e);
                    triangleEdges.Add(e2);
                    triangleEdges.Add(e3);

                    for (int i = 0; i < triangleEdges.Count; i++)
                    {
                        var edgeI = triangleEdges[i];
                        var edgeJ = triangleEdges.Neighbour(i, 1);
                        var edgeK = triangleEdges.Neighbour(i, 2);
                        var pt = edgeJ.EdgePoints.FirstOrDefault(p => edgeK.ContainsEdgePointId(p));
                        var peprInters = ParcelGenerator.GetPerpendicularIntersection(edgeI.p0.pos, edgeI.p1.pos, pt.pos);
                        var height = (peprInters - pt.pos).magnitude;
                        lenList.Add((float)Math.Round(height, 2));
                        totalLens.Add(edgeI.CalculateLength() + edgeJ.CalculateLength() + edgeK.CalculateLength());
                        if (height < minTriangleHeight)
                        {
                            var possibleToRemove = triangleEdges.Where(e => e.Removable).ToList();
                            if(possibleToRemove.Any())
                            {
                                var toRemove = possibleToRemove.GetRandom(rnd);
                                edgesDict[d].Remove(toRemove);
                                invalidTriangles.Add(triangleEdges.ToArray());
                            }
                            else if(height == 0)
                            {
                                height0Triangles.Add(triangleEdges.ToArray());
                                Debug.LogWarning($"Cannot remove triangle with edges {edgeI.Id}, {edgeJ.Id}, {edgeK.Id} because no removable edge found.(TotalLen = {totalLens.Last()})");
                                //RemoveANY EDGEFROM EMPTYTRIANGLE
                            }
                            else
                            {
                                invalidTriangles.Add(triangleEdges.ToArray());
                                Debug.LogWarning($"Cannot remove triangle with edges {edgeI.Id}, {edgeJ.Id}, {edgeK.Id} because no removable edge found.(Height = {height})");
                            }
                            triangles.Add(new Tuple<LineSegment, LineSegment, LineSegment>(edgeI, edgeJ, edgeK));
                            break;
                        }
                    }
                }

            }
            Debug.LogWarning($"Triangles removed: {triangles.Count}");
            Debug.LogWarning($"Heights LenList: { string.Join('_', lenList.OrderBy(l => l))}");
            Debug.LogWarning($"Triangle lens: {string.Join('_', totalLens.OrderBy(l => l))}");
        }

        void AddIdentifiedIntersectionsToStreets()
        {
            int count = 0;
            s.notJoinedRoads.Select(r => r.CalculateLength());
            foreach (var inters in intersections)
            {
                foreach (var street in s.notJoinedRoads)
                {
                    if (isPointOnLine(street.p0, street.p1, inters) && !street.ContainsCheckpoint(inters))
                    {
                        for (int i = 0; i < street.points.Count - 1; i++)
                        {
                            var curr = street.points[i];
                            var next = street.points[i + 1];
                            if (isPointOnLine(curr, next, inters))
                            {
                                street.InsertCheckpoint(inters, i + 1);
                                //Debug.Log("Intersection point added to street!!!!");
                                count++;
                                break;
                            }
                        }
                    }
                }
            }

            Debug.Log($"Intersections added: {count}");
        }
        int RemoveLoseEdges(District d, List<LineSegment> edges)
        {
            var allPts = edges.SelectMany(e => e.EdgePoints).ToList();
            var loseEdges = edges.Where(e => allPts.Count(s => s.Id == e.p0.Id) == 1 && allPts.Count(s => s.Id == e.p1.Id) == 1).ToList();
            loseEdges = edges.Where(e => e.p0.Neighbours.Count == 1 || e.p1.Neighbours.Count == 1).ToList();
            
            if(loseEdges.Any(e => !e.Removable))
            {
                Debug.LogError("Non removable edges are lose");
            }

            edges.RemoveList(loseEdges);
            if (loseEdges.Any())
            {
                this.LoseEdges.AddRange(loseEdges);
                Debug.Log($"Removed {loseEdges.Count} lose edges");
            }
            return loseEdges.Count;
        }
        

        //int RemoveOutsideEdges(List<PtWSgmnts> limit, List<LineSegment> edges)
        //{
        //    var allPts = edges.SelectMany(e => e.EdgePoints).ToList();
        //    var edgesOutside = edges.Where(e => e.Removable).Where(e => !d.ContainsPoint(e.p0) && !d.ContainsPoint(e.p1)).ToList();
        //    edges.RemoveList(edgesOutside);
        //    if (edgesOutside.Any())
        //    {
        //        this.LoseEdges.AddRange(edgesOutside);
        //        Debug.Log($"Removed {edgesOutside.Count} lose edges");
        //    }            
        //    return edgesOutside.Count;
        //}
        void RemoveEdgesWIthPointsOutside(District d, List<LineSegment> edges)
        {
            var allPts = edges.SelectMany(e => e.EdgePoints).ToList();
            var edgesOutside = edges.Where(e => !d.ContainsPoint(e.p0) || !d.ContainsPoint(e.p1)).ToList();
            int initCount = edges.Count;
            edges.RemoveList(edgesOutside);
            if(edgesOutside.Any())
                Debug.Log($"Removed edges with points outside: {edgesOutside.Count}");

        }
        int RemoveDeadPoints(District d, List<LineSegment> edges)
        {
            var allPts = edges.SelectMany(e => e.EdgePoints).ToList();
            var loseEdgesList = edges.Where(e => allPts.Count(s => s.Id == e.p0.Id) == 1 || allPts.Count(s => s.Id == e.p1.Id) == 1).ToList();
            //edges.RemoveList(loseEdgesList);

            if (loseEdgesList.Any())
            {
                this.deadEnds.AddRange(loseEdgesList);
                Debug.Log($"Removed {loseEdgesList.Count} lose edges");
            }
            return loseEdgesList.Count;
        }
        int RemoveZeroLenEdges(List<LineSegment> edges)
        {
            //foreach (var edge in distVEdges)
            int count = 0;
            var zeroEdges = new List<LineSegment>();
            for(int i = 0; i < edges.Count; i++)
            {
                if (edges[i].CalculateLength() == 0)
                {
                    zeroEdges.Add(edges[i]);
                    count++;
                }
                if (edges[i].Length < MinEdgeLength)
                {
                    zeroEdges.Add(edges[i]);
                }
            }

            if(count > 0)
            {
                Debug.LogWarning($"Removed {count} legdes of len 0");
                ZeroLenEdges.AddRange(zeroEdges);
                edges.RemoveList(zeroEdges);
            }

            var lens = edges.Select(e => Math.Round(e.Length, 2)).OrderBy(e => e).ToList();
            Debug.Log($"Edges lengths: {string.Join(' ', lens)}");
            return count;
        }
        int JoinSamePosPtsAndRemoveEmptyLines(List<LineSegment> edges)
        {
            int count = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = 0; j < edges.Count; j++)
                {
                    if (i != j)
                    {
                        count += edges[j].ReplaceEdgePointWithSamePos(edges[i]);
                    }
                }
            }
            int count2 = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].CalculateLength() == 0)
                {
                    edges.RemoveAt(i--);
                    count2++;
                }
            }

            if (count > 0)
                Debug.Log($"______________> For {edges.Count} edges merged {count} points with same coords");
            if (count2 > 0)
                Debug.Log($"______________> For {edges.Count} edges removed {count2} with 0 length");
            return count;
        }
        int WieldPtsThatAreTooClose(District d, List<LineSegment> edges, Vector2 center, float minDist = 1f)
        {
            int count = 0;

            var removedMainRoadPts = 0;
            foreach (var str in d.bStr)
            {
                for (int i = 0; i < str.points.Count - 1; i++)
                {
                    var ptI = str.points[i];
                    var ptNext = str.points[i + 1];
                    if (ptI.DistanceTo(ptNext) < minDist)
                    {
                        var newPos = Vector2.Lerp(ptI.pos, ptNext.pos, .5f);
                        ptI.pos = newPos;
                        ptNext.pos = newPos;
                        str.points.Remove(ptI);
                        i--;
                        removedMainRoadPts++;
                    }
                }
            }
            //Debug.Log($"Removed main road points: {removedMainRoadPts}");
            //node level 2 with 180 deg angle
            //street nodes with dist< 1

            if (count > 0)
            {
                Debug.Log($"Corrected {count} edge points that were too close");
            }
            return count;
        }
        //seed: 68e7c8ae-9808-4335-ab18-2a4de60b7804
        void AddDistrictEdges(District d, List<LineSegment> edges, int dIndex)
        {
            for (int i = 0; i < d.points.Count - 1; i++)
            {
                edges.Add(new LineSegment(d.points[i], d.points[i + 1]));
                edges.Last().Removable = false;
            }
            if (d.points[^1].Id != d.points[0].Id)
            {
                var edge = new LineSegment(d.points[^1], d.points[0]);
                if(edge.CalculateLength() > 0)
                {
                    edges.Add(edge);
                    edges.Last().Removable = false;
                }
            }

            var edgePts = new HashSet<PtWSgmnts>(edges.SelectMany(e => new[] { e.p0, e.p1 }));
            var missingPts = d.points.Where(p => !edges.Any(e => e.EdgePoints.Contains(p))).ToList();
            if (missingPts.Any())
            {
                Debug.LogWarning($"Missing edge for points: {string.Join(", ", missingPts.Select(p => p.Id))}");
            }
        }

        List<PtWSgmnts> ptsInserts = new();
        void AddVoronoisOutputToLists()
        {
            spanningTree.Clear();
            triangles.Clear();
            rects.Clear();
            foreach(var v in districtDiagrams)
            {
                spanningTree.AddRange(v.SpanningTree());
                triangles.AddRange(v.DelaunayTriangulation());
                rects.Add(v.plotBounds);
            }
        }

        List<Voronoi> districtDiagrams = new();
        List<LineSegment> InvalidEdges = new();
        List<LineSegment> MissingEdges = new();
        List<PtWSgmnts> intersections = new();

        List<PtWSgmnts> duplicatedNodes = new();
        List<List<PtWSgmnts>> rawCycles = new();
        List<List<PtWSgmnts>> allCycles = new();
        List<List<PtWSgmnts>> facesExtracted = new();
        List<LineSegment> shortenedEdges = new();
        List<LineSegment> deadEnds = new();
        List<LineSegment> LoseEdges = new();
        List<LineSegment> overlappingEdges = new();
        List<LineSegment> distVEdges = new();
        List<LineSegment> spanningTree = new();
        List<LineSegment> triangles = new();
        List<LineSegment> ZeroLenEdges = new();
        List<Rect> rects = new();

        private void DrawFace(List<PtWSgmnts> face, Color color, Vector2? shift = null)
        {
            Gizmos.color = color;
            var shiftVal = shift.HasValue ? shift.Value : Vector2.zero;
            for (int i = 0; i < face.Count; i++)
            {
                var a = face[i];
                var b = face[(i + 1) % face.Count];
                Gizmos.DrawLine(a.pos + shiftVal, b.pos + shiftVal);
            }
        }

        private void DrawNeighbours(PtWSgmnts node, int index, float scale = 0.2f)
        {
            var shift = new Vector2(-50, 50);
            Vector2 center = node.pos;

            for (int i = 0; i < node.Neighbours.Count; i++)
            {
                var n = node.Neighbours[i];
                Gizmos.color = Color.HSVToRGB((float)i / node.Neighbours.Count, 1f, 1f);
                Gizmos.DrawLine(center + shift, n.pos + shift);

                // Opcjonalnie – dodaj znacznik kierunku
                Vector2 dir = (n.pos - center).normalized * scale;
                Gizmos.DrawSphere(center + dir + shift, 0.02f);
            }

            // Narysuj punkt centralny
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(center, 0.03f);
        }

        int currClosePt = 0;
        public void NextCloseTriangle()
        {
            currClosePt = (currClosePt + 1) % closePoints.Count;
        }

        public void PrevCloseTriangle()
        {
            currClosePt = (currClosePt - 1 + closePoints.Count) % closePoints.Count;
        }
        public void OnDrawGizmos()
        {
            //Gizmos.color = Color.green;
            var shift = new Vector2(0, -10);
            //GizmoDrawer.DrawSegments(allEdges, shift);

            shift = new Vector2(0, -51);
            for (int i = 0; i < facesExtracted.Count; i++)
            {
                Color c = Color.HSVToRGB((float)i / facesExtracted.Count, 1f, 1f);
                DrawFace(facesExtracted[i], c, shift);
            }

            shift = new Vector2(50, -51);
            for (int i = 0; i < facesExtracted.Count; i++)
            {
                Color c = Color.HSVToRGB((float)i / facesExtracted.Count, 1f, 1f);
                DrawFace(facesExtracted[i], c, shift);
                shift += new Vector2(5,-5);
            }

            shift = new Vector2(50, 100);
            for (int i = 0; i < allCycles.Count; i++)
            {
                Color c = Color.HSVToRGB((float)i / allCycles.Count, 1f, 1f);
                DrawFace(allCycles[i], c, shift);
            }

            shift =  new Vector2(50, 50);
            for (int i = 0; i < allCycles.Count; i++)
            {
                Color c = Color.HSVToRGB((float)i / allCycles.Count, 1f, 1f);
                DrawFace(allCycles[i], c, shift);
                shift += new Vector2(10, 0);
            }

            shift = new Vector2(50, 150);
            for (int i = 0; i < rawCycles.Count; i++)
            {
                Color c = Color.HSVToRGB((float)i / rawCycles.Count, 1f, 1f);
                DrawFace(rawCycles[i], c, shift);
                shift += new Vector2(10, 0);
            }

            shift = new Vector2(0, 50);
            for (int i = 0; i < allCycles.Count; i++)
            {
                Color c = Color.HSVToRGB((float)i / allCycles.Count, 1f, 1f);
                DrawFace(allCycles[i], c, shift);
            }

            Gizmos.color = Color.black;
            shift = new Vector2(50, 100);
            GizmoDrawer.DrawSegments(MissingEdges, shift, .2f);

            shift = new Vector2(50, 100);
            GizmoDrawer.DrawSegments(InvalidEdges, shift, .2f);

            if (DrawEdges)
            {
                if (DrawRemovedEdges)
                {
                    Gizmos.color = Color.red;
                    GizmoDrawer.DrawSegments(overlappingEdges);

                    Gizmos.color = Color.yellow;
                    GizmoDrawer.DrawSegments(LoseEdges, new Vector2(.2f, 0));

                    Gizmos.color = Color.magenta;
                    GizmoDrawer.DrawSegments(deadEnds, new Vector2(.1f, -.1f));

                    Gizmos.color = Color.cyan;
                    //GizmoDrawer.DrawSpheres(intersections, new Vector2(.1f, -.1f), .2f, .001f);

                    shift = new Vector2(30f, -.1f);
                    Gizmos.color = Color.black;
                    foreach (var tr in invalidTriangles)
                    {
                        GizmoDrawer.DrawSegments(tr.ToList(), shift, .4f);
                    }

                    Gizmos.color = Color.green;
                    foreach (var tr in height0Triangles)
                    {
                        GizmoDrawer.DrawSegments(tr.ToList(), shift, .4f);
                    }
                    //InvalidPoints
                }                
                Gizmos.color = Color.white;
                GizmoDrawer.DrawSegments(distVEdges, null,  .15f);

                Gizmos.color = Color.blue;
                //GizmoDrawer.DrawSegments(allEdges, null, .15f);
                shift = Vector2.zero;
                foreach (var edge in allEdges)
                {
                    if(edge.Draw)
                    {
                        Gizmos.color = Color.blue;
                    }                        
                    else
                    {
                        Gizmos.color = Color.red;
                    }
                    GizmoDrawer.DrawSegment(edge, shift);
                    GizmoDrawer.DrawSpheres(edge.EdgePoints.ToList(), shift, .15f);
                    //shift += new Vector2(0.01f, 0.01f);
                }

                //Gizmos.color = Color.red;
                //GizmoDrawer.DrawSpheres(intersections, 0.15f);
                foreach (var emptyEdge in ZeroLenEdges)
                {
                    Gizmos.color = Color.black;
                    Gizmos.DrawSphere(emptyEdge.p0.pos + new Vector2(-.1f, -.1f), .1f);
                    Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(emptyEdge.p1.pos + new Vector2(.1f, .1f), .1f);
                }

                Gizmos.color = Color.green;
                foreach(var point in lonelyPts)
                {
                    Gizmos.DrawSphere(point.pos, .3f);
                }

                
                shift = new Vector2(-.1f, .1f);
                foreach(var pair in coveringSegments)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(pair.Item2.p0.pos + shift, pair.Item2.p1.pos + shift);

                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(pair.Item1.p0.pos - shift, pair.Item1.p1.pos + shift);
                    if(pair.Item1.Length < 0.001f)
                    {
                        Debug.DrawRay(pair.Item1.p0.pos, Vector2.up * 4);
                    }

                    if (pair.Item2.Length < 0.001f)
                    {
                        Debug.DrawRay(pair.Item2.p0.pos, Vector2.up * 4);
                    }
                }

                Color[] colors1 = new Color[] { Color.red, Color.yellow, Color.green};
                float[] sizes = new float[] { .1f, .05f, .025f };
                foreach (var pt in allPtsIdMatters)
                {
                    break;
                    var indx = Math.Min(pt.Neighbours.Count, 2);
                    Gizmos.color = colors1[indx];
                    if(indx < 2)
                        Gizmos.DrawSphere(pt.pos, sizes[indx]);
                }
            }

            Gizmos.color = Color.black;

            //foreach(var pair in closePoints)
            {
                //GizmoDrawer.DrawSegment(pair.Item1.p0, pair.Item1.p1);
                //Gizmos.DrawSphere(pair.Item2.pos, .3f);
                if(closePoints.Any())
                {
                    GizmoDrawer.DrawSegment(closePoints[currClosePt].Item1.p0, closePoints[currClosePt].Item1.p1);
                    Gizmos.DrawSphere(closePoints[currClosePt].Item2.pos, .4f);
                }
            }


            if (DrawTriangles)
            {
                Gizmos.color = Color.black;
                GizmoDrawer.DrawSegments(triangles);
            }

            if (DrawSpanningTree)
            {
                Gizmos.color = Color.yellow;
                foreach (var line in spanningTree)
                {
                    GizmoDrawer.DrawSegment(line);
                }
            }
            shift = new Vector2(.1f, -.1f);
            var colors = new Color[] { Color.yellow, Color.red, Color.green, Color.blue, Color.magenta, Color.cyan };

            var index = 0;
            if (DrawBoundaries)
            {
                foreach (var rect in rects)
                {
                    Gizmos.color = colors[index++];
                    Gizmos.DrawLine(new Vector2(rect.x, rect.y) + shift, new Vector2(rect.x, rect.yMax) + shift);
                    Gizmos.DrawLine(new Vector2(rect.x, rect.yMax) + shift, new Vector2(rect.xMax, rect.yMax) + shift);
                    Gizmos.DrawLine(new Vector2(rect.xMax, rect.yMax) + shift, new Vector2(rect.xMax, rect.y) + shift);
                    Gizmos.DrawLine(new Vector2(rect.xMax, rect.y) + shift, new Vector2(rect.x, rect.y) + shift);
                    //shift.x -= .1f;
                    //shift.y += .1f;
                }
            }

            if(DrawMainRoads)
            {
                shift = new Vector2(.2f,  .2f);
                shift = Vector2.zero;
                index = 0;
                foreach (var road in s.mainRoads)
                {
                    Gizmos.color = colors.ToList().Neighbour(index++, 0);
                    GizmoDrawer.DrawVectorList(road.points, shift, false,  .25f);
                    //shift += new Vector2(.1f,  .1f);
                }                
                Gizmos.color = colors.ToList().Neighbour(index++, 0);
                GizmoDrawer.DrawVectorList(s.innerCircleStreet.points, shift, true, .1f);
            }
            

            
            if (DrawDistricts)
            {
                Gizmos.color = Color.white;
                foreach (var d in s.InnerDistricts)
                {
                    GizmoDrawer.DrawVectorList(d.points, shift, true,  .2f);
                    if (edgesDict.ContainsKey(d))
                        GizmoDrawer.DrawSegments(edgesDict[d], shift,   .15f);
                }
                
                Gizmos.color = Color.green;
                shift = new Vector2(.2f,  .2f);
               
                index = 0;
                Gizmos.color = Color.white;
                foreach (var d in s.InnerDistricts)
                {
                    shift += new Vector2(35, 0.1f);
                    GizmoDrawer.DrawVectorList(d.points, shift, true,  .2f);
                    if (edgesDict.ContainsKey(d))
                        GizmoDrawer.DrawSegments(edgesDict[d], shift, .15f);
                    if(index++ == 1)
                    {
                        shift += new Vector2(-70, -35f);
                    }
                }                
            }

            if (DrawBlocks)
            {
                index = 0;
                {
                    shift = new Vector2(0.1f, 100.1f);
                    foreach(var d in s.InnerDistricts)
                    {
                        foreach (var b in d.Blocks)
                        {
                            Gizmos.color = colors[index];
                            index = index.WrapIndex(1, colors.ToList());
                            GizmoDrawer.DrawVectorList(b.points, shift, true,  .2f);
                            //shift += new Vector2(11.1f, 0f);
                        }
                        //shift += new Vector2(.1f, -55f);
                    }

                    shift = new Vector2(50.1f, 100.1f);
                    //foreach (var d in s.InnerDistricts)
                    {
                        foreach (var b in s.Blocks)
                        {
                            Gizmos.color = colors[index];
                            index = index.WrapIndex(1, colors.ToList());
                            GizmoDrawer.DrawVectorList(b.points, shift, true, .2f);
                            //shift += new Vector2(11.1f, 0f);
                        }
                        //shift += new Vector2(.1f, -55f);
                    }

                }
            }
        }

        internal static void ClearSeeds()
        {
            seedValues.Clear();
        }
    }
}