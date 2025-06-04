using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Assets.Game.Scripts.Gen.Models
{
    public class Point
    {        
        public Vector2 pos { get; set; }

        public Point(float x, float y)
        {
            pos = new Vector2(x, y);
        }

        internal static PtWSgmnts GetCenter(params Point[] points)
        {
            var xAvg = points.Select(p => p.pos.x).Sum() / (float)points.Length;
            var yAvg = points.Select(p => p.pos.y).Sum() / (float)points.Length;
            return new PtWSgmnts(xAvg, yAvg);
        }
    }
}
