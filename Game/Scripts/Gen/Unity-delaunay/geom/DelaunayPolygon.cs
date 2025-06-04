using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Delaunay.Geo
{
	public class DelaunayPolygon
	{
		public List<Vector2> Vertices { get; set; }

		public DelaunayPolygon (params Vector2[] vertices)
		{
			Vertices = vertices.ToList();
		}
        public DelaunayPolygon(List<Vector2> vertices)
        {
            Vertices = vertices;
        }

        public DelaunayPolygon()
		{
            Vertices = new List<Vector2>();
        }
		public void Add(Vector2 vertex)
        {
            Vertices.Add(vertex);
        }

        public float Area ()
		{
			return Mathf.Abs (SignedDoubleArea () * 0.5f); // XXX: I'm a bit nervous about this; not sure what the * 0.5 is for, bithacking?
		}

		public Winding Winding ()
		{
			float signedDoubleArea = SignedDoubleArea ();
			if (signedDoubleArea < 0) {
				return Geo.Winding.CLOCKWISE;
			}
			if (signedDoubleArea > 0) {
				return Geo.Winding.COUNTERCLOCKWISE;
			}
			return Geo.Winding.NONE;
		}

		private float SignedDoubleArea () // XXX: I'm a bit nervous about this because Actionscript represents everything as doubles, not floats
		{
			int index, nextIndex;
			int n = Vertices.Count;
			Vector2 point, next;
			float signedDoubleArea = 0; // Losing lots of precision?
			for (index = 0; index < n; ++index) {
				nextIndex = (index + 1) % n;
				point = Vertices [index];
				next = Vertices [nextIndex];
				signedDoubleArea += point.x * next.y - next.x * point.y;
			}
			return signedDoubleArea;
		}
	}
}
