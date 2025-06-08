using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Utility;
using System.IO;

namespace Assets.Game.Scripts.Gen.Models
{
    public class LineSegment: Polygon
	{
        public bool HasInncerCircleInters => IntersIndex.HasValue;
        public int? IntersIndex => (InnerCircleInters is null)? null : points.IndexOf(InnerCircleInters);
        public PtWSgmnts InnerCircleInters { get; private set; }
        public bool castleRoad = false;
        static int instanceIndex = 0;
        public int Id { get; private set; } = -1;

        public List<PtWSgmnts> CptsNoEdges => points.Where(c => points.IndexOf(c) > 0 && points.IndexOf(c) < points.Count - 1).ToList();

        public PtWSgmnts p0 
        { 
            get => points.First(); 
            set 
            { 
                points[0] = value;
                this.CalculatePathLength();
            } 
        }
        public PtWSgmnts p1 { get => points[^1]; set { points[^1] = value; } }
        public PtWSgmnts GetDirectionFromP0() => points[1];
        public PtWSgmnts GetDirectionFromP1() => points.LastButOne();
        public PtWSgmnts[] EdgePoints => new PtWSgmnts[] { p0, p1 };

        public bool HasConnectionWithCity { get; internal set; } = false;
        public bool Joined { get; internal set; } = false;
        public bool Removable { get; internal set; } = true;

        public static LineSegment CreateTempSegment(PtWSgmnts p0, PtWSgmnts p1)
        {
            var s = new LineSegment();
            s.points.Add(p0);
            s.points.Add(p1);
            return s;
        }
        public LineSegment()
        {

        }

        public LineSegment(Vector2 p0, Vector2 p1)
        {
            this.AddCheckPoints(new PtWSgmnts(p0), new PtWSgmnts(p1));
            this.p0.AddNeighbour(this.p1);
            this.CalculatePathLength();
        }

        public LineSegment(List<PtWSgmnts> points, bool major = true)
        {
            this.points.AddRange(points);
            this.p0.AddNeighbour(this.p1);
            if (major)
            {
                this.p0.AddMainPath(this);
                this.p1.AddMainPath(this);
            }
            else
            {
                this.p0.AddMinorPath(this);
                this.p1.AddMinorPath(this);
            }
            this.CalculatePathLength();
            Id += instanceIndex++;
        }

        public LineSegment(PtWSgmnts p0, PtWSgmnts p1, bool major = true)
        {
            this.points.Add(p0);
            this.points.Add(p1);
            this.p0.AddNeighbour(this.p1);
            if (major)
            {
                this.p0.AddMainPath(this);
                this.p1.AddMainPath(this);
            }
            else
            {
                this.p0.AddMinorPath(this);
                this.p1.AddMinorPath(this);
            }
            this.CalculatePathLength();
            Id += instanceIndex++;
        }

        public static int CompareLengths_MAX (LineSegment segment0, LineSegment segment1)
		{
			float length0 = Vector2.Distance (segment0.p0.pos, segment0.p1.pos);
			float length1 = Vector2.Distance (segment1.p0.pos, segment1.p1.pos);
			if (length0 < length1) {
				return 1;
			}
			if (length0 > length1) {
				return -1;
			}
			return 0;
		}

        public Vector2? GetIntersectionPoint(LineSegment s)
        {
            var p1 = this.p0.pos;
            var p2 = this.p1.pos;
            var p3 = s.p0.pos;
            var p4 = s.p1.pos;
            return VectorIntersect.GetIntersectionPoint(p1, p2, p3, p4);
        }

        public bool Intersects(LineSegment s)
        {
            return GetIntersectionPoint(s) != null;
        }

		public static int CompareLengths (LineSegment edge0, LineSegment edge1)
		{
			return - CompareLengths_MAX (edge0, edge1);
		}

        public bool ContainsEdgePointPos(PtWSgmnts point)
        {
            return (PointsComparer.SameCoords(point, p0) || PointsComparer.SameCoords(point, p1));
        }
        public bool ContainsEdgePointId(PtWSgmnts point)
        {
            return (PointsComparer.SameId(point, p0) || PointsComparer.SameId(point, p1));
        }

