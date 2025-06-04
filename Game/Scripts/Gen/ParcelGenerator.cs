using Assets.Game.Scripts.Editors;
using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Utility;
using Delaunay;
using NUnit;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal.Internal;

namespace Assets.Game.Scripts.Gen
{
    public class ParcelGenerator: MonoBehaviour
    {
        public void Start()
        {

        }

        List<List<Vector2>> parcels = new();
        List<Vector2> cycle = new();
        public void OnDrawGizmos()
        {
            var shift = new Vector2(-25.5f, -25.5f);
            shift = Vector2.zero;
            foreach (var parcel in parcels)
            {
                GizmoDrawer.DrawVectorList(parcel, shift, true, 0.5f);
            //    shift += new Vector2(.15f, .15f);
            }

            shift = new Vector2(.1f, .1f);
            Gizmos.color = Color.yellow;
            GizmoDrawer.DrawVectorList(cycle, shift, true, 0.05f);
        }
        public void Generate()
        {
            index = 0;
            parcels.Clear();
            var rnd = new System.Random(985797232);
            var radius = 15f;
            var pointCount = rnd.Next(4, 8);
            cycle = GenerateRandomClosedCycle(pointCount, radius, rnd);
            cycle = cycle.RotateAroundCenter(-180);
            cycle = cycle.ReorderPointsByAngleCW();

            var last2 = cycle.TakeLast(2).ToList();
            cycle.RemoveList(last2);
            cycle.InsertRange(0, last2);
            //Debug.DrawRay(cycle[0], Vector2.up * 2f, Color.cyan);
            //Debug.DrawRay(cycle[1], Vector2.up * 2f, Color.cyan);
            //Debug.DrawRay(cycle[2], Vector2.up * 2f, Color.cyan);
            //SceneView.RepaintAll();
            //return;
            rnd = new System.Random();
            //parcels = MakeLotsByCuttingEdges(rnd, cycle.ToList(), rnd.NextFloat(4, 7));
            parcels = MakeLotsBasedOnAngle(rnd, cycle, 4, 6);
            Debug.Log($"Parcels created: {parcels.Count}");

            SceneView.RepaintAll();
        }

        class AngleDivision
        {
            private Vector2 currPolyPoint;
            private Vector2 newMeet;
            private bool PrimNext;
            private Vector2 NewMeetPrimCandidate, NewMeetPrimCandidateNext;
            public Vector2[] GetVericesForNextLot()
            {
                var vs = new List<Vector2>();
                vs.Add(newMeet);
                if (PrimNext)
                {
                    vs.Add(NewMeetPrimCandidateNext);
                }
                else
                {
                    vs.Add(NewMeetPrimCandidate);
                    vs.Add(currPolyPoint);
                }
                return vs.ToArray();
            }
            public Vector2[] GetVericesForPrevLot()
            {
                var vs = new List<Vector2>();
                vs.Add(newMeet);
                if (PrimNext)
                {
                    vs.Add(NewMeetPrimCandidate);
                    vs.Add(currPolyPoint);
                }
                else
                {
                    vs.Add(NewMeetPrimCandidateNext);
                }
                return vs.ToArray();
            }

            List<Vector2> pts => new List<Vector2>() {currPolyPoint,newMeet, NewMeetPrimCandidate, NewMeetPrimCandidateNext };

            static Color[] colors = new Color[] { Color.cyan, Color.magenta, Color.green, Color.blue, Color.red, Color.black, Color.gray};
            public AngleDivision(Vector2 currPolyPoint, Vector2 newMeet, Vector2 newMeetPrimCandCurr, Vector2 newMeetPrimCandNext, bool primNext)
            {
                this.currPolyPoint = currPolyPoint;
                this.newMeet = newMeet;
                this.NewMeetPrimCandidate = newMeetPrimCandCurr;
                this.NewMeetPrimCandidateNext = newMeetPrimCandNext;
                this.PrimNext = primNext;

                DrawPolygon(pts, colors[index++]);
            }
        }


