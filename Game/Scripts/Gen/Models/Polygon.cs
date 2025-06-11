using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Delaunay.Geo;
using Unity.VisualScripting;
using Assets.Game.Scripts.Utility;
using UnityEngine.Rendering.VirtualTexturing;
using System;
using Assets.Game.Scripts.Gen.GraphGenerator;
using NUnit.Framework;
using Assets.Game.Scripts.Editors;
using Unity.Profiling;

namespace Assets.Game.Scripts.Gen.Models
{
    public class Polygon
    {
        static uint Index = 0;
        public static void ResetCount()
        {
            Index = 0;
        }
        public uint Id { get; protected set; }

        public Polygon GetDeepClone()
        {
            Id = Index++;
            var newPoly = new Polygon(this.points.Select(p => new PtWSgmnts(p.pos)).ToList());            
            return newPoly;
        }

        internal Rect GetRectangleCircumscribedInPolygon(float offset = 0f)
        {
            var pts = this.points.OrderBy(p => p.pos.x).ToList();
            var minX = pts[0].pos.x;
            var maxX = pts.Last().pos.x;

            pts = this.points.OrderBy(p => p.pos.y).ToList();
            var minY = pts[0].pos.y;
            var maxY = pts.Last().pos.y;
            var xSize = maxX - minX;
            var ySize = maxY - minY;
            return new Rect(new Vector2(minX - offset * xSize, minY - offset * ySize), new Vector2(xSize * (1 + 2 * offset), ySize * (1 + 2 * offset)));
        }
        public float Length { get; private set; }
        public List<PtWSgmnts> points { get; set; } = new();

        public Polygon()
        {
            Id = Index++;
        }

        public Polygon(List<Vector2> points)
        {
            Id = Index++;
            this.points = points.Select(p => new PtWSgmnts(p)).ToList();
        }

        public Polygon(List<PtWSgmnts> points)
        {
            Id = Index++;
            this.points = points;
        }

        public Polygon(params List<PtWSgmnts>[] pointArrays)
        {
            Id = Index++;
            var many = pointArrays.SelectMany(p => p).ToList();
            this.points = many.Distinct(new PointsComparer(true)).ToList();
        }

        public List<LineSegment> CreateEdges()
        {
            var lines = new List<LineSegment>();
            for (int i = 0; i < this.points.Count; i++)
            {
                var p = this.points[i];
                var nextP = this.points.Neighbour(i, 1);
                lines.Add(new LineSegment(p, nextP));
            }
            return lines;
        }

        public Polygon(params PtWSgmnts[] points)
        {
            Id = Index++;
            this.points = points.ToList();
        }

        public Polygon(params Vector2[] vertices)
        {
            Id = Index++;
            this.points = vertices.Select(v => new PtWSgmnts(v)).ToList();
        }

        public bool ContainsSomeEdge(LineSegment edge)
        {
            return this.ContainsPoint(edge.p0) || this.ContainsPoint(edge.p1);
        }
        public void ReorderPointsByAngleCCW()
        {
            this.points.Select(p => p.pos).ToList().ReorderPointsByAngleCCW();
        }

        public bool ContainsCheckpointPos(PtWSgmnts point, float epsilon = 1f)
        {
            var pt = points.Where(p => (p.pos - point.pos).magnitude < epsilon).FirstOrDefault();
            return pt != null;
        }

        public bool ContainsCheckpoint(PtWSgmnts point)
        {
            var pt = points.Where(p => p.Id == point.Id).FirstOrDefault();
            return pt != null;
        }
        public bool ContainsCheckpoints(params PtWSgmnts[] points)
        {
            foreach (var point in points)
            {
                var result = this.ContainsCheckpoint(point);
                if (!result)
                    return false;
            }
            return true;
        }

        public bool ContainsCheckpoints(List<PtWSgmnts> points)
        {
            foreach (var point in points)
            {
                var result = this.ContainsCheckpoint(point);
                if (!result)
                    return false;
            }
            return true;
        }

        internal float GetCircumference()
        {
            var len = 0f;
            var loopedPts = points.ToList();
            loopedPts.Add(points[0]);
            for (int i = 0; i < loopedPts.Count - 1; i++)
            {
                len += loopedPts[i].DistanceTo(loopedPts[i + 1]);
            }
            return len;
        }

