using Assets.Game.Scripts.Gen.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace Assets.Game.Scripts.Gen
{
    public static class FaceExtractor
    {
        public static List<Polygon> ExtractMinimalCoveringFacesIterative(List<LineSegment> edges, int iterations = 10)
        {
            var allEdges = new HashSet<string>(edges.Select(e => EdgeKey(e.p0.Id, e.p1.Id)));
            var allCycles = new List<List<PtWSgmnts>>();
            var usedKeys = new HashSet<string>();

            var rng = new System.Random();

            for (int i = 0; i < iterations; i++)
            {
                var cycles = ExtractFacesRandomized(edges, rng);
                foreach (var cycle in cycles)
                {
                    string key = GetCycleKey(cycle);
                    if (!usedKeys.Contains(key))
                    {
                        allCycles.Add(cycle);
                        usedKeys.Add(key);
                    }
                }
            }

            var edgeCovered = new HashSet<string>();
            var result = new List<List<PtWSgmnts>>();

            foreach (var cycle in allCycles.OrderBy(c => c.Count))
            {
                var cycleEdges = new List<string>();
                for (int i = 0; i < cycle.Count; i++)
                {
                    var a = cycle[i];
                    var b = cycle[(i + 1) % cycle.Count];
                    var k = EdgeKey(a.Id, b.Id);
                    cycleEdges.Add(k);
                }

                if (cycleEdges.Any(k => !edgeCovered.Contains(k)))
                {
                    result.Add(cycle);
                    foreach (var k in cycleEdges)
                        edgeCovered.Add(k);
                }

                if (edgeCovered.SetEquals(allEdges))
                    break;
            }

            return result.Select(l => new Polygon(l)).ToList();
        }
        
        private static List<List<PtWSgmnts>> ExtractFacesRandomized(List<LineSegment> edges, System.Random rng)
        {
            var graph = BuildGraph(edges);
            var visitedEdges = new HashSet<string>();
            var faces = new List<List<PtWSgmnts>>();

            foreach (var kvp in graph.ToList())
            {
                var center = kvp.Key.pos;
                graph[kvp.Key] = kvp.Value
                    .OrderBy(n => Mathf.Atan2(n.pos.y - center.y, n.pos.x - center.x) + rng.NextDouble() * 0.1)
                    .ToList();
            }

            foreach (var from in graph.Keys.OrderBy(_ => rng.Next()))
            {
                foreach (var to in graph[from])
                {
                    string edgeKey = EdgeKey(from.Id, to.Id);
                    if (visitedEdges.Contains(edgeKey)) continue;

                    var face = WalkFace(from, to, graph, visitedEdges);
                    if(face != null && IsValidCycle(face, graph))
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

                if (face.Contains(next)) return null;

                face.Add(next);
                visitedEdges.Add(EdgeKey(current.Id, next.Id));
                prev = current;
                current = next;
            }

            return face;
        }

        private static bool IsValidCycle(List<PtWSgmnts> cycle, Dictionary<PtWSgmnts, List<PtWSgmnts>> graph)
        {
            if (cycle.Count < 3) return false;
            for (int i = 0; i < cycle.Count; i++)
            {
                var prev = cycle[(i - 1 + cycle.Count) % cycle.Count];
                var next = cycle[(i + 1) % cycle.Count];
                var current = cycle[i];

                if (!(graph[current].Contains(prev) && graph[current].Contains(next)))
                    return false;
            }

            var unique = new HashSet<int>(cycle.Select(p => p.Id));
            if (unique.Count != cycle.Count) return false; // duplikaty

            return true;
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