        public static List<List<Vector2>> MakeLotsBasedOnAngle(System.Random rnd, List<Vector2> poly, float minDepth, float maxDepth)
        {
            var lots = new List<List<Vector2>>();
            var center = poly.FindCenter();
            //find angle around 90deg and take the whole thing
            //if opposite angle is different stay
            //for more less regular/circular cycle

            var angles = new List<float>();
            var depths = new List<float>();
            for (int i = 0; i < poly.Count; i++)
            {
                var prevP = poly[(i - 1 + poly.Count) % poly.Count];
                var nextP = poly[(i + 1) % poly.Count];
                var edge = prevP - poly[i];
                var nextEdge = nextP - poly[i];
                var angle = Vector2.Angle(edge, nextEdge);
                angles.Add(angle);
                depths.Add(rnd.NextFloat(minDepth, maxDepth));
            }

            Dictionary<int, AngleDivision> divElements = new Dictionary<int, AngleDivision>();

            for (int i = 0; i < angles.Count; i++)
            {
                var rnDep = depths[i];
                Vector2 ptNext = Vector2.zero;
                
                var prevP = poly.Neighbour(i, -1);
                var nextP = poly.Neighbour(i, 1);
                var next2P = poly.Neighbour(i, 2);
                Vector2 shallowMeetiPlus1, afterMiddle, shallowMeeti, meetInTheMiddle;
                if (angles[i] < 110)
                {
                    var inters1 = GetOffsetAndInters(nextP, rnDep, poly[i], prevP, poly, i == 4);
                    (meetInTheMiddle, shallowMeeti, shallowMeetiPlus1) = MeetIntersAndGetShallowMeetsForSmallAngle(poly, i, depths[i], depths.Neighbour(i, 1));
                    lots.Add(new List<Vector2>() { shallowMeeti, poly[i], shallowMeetiPlus1, meetInTheMiddle });
                }
                else
                {
                    var t = rnDep / ((poly[0] - nextP).magnitude);
                    var rotPt = Vector2.Lerp(poly[0], nextP, t);

                    meetInTheMiddle = MeetIntersectionsInTheMiddle(poly, i, depths[i], depths.Neighbour(i, 1));
                    (meetInTheMiddle, shallowMeeti, shallowMeetiPlus1) = MeetIntersAndGetShallowMeetsForBigAngle(poly, i, depths[i], depths.Neighbour(i, 1));

                    var x = Mathf.Sqrt(depths.Neighbour(i, 1).Pow(2) + depths[i].Pow(2));                    
                    t = x / (nextP - poly[i]).magnitude;
                    var sizeFactor = rnd.NextFloat();
                    var beforeMiddle = Vector2.Lerp(meetInTheMiddle, meetInTheMiddle + poly[i], t); //ratio between meetInTheMiddle and max back
                    var shallowBack = poly[i] - (beforeMiddle - meetInTheMiddle);
                    if (i == 3)
                        DrawRays(Color.red, meetInTheMiddle, shallowMeeti, shallowMeetiPlus1, beforeMiddle );

                    var y = x * Mathf.Sin(45f / Mathf.PI) / Mathf.Sin((90 - angles[i]) / 2f / Mathf.PI);
                    t = x / (next2P - nextP).magnitude;
                    shallowMeetiPlus1 = Vector2.Lerp(poly[i], nextP, t * (1 - sizeFactor));
                    afterMiddle = meetInTheMiddle + shallowMeetiPlus1 - poly[i];

                    //cornerLot
                    var lot = new List<Vector2>() { shallowBack, beforeMiddle, meetInTheMiddle, afterMiddle, shallowMeetiPlus1, poly[i] };
                    lot.ReorderPointsByAngleCCW();
                    lots.Add(lot);
                    //prevEdgeLot??
                }

                if (i == 3)
                    return lots;
            }

            return lots;

            for (int i = 0; i < divElements.Keys.Count - 1; i++)
            {
                var lot = new List<Vector2>();
                lot.AddRange(divElements[i].GetVericesForPrevLot());
                lot.AddRange(divElements[(i + 1) % poly.Count].GetVericesForNextLot());

                lots.Add(lot.ReorderPointsByAngleCCW());
            }
            return lots;
        }

        static Vector2 GetPerpendicularIntersection(Vector2 start, Vector2 end, Vector2 point)
        {
            Vector2 AB = end - start;
            Vector2 AP = point - start;

            float t = Vector2.Dot(AP, AB) / Vector2.Dot(AB, AB);

            // Punkt przecięcia
            Vector2 H = start + t * AB;
            return H;
        }


        static int index = 0;
        static Vector2 GetOffsetAndInters(Vector2 nextP, float rnDep, Vector2 currP, Vector2 prevP, List<Vector2> poly, bool draw = false)
        {
            var (offset1, offset2) = GetOffsetLine(nextP, currP, rnDep, (nextP - currP).magnitude * 2, poly);
            if(draw)
            {
                DrawPolygon(new List<Vector2> { nextP, currP }, (index++ == 0 ? Color.cyan : Color.red));
                DrawPolygon(new List<Vector2> { offset1, offset2 }, (index++ == 0 ? Color.cyan : Color.red));
            }
            return VectorIntersect.GetIntersectionPoint(offset1, offset2, currP, prevP).Value;
        }

