using Delaunay.Geo;
using System.Collections.Generic;

namespace Assets.Game.Scripts.Gen.Models
{
    public class WorldModel
    {
        public int worldSeed;
        public static List<LineSegment> m_delaunayTriangulation;

        private List<PtWSgmnts> cities { get; set; } = new List<PtWSgmnts>();
        public List<SettlementModel> settlementModels { get; protected set; } = new List<SettlementModel>();

        public List<LineSegment> mainRoads { get; protected set; } = new List<LineSegment>();

        public WorldModel(List<PtWSgmnts> points, List<LineSegment> mainRoads)
        {
            this.cities = points;
            this.mainRoads = mainRoads;
            foreach (var point in cities)
            {
                var settlement = new SettlementModel(point);
                settlementModels.Add(settlement);
            }
        }
    }
}