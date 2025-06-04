using Assets.Game.Scripts.Utility;
using Delaunay;
using Delaunay.Geo;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SysRand = System.Random;
namespace Assets.Game.Scripts.Gen.Models
{
    public class SettlementModel: PtWSgmnts
    {
        internal List<Vector2> roadDirs = new();
        internal Vector2 center => this.pos;
        internal District mainSquare;
        public SettlementModel(PtWSgmnts p) : base(p.pos)
        {            
            base.majorPaths = majorPaths;
            base.neighbourVillages = neighbourVillages;
            base.TriangleNeighbours = TriangleNeighbours;
            
            SysRand rnd = new SysRand();
            GenerateCityShape(rnd);
        }
        public DelaunayPolygon cityShape { get; protected set; } = new DelaunayPolygon();

        DelaunayPolygon GenerateCityShape(SysRand rnd)
        {
            var polygon = new Circle(pos.x, pos.y, 10f).ToPolygon(rnd, 9, 2f);
            return polygon;
        }

        internal void Draw(Texture2D tex)
        {
            SysRand rnd = new SysRand();
            var polygon = new Circle(pos.x, pos.y, 10f).ToPolygon(rnd, 9, 2f);

            for (int i = 0; i < polygon.Vertices.Count; i++)
            {
                var v = polygon.Vertices[i];
                var size = 1;
                var colors = new Color[(int)Mathf.Pow(size * 2, 2)];
                for (int j = 0; j < colors.Length; j++)
                {
                    colors[j] = Color.red;
                }
                try
                {
                    tex.SetPixels((int)(v.x - size), (int)(v.y - size), size * 2, size * 2, colors);
                }
                catch(Exception e)
                {
                    Debug.LogWarning(e);
                }
            }
        }

        public bool generated { get; protected set; } = false;
        //main roads
        public List<Street> mainRoads { get; set; }  = new();
        public List<Street> notJoinedRoads { get; set; } = new();//mainRoads.Where(r => !r.Joined).ToList();
        public List<LineSegment> blockDivStreets { get; protected set; } = new();
        public List<Street> districtDivStreets { get; protected set; } = new();
        //walls        
        public Wall wall { get; set; } = new Wall();
        public Wall Citadel { get; set; } = new Wall();
        
        public Street innerCircleStreet = new();
        public Street InnerCircle => wall.points.Any() ? wall.innerWallStreet : innerCircleStreet;
        //buildings
        public List<BuildingModel> buildings { get; protected set; } = new();
        //greens
        public List<DelaunayPolygon> greens { get; protected set; } = new();
        public List<River> rivers { get; protected set; } = new();
        public List<Vector2> OuterCircle { get; internal set; } = new();
        public List<District> InnerDistricts { get; internal set; } = new();
        public List<District> OuterDistricts { get; internal set; } = new();
        public List<Block> Blocks { get; internal set; } = new();
        public List<Lot> Lots { get; internal set; } = new();
    }    
}