        static Vector2 MeetIntersectionsInTheMiddle(List<Vector2> p, int vIndex, float distance, float nextDistance)
        {
            Vector2 result = Vector2.zero;
            var currP = p[vIndex];
            var prevP = p[(vIndex - 1 + p.Count) % p.Count];
            var nextP = p[(vIndex + 1) % p.Count];
            var offset1 = GetOffsetLine(prevP, currP, distance, (prevP - currP).magnitude / 2f, p);
            var offset2 = GetOffsetLine(nextP, currP, nextDistance, (nextP - currP).magnitude / 2f, p);

            //Debug.DrawLine(offset1.Item1, offset1.Item2, Color.cyan);
            //Debug.DrawLine(offset2.Item1, offset2.Item2, Color.magenta);

            var inters = VectorIntersect.GetIntersectionPoint(offset1.Item1, offset1.Item2, offset2.Item1, offset2.Item2);
            //Debug.DrawRay(inters.Value, Vector2.up * 3f, Color.red, 3f);
            return inters.Value;            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        /// <param name="vIndex"></param>
        /// <param name="distance"></param>
        /// <param name="nextDistance"></param>
        /// <returns>meetIntTheMiddle,shallowPrev,shallowNext</returns>
        static (Vector2, Vector2, Vector2) MeetIntersAndGetShallowMeetsForSmallAngle(List<Vector2> p, int vIndex, float distance, float nextDistance)
        {
            var currP = p[vIndex];
            var prevP = p[(vIndex - 1 + p.Count) % p.Count];
            var nextP = p[(vIndex + 1) % p.Count];
            var offset1 = GetOffsetLine(prevP, currP, distance, (prevP - currP).magnitude / 2f, p);
            var offset2 = GetOffsetLine(nextP, currP, nextDistance, (nextP - currP).magnitude / 2f, p);
            var inters = VectorIntersect.GetIntersectionPoint(offset1.Item1, offset1.Item2, offset2.Item1, offset2.Item2);
            var shallowPrev = GetPerpendicularIntersection(prevP, currP, inters.Value);
            var shallowNext = GetPerpendicularIntersection(nextP, currP, inters.Value);
            return (inters.Value, shallowPrev, shallowNext);
        }

        static (Vector2, Vector2, Vector2) MeetIntersAndGetShallowMeetsForBigAngle(List<Vector2> p, int vIndex, float distance, float nextDistance)
        {
            var currP = p[vIndex];
            var prevP = p[(vIndex - 1 + p.Count) % p.Count];
            var nextP = p[(vIndex + 1) % p.Count];
            var offset1 = GetOffsetLine(prevP, currP, distance, (prevP - currP).magnitude / 2f, p);
            var offset2 = GetOffsetLine(nextP, currP, nextDistance, (nextP - currP).magnitude / 2f, p);
            var inters = VectorIntersect.GetIntersectionPoint(offset1.Item1, offset1.Item2, offset2.Item1, offset2.Item2);
            var shallowPrev = GetPerpendicularIntersection(prevP, currP, inters.Value);
            var shallowNext = GetPerpendicularIntersection(nextP, currP, inters.Value);
            return (inters.Value, shallowPrev, shallowNext);
        }


        public static List<List<Vector2>> MakeLotsByCuttingEdges(System.Random rnd, List<Vector2> polygon, float depth)
        {                        
            var lots = new List<List<Vector2>>();
            var center = polygon.FindCenter();
            var longestEdge = polygon.GetLongestEdgeLength();
           
            HashSet<(Vector2, Vector2)> usedEdges = new();
            (Vector2, Vector2) lastEdges = new();
            while (usedEdges.Count < polygon.Count)
            {                
                var (bestP, bestP2) = FindBestEdge(polygon, usedEdges);
                if (bestP is null || bestP2 is null)
                {
                    break;                    
                }
                DrawRays(Color.cyan, bestP.Value, bestP2.Value);

                var (offset1, offset2) = GetOffsetLine(bestP.Value, bestP2.Value, rnd.NextFloat(depth - 1, depth + 1), longestEdge, polygon);
                DrawRays(Color.black, offset1, offset2);

                var intersDict = GetIntersections(polygon, offset1, offset2);
                if (!intersDict.Keys.Any())
                {
                    usedEdges.Add((bestP.Value, bestP2.Value));
                    continue;
                }
                DrawRays(Color.red, intersDict.Values.First(), intersDict.Values.Last());

                //polygon.Add(intersDict.Values.First());
                //var index1 = polygon.IndexOf(intersDict.Keys.First().Item1);
                //var index2 = polygon.IndexOf(intersDict.Keys.First().Item2);
                //var maxIndex = Math.Max(index1, index2);
                //polygon.Insert(maxIndex, intersDict[intersDict.Keys.First()]);

                //index1 = polygon.IndexOf(intersDict.Keys.Last().Item1);
                //index2 = polygon.IndexOf(intersDict.Keys.Last().Item2);
                //var minIndex = Math.Min(index1, index2);
                //polygon.Insert(minIndex, intersDict[intersDict.Keys.Last()]);
                polygon.AddRange(intersDict.Values.ToList());
                polygon = polygon.ReorderPointsByAngleCCW();

                List<Vector2> newLotPts = intersDict.Values.ToList();
                usedEdges.Add((newLotPts[0], newLotPts[1]));
                lastEdges = usedEdges.Last();
                var newLot = SplitPolygon(polygon, usedEdges.Last(), bestP.Value);
                lots.Add(newLot);

                var notBestP = polygon.First(p => !newLot.Contains(p));
                polygon = SplitPolygon(polygon, lastEdges, notBestP);
            }
            return lots;
        }

        public static void DrawPolygon(List<Vector2> polygon, Color color, Vector2? shift = null, bool drawRays = false)
        {
            Debug.Log($"Polygon points count: {polygon.Count}");
            var shiftVal = shift?? Vector2.zero;
            for (int i = 0; i < polygon.Count; i++)
            {
                var start = polygon[i] + shiftVal;
                var end = polygon[(i - 1 + polygon.Count) % polygon.Count] + shiftVal;
                Debug.DrawLine(start, end, color, 1f);
            }
            if (drawRays)
                DrawPolygonRays(polygon, color, shift);
        }        

        public static void DrawPolygonRays(List<Vector2> polygon, Color color, Vector2? shift = null)
        {
            var shiftVal = shift ?? Vector2.zero;
            Debug.Log($"Polygon points count: {polygon.Count}");
            for (int i = 0; i < polygon.Count; i++)
            {
                Debug.DrawRay(polygon[i] + shiftVal, Vector3.up * (i + 1), color, 5f);
            }
        }

        static void DrawRays(Color color, params Vector2[] pos)
        {
            var index = 1f;
            foreach (var p in pos)
            {
                Debug.DrawRay(p, Vector3.up * 1f * index, color, 3f);
                index += 2;
            }           
        }

        public static List<Vector2> SplitPolygon(List<Vector2> polygon, (Vector2, Vector2) offsets, params Vector2[] containedPoints)
        {
            Vector2 i0 = offsets.Item1;
            Vector2 i1 = offsets.Item2;

            int idx0 = polygon.IndexOf(i0);
            int idx1 = polygon.IndexOf(i1);

            if (idx0 == -1 || idx1 == -1 || idx0 == idx1)
                return null;

            var polyA = new List<Vector2>();
            int i = idx0;
            var step = idx0 < idx1 ? 1 : -1;
            do
            {
                polyA.Add(polygon[i]);
                i = i.WrapIndex(step, polygon);

            }
            while (!polyA.Contains(i1) && polyA.Count < polygon.Count);

            var polyB = new List<Vector2> ();
            i = idx1;
            do
            {
                polyB.Add(polygon[i]);
                i = i.WrapIndex(step, polygon);
            }
            while (!polyB.Contains(i0) && polyB.Count < polygon.Count);

            //DrawPolygon(polyA, Color.magenta, new Vector2(0, .5f));
            //DrawPolygon(polyB, Color.cyan, new Vector2(0, -.5f));

            if (polyB.ContainsAll(containedPoints))
                return polyB;
            else if (polyA.ContainsAll(containedPoints))
                return polyA;
            else return new List<Vector2>();
        }

        public static List<Vector2> MySplitPolygon(List<Vector2> polygon, (Vector2, Vector2) offsets, params Vector2[] containedPoints)
        {
            Vector2 i0 = offsets.Item1;
            Vector2 i1 = offsets.Item2;
            var polyA = new List<Vector2> { i0 };
            var polyB = new List<Vector2> { i1 };

            int idx0 = polygon.IndexOf(i0);
            int idx1 = polygon.IndexOf(i1);

            if (idx0 == -1 || idx1 == -1 || idx0 == idx1)
                return null;

            var i = idx0;
            do
            {
                polyA.Add(polygon[(i++) % polygon.Count]);
            }
            while (!polyA.Contains(i1));

            i = idx1;
            do
            {
                polyB.Add(polygon[(i++) % polygon.Count]);
            }
            while (!polyB.Contains(i0));

            DrawPolygon(polyA, Color.magenta, new Vector2(15, .5f));
            DrawPolygon(polyB, Color.cyan, new Vector2(15, -.5f));
            if (polyB.ContainsAll(containedPoints))
                return polyB;
            else return polyA;
        }


   
        public static Dictionary<(Vector2, Vector2), Vector2> GetIntersections(List<Vector2> polly, Vector2 offset1, Vector2 offset2)
        {
            var result = new Dictionary<(Vector2, Vector2), Vector2>();
            for (int i = 0; i < polly.Count; i++)
            {
                var prevP = polly[(i - 1 + polly.Count) % polly.Count];

                var inters = VectorIntersect.GetIntersectionPoint(prevP, polly[i], offset1, offset2);
                if (inters.HasValue)
                {
                    result.Add((prevP, polly[i]), inters.Value);
                    if (result.Keys.Count == 2)
                    {
                        return result;
                    }
                }
            }
            if(result.Keys.Count < 2)
            {
                result.Clear();
            }
            return result;
        }

        public static (Vector2?, Vector2?) FindBestEdge(List<Vector2> p, HashSet<(Vector2, Vector2)> edges)
        {
            var bestAnglesDict = new KeyValuePair<(Vector2?, Vector2?), float>((null, null), 999);
            for (int i = 0; i < p.Count; i++)
            {
                var prevP = p[(i - 1 + p.Count) % p.Count];
                var prev2P = p[(i - 2 + p.Count) % p.Count];
                var nextP = p[(i + 1) % p.Count];

                var prevEdge = prevP - prev2P;
                var edge = p[i] - prevP;
                var nextEdge = nextP - p[i];

                var prevAngle = Vector2.Angle(prevEdge, edge);
                var nextAngle = Vector2.Angle(nextEdge, edge);
                var angles = prevAngle - 90 + nextAngle - 90;
                if (angles < bestAnglesDict.Value && !edges.Contains((p[i], prevP)))
                {
                    bestAnglesDict = new KeyValuePair<(Vector2?, Vector2?), float>((p[i], prevP), angles);
                }                
            }
            return bestAnglesDict.Key;
        }

        public static (Vector2, Vector2) GetOffsetLine(Vector2 p0, Vector2 p1, float distance, float extendLength, List<Vector2> polygon)
        {
            var polCenter = polygon.FindCenter();
            Vector2 dir = (p1 - p0).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            Vector2 offset = normal * distance;
            Vector2 testPoint = p0 + (p1 - p0) / 2f + normal * 0.01f;

            //Debug.DrawRay(offset, Vector2.up * 2f, Color.yellow, 3f);
            if (!polygon.ContainsPoint(testPoint))
            {
                normal = -normal;
            }
            offset = normal * distance;

            var perP0 = p0 + offset;
            var perP1 = p1 + offset;

            Vector2 extendedStart = perP0 - dir * 2 * extendLength;
            Vector2 extendedEnd = perP1 + dir * 2 * extendLength;

            return (extendedStart, extendedEnd);
        }

        public static List<List<Vector2>> ExtractParallelLotsSmart(List<Vector2> polygon, float lotDepth)
        {
            var lots = new List<List<Vector2>>();
            var workingPolygon = new List<Vector2>(polygon);

            for (int step = 0; step < polygon.Count; step++)
            {
                int bestEdgeIndex = step % workingPolygon.Count;

                int nextIdx = (bestEdgeIndex + 1) % workingPolygon.Count;
                Vector2 p0 = workingPolygon[bestEdgeIndex];
                Vector2 p1 = workingPolygon[nextIdx];
                Vector2 edgeDir = (p1 - p0).normalized;
                Vector2 normal = new Vector2(-edgeDir.y, edgeDir.x);

                float maxAllowedDepth = Mathf.Min(lotDepth, (p1 - p0).magnitude * 1.5f);
                Vector2 offset = normal * maxAllowedDepth;
                Vector2 p0o = p0 + offset;
                Vector2 p1o = p1 + offset;

                Vector2? i0 = null;
                Vector2? i1 = null;

                for (int s = 1; s < workingPolygon.Count / 2; s++)
                {
                    Vector2 prev0 = workingPolygon[(bestEdgeIndex - s + workingPolygon.Count) % workingPolygon.Count];
                    Vector2 prev1 = workingPolygon[(bestEdgeIndex - s + 1 + workingPolygon.Count) % workingPolygon.Count];
                    i0 = IntersectLines(prev0, prev1, p0o, p1o);
                    if (i0 != null) break;
                }

                for (int s = 1; s < workingPolygon.Count / 2; s++)
                {
                    Vector2 next0 = workingPolygon[(nextIdx + s) % workingPolygon.Count];
                    Vector2 next1 = workingPolygon[(nextIdx + s + 1) % workingPolygon.Count];
                    i1 = IntersectLines(next0, next1, p0o, p1o);
                    if (i1 != null) break;
                }

                if (i0 == null || i1 == null) continue;

                var lot = new List<Vector2> { p0, p1, i1.Value, i0.Value };

                // Upewnij się, że lot jest zgodny ze wskazówkami zegara
                if (Vector2.SignedAngle(lot[1] - lot[0], lot[2] - lot[1]) < 0)
                    lot.Reverse();

                lots.Add(lot);

                workingPolygon = PolygonUtils.PolygonClip(workingPolygon, lot);
                if (workingPolygon.Count < 3) break;
            }

            return lots;
        }

        public static Vector2? IntersectLines(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, float maxDistFromSegment = 3f)
        {
            float x1 = a1.x, y1 = a1.y;
            float x2 = a2.x, y2 = a2.y;
            float x3 = b1.x, y3 = b1.y;
            float x4 = b2.x, y4 = b2.y;

            float denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Mathf.Abs(denom) < 1e-6f) return null;

            float px = ((x1 * y2 - y1 * x2) * (x3 - x4) - (x1 - x2) * (x3 * y4 - y3 * x4)) / denom;
            float py = ((x1 * y2 - y1 * x2) * (y3 - y4) - (y1 - y2) * (x3 * y4 - y3 * x4)) / denom;
            var intersection = new Vector2(px, py);

            Vector2 seg = b2 - b1;
            Vector2 toIntersection = intersection - b1;
            float proj = Vector2.Dot(toIntersection, seg.normalized);
            if (proj < -maxDistFromSegment || proj > seg.magnitude + maxDistFromSegment)
                return null;

            return intersection;
        }

