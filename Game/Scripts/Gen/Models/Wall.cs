using Assets.Game.Scripts.Utility;
using Delaunay.Geo;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Game.Scripts.Gen.Models
{
    public class Wall: Street
    {
        public Wall()
        {
        }

        public Wall(Polygon polygon, float thickness) :base(polygon.Points, thickness)
        {
            
        }
    }
}
