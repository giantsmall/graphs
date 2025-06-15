using Assets.Game.Scripts.Utility;
using Delaunay.Geo;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Game.Scripts.Gen.Models
{
    public class Moat : Street
    {
        public Moat()
        {
        }

        public Moat(Polygon polygon, float thickness) : base(polygon.Points, thickness)
        {

        }
    }
}