        List<List<Vector2>> ExtractParallelLots(List<Vector2> cycle, float lotDepth)
        {
            var resultLots = new List<List<Vector2>>();
            var workingCycle = new List<Vector2>(cycle);

            for (int i = 0; i < cycle.Count; i++)
            {
                Vector2 p0 = cycle[i];
                Vector2 p1 = cycle[(i + 1) % cycle.Count];
                Vector2 dir = (p1 - p0).normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x);
                Vector2 offset = normal * lotDepth;

                // Tworzymy "pas" - równoległy czworokąt
                Vector2 p0o = p0 + offset;
                Vector2 p1o = p1 + offset;

                // Szukamy przecięć pasa z sąsiednimi krawędziami
                Vector2 prev = cycle[(i - 1 + cycle.Count) % cycle.Count];
                Vector2 next = cycle[(i + 2) % cycle.Count];
                
                Vector2? i0Null = VectorIntersect.IntersectLines(prev, p0, p0o, p1o); // wcześniejsza krawędź z dolnym bokiem
                Vector2? i1Null = VectorIntersect.IntersectLines(p1, next, p0o, p1o).Value; // następna krawędź z górnym bokiem

                var i0 = i0Null.Value;
                var i1 = i1Null.Value;

                if (!i0.IsValid() || !i1.IsValid()) continue;

                var lot = new List<Vector2> { p0, p1, i1, i0 };
                resultLots.Add(lot);

                // Odejmujemy działkę od oryginalnego cyklu
                workingCycle = PolygonUtils.PolygonClip(workingCycle, lot);
            }

