using Assets.Game.Scripts.Editors;
using Delaunay.Geo;
using UnityEngine;
using Assets.Game.Scripts.Gen.Models;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.InputSystem.LowLevel;
using Assets.Game.Scripts.Utility;

namespace Assets.Game.Scripts.Gen.GraphGenerator
{
    public class DetectGraphCycles
    {
        private static HashSet<int> usedDegree2Ids;

        public static List<List<PtWSgmnts>> FindMinimalCycles(List<LineSegment> edges, int maxCycleLength = 10)
        {
            var graph = BuildGraph(edges);
            var cycles = new HashSet<string>();
            var result = new List<List<PtWSgmnts>>();
            usedDegree2Ids = new HashSet<int>();

            var keys = graph.Keys.ToList();
            for(int i = 0; i < keys.Count; i++)
            {
                graph[keys[i]] = graph[keys[i]].OrderBy(n => n.Id).ToList();
            }
             

            foreach (var start in graph.Keys)
            {
                if (graph[start].Count == 2 && usedDegree2Ids.Contains(start.Id))
                    continue;

                DFS(start, start, graph, new List<PtWSgmnts>(), new HashSet<PtWSgmnts>(), cycles, result, maxCycleLength);
            }

            return result;
        }

        private static void DFS(PtWSgmnts current, PtWSgmnts start, Dictionary<PtWSgmnts, List<PtWSgmnts>> graph,
                                 List<PtWSgmnts> path, HashSet<PtWSgmnts> visited,
                                 HashSet<string> cycles, List<List<PtWSgmnts>> result,
                                 int maxCycleLength)
        {
            path.Add(current);
            visited.Add(current);

            foreach (var neighbor in graph[current])
            {
                if (neighbor == start && path.Count >= 3 && path.Count <= maxCycleLength)
                {
                    var normalized = NormalizeCycle(path);
                    var key = GetCycleKey(normalized);

                    if (!cycles.Contains(key) && IsValidCycle(normalized))
                    {
                        cycles.Add(key);
                        result.Add(new List<PtWSgmnts>(normalized));

                        foreach (var pt in normalized)
                        {
                            if (graph[pt].Count == 2)
                                usedDegree2Ids.Add(pt.Id);
                        }
                    }
                }
                else if (!visited.Contains(neighbor) && path.Count < maxCycleLength)
                {
                    if (graph[neighbor].Count == 2 && usedDegree2Ids.Contains(neighbor.Id))
                        continue;

                    DFS(neighbor, start, graph, path, new HashSet<PtWSgmnts>(visited), cycles, result, maxCycleLength);
                }
            }

            path.RemoveAt(path.Count - 1);
        }

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
        private static string GetCycleKey2(List<PtWSgmnts> cycle)
        {
            var ids = cycle.Select(p => p.Id).OrderBy(id => id).ToList();

            // rotacja
            int minIndex = ids.IndexOf(ids.Min());
            var rotated = ids.Skip(minIndex).Concat(ids.Take(minIndex)).ToList();
            var rotatedRev = rotated.AsEnumerable().Reverse().ToList();

            string keyFwd = string.Join(";", rotated);
            string keyRev = string.Join(";", rotatedRev);

            return string.CompareOrdinal(keyFwd, keyRev) <= 0 ? keyFwd : keyRev;
        }
        private static string GetCycleKey(List<PtWSgmnts> cycle)
        {
            var ids = cycle.Select(p => p.Id).OrderBy(id => id).ToList();
            var fwd = string.Join(";", ids);
            var rev = string.Join(";", ids.Reversed());
            return string.CompareOrdinal(fwd, rev) <= 0 ? fwd : rev;
        }

