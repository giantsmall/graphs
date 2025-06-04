using Assets.Game.Scripts.Editors;
using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Utility;
using Delaunay;
using NUnit.Framework;
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

    public class RoadGraphGenChaos : MonoBehaviour
    {
        static List<int> seedValues = new List<int>();
        static int currentSeed = 0;
        public bool DrawTriangles = true;
        public bool DrawSpanningTree = true;
        public bool DrawEdges = true;
        public bool DrawRemovedEdges = true;
        public bool DrawBoundaries = true;
        public bool DrawDistricts = true;
        public bool DrawBlocks = true;
        public bool DrawLots = true;
        public float MinEdgeLength = 2f;
        public float MinBlockPtsRad = 8f;

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
            if(seedValues.Any())
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

            if(!seedValues.Any())
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
            ClearLog();
            Population = 600;

            var center = new Vector2(MapSize / 2f, MapSize / 2f);
            center += new Vector2(rnd.NextFloat(-5, 5), rnd.NextFloat(-5, 5));
            s = new SettlementModel(new PtWSgmnts(center));
            CreateMainRoadsAndInitiallyMerge();
            CreateInnerCircle();
            AddRoadToFillGaps();
            OrderRoadsAndGateByAngle();

            DefineDistrictsWithinInnerCircle();
            SplitDistrictsIntoBlocks();

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

            //first road join
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
        public static float maxStreetLen = 3f;

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
            s.innerCircleStreet = new (pts.Select(p => new PtWSgmnts(p)).ToList());

            foreach(var road in s.notJoinedRoads)
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

            if(insertIndex < 0)
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

        void DefineDistrictsWithinInnerCircle()
        {
            var gates = s.wall.gates;
            for (int i = 0; i < s.notJoinedRoads.Count - 1; i++)
            {
                DefineInitialDistrict(s.notJoinedRoads[i], s.notJoinedRoads[i + 1]);
            }
            DefineInitialDistrict(s.notJoinedRoads.Last(), s.notJoinedRoads[0]);
        }

        void DefineInitialDistrict(Street r1, Street r2)            
        {
            //center needs to be duplicated for r1 and r2 to be able to get intersections
            var road1InnerPoints = r1.GetPointsUntilInnerCircle();
            var road2InnerPoints = r2.GetPointsUntilInnerCircle();

            var innerCirclePoints = s.innerCircleStreet.points.TakeLesserRangeWrapped(r1.InnerCircleInters, r2.InnerCircleInters, false);

            var list = innerCirclePoints.Where(p => p.IntersectsWIthMainRoad).ToList();

            var streets = new List<Street>() { r1, s.innerCircleStreet, r2};
            var d = new District(streets, road1InnerPoints, innerCirclePoints, road2InnerPoints.Reversed());

            var dupPts = d.points.Where(p => d.points.Count(dp => dp.Id == p.Id) > 1).ToList();
            dupPts = d.points.Where(p => d.points.Count(dp => dp.pos == p.pos) > 1).ToList();
            if (dupPts.Count > 0)
            {
                var r1Contains = r1.ContainsCheckpoint(dupPts[0]);
                var r2Contains = r1.ContainsCheckpoint(dupPts[0]);
                var innerCont = s.innerCircleStreet.ContainsCheckpoint(dupPts[0]);
            }
            s.InnerDistricts.Add(d);
        }

        void OrderRoadsAndGateByAngle()
        {
            s.mainRoads = s.mainRoads.OrderBy(r => s.innerCircleStreet.points.IndexOf(r.InnerCircleInters)).ToList();
            s.notJoinedRoads = s.notJoinedRoads.OrderBy(r => s.innerCircleStreet.points.IndexOf(r.InnerCircleInters)).ToList();
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

        //2e9c1b25-90c6-4969-b4ad-7d891a15a2cf
        void SplitDistrictsIntoBlocks()
        {
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


            foreach (var d in s.InnerDistricts)
            {
                var center = d.FindCenter();
                var rect = d.GetRectangleCircumscribedInPolygon();
                var pts = PoissonDiscSampler2D.GeneratePoints(rnd, MinBlockPtsRad, rect, 3);
                var v = new Voronoi(pts, rect);
                districtDiagrams.Add(v);
                var edges = v.VoronoiDiagram();
                
                JoinSamePosPts(edges);
                IdentifyIntersections(d, edges, center);
                WieldPtsThatAreTooClose(d, edges, center, 3f);
                RemoveLoseEdges(d, edges);
                RemoveOutsideEdges(d, edges);
                RemoveEdgesWIthOutsidePoints(d, edges);

                var invalidEdges = new List<LineSegment>();
                invalidEdges = edges.Where(e => e.p0.Id == e.p1.Id).ToList();
                edgesDict.Add(d, edges.Except(InvalidEdges).ToList());
            }

            AddIntersectionsToStreets();

            foreach (var d in s.InnerDistricts)
            {
                var newPts = d.CheckForDistrictEdgePoints(s);
                d.ReorderPointsByAngle();
            }

            foreach (var d in s.InnerDistricts)
            {
                var allPoints = edgesDict[d].SelectMany(e => e.EdgePoints).ToList();
                var lonelyPtsTmp = allPoints
                    .Where(p => allPoints.Count(p2 => Vector2.Distance(p.pos, p2.pos) < .2f) < 2)
                    .Distinct(new PointsComparer())
                    .ToList();

                AddDistrictEdges(d, edgesDict[d], s.InnerDistricts.IndexOf(d));
                edgesDict[d].ForEach(e => e.p0.AddNeighbour(e.p1));

                RemoveLoseEdges(d, edgesDict[d]);
                RemoveOutsideEdges(d, edgesDict[d]);

                allPoints = edgesDict[d].SelectMany(e => e.EdgePoints).ToList();
                lonelyPtsTmp = allPoints
                    .Where(p => allPoints.Count(p2 => Vector2.Distance(p.pos, p2.pos) < .2f) < 2)
                    .Distinct(new PointsComparer())
                    .ToList();
                lonelyPts.AddRange(lonelyPtsTmp);

                foreach (var pt in d.points)
                {
                    bool usedInEdge = edgesDict[d].Any(e => e.p0.Id == pt.Id || e.p1.Id == pt.Id);
                    if (!usedInEdge)
                    {
                        Debug.LogWarning($"Point {pt.Id} is not used in any edge in district {d.Id}");
                    }
                }

                if (lonelyPtsTmp.Any())
                {
                    var foundPts = new List<bool>();
                    foreach (var lPt in lonelyPtsTmp)
                    {
                        d.ContainsCheckpoint(lPt);
                    }
                }                
            }

            //missing edges: 19898a70-ecf9-4cf7-931b-8c28d99d6f09
            //invalid edges: dc3f8a70-7215-4977-b867-be9d98b213dc

            foreach (var d in s.InnerDistricts)
            {
                nodeDict.Add(d, new List<PtWSgmnts>());
                foreach (var edge in edgesDict[d])
                {
                    edge.p0.AddNeighbour(edge.p1);
                    if(!allEdges.Any(e => e.ContainsAllEdgePointsPos(edge.EdgePoints)))
                        allEdges.Add(edge);
                    if(!allNodes.Contains(edge.p0))
                    {
                        allNodes.Add(edge.p0);
                    }
                    if (!allNodes.Contains(edge.p1))
                    {
                        allNodes.Add(edge.p1);
                    }

                    if (!nodeDict[d].Contains(edge.p0))
                    {
                        nodeDict[d].Add(edge.p0);
                    }
                    if (!nodeDict[d].Contains(edge.p1))
                    {
                        nodeDict[d].Add(edge.p1);
                    }
                }
            }

            //kupa zbêdnych krawêdzi: 2d2f1dbb-4797-4913-b10a-2d2eb95d7f7a
            //DetectGraphCycles2.AnalyzeEdgeSanity(allEdges);
            //DetectGraphCycles2.ValidateCycle = true;
            //rawCycles = DetectGraphCycles.FindMinimalCycles(allEdges);
            //allCycles = DetectGraphCycles.TrySplitInvalidCycles(allEdges, rawCycles);
            //Debug.Log($"Liczba cykli: {allCycles.Count}, surowych: {rawCycles.Count}");

            //var missingEdgesStr = DetectGraphCycles2.AnalyzeCycleCoverage(allEdges, allCycles);
            //InvalidEdges = DetectGraphCycles2.AnalyzeInvalidEdges(allEdges, allCycles);
            //foreach (var edge in missingEdgesStr)
            //{
            //    var ids = edge.Split('_').ToList().ConvertAll(e => Convert.ToInt32(e));
            //    var node1 = allNodes.First(n => n.Id == ids[0]);
            //    var neighbour = node1.Neighbours.FirstOrDefault(n => n.Id == ids[1]);

            //    MissingEdges.Add(new LineSegment(node1, neighbour));
            //    MissingEdges.Last().CalculatePathLength();
            //}

            //foreach (var cycle in allCycles)
            //{
            //    var block = new Block(new List<Street>(), s.InnerDistricts[1], cycle);
            //    s.Blocks.Add(block);
            //}


            //var lenghts = missingEdgesStr.Select(e => MathF.Round(e.Length, 3)).ToList();
            //if (InvalidEdges.Count > 0)
            //    Debug.LogWarning($"CYCLES: Invalid edges counts: {InvalidEdges.Count}");

            //if (lenghts.Any())
            //    Debug.LogWarning($"CYCLES: Missing Edges lenghts: {string.Join(' ', lenghts)}");

            //var count = allNodes.Select(n => n.Id).Distinct().Count();
            //if (count != allNodes.Count)
            //{
            //    Debug.LogWarning($"CYCLES: Ids less than allnodes: {allNodes.Count} > {count}");
            //}

            var shortEdges = allEdges.Where(e => e.CalculatePathLength() < 0.5f).ToList();
            if (shortEdges.Any())
            {
                allEdges.RemoveList(shortEdges);
                Debug.Log($"Short edges removed: {shortEdges.Count}");
            }

            var nodes = allNodes.Distinct(new PointsComparer()).ToList();
            if (nodes.Count < allNodes.Count)
            {
                
                Debug.LogWarning($"Duplicated node positions found: {allNodes.Count - nodes.Count}");
            }

            facesExtracted = FaceExtractor3.ExtractMinimalCoveringFacesIterative(allEdges, 55);
            //facesExtracted = FaceExtractor.ExtractFacesWithoutOuter(allEdges);
            var missingEdgesStr = DetectGraphCycles2.AnalyzeCycleCoverage(allEdges, facesExtracted, "FACES");
            InvalidEdges = DetectGraphCycles2.AnalyzeInvalidEdges(allEdges, facesExtracted, "FACES");

            var lenghts = missingEdgesStr.Select(e => MathF.Round(e.Length, 3)).ToList();
            if (InvalidEdges.Count > 0)
                Debug.LogWarning($"FACES: Invalid edges counts: {InvalidEdges.Count}");

            if (lenghts.Any())
                Debug.LogWarning($"FACES: Missing Edges lenghts: {string.Join(' ', lenghts)}");

            var count = allNodes.Select(n => n.Id).Distinct().Count();
            if (count != allNodes.Count)
            {
                Debug.LogWarning($"FACES: Ids less than allnodes: {allNodes.Count} > {count}");
            }

        }

        void AddIntersectionsToStreets()
        {
            s.notJoinedRoads.Select(r => r.CalculatePathLength());
            foreach (var inters in intersections)
            {
                break;
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
                                Debug.Log("Intersection point added to street!!!!");
                                break;
                            }
                        }
                    }
                }
            }
        }

        int RemoveLoseEdges(District d, List<LineSegment> edges)
        {
            var allPts = edges.SelectMany(e => e.EdgePoints).ToList();
            var loseEdgesList = edges.Where(e => allPts.Count(s => s.Id == e.p0.Id) == 1 && allPts.Count(s => s.Id == e.p1.Id) == 1).ToList();
            edges.RemoveList(loseEdgesList);
            if (loseEdgesList.Any())
            {
                this.LoseEdges.AddRange(loseEdgesList);
                Debug.Log($"Removed {loseEdgesList.Count} lose edges");
            }
            return loseEdgesList.Count;
        }
        int RemoveOutsideEdges(District d, List<LineSegment> edges)
        {
            var allPts = edges.SelectMany(e => e.EdgePoints).ToList();
            var edgesOutside = edges.Where(e => !d.ContainsPoint(e.p0) && !d.ContainsPoint(e.p1)).ToList();
            edges.RemoveList(edgesOutside);
            if (edgesOutside.Any())
            {
                this.LoseEdges.AddRange(edgesOutside);
                Debug.Log($"Removed {edgesOutside.Count} lose edges");
            }            
            return edgesOutside.Count;
        }

        void RemoveEdgesWIthOutsidePoints(District d, List<LineSegment> edges)
        {
            var allPts = edges.SelectMany(e => e.EdgePoints).ToList();
            var edgesOutside = edges.Where(e => !d.ContainsPoint(e.p0) || !d.ContainsPoint(e.p1)).ToList();
            edges.RemoveList(edgesOutside);
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
                if (edges[i].CalculatePathLength() == 0)
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

        int JoinSamePosPts(List<LineSegment> edges)
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
            if(count > 0)
                Debug.Log($"For {edges.Count} edges merged {count} points with same coords");
            return count;
        }

        int WieldPtsThatAreTooClose(District d, List<LineSegment> edges, Vector2 center, float minDist = 2f)
        {
            int count = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                var ptsByDist = d.points.OrderBy(p => p.DistanceTo(edge.p0)).ToList();
                if (ptsByDist[0].DistanceTo(edge.p0) < 3f && ptsByDist[0].DistanceTo(edge.p0) > 0 && d.ContainsPoint(edge.p0))
                {
                    var newEdge = new LineSegment(ptsByDist[0], edge.p0);
                    edges.Insert(0, newEdge);
                    var ratioToCenter = newEdge.Length / Vector2.Distance(center, ptsByDist[0].pos);
                    edge.p0.pos = Vector2.Lerp(edge.p0.pos, center, ratioToCenter);
                    i++;
                }

                ptsByDist = d.points.OrderBy(p => p.DistanceTo(edge.p1)).ToList();
                if (ptsByDist[0].DistanceTo(edge.p1) < 3f && ptsByDist[0].DistanceTo(edge.p1) > 0 && d.ContainsPoint(edge.p1))
                {
                    var newEdge = new LineSegment(ptsByDist[0], edge.p1);
                    edges.Insert(0, newEdge);
                    var ratioToCenter = newEdge.Length / Vector2.Distance(center, ptsByDist[1].pos);
                    edge.p1.pos = Vector2.Lerp(edge.p1.pos, center, ratioToCenter);
                    i++;
                }
            }
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
            }
            if (d.points.Last().Id != d.points[0].Id)
            {
                edges.Add(new LineSegment(d.points.Last(), d.points[0]));
            }

            var edgePts = new HashSet<PtWSgmnts>(edges.SelectMany(e => new[] { e.p0, e.p1 }));

            var missingPts = d.points.Where(p => !edges.Any(e => e.EdgePoints.Contains(p))).ToList();
            if (missingPts.Any())
            {
                Debug.LogError($"Missing edge for points: {string.Join(", ", missingPts.Select(p => p.Id))}");
            }
        }

        List<PtWSgmnts> intersections = new List<PtWSgmnts>();
        int IdentifyIntersections(District d, List<LineSegment> edges, Vector2 center)
        {
            int count = 0;
            d.ReorderPointsByAngle();
            for (int j = 0; j < edges.Count; j++)
            {
                var edge = edges[j];
                for (int i = 0; i < d.points.Count - 1; i++)
                {
                    var pt = d.points[i];
                    var nextPt = d.points[i + 1];
                    var found = IdentifyIntersection(pt, nextPt, edge, d, i, center);
                    if (found == 1)
                    {
                        i++;
                        count++;
                    }
                }
                count += IdentifyIntersection(d.points.Last(), d.points[0], edge, d, d.points.Count - 1, center, true);
            }
            if (count > 0)
            {
                Debug.Log($"Identified intersections and shortened {count} edges out of {edges.Count} for district {d.Id}");
            }
            return count;
        }

        int IdentifyIntersection(PtWSgmnts pt, PtWSgmnts nextPt, LineSegment edge, District d, int i, Vector2 center, bool last = false)
        {
            var inters = VectorIntersect.GetIntersectionPoint(pt, nextPt, edge);
            if (inters.HasValue)
            {
                var intersPt = new PtWSgmnts(inters.Value);
                
                var lonelyIds = new int[] { 97, 173, 176, 226, 228, 260, 261 };
                if (lonelyIds.Contains(intersPt.Id) )
                {

                }
                    
                Street street = null;
                if (s.innerCircleStreet.ContainsCheckpoints(pt, nextPt))
                {
                    street = s.innerCircleStreet;
                }
                else
                {
                    street = d.bStr.FirstOrDefault(s => s.ContainsCheckpoints(pt, nextPt));                    
                }

                if (street != null)
                {
                    var orderedCircles = street.points.OrderBy(p => p.DistanceTo(intersPt)).ToList();
                    var orderedCircle = orderedCircles.First();
                    var dist = orderedCircle.DistanceTo(intersPt);
                    
                    if (dist < .1f)
                    {
                        //is it point already intersected?
                    }
                    else if (dist < 2f)
                    {//point too close to existing one
                        intersPt = orderedCircle;
                    }
                    else if (dist > 2f)
                    {
                        var index1 = street.points.IndexOf(pt);
                        var index2 = street.points.IndexOf(nextPt);
                        if (index1 < 0 || index2 < 0)
                        {
                            Debug.LogError($"Street {street.Id} does not contain both {pt.Id} and {nextPt.Id}");
                            return 0;
                        }
                        street.InsertCheckpoint(intersPt, Math.Max(index1, index2));

                        int insertIndex = d.points.IndexOf(pt) + 1;
                        if (insertIndex >= d.points.Count)
                            insertIndex = 0; // wrap
                        d.InsertCheckpoint(intersPt, insertIndex);
                        //d.InsertCheckpoint(intersPt, i.WrapIndex(1, d.points));
                        //Debug.DrawRay(intersPt.pos, Vector3.up * .5f, Color.blue, 5f);
                        intersections.Add(intersPt);
                    }
                }
                else
                {                    
                    var streets = d.bStr.Where(s => s.ContainsCheckpoint(nextPt) || s.ContainsCheckpoint(nextPt)).ToList();
                    foreach (var str in streets)
                    {
                        var indexOfPt = str.points.IndexOf(pt);
                        var indexOfNextPt = str.points.IndexOf(nextPt);
                        var insertIndex = (indexOfPt > -1? indexOfPt: indexOfNextPt);
                        //str.InsertCheckpoint(intersPt, insertIndex);
                    }
                }

                if (!d.ContainsCheckpoint(intersPt))
                {
                    int insertIndex = d.points.IndexOf(pt) + 1;
                    if (insertIndex >= d.points.Count)
                        insertIndex = 0; // wrap
                    d.InsertCheckpoint(intersPt, insertIndex);
                    //d.InsertCheckpoint(intersPt, i.WrapIndex(1, d.points));
                    //Debug.DrawRay(d.points[insertIndex].pos, Vector3.up * .5f, Color.blue, 5f);
                    intersections.Add(intersPt);
                }

                d.ReorderPointsByAngle();
                for (int z = 0; z < d.points.Count; z++)
                {
                    var a = d.points[z];
                    var b = d.points[(z + 1) % d.points.Count];
                    if (a == null || b == null) Debug.LogError("Null in district points");

                    if (a == b)
                    {
                        Debug.LogError($"Duplicate point {a.Id} in district {d.Id}");
                    }
                }

                var oldSegment = edge.ReplaceFurtherEdgePt(center, intersPt);
                oldSegment.ReplaceCloserEdgePt(center, intersPt);
                shortenedEdges.Add(edge);
                overlappingEdges.Add(oldSegment);
            }
            return inters.HasValue? 1 : 0;
        }


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
        
        public List<LineSegment> InvalidEdges = new();
        public List<LineSegment> MissingEdges = new();

        public List<PtWSgmnts> duplicatedNodes = new();
        public List<List<PtWSgmnts>> rawCycles = new();
        public List<List<PtWSgmnts>> allCycles = new();
        public List<List<PtWSgmnts>> facesExtracted = new();
        public List<LineSegment> shortenedEdges = new();
        public List<LineSegment> deadEnds = new();
        public List<LineSegment> LoseEdges = new();
        public List<LineSegment> overlappingEdges = new();
        public List<LineSegment> distVEdges = new();
        public List<LineSegment> spanningTree = new();
        public List<LineSegment> triangles = new();
        public List<LineSegment> ZeroLenEdges = new();
        public List<Rect> rects = new();

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

        public void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            var shift = new Vector2(0, -10);
            GizmoDrawer.DrawSegments(allEdges, shift);

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
                if(DrawRemovedEdges)
                {
                    Gizmos.color = Color.red;
                    GizmoDrawer.DrawSegments(overlappingEdges, new Vector2(-.1f, -.1f));

                    Gizmos.color = Color.yellow;
                    GizmoDrawer.DrawSegments(LoseEdges, new Vector2(.2f, 0));

                    Gizmos.color = Color.magenta;
                    GizmoDrawer.DrawSegments(deadEnds, new Vector2(.1f, -.1f));

                    Gizmos.color = Color.cyan;
                    //GizmoDrawer.DrawSpheres(intersections, new Vector2(.1f, -.1f), .2f, .001f);
                }
                Gizmos.color = Color.white;
                GizmoDrawer.DrawSegments(distVEdges, null, .1f);

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
            
            if (DrawBoundaries)
            {
                var index = 0;
                foreach (var rect in rects)
                {
                    Gizmos.color = colors[index++];
                    Gizmos.DrawLine(new Vector2(rect.x, rect.y) + shift, new Vector2(rect.x, rect.yMax) + shift);
                    Gizmos.DrawLine(new Vector2(rect.x, rect.yMax) + shift, new Vector2(rect.xMax, rect.yMax) + shift);
                    Gizmos.DrawLine(new Vector2(rect.xMax, rect.yMax) + shift, new Vector2(rect.xMax, rect.y) + shift);
                    Gizmos.DrawLine(new Vector2(rect.xMax, rect.y) + shift, new Vector2(rect.x, rect.y) + shift);
                    shift.x -= .1f;
                    shift.y += .1f;
                }
            }

            Gizmos.color = Color.white;
            foreach (var road in s.mainRoads)
            {
                //GizmoDrawer.DrawVectorList(road.points, false, .1f);
            }
            //GizmoDrawer.DrawVectorList(s.innerCircleStreet.points, true,  .1f);

            shift = new Vector2(.1f, -.1f);
            if (DrawDistricts)
            {
                foreach (var d in s.InnerDistricts)
                {
                    GizmoDrawer.DrawVectorList(d.points, true, .2f);
                }

                shift = new Vector2(-50f, 0f);
                Gizmos.color = Color.yellow;
                foreach (var p in allNodes)
                {
                    foreach(var n in p.Neighbours)
                    {
                        GizmoDrawer.DrawSegment(p, n, shift);
                    }
                }

                shift = new Vector2(50f, 0f);
                Gizmos.color = Color.blue;
                foreach (var e in allEdges)
                {
                    GizmoDrawer.DrawSegment(e, shift);
                    //GizmoDrawer.DrawVectorList(d.points, shift, true, .2f);
                }
            }
            
            if (DrawBlocks)
            {
                var index = 0;
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