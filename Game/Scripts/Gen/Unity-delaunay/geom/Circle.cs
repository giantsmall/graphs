using UnityEngine;
using System;
using Assets.Game.Scripts.Utility;

namespace Delaunay
{	
	namespace Geo
	{
		public sealed class Circle
		{
			public Vector2 center;
			public float radius;
		
			public Circle (float centerX, float centerY, float radius)
			{
				this.center = new Vector2 (centerX, centerY);
				this.radius = radius;
			}

			public DelaunayPolygon ToPolygon(System.Random rnd, int numPoints, float dev = 0f)
            {
                DelaunayPolygon polygon = new DelaunayPolygon();
                float angleStep = 2 * Mathf.PI / numPoints;

				dev = rnd.NextFloat(dev - 1, dev + 1);
                for (int i = 0; i < numPoints; i++)
                {
                    float angle = i * angleStep;
                    float x = center.x + radius * Mathf.Cos(angle);					
                    float y = center.y + radius * Mathf.Sin(angle);
					if(dev != 0)
					{
						x += Mathf.Cos(rnd.NextFloat(2 * MathF.PI)) * dev;
                        y += Mathf.Cos(rnd.NextFloat(2 * MathF.PI)) * dev;
                    }
                    polygon.Add(new Vector2(x, y));
                }
                return polygon;
            }

            public override string ToString ()
			{
				return "Circle (center: " + center.ToString () + "; radius: " + radius.ToString () + ")";
			}

		}
	}
}