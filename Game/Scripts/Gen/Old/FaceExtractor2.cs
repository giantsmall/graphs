using Assets.Game.Scripts.Gen.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace Assets.Game.Scripts.Gen
{

    public class FaceExtractor2
    {
        public static List<List<PtWSgmnts>> ExtractFaces(List<LineSegment> edges)
        {
            var graph = BuildGraph(edges);
            var visitedEdges = new HashSet<string>();
            var faces = new List<List<PtWSgmnts>>();

            // Sort neighbours counter-clockwise
            foreach (var kvp in graph.ToList())
            {
                var center = kvp.Key.pos;
                graph[kvp.Key] = kvp.Value
                    .OrderBy(n => Mathf.Atan2(n.pos.y - center.y, n.pos.x - center.x))
                    .ToList();
            }

            foreach (var from in graph.Keys)
            {
                foreach (var to in graph[from])
                {
                    string edgeKey = EdgeKey(from.Id, to.Id);
                    if (visitedEdges.Contains(edgeKey)) continue;

                    var face = WalkFace(from, to, graph, visitedEdges);
                    if (face != null && face.Count > 2)
                    {
                        var normalized = NormalizeCycle(face);
                        string key = GetCycleKey(normalized);

                        if (!faces.Any(f => GetCycleKey(f) == key))
                            faces.Add(normalized);
                    }
                }
            }

            return faces;
        }

        private static List<PtWSgmnts> WalkFace(PtWSgmnts from, PtWSgmnts to,
            Dictionary<PtWSgmnts, List<PtWSgmnts>> graph, HashSet<string> visitedEdges)
        {
            var face = new List<PtWSgmnts>();
            var start = from;
            var current = to;
            var prev = from;

            face.Add(start);
            face.Add(current);
            visitedEdges.Add(EdgeKey(prev.Id, current.Id));

            while (true)
            {
                var neighbors = graph[current];
                int i = neighbors.FindIndex(p => p.Id == prev.Id);
                int nextIndex = (i + 1) % neighbors.Count;
                var next = neighbors[nextIndex];

                if (next == start)
                {
                    visitedEdges.Add(EdgeKey(current.Id, next.Id));
                    break;
                }

                if (face.Contains(next)) return null; // looped back prematurely

                face.Add(next);
                visitedEdges.Add(EdgeKey(current.Id, next.Id));
                prev = current;
                current = next;
            }

            return face;
        }

        private static string EdgeKey(int a, int b) => a < b ? $"{a}_{b}" : $"{b}_{a}";

        private static List<PtWSgmnts> NormalizeCycle(List<PtWSgmnts> cycle)
        {
            int minIndex = 0;
            for (int i = 1; i < cycle.Count; i++)
            {
                if (cycle[i].Id < cycle[minIndex].Id)
                    minIndex = i;
            }

            var rotated = new List<PtWSgmnts>();
            for (int i = 0; i < cycle.Count; i++)
                rotated.Add(cycle[(minIndex + i) % cycle.Count]);

            return rotated;
        }

        private static string GetCycleKey(List<PtWSgmnts> cycle)
        {
            var ids = cycle.Select(p => p.Id).ToList();
            var rev = ids.AsEnumerable().Reverse().ToList();
            var keyFwd = string.Join(";", ids);
            var keyRev = string.Join(";", rev);
            return string.CompareOrdinal(keyFwd, keyRev) <= 0 ? keyFwd : keyRev;
        }

        private static Dictionary<PtWSgmnts, List<PtWSgmnts>> BuildGraph(List<LineSegment> edges)
        {
            var graph = new Dictionary<PtWSgmnts, List<PtWSgmnts>>();

            foreach (var edge in edges)
            {
                var a = edge.p0;
                var b = edge.p1;

                if (!graph.ContainsKey(a))
                    graph[a] = new List<PtWSgmnts>();
                if (!graph.ContainsKey(b))
                    graph[b] = new List<PtWSgmnts>();

                if (!graph[a].Contains(b))
                    graph[a].Add(b);
                if (!graph[b].Contains(a))
                    graph[b].Add(a);
            }

            return graph;
        }
    }
}