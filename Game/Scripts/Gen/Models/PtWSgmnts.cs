using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Assets.Game.Scripts.Utility;
using UnityEngine.Rendering;

namespace Assets.Game.Scripts.Gen.Models
{
    public class PtWSgmnts : Point
    {
        public override bool Equals(object obj)
        {
            return obj is PtWSgmnts other && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public static int instanceCount { get; private set; } = 0;
        public static void ResetCount () { instanceCount = 0; }
        public int Id { get; private set; } = -1;
        public bool Fixed { get; internal set; } = false;
        public bool IntersectsWIthMainRoad { get; internal set; }

        public List<LineSegment> majorPaths = new();
        public List<LineSegment> minorPaths = new();

        public List<PtWSgmnts> TriangleNeighbours = new();
        public List<PtWSgmnts> neighbourCities = new();
        public List<PtWSgmnts> neighbourVillages = new();
        public List<PtWSgmnts> Neighbours = new();
        public bool PartOfMainRoad = true;

        public PtWSgmnts(float x, float y) : base(x, y)
        {
            Id = instanceCount++;
        }

        public PtWSgmnts(Vector2 p, bool partOfMainRoad = true) : base(p.x, p.y)
        {
            Id = instanceCount++;
            PartOfMainRoad = partOfMainRoad;
        }

        public PtWSgmnts() : base(0, 0)
        {
            Id = instanceCount++;
        }

        public void AddMainPath(LineSegment segment)
        {
            if (!majorPaths.Contains(segment))
            {
                majorPaths.Add(segment);
                AddNeighbourCity(segment.TheOtherPoint(this));
            }
        }
        public void AddMajorPaths(params LineSegment[] segments)
        {
            foreach (var segment in segments)
                AddMainPath(segment);
        }

        public void AddRelations(params LineSegment[] segments)
        {
            foreach (var segment in segments)
            {
                var theOtherPoint = segment.TheOtherPoint(this);
                if (!TriangleNeighbours.Any(n => PointsComparer.StaticEq(n, theOtherPoint)))
                {
                    TriangleNeighbours.Add(theOtherPoint);
                }
            }
        }
        public Vector2 GetRotatedAround(Vector2 pt, float angle)
        {
            return this.pos.RotateAroundPivot(pt, angle);
        }

        public PtWSgmnts GetRotatedAround(PtWSgmnts pt, float angle)
        {
            return new PtWSgmnts(this.pos.RotateAroundPivot(pt.pos, angle));
        }

        bool ContainsNeighbourCity(PtWSgmnts p)
        {
            return this.neighbourCities.Any(n => PointsComparer.StaticEq(p, n));
        }

        public void AddNeighbours(List<PtWSgmnts> points)
        {
            this.AddNeighbours(points.ToArray());
        }


        public void AddNeighbours(params PtWSgmnts[] points)
        {
            foreach (var point in points)
            {
                if (!Neighbours.Select(n => n.Id).Contains(point.Id) && point != this)
                {
                    Neighbours.Add(point);                    
                    point.AddNeighbours(this);
                }
            }
        }

        public void AddNeighbourCity(params PtWSgmnts[] points)
        {
            foreach (var point in points)
            {
                if (!ContainsNeighbourCity(point))
                {
                    neighbourCities.Add(point);
                }
            }
        }

        internal void AddMinorPath(params LineSegment[] segments)
        {
            foreach(var s in segments)
            {
                if (!this.minorPaths.Contains(s))
                {
                    this.minorPaths.Add(s);
                    s.TheOtherPoint(this).AddMinorPath(s);
                }
            }
        }


        internal bool ContainsMinorPathTo(PtWSgmnts closestVillage)
        {
            return minorPaths.Any(p => p.ContainsEdgePointPos(closestVillage));
        }

        internal bool ContainsMinorPath(LineSegment path)
        {
            return minorPaths.Any(p => p.ContainsAllEdgePointsPos(path.EdgePoints));
        }

        internal bool ContainsMajorPath(LineSegment path)
        {
            return majorPaths.Any(p => p.ContainsAllEdgePointsPos(path.EdgePoints));
        }

        internal bool IsConnectedTo(PtWSgmnts point)
        {
            return this.majorPaths.Any(p => p.ContainsEdgePointPos(point)) 
                || this.minorPaths.Any(p => p.ContainsEdgePointPos(point));
        }

        public (LineSegment, LineSegment, LineSegment) GetSegmentsIntersecting(LineSegment path)
        {
            (var majorPath, var majorSubSegment, var minor) = this.majorPaths.Select(mp => mp.GetSubSegmentsIntersecting(path))
                                               .FirstOrDefault(r => r.Item2 != null && r.Item3 != null);
            return (majorPath, majorSubSegment, minor);
        }

        public void Distort(System.Random rnd, float maxDist = 1f)
        {
            this.pos += new Vector2(rnd.NextFloat(-maxDist, maxDist), rnd.NextFloat(-maxDist, maxDist));
        }

        internal void MakeMajor(LineSegment newPath)
        {
            if(this.ContainsMinorPath(newPath))
            {
                this.minorPaths.Remove(newPath);
            }
            else
            {

            }

            if(!this.ContainsMajorPath(newPath))
            {
                this.majorPaths.Add(newPath);
            }
            else
            {

            }
        }

        internal float DistanceTo(PtWSgmnts p)
        {
            return this.DistanceTo(p.pos);
        }
        internal float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(this.pos, p);
        }

        internal float DistanceToSegment(LineSegment s)
        {
            return DistanceToLine(s.p0, s.p1);            
        }

        internal float DistanceToLine(PtWSgmnts start, PtWSgmnts end)
        {
            //altitude of P in triangle P, p0, p1
            var a = start.DistanceTo(end.pos);
            var b = start.DistanceTo(this.pos);
            var c = end.DistanceTo(this.pos);
            var s = (a + b + c) / 2;
            var distance = 2 * Mathf.Sqrt(s * (s - a) * (s - b) * (s - c)) / a;
            return distance;
        }
    }

    public class PointsComparer : EqualityComparer<PtWSgmnts>
    {
        static bool boolIdMatters;
        public PointsComparer(bool idMatters = false)
        {
            boolIdMatters = idMatters;
        }


        public static bool SameCoords(PtWSgmnts p1, PtWSgmnts p2)
        {
            var sameCoord = p1.pos.x == p2.pos.x && p1.pos.y == p2.pos.y;
            return sameCoord;
        }
        
        public static bool DifferentId(PtWSgmnts p1, PtWSgmnts p2)
        {
            return p1.Id != p2.Id;
        }

        public static bool SameId(PtWSgmnts p1, PtWSgmnts p2)
        {
            return p1.Id == p2.Id;
        }

        public static bool StaticEq(PtWSgmnts p1, PtWSgmnts p2)
        {
            var sameCoord = SameCoords(p1, p2);
            
            var sameId = SameId(p1, p2);
            var result = boolIdMatters ? sameId : sameCoord;
            return result;
        }

        public override bool Equals(PtWSgmnts p1, PtWSgmnts p2)
        {
            return StaticEq(p1, p2);
        }

        public override int GetHashCode(PtWSgmnts p)
        {
            return (int)(p.pos.x * 10e10 + p.pos.y * 10e10);
        }
    }
}
