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
    public class RoadGraphGenChaosNew : MonoBehaviour
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
        public float OuterVoronoiSize = 10f;
        public float InnerVoronoiSize = 5f;
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
            allEdges.Clear();

            PtWSgmnts.ResetCount();
            LineSegment.ResetCount();
            ClearLog();
            Population = 600;

            var center = new Vector2(MapSize / 2f, MapSize / 2f);
            center += new Vector2(rnd.NextFloat(-5, 5), rnd.NextFloat(-5, 5));
            s = new SettlementModel(new PtWSgmnts(center));

            var innerCircleRadius = Mathf.Sqrt(Population);
            s.OuterCircle = new(CreateCircle(innerCircleRadius * 2));
            SplitouterCircle();

            s.InnerCircle = new(CreateCircle(innerCircleRadius));
        }

        internal static System.Random GetRandom()
        {
            return rnd;
        }

        List<PtWSgmnts> CreateCircle(float wallRadius)
        {
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
            return pts.Select(p => new PtWSgmnts(p)).ToList();
        }

        
        void SplitouterCircle()
        {
            var outerCircle = s.OuterCircle;
            var outerPolygon = new Polygon(outerCircle);
            var center = outerPolygon.FindCenter();

            var rect = outerPolygon.GetRectangleCircumscribedInPolygon(.05f);
            var pts = PoissonDiscSampler2D.GeneratePoints(rnd, OuterVoronoiSize, rect);

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
                    pts = PoissonDiscSampler2D.GeneratePoints(rnd, OuterVoronoiSize, rect);
                    v = new Voronoi(pts, rect);
                }
            }
            var edges = v.VoronoiDiagram();
            allEdges.AddRange(edges);

            JoinSamePosPtsAndRemoveEmptyLines(allEdges);
            RemoveDuplicates();
            RemoveLevel1Edges(ref allEdges);
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

        int JoinSamePosPtsAndRemoveEmptyLines(List<LineSegment> edges)
        {
            int count = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = 0; j < edges.Count; j++)
                {
                    if (i != j)
                    {
                        count += edges[j].ReplaceEdgePointWithSamePosOLD(edges[i]);
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

            for (int i = 0; i < edges.Count; i++)
            {
                //p0 neighbour edges
                //p1 niehgbours edges
            }


            if (count > 0)
                Debug.Log($"______________> For {edges.Count} edges merged {count} points with same coords");
            if (count2 > 0)
                Debug.Log($"______________> For {edges.Count} edges removed {count2} with 0 length");
            return count;
        }

        void RemoveLevel1Edges(ref List<LineSegment> edges)
        {
            var edgesToRemove = new List<LineSegment>();
            do
            {
                edgesToRemove.Clear();
                foreach (var edge in edges)
                {
                    if (edge.p0.Neighbours.Count < 2)
                    {
                        edge.p1.Neighbours.Remove(edge.p0);
                        edge.p1.Neighbours = edge.p1.Neighbours.Distinct(new PointsComparer(true)).ToList();
                        edgesToRemove.Add(edge);
                    }

                    if (edge.p1.Neighbours.Count < 2)
                    {
                        edge.p0.Neighbours.Remove(edge.p1);
                        edge.p0.Neighbours = edge.p0.Neighbours.Distinct(new PointsComparer(true)).ToList();
                        edgesToRemove.Add(edge);
                    }
                }

                Debug.LogWarning($"Level1Edges count = {edgesToRemove.Count}");
                edges = edges.Except(edgesToRemove).ToList();
            }
            while (edgesToRemove.Any());

            var shift = new Vector2(.3f, .3f);
            foreach (var edge in edgesToRemove)
            {
                Debug.DrawLine(edge.p0.pos + shift, edge.p1.pos + shift, Color.red);
            }

            if (!edgesToRemove.Any())
            {
                Debug.LogWarning("No points with neighbour leve 2 or above..");
            }
        }

        int IdentifyIntersections(List<LineSegment> edges, List<PtWSgmnts> limit, Vector2 center)
        {
            int count = 0;

            for (int j = 0; j < edges.Count; j++)
            {
                var edge = edges[j];
                for (int i = 0; i < limit.Count; i++)
                {
                    var pt = limit[i];
                    var nextPt = limit.Neighbour(i, 1);
                    if (pt.Id == nextPt.Id)
                    {

                    }
                    var intersections = IdentifyIntersection(pt, nextPt, edge, limit, i, center, false);
                    if (intersections.Count >= 1)
                    {
                        i++;
                        count += intersections.Count;
                    }
                }
            }

            Debug.Log($"Identified intersections and shortened {count} edges out of {edges.Count}");
            return count;
        }
        List<PtWSgmnts> IdentifyIntersection(PtWSgmnts pt, PtWSgmnts nextPt, LineSegment edge, List<PtWSgmnts> limit, int i, Vector2 center, bool MakeMovable = false)
        {
            List<PtWSgmnts> localInters = new();

            var inters = VectorIntersect.GetIntersectionPoint(pt, nextPt, edge);
            if (inters.HasValue)
            {
                var intersPt = new PtWSgmnts(inters.Value, MakeMovable);

                var street = limit;
                if (street != null)
                {
                    var orderedCircles = street.OrderBy(p => p.DistanceTo(intersPt)).ToList();
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
                        var index1 = street.IndexOf(pt);
                        var index2 = street.IndexOf(nextPt);
                        if (index1 < 0 || index2 < 0)
                        {
                            //Debug.LogError($"Street {street.Id} does not contain both {pt.Id} and {nextPt.Id}");
                            return localInters;
                        }


                        //limit.InsertCheckpoint(intersPt, insertIndex);
                        //localInters.Add(intersPt);
                        //if (InsertIntersections)
                        //    street.InsertCheckpoint(intersPt, Math.Max(index1, index2));
                    }
                }
                else
                {
                    //int insertIndex = limit.points.IndexOf(pt) + 1;
                    //if (insertIndex >= limit.points.Count)
                    //    insertIndex = 0; // wrap
                    //limit.InsertCheckpoint(intersPt, insertIndex);
                    //localInters.Add(intersPt);

                    //var streets = limit.bStr.Where(s => s.ContainsCheckpoint(nextPt) || s.ContainsCheckpoint(nextPt)).ToList();
                    //foreach (var str in streets)
                    //{
                    //    var indexOfPt = str.points.IndexOf(pt);
                    //    var indexOfNextPt = str.points.IndexOf(nextPt);
                    //    insertIndex = (indexOfPt > -1 ? indexOfPt : indexOfNextPt);
                    //    if (InsertIntersections)
                    //        str.InsertCheckpoint(intersPt, insertIndex);
                    //}
                }

                //edge.ReplaceOutsideEdgePtReturnOld(center, intersPt, limit);
                nextPt.AddNeighbours(intersPt);
                pt.AddNeighbours(intersPt);
            }
            return localInters;
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
        List<PtWSgmnts> lonelyPts = new();
        Dictionary<District, List<LineSegment>> edgesDict = new();
        Dictionary<District, List<PtWSgmnts>> nodeDict = new();
        List<PtWSgmnts> allNodes = new List<PtWSgmnts>();
        List<LineSegment> allEdges = new List<LineSegment>();


        public void OnDrawGizmos()
        {
            Color[] colors = new Color[] {Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan, Color.cyan, Color.cyan, Color.cyan, Color.cyan };

            Gizmos.color = Color.gray;
            GizmoDrawer.DrawVectorList(s.OuterCircle, true,    .1f);

            Gizmos.color = Color.white;
            GizmoDrawer.DrawSegments(allEdges);
            var shift = new Vector2(.1f, .1f);

            foreach (var edge in allEdges)
            {
                if(edge.p0.Neighbours.Count == 1)
                {
                    foreach(var n in edge.p0.Neighbours)
                    {
                        Gizmos.color = Color.black;
                        Gizmos.DrawSphere(n.pos + shift, .5f);
                    }
                }

                if (edge.p1.Neighbours.Count == 1)
                {
                    foreach (var n in edge.p1.Neighbours)
                    {
                        Gizmos.color = Color.black;
                        Gizmos.DrawSphere(n.pos + shift, .5f);
                        shift += new Vector2(.1f, .1f);
                    }
                }
            }

            foreach (var edge in allEdges)
            {
                Gizmos.color = colors[edge.p0.Neighbours.Count];
                Gizmos.DrawSphere(edge.p0.pos, .3f);
                Gizmos.color = colors[edge.p1.Neighbours.Count];
                Gizmos.DrawSphere(edge.p1.pos, .3f);                
            }
        }
    }
}