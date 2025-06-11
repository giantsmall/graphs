
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
using UnityEngine.Rendering.Universal;

namespace Assets.Game.Scripts.Gen
{
    //rozszerzyæ to o np. wykrywanie wysp (oddzielnych komponentów grafu), samotnych wierzcho³ków albo topologiczne „dziury”.
    //Seed with very small triangle or strange wields: 68e7c8ae-9808-4335-ab18-2a4de60b7804
    //See with strange wields      : ff89438a-e1d3-422c-b6e9-aa88191b2c12
    public static class PolygonMerge
    {
        public static List<Polygon> MergeAdjacentPolygons(List<Polygon> polygons)
        {
            bool changed = true;

            while (changed)
            {
                changed = false;

                for (int i = 0; i < polygons.Count; i++)
                {
                    for (int j = i + 1; j < polygons.Count; j++)
                    {
                        if (TryMergePolygons(polygons[i], polygons[j], out Polygon merged))
                        {
                            polygons[i] = merged;
                            polygons.RemoveAt(j);
                            changed = true;
                            break;
                        }
                    }

                    if (changed) break;
                }
            }

            return polygons;
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

            merged = new Polygon
            {
                points = path.Select((p, idx) => new PtWSgmnts { pos = p}).ToList()
            };

            return true;
        }

        private static List<(Vector2, Vector2)> GetEdges(Polygon poly)
        {
            var pts = poly.points.Select(p => p.pos).ToList();
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