        public bool ContainsAnyEdgePointPos(params PtWSgmnts[] points)
        {
			return points.Any(p => ContainsEdgePointPos(p));
        }

        public bool ContainsAnyEdgePointId(params PtWSgmnts[] points)
        {
            return points.Any(p => ContainsEdgePointId(p));
        }

        public bool ContainsAllEdgePointsPos(params PtWSgmnts[] points)
        {
            return points.All(p => ContainsEdgePointPos(p)) && points.Length == 2;
        }

        public PtWSgmnts TheOtherPoint(PtWSgmnts p) => TheOtherPoint(p.pos);

        public PtWSgmnts TheOtherPoint(Vector2 p)
        {
            if (p == p0.pos)
            {
                return p1;
            }
            else if (p == p1.pos)
            {
                return p0;
            }
            else
            {
                throw new ArgumentException("The point is not part of the line segment");
            }
        }

        internal void PointInnerCircleInters(PtWSgmnts castleGate)
        {
            if (!points.Contains(castleGate))
                Debug.LogError("Gate point is not part of the road!!");
            this.InnerCircleInters = castleGate;
        }

        public void ExtendIfBelow(float minLength)
        {
            if (this.Length < minLength)
            {
                var missingLength = minLength - this.Length;
                var middlePoint = Vector2.Lerp(p0.pos, p1.pos, 0.5f);
                this.p1.pos += (this.p1.pos - middlePoint) * missingLength;
                this.p0.pos -= (middlePoint - this.p0.pos) * missingLength;
                this.CalculatePathLength();
            }
        }

        public float AngleBetweenLine(LineSegment segment)
        {
            float angle = Vector2.Angle(p1.pos - p0.pos, segment.p1.pos - segment.p0.pos);
            return angle;
        }

