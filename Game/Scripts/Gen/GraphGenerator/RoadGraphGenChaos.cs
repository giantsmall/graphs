using Assets.Game.Scripts.Editors;
using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Gen.Utils;
using Assets.Game.Scripts.Utility;
using Assets.Game.Scripts.Utility.NotAccessible;
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
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.TextCore.Text;

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
        static List<string> seedValues = new List<string>();
        static int currentSeed = 0;
        public int mainRoadsCount = 3;

        public bool DealWithShortBlockEdges = true;
        public bool CreateLots = false;
        public bool BuildMainRoads = false;
        public bool BuildRoads = false;

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
        public bool ResolveShortEdges = false;

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
            }

            MD5 md5Hasher = MD5.Create();
            var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(Seed));
            var intValue = BitConverter.ToInt32(hashed, 0);

            rnd = new System.Random(intValue);
            ClearLog();
            Log.InfoToFile("Seed = " + Seed);
            Generate();
        }
        #endregion

        void Generate()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            stopwatch.Start();
            
            #region clear
            GraphHelpers.intersectingSegments.Clear();
            innerCircle.Clear();
            badPoints.Clear();
            badLots.Clear();
            gatesCandidates.Clear();
            allEdges = new List<LineSegment>();
            blocks = new List<Polygon>();
            districtFaces.Clear();
            allLots.Clear();
            InnerCirclePopulation = 600;
            outerWall.Points.Clear();
            innerMoat.Points.Clear();
            outerMoat.Points.Clear();
            roadAroundMoatInner.Points.Clear();
            roadAroundMoatOuter.Points.Clear();
            mainRoadDirs.Clear();
            tooShortLines.Clear();
            PtWSgmnts.ResetCount();
            LineSegment.ResetCount();
            Polygon.ResetCount();
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
            var outerNeighbours = IdentifyNeighbours(innerCircle, outerDistricts);
            PolygonMerge.IncludeDeepNeighbours(innerCircle, outerDistricts);
            districtFaces = outerDistricts;
            PolygonMerge.RemoveNotExistingRelationShips(outerNeighbours);

            blocks = SplitInnerAreaIntoPolygons(innerCircle, InnerVoronoiSize);
            if(blocks is null || !blocks.Any())
            {
                blocks = new List<Polygon>();
                Debug.LogError("No blocks generated");
                return;
            }

            //PolygonMerge.MergeClosePoints(innerCircle, .5f);
            //PolygonMerge.ReplacePointsWithSamePos(blocks);
            PolygonMerge.FlattenAdjacentTriangles(blocks, innerCircle);
            var allDistricts = outerNeighbours.ToList();
            allDistricts.AddRange(blocks);
            allDistricts = allDistricts.Distinct().ToList();
            RemoveAnglesAround180Degrees(innerCircle, allDistricts);

            outerNeighbours = IdentifyNeighbours(innerCircle, outerDistricts);
            var innerNeighbours = IdentifyNeighbours(innerCircle, blocks);

            gatesCandidates = MergeClosestInnerRoadsToOuterReturnGateCandidates(innerCircle, innerNeighbours, outerNeighbours);

            PolygonMerge.MergeClosePoints(allDistricts, .15f);
            IdentifyTooShortEdges();
            if (ResolveShortEdges)
                ResolveTooShortEdges(blocks, innerNeighbours);

            //Remove duplicates or too short edges
            for (int i = 0; i < innerCircle.Count; i++)
            {
                var pt = innerCircle[i];
                var nextPt = innerCircle.Neighbour(i, 1);
                var dist = pt.DistanceTo(nextPt);                
                if (dist < 1 && dist > 0)
                {
                    if(pt.parentCount < 4)
                    {
                        pt.RemoveFromParentPolygons();
                        innerCircle.RemoveCheckPoint(pt);
                    }
                    else if(nextPt.parentCount < 4)
                    {
                        nextPt.RemoveFromParentPolygons();
                        innerCircle.RemoveCheckPoint(nextPt);
                    }
                }
            }
            innerCircle.RemoveDuplicates();

            if (BuildRoads)
            {
                InsertRoadsBetweenBlocks(outerNeighbours);

                var innerStrCenter = BuildLayerAroundPolygon(innerCircle, RoadWidth / 2f);            
                var prev = BuildLayerAroundPolygon(innerCircle, RoadWidth / 2f);
                s.innerStreet = new Street(prev.Points, RoadWidth);

                if (BuildWall)
                {
                    var innerWall = BuildLayerAroundPolygon(innerCircle, WallThickness / 2f);
                    var wallCenter = BuildLayerAroundPolygon(innerCircle, WallThickness / 2f);
                    s.wall = new Wall(wallCenter, WallThickness);

                    Debug.Log($"wall points = {s.wall.Points.Count}");
                    //RemoveAnglesAround180Degrees(wall, distrNeighToInnerCircle, true);
                }
                if (BuildMoat)
                {
                    BuildLayerAroundPolygon(innerCircle, MoatDistFromWall);
                    BuildLayerAroundPolygon(innerCircle, MoatWidth / 2f);
                    var moatCenter = BuildLayerAroundPolygon(innerCircle, MoatWidth / 2f);
                    s.moat = new Moat(moatCenter, MoatWidth);
                    Debug.Log($"Moat points = {s.moat.Points.Count}");

                    BuildLayerAroundPolygon(innerCircle, MoatDistToRoad);
                    var moatStreetCenter = BuildLayerAroundPolygon(innerCircle, RoadWidth / 2f);
                    Debug.Log($"Moat street points = {s.moatStreet.Points.Count}");
                    roadAroundMoatOuter = BuildLayerAroundPolygon(innerCircle, RoadWidth / 2f);
                    s.moatStreet = new Street(roadAroundMoatOuter.Points, RoadWidth);

                    //remove neighbour points from outer inner circle
                    foreach (var neigh in outerNeighbours)
                    {
                        var ptsIinside = neigh.Points.Where(p => roadAroundMoatInner.ContainsPoint(p)).ToList();
                        foreach (var pt in ptsIinside)
                        {
                            var closestPt = roadAroundMoatOuter.Points.OrderBy(p => p.DistanceTo(pt)).First();
                            pt.pos = closestPt.pos;
                        }
                    }
                }
            }
            if(BuildMainRoads)
                CreateMainRoads(innerNeighbours, outerNeighbours);


            if (CreateLots)
                FillBlocksWithLots(minLotDepth, maxLotDepth, minLotWidth, maxLotWidth);
            Log.InfoToFile($"Created in {stopwatch.Elapsed.TotalMilliseconds.Round(2)} ms!");

        }

        void IdentifyTooShortEdges()
        {
            tooShortLines.Clear();
            foreach (var block in blocks)
            {
                PolygonMerge.MergeClosePoints(block, .1f);
                var edgesLens = block.GetEdgeLengths();
                var shortestEdge = edgesLens.Min();
                var shortestEdgeIndex = edgesLens.IndexOf(shortestEdge);
                if (shortestEdge < maxLotWidth)
                {
                    //GizmosDrawer.DrawRay(block.FindCenter(), Color.cyan);
                    var pt = block[shortestEdgeIndex];
                    var nextPt = block.Neighbour(shortestEdgeIndex, 1);
                    if (pt.DistanceTo(nextPt) > 0)
                        tooShortLines.Add(new LineSegment(pt, nextPt));
                    else
                        GizmosDrawer.DrawRay(pt.pos, Color.blue);
                }
            }
            tooShortLines = tooShortLines.Distinct(new SegmentComparer(true)).ToList();
            Debug.Log($"{tooShortLines.Count} too short lines found.");
        }

        void ResolveTooShortEdges(List<Polygon> blocks, List<Polygon> innerNeighbours)
        {
            blocks.AddRange(innerNeighbours.Except(blocks));
            blocks = blocks.Distinct().ToList();

            var shift2 = new Vector2(.1f,  .1f);
            Debug.Log($"Edges to expand: {tooShortLines.Count}");

            int count = 0;
            foreach (var edge in tooShortLines)
            {
                var p0contained = innerCircle.ContainsCheckpoint(edge.p0);
                var p1contained = innerCircle.ContainsCheckpoint(edge.p1);
                if (!p0contained && !p1contained)
                {
                    var middlePt = (edge.p0.pos + edge.p1.pos) / 2f;
                    edge.p0.pos = middlePt;
                    edge.p1.pos = middlePt;
                    PolygonMerge.MergePoints(edge.p0, edge.p1);
                }
                else if(p0contained != p1contained)
                {
                    var fixedPt = edge.p0;
                    var ptToMove = edge.p1;
                    if(p1contained)
                    {
                        fixedPt = edge.p1;
                        ptToMove = edge.p0;
                    }

                    var dir = (ptToMove.pos - fixedPt.pos).normalized;
                    ptToMove.pos += dir * 1;

                }
                Debug.Log($"New edge len: {edge.CalculateLength()}");
            }
            Debug.Log($"Edges expanded {count} out of {tooShortLines.Count}");

            PolygonMerge.MergeClosePoints(blocks);
            

            IdentifyTooShortEdges();
            return;
            foreach (var shortEdge in tooShortLines)
            {
                var middlePt = (shortEdge.p0.pos + shortEdge.p1.pos) / 2f;
                shortEdge.p0.pos = middlePt;
                shortEdge.p1.pos = middlePt;

                PolygonMerge.MergePoints(shortEdge.p0, shortEdge.p1);
                Debug.Log($"New edge len: {shortEdge.CalculateLength()}");
            }
        }

        public static List<LineSegment> tooShortLines = new List<LineSegment>();

        void FillBlocksWithLots(float minDepth, float maxDepth, float minWidth, float maxWidth)
        {            
            allLots.Clear();
            int lotGenFailed = 0;
            foreach (var face in blocks)
            {
                face.RemoveDuplicates();

                var lotsBefore = allLots.Count;
                //int eachBlockHasChance = 1;
                for(int i = 0; i < 5; i++)
                {
                    //Debug.Log($">>>>>>> Iteration {eachBlockHasChance} for face id {face.Id}");
                    //try
                    {
                        var lots = ParcelGenerator.MakeLots(rnd, face, minDepth, maxDepth, minWidth, maxWidth);
                        var lotCount = allLots.Count;
                        lots = ValidateLots(lots, face);
                        //if(lotCount == lots.Count)
                        //{
                        //    allLots.AddRange(lots);
                        //    break;
                        //}

                        allLots.AddRange(lots);
                        break;
                        //if (!lots.Any())
                            //break;                            
                    }
                    //catch (Exception e)
                    {
                        //Debug.LogError($"Error during creation of lots: {e.Message}");
                        //Debug.LogError(e.StackTrace);
                    }
                }
                
                //while (eachBlockHasChance++ < 3);
                if (allLots.Count == lotsBefore)
                {
                    GizmosDrawer.DrawRay(face.FindCenter(), Color.red);
                    lotGenFailed++;
                }
                    
            }
            Debug.Log($"Lots created = {allLots.Count} for {blocks.Count - lotGenFailed} blocks. For {lotGenFailed} blocks lot generation failed");
        }

        List<Polygon> ValidateLots(List<Polygon> lots, Polygon outerPoly)
        {
            if (lots.Count == 0)
                return lots;

            //expanding outside outer polygon
            badLots.AddRange(lots.Where(l => l.Points.Any(p => !outerPoly.ContainsPoint(p, true))).ToList());
            badPoints.AddRange(lots.SelectMany(l => l.Points).Where(p => !outerPoly.ContainsPoint(p, true)).ToList());

            //GizmosDrawer.DrawRay(badPoints.First(), Color.red);

            //List<float> dists = new List<float>();
            //var ptContained = outerPoly.ContainsPoint(badPoints.First());
            //if (!ptContained)
            //{
            //    for (int i = 0; i < outerPoly.Count; i++)
            //    {
            //        var pt = outerPoly[i];
            //        var nextPt = outerPoly.Neighbour(i, 1);
            //        var inters = Vector.GetPerpendicularIntersection(pt, nextPt, badPoints.First());
            //        Debug.LogWarning("Drawing line");
            //        Debug.DrawLine(inters, badPoints.First().pos, Color.blue);
            //        GizmosDrawer.DrawRay(inters, Color.red);
            //        dists.Add(inters.DistanceTo(badPoints.First().pos));
            //    }
            //}

            //Debug.LogWarning(dists.OrderBy(d => d).ToList().Join(' '));
            ////area bigger than maxLotWidth * maxLotDepth
            //lots = lots.Where(l => l.CalculateArea() < maxLotDepth * maxLotWidth).ToList();
            //return lots;
            //overlapping one anoter

            var overlappingRanking = new List<int>();
            for (int i = 0; i < lots.Count; i++)
            {
                overlappingRanking.Add(lots[i].NumberOfPolygonsOverlapping(lots));
            }

            if (overlappingRanking.Any(v => v > 0))
            {

            }

            return lots.Except(badLots).ToList();
        }

        List<Vector2> MergeClosestInnerRoadsToOuterReturnGateCandidates(Polygon innerCircle, List<Polygon> innerNeighs, List<Polygon> outerNeighs)
        {
            Log.InfoToFile($"Gates candidates to be done");
            innerCircle.RemovePointsWithSamePos();
            innerCircle.ReorderPointsByAngleCW();
            var allFaces = innerNeighs.ToList();
            allFaces.AddRange(outerNeighs.ToList());
            allFaces.Add(innerCircle);
            allFaces = allFaces.Distinct().ToList();
            PolygonMerge.ReplacePointsWithSamePos(allFaces);

            var innerCandidates = innerCircle.Points.Where(p => innerNeighs.Count(n => n.ContainsCheckpoint(p)) > 1).ToList();
            var outerCandidates = innerCircle.Points.Where(p => outerNeighs.Count(n => n.ContainsCheckpoint(p)) > 1).ToList();
            var innerTemp = innerCandidates.Except(outerCandidates).ToList();
            outerCandidates = outerCandidates.Except(innerCandidates).ToList();
            innerCandidates = innerTemp;

            var gateCandidates = new List<Vector2>();
            foreach (var outerCand in outerCandidates)
            {
                var closestInner = innerCandidates.OrderBy(c => c.DistanceTo(outerCand)).First();
                var angle = innerCircle.GetInnerAngleOf(outerCand);
                if (angle == 180)
                {
                    var newPos = (closestInner.pos + outerCand.pos) / 2f;
                    closestInner.pos = newPos;
                    outerCand.pos = newPos;
                    gateCandidates.Add(newPos);
                }
                else
                {
                    closestInner.pos = outerCand.pos;
                    gateCandidates.Add(outerCand.pos);
                }
                //GizmosDrawer.DrawRay(outerCand.pos, Color.red);
            }
            Log.InfoToFile($"Gates candidates done.");
            return gateCandidates;
        }

        void InsertRoadsBetweenBlocks(List<Polygon> outerNeighs)
        {
            foreach (var block in blocks)
            {
                ShrinkPolygon(block, RoadWidth);
            }
        }

        void ShrinkPolygon(Polygon block, float size)
        {
            var center = block.FindCenter();
            var clonedPoints = block.Points.Select(p => new PtWSgmnts(p.pos)).ToArray();
            block.Clear();
            block.AddCheckPoints(clonedPoints);

            var newPoses = new List<Vector2>();
            for (int i = 0; i < block.Count; i++)
            {
                var pt = block[i];
                var ptPrev = block.Neighbour(i, -1);
                var ptNext = block.Neighbour(i, 1);

                var t = size / pt.DistanceTo(ptPrev);
                var prevLerp = Vector2.Lerp(pt.pos, ptPrev.pos, t);
                
                t = size / pt.DistanceTo(ptNext);
                var nextLerp = Vector2.Lerp(pt.pos, ptNext.pos, t);
                Vector2 newPos = (prevLerp + nextLerp) / 2f;
                block[i].pos = newPos;
            }
        }

        void ExpandPolygon(Polygon polygon, float width)
        {            
            var center = polygon.FindCenter();
            for (int i = 0; i < polygon.Count; i++)
            {
                var newPos = Vector.ExtendSegment(center, polygon[i].pos, width);               
                polygon[i].pos = newPos;
            }
        }

        Polygon BuildLayerAroundPolygon(Polygon polygon, float width)
        {
            var clone = polygon.GetDeepClone();
            ExpandPolygon(polygon, width);
            return clone;
        }

        public void CreateMainRoads(List<Polygon> innerNeighs, List<Polygon> outerNeighs)
        {
            //gatesCandidates = IdentifyGatesCandidates(innerCircle, innerNeighs, outerNeighs);
            mainRoadDirs = MainRoadDirGen.GenerateMainRoadDirections(rnd, MainRoadsCount);
            foreach (var dir in mainRoadDirs)
            {
                var gate = gatesCandidates.OrderBy(g => g.DistanceTo(dir)).Take(2).GetRandom(rnd);
                //perp intersection for wall, moat and moat road
                var mainRoad = new Street(new PtWSgmnts(gate), new PtWSgmnts(dir));
                s.mainRoads.Add(mainRoad);
            }

            if (BuildWall && false)
            {
                ClearLog();
                //perp intersection through wall
                foreach (var road in s.mainRoads)
                {

                    var moatStrPts = roadAroundMoatOuter.Points;
                    moatStrPts = moatStrPts.OrderBy(p => p.DistanceTo(road.p0.pos)).Take(2).ToList();
                    var moatStrLink = Vector.GetPerpendicularIntersection(moatStrPts[0].pos, moatStrPts[1].pos, road.p0.pos);

                    var angle = Vector.GetAngleBetweenVectors(road.p0.pos, moatStrLink, road.p1.pos);
                    float len = rnd.NextFloat(2, 5f);
                    moatStrLink = Vector.ExtendSegment(road.p0.pos, moatStrLink, len);
                    road.InsertCheckpoint(new PtWSgmnts(moatStrLink), 1);

                    //divide line to 10 points and for each point get 3-4 districts distinct
                    var hullInter = Vector.GetIntersectionWithPolygon(moatStrLink, road.p1.pos, voronoi.HullPointsInOrder());

                    List<Vector2> line = Vector.GetPointsBetween(moatStrLink, hullInter, 10);
                    var districts = new List<Polygon>();
                    foreach (var pt in line)
                    {
                        var closestDistricts = districtFaces.OrderBy(f => f.FindCenter().DistanceTo(pt)).Take(3).ToList();
                        districts.AddRange(closestDistricts);
                    }
                    districts = districts.Distinct().ToList();
                    var moatLinkDistrict = districts.First(d => d.ContainsPoint(moatStrLink));

                    //intersection with hull
                    var stDistrict = districts.OrderBy(d => d.FindCenter().DistanceTo(hullInter)).First();
                    var stPt = stDistrict.Points.OrderBy(p => p.DistanceTo(hullInter)).First();
                    road.InsertCheckpoint(stPt, road.Count - 1);

                    var path = new List<PtWSgmnts>();
                    
                    int failsafe = 50;
                    while (stDistrict.Id != moatLinkDistrict.Id && failsafe-- > 0)
                    {
                        stPt = stDistrict.Points.OrderBy(p => p.DistanceTo(hullInter)).First();
                        path.AddRange(stDistrict.GetNeighgboursPtCloserTo(stPt, moatStrLink));
                        stDistrict = districts.Except(stDistrict).Where(d => d.ContainsCheckpointPos(path.Last())).OrderBy(d => d.FindCenter().DistanceTo(moatStrLink)).First();
                    }


                    if (Vector.GetAngleBetweenVectors(road.p0.pos, moatStrLink, path.Last().pos) < 90)
                        path.Remove(path.Last());
                    else
                    {
                        //if
                    }

                    if (failsafe <= 0)
                    {
                        Debug.LogWarning($"Failsafe triggered, points added {path.Count}");
                        for (int i = 0; i < path.Count; i++)
                        {
                            Debug.DrawRay(path[i].pos + Vector2.right *  .1f * i, Vector2.up * i / 2f, Color.cyan);
                        }
                        break;
                    }
                    road.InsertCheckpoints(2, path.Reversed().Distinct(new PointsComparer()).ToArray());
                    //for (int i = 2; i < road.points.Count - 1; i++)
                    //{
                    //    var pi = road.points[i].pos;
                    //    var pPrev = road.points.Neighbour(i, -1).pos;
                    //    var pNext = road.points.Neighbour(i, 1).pos;

                    //    var ang = Vector.GetAngleBetweenVectors(pPrev, pi, pNext);
                    //    if (Mathf.Abs(ang) < 120)
                    //    {
                    //        Debug.Log($"Angle = {ang}");
                    //        road.points.RemoveAt(i);
                    //        i--;
                    //    }
                    //}
                }
            }
        }

        private List<Vector2> IdentifyGatesCandidates(Polygon mainPoly, List<Polygon> innerBlocks, List<Polygon> outerBlocks)
        {
            var candidates = new List<Vector2>();
            foreach (var pt in mainPoly.Points)
            {
                var count = innerBlocks.Count(b => b.ContainsCheckpointPos(pt));
                if (count > 1)
                {
                    candidates.Add(pt.pos);
                }
            }
            return candidates;
        }

        public static List<Polygon> IdentifyNeighbours(Polygon needsNeighbours, List<Polygon> polys)
        {
            return polys.Where(b => b.Points.Any(p => needsNeighbours.ContainsCheckpointPos(p))).ToList();
        }

        List<Polygon> SplitInnerAreaIntoPolygons(Polygon outerPolygon, float polySize, float offset = 0)
        {
            Log.InfoToFile($"Creating inner polygons");
            var outerCircle = s.OuterCircle;
            var center = outerPolygon.FindCenter();
            var outerArea = outerPolygon.CalculateArea();

            var rect = outerPolygon.GetRectangleCircumscribedInPolygon(offset);
            var pts = PoissonDiscSampler2D.GeneratePoints(rnd, polySize, rect);
            Voronoi v = new Voronoi(pts, rect);
            var failsafe = 100;
            while (v.DelaunayTriangulation().Count < 4 && failsafe-- > 0)
            {
                if (rect.size == Vector2.zero)
                {
                    Debug.LogError("PoissonDisc rect size = 0!!!");
                    return new List<Polygon>();
                }
                pts = PoissonDiscSampler2D.GeneratePoints(rnd, polySize, rect);
                v = new Voronoi(pts, rect);
            }
            if (failsafe == 0)
                Debug.LogError("Failsafe reached 0!");

            var edges = v.VoronoiDiagram();
            GraphHelpers.JoinSamePosPtsAndRemoveEmptyLines(edges);
            GraphHelpers.RemoveOutsideEdges(ref edges, outerPolygon);
            GraphHelpers.IdentifyIntersections(edges, outerPolygon, center);
            edges.AddRange(outerPolygon.CreateEdges());
            allEdges = edges;

            List<Polygon> bestFaces = null;
            var bestArea = 0f;
            int count = 15;
            do
            {
                var faces = FaceExtractor3.ExtractMinimalCoveringFacesIterative(edges, 15);
                var fCount = faces.Count;
                faces = faces.Distinct(new PolygonComparer()).ToList();
                Debug.Log($"Faces count = {fCount}. after filtering: {faces.Count}");
                var facesArea = faces.Sum(p => p.CalculateArea());
                outerArea = outerPolygon.CalculateArea();

                if (facesArea > bestArea && facesArea <= outerArea)
                {
                    bestArea = facesArea;
                    bestFaces = faces;
                }

                if (outerArea == bestArea)
                {
                    Debug.Log("All faces extracted");
                    break;
                }
                else if(count-- == 0)
                {
                    Debug.LogError("Extraction worked for 10 iterations and exited");
                    break;
                }
                else
                {
                    Debug.LogWarning($"Areas did not match. Again. Best faces area so far: {bestArea / outerArea}");
                    if (bestArea > outerArea)
                    {
                        Debug.LogError($"Best area should not exceed outer area - current ratio: {bestArea / outerArea}");
                    }
                }
            }
            while (true);
            //DetectGraphCycles.DetectCyclesSkippingEdges(edges, bestFaces.Select(f => f.Points).ToList());

            Log.InfoToFile($"Inner polygons done {bestArea / outerArea}");
            return bestFaces;
        }

        List<Polygon> SplitOuterAreaIntoPolygons(Polygon outerPolygon, float polySize, float offset = 0)
        {
            Log.InfoToFile("SplitOuterAreaIntoPolygons");
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
            Log.InfoToFile("GenerateVoronoiPolygons");
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
            Log.InfoToFile("GenerateVoronoiPolygons done");
            return polygons;
        }

        public static void RemoveAnglesAround180Degrees(Polygon poly, List<Polygon> neighbours)
        {
            if (neighbours.Any(n => n.Id == poly.Id))
                Debug.LogWarning("InnerDistrict found in neighbours!!!");
            RemoveAnglesAround180Degrees(new List<Polygon>() { poly }, neighbours);
        }

        static void RemoveAnglesAround180Degrees(List<Polygon> polys)
        {
            RemoveAnglesAround180Degrees(polys, new List<Polygon>());
        }

        static void RemoveAnglesAround180Degrees(List<Polygon> polys, List<Polygon> neighbours)
        {            
            Debug.Log($"All neighbours: {neighbours.Count}");
            foreach (var poly in polys)
            {
                int count = 0;
                int total = 0;
                do
                {
                    count = 0;
                    //var angles = poly.GetInnerAngles();
                    //Debug.Log("Remove angles 180 deg: " + angles.Join(';'));
                    for (int i = 0; i < poly.Count; i++)
                    {
                        var pt = poly[i];
                        var ptNext = poly.Neighbour(i, 1);
                        var ptPrev = poly.Neighbour(i, -1);
                        var angle = Vector.GetInternalAngle(ptPrev.pos, pt.pos, ptNext.pos);

                        if (angle > 170f && angle < 190f)
                        {
                            var p = poly[i];
                            var prevP = poly.Neighbour(i, -1);
                            var nextP = poly.Neighbour(i, 1);

                            var expNewPos = Vector.GetPerpendicularIntersection(prevP.pos, nextP.pos, p.pos);
                            var filteredNeighbours = neighbours.Where(n => n.ContainsCheckpointPos(p, .2f)).ToList();
                            if (filteredNeighbours.Count > 1)
                            {
                                foreach (var neigh in filteredNeighbours)
                                {
                                    neigh.UpdateCheckPointsPosByPos(p.pos, expNewPos);
                                }

                                if (p.pos != expNewPos)
                                {
                                    count++;
                                }
                                p.pos = expNewPos;
                            }
                            else
                            {

                                if (filteredNeighbours.Any())
                                    filteredNeighbours[0].DeletePointByPos(p.pos);
                                poly.RemoveAt(i);
                                i = 0;
                            }
                        }
                    }

                }
                while (count > 0 && total++ < 500);
                if(total == 500)
                {
                    Debug.LogError("Total reached 500!");
                }
            }
        }

        private void DrawFace(Polygon face, Color color, Vector2? shift = null, Vector2? itshift = null)
        {
            Gizmos.color = color;
            var shiftVal = shift.HasValue ? shift.Value : Vector2.zero;
            var itShiftVal= itshift.HasValue ? itshift.Value : Vector2.zero;
            for (int i = 0; i < face.Points.Count; i++)
            {
                var a = face[i].pos;
                var b = face.Points.Neighbour(i, 1).pos;
                Gizmos.DrawLine(a + shiftVal, b + shiftVal);
                Gizmos.DrawSphere(a + shiftVal, 0.05f);
                shiftVal += itShiftVal;
            }
        }

        private void DrawFaces(List<Polygon> faces, Color color, Vector2? shift = null, Vector2? itshift = null)
        {
            foreach(var face in faces)
            {
                DrawFace(face, color, shift, itshift);
            }
        }

        private void DrawStreet(Street s, Color color)
        {
            if (s is null)
                return;
            Gizmos.color = color;
            var center = s.FindCenter();
            for(int i = 0; i < s.Points.Count; i++)
            {
                var nextP = s.Points.Neighbour(i, 1);                
                var outerPos = Vector.ExtendSegment(center, s[i].pos, s.thickness / 2f);
                var outerNext = Vector.ExtendSegment(center, nextP.pos, s.thickness / 2f);

                var innerPos = Vector.ExtendSegment(center, s[i].pos, -s.thickness / 2f);
                var innerNext = Vector.ExtendSegment(center, nextP.pos, -s.thickness / 2f);

                Gizmos.DrawLine(innerPos, innerNext);
                Gizmos.DrawLine(outerPos, outerNext);
                Gizmos.DrawSphere(innerNext, 0.05f);
                Gizmos.DrawSphere(outerNext, 0.05f);
            }
        }

        public void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(new Vector2(-5, 0), new Vector2(-5, maxLotWidth));

            var shift150_150 = new Vector2(150, -150f);
            var shiftDot1 = new Vector2(0f, 0f);

            foreach(var face in districtFaces)
            {                
                Color c = Color.black;
                DrawFace(face, c, shiftDot1);
                //DrawFace(districtFaces[i].points, c, shift150_150);
                shift150_150 += new Vector2(.1f, -.1f);
            }

            //Gizmos.color = Color.magenta;
            //GizmosDrawer.DrawSegments(allEdges);

            DrawFaces(blocks, Color.white);
            DrawFaces(allLots, Color.white);
            DrawFaces(badLots, Color.cyan);
            Gizmos.color = Color.red;
            GizmosDrawer.DrawSpheres(badPoints, .05f);

            var innerCirColor = BuildWall ? Color.yellow : Color.white;
            //DrawFace(innerCircle, Color.gray);

            //DrawFace(innerCircle, innerCirColor);
            DrawStreet(s.innerStreet, Color.white);
            DrawStreet(s.wall, Color.yellow);
            DrawStreet(s.moat, Color.blue);            
            DrawStreet(s.moatStreet, Color.cyan);

            Gizmos.color = Color.magenta;
            //GizmosDrawer.DrawSegments(tooShortLines);
            //DrawFace(innerMoat, Color.blue);
            //DrawFace(outerMoat, Color.blue);
            //DrawFace(roadAroundMoatInner, Color.white);
            //DrawFace(roadAroundMoatOuter, Color.white);

            Gizmos.color = Color.green;
            //GizmosDrawer.DrawSpheres(gatesCandidates, .2f);
        }
    }
}
