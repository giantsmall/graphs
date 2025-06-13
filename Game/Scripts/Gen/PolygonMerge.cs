
using Assets.Game.Scripts.Editors;
using Assets.Game.Scripts.Gen.GraphGenerator;
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
                //Polygon.DrawParentCountPerPt(mergedPoly);
            }
            return polygonsToMerge.First();
        }

        public static Polygon IncludeDeepNeighbours(Polygon poly, List<Polygon> neighbours)
        {            
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
                        if (ratio > 2)
                        {
                            var polyPoints = poly.Count;

                            if (poly[i].parentCount <= 2)
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
            while (count > 0);


            var angs = poly.GetInnerAngles();
            Debug.Log(angs.Join('_'));

            foreach (var pt in poly.Points)
            {
                //GizmosDrawer.DrawRays(pt.pos, Color.red, pt.parentCount);
            }

            neighbours.ForEach(n => n.ReplacePointsWithSamePos(poly.Points));

            angs = poly.GetInnerAngles();
            for (int i = 0; i < angs.Count; i++)
            {
                if (angs[i] > 180)
                {
                    var ratio = CalculateEdgeLengthRatio(poly, i);
                    Debug.Log($"RATIO: {ratio}");

                    if (ratio < 2)
                    {
                        GizmosDrawer.DrawRay(poly[i].pos, Color.red);
                        var neighs = poly[i].Parents.ToList(); 
                        var neighByPos = neighbours.Where(n => n.ContainsCheckpoint(poly[i])).ToList();
                        Debug.Log($"neighs COUNT: {neighs.Count}, byPos: {neighByPos.Count}");

                        GizmosDrawer.DrawRays(neighByPos.Select(n => n.FindCenter()).ToList(), Color.red);

                        if (neighByPos.Count == 1)
                        {
                            //neighs.Add(poly);
                            poly.AbsorbPolygon(neighByPos.First());
                            neighbours.Remove(neighByPos.First());
                            break;
                        }
                    }
                }
                else if (angs[i] > 160)
                {
                    GizmosDrawer.DrawRay(poly[i].pos, Color.blue);

                }
            }

            return poly;
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

            while (current != start)
            {
                result.Add(current);
                if (!edgeDict.ContainsKey(current) || edgeDict[current].Count == 0)
                    break;

                var next = edgeDict[current][0];
                edgeDict[current].RemoveAt(0);
                current = next;
            }
            return result;
        }
    }
}