using Assets.Game.Scripts.Gen.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Delaunay.Geo;
using Assets.Game.Scripts.Utility;

namespace Assets.Game.Scripts.Editors
{
    public static class GizmosDrawer
    {
        static void DrawLine(Vector2 p1, Vector2 p2)
        {
            Gizmos.DrawLine(p1, p2);
        }

        static void DrawLine(Vector2 p1, Vector2 p2, Vector2 shift)
        {
            Gizmos.DrawLine(p1 + shift, p2 + shift);
        }

        public static void DrawSpheres(List<PtWSgmnts> pts, float sphereSize, float itSizeChange = 0f)
        {
            DrawSpheres(pts, Vector2.zero, sphereSize, itSizeChange);
        }

        public static void DrawSpheres(List<Vector2> pts, float sphereSize, float itSizeChange = 0f)
        {
            if (sphereSize > 0)
            {
                foreach (var pt in pts)
                {
                    Gizmos.DrawSphere(pt, sphereSize);
                    sphereSize += itSizeChange;
                }
            }
        }

        public static void DrawSpheres(List<PtWSgmnts> pts, Vector2 shift, float sphereSize, float itSizeChange = 0f)
        {
            if (sphereSize > 0)
            {
                foreach (var pt in pts)
                {
                    Gizmos.DrawSphere(pt.pos + shift, sphereSize);
                    sphereSize += itSizeChange;
                }
            }
        }

        public static void DrawGizmos(WorldModel worldModel)
        {
            if (worldModel is null)
            {
                return;
            }

            for (int i = 0; i < worldModel.settlementModels.Count; i++)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(worldModel.settlementModels[i].pos, 0.2f);
                foreach (var segment in worldModel.settlementModels[i].majorPaths)
                {
                    Gizmos.color = Color.green;
                    //Gizmos.DrawLine(segment.p0.pos, segment.p1.pos);
                    DrawLine(segment.p0.pos, segment.p1.pos);
                }
            }

            Gizmos.color = Color.yellow;
            if (WorldModel.m_delaunayTriangulation != null)
            {
                for (int i = 0; i < WorldModel.m_delaunayTriangulation.Count; i++)
                {
                    Vector2 left = (Vector2)WorldModel.m_delaunayTriangulation[i].p0.pos;
                    Vector2 right = (Vector2)WorldModel.m_delaunayTriangulation[i].p1.pos;
                    DrawLine((Vector3)left, (Vector3)right, new Vector2(-0.25f, -0.25f));
                }
            }

            Gizmos.color = Color.blue;
            for (int i = 0; i < worldModel.mainRoads.Count; i++)
            {
                LineSegment seg = worldModel.mainRoads[i];
                Vector2 left = (Vector2)seg.p0.pos;
                Vector2 right = (Vector2)seg.p1.pos;
                DrawLine((Vector3)left, (Vector3)right, new Vector2(0.25f, 0.25f));
            }
        }

        internal static void DrawSegment(LineSegment edge, Vector2? shift = null)
        {
            DrawSegment(edge.p0, edge.p1, shift);
        }

        internal static void DrawSegment(PtWSgmnts p0, PtWSgmnts p1, Vector2? shift = null)
        {
            var shiftVal = shift ?? Vector2.zero;
            Gizmos.DrawLine(p0.pos + shiftVal, p1.pos + shiftVal);
        }

        internal static void DrawSegments(List<LineSegment> edges, Vector2? shift = null, float sphereSize = 0f)
        {
            var dist = shift.HasValue? shift.Value : Vector2.zero;      
            foreach(var edge in edges)
            {
                Gizmos.DrawLine(edge.p0.pos + dist, edge.p1.pos + dist);
                if(sphereSize > 0)
                {
                    Gizmos.DrawSphere(edge.p0.pos + dist, sphereSize);
                    Gizmos.DrawSphere(edge.p1.pos + dist, sphereSize);
                    dist += dist;
                }
            }
        }

        internal static void DrawPolygon(Polygon poly, Vector2? shift = null, float sphereSize = 0f)
        {
            var dist = shift.HasValue ? shift.Value : Vector2.zero;
            for(var i = 0; i < poly.Count; i++)
            {
                var point = poly[i];
                var nextPt = poly.Neighbour(i, 1);
                Gizmos.DrawLine(point.pos + dist, nextPt.pos + dist);
                if (sphereSize > 0)
                {
                    Gizmos.DrawSphere(point.pos + dist, sphereSize);
                    Gizmos.DrawSphere(nextPt.pos + dist, sphereSize);
                }
            }
        }

        internal static void DrawVectorList(List<Vector2> list, bool looped = false, float circleSize = 0f)
        {
            DrawVectorList(list, Vector2.zero, looped, circleSize);
        }

        internal static void DrawVectorList(List<Vector2> list, Vector2 shift, bool looped = false, float circleSize = 0f)
        {
            if (list == null || !list.Any())
                return;

            for (int i = 0; i < list.Count - 1; i++)
            {
                Gizmos.DrawLine(list[i] + shift, list[i + 1] + shift);                
            }
            if (circleSize > 0)
            {            
                for (int i = 0; i < list.Count; i++)
                {                             
                    Gizmos.DrawSphere(list[i] + shift, circleSize);
                }
            }
            if (looped)
                Gizmos.DrawLine(list.Last() + shift, list.First() + shift);
        }
        internal static void DrawVectorList(List<PtWSgmnts> list, bool looped = false, float circleSize = 0f)
        {
            DrawVectorList(list, Vector2.zero, looped, circleSize);
        }

        internal static void DrawVectorList(List<PtWSgmnts> list, Vector2 shift, bool looped = false, float circleSize = 0f)
        {
            if (list == null || !list.Any())
                return;
            for (int i = 0; i < list.Count - 1; i++)
            {
                Gizmos.DrawLine(list[i].pos + shift, list[i + 1].pos + shift);
            }

            DrawSpheres(list, shift, circleSize);
            if (looped)
                Gizmos.DrawLine(list.Last().pos + shift, list.First().pos + shift);
        }



        internal static void DrawRay(PtWSgmnts value, Color color)
        {
            DrawRay(value.pos, color);
        }

        internal static void DrawRay(Vector2 value, Color color)
        {
            Debug.DrawRay(value, Vector2.up * 5f, color);
        }

        internal static void DrawRays(Vector2 value, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Debug.DrawRay(value + new Vector2(.25f * i, 0), Vector2.up * 5f, color);
            }
            
        }

        internal static void DrawRays(List<Vector2> pts, Color color)
        {
            foreach (var pt in pts)
            {
                Debug.DrawRay(pt, Vector2.up * 5f, color);
            }

        }

        internal static void DrawRays(List<PtWSgmnts> pts, Color color)
        {
            DrawRays(pts.Select(p => p.pos).ToList(), color);
        }
    }
}