        private static bool IsValidCycle(List<PtWSgmnts> cycle)
        {
            if (cycle.Count < 3) return false;

            float area = 0f;
            for (int i = 0; i < cycle.Count; i++)
            {
                var a = cycle[i].pos;
                var b = cycle[(i + 1) % cycle.Count].pos;
                area += (a.x * b.y - b.x * a.y);
            }
            area = Mathf.Abs(area) * 0.5f;
            if (area < 0.001f) return false;

            // filtruj cykle z martwymi końcami
            foreach (var pt in cycle)
            {
                int inCycleCount = 0;
                foreach (var n in pt.Neighbours)
                {
                    if (cycle.Contains(n))
                        inCycleCount++;
                }

                if (inCycleCount < 2)
                    return false;
            }

            return true;
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

        private static string UndirectedKey(int a, int b)
        {
            return a < b ? $"{a}_{b}" : $"{b}_{a}";
        }

        public static void DetectCyclesSkippingEdges(List<LineSegment> edges, List<List<PtWSgmnts>> cycles)
        {
            var edgeSet = new HashSet<string>();
            foreach (var edge in edges)
                edgeSet.Add(UndirectedKey(edge.p0.Id, edge.p1.Id));

            foreach (var cycle in cycles)
            {
                var ids = cycle.Select(p => p.Id).ToHashSet();

                for (int i = 0; i < cycle.Count; i++)
                {
                    var a = cycle[i];
                    var b = cycle[(i + 1) % cycle.Count];
                    var key = UndirectedKey(a.Id, b.Id);

                    // Ten edge występuje fizycznie w cyklu
                    edgeSet.Remove(key);
                }

                // Teraz: czy cykl „zawiera” punkty jakiejś krawędzi, ale nie samą krawędź?
                foreach (var edge in edges)
                {
                    var key = UndirectedKey(edge.p0.Id, edge.p1.Id);
                    if (!edgeSet.Contains(key)) continue;

                    if (cycle.Any(p => p.Id == edge.p0.Id) && cycle.Any(p => p.Id == edge.p1.Id))
                    {
                        Debug.LogWarning($"⚠️ Cykl zawiera punkty {edge.p0.Id} i {edge.p1.Id}, ale nie krawędź {key} – możliwe przeskoczenie ściany");
                    }
                }
            }
        }

        public static List<List<PtWSgmnts>> TrySplitInvalidCycles(List<LineSegment> edges, List<List<PtWSgmnts>> cycles, int maxCycleLength = 10)
        {
            var edgeSet = new HashSet<string>(edges.Select(e => UndirectedKey(e.p0.Id, e.p1.Id)));
            var edgeDict = new Dictionary<string, (PtWSgmnts, PtWSgmnts)>();
            foreach (var e in edges)
            {
                var k = UndirectedKey(e.p0.Id, e.p1.Id);
                if (!edgeDict.ContainsKey(k))
                    edgeDict[k] = (e.p0, e.p1);
            }

            var result = new List<List<PtWSgmnts>>();
            foreach (var cycle in cycles)
            {
                var cycleEdgeSet = new HashSet<string>();
                for (int i = 0; i < cycle.Count; i++)
                {
                    var a = cycle[i];
                    var b = cycle[(i + 1) % cycle.Count];
                    cycleEdgeSet.Add(UndirectedKey(a.Id, b.Id));
                }

                bool broken = false;
                foreach (var missing in edgeSet)
                {
                    if (cycleEdgeSet.Contains(missing)) continue;
                    var (a, b) = edgeDict[missing];

                    if (cycle.Any(p => p.Id == a.Id) && cycle.Any(p => p.Id == b.Id))
                    {
                        // potencjalne przeskoczenie ściany — próbujemy rozbić
                        var newEdges = edges.ToList();
                        newEdges.Add(new LineSegment(a, b));

                        var candidates = FindMinimalCycles(newEdges, maxCycleLength);
                        var filtered = candidates.Where(c => c.Any(p => cycle.Contains(p)) && c.Count < cycle.Count).ToList();

                        if (filtered.Count >= 2)
                        {
                            result.AddRange(filtered);
                            broken = true;
                            break;
                        }
                    }
                }

                if (!broken)
                    result.Add(cycle);
            }

            return result;
        }
    }
}