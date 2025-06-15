
using Assets.Game.Scripts.Editors;
using Assets.Game.Scripts.Gen.GraphGenerator;
using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Utility;
using Assets.Game.Scripts.Utility.NotAccessible;
using ClipperLib;
using Delaunay;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using SharpGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

namespace Assets.Game.Scripts.Gen
{
    //rozszerzyæ to o np. wykrywanie wysp (oddzielnych komponentów grafu), samotnych wierzcho³ków albo topologiczne „dziury”.
    //Seed with very small triangle or strange wields: 68e7c8ae-9808-4335-ab18-2a4de60b7804
    //See with strange wields      : ff89438a-e1d3-422c-b6e9-aa88191b2c12
    public static class PolygonMerge
    {
        public static Polygon MergeAdjacentPolygons(params Polygon[] polygonsToMerge)
        {
            return MergeAdjacentPolygons(polygonsToMerge.ToList());
        }

        public static Polygon MergeAdjacentPolygons(List<Polygon> polygonsToMerge)
        {
            Log.InfoToFile("MergeAdjacentPolygons");
            List<Polygon> initPolys = polygonsToMerge.ToList();
            List<PtWSgmnts> initPts = initPolys.SelectMany(p => p.Points).Distinct(new PointsComparer(true)).ToList();
            
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < polygonsToMerge.Count; i++)
                {
                    for (int j = i + 1; j < polygonsToMerge.Count; j++)
                    {
                        if (TryMergePolygons(polygonsToMerge[i], polygonsToMerge[j], out Polygon merged))
                        {
                            polygonsToMerge[i] = merged;
                            polygonsToMerge.RemoveAt(j);
                            changed = true;
                            break;
                        }
                    }
                    if (changed) break;
                }
            }
            var mergedPoly = polygonsToMerge.First();
            if (polygonsToMerge.Count > 1)
            {
                Debug.LogError("Polygons not merged correctly");
            }
            else
            {
                initPolys.ForEach(p => p.Clear());
                mergedPoly.ReplacePointsWithSamePos(initPts);

                mergedPoly.Points.ForEach(p => p.AddParentPolygon(mergedPoly));
            }
            Log.InfoToFile("MergeAdjacentPolygons done.");
            return polygonsToMerge.First();
        }

        static void StraightenOpenAnglesWithLenRatio(Polygon poly, List<Polygon> neighbours, float minRatio, bool draw = false)
        {
            Log.InfoToFile("Starting method StraightenOpenAnglesWithLenRatio");

            int total = 0;
            int count = 0;
            do
            {
                var angles = poly.GetInnerAngles();
                Debug.Log(angles.Join('_'));
                count = 0;
                for (int i = 0; i < angles.Count; i++)
                {
                    if (angles[i] > 180)
                    {
                        var ratio = CalculateEdgeLengthRatio(poly, i);
                        if (ratio > minRatio)
                        {
                            var polyPoints = poly.Count;
                            if (poly[i].parentCount == 2)
                            {
                                count += poly[i].RemoveFromParentPolygons();
                                angles = poly.GetInnerAngles();
                                i--;
                            }
                            else
                            {
                                Vector2 inters = poly.GetPerIntersWithNeighbours(i);
                                poly[i].pos = inters;
                            }
                        }
                    }
                }
            }
            while (count > 0 && total++ < 500);
            if (total == 500)
                Debug.LogError($"While iterations exceeded total");
        }

        static void AbsorbNeighbWithOpenAnglesWithLenRatio(Polygon poly, List<Polygon> neighbours, float maxRatio)
        {
            var angs = poly.GetInnerAngles();
            Debug.Log(angs.Join(','));
            int total = 0;
            for (int i = 0; i < angs.Count; i++)
            {
                if (angs[i] > 250)
                {
                    total++;
                    var ratio = CalculateEdgeLengthRatio(poly, i);
                    Debug.Log($"RATIO: {ratio}");
                    if (ratio < 2)
                    {
                        var neighs = poly[i].Parents.Except(poly).ToList();
                        var neighByPos = neighbours.Where(n => n.ContainsCheckpoint(poly[i])).ToList();
                        Debug.Log($"neighs COUNT: {neighs.Count}, byPos: {neighByPos.Count}");
                        if (neighs.Count == 1)
                        {
                            var pt = poly[i];

                            pt.RemoveFromParentPolygon(poly);
                            pt.RemoveFromParentPolygon(neighs.First());
                            poly.AbsorbPolygon(neighs.First());

                            poly.RemoveCheckPoint(pt);
                            neighs.First().RemoveCheckPoint(pt);                            
                            neighbours.Remove(neighs.First());
                            i = 0;
                            angs = poly.GetInnerAngles();
                        }
                    }
                }
            }
            if (total > 1000)
            {
                Debug.LogError("Total iterations exceeded 1000!.");
                return;
            }
        }
        public static void IncludeDeepNeighbours(Polygon poly, List<Polygon> neighbours)
        {
            Log.InfoToFile("Starting method IncludeDeepNeighbours");
            StraightenOpenAnglesWithLenRatio(poly, neighbours, 2);
            Log.InfoToFile("Starting method ReplacePointsWithSamePos");
            ReplacePointsWithSamePos(neighbours, poly.Points);
            Log.InfoToFile("Starting method RemoveNotExistingRelationShips");
            RemoveNotExistingRelationShips(neighbours);
            Log.InfoToFile("Starting method AbsorbNeighbWithOpenAnglesWithLenRatio");
            AbsorbNeighbWithOpenAnglesWithLenRatio(poly, neighbours, 2);
            Log.InfoToFile("Starting method ReplacePointsWithSamePos");
            ReplacePointsWithSamePos(neighbours, poly.Points);
            Log.InfoToFile("Starting method ReplacePointsWithSamePos");
            StraightenOpenAnglesWithLenRatio(poly, neighbours, 1, true);
            Log.InfoToFile("Starting method RemoveAnglesAround180Degrees");
            RoadGraphGenChaos.RemoveAnglesAround180Degrees(poly, neighbours);
            Log.InfoToFile("IncludeDeepNeighbours done");
        }


        public static void ReplacePointsWithSamePos(List<Polygon> polygons)
        {
            var uniquePts = polygons.SelectMany(p => p.Points).Distinct(new PointsComparer()).ToList();
            ReplacePointsWithSamePos(polygons, uniquePts);            
        }

        public static void ReplacePointsWithSamePos(List<Polygon> polygons, List<PtWSgmnts> points)
        {
            polygons.ForEach(n => n.ReplacePointsWithSamePos(points));
        }


        public static void RemoveNotExistingRelationShips(List<Polygon> polys)
        {
            var totalPoints = polys.SelectMany(p => p.Points).Distinct(new PointsComparer(true)).ToList();
            totalPoints.ForEach(p => p.RemoveParentsIfNotPartOf());
        }

        static float CalculateEdgeLengthRatio(Polygon poly, int i)
        {
            var prevLen = poly[i].DistanceTo(poly.Neighbour(i, -1));
            var nextLen = poly[i].DistanceTo(poly.Neighbour(i, 1));
            var min = Math.Min(prevLen, nextLen);
            var max = Math.Max(prevLen, nextLen);
            var ratio = max / min;
            return ratio;
        }

        private static bool TryMergePolygons(Polygon a, Polygon b, out Polygon merged)
        {
            var edgesA = GetEdges(a);
            var edgesB = GetEdges(b);

            // ZnajdŸ wspólne krawêdzie (w przeciwnym kierunku)
            var shared = new HashSet<(Vector2, Vector2)>(
                edgesA.Select(e => (e.Item2, e.Item1)) // odwrócone
                .Intersect(edgesB)
            );

            if (shared.Count == 0)
            {
                merged = null;
                return false;
            }

            // Po³¹cz punkty obu polygonów i usuñ wspólne krawêdzie
            var combinedEdges = new List<(Vector2, Vector2)>();
            combinedEdges.AddRange(edgesA);
            combinedEdges.AddRange(edgesB);

            combinedEdges.RemoveAll(e => shared.Contains(e) || shared.Contains((e.Item2, e.Item1)));

            // Posk³adaj z krawêdzi nowy polygon (z³o¿ony z jednej pêtli)
            var path = ReconstructLoop(combinedEdges);
            merged = new Polygon(path.Select((p, idx) => new PtWSgmnts { pos = p }).ToList());
            return true;
        }

        private static List<(Vector2, Vector2)> GetEdges(Polygon poly)
        {
            var pts = poly.Points.Select(p => p.pos).ToList();
            var edges = new List<(Vector2, Vector2)>();
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                edges.Add((a, b));
            }
            return edges;
        }

        private static List<Vector2> ReconstructLoop(List<(Vector2, Vector2)> edges)
        {
            var edgeDict = edges
                .GroupBy(e => e.Item1)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Item2).ToList());

            var result = new List<Vector2>();
            if (edges.Count == 0) return result;

            Vector2 start = edges[0].Item1;
            result.Add(start);
            Vector2 current = edges[0].Item2;

            int total = 0;
            while (current != start && total++ < 500)
            {
                result.Add(current);
                if (!edgeDict.ContainsKey(current) || edgeDict[current].Count == 0)
                    break;

                var next = edgeDict[current][0];
                edgeDict[current].RemoveAt(0);
                current = next;
            }
            if (total == 500)
                Debug.LogError($"While iterations exceeded total");
            return result;
        }

        public static void MergeClosePoints(List<Polygon> polys, float epsilon = .1f)
        {
            foreach(var poly in polys)
            {
                MergeClosePoints(poly, epsilon);
            }
        }

        public static void MergeClosePoints(Polygon pol, float epsilon = .1f)
        {
            pol.RemoveDuplicates();
            for (int i = pol.Count - 1; i >= 0; i--)
            {                
                var pt = pol[i];
                var prevP = pol.Neighbour(i, -1);
                var prevIndex = pol.Points.IndexOf(prevP);

                if (pt.DistanceTo(prevP) < epsilon)
                {
                    MergePoints(pt, prevP);
                }
            }
        }

        public static void MergePoints(PtWSgmnts ptToRemain, PtWSgmnts ptToRemove)
        {            
            if(ptToRemain.Id == ptToRemove.Id)
            {
                return;
            }
            ptToRemain.AddParentPolygons(ptToRemove.Parents);
            
            for (int i = ptToRemain.Parents.Count - 1; i >= 0; i--)
            {
                var p = ptToRemain.Parents[i];
                var indexOfPtToRemove = p.Points.IndexOf(ptToRemove);
                p.InsertCheckpoint(ptToRemain, indexOfPtToRemove);
                p.RemoveCheckPoint(ptToRemove);
            }
        }
        public static void MergePoints(PtWSgmnts ptToRemain, params PtWSgmnts[] ptsToRemove)
        {
            foreach(var ptToRemove in ptsToRemove)
            {
                MergePoints(ptToRemain, ptToRemove);
            }
        }

        public static void FlattenAdjacentTriangles(List<Polygon> polys, Polygon boundaryPoly)
        {
            var innerCircCenter = boundaryPoly.FindCenter();
            var neighbourBlocks = polys.Where(b => b.Points.Any(p => boundaryPoly.ContainsPoint(p))).ToList();
            Debug.LogWarning($"NEIGHBOURS: {neighbourBlocks.Count}");
            foreach (var neigh in neighbourBlocks)
            {
                neigh.RemovePointsWithSamePos();
                var neighPtsOnBoundary = boundaryPoly.ContainsCheckpointsByPos(neigh.Points);

                PtWSgmnts furthestPt = null;
                try
                {
                    furthestPt = neigh.Points.First(p => !boundaryPoly.ContainsCheckpointPos(p));
                }
                catch (Exception e)
                {
                    GizmosDrawer.DrawRays(neigh.Points, Color.red);
                    return;
                }   
                
                if (neigh.Count == neighPtsOnBoundary + 1)
                {
                    var remainingPts = neigh.Points.Where(p => boundaryPoly.ContainsCheckpointPos(p)).ToList();
                    furthestPt = neigh.Points.First(p => !boundaryPoly.ContainsCheckpointPos(p));
                    Vector2 newPos = Vector2.zero;
                    try
                    {
                        if(neigh.Count == 3)
                            newPos = Vector.GetPerpendicularIntersection(remainingPts[0].pos, remainingPts[1].pos, furthestPt.pos);
                        else
                        {
                            newPos = neigh.Neighbour(furthestPt, 2).pos;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message);
                        Debug.DrawRay(furthestPt.pos, Vector2.up, Color.red);
                        if (remainingPts.Any())
                        {
                            Debug.DrawRay(remainingPts[0].pos, Vector2.up, Color.blue);
                            Debug.DrawRay(remainingPts[1].pos, Vector2.up, Color.blue);
                        }
                        break;
                    }
                    polys.SelectMany(p => p.GetCheckpointsByPos(furthestPt))
                         .ToList()
                         .ForEach(p => { p.pos = newPos; });
                    polys.Remove(neigh);

                    boundaryPoly.InsertCheckpointByPos(newPos);
                }
            }
            RemoveTooSmallPolygons(polys, boundaryPoly);
        }

        public static List<Polygon> RemoveTooSmallPolygons(List<Polygon> polys, Polygon innerCircle)
        {
            var polysToRemove = new List<Polygon>();
            foreach(var poly in polys)
            {
                if (poly.CalculateArea() < 1)
                {
                    var innerCircPts = innerCircle.Points.Where(p => poly.ContainsCheckpoint(p)).ToList();
                    var centerPt = innerCircPts.Any()? innerCircPts[0] : poly.Points.First();
                    foreach (var pt in poly.Points)
                    {
                        pt.pos = centerPt.pos;
                    }
                    MergePoints(centerPt, poly.Points.Except(centerPt).ToArray());
                    polysToRemove.Add(poly);
                }
            }
            return polys.Except(polysToRemove).ToList();
        }
    }
}