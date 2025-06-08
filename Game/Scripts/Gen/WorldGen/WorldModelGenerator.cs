using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Delaunay.Geo;
using UnityEditor;
using Assets.Game.Scripts.Editors;

namespace Assets.Game.Scripts.Gen.WorldGen
{
    public class WorldModelGenerator: MonoBehaviour
    {
        public int seed;

        public WorldModel GenerateSector(int worldSeed, int m_mapWidth, int m_mapHeight, int avgDensity)
        {
            var rnd = new System.Random(worldSeed);

            var m_points = new List<Vector2>();
            var count = m_mapHeight * m_mapWidth * avgDensity / 10000;
            for (uint i = 0; i < count; i++)
            {
                m_points.Add(new Vector2(
                        rnd.Next(0, m_mapWidth),
                        rnd.Next(0, m_mapHeight))
                );
            }
            Delaunay.Voronoi v = new Delaunay.Voronoi(m_points, new Rect(0, 0, m_mapWidth, m_mapHeight));

            //var m_edges = v.VoronoiDiagram();
            var m_delaunayTriangulation = v.DelaunayTriangulation();
            var m_spanningTree = v.SpanningTree();
            var points = m_spanningTree.SelectMany(s => s.EdgePoints)
                                    .Distinct(new PointsComparer(false))
                                    .ToList();

            bool addShortestRelationToSegments = true;

            foreach (var point in points)
            {
                var segments = m_spanningTree.Where(s => s.ContainsAnyEdgePointPos(point));
                point.AddMajorPaths(segments.ToArray());

                var relations = m_delaunayTriangulation.Where(t => t.EdgePoints.Contains(point)).ToArray();
                point.AddRelations(relations);

                var shortestRelation = relations.OrderBy(r => r.Length).FirstOrDefault();
                var longestPath = point.majorPaths.OrderByDescending(s => s.Length).FirstOrDefault();
                if (addShortestRelationToSegments && shortestRelation != null && longestPath != null)
                {
                    if(shortestRelation.Length < longestPath.Length)
                    {
                        point.AddRelations(shortestRelation);
                    }
                }
            }

            worldModel = new WorldModel(points, m_spanningTree);
            WorldModel.m_delaunayTriangulation = m_delaunayTriangulation;
            return worldModel;
        }

        WorldModel worldModel;

        void OnDrawGizmos()
        {
            GizmoDrawer.DrawGizmos(worldModel);
        }
    }
}