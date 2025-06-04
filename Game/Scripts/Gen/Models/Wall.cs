using Assets.Game.Scripts.Utility;
using Delaunay.Geo;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Game.Scripts.Gen.Models
{
    public class Wall: LineSegment
    {
        public Street innerWallStreet { get; protected set; } = new(true);
        public LineSegment moat = new();
        public Street outerWallStreet { get; protected set; } = new(true);

        public List<PtWSgmnts> gates = new List<PtWSgmnts>();
        public Wall()
        {
        }

        public Wall(List<Vector2> points, bool Moat = false) 
        {
            if (!points.Any())
                return;

            this.innerWallStreet.points.AddRange(points.Select(p => new PtWSgmnts(p)));

            this.BuildRoadsAndMoat(Moat);
        }

        
        public override List<PtWSgmnts> GetPointsUntilGate(bool takeFirst = true)
        {
            throw new Exception();
        }

        internal List<PtWSgmnts> GetPointsBetweenGates(int start, int end, bool include = true)
        {
            return innerWallStreet.points.TakeLesserRangeWrapped(gates[start], gates[end], include);
        }

        internal List<PtWSgmnts> GetPointsBetweenGates(PtWSgmnts start, PtWSgmnts end, bool include = true)
        {
            return innerWallStreet.points.TakeLesserRangeWrapped(start, end, include);
        }

        public void BuildRoadsAndMoat(bool buildMoat)
        {
            var center = innerWallStreet.FindCenter();
            var innerRoadDistFromWall = .5f;
            var moatDistFromWall = 2 * innerRoadDistFromWall;
            foreach(var p in innerWallStreet.points)
            {
                var distToCenter = p.DistanceTo(center);
                var t = innerRoadDistFromWall / distToCenter;
                var wallPt = new PtWSgmnts(Vector2.Lerp(p.pos, center, t));
                var dist = p.pos - wallPt.pos;
                wallPt.pos = wallPt.pos + 2 * dist;

                this.points.Add(wallPt);    
                
                if(buildMoat)
                {
                    var moatPt = new PtWSgmnts(p.pos + 3 * dist);                    
                    var outerStreetPt = new PtWSgmnts(p.pos + 5 * dist);
                    moat.AddCheckPoints(moatPt);
                    outerWallStreet.AddCheckPoints(outerStreetPt);
                }
                else
                {
                    var outerWallPt = new PtWSgmnts(wallPt.pos + dist);
                    outerWallStreet.AddCheckPoints(outerWallPt);
                }
            }
        }
    }
}