        internal List<PtWSgmnts> GetOverlappingPoints(Polygon polygon)
        {
            var allContainedPoints = polygon.points.Where(p => this.ContainsPoint(p) && !this.points.Contains(p)).ToList();
            var pointsInsidePolygon = allContainedPoints.ToList();
            foreach (var pt in allContainedPoints)
            {
                for (int i = 0; i < this.points.Count - 1; i++)
                {
                    if (PointOnLineSegment(pt, points[i], points[i + 1]))
                    {
                        pointsInsidePolygon.Remove(pt);
                        break;
                    }
                }
            }
            return pointsInsidePolygon;
        }

        internal List<PtWSgmnts> GetPointsOutside(List<PtWSgmnts> points)
        {
            return GetPoints(points, true);
        }

        internal List<PtWSgmnts> GetPointsInside(List<PtWSgmnts> points)
        {
            return GetPoints(points, true);
        }

        public List<PtWSgmnts> GetPoints(List<PtWSgmnts> points, bool inside)
        {
            var start = 0;
            var end = points.Count - 1;
            var middle = start.GetMiddleNumber(end);
            var middleInside = this.ContainsPoint(points[middle]);
            var nextInside = this.ContainsPoint(points[middle + 1]);
            do
            {
                if (middleInside)
                {
                    start = middle;
                }
                else
                {
                    end = middle;
                }
                middle = start.GetMiddleNumber(end);
                middleInside = this.ContainsPoint(points[middle]);
                nextInside = this.ContainsPoint(points[middle + 1]);
            }
            while ((!middleInside && nextInside) == inside);
            return points.Take(middle).ToList();
        }

        public List<Vector2> GetVectors()
        {
            return points.Select(p => p.pos).ToList();
        }


        public static LineSegment GetParallelLine(LineSegment s, float distance)
        {
            var shiftV1 = s.p1.pos + new Vector2(distance, 0);
            var shiftV0 = s.p0.pos + new Vector2(distance, 0);

            var angle = Vector2.Angle(s.p1.pos - s.p0.pos, s.p1.pos - shiftV0);
            shiftV1 = shiftV1.RotateAroundPivot(s.p1.pos, 90 - angle);
            shiftV0 = shiftV0.RotateAroundPivot(s.p0.pos, 90 - angle);
            return new LineSegment(shiftV0, shiftV1);
        }

        public static LineSegment GetParallelLine(LineSegment s, float distance, Vector2 dir)
        {
            var shiftV1 = s.p1.pos + new Vector2(distance, 0);
            var shiftV0 = s.p0.pos + new Vector2(distance, 0);

            var angle = Vector2.Angle(s.p1.pos - s.p0.pos, s.p1.pos - shiftV0);

            shiftV0 = shiftV0.RotateAroundPivot(s.p0.pos, 90 - angle);
            shiftV1 = shiftV1.RotateAroundPivot(s.p1.pos, 90 - angle);
            if (shiftV0.DistanceTo(dir) > s.p0.pos.DistanceTo(dir))
            {
                shiftV1 = shiftV1.RotateAroundPivot(s.p1.pos, -90 + angle);
                shiftV0 = shiftV0.RotateAroundPivot(s.p0.pos, -90 + angle);
            }

            return new LineSegment(shiftV0, shiftV1);
        }

        public static bool PointOnLineSegment(PtWSgmnts Pt1, PtWSgmnts Pt2, PtWSgmnts Pt, double epsilon = 0.001)
        {
            var pt = Pt.pos;
            var pt1 = Pt1.pos;
            var pt2 = Pt2.pos;
            if (pt.x - Math.Max(pt1.x, pt2.x) > epsilon ||
                Math.Min(pt1.x, pt2.x) - pt.x > epsilon ||
                pt.y - Math.Max(pt1.y, pt2.y) > epsilon ||
                Math.Min(pt1.y, pt2.y) - pt.y > epsilon)
                return false;

            if (Math.Abs(pt2.x - pt1.x) < epsilon)
                return Math.Abs(pt1.x - pt.x) < epsilon || Math.Abs(pt2.x - pt.x) < epsilon;
            if (Math.Abs(pt2.y - pt1.y) < epsilon)
                return Math.Abs(pt1.y - pt.y) < epsilon || Math.Abs(pt2.y - pt.y) < epsilon;

            double x = pt1.x + (pt.y - pt1.y) * (pt2.x - pt1.x) / (pt2.y - pt1.y);
            double y = pt1.y + (pt.x - pt1.x) * (pt2.y - pt1.y) / (pt2.x - pt1.x);

            return Math.Abs(pt.x - x) < epsilon || Math.Abs(pt.y - y) < epsilon;
        }

