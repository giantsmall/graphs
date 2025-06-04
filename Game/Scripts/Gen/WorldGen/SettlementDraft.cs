using Assets.Game.Scripts.Gen.Models;
using Delaunay.Geo;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Game.Scripts.Gen.WorldGen
{
    public class SettlementDraft: DelaunayPolygon
    {
        public int Seed { get; protected set; } = 0;
        public string Name { get; protected set; } = "Settlement";
        public int Population { get; protected set; } = 0;
      

        public List<Point> mainRoadsDirections { get; protected set; } = new List<Point>();
        public SettlementDraft(PtWSgmnts point) //City, village -> big,avg,small
        {
            //pick closest neighbou
            
        }
    }
}