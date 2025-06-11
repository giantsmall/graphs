using Assets.Game.Scripts.Editors;
using Assets.Game.Scripts.Gen.GraphGenerator;
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

        List<Polygon> parcels = new();
        List<List<Vector2>> cycles = new();
        public void OnDrawGizmos()
        {
            var shift = new Vector2(-25.5f, -25.5f);
            shift = Vector2.zero;
            foreach (var parcel in parcels)
            {
                GizmosDrawer.DrawVectorList(parcel.points, shift, true, 0.15f);
            //    shift += new Vector2(.15f, .15f);
            }

            Gizmos.color = Color.yellow;
            foreach(var cycle in cycles)
                GizmosDrawer.DrawVectorList(cycle, shift, true, 0.05f);
        }
        public void Generate()
        {
            
            RoadGraphGenChaos.ClearLog();
            index = 0;
            parcels.Clear();
            cycles.Clear();
            var seed = 2094867748;
            var rnd = new System.Random(seed);
            Debug.Log(seed);

            var radius = 15f;
            var pointCount = rnd.Next(4, 6);
            var minWidth = 4f;

            var cycle = GenerateRandomClosedCycle(pointCount, radius, rnd);
            for (int i = 0; i < cycle.Count; i++)
            {
                var curr = cycle[i];
                var prev = cycle.Neighbour(i, -1);
                if (curr.DistanceTo(prev) < minWidth)
                {
                    cycle.RemoveAt(i);
                }
            }
            cycle = cycle.RotateAroundCenter(-180);
            cycle = cycle.ReorderPointsByAngleCW();
            var last2 = cycle.TakeLast(2).ToList();
            cycle.RemoveList(last2);
            cycle.InsertRange(0, last2);

            cycles.Add(cycle);
            parcels = MakeLots(rnd, new Polygon(cycle), 2, 3, 2, 3);

            minWidth = 3f;
            var maxWidth = 6f;
            var minDepth = 3f;
            var maxDepth = 5f;

            
            var trapez = new List<Vector2>() { new Vector2(), new Vector2(4 * minWidth, 0), new Vector2(3.5f * minWidth, maxDepth), new Vector2(0, maxDepth) };
            cycles.Add(trapez);
            parcels.AddRange(MakeLots(rnd, new Polygon(trapez), 2, 3, 2, 3));

            var shallowRect = new List<Vector2>() { new Vector2(), new Vector2(4 * minWidth, 0), new Vector2(4 * minWidth, minDepth), new Vector2(0, 3) };
            shallowRect.ShiftBy(new Vector2(20f, 0));
            cycles.Add(shallowRect);
            parcels.AddRange(MakeLots(rnd, new Polygon(shallowRect), 2, 3, 2, 3));

            var deltoid = new List<Vector2>() { new Vector2(0, 0), new Vector2(minWidth, 4 * maxWidth), new Vector2(0, 5 * maxWidth), new Vector2(-minWidth, 4 * maxWidth) };
            deltoid.ShiftBy(new Vector2(40f, 0));
            cycles.Add(deltoid);
            parcels.AddRange(MakeLots(rnd, new Polygon(deltoid), 2, 3, 2, 3));

            SceneView.RepaintAll();
            
            rnd = new System.Random();
            //parcels = MakeLotsBasedOnAngle(rnd, cycle, 4, 5, minWidth, 5);
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
        class LotPts
        {
            public Vector2 LastDeep, FirstDeep, LastShallow, FirstShallow, MeetInTheMiddle;
            public float GetArea()
            {
                var polygon = new Polygon(LastDeep, FirstDeep, LastShallow, FirstShallow, MeetInTheMiddle);
                return polygon.CalculateArea();
            }

            public LotPts(Vector2 LastDeep, Vector2 LastShallow, Vector2 FirstDeep, Vector2 FirstShallow, Vector2 MeetInTheMiddle)
            {                
                this.LastDeep = LastDeep; 
                this.LastShallow = LastShallow;
                this.FirstDeep = FirstDeep;
                this.FirstShallow = FirstShallow;
                this.MeetInTheMiddle = MeetInTheMiddle;
            }
        }
        class EdgeLotPart
        {
            public Vector2 LastDeep, FirstDeep, LastShallow, FirstShallow;
            public EdgeLotPart(Vector2 LastDeep, Vector2 LastShallow, Vector2 FirstDeep, Vector2 FirstShallow)
            {
                this.LastDeep = LastDeep;
                this.LastShallow = LastShallow;
                this.FirstDeep = FirstDeep;
                this.FirstShallow = FirstShallow;                
            }
        }

        public static List<Polygon> MakeLots(System.Random rnd, Polygon block, float minDepth, float maxDepth, float minWidth, float maxWidth, float maxSmallAngle = 110)
        {
            var dists = block.GetEdgeDistancesToCenter();
            var edgesLens = block.GetEdgeLengths();
            var shortestEdge = edgesLens.Min();            
            var longestEdge = edgesLens.Max();
            var longestEdgeIndex = edgesLens.IndexOf(longestEdge);
            var shortestEdgeIndex = edgesLens.IndexOf(shortestEdge);
            var avgLen = edgesLens.Average();
            var blockArea = block.CalculateArea();            
            var minArea = minDepth * minWidth;
            Debug.Log($"Shortest len to average ratio = {shortestEdge / (avgLen / 2f)}");
            Debug.Log($"Shortest len wider than min width: {shortestEdge} vs {minWidth}");
            Debug.Log($"Area: {blockArea}, min = {minArea}");

            var result = new List<Polygon>();
            var shortestLenCondition = shortestEdge > maxWidth && shortestEdge > avgLen / 2f;
            var minAreaConditionMet = blockArea > minArea;

            if(!minAreaConditionMet)
            {
                result.Add(new Polygon(MakeSingleLot(block)));
                return result;
            }

            if (block.points.Count < 3)
            {
                Debug.LogError("Block has less than 3 points");
            }


            if (block.points.Count == 3)
            {
                var triangleNiceAndSmooth = shortestLenCondition;
                var heightOfLongestEdgeBigEnough = block.GetHeightOfEdge(longestEdgeIndex) > minDepth;
                if (triangleNiceAndSmooth)
                {
                    result = MakeLotsBasedOnAngle(rnd, block, minDepth, maxDepth, minWidth, maxWidth, maxSmallAngle);
                }
                else if(heightOfLongestEdgeBigEnough)
                {
                    result.Add(new Polygon(MakeSingleLot(block)));
                }
                else if (!heightOfLongestEdgeBigEnough)
                {
                    result.Add(new Polygon(MakeSingleLot(block)));
                }
                return result;
            }

            if (!shortestLenCondition)
            {
                (Polygon cutLot, Polygon newPolygon) = CutOffShortestEge(block, shortestEdgeIndex);
            }

            //rect
            //no edges too short
            if (block.points.Count == 4)
            {
                var niceRect = shortestLenCondition;
                var deepEnough = dists.All(d => d > minDepth);
                if (niceRect && deepEnough)
                {
                    result = MakeLotsBasedOnAngle(rnd, block, minDepth, maxDepth, minWidth, maxWidth, maxSmallAngle);
                }                
                else if (!deepEnough && longestEdge > 2 * minWidth)
                {
                    result = MakeSingleLotLayerPerpToLongestEdge(rnd, block, minDepth, maxDepth, minWidth, maxWidth);
                }
                else if(deepEnough && longestEdge > 2 * minWidth)
                {
                    result = MakeTwoLotLayerPerpToLongestEdge(rnd, block, minDepth, maxDepth, minWidth, maxWidth);
                }
            }
            
            //pentagon
            if (block.points.Count >= 5)
            {
                var pentagonNiceAndSmooth = shortestLenCondition;
                if (pentagonNiceAndSmooth)
                {
                    result = MakeLotsBasedOnAngle(rnd, block, minDepth, maxDepth, minWidth, maxWidth, maxSmallAngle);
                }
                else
                {
                    Debug.LogWarning("Pentagon not nice enough");
                }
            }
            return result;
        }

        private static (Polygon cutLot, Polygon newPolygon) CutOffShortestEge(Polygon block, int shortestEdgeIndex)
        {
            //make perp line based on angles with neighbours and get offset distance to cut

            var prevP = block.points.Neighbour(shortestEdgeIndex, -1);
            var shortestEdgeP1 = block.points[shortestEdgeIndex];
            var shortestEdgePNext = block.points.Neighbour(shortestEdgeIndex, 1);
            var nextP = block.points.Neighbour(shortestEdgeIndex, 2);

            var prevAng = Vector2.Angle(prevP.pos - shortestEdgeP1.pos, shortestEdgePNext.pos - shortestEdgeP1.pos);
            var nextAng = Vector2.Angle(nextP.pos - shortestEdgePNext.pos, shortestEdgePNext.pos - shortestEdgeP1.pos);


            var cutLot = new Polygon();
            var newPolygon = new Polygon();
            return (cutLot, newPolygon);
        }

        public static List<Polygon> MakeLotsBasedOnAngle(System.Random rnd, Polygon poly, float minDepth, float maxDepth, float minWidth, float maxWidth, float maxSmallAngle = 110)
        {
            return MakeLotsBasedOnAngle(rnd, poly.GetVectors(), minDepth, maxDepth, minWidth, maxWidth);
        }
        public static List<Polygon> MakeLotsBasedOnAngle(System.Random rnd, List<Vector2> poly, float minDepth, float maxDepth, float minWidth, float maxWidth, float maxSmallAngle = 110)
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

            List<LotPts> lotPtsList = new List<LotPts>();

            var maxInnerWidth = maxDepth / 2f;
            var minIneerWidth = minDepth / 2f;

            for (int i = 0; i < angles.Count; i++)
            {
                var rnDep = depths[i];
                var prevP = poly.Neighbour(i, -1);
                var nextP = poly.Neighbour(i, 1);
                var next2P = poly.Neighbour(i, 2);
                Vector2 shallowMeetiPlus1, shallowMeeti, meetInTheMiddle;
                if (angles[i] < maxSmallAngle)
                {
                    var inters1 = GetOffsetAndInters(nextP, rnDep, poly[i], prevP, poly);
                    (meetInTheMiddle, shallowMeeti, shallowMeetiPlus1) = MeetIntersAndGetShallowMeetsForSmallAngle(poly, i, depths[i], depths.Neighbour(i, 1));
                    //lots.Add(new List<Vector2>() { shallowMeeti, poly[i], shallowMeetiPlus1, meetInTheMiddle });
                    lotPtsList.Add(new LotPts(meetInTheMiddle, shallowMeetiPlus1, meetInTheMiddle, shallowMeeti, meetInTheMiddle));
                }
                else
                {
                    var t = rnDep / ((poly[0] - nextP).magnitude);
                    var rotPt = Vector2.Lerp(poly[0], nextP, t);
                    (meetInTheMiddle, shallowMeeti, shallowMeetiPlus1) = MeetIntersAndGetShallowMeetsForBigAngle(poly, i, depths);
                    //var (offset1, offset2) = MeetIntersAndGetOffsetsForBigAngle(poly, i, depths[i], depths.Neighbour(i, 1));
                    //var lot = new List<Vector2>() { shallowMeeti, poly[i], shallowMeetiPlus1, meetInTheMiddle, };                    
                    //lots.Add(lot.ReorderPointsByAngleCCW());
                    lotPtsList.Add(new LotPts(meetInTheMiddle, shallowMeetiPlus1, meetInTheMiddle, shallowMeeti, meetInTheMiddle));
                }
            }

            for (int i = 0; i < angles.Count; i++)
            {
                var lot = new List<Vector2>() { lotPtsList[i].FirstShallow, poly[i], lotPtsList[i].LastShallow, lotPtsList[i].MeetInTheMiddle };                
                lots.Add(lot);
            }

            var edgeLots = new List<List<Vector2>>();
            for (int i = 0; i < angles.Count; i++)
            {
                var edgeLot = new List<Vector2>();
                var prevLot = lotPtsList.Neighbour(i, -1);
                var currLot = lotPtsList[i];

                edgeLot.AddItems(currLot.FirstShallow, currLot.FirstDeep, prevLot.LastDeep, prevLot.LastShallow);
                lots.Add(edgeLot);
                //var lotWidth = (currLot.FirstDeep - prevLot.LastDeep).magnitude;
                //var lotWidths = new List<float>();
                //if (lotWidth > maxWidth)
                //{
                //    lotWidths.AddRange(lotWidth.GetRandomSplit(minWidth, maxWidth, rnd));
                //}
                //else lotWidths.Add(lotWidth);

                //var prevLotLastDeep = prevLot.LastDeep;
                //var prevLotLastShallow = prevLot.LastShallow;
                //var lotIndex = 0;
                //foreach (var lotW in lotWidths)
                //{
                //    var t = lotW / lotWidth;
                //    lotWidth -= lotW;
                //    var newLastShallow = Vector2.Lerp(prevLotLastShallow, currLot.FirstShallow, t);
                //    var newLastDeep = Vector2.Lerp(prevLotLastDeep, currLot.FirstDeep, t);

                //    var lot = new List<Vector2>() { newLastDeep, prevLotLastDeep, prevLotLastShallow, newLastShallow };
                //    edgeLots.Add(lot);
                //    lots.Insert(i + lotIndex++, lot);

                //    prevLotLastDeep = newLastDeep;
                //    prevLotLastShallow = newLastShallow;
                //}                
            }
            return lots.Select(l => new Polygon(l)).ToList();
            //return edgeLots;
        }

        public static List<Polygon> MakeSingleLotLayerPerpToLongestEdge(System.Random rnd, Polygon poly, float minDepth, float maxDepth, float minWidth, float maxWidth)
        {
            //throw new Exception("MakeSingleLotLayerPerpToLongestEdge - Work in progress");
            var longestEdge = poly.GetEdgeLengths().OrderBy(l => l).First();
            var longestEdgeIndex = poly.GetEdgeLength(longestEdge);
            var lots = new List<List<Vector2>>();
            var lotWidths = new List<float>();
            if (longestEdge > maxWidth)
            {
                lotWidths.AddRange(longestEdge.GetRandomSplit(minWidth, maxWidth, rnd));
            }
            else lotWidths.Add(longestEdge);

            var prevLotLastDeep = Vector2.zero; //intersect any edge perp to longestOne at its start? or just take any vertext
            var prevLotLastShallow = poly.points[longestEdgeIndex].pos;
            foreach (var lotW in lotWidths)
            {
                var t = lotW / longestEdge;
                longestEdge -= lotW;
                var newLastShallow = Vector2.Lerp(prevLotLastShallow, poly.points.Neighbour(longestEdgeIndex, 1).pos, t);
                var newLastDeep = Vector2.zero; //intersect any edge perp to longestOne

                var lot = new List<Vector2>() { newLastDeep, prevLotLastDeep, prevLotLastShallow, newLastShallow };
                lots.Add(lot);

                prevLotLastDeep = newLastDeep;
                prevLotLastShallow = newLastShallow;
            }

            return lots.Select(l => new Polygon(l)).ToList();
        }

        public static List<Vector2> MakeSingleLot(Polygon poly)
        {
            Debug.LogWarning($"Not Implemented: {nameof(MakeSingleLot)}");
            return poly.GetVectors();
        }

        public static List<Polygon> MakeTwoLotLayerPerpToLongestEdge(System.Random rnd, Polygon poly, float minDepth, float maxDepth, float minWidth, float maxWidth)
        {
            Debug.LogWarning($"Not Implemented: {nameof(MakeTwoLotLayerPerpToLongestEdge)}");
            var lots = new List<Polygon>();
            return lots;
        }

        public static float PerpendicularDistance(LineSegment line, PtWSgmnts point)
        {
            var inters = VectorIntersect.GetPerpendicularIntersection(line.p0.pos, line.p1.pos, point.pos);
            return inters.DistanceTo(point.pos);
        }

        public static float PerpendicularDistance(Vector2 start, Vector2 end, Vector2 point)
        {
            var inters = VectorIntersect.GetPerpendicularIntersection(start, end, point);
            return inters.DistanceTo(point);
        }


        static int index = 0;
        static Vector2 GetOffsetAndInters(Vector2 nextP, float rnDep, Vector2 currP, Vector2 prevP, List<Vector2> poly)
        {
            var (offset1, offset2) = GetOffsetLine(nextP, currP, rnDep, (nextP - currP).magnitude * 2, poly);
            var intsr = VectorIntersect.GetIntersectionPoint(offset1, offset2, currP, prevP);

            if(intsr.HasValue)
            {
                return intsr.Value;
            }
            else
            {
                
                //Debug.DrawLine(offset1, offset2, Color.red);
                //Debug.DrawLine(nextP, currP, Color.blue);
                return Vector2.zero;
            }
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
            Vector2 shallowPrev = Vector2.zero, shallowNext = Vector2.zero;
            if (inters.HasValue)
            {
                shallowPrev = VectorIntersect.GetPerpendicularIntersection(prevP, currP, inters.Value);
                shallowNext = VectorIntersect.GetPerpendicularIntersection(nextP, currP, inters.Value);
            }
            else
            {
                Debug.DrawLine(offset1.Item1, offset1.Item2, Color.red);
                Debug.DrawLine(offset2.Item1, offset2.Item2, Color.blue);
            }
            return (inters.Value, shallowPrev, shallowNext);
        }

        static (Vector2, Vector2, Vector2) MeetIntersAndGetShallowMeetsForBigAngle(List<Vector2> p, int index, List<float> distances, bool draw = false)
        {            
            var currP = p[index];
            var prevP = p.Neighbour(index, -1);
            var nextP = p.Neighbour(index, 1);

            var nextI = p.IndexOf(nextP);

            var offset1 = GetOffsetLine(prevP, currP, distances[index], (prevP - currP).magnitude * 2f, p);
            var offset2 = GetOffsetLine(nextP, currP, distances[nextI], (nextP - currP).magnitude * 2f, p);
            var inters = VectorIntersect.GetIntersectionPoint(offset1.Item1, offset1.Item2, offset2.Item1, offset2.Item2);
            if(!inters.HasValue)
            {
                var next2P = p.Neighbour(index, 2);
                var next2I = p.IndexOf(next2P);
                offset1 = GetOffsetLine(prevP, currP, distances[index], (prevP - currP).magnitude * 2f, p);
                offset2 = GetOffsetLine(next2P, nextP, distances[next2I], (next2P - nextP).magnitude * 2f, p);
                inters = VectorIntersect.GetIntersectionPoint(offset1.Item1, offset1.Item2, offset2.Item1, offset2.Item2);
                if(!inters.HasValue)
                {
                    Debug.LogError("Intersection has no value again ;/");
                    Debug.DrawLine(offset1.Item1, offset1.Item2, Color.green);
                    Debug.DrawLine(offset2.Item1, offset2.Item2, Color.blue);
                }
            }

            var shallowPrev = VectorIntersect.GetPerpendicularIntersection(prevP, currP, inters.Value);
            var shallowNext = VectorIntersect.GetPerpendicularIntersection(nextP, currP, inters.Value);
            return (inters.Value, shallowPrev, shallowNext);
        }

        static ((Vector2, Vector2), (Vector2, Vector2)) MeetIntersAndGetOffsetsForBigAngle(List<Vector2> p, int vIndex, float distance, float nextDistance, bool draw = false)
        {
            var currP = p[vIndex];
            var prevP = p[(vIndex - 1 + p.Count) % p.Count];
            var nextP = p[(vIndex + 1) % p.Count];
            var offset1 = GetOffsetLine(prevP, currP, distance, 0, p);
            var offset2 = GetOffsetLine(nextP, currP, nextDistance, 0, p);
            
            var inters = VectorIntersect.GetIntersectionPoint(offset1.Item1, offset1.Item2, offset2.Item1, offset2.Item2);
            if(inters.HasValue)
            {
                offset1.Item2 = inters.Value;
                offset2.Item2 = inters.Value;
                return (offset1, offset2);
            }
            else
            {
                Debug.LogError("Intersection has no value ;/");
                DrawPolygon(new List<Vector2> { offset1.Item1, offset1.Item2 }, Color.green);
                DrawPolygon(new List<Vector2> { offset2.Item1, offset2.Item2 }, Color.blue);
                return ((Vector2.zero, Vector2.zero), (Vector2.zero, Vector2.zero));
            }
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
        public static bool IsPointOnSegment(Vector2 p, Vector2 a, Vector2 b, float epsilon = 0.01f)
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
