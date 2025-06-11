

using Assets.Game.Scripts.Utility;
using Delaunay.Geo;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Game.Scripts.Gen.Models
{
    public class Lot: Polygon
    {
        public District district => parentBlock.parentDistrict;
        public Block parentBlock { get; set; }
        public List<Street> streets = new();
        public Lot(Block parentBlock)
        {
            this.parentBlock = parentBlock;
        }

        public Lot(List<Vector2> points)
        {
            this.points = points.Select(p => new PtWSgmnts(p)).ToList();
            this.parentBlock = null;
        }
        internal void AssignStreets(List<Street> streets)
        {
            this.streets = streets.Where(s => s.ContainsAnyPoint(this.points)).ToList();
            foreach (var street in this.streets)
            {
                street.Lots.Add(this);
            }
        }

        public void AbsorbPolygon(Lot lotJ, List<PtWSgmnts> containedPoints)
        {
            lotJ.points.RemoveList(containedPoints);
            this.AbsorbPolygon(lotJ);
        }

        public void AbsorbPolygon(Lot lotJ)
        {
            this.points.AddRange(lotJ.points);
            lotJ.points.Clear();

            var center = this.FindCenter();
            this.points = this.points.OrderBy(p => Vector2.Angle(p.pos - center, center + Vector2.one - center)).ToList();
            this.points = this.points.Distinct(new PointsComparer(false)).ToList();
        }
    }
}