            return resultLots;
        }


        private static List<Vector2> SubtractPolygon(List<Vector2> baseCycle, List<Vector2> parcel)
        {
            var newCycle = new List<Vector2>();

            for (int i = 0; i < baseCycle.Count; i++)
            {
                var pt = baseCycle[i];
                if (!IsPointInPolygon(pt, parcel))
                    newCycle.Add(pt);

                var next = baseCycle[(i + 1) % baseCycle.Count];

                // Wstaw parcel points między a i b jeśli sąsiadują
                foreach (var corner in parcel)
                {
                    if (IsPointOnSegment(corner, pt, next) && !newCycle.Contains(corner))
                        newCycle.Add(corner);
                }
            }

            // Usuwamy duplikaty
            newCycle = newCycle.Distinct().ToList();

            if (newCycle.Count > 0 && newCycle[0] != newCycle[^1])
                newCycle.Add(newCycle[0]);

            return newCycle;
        }
       

        private static bool IsPointOnSegment(Vector2 p, Vector2 a, Vector2 b, float epsilon = 0.01f)
        {
            var ab = b - a;
            var ap = p - a;
            float dot = Vector2.Dot(ab.normalized, ap.normalized);
            return dot > 0.99f && ap.magnitude <= ab.magnitude + epsilon;
        }
        private static List<Vector2> ReplaceEdgeWithPolygon(List<Vector2> baseCycle, int edgeIndex, List<Vector2> parcel)
        {
            var newCycle = new List<Vector2>();
            int nextIndex = (edgeIndex + 1) % baseCycle.Count;

            for (int i = 0; i < baseCycle.Count; i++)
            {
                if (i == edgeIndex)
                {
                    // wstaw działkę zamiast a->b
                    var inner = parcel.Skip(1).Take(parcel.Count - 2); // pomijamy p0 i p0 (zamknięcie)
                    newCycle.AddRange(inner);
                }
                else if (i != nextIndex)
                {
                    newCycle.Add(baseCycle[i]);
                }
            }

            if (newCycle.Count > 0 && newCycle[0] != newCycle[^1])
                newCycle.Add(newCycle[0]);

            return newCycle;
        }

