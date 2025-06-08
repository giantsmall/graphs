using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Game.Scripts.Gen.GraphGenerator
{    
    public class DetectGraphCycles2
    {
        public static bool ValidateCycle = true;
        public static void TryPatchMissingEdges(List<LineSegment> edges, List<List<PtWSgmnts>> cycles, int maxCycleLength = 10)
        {
            var edgeMap = edges.ToHashSet(new SegmentComparer());
            var coveredEdges = new HashSet<string>();

            foreach (var cycle in cycles)
            {
                for (int i = 0; i < cycle.Count; i++)
                {
                    var a = cycle[i];
                    var b = cycle[(i + 1) % cycle.Count];
                    coveredEdges.Add(UndirectedKey(a.Id, b.Id));
                }
            }

            var graph = BuildGraph(edges);
            var newCycles = new List<List<PtWSgmnts>>();

            foreach (var edge in edges)
            {
                var key = UndirectedKey(edge.p0.Id, edge.p1.Id);
                if (coveredEdges.Contains(key)) continue;

                // Spróbuj znaleźć cykl zawierający edge.p0 -> edge.p1
                var path = new List<PtWSgmnts>();
                var success = DFSFindCycle(edge.p1, edge.p0, graph, path, new HashSet<PtWSgmnts>(), maxCycleLength - 1);

                if (success)
                {
                    path.Reverse();
                    path.Add(edge.p1); // domykamy cykl
                    var normalized = NormalizeCycle(path);
                    var keyStr = GetCycleKey(normalized);

                    if (!cycles.Any(c => GetCycleKey(c) == keyStr))
                    {
                        Debug.LogWarning($"✅ Uzupełniono brakujący cykl dla krawędzi {key}");
                        newCycles.Add(normalized);
                    }
                }
                else
                {
                    Debug.LogError($"❌ Nie udało się znaleźć cyklu zawierającego krawędź {key}");
                }
            }

            cycles.AddRange(newCycles);
        }

        private static bool DFSFindCycle(PtWSgmnts current, PtWSgmnts target, Dictionary<PtWSgmnts, List<PtWSgmnts>> graph,
                                 List<PtWSgmnts> path, HashSet<PtWSgmnts> visited, int depthRemaining)
        {
            if (depthRemaining <= 0)
                return false;

            visited.Add(current);
            path.Add(current);

            foreach (var neighbor in graph[current])
            {
                if (neighbor == target && path.Count >= 2)
                {
                    return true;
                }

                if (!visited.Contains(neighbor))
                {
                    if (DFSFindCycle(neighbor, target, graph, path, new HashSet<PtWSgmnts>(visited), depthRemaining - 1))
                        return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        public static void AnalyzeEdgeSanity(ref List<LineSegment> edges)
        {
            var edgeSet = new HashSet<string>();
            var seenPairs = new HashSet<(int, int)>();
            var pointDuplicates = new Dictionary<int, List<Vector2>>();

            //foreach (var edge in edges)
            for (int i = 0; i < edges.Count; i++)            
            {
                var edge = edges[i];
                if (edge.p0.Id == edge.p1.Id)
                {
                    Debug.LogWarning($"❌ Self-loop: punkt {edge.p0.Id} łączy się sam ze sobą. Len = {edge.CalculateLength()}");
                    Debug.DrawRay(edge.p0.pos, Vector2.up * 4f, Color.black);
                }

                var key = UndirectedKey(edge.p0.Id, edge.p1.Id);
                if (!edgeSet.Add(key))
                {
                    Debug.LogWarning($"⚠️ Usunięto duplikat krawędzi o id = {edge.Id}: {edge.p0.Id} <-> {edge.p1.Id}. Len = {edge.CalculateLength()}");
                    //Debug.DrawRay(edge.p0.pos, Vector2.up, Color.cyan);
                    //Debug.DrawLine(edge.p0.pos, edge.p1.pos, Color.magenta);
                    //Debug.DrawRay(edge.p1.pos, Vector2.up, Color.cyan);
                    edges = edges.Where(e => e.Id != edge.Id).ToList();
                    i--;
                }
                    

                // Sprawdź, czy istnieją różne pozycje dla tego samego ID
                void TrackPt(PtWSgmnts pt)
                {
                    if (!pointDuplicates.ContainsKey(pt.Id))
                        pointDuplicates[pt.Id] = new List<Vector2>();

                    if (!pointDuplicates[pt.Id].Any(p => Vector2.Distance(p, pt.pos) < 0.001f))
                        pointDuplicates[pt.Id].Add(pt.pos);
                }

                TrackPt(edge.p0);
                TrackPt(edge.p1);
            }

            foreach (var kvp in pointDuplicates)
            {
                if (kvp.Value.Count > 1)
                {
                    Debug.LogError($"❌ Punkt Id={kvp.Key} występuje z wieloma pozycjami:");
                    foreach (var pos in kvp.Value)
                        Debug.LogError($" - {pos}");
                }
            }
        }



        public static List<LineSegment> AnalyzeInvalidEdges(List<LineSegment> edges, List<List<PtWSgmnts>> cycles, string commentPRefix = "CYCLES")
        {
            var validEdgeKeys = new HashSet<string>();
            foreach (var edge in edges)
            {
                validEdgeKeys.Add(UndirectedKey(edge.p0.Id, edge.p1.Id));
            }

            var invalidEdges = new List<LineSegment>();

            foreach (var cycle in cycles)
            {
                for (int i = 0; i < cycle.Count; i++)
                {
                    var a = cycle[i];
                    var b = cycle[(i + 1) % cycle.Count];
                    string key = UndirectedKey(a.Id, b.Id);

                    if (!validEdgeKeys.Contains(key))
                    {
                        invalidEdges.Add(new LineSegment(a, b));
                        Debug.LogWarning($"{commentPRefix}: ❌ NIEWAŻNA krawędź w cyklu: {a.Id} <-> {b.Id}");
                    }
                }
            }


            Debug.Log($"{commentPRefix}: Liczba nadmiarowych (niewystępujących w grafie) krawędzi w cyklach: {invalidEdges.Count}");
            return invalidEdges;
        }

        public static void AnalyzeCycleCoverageDetailed(List<LineSegment> edges, List<List<PtWSgmnts>> cycles, string commentPRefix = "CYCLES")
        {
            var allEdges = new HashSet<string>();
            var edgeMap = new Dictionary<string, (PtWSgmnts, PtWSgmnts)>();

            foreach (var edge in edges)
            {
                string key = UndirectedKey(edge.p0.Id, edge.p1.Id);
                allEdges.Add(key);
                edgeMap[key] = (edge.p0, edge.p1);
            }

            var usedEdges = new HashSet<string>();
            var nodeCycleCount = new Dictionary<int, int>();

            foreach (var cycle in cycles)
            {
                for (int i = 0; i < cycle.Count; i++)
                {
                    var a = cycle[i];
                    var b = cycle[(i + 1) % cycle.Count];

                    string key = UndirectedKey(a.Id, b.Id);
                    usedEdges.Add(key);

                    nodeCycleCount[a.Id] = nodeCycleCount.GetValueOrDefault(a.Id, 0) + 1;
                    nodeCycleCount[b.Id] = nodeCycleCount.GetValueOrDefault(b.Id, 0) + 1;
                }
            }

            var missingEdges = allEdges.Except(usedEdges).ToList();

            Debug.Log($"{commentPRefix}: --- ANALIZA CYKLI ---");
            Debug.Log($"{commentPRefix}: Użyte krawędzie: {usedEdges.Count} / {allEdges.Count}");
            Debug.Log($"{commentPRefix}: Brakujące krawędzie: {missingEdges.Count}");

            foreach (var key in missingEdges)
            {
                var (a, b) = edgeMap[key];

                int aCycles = nodeCycleCount.GetValueOrDefault(a.Id, 0);
                int bCycles = nodeCycleCount.GetValueOrDefault(b.Id, 0);

                Debug.LogWarning($"{commentPRefix}: Krawędź {key} nie pokryta przez żaden cykl:");
                Debug.Log($"{commentPRefix}:  - Punkt A (Id={a.Id}, deg={a.Neighbours.Count}, cycles={aCycles})");
                Debug.Log($"{commentPRefix}:  - Punkt B (Id={b.Id}, deg={b.Neighbours.Count}, cycles={bCycles})");
            }
        }

        public static List<string> AnalyzeCycleCoverage(List<LineSegment> edges, List<List<PtWSgmnts>> cycles, string commentPRefix = "CYCLES")
        {
            var allEdges = new HashSet<string>();
            foreach (var edge in edges)
            {
                string key = UndirectedKey(edge.p0.Id, edge.p1.Id);
                allEdges.Add(key);
            }

            var usedEdges = new HashSet<string>();
            foreach (var cycle in cycles)
            {
                for (int i = 0; i < cycle.Count; i++)
                {
                    var a = cycle[i];
                    var b = cycle[(i + 1) % cycle.Count];
                    usedEdges.Add(UndirectedKey(a.Id, b.Id));
                }
            }

            var missingEdges = allEdges.Except(usedEdges).ToList();
            Debug.Log($"{commentPRefix}: Użyte krawędzie: {usedEdges.Count} / {allEdges.Count}");
            Debug.Log($"{commentPRefix}: Brakujące krawędzie: {missingEdges.Count}");

            foreach (var e in missingEdges)
            {
                Debug.LogWarning($"{commentPRefix}: Krawędź niepokryta przez żaden cykl: {e}");
            }
                
            return missingEdges;
        }

        private static string UndirectedKey(int a, int b)
        {
            return a < b ? $"{a}_{b}" : $"{b}_{a}";
        }


        public static List<List<PtWSgmnts>> FindMinimalCycles(List<LineSegment> edges, int maxCycleLength = 10)
        {
            var graph = BuildGraph(edges);
            var cycles = new HashSet<string>();
            var result = new List<List<PtWSgmnts>>();

            var nodes = graph.Keys.ToList();
            
            for(int i = 0; i < nodes.Count; i++)
            {
                graph[nodes[i]] = graph[nodes[i]].OrderBy(n => n.Id).ToList();
            }
                

            foreach (var start in graph.Keys)
            {
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
                    }
                }
                else if (!visited.Contains(neighbor) && path.Count < maxCycleLength)
                {
                    DFS(neighbor, start, graph, path, new HashSet<PtWSgmnts>(visited), cycles, result, maxCycleLength);
                }
            }

            path.RemoveAt(path.Count - 1);
        }

        private static bool IsValidCycle(List<PtWSgmnts> cycle)
        {
            if (cycle.Count < 3) return false;

            if(!ValidateCycle)
                return true;

            float area = 0f;
            for (int i = 0; i < cycle.Count; i++)
            {
                var a = cycle[i].pos;
                var b = cycle[(i + 1) % cycle.Count].pos;
                area += (a.x * b.y - b.x * a.y);
            }
            area = Mathf.Abs(area) * 0.5f;

            return area > 0.001f; // pomija bardzo małe lub zdegenerowane cykle
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

        private static string GetCycleKey(List<PtWSgmnts> cycle)
        {
            var fwd = string.Join(";", cycle.Select(p => p.Id));
            var rev = string.Join(";", cycle.AsEnumerable().Reverse().Select(p => p.Id));
            return string.CompareOrdinal(fwd, rev) <= 0 ? fwd : rev;
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