        internal (LineSegment, LineSegment, LineSegment) GetSubSegmentsIntersecting(LineSegment path)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                for (int j = 0; j < path.points.Count - 1; j++)
                {
                    var intersection = VectorIntersect.GetIntersectionPoint(points[i].pos, points[i + 1].pos,
                                                                                path.points[j].pos, path.points[j + 1].pos);
                    if (intersection != null)
                    {
                        return (this,
                                CreateTempSegment(points[i], points[i + 1]),
                                CreateTempSegment(path.points[j], path.points[j + 1]));
                    }

                }
            }
            return (this, null, null);
        }

        public void ReplaceCheckpoints(IEnumerable<PtWSgmnts> points)
        {
            this.points = points.ToList();
            this.Joined = true;
            this.Joined = false;
        }

        public void InsertInnerCircleIntersection(PtWSgmnts gate, int index)
        {
            this.points.Insert(index, gate);
            this.PointInnerCircleInters(gate);
            var gateIndex = this.IntersIndex;
        }

        internal PtWSgmnts IntersectCheckpoint(LineSegment intersPath, PtWSgmnts intersectingCheckPoint, bool major = false)
        {
            this.HasConnectionWithCity = true;
            IntersectCheckpoint(intersectingCheckPoint, intersPath.p0, intersPath.p1);
            return intersectingCheckPoint;
        }

        internal PtWSgmnts IntersectCheckpoint(LineSegment intersPath, Vector2 intersectionPoint, bool major = false)
        {
            var intersectingCheckPoint = new PtWSgmnts(intersectionPoint);
            IntersectCheckpoint(intersectingCheckPoint, intersPath.p0, intersPath.p1);
            return intersectingCheckPoint;
        }

        void IntersectCheckpoint(PtWSgmnts pws, PtWSgmnts cp1, PtWSgmnts cp2)
        {
            var cp1Index = points.IndexOf(cp1);
            var cp2Index = points.IndexOf(cp2);
            var index = 0;
            if (Vector2.Distance(this.EdgePoints[0].pos, cp1.pos) < Vector2.Distance(this.EdgePoints[0].pos, cp2.pos))
            {
                index = Math.Max(cp1Index, cp2Index);
            }
            else if (Vector2.Distance(this.EdgePoints[1].pos, cp1.pos) < Vector2.Distance(this.EdgePoints[1].pos, cp2.pos))
            {
                index = Math.Min(cp1Index, cp2Index);
            }
            else
            {
                throw new Exception("No intersection.");
            }
            this.points.Insert(index, pws);
        }

        internal Vector2 EdgeDiff()
        {
            return p1.pos - p0.pos;
        }
        public virtual List<PtWSgmnts> GetPointsUntilGate(bool includeFirst = true)
        {
            if (InnerCircleInters != null)
            {
                var list = points.Take(IntersIndex.Value + 1).ToList();
                if(!list.Last().IntersectsWIthMainRoad)
                {

                }
                if (!includeFirst)
                    list.RemoveAt(0);
                return list;
            }
            return points;
        }

        public static void WieldIntersectingRoads(params LineSegment[] roadsArr) => WieldIntersectingRoads(null, roadsArr);

        public static void WieldIntersectingRoads(int? indexToBeMerged, params LineSegment[] roadsArr)
        {
            var roads = roadsArr.Take(2).ToList();
            if (roads[1].points.Contains(roads[0].p0) || roads[0].CptsNoEdges.Contains(roads[1].p0))
            {
                return;
            }

            var r0PtsTillGts = roads[0].GetPointsUntilGate();
            r0PtsTillGts.Reverse();
            var r1PtsTillGts = roads[1].GetPointsUntilGate();
            r0PtsTillGts.Reverse();
            for (int i = 1; i < r0PtsTillGts.Count - 1; i++)
            {
                for (int j = 1; j < r1PtsTillGts.Count - 1; j++)
                {
                    var intersPoint = VectorIntersect.GetIntersectionPoint(r0PtsTillGts[i - 1], r0PtsTillGts[i], r1PtsTillGts[j - 1], r1PtsTillGts[j]);
                    if (intersPoint.HasValue)
                    {
                        LineSegment roadToMergeTo = null;
                        LineSegment roadToBeMerged = null;
                        if (indexToBeMerged.HasValue)
                        {
                            roadToMergeTo = roads[1 - indexToBeMerged.Value];
                        }
                        else
                        {
                            var dist0 = Vector2.Distance(r0PtsTillGts[i].pos, roads[0].p0.pos);
                            var dist1 = Vector2.Distance(r1PtsTillGts[i].pos, roads[1].p0.pos);

                            indexToBeMerged = (dist0 < dist1) ? 1 : 0;
                            roadToMergeTo = roads[1 - indexToBeMerged.Value];
                        }

                        roadToBeMerged = roads[indexToBeMerged.Value];

                        if (roads[0] == roadToBeMerged)
                        {
                            var newCpts = roadToBeMerged.points.TakeStartingFrom(i - 1);
                            newCpts.InsertRange(0, roads[1].points.Take(i + 1));
                            roadToBeMerged.ReplaceCheckpoints(newCpts);
                        }
                        else
                        {
                            var newCpts = roadToBeMerged.points.TakeStartingFrom(j - 1);
                            newCpts.InsertRange(0, roads[0].points.Take(j + 1));
                            roadToBeMerged.ReplaceCheckpoints(newCpts);
                        }

                        if (!roadToMergeTo.ContainsCheckpoint(roadToBeMerged.p0))
                        {

                        }
                        return;
                    }
                }
            }
        }

        internal bool ContainsAnyPoint(IEnumerable<PtWSgmnts> points)
        {
            return this.points.Any(p => points.Contains(p));
        }

        public void Draw(Texture2D a_Texture)
        {
            float x1 = p0.pos.x;
            float y1 = p0.pos.y;
            float x2 = p1.pos.x;
            float y2 = p1.pos.y;   
                
            Color a_Color = Color.black;

            float xPix = x1;
            float yPix = y1;

            float width = x2 - x1;
            float height = y2 - y1;
            float length = Mathf.Abs(width);
            if (Mathf.Abs(height) > length) length = Mathf.Abs(height);
            int intLength = (int)length;
            float dx = width / (float)length;
            float dy = height / (float)length;
            for (int i = 0; i <= intLength; i++)
            {
                a_Texture.SetPixel((int)xPix, (int)yPix, a_Color);

                xPix += dx;
                yPix += dy;
            }
        }

        internal List<PtWSgmnts> GetPointsUntilInnerCircle(bool includeFirst = true)
        {
            return GetPointsUntilGate(includeFirst);
        }

        internal LineSegment ReplaceCloserEdgePt(Vector2 distanceTo, PtWSgmnts replaceWith)
        {
            var oldSegment = new LineSegment(p0, p1);
            if (this.p0.DistanceTo(distanceTo) < this.p1.DistanceTo(distanceTo))
            {
                p0 = replaceWith;
            }
            else
            {
                p1 = replaceWith;
            }         
            return oldSegment;
        }

        internal LineSegment ReplaceOutsideEdgePtReturnOld(Vector2 distanceTo, PtWSgmnts replaceWith, District d)
        {
            var oldSegment = new LineSegment(p0, p1);

            if (d.ContainsPoint(p1) && !d.ContainsPoint(p0))
            {
                p0 = replaceWith;
            }
            else if (!d.ContainsPoint(p1) && d.ContainsPoint(p0))
            {
                p1 = replaceWith;
            }
            else if (!d.ContainsPoint(p1) && !d.ContainsPoint(p0))
            {
                if (this.p0.DistanceTo(replaceWith) < this.p1.DistanceTo(replaceWith))
                    p0 = replaceWith;
                else p1 = replaceWith;
            }
            else // both points are inside the district
            {
                if(!d.ContainsCheckpoint(p1) && !d.ContainsCheckpoint(p0))
                    Debug.LogWarning($"Both points inside district -> there should be no intersection!! {replaceWith.pos}");
            }
            return oldSegment;
        }

        internal int ReplaceEdgePointWithSamePos(LineSegment line)
        {
            int count = 0;
            if (PosMatchButNotId(p0, line.p0))
            {
                this.p0 = line.p0;
                count++;
            }
            if (PosMatchButNotId(p1, line.p0))
            {
                this.p1 = line.p0;
                count++;
            }

            if (PosMatchButNotId(p0, line.p1))
            {
                this.p0 = line.p1;
                count++;
            }
            else if (PosMatchButNotId(p1, line.p1))
            {
                this.p1 = line.p1;
                count++;
            }
            if (count > 1)
            {
                Debug.LogWarning($"!!Replaced {count} points!!!");
            }
            return count;
        }

        bool CloseEngough(PtWSgmnts p1, PtWSgmnts p2, float minDist = 0.3f)
        {
            return p1.DistanceTo(p2) < minDist;
        }

        bool PosMatchButNotId(PtWSgmnts p1, PtWSgmnts p2, float epsilon = .4f)
        {
            return Vector2.Distance(p1.pos, p2.pos) < epsilon && p1.Id != p2.Id;
        }
    }

    public class SegmentComparer : EqualityComparer<LineSegment>
    {
        public static bool StaticEq(LineSegment s1, LineSegment s2)
        {
            var result = PointsComparer.SameCoords(s1.p1, s2.p1) && PointsComparer.SameCoords(s1.p0, s2.p0) && s1.Id == s2.Id;
            return result;
        }

        public static bool SameCoords(LineSegment s1, LineSegment s2)
        {
            var result = PointsComparer.SameCoords(s1.p1, s2.p1) && PointsComparer.SameCoords(s1.p0, s2.p0) && s1.Id != s2.Id;
            return result;
        }

        public override bool Equals(LineSegment s1, LineSegment s2)
        {
            return StaticEq(s1, s2);
        }

        public override int GetHashCode(LineSegment s)
        {
            return s.Id;
        }
    }
}