        public bool ContainsPoint(PtWSgmnts p)
        {
            if (this.ContainsCheckpoint(p))
                return true;
            return this.ContainsPoint(p.pos);
        }

        public int NumberOfPolygonsOverlapping(List<Polygon> p)
        {

            return 0;
            //for (int i = 0; i < this.points.Count; i++)
            //{
            //    var pi = this.points[i];
            //    var nextP = this.points.Neighbour(i, 1);

            //    if (ParcelGenerator.IsPointOnSegment(p.pos, pi.pos, nextP.pos))
            //        return true;
            //}
            //return false;
        }


        public bool ContainsPoint(Vector2 p)
        {
            var poly = points.Select(p => p.pos).ToArray();
            Vector2 p1, p2;
            bool inside = false;

            if (poly.Length < 3)
            {
                return false;
            }

            var oldPoint = new Vector2(poly[poly.Length - 1].x, poly[poly.Length - 1].y);

            for (int i = 0; i < poly.Length; i++)
            {
                var newPoint = new Vector2(poly[i].x, poly[i].y);

                if (newPoint.x > oldPoint.x)
                {
                    p1 = oldPoint;
                    p2 = newPoint;
                }
                else
                {
                    p1 = newPoint;
                    p2 = oldPoint;
                }

                if ((newPoint.x < p.x) == (p.x <= oldPoint.x)
                    && (p.y - p1.y) * (p2.x - p1.x)
                    < (p2.y - p1.y) * (p.x - p1.x))
                {
                    inside = !inside;
                }
                oldPoint = newPoint;
            }
            return inside;
        }

        public Vector2 FindCenter()
        {
            Vector2 pos = Vector2.zero;
            foreach (var p in points)
            {
                pos += p.pos;
            }
            return pos / points.Count;
        }

        public (PtWSgmnts, PtWSgmnts) GetClosestPointsWithMostDistantIndexes()
        {
            int minInd1 = 0;
            int minInd2 = points.Count / 2;

            var minDistance = points[minInd1].DistanceTo(points[minInd2]);
            for (int i = 0; i < points.Count / 2; i++)
            {
                var theOtherEnd = i.WrapIndex(points.Count / 2, points);
                var dist = points[i].DistanceTo(points[theOtherEnd]);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    minInd1 = i;
                    minInd2 = theOtherEnd;
                }
            }

            var rnd = RoadGraphGenChaos.GetRandom();
            if (rnd.NextBool(.3f))
            {
                var tmp = minInd1.WrapIndex(rnd.Next(-1, 2), points);
                if (points[tmp].DistanceTo(points[minInd2]) <= 1.6 * points[minInd1].DistanceTo(points[minInd2]))
                {
                    minInd1 = tmp;
                }

            }
            else if (rnd.NextBool(.3f))
            {
                var tmp = minInd2.WrapIndex(rnd.Next(-1, 2), points);
                if (points[tmp].DistanceTo(points[minInd1]) <= 1.6 * points[minInd1].DistanceTo(points[minInd2]))
                {
                    minInd2 = tmp;
                }
            }

            return (points[minInd1], points[minInd2]);
        }

        public (List<PtWSgmnts>, List<PtWSgmnts>) DividePolygonPoints(PtWSgmnts pt1, PtWSgmnts pt2)
        {
            var list1 = points.TakeRangeWrapped(pt1, pt2);
            var list2 = points.TakeRangeWrapped(pt2, pt1);
            return (list1, list2);
        }

        public float CalculateArea()
        {
            var newList = points.ToList();
            newList.Add(points[0]);
            var area = Math.Abs(newList.Take(newList.Count - 1)
               .Select((p, i) => (newList[i + 1].pos.x - p.pos.x) * (newList[i + 1].pos.y + p.pos.y))
               .Sum() / 2);
            return area;
        }