        private static bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            int crossings = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];

                if ((a.y > point.y) != (b.y > point.y))
                {
                    float t = (point.y - a.y) / (b.y - a.y);
                    float xCross = a.x + t * (b.x - a.x);
                    if (point.x < xCross)
                        crossings++;
                }
            }
            return (crossings % 2) == 1;
        }



        private static List<Vector2> ClipPolygon(List<Vector2> poly, List<Vector2> clip)
        {
            var output = new List<Vector2>(poly);

            for (int i = 0; i < clip.Count; i++)
            {
                var input = new List<Vector2>(output);
                output.Clear();

                var a = clip[i];
                var b = clip[(i + 1) % clip.Count];
                var edgeDir = (b - a).normalized;
                var edgeNormal = new Vector2(-edgeDir.y, edgeDir.x);

                for (int j = 0; j < input.Count; j++)
                {
                    var curr = input[j];
                    var prev = input[(j - 1 + input.Count) % input.Count];

                    bool currInside = Vector2.Dot(curr - a, edgeNormal) >= 0;
                    bool prevInside = Vector2.Dot(prev - a, edgeNormal) >= 0;

                    if (currInside)
                    {
                        if (!prevInside)
                            output.Add(LineSegmentIntersection(prev, curr, a, b));

                        output.Add(curr);
                    }
                    else if (prevInside)
                    {
                        output.Add(LineSegmentIntersection(prev, curr, a, b));
                    }
                }
            }

            if (output.Count > 0 && output.First() != output.Last())
                output.Add(output.First());

            return output;
        }

        private static Vector2 LineSegmentIntersection(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            var r = p2 - p1;
            var s = q2 - q1;
            var denom = r.x * s.y - r.y * s.x;

            if (Mathf.Abs(denom) < 1e-6f) return (p1 + p2) / 2f;

            var t = ((q1 - p1).x * s.y - (q1 - p1).y * s.x) / denom;
            return p1 + t * r;
        }

        public static List<List<Vector2>> SubdivideBySeeds(List<Vector2> cycle, int seedCount, System.Random rng)
        {
            var result = new List<List<Vector2>>();

            if (cycle.First() == cycle.Last())
                cycle = cycle.Take(cycle.Count - 1).ToList();

            var bounds = GetBounds(cycle);
            var seeds = new List<Vector2>();

            int tries = 0;
            while (seeds.Count < seedCount && tries < seedCount * 10)
            {
                var pt = new Vector2(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, (float)rng.NextDouble()),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, (float)rng.NextDouble()));

                if (IsPointInPolygon(pt, cycle))
                    seeds.Add(pt);

                tries++;
            }

            for (int i = 0; i < cycle.Count; i++)
            {
                var a = cycle[i];
                var b = cycle[(i + 1) % cycle.Count];
                var mid = (a + b) * 0.5f;

                var closestSeed = seeds.OrderBy(s => Vector2.SqrMagnitude(mid - s)).First();
                var tri = new List<Vector2> { a, b, closestSeed, a };

                if (AllPointsInPolygon(tri, cycle))
                    result.Add(tri);
            }

            return result;
        }

        private static Rect GetBounds(List<Vector2> points)
        {
            var bounds = new Bounds(Vector3.zero, points[0]);
            foreach (var pt in points)
            {
                bounds.Encapsulate(pt);
            }            
            return new Rect(bounds.min, bounds.size);
        }

        public static List<List<Vector2>> SubdivideKanciasto(List<Vector2> cycle, float minDepth, float maxDepth, float minWidthRatio, float maxWidthRatio, System.Random rng)
        {
            var result = new List<List<Vector2>>();

            if (cycle.First() == cycle.Last())
                cycle = cycle.Take(cycle.Count - 1).ToList();

            for (int i = 0; i < cycle.Count; i++)
            {
                var a = cycle[i];
                var b = cycle[(i + 1) % cycle.Count];
                var edge = b - a;
                var edgeLength = edge.magnitude;
                var normal = new Vector2(-edge.y, edge.x).normalized;

                float t = 0f;
                Vector2? prevBackLeft = null;

                while (t < 1f)
                {
                    float tStep = Mathf.Clamp((float)rng.NextDouble() * (maxWidthRatio - minWidthRatio) + minWidthRatio, 0.05f, 0.5f);
                    if (t + tStep > 1f) tStep = 1f - t;

                    var p0 = Vector2.Lerp(a, b, t);
                    var p1 = Vector2.Lerp(a, b, t + tStep);
                    float depth = Mathf.Lerp(minDepth, maxDepth, (float)rng.NextDouble());
                    float inwardShift = (float)rng.NextDouble() * 0.4f + 0.8f;
                    var inward = normal * depth * inwardShift;

                    var backRight = p1 + inward;
                    var backLeft = p0 + inward;
                    var width = (p1 - p0).magnitude;

                    float minWidth = depth * 0.5f;
                    if (width < minWidth)
                    {
                        if (result.Count > 0 && result[^1].Count >= 5)
                        {
                            var last = result[^1];
                            last[2] = backRight;
                            last[3] = backLeft;
                            last[4] = last[0]; // zamknięcie
                        }
                        else
                        {
                            result.Add(new List<Vector2> { p0, p1, (p0 + p1) * 0.5f + inward, p0 });
                        }
                        t += tStep;
                        continue;
                    }

                    var poly = new List<Vector2> { p0, p1, backRight, backLeft, p0 };

                    if (AllPointsInPolygon(poly, cycle))
                    {
                        result.Add(poly);
                        prevBackLeft = backRight;
                    }
                    else
                    {
                        depth *= 0.5f;
                        inward = normal * depth * inwardShift;
                        backRight = p1 + inward;
                        backLeft = p0 + inward;
                        poly = new List<Vector2> { p0, p1, backRight, backLeft, p0 };

                        if (AllPointsInPolygon(poly, cycle))
                        {
                            result.Add(poly);
                            prevBackLeft = backRight;
                        }
                        else
                        {
                            break;
                        }
                    }

                    t += tStep;
                }
            }

            return result;
        }

        private static bool AllPointsInPolygon(List<Vector2> poly, List<Vector2> boundary)
        {
            foreach (var pt in poly)
            {
                if (!IsPointInPolygon(pt, boundary))
                    return false;
            }
            return true;
        }

       

        public static List<List<Vector2>> SubdivideAlongEdges(List<Vector2> cycle, float depth, System.Random rng)
        {
            var result = new List<List<Vector2>>();

            if (cycle.First() == cycle.Last())
                cycle = cycle.Take(cycle.Count - 1).ToList();

            for (int i = 0; i < cycle.Count; i++)
            {
                var a = cycle[i];
                var b = cycle[(i + 1) % cycle.Count];

                var edge = b - a;
                var normal = new Vector2(-edge.y, edge.x).normalized;

                int count = rng.Next(2, 6); // liczba działek przy tej krawędzi
                for (int j = 0; j < count; j++)
                {
                    float t0 = (float)j / count;
                    float t1 = (float)(j + 1) / count;

                    var p0 = Vector2.Lerp(a, b, t0);
                    var p1 = Vector2.Lerp(a, b, t1);
                    var p2 = p1 + normal * depth;
                    var p3 = p0 + normal * depth;

                    result.Add(new List<Vector2> { p0, p1, p2, p3, p0 }); // zamknięta działka
                }
            }

            return result;
        }
        public static List<List<Vector2>> SubdivideClosedCycle(List<Vector2> boundary, int seedCount, System.Random rng)
        {
            var parcels = new List<List<Vector2>>();

            var min = boundary[0];
            var max = boundary[0];
            foreach (var p in boundary)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            var seeds = new List<Vector2>();
            int attempts = 0;
            while (seeds.Count < seedCount && attempts < seedCount * 50)
            {
                var x = (float)(min.x + rng.NextDouble() * (max.x - min.x));
                var y = (float)(min.y + rng.NextDouble() * (max.y - min.y));
                var point = new Vector2(x, y);

                if (IsPointInPolygon(point, boundary))
                    seeds.Add(point);

                attempts++;
            }

            float gridSize = Mathf.Max((max.x - min.x), (max.y - min.y)) / 50f;
            var cells = new Dictionary<Vector2, List<Vector2>>();
            foreach (var seed in seeds)
                cells[seed] = new List<Vector2>();

            for (float x = min.x; x <= max.x; x += gridSize)
            {
                for (float y = min.y; y <= max.y; y += gridSize)
                {
                    var pt = new Vector2(x, y);
                    if (!IsPointInPolygon(pt, boundary)) continue;

                    Vector2 closestSeed = seeds.OrderBy(s => Vector2.SqrMagnitude(pt - s)).First();
                    cells[closestSeed].Add(pt);
                }
            }

            foreach (var kvp in cells)
            {
                if (kvp.Value.Count < 3) continue;
                var hull = ConvexHull(kvp.Value);
                parcels.Add(hull);
            }

            return parcels;
        }


        public static List<List<Vector2>> SubdivideClosedCycleIrregular(List<Vector2> boundary, int seedCount, System.Random rnd)
        {
            var parcels = new List<List<Vector2>>();

            // 1. Oblicz bounding box
            var min = boundary[0];
            var max = boundary[0];
            foreach (var p in boundary)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            // 2. Znajdź nasiona wewnątrz wielokąta
            var seeds = new List<Vector2>();
            int attempts = 0;
            while (seeds.Count < seedCount && attempts < seedCount * 50)
            {
                var x = (float)(min.x + rnd.NextDouble() * (max.x - min.x));
                var y = (float)(min.y + rnd.NextDouble() * (max.y - min.y));
                var point = new Vector2(x, y);

                if (IsPointInPolygon(point, boundary))
                    seeds.Add(point);

                attempts++;
            }

            // 3. Podziel wnętrze na siatkę i przypisz do najbliższego nasiona
            float gridSize = Mathf.Max((max.x - min.x), (max.y - min.y)) / 50f;
            var cells = new Dictionary<Vector2, List<Vector2>>();
            foreach (var seed in seeds)
                cells[seed] = new List<Vector2>();

            for (float x = min.x; x <= max.x; x += gridSize)
            {
                for (float y = min.y; y <= max.y; y += gridSize)
                {
                    var pt = new Vector2(x, y);
                    if (!IsPointInPolygon(pt, boundary)) continue;

                    Vector2 closestSeed = seeds.OrderBy(s => Vector2.SqrMagnitude(pt - s)).First();
                    cells[closestSeed].Add(pt);
                }
            }

            // 4. Każda działka to convex hull z przypisanych punktów + ewentualne przecięcia z granicą
            foreach (var kvp in cells)
            {
                if (kvp.Value.Count < 3) continue;
                var hull = ConvexHull(kvp.Value);
                parcels.Add(hull);
            }

            return parcels;
        }


        // Graham scan / Gift wrapping (dla 2D punktów)
        private static List<Vector2> ConvexHull(List<Vector2> points)
        {
            points = points.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            if (points.Count < 3) return points;

            var lower = new List<Vector2>();
            foreach (var p in points)
            {
                while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }

            var upper = new List<Vector2>();
            for (int i = points.Count - 1; i >= 0; i--)
            {
                var p = points[i];
                while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        public static List<List<Vector2>> _SubdivideRadially(List<Vector2> cycle, int count)
        {
            if (cycle.Count < 3 || count < 2)
                return new List<List<Vector2>>();

            // Usuń końcowe domknięcie jeśli jest
            if (cycle.First() == cycle.Last())
                cycle = cycle.Take(cycle.Count - 1).ToList();

            // Środek cyklu
            var centroid = Vector2.zero;
            foreach (var pt in cycle)
                centroid += pt;
            centroid /= cycle.Count;

            int step = Mathf.Max(1, cycle.Count / count);
            var result = new List<List<Vector2>>();

            for (int i = 0; i < cycle.Count; i += step)
            {
                var a = cycle[i];
                var b = cycle[(i + step) % cycle.Count];

                result.Add(new List<Vector2> { a, b, centroid, a }); // zamknięty
            }

            return result;
        }


        public static List<Vector2> GenerateRandomClosedCycle(int pointCount, float radius, System.Random rnd)
        {
            var points = new List<Vector2>();

            for (int i = 0; i < pointCount; i++)
            {
                float angle = (float)(rnd.NextDouble() * 2 * Math.PI);
                float r = radius * (0.7f + 0.3f * (float)rnd.NextDouble());
                var x = r * Mathf.Cos(angle);
                var y = r * Mathf.Sin(angle);
                points.Add(new Vector2(x, y));
            }

            var center = Vector2.zero;
            points = points.OrderBy(p => Mathf.Atan2(p.y - center.y, p.x - center.x)).ToList();
            //points.Add(points[0]); // zamknięcie cyklu
            return points;
        }
    }

}
