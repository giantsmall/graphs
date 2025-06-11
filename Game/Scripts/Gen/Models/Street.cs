using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Utility;
using System.IO;
using Delaunay.Geo;
using Unity.Collections;
using Unity.VisualScripting;
using System.Security.Principal;

namespace Assets.Game.Scripts.Gen.Models
{
    public class Street : LineSegment
    {
        public static uint index { get; private set; } = 0;
        public new uint Id { get; protected set; }
        public List<Lot> Lots { get; internal set; } = new();

        public Street()
        {
            this.Id = index++;
        }

        public Street(List<PtWSgmnts> points, float thickness) : base(points, thickness)
        {
            this.Id = index++;
        }

        public Street(PtWSgmnts start, PtWSgmnts end) : base()
        {
            this.Id = index++;
            this.points.Add(start);
            this.points.Add(end);
        }
    }
}