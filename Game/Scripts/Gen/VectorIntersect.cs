using Assets.Game.Scripts.Editors;
using Delaunay.Geo;
using UnityEngine;
using Assets.Game.Scripts.Gen.Models;
using System;
using System.Collections.Generic;
using Assets.Game.Scripts.Gen;

public class VectorIntersect : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDrawGizmos()
    {
        LineSegment segment = new LineSegment(new PtWSgmnts(0f, 0f), new PtWSgmnts(2f, 2f));
        //LineSegment segment2 = new LineSegment(new PointWithSegments(0f, 2f), new PointWithSegments(-2, -4f));
        LineSegment segment2 = new LineSegment(new PtWSgmnts(0f, 2f), new PtWSgmnts(2f, 0f));

        Gizmos.color = Color.red;
        //Gizmos.DrawLine(segment.p0.pos, segment.p1.pos);
        //Gizmos.DrawLine(segment2.p0.pos, segment2.p1.pos);

        var result = segment.Intersects(segment2);
        var point = segment.GetIntersectionPoint(segment2);

        var r1 = GetIntersectionPoint(segment.p0.pos, segment.p1.pos, segment2.p0.pos, segment2.p1.pos);
        var r2 = GetIntersection(segment.p0.pos, segment.p1.pos, segment2.p0.pos, segment2.p1.pos);
    }
    public static Vector2? GetIntersectionPoint(PtWSgmnts p1, PtWSgmnts p2, PtWSgmnts p3, PtWSgmnts p4)
    {
        return GetIntersectionPoint(p1.pos, p2.pos, p3.pos, p4.pos);
    }
    public static Vector2? GetIntersectionPoint(PtWSgmnts p1, PtWSgmnts p2, LineSegment s2)
    {
        return GetIntersectionPoint(p1.pos, p2.pos, s2.p0.pos, s2.p1.pos);        
    }
    public static Vector2? GetIntersectionPoint(LineSegment s1, LineSegment s2)
    {
        return GetIntersectionPoint(s1.p0.pos, s1.p1.pos, s2.p0.pos, s2.p1.pos);
    }

    public static Vector2? IntersectLines(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        Vector2 da = a2 - a1;
        Vector2 db = b2 - b1;
        Vector2 dp = a1 - b1;

        float dapPerpDb = da.x * db.y - da.y * db.x;
        
        if (Mathf.Abs(dapPerpDb) < 1e-6f)
            return null; // linie równoleg³e

        float t = (db.x * dp.y - db.y * dp.x) / dapPerpDb;
        return a1 + t * da;
    }

    public static Vector2? IntersectLines2(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, float epsilon = 1e-8f)
    {
        double x1 = a1.x, y1 = a1.y;
        double x2 = a2.x, y2 = a2.y;
        double x3 = b1.x, y3 = b1.y;
        double x4 = b2.x, y4 = b2.y;

        double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < epsilon)
            return null; // niemal równoleg³e

        double px = ((x1 * y2 - y1 * x2) * (x3 - x4) - (x1 - x2) * (x3 * y4 - y3 * x4)) / denom;
        double py = ((x1 * y2 - y1 * x2) * (y3 - y4) - (y1 - y2) * (x3 * y4 - y3 * x4)) / denom;

        return new Vector2((float)px, (float)py);
    }

    public static Vector2? GetIntersectionPoint(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d1 = Vector3.Cross(p1 - p3, p4 - p3).z;
        float d2 = Vector3.Cross(p2 - p3, p4 - p3).z;
        if (d1 - d2 == 0) return null; // P1P2 and P3P4 is parallel in this case
        Vector3 intersection = (d1 * p2 - d2 * p1) / (d1 - d2);

        Vector3 aDiff = p2 - p1;
        Vector3 bDiff = p4 - p3;
        if (LineLineIntersection(out intersection, p1, aDiff, p3, bDiff))
        {
            float aSqrMagnitude = aDiff.sqrMagnitude;
            float bSqrMagnitude = bDiff.sqrMagnitude;

            if ((intersection - (Vector3)p1).sqrMagnitude <= aSqrMagnitude
                 && (intersection - (Vector3)p2).sqrMagnitude <= aSqrMagnitude
                 && (intersection - (Vector3)p3).sqrMagnitude <= bSqrMagnitude
                 && (intersection - (Vector3)p4).sqrMagnitude <= bSqrMagnitude)
            {
                return intersection;
            }
        }
        return null;
    }
    public static bool LineLineIntersection(out Vector3 intersection, Vector3 linePoint1,
       Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2)
    {

        Vector3 lineVec3 = linePoint2 - linePoint1;
        Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
        Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

        float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

        //is coplanar, and not parallel
        if (Mathf.Abs(planarFactor) < 0.0001f
                && crossVec1and2.sqrMagnitude > 0.0001f)
        {
            float s = Vector3.Dot(crossVec3and2, crossVec1and2)
                    / crossVec1and2.sqrMagnitude;
            intersection = linePoint1 + (lineVec1 * s);
            return true;
        }
        else
        {
            intersection = Vector3.zero;
            return false;
        }
    }

    private static Vector3 GetIntersection(Vector3 A, Vector3 a, Vector3 B, Vector3 b)
    {
        Vector2 p1 = new Vector2(A.x, A.y);
        Vector2 p2 = new Vector2(a.x, a.y);

        Vector2 p3 = new Vector2(B.x, B.y);
        Vector2 p4 = new Vector2(b.x, b.y);

        float denominator = (p4.y - p3.y) * (p2.x - p1.x) - (p4.x - p3.x) * (p2.y - p1.y);

        float u_a = ((p4.x - p3.x) * (p1.y - p3.y) - (p4.y - p3.y) * (p1.x - p3.x)) / denominator;
        float u_b = ((p2.x - p1.x) * (p1.y - p3.y) - (p2.y - p1.y) * (p1.x - p3.x)) / denominator;

        float IntersectionX = p1.x + u_a * (p2.x - p1.x);
        float IntersectionY = p1.y + u_a * (p2.y - p1.y);
        Vector3 Intersection = new Vector2(IntersectionX, IntersectionY);

        return Intersection;
    }
}
