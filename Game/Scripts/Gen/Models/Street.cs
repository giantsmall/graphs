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
        public bool wallStreet = false;

        public static uint index { get; private set; } = 0;
        public new uint Id { get; protected set; }
        public List<Lot> Lots { get; internal set; } = new();

        public Street(bool wallStreet = false)
        {
            this.Id = index++;
            this.wallStreet = wallStreet;
        }

        public Street(List<PtWSgmnts> points, bool wallStreet = false) : base(points)
        {
            this.Id = index++;
            this.wallStreet = wallStreet;
        }

        public Street(PtWSgmnts start, PtWSgmnts end) : base()
        {
            this.Id = index++;
            this.points.Add(start);
            this.points.Add(end);
        }
    }
}