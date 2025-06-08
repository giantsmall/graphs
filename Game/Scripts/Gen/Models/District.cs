using Assets.Game.Scripts.Gen.GraphGenerator;
using Assets.Game.Scripts.Utility;
using Delaunay.Geo;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Game.Scripts.Gen.Models
{
    public class District : Polygon
    {
        public static uint index { get; private set; } = 0;
        public uint Id { get; protected set; }

        public List<Block> Blocks { get; protected set; } = new();
        //protected List<PtWSgmnts>[] pointGroups;
        public List<Street> bStr = new();
        internal List<PtWSgmnts> intersections = new();

        public District(params Vector2[] vertices) : base(vertices)
        {
            Id = index++;
        }

        public District(params PtWSgmnts[] points) : base(points)
        {
            Id = index++;
        }

        public District(List<Street> streets, params List<PtWSgmnts>[] points) : base(points)
        {
            Id = index++;
            //pointGroups = points;
            this.bStr = streets;
        }

        public District(List<Street> streets) : base()
        {
            Id = index++;
            //pointGroups = points;
            this.bStr = streets;
            this.RefreshPtsFromRoads(true);
        }

        public void RefreshPtsFromRoads(bool FirstForlastRoad = true)
        {
            var r1InnPts = bStr[0].GetPointsUntilInnerCircle();
            var InnCirPts = bStr[1].points.TakeLesserRangeWrapped(bStr[0].InnerCircleInters, bStr[2].InnerCircleInters, false);
            var r2InnPts = bStr[2].GetPointsUntilInnerCircle(FirstForlastRoad).Reversed();

            var pointArrays = new List<PtWSgmnts>[] { r1InnPts, InnCirPts, r2InnPts };
            var many = pointArrays.SelectMany(p => p).ToList();
            this.points = many.Distinct(new PointsComparer(true)).ToList();
            this.points.Clear();
            this.points.AddRange(r1InnPts);
            this.points.AddRange(InnCirPts);
            this.points.AddRange(r2InnPts);
        }

        [Obsolete]
        public List<District> Subdivide(SettlementModel s, float maxArea = 200, bool shift = false, int index = 0)
        {
            return new List<District>();
        }

        private void RemoveUnusedStreets()
        {
            var list = new List<Street>();
            for (int i = bStr.Count - 1; i >= 0; i--)
            {
                if (!bStr[i].points.Any(bsp => this.points.Contains(bsp)))
                {
                    list.Add(bStr[i]);
                }
            }
            bStr.RemoveList(list);
        }

        List<PtWSgmnts> SplitSegment(PtWSgmnts p1, PtWSgmnts p2, float min = .8f, float max = 1f)
        {
            var rnd = RoadGraphGenChaos.GetRandom();
            var middlePoints = new List<PtWSgmnts>();
            var tooBig = Vector2.Distance(p1.pos, p2.pos) > max;
            var tooShort = Vector2.Distance(p1.pos, p2.pos) < min;
            if (!tooBig && !tooShort)
            {
                var p = new PtWSgmnts(Vector2.Lerp(p1.pos, p2.pos, rnd.NextFloat(.4f, .6f)));                
                middlePoints.Add(p);
            }
            else if (tooBig)
            {
                var mp = new PtWSgmnts(Vector2.Lerp(p1.pos, p2.pos, .33f) + rnd.NextVector2(.5f));
                var mp2 = new PtWSgmnts(Vector2.Lerp(mp.pos, p2.pos, .5f) + rnd.NextVector2(.5f));
                middlePoints.Add(mp);
                middlePoints.Add(mp2);
            }
            return middlePoints;
        }
      

        public void SplitIntoBlocksWithinInnerCircle(SettlementModel s, float maxBlockSize)
        {
            var rnd = RoadGraphGenChaos.GetRandom();
            this.Blocks = new List<Block>();
            var block = new Block(this.bStr, this, this.points);
            if (this.CalculateArea() > maxBlockSize)
            {
                var (totalBlocks, newRoads) = block.DivideIntoBlocks(this, maxBlockSize);                
                this.Blocks.AddRange(totalBlocks);
                s.Blocks.AddRange(totalBlocks);
                this.bStr.AddRange(newRoads);
                s.blockDivStreets.AddRange(newRoads);
            }
            else
            {
                s.Blocks.Add(block);
            }
        }

        public void SplitIntoBlocksOutsideInnerCircle(SettlementModel s, float maxBlockSize)
        {
            throw new NotImplementedException();
        }

        public static PtWSgmnts GetClosestPoint(Vector2 closestTo, IEnumerable<PtWSgmnts> points)
        {
            var cloestToP = new PtWSgmnts(closestTo);
            return points.OrderBy(p => p.DistanceTo(cloestToP)).First();
        }

        internal List<Vector2> GetRandomPointsInside(int ptCount = 4)
        {
            var distFactor = 1f;
            var rnd = RoadGraphGenChaos.GetRandom();
            var list = new List<Vector2>();
            var center = this.FindCenter();
            var edgePts = this.points.Where(p => p.IntersectsWIthMainRoad).OrderByDescending(p => this.points.IndexOf(p)).ToList();

            list.Add(center + rnd.NextVector2());            
            foreach(var pt in edgePts)
            {
                var newPt = Vector2.Lerp(pt.pos, center, rnd.NextFloat(.15f, .25f)) + rnd.NextVector2(distFactor);
                list.Add(newPt);
            }
            var newPt2 = Vector2.Lerp(this.points[0].pos, center, rnd.NextFloat(.3f, .5f)) + rnd.NextVector2(distFactor);
            list.Add(newPt2);
            newPt2 = this.points[0].pos + (center - this.points[0].pos) * 1.5f + rnd.NextVector2(distFactor);
            list.Add(newPt2);

            var closestPtToCenter = this.points.Select(p => p.DistanceTo(center)).OrderBy(d => d).First();
            do
            {
                var len = rnd.NextFloat(closestPtToCenter * .5f) + closestPtToCenter * .3f;
                var pt = new PtWSgmnts(center + new Vector2(len, 0));
                pt.GetRotatedAround(center, rnd.NextFloat(360));
                list.Add(pt.pos);
            }
            while (list.Count < ptCount + 1);
            list.RemoveRandom(rnd);
            return list;
        }

        internal void ReorderPointsByAngle()
        {
            var center = this.FindCenter();
            this.points = this.points.ReorderPointsByAngleCW(center);
            var p0 = this.points[0];
            this.points = this.points
                .Distinct(new PointsComparer(true))
                .OrderBy(p => Vector2.SignedAngle(Vector2.right, p.pos - center)) // bardziej stabilne ni¿ `Angle()`
                .ToList();

            if (this.points[0].Id != p0.Id)
            {
                var p0Index = this.points.IndexOf(p0);
                var init = this.points.Take(p0Index).ToList();
                var rest = this.points.TakeStartingFrom(p0Index - 1);
                this.points.Clear();
                this.points.AddRange(rest);
                this.points.AddRange(init);
            }
        }

        internal List<PtWSgmnts> CheckForDistrictEdgePoints(SettlementModel s)
        {
            var r1InnerPts = bStr[0].GetPointsUntilInnerCircle();
            var r2InnerPts = bStr[2].GetPointsUntilInnerCircle(false);
            var index1 = bStr[0].IntersIndex;
            var incex2 = bStr[2].IntersIndex;
            var innerCircPts = bStr[bStr.IndexOf(s.innerCircleStreet)].points.TakeLesserRangeWrapped(bStr[0].InnerCircleInters, bStr[2].InnerCircleInters, false);
            
            var newPts = new List<PtWSgmnts>();
            newPts.AddRange(r1InnerPts.Where(p => !this.ContainsCheckpoint(p)).ToList());
            newPts.AddRange(innerCircPts.Where(p => !this.ContainsCheckpoint(p)).ToList());
            newPts.AddRange(r2InnerPts.Reversed().Where(p => !this.ContainsCheckpoint(p)).ToList());

            if (newPts.Any())
            {
                this.points.AddRange(newPts);

                // Spróbuj posortowaæ, jeœli s¹ duplikaty
                var center = this.FindCenter();
                var distinctPts = this.points.Distinct(new PointsComparer(false)).ToList();
                if (distinctPts.Count < this.points.Count)
                {
                    this.points = this.points
                        .OrderBy(p => Vector2.SignedAngle(p.pos - center, Vector2.right))
                        .ToList();
                }
            }

            return this.points;
        }

        // Proste porównanie dwóch list punktów pod wzglêdem pozycji (z tolerancj¹)
        private bool ArePointListsEqual(List<PtWSgmnts> a, List<PtWSgmnts> b)
        {
            if (a.Count != b.Count)
                return false;

            var comparer = new PointsComparer();
            for (int i = 0; i < a.Count; i++)
            {
                if (!comparer.Equals(a[i], b[i]))
                    return false;
            }
            return true;
        }
    }
}