        public Dictionary<PtWSgmnts, float> GetDicWithDistFromCenterPts(List<float> devs)
        {
            var dic = new Dictionary<PtWSgmnts, float>();
            for (int i = 0; i < this.points.Count; i++)
            {
                dic.Add(this.points[i], devs[i]);
            }
            return dic;
        }

        public (PtWSgmnts, Dictionary<PtWSgmnts, float>) GetPointClosestToCenterAndDistDic(Vector2 center)
        {
            var distToCenter = this.points.Select(p => p.DistanceTo(center)).ToList();
            var avg = distToCenter.Average();
            var devs = distToCenter.Select(d => d - avg).ToList();
            var anyPointTooClose = devs.Count(d => d < 0);
            var pointsDistDic = GetDicWithDistFromCenterPts(devs);
            return (pointsDistDic.OrderBy(d => d.Value).First().Key, pointsDistDic);
        }

        public void SplitAndDistort(float maxLength)
        {
            var rnd = RoadGraphGenChaos.GetRandom();
            SplitAndDistortOnWorldMap(rnd, maxLength);
        }

        public void SplitAndDistortOnWorldMap(float maxLength)
        {
            var rnd = RoadGraphGenChaos.GetRandom();
            SplitAndDistortOnWorldMap(rnd, maxLength);
        }

