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
using UnityEngine.Rendering.Universal;

namespace Assets.Game.Scripts.Gen
{
    //rozszerzyæ to o np. wykrywanie wysp (oddzielnych komponentów grafu), samotnych wierzcho³ków albo topologiczne „dziury”.
    //Seed with very small triangle or strange wields: 68e7c8ae-9808-4335-ab18-2a4de60b7804
    //See with strange wields      : ff89438a-e1d3-422c-b6e9-aa88191b2c12
    public static class GraphHelpers
    {
        public static List<PtWSgmnts> CreateCircle(float wallRadius, System.Random rnd, float maxStreetLen, Vector2 center)
        {            
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
                var point = center + new Vector2(wallRadius, 0) + rnd.NextVector2(.15f);
                point = point.RotateAroundPivot(center, currAngle);
                pts.Add(point);
                currAngle += angle;
            }
            return pts.Select(p => new PtWSgmnts(p)).ToList();
        }
        public static int JoinSamePosPtsAndRemoveEmptyLines(List<LineSegment> edges)
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

            if (count > 0)
                Debug.Log($"______________> For {edges.Count} edges merged {count} points with same coords");
            if (count2 > 0)
                Debug.Log($"______________> For {edges.Count} edges removed {count2} with 0 length");
            return count;
        }

        public static void AnalyzeEdges(List<LineSegment> allEdges)
        {
            //var lengths = allEdges.OrderBy(e => e.CalculateLength()).Select(e => e.Length).ToList();
            //var avg = lengths.Average();
            //var min = lengths.Min();
            //var max = lengths.Max();
            //Debug.Log($"Lengths. Min ={min}, avg = {avg} max ={max}.");

            var allNodes = allEdges.SelectMany(e => e.EdgePoints).Distinct(new PointsComparer(true)).ToList();
            allNodes = allNodes.OrderBy(n => n.Neighbours.Count).ToList();

            var _0Count = allNodes.Count(n => n.Neighbours.Count == 0);
            var _1Count = allNodes.Count(n => n.Neighbours.Count == 1);
            var _2Count = allNodes.Count(n => n.Neighbours.Count == 2);
            var _3Count = allNodes.Count(n => n.Neighbours.Count == 3);
            var _4Count = allNodes.Count(n => n.Neighbours.Count == 4);
            var _5Count = allNodes.Count(n => n.Neighbours.Count >= 5);
            if (_0Count > 0)
                Debug.Log($"Count0 = {_0Count}");
            if (_1Count > 0)
                Debug.Log($"Count1 = {_1Count}");
            if (_2Count > 0)
                Debug.Log($"Count2 = {_2Count}");
            if (_3Count > 0)
                Debug.Log($"Count3 = {_3Count}");
            if (_4Count > 0)
                Debug.Log($"Count4 = {_4Count}");
            if (_5Count > 0)
                Debug.Log($"Count5 or more = {_5Count}");
            var shift = new Vector2(.21f, .21f);

            Color[] colors = new Color[] { Color.red, Color.yellow, Color.green, Color.blue, Color.magenta, Color.cyan, Color.black };
            foreach (var node in allNodes)
            {
                if (node.Neighbours.Count == 2)
                {
                    var edges = allEdges.Where(e => e.EdgeIds.Contains(node.Id)).ToList();
                    Debug.DrawRay(node.pos, Vector2.up * 4, Color.red);
                    foreach (var edge in edges)
                    {
                        if (!allEdges.Contains(edge))
                        {
                            Debug.LogWarning($"Edge {edge.Id} not found in allEdges list. Node: {node.Id} Neighbours: {node.Neighbours.Count}");
                        }
                        else
                        {
                            Debug.DrawLine(edge.p0.pos + shift, edge.p1.pos + shift, colors[node.Neighbours.Count]);
                            shift += new Vector2(.1f, .1f);
                        }
                    }
                }
                if (node.Neighbours.Count < 5)
                {
                    //Debug.DrawRay(node.pos, Vector2.up * 4, colors[node.Neighbours.Count]);
                }
            }

            var neighbours = allEdges.Where(n => n.Id == allNodes[0].Id || n.p1.Id == allNodes[0].Id).ToList();

            var rMin = allNodes.Min(n => n.Neighbours.Count);
            var rMax = allNodes.Max(n => n.Neighbours.Count);
            var rAvg = allNodes.Average(n => n.Neighbours.Count);
            Debug.Log($"Relations. Min ={rMin}, avg = {rAvg} max ={rMax}.");
            Debug.Log($"edge0. neighbours count = {neighbours.Count}");
        }

        public static void RemoveOutsideEdges(ref List<LineSegment> edges, Polygon p)
        {
            Debug.Log($"Edges = {edges.Count}");
            edges = edges.Where(e => p.ContainsSomeEdge(e)).ToList();
            Debug.Log($"Edges after removing outside = {edges.Count}");
        }
        public static void RemoveDuplicatesOf(List<LineSegment> allEdges, LineSegment ls, bool confirm0Duplicates = false)
        {
            allEdges = allEdges.OrderBy(e => e.Id).ToList();
            var duplicates = allEdges.Where(e => e.EdgeIds.Contains(ls.p0.Id) && e.EdgeIds.Contains(ls.p1.Id) && e.Id != ls.Id).ToList();
            duplicates = allEdges.Where(e => e.EdgeIds.Contains(ls.p0.Id) && e.EdgeIds.Contains(ls.p1.Id) && e.Id != ls.Id).ToList();
            if (confirm0Duplicates || duplicates.Any())
            {
                Debug.Log($"Removed {duplicates.Count} duplicates of edge ({ls.Id}):{ls.p0.Id} <-> {ls.p1.Id}");
            }
            if (duplicates.Any())
            {
                allEdges.RemoveList(duplicates);
            }
        }

        public static void RemoveDuplicates(List<LineSegment> allEdges)
        {
            allEdges = allEdges.OrderBy(e => e.Id).ToList();
            for (int i = 0; i < allEdges.Count; i++)
            {
                RemoveDuplicatesOf(allEdges, allEdges[i]);
            }
        }

        public static void RemoveLevel1Edges(ref List<LineSegment> edges)
        {
            var removedEdges = new List<LineSegment>();
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
                removedEdges.AddRange(edgesToRemove);
            }
            while (edgesToRemove.Any());

            var shift = new Vector2(.0f, .0f);
            foreach (var edge in removedEdges)
            {
                Debug.DrawLine(edge.p0.pos + shift, edge.p1.pos + shift, Color.red);
            }

            if (!edgesToRemove.Any())
            {
                Debug.LogWarning("No points with neighbour leve 2 or above..");
            }
        }
        public static int IdentifyIntersections(List<LineSegment> edges, Polygon p, Vector2 center)
        {
            var totalCount = 0;
            int count = 0;

            do
            {
                count = 0;
                foreach (var edgeToIntersect in edges)
                {
                    for (int i = 0; i < p.Count; i++)
                    {
                        var nextIndex = (i + 1) % p.Count;
                        var pt = p[i];
                        var nextP = p[nextIndex];
                        
                        var intersection = IdentifyIntersection(edgeToIntersect, pt, nextP, p, i, center);
                        if (intersection != null)
                        {
                            p.InsertCheckpoint(intersection, nextIndex);
                        }
                    }
                }

                Debug.Log($"Identified intersections and shortened {count} edges out of {edges.Count}");
                totalCount += count;
            }
            while (count> 0);
            
            return totalCount;
        }

        public static List<LineSegment> intersectingSegments = new List<LineSegment>();
        public static PtWSgmnts IdentifyIntersection(LineSegment intersectingEdge, PtWSgmnts pt, PtWSgmnts nextPt, Polygon p, int i, Vector2 center)//
        {
            var inters = Vector.GetIntersectionPoint(intersectingEdge.p0, intersectingEdge.p1, pt, nextPt);
            if (inters.HasValue)
            {
                if (!p.ContainsPoint(intersectingEdge.p0))
                {
                    intersectingEdge.p0.pos = inters.Value;
                    return intersectingEdge.p0;
                }
                else if (!p.ContainsPoint(intersectingEdge.p1, false))
                {
                    intersectingEdge.p1.pos = inters.Value;
                    return intersectingEdge.p1;
                }
                else
                {
                    //Debug.LogWarning($"Both points belong to polygon! {inters.Value}");
                    //GizmosDrawer.DrawRay(inters.Value, Color.red);
                }
            }
            return null;
        }
    }
}