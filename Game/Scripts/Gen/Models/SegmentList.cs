using Delaunay.Geo;
using System.Collections.Generic;

namespace Assets.Game.Scripts.Gen.Models
{
    public abstract class SegmentList
    {
        public List<LineSegment> segments { get; protected set; } = new List<LineSegment>();        
        public byte Width { get; protected set; } = 1;
    }
}