        public void SplitAndDistortOnWorldMap(System.Random rnd, float maxLength)
        {
            var len = this.CalculateLength();
            if (len >= maxLength)
            {
                var distortionFactor = .25f;
                var subdivided = false;
                do
                {
                    subdivided = false;
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        var subLen = this.SubSegmentLength(i, i + 1);
                        if (subLen >= maxLength)
                        {
                            SubdivideIteration(i, i + 1, distortionFactor);
                            subdivided = true;
                        }
                    }
                    distortionFactor *= VoronoiDemo.DistortionDecay / 2f;
                }
                while (subdivided);
                this.CalculateLength();
            }
        }

        public void Split(System.Random rnd, float maxLength)
        {
            this.CalculateLength();
            var len = this.Length;
            if (len >= maxLength)
            {
                var subdivided = false;
                do
                {
                    subdivided = false;
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        var subLen = this.SubSegmentLength(i, i + 1);
                        if (subLen >= maxLength)
                        {
                            SubdivideIteration(i, i + 1);
                            subdivided = true;
                        }
                    }

                }
                while (subdivided);
                this.CalculateLength();
            }
        }
        public void Distort(System.Random rnd, float maxLength)
        {
            //main trend length
            //sub Trend
            var mainAngle = rnd.NextFloat();
            var maxDev = this.Length / 5f;
            var count = points.Count;
            for (int i = 1; i < count - 1; i++)
            {
                var rotatedPos = points[0].pos.RotateAroundPivot(points[i].pos, 90);
                points[i].pos = Vector2.Lerp(rotatedPos, points[i].pos, i / (float)count);
            }
        }


        public void SubdivideIteration(int index, int nextIndex, float distortionFactor = 0)
        {
            var rnd = RoadGraphGenChaos.GetRandom();
            var t = rnd.NextFloat() * 0.2f + 0.4f;
            var middlePoint = new PtWSgmnts(Vector2.Lerp(this.points[index].pos, this.points[nextIndex].pos, t));
            this.points.Insert(nextIndex, middlePoint);
            Distort(index, nextIndex, distortionFactor);
        }

        void Distort(int index, int nextIndex, float distortionFactor)
        {
            var rnd = RoadGraphGenChaos.GetRandom();
            if (distortionFactor > 0)
            {
                var angle = Mathf.PI * 2 * rnd.NextFloat();
                var length = this.SubSegmentLength(index, nextIndex);
                var len = rnd.NextFloat() * 0.15f + 0.15f;
                points[nextIndex].pos += new Vector2(length * len * MathF.Cos(angle), length * len * MathF.Sin(angle)) * distortionFactor;
            }
        }



        public float CalculateLength()
        {
            var sum = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                sum += SubSegmentLength(i, i + 1);
            }
            this.Length = sum;
            return this.Length;
        }

        internal float SubSegmentLength(int index, int nextindex)
        {
            var result = Vector2.Distance(points[index].pos, points[nextindex].pos);
            return result;
        }

        internal void AddCheckPoints(params PtWSgmnts[] points)
        {
            this.points.AddRange(points);
            this.CalculateLength();
        }
        internal void AddCheckPoints(params Vector2[] points)
        {
            this.points.AddRange(points.Select(p => new PtWSgmnts(p)));
            this.CalculateLength();
        }


        public void InsertCheckpoints(int index, params PtWSgmnts[] p)
        {
            this.points.InsertRange(index, p);
        }

        public void InsertCheckpoint(PtWSgmnts p, int index)
        {
            this.points.Insert(index, p);
        }

        internal void SubdivideOnceAndMoveNewPointsToCenter(System.Random rnd, Vector2 center)
        {
            var newPoints = new List<PtWSgmnts>();
            for (int i = this.points.Count - 2; i >= 0; i--)
            {
                var middlePoint = new PtWSgmnts(Vector2.Lerp(this.points[i].pos, this.points[i + 1].pos, 0.5f));
                this.points.Insert(i + 1, middlePoint);
                newPoints.Add(middlePoint);
            }

            foreach (var point in newPoints)
            {
                point.pos = Vector2.Lerp(center, point.pos, rnd.NormalFloat(0.6f, 0.8f));
            }
        }

        internal List<PtWSgmnts> LoopedCheckPoints()
        {
            var list = points.ToList();
            if (points.Any())
                list.Add(points.First());
            return list;
        }

        internal float GetFirstCheckPointsAngle(Polygon pol)
        {
            return Vector2.Angle(points[1].pos - points[0].pos, pol.points[1].pos - pol.points[0].pos);
        }

        internal List<float> GetEdgeDistancesToCenter()
        {
            var center = this.FindCenter();
            var list = new List<float>();
            for (int i = 0; i < this.points.Count; i++)
            {
                list.Add(points[i].DistanceTo(center));
            }
            return list;
        }

        internal List<float> GetEdgeLengths()
        {
            var list = new List<float>();
            for (int i = 0; i < this.points.Count; i++)
            {
                list.Add(points[i].DistanceTo(points.Neighbour(i, 1)));
            }
            return list;
        }

        public int GetEdgeLength(float len)
        {
            for (int i = 0; i < this.points.Count; i++)
            {
                if (points[i].DistanceTo(points.Neighbour(i, 1)) == len)
                    return i;
            }
            return -1;
        }

        internal float GetHeightOfEdge(int edgeLenIndex)
        {
            var vert1 = points[edgeLenIndex].pos;
            var vert2 = points.Neighbour(edgeLenIndex, 1).pos;
            var heightEdge = points.Neighbour(edgeLenIndex, -1).pos;
            if (this.points.Count == 3)
            {
                return ParcelGenerator.PerpendicularDistance(vert1, vert2, heightEdge);
            }
            else
            {
                Debug.LogWarning($"Works only for triangle, number of verts = {points.Count}");
                return ParcelGenerator.PerpendicularDistance(vert1, vert2, heightEdge);
            }
        }

        internal LineSegment GetshortestEdge()
        {
            var list = new List<float>();
            for (int i = 0; i < this.points.Count; i++)
            {
                list.Add(points[i].DistanceTo(points.Neighbour(i, 1)));
            }

            var minIndex = list.IndexOf(list.Where(v => v > 0).Min());
            return new LineSegment(points[minIndex], points.Neighbour(minIndex, 1));
        }

        internal bool PointsAreOnEdge(PtWSgmnts p0, PtWSgmnts p1)
        {
            var p1OnSegment = false;
            var p2OnSegment = false;
            for (int i = 0; i < this.points.Count; i++)
            {
                var pi = this.points[i];
                var pNext = this.points.Neighbour(i, 1);
                if(!p1OnSegment)
                    p1OnSegment = p1OnSegment || ParcelGenerator.IsPointOnSegment(p0.pos, pi.pos, pNext.pos);
                if(!p2OnSegment)
                    p2OnSegment = p2OnSegment || ParcelGenerator.IsPointOnSegment(p1.pos, pi.pos, pNext.pos);
                if(p1OnSegment && p2OnSegment)
                    return true;
            }
            return false;
        }

        internal bool PointOnEdge(PtWSgmnts p0)
        {
            var p1OnSegment = false;
            for (int i = 0; i < this.points.Count; i++)
            {
                var pi = this.points[i];
                var pNext = this.points.Neighbour(i, 1);
                if (!p1OnSegment)
                    p1OnSegment = p1OnSegment || ParcelGenerator.IsPointOnSegment(p0.pos, pi.pos, pNext.pos);
                if (p1OnSegment)
                    return true;
            }
            return false;
        }

        internal List<float> GetInnerAngles()
        {
            var angles = new List<float>();
            for (int i = 0; i < this.points.Count; i++)
            {
                var pI = this.points[i];
                var prevP = this.points.Neighbour(i, -1);
                var nextP = this.points.Neighbour(i, 1);
                var angle = Vector2.SignedAngle(pI.pos - prevP.pos, pI.pos - nextP.pos);
                angles.Add(Mathf.Round(angle));
            }
            return angles;
        }

        internal void RemovePointWithSamePos()
        {
            int count = 0;
            for (int i = 0; i < this.points.Count; i++)
            {
                var p = this.points[i];
                var nextP = this.points.Neighbour(i, 1);
                if((p.pos - nextP.pos).magnitude < float.Epsilon)
                {
                    this.points.RemoveAt(i);
                    count++;
                    i--;
                }
            }
            if(count > 0)
                Debug.LogWarning($"{count} duplicates removed from polyon");
            return;
            var ptsWithSamePos = this.points.GroupBy(p => p.pos).Where(g => g.Count() > 1).SelectMany(g => g.Skip(1)).ToList();
            if (ptsWithSamePos.Count > 0)
            {
                Debug.DrawRay(ptsWithSamePos[0].pos, Vector2.up * ptsWithSamePos.Count, Color.red);
                count = ptsWithSamePos.Count;                
                this.points = this.points.Except(ptsWithSamePos).ToList();
                Debug.LogWarning($"Duplicates found. {count}. Number left after removal: {ptsWithSamePos.Count}");
            }
        }

        internal void RemovePointWByPos(PtWSgmnts p0)
        {
            var ptsWithP0Pos = this.points.Where(p => p.pos == p0.pos).ToList();
            if(ptsWithP0Pos.Count > 1)
            {
                Debug.LogWarning("More than one point of same pos found.");
            }
            this.points = this.points.Except(ptsWithP0Pos).ToList();
        }

        internal void UpdatePointPosByPos(PtWSgmnts currP, Vector2 newPos)
        {
            var ptsWithP0Pos = this.points.Where(p => p.pos == currP.pos).ToList();
            if (ptsWithP0Pos.Count > 1)
            {
                Debug.LogWarning($"More than one point of same pos found. {ptsWithP0Pos.Count}");
                
            }
            foreach(var pt in ptsWithP0Pos)
            {
                pt.pos = newPos;
            }
        }

        internal bool PosOnPolygonEdge(params PtWSgmnts[] pts)
        {
            foreach(var p in pts)
            {
                for (int i = 0; i < this.points.Count; i++)
                {
                    var pi = this.points[i];
                    var pNext = this.points.Neighbour(i, 1);
                    if (ParcelGenerator.IsPointOnSegment(p.pos, pi.pos, pNext.pos))
                        return true;
                }
            }
            return false;
        }

        internal List<PtWSgmnts> GetNeighgboursPtCloserTo(PtWSgmnts neighbourTo, Vector2 destination)
        {
            var result = new List<PtWSgmnts>();
            float resultDist = float.MaxValue;
            float neighDist = float.MaxValue;

            do
            {
                var neighIndes = this.points.IndexOf(neighbourTo);
                var prev = this.points.Neighbour(neighIndes, -1);
                var next = this.points.Neighbour(neighIndes, 1);
                var prevDist = prev.DistanceTo(destination);
                var nextDist = next.DistanceTo(destination);
                neighDist = neighbourTo.DistanceTo(destination);

                var resultPt = next;
                resultDist = nextDist;
                if (prevDist < nextDist)
                {
                    resultPt = prev;
                    resultDist = prevDist;
                }
                result.Add(resultPt);
                neighbourTo = resultPt;
            }
            while (resultDist < neighDist);
            return result;
        }
    }
}