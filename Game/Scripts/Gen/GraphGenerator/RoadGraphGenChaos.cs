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
        Polygon roadAroundMoat = new();
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
            GraphHelpers.intersectingSegments.Clear();
            badPoints.Clear();
            badLots.Clear();

            allEdges.Clear();
            blocks.Clear();
            districtFaces.Clear();
            allLots.Clear();
            InnerCirclePopulation = 600;
            outerWall.points.Clear();
            innerMoat.points.Clear();
            outerMoat.points.Clear();
            roadAroundMoat.points.Clear();
            roadAroundMoatOuter.points.Clear();
            mainRoadDirs.Clear();
            PtWSgmnts.ResetCount();
            LineSegment.ResetCount();
            Polygon.ResetCount();
            s = new SettlementModel(new PtWSgmnts());

            ClearLog();

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

            List<Polygon> mergedPolygons = PolygonMerge.MergeAdjacentPolygons(innerCircleCycles);
            districtFaces.AddRange(mergedPolygons);
            innerCircle = mergedPolygons.FirstOrDefault();
            if (PolishInnerCircle)
            {
                RemoveVerticesFromShortEdges(innerCircle, districtFaces);
                ReduceObtuseAngles(innerCircle, districtFaces);
            }

            blocks = SplitInnerAreaIntoPolygons(innerCircle, InnerVoronoiSize);
            gatesCandidates = IdentifyGatesCandidates(innerCircle, blocks);
            Debug.Log($"Blocks count = {blocks.Count}");
            InsertRoadsBetweenBlocks();
            
            var distrNeighToInnerCircle = ExpandInnerCircleToFitRoad(innerCircle);
            //RemoveAnglesAround180Degrees(blocks);

            //Line below should use the most outer polygon overlaying inner circle
            //RemoveAnglesAround180Degrees(innerCircle, distrNeighToInnerCircle, true);

            if (BuildWall)
            { 
                var wallCenter = BuildLayerAroundInnerCirlce(innerCircle, distrNeighToInnerCircle, WallThickness / 2f);
                outerWall = BuildLayerAroundInnerCirlce(wallCenter, distrNeighToInnerCircle, WallThickness / 2f);
                s.wall = new Wall(wallCenter, WallThickness);

                //RemoveAnglesAround180Degrees(wall, distrNeighToInnerCircle, true);
            }
            if(BuildMoat)
            {            
                innerMoat = BuildLayerAroundInnerCirlce(outerWall, distrNeighToInnerCircle, MoatDistFromWall);
                var moatCenter = BuildLayerAroundInnerCirlce(innerMoat, distrNeighToInnerCircle, MoatWidth / 2f);
                s.moat = new Moat(moatCenter, MoatWidth);
                outerMoat = BuildLayerAroundInnerCirlce(moatCenter, distrNeighToInnerCircle, MoatWidth / 2f);

                roadAroundMoat = BuildLayerAroundInnerCirlce(outerMoat, distrNeighToInnerCircle, MoatDistToRoad);
                var moatStreetCenter = BuildLayerAroundInnerCirlce(roadAroundMoat, distrNeighToInnerCircle, RoadWidth / 2f);
                s.moatStreet = new Street(moatStreetCenter.points, RoadWidth);
                roadAroundMoatOuter = BuildLayerAroundInnerCirlce(moatStreetCenter, distrNeighToInnerCircle, RoadWidth / 2f);                
            }

            Debug.Log($"Blocks count = {blocks.Count}");
            if (DealWithShortBlockEdges)
            {
                MergeShortestEdges(blocks, innerCircle);
                MoveRemainingShortEdgesPointsAway(blocks, 2 * minLotWidth, MinBlockAreaToPutShortEdgesAway);
            }
            CreateMainRoads();
            if (CreateLots)
                FillBlocksWithLots(minLotDepth, maxLotDepth, minLotWidth, maxLotWidth);
        }

        void MoveRemainingShortEdgesPointsAway(List<Polygon> polys, float minWidth, float minBlockArea)
        {
            foreach(var poly in polys)
            {
                var edgesLengths = poly.GetEdgeLengths();
                Debug.Log($"Edges lengths ({poly.points.Count}): {edgesLengths.Select(e => Math.Round(e, 2)).Join(';')}");
                for (int i = 0; i < edgesLengths.Count; i++)
                {

                    if (edgesLengths[i] < minWidth && poly.CalculateArea() > minBlockArea)
                    {
                        var pPrev = poly.points.Neighbour(i, -1);
                        var pi = poly.points[i];
                        var pNext = poly.points.Neighbour(i, 1);
                        var p2Next = poly.points.Neighbour(i, 2);

                        var t = .15f;
                        pi.pos = Vector2.Lerp(pi.pos, pPrev.pos, t);

                        t = .15f;
                        pNext.pos = Vector2.Lerp(pNext.pos, p2Next.pos, t); 
                    }
                }
            }
        }

        private List<Vector2> IdentifyGatesCandidates(Polygon parentPoly, List<Polygon> blocks)
        {
            var candidates = new List<Vector2>();
            foreach (var pt in parentPoly.points)
            {
                var count = blocks.Count(b => b.ContainsCheckpointPos(pt));
                if(count >= 2)
                {
                    candidates.Add(pt.pos);
                }
            }
            return candidates;
        }

        public void CreateMainRoads()
        {
            mainRoadDirs = MainRoadDirGen.GenerateMainRoadDirections(rnd, MainRoadsCount);
           
            foreach(var dir in mainRoadDirs)
            {
                var gate = gatesCandidates.OrderBy(g => g.DistanceTo(dir)).Take(2).GetRandom(rnd);
                //perp intersection for wall, moat and moat road

                var mainRoad = new Street(new PtWSgmnts(gate), new PtWSgmnts(dir));
                s.mainRoads.Add(mainRoad);
            }

            if (BuildWall)
            {
                int maxCount = 100;

                ClearLog();
                //perp intersection through wall
                foreach(var road in s.mainRoads)
                {
                    var moatStrPts = roadAroundMoatOuter.points;
                    moatStrPts = moatStrPts.OrderBy(p => p.DistanceTo(road.p0.pos)).Take(2).ToList();
                    var moatStrLink = VectorIntersect.GetPerpendicularIntersection(moatStrPts[0].pos, moatStrPts[1].pos, road.p0.pos);

                    var angle = VectorIntersect.GetAngleBetweenVectors(road.p0.pos, moatStrLink, road.p1.pos);
                    float len = rnd.NextFloat(2, 5f);

                    moatStrLink = VectorIntersect.ExtendSegment(road.p0.pos, moatStrLink, len);
                    road.points.Insert(1, new PtWSgmnts(moatStrLink));


                    //Find closest voronoi Hull point:
                    var hullpts = voronoi.HullPointsInOrder();
                    for (int i = 0; i < hullpts.Count; i++)
                    {
                        var distContainingMoastLink = districtFaces.First(d => d.ContainsPoint(moatStrLink));

                        var hullI = hullpts[i];
                        var hullNextI = hullpts.Neighbour(i, 1);    
                        var inters =  VectorIntersect.GetIntersectionPoint(hullI, hullNextI, road.p1.pos, moatStrLink);
                        if (inters.HasValue)
                        {
                            PtWSgmnts prevP = new PtWSgmnts(inters.Value);
                            Polygon closestDist = districtFaces.OrderBy(d => d.FindCenter().DistanceTo(prevP.pos)).First();
                            closestDist.RemovePointWithSamePos();

                            List<Vector2> usedPos = new List<Vector2>();

                            do
                            {
                                var lastPt = closestDist.points.OrderBy(p => p.DistanceTo(prevP)).First();
                                if(!usedPos.Contains(lastPt.pos))
                                {
                                    Debug.DrawRay(lastPt.pos + new Vector2(.1f, 0), Vector2.up * 2f, Color.yellow);
                                    Debug.Log($"point added: {lastPt.pos}");
                                    road.InsertCheckpoint(lastPt, 2);
                                    usedPos.Add(lastPt.pos);
                                }

                                if (lastPt is null)
                                    break;

                                //UPDATE CODE BELOW::
                                //lastPt = closestDist.GetNeighgboursPtCloserTo(lastPt, moatStrLink);
                                if (lastPt != null)
                                {
                                    Debug.DrawRay(lastPt.pos, Vector2.up * 2f, Color.red);
                                    Debug.Log($"point added II: {lastPt.pos}");
                                    road.InsertCheckpoint(lastPt, 2);
                                    usedPos.Add(lastPt.pos);
                                    prevP = lastPt;
                                }
                                else
                                {
                                    try
                                    {
                                        var dists = districtFaces.Where(d => d.ContainsCheckpointPos(prevP, 2f)).ToList();
                                        foreach (var dist in dists)
                                        {
                                            Debug.DrawLine(dist.points[0].pos, dist.points[^1].pos, Color.blue);
                                        }

                                        closestDist = dists.OrderBy(d => d.FindCenter().DistanceTo(prevP.pos)).FirstOrDefault();
                                        if (closestDist is null)
                                        {
                                            Debug.Log("CLOSEST DIST NULL");
                                            break;
                                        }
                                        else
                                        {
                                            Debug.DrawLine(closestDist.points[0].pos, closestDist.points[^1].pos, Color.green);
                                        }
                                            
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogError($"Error finding closest district: {e.Message}");
                                        break;
                                    }
                                }
                            }
                            while (closestDist.Id != distContainingMoastLink.Id && maxCount-- > 0);
                            Debug.Log("Loop ended");
                        }
                    }

                    break;
                }

            }
        }

        Polygon BuildLayerAroundInnerCirlce(Polygon innerCircle, List<Polygon> neighbourDistricts, float width)
        {
            var innerCircClone = innerCircle.GetDeepClone();
            var center = innerCircClone.FindCenter();
            for (int i = 0; i < innerCircClone.points.Count; i++)
            {
                var neighbourdsFaces = neighbourDistricts.Where(l => l.ContainsCheckpointPos(innerCircle.points[i])).ToList();

                var p = innerCircClone.points[i];
                var newPos = VectorIntersect.ExtendSegment(center, p.pos, width);
                neighbourdsFaces.ForEach(pol =>
                {
                    pol.UpdatePointPosByPos(p, newPos);
                });
                innerCircClone.points[i].pos = newPos;
            }
            return innerCircClone;
        }

        void RemoveAnglesAround180Degrees(Polygon poly, List<Polygon> neighbours, bool listAngles = false)
        {
            RemoveAnglesAround180Degrees(new List<Polygon>() { poly }, new List<Polygon>(), listAngles);
        }

        void RemoveAnglesAround180Degrees(List<Polygon> polys, bool listAngles = false)
        {
            RemoveAnglesAround180Degrees(polys, new List<Polygon>(), listAngles);
        }

        void RemoveAnglesAround180Degrees(List<Polygon> polys, List<Polygon> neighbours, bool listAngles = false)
        {
            foreach (var poly in polys)
            {
                var angles = poly.GetInnerAngles();
                for (int i = 0; i < angles.Count; i++)
                {
                    if (Math.Abs(angles[i]) > 170f && Math.Abs(angles[i]) < 190f)
                    {
                        var p = poly.points[i];
                        var prevP = poly.points.Neighbour(i, -1);
                        var nextP = poly.points.Neighbour(i, 1);
                        
                        var expNewPos = VectorIntersect.GetPerpendicularIntersection(prevP.pos, nextP.pos, p.pos);
                        var polysToUpdate = neighbours.Where(n => n.ContainsCheckpointPos(poly.points[i])).ToList();
                        foreach (var neigh in polysToUpdate)
                        {
                            neigh.UpdatePointPosByPos(p, expNewPos);
                        }
                        poly.points[i].pos = expNewPos;
                        //poly.points.RemoveAt(i);
                        //angles = poly.GetInnerAngles();
                        //i = 0;
                    }
                }   
            }
        }

        List<Polygon> ExpandInnerCircleToFitRoad(Polygon innerCircle)
        {
            List<Polygon> neighbourDistricts = new List<Polygon>();
            var center = innerCircle.FindCenter();  
            for (int i = 0; i < innerCircle.points.Count; i++)
            {
                var neighbourdsFaces = districtFaces.Where(l => l.ContainsCheckpointPos(innerCircle.points[i])).ToList().Except(innerCircle).ToList();
                
                var p = innerCircle.points[i];
                var newPos = VectorIntersect.ExtendSegment(center, p.pos, RoadWidth);
                
                neighbourdsFaces.ForEach(pol => 
                {
                    pol.UpdatePointPosByPos(p, newPos);
                });
                p.pos = newPos;

                neighbourDistricts.AddRange(neighbourdsFaces);
            }

            return neighbourDistricts;
        }

        void InsertRoadsBetweenBlocks()
        {
            foreach (var block in blocks)
            {
                var center = block.FindCenter();
                //clone points
                var clonedPoints = block.points.Select(p => new PtWSgmnts(p.pos)).ToList();                
                block.points = clonedPoints;    

                for (int i = 0; i < block.points.Count; i++)
                {
                    var t = (RoadWidth / 2f) / block.points[i].pos.DistanceTo(center);  
                    var p = block.points[i];

                    var newPos = Vector2.Lerp(p.pos, center, t);
                    p.pos = newPos;
                }
            }
        }

        void RemoveVerticesFromShortEdges(Polygon polygon, List<Polygon> parentGroup)
        {
            //Removing vertices belonging to short edges
            int totalPointsRemoved = 0;
            var edgesLengths = polygon.GetEdgeLengths();
            Debug.Log($"Edges lengths ({polygon.points.Count}): {edgesLengths.Select(e => Math.Round(e, 2)).Join(';')}");
            Debug.Log($"Edges Below {2 * maxLotWidth}: {edgesLengths.Select(e => Math.Round(e, 2)).Where(p => p < 2 * maxLotWidth).Join(';')}");
            for (int i = 0; i < edgesLengths.Count; i++)
            {
                if (edgesLengths[i] < 2 * maxLotWidth)
                {
                    var p0 = polygon.points[i];
                    var p1 = polygon.points.Neighbour(i, 1);

                    var p0RelatedPolys = parentGroup.Where(l => l.ContainsCheckpointPos(p0)).ToList();
                    var p1RelatedPolys = parentGroup.Where(l => l.ContainsCheckpointPos(p1)).ToList();
                    Debug.Log($"REMOVING ASHHH, related polygons: {p0RelatedPolys.Count}");
                    Debug.Log($"p0 neigh = {p0RelatedPolys.Count}, p1 neigh = {p1RelatedPolys.Count}");

                    bool found = false;
                    if (p0RelatedPolys.Count == 2)
                    {
                        p0RelatedPolys.ForEach(pol =>
                        {
                            var count = pol.points.Count;
                            pol.RemovePointWByPos(p0);
                            Debug.Log($"Before: {count}, after: {pol.points.Count}");
                            totalPointsRemoved += count - pol.points.Count;
                        });
                        found = true;
                    }
                    
                    if (p1RelatedPolys.Count == 2)
                    {
                        p0RelatedPolys.ForEach(pol =>
                        {
                            var count = pol.points.Count;
                            pol.RemovePointWByPos(p1);
                            Debug.Log($"Before: {count}, after: {pol.points.Count}");
                            totalPointsRemoved += count - pol.points.Count;
                        });
                        found = true;
                    }                    
                    
                    if (p0RelatedPolys.Count > 2 && p1RelatedPolys.Count > 2)
                    {
                        Debug.LogWarning($"Edge len: {edgesLengths[i]}, {p0RelatedPolys.Count}, {p1RelatedPolys.Count} Removing short edges: both points got more than two neighbourds!. Step skipped");
                    } 

                    if(found)
                    {
                        edgesLengths = polygon.GetEdgeLengths();
                        i = Math.Max(i - 2, -1);
                    }
                }
            }
            edgesLengths = polygon.GetEdgeLengths();
            Debug.Log($"Total points removed: {totalPointsRemoved}");
            Debug.Log($"Edges lengths ({polygon.points.Count}): {edgesLengths.Select(e => Math.Round(e, 2)).Join(';')}");
            Debug.Log($"Edges Below {2 * maxLotWidth}: {edgesLengths.Select(e => Math.Round(e, 2)).Where(p => p < 2 * maxLotWidth).Join(';')}");
        }

        void ReduceObtuseAngles(Polygon polygon, List<Polygon> parentGroup)
        {
            var angles = polygon.GetInnerAngles();
            Debug.Log($"Angles: {angles.Join(';')}");
            for (int i = 0; i < angles.Count; i++)
            {
                var angle = angles[i];
                if(angle >= 0)
                {
                    var currP = polygon.points[i];
                    var prevP = polygon.points.Neighbour(i, -1);
                    var nextP = polygon.points.Neighbour(i, 1);
                    var newPos = VectorIntersect.GetPerpendicularIntersection(prevP.pos, nextP.pos, currP.pos);

                    parentGroup.Where(l => l.ContainsCheckpointPos(currP))
                               .ToList()
                               .ForEach(pol => { pol.UpdatePointPosByPos(currP, newPos); });
                    polygon.points[i].pos = newPos;
                }
            }
        }

        void MergeShortestEdges(List<Polygon> polygons, Polygon outerPolygon)
        {
            int cap = 0;
            int count = 0;
            do
            {
                cap++;
                count = 0;
                var districtEdges = polygons.SelectMany(l => l.CreateEdges()).Distinct(new SegmentComparer(true)).ToList();
                
                foreach (var poly in polygons)
                {
                    var edge = poly.GetshortestEdge();
                    var shortestEdge = edge.GetshortestEdge();
                    if (shortestEdge.Length < minLotWidth)
                    {
                        var p0 = edge.p0;
                        var p1 = edge.p1;

                        var relatedLotsP0 = polygons.Where(l => l.ContainsCheckpointPos(p0)).ToList();
                        var relatedLotsP1 = polygons.Where(l => l.ContainsCheckpointPos(p1)).ToList();

                        var p0OnEdge = outerPolygon.PointOnEdge(p0);
                        var p1OnEdge = outerPolygon.PointOnEdge(p1);
                        var noPointsOnOuterPoly = !outerPolygon.PosOnPolygonEdge(p0, p1);
                        if (p0OnEdge == p1OnEdge && noPointsOnOuterPoly)
                        {
                            districtEdges.Remove(edge);
                            var midPoint = (p0.pos + p1.pos) / 2f;


                            outerPolygon.UpdatePointPosByPos(p0, midPoint);
                            relatedLotsP0.ToList()
                                         .ForEach(pol => { pol.UpdatePointPosByPos(p0, midPoint); });
                            relatedLotsP1.ToList()
                                         .ForEach(pol => { pol.UpdatePointPosByPos(p1, midPoint); });

                            p0.pos = midPoint;
                            p1.pos = midPoint;
                            p0.AbsorbNeighbour(p1);
                            foreach (var lot in relatedLotsP0)
                            {
                                var indexOfP0 = lot.points.IndexOf(p0);
                                var indexOfP1 = lot.points.IndexOf(p1);
                                if (indexOfP1 > -1)
                                {
                                    lot.InsertCheckpoint(p0, indexOfP1);
                                    lot.points.Remove(p1);
                                }
                                else if (indexOfP0 > -1)
                                {
                                    lot.InsertCheckpoint(p1, indexOfP0);
                                    lot.points.Remove(p0);
                                }
                            }
                            count++;
                        }
                        else if (p0OnEdge != p1OnEdge && noPointsOnOuterPoly)
                        {
                            var pToAbsorb = p0;
                            var ptGrowing = p1;
                            if (p0OnEdge)
                            {
                                pToAbsorb = p1;
                                ptGrowing = p0;
                            }
                            pToAbsorb.pos = ptGrowing.pos;
                            ptGrowing.AbsorbNeighbour(pToAbsorb);
                            foreach (var lot in relatedLotsP0)
                            {
                                lot.points.Remove(pToAbsorb);
                            }
                            count++;
                        }
                    }
                }
                polygons.ForEach(p => p.RemovePointWithSamePos());
                break;
            }
            while (count > 0 && cap < 100);
            if (cap >= 100)
            {
                Debug.LogWarning("Cap reached 100!!!");
            }
        }

        void FillBlocksWithLots(float minDepth, float maxDepth, float minWidth, float maxWidth)
        {
            int lotGenFailed = 0;
            foreach (var face in blocks)
            {
                try
                {
                    var lots = ParcelGenerator.MakeLots(rnd, face, minDepth, maxDepth, minWidth, maxWidth);
                    lots = ValidateLots(lots, face);
                    allLots.AddRange(lots);
                    if (lots.Count == 0)
                        lotGenFailed++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error during creation of lots: {e.Message}");
                    Debug.LogError(e.StackTrace);
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
            badLots.AddRange(lots.Where(l => l.points.Any(p => !outerPoly.ContainsPoint(p))).ToList());

            badPoints.AddRange(lots.SelectMany(l => l.points).Where(p => !outerPoly.ContainsPoint(p)).ToList());

            ////area bigger than maxLotWidth * maxLotDepth
            //lots = lots.Where(l => l.CalculateArea() < maxLotDepth * maxLotWidth).ToList();
            //return lots;

            //overlapping one anoter

            var overlappingRanking = new List<int>(); 
            for (int i = 0; i < lots.Count; i++)
            {
                overlappingRanking.Add(lots[i].NumberOfPolygonsOverlapping(lots));
            }

            if(overlappingRanking.Any(v => v > 0))
            {

            }

            return lots.Except(badLots).ToList();
        }

        internal static System.Random GetRandom()
        {
            return rnd;
        }

        List<Polygon> SplitInnerAreaIntoPolygons(Polygon outerPolygon, float polySize, float offset = 0)
        {
            var outerCircle = s.OuterCircle;
            var center = outerPolygon.FindCenter();
            var rect = outerPolygon.GetRectangleCircumscribedInPolygon(offset);
            var pts = PoissonDiscSampler2D.GeneratePoints(rnd, polySize, rect);
            Voronoi v = new Voronoi(pts, rect);
            var it = 100;
            while (v.DelaunayTriangulation().Count < 4 && it > 0)
            {
                if (rect.size == Vector2.zero)
                {
                    Debug.LogError("PoissonDisc rect size = 0!!!");
                    return new List<Polygon>();
                }
                pts = PoissonDiscSampler2D.GeneratePoints(rnd, polySize, rect);
                v = new Voronoi(pts, rect);                
            }
            var edges = v.VoronoiDiagram();
            GraphHelpers.JoinSamePosPtsAndRemoveEmptyLines(edges);
            GraphHelpers.RemoveOutsideEdges(ref edges, outerPolygon);
            GraphHelpers.IdentifyIntersections(edges, outerPolygon, center);
            edges.AddRange(outerPolygon.CreateEdges());

            List<Polygon> bestFaces = null;
            var bestArea = 0f;
            int count = 10;
            do
            {
                var faces = FaceExtractor.ExtractMinimalCoveringFacesIterative(edges, 10);
                var facesArea = faces.Sum(p => p.CalculateArea());
                var outerArea = outerPolygon.CalculateArea();                
                if(outerArea > bestArea)
                {
                    bestArea = outerArea;
                    bestFaces = faces;
                }

                if (outerArea == facesArea)
                {
                    bestArea = outerArea;
                    bestFaces = faces;
                    break;
                }
                Debug.LogWarning($"Areas did not match. Again. Best faces area so far: {bestArea / outerArea}");
            }
            while (--count > 0);
            return bestFaces;
        }

        public class VoronoiFace
        {
            public Vector2 site;
            public List<Vector2> vertices = new List<Vector2>();
        }

        public List<Polygon> GenerateVoronoiPolygons(List<Vector2> points, Rect bounds)
        {
            // Konwersja do typu wymaganego przez bibliotekê
            var delaunayPoints = points.Select(p => new Vertex(p.x, p.y)).ToList();

            // Tworzenie diagramu Delaunay i generowanie Voronoi
            voronoi = new Voronoi(points, bounds);
            var triangulation = voronoi.DelaunayTriangulation();

            var polygons = new List<Polygon>();

            foreach (var region in voronoi.Regions())
            {
                var poly = new Polygon();
                poly.points = region
                    .Select((v, i) => new PtWSgmnts { pos = v })
                    .ToList();
                polygons.Add(poly);
            }

            Debug.Log($"Created polygons: {polygons.Count}");
            return polygons;
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
        private void DrawFace(Polygon face, Color color, Vector2? shift = null)
        {
            DrawFace(face.points, color, shift);
        }
        private void DrawFace(List<PtWSgmnts> face, Color color, Vector2? shift = null)
        {
            DrawFace(face.Select(p => p.pos).ToList(), color, shift);
        }


        private void DrawFace(List<Vector2> face, Color color, Vector2? shift = null)
        {
            Gizmos.color = color;
            var shiftVal = shift.HasValue ? shift.Value : Vector2.zero;
            for (int i = 0; i < face.Count; i++)
            {
                var a = face[i];
                var b = face[(i + 1) % face.Count];
                Gizmos.DrawLine(a + shiftVal, b + shiftVal);
                Gizmos.DrawSphere(a + shiftVal, 0.1f);
            }
        }


        List<List<Vector2>> tempRegions = new List<List<Vector2>>();

        public void OnDrawGizmos()
        {
            var shift150_150 = new Vector2(150, -150f);
            var shift150_0 = new Vector2(0, 0f);

            

            for (int i = 0; i < districtFaces.Count; i++)
            {
                Color c = Color.HSVToRGB((float)i / districtFaces.Count, 1f, 1f);
                c = Color.black;
                DrawFace(districtFaces[i].points, c, shift150_0);
                //DrawFace(districtFaces[i].points, c, shift150_150);
                shift150_150 += new Vector2(.1f, -.1f);
            }
           

            Gizmos.color = Color.white;
            GizmosDrawer.DrawSpheres(mainRoadDirs, 1.5f);
            foreach (var lot in allLots)
            {
                DrawFace(lot, Color.white, shift150_0);
            }

            foreach (var lot in badLots)
            {
                DrawFace(lot, Color.gray, shift150_0);
            }

            Gizmos.color = Color.red;
            GizmosDrawer.DrawSpheres(badPoints, .2f);


            for (int i = 0; i < blocks.Count; i++)
            {
                //Color c = Color.HSVToRGB((float)i / blocks.Count, 1f, 1f);
                Color c = Color.cyan;
                DrawFace(blocks[i].points, c, shift150_0);
                //DrawFace(blocks[i].points, c, shift150_150);
                shift150_150 += new Vector2(.04f, -.04f);
            }
            var innerCirColor = BuildWall ? Color.yellow : Color.red ;
            
            DrawFace(innerCircle, innerCirColor, shift150_0);
            DrawFace(outerWall, Color.yellow, shift150_0);

            DrawFace(innerMoat, Color.blue, shift150_0);
            DrawFace(outerMoat, Color.blue, shift150_0);
            DrawFace(roadAroundMoat, Color.white, shift150_0);
            DrawFace(roadAroundMoatOuter, Color.white, shift150_0);


            Gizmos.color = Color.red;
            GizmosDrawer.DrawSpheres(voronoi.HullPointsInOrder(), .2f);

            Gizmos.color = Color.white;
            foreach (var mainRoad in s.mainRoads)
            {
                GizmosDrawer.DrawVectorList(mainRoad.points);
            }

            Gizmos.color = Color.green;
            GizmosDrawer.DrawSpheres(gatesCandidates, 0.1f);

            Gizmos.color = Color.white;
            //GizmosDrawer.DrawSegments(tempEdges);
            Gizmos.color = Color.red;
            GizmosDrawer.DrawSegments(GraphHelpers.intersectingSegments);

            Color[] colors = new Color[] {Color.red, Color.yellow, Color.green, Color.blue, Color.magenta, Color.cyan, Color.black };
            Gizmos.color = Color.gray;
            //GizmosDrawer.DrawVectorList(s.OuterCircle, true, .1f);
            //GizmosDrawer.DrawVectorList(s.InnerCircle, true,  .1f);
            //Gizmos.color = Color.white;
            //var shift = new Vector2(0, -151);
            //GizmoDrawer.DrawSegments(allEdges, shift);
        }
    }
}