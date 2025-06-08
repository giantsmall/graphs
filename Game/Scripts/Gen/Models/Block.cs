using Assets.Game.Scripts.Gen.GraphGenerator;
using Assets.Game.Scripts.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Game.Scripts.Gen.Models
{
    public class Block : Polygon
    {
        public bool fullDistrict = true;

        public uint Id { get; protected set; }
        public static uint index { get; private set; }
        public District parentDistrict { get; protected set; }
        public List<Street> streets { get; protected set; } = new();
        public List<Lot> lots { get; protected set; } = new();

        public Block(List<Street> streets, District parentDistrict, params List<PtWSgmnts>[] points) : base(points)
        {
            Id = index++;
            this.parentDistrict = parentDistrict;
            this.streets.AddRange(streets);
            parentDistrict.Blocks.Add(this);
        }

        public PtWSgmnts FindNearestPointOnStreet(LineSegment segment, Vector2 point)
        {
            return segment.points.OrderBy(p => p.DistanceTo(point)).First();
        }

        public Vector2 FindNearestPointOnLine(Vector2 origin, Vector2 direction, Vector2 point)
        {
            direction.Normalize();
            Vector2 lhs = point - origin;
            float dotP = Vector2.Dot(lhs, direction);
            return origin + direction * dotP;
        }

        List<PtWSgmnts> FindEdgePoints(float angle)
        {
            var edgePoints = new List<PtWSgmnts>(); 
            for (int i = 1; i < this.points.Count - 1; i++)
            {
                if (Vector2.Angle(points[i - 1].pos - points[i].pos, points[i + 1].pos - points[i].pos) < 135)
                {
                    edgePoints.Add(points[i]);
                }
            }
            if (Vector2.Angle(points.LastButOne().pos - points.Last().pos, points[0].pos - points.Last().pos) < 135)
            {
                edgePoints.Add(points.Last());
            }
            if (Vector2.Angle(points[1].pos - points.First().pos, points.Last().pos - points.First().pos) < 135)
            {
                edgePoints.Add(points.First());
            }
            return edgePoints; ;
        }

        List<PtWSgmnts> FindIntersections()
        {
            var mutualPoints = new List<PtWSgmnts>();
            for (int i = 0; i < streets.Count; i++)
            {
                for (int j = 0; j < streets.Count; j++)
                {
                    if(i != j)
                    {
                        var pts = streets[i].points.Where(p => streets[j].points.Contains(p));
                        mutualPoints.AddRange(pts);
                    }
                }
            }
            mutualPoints = mutualPoints.Distinct(new PointsComparer(false)).ToList();
            return mutualPoints;
        }

        List<PtWSgmnts> GetPointsCloserToCenter(float t)
        {
            return GetPointsCloserToCenter(this.points, t);
        }

        static List<PtWSgmnts> GetPointsCloserToCenter(List<PtWSgmnts> points, float t)
        {
            var list = new List<PtWSgmnts>();
            var center = new Polygon(points).FindCenter();

            foreach (var pt in points)
            {
                //var distToCenter = Vector2.Distance(center, pt.pos);
                var newPt = Vector2.Lerp(center, pt.pos, t);
                list.Add(new PtWSgmnts(newPt));
            }
            return list;
        }
        
        Street GetStraightRoad()
        {
            var minDev = 999f;
            var index = 0;
            for (int i = 0; i < this.streets.Count; i++)
            {
                var street = this.streets[i];
                var dev = street.points.TakeMiddleOne().DistanceToSegment(street);
                if(dev < minDev)
                {
                    minDev = dev;
                    index = i;
                }
            }
            return streets[index];
        }

        Street GetCurvedRoad()
        {
            var max = 0f;
            var index = 0;
            for (int i = 0; i < this.streets.Count; i++)
            {
                var street = this.streets[i];
                var dev = street.points.TakeMiddleOne().DistanceToSegment(street);
                if (dev > max)
                {
                    max = dev;
                    index = i;
                }
            }
            return streets[index];
        }

        public void DesignateLots(SettlementModel s)
        {
            if(streets.Count < 3)
            {
                Debug.LogWarning("Not enough roads to build lots");
                return;
            }
            if (this.lots.Any())
            {
                return;
            }

            var rnd = RoadGraphGenChaos.GetRandom();
            var center = new PtWSgmnts(this.FindCenter());

            var lotsSizes = this.points.Count.DivideIntoSizes(2, 3, rnd);
            var edgePoints = FindIntersections(); //FindEdgePoints(135);

            //first lot
            var total = 0;
            var perpPts = new List<PtWSgmnts>();
            var ptsCloser = GetPointsCloserToCenter(.5f);
            //foreach (var size in lotsSizes)
            for(int i = 0; i < lotsSizes.Count; i++)
            {
                var size = lotsSizes[i];
                var lot = new Lot(this);
                lot.points.AddRange(this.points.GetRange(total, size + 1));
                lot.AssignStreets(streets);

                //var lastIndex = this.points.IndexOf(lot.points.Last());

                //if (edgePoints.Contains(lot.points.Last()))
                //{
                //    lot.points.AddRange(this.points.GetRange(lastIndex + 1, 1));
                //    lot.points.AddRange(ptsCloser.GetRange(lastIndex + 1, 1).Reversed());
                //}
                //lot.points.Add(ptsCloser[total + size + 1]);
                //lot.points.Add(ptsCloser[total]);
                total += size;

                if (edgePoints.Contains(lot.points.Last()))
                {
                    total++;
                    lotsSizes[lotsSizes.Count - 1]--;
                }
               
                this.lots.Add(lot);
            }

            var freePts = this.points.Count - total;
            if (true || edgePoints.Contains(this.lots[0].points[0]))
            {
                if (freePts > 0)
                {
                    this.lots[0].points.InsertRange(0, this.points.TakeLast(freePts).ToList());
                }
                else
                {
                    this.lots[0].AbsorbPolygon(this.lots.Last());
                }
            }

            var lastPoints = this.lots.Select(l => l.points.Last()).ToList();

            this.lots = lots.Where(l => l.points.Any()).ToList();
            foreach (var lot in this.lots)
            {
                lot.points.Add(center);
            }
            s.Lots.AddRange(lots);
            return;


            foreach (var lot in this.lots)
            {
                break;
                if (lot.streets.Count == 1)
                {
                    var newPt = RotatePtAndPickOneInsidePolygon(lot.points[1], lot.points[0], 90);
                    var newPt2 = RotatePtAndPickOneInsidePolygon(lot.points.LastButOne(), lot.points.Last(), 90);

                    if(newPt != null)
                        lot.points.Insert(0, newPt);

                    if (newPt2 != null)
                        lot.points.Add(newPt2);
                }
                else if (lot.streets.Count == 2)
                {
                    var stStreetsPt = lot.streets[0].points.Count(p => lot.points.Contains(p));
                    var stStreetsPt2 = lot.streets[1].points.Count(p => lot.points.Contains(p));
                    var newPt = RotatePtAndPickOneInsidePolygon(lot.points.LastButOne(), lot.points.Last(), 90);
                    if (newPt != null)
                        lot.points.Add(newPt);
                }
            }
            s.Lots.AddRange(lots);
        }

        PtWSgmnts RotatePtAndPickOneInsidePolygon(PtWSgmnts ptToRotate, PtWSgmnts pivot, float angle)
        {
            var anglePt = ptToRotate.GetRotatedAround(pivot, angle);
            var minusAngle = ptToRotate.GetRotatedAround(pivot, 360-angle);
            PtWSgmnts ptToinclude = null;
            if (this.ContainsPoint(anglePt))
            {
                ptToinclude = anglePt;
            }
            else if (this.ContainsPoint(minusAngle))
            {
                ptToinclude = minusAngle;
            }
            else
            {
                return null;
            }

            return ptToinclude;
        }


        
        void MeetTwoPerpLines(Lot lot, List<PtWSgmnts> existingPerps)
        {
            if(existingPerps.Any())
            {
                lot.points.Add(existingPerps.Last());
            }
            else
            {                                
                var p1 = lot.points[0];                
                var p1Prim = p1.GetRotatedAround(this.points.ItemBefore(p1), 30);
                var p2 = lot.points.Last();
                var p2Prim = p2.GetRotatedAround(this.points.ItemAfter(p2), -30);
                var intersection = VectorIntersect.GetIntersectionPoint(p1Prim, p1, p2Prim, p2);
                PtWSgmnts pt = new PtWSgmnts(intersection.Value);
                lot.points.Add(pt);
                existingPerps.Add(pt);
            }
        }

        void AddPerpLineReturnLastPt(Lot lot, List<PtWSgmnts> existingPerps)
        {            
            var p2 = lot.points.Last();
            var p2Prim = p2.GetRotatedAround(this.points.ItemBefore(p2), 30);

            lot.points.Add(p2Prim);

            if(existingPerps.Any())
            {
                lot.points.Add(existingPerps.Last());
            }
            else
            {
                var p1 = lot.points[0];
                var p1Prim = p1.GetRotatedAround(this.points.ItemBefore(p1), 30);
                lot.points.Add(p1Prim);
                existingPerps.Add(p1);
            }

            existingPerps.Add(p2Prim);
        }

        
        public void DesignateLotsWithinWalls(SettlementModel sModel, int lotSize)
        {
            var rnd = RoadGraphGenChaos.GetRandom();
            var streetPoints = new List<PtWSgmnts>[streets.Count];
            for (int i = 0; i < streets.Count; i++)
            {
                streetPoints[i] = streets[i].points.Where(p => this.points.Contains(p)).Distinct(new PointsComparer(false)).ToList();
            }

            var lots = new List<Lot>();
            var center = new PtWSgmnts(this.FindCenter());
            
            for(int i = 0; i < streetPoints.Length; i++)
            {
                var street = streetPoints[i];
                if(street is null || street.Count == 0)
                {
                    continue;
                }

                var middleIndex = street.GetMiddleIndex();
                if(middleIndex > 0)
                {
                    var angle = 90;
                    var newPos = street[middleIndex].pos.RotateAroundPivot(street[middleIndex - 1].pos, angle);
                    var newPosOpp = street[middleIndex].pos.RotateAroundPivot(street[middleIndex - 1].pos, -angle);
                    if (Vector2.Distance(newPos, center.pos) > Vector2.Distance(newPosOpp, center.pos))
                    {
                        angle *= -1;
                    }

                    var backYard = street.GetParallelList(center.pos, lotSize - 1, angle);
                    var lotsSizes = street.Count.DivideIntoSizes(lotSize, lotSize, rnd);
                    lotsSizes.RemoveAt(0);
                    if(lotsSizes.Any())
                        lotsSizes.Remove(lotsSizes.Last());
                    else
                    {

                    }
                    var total = 3;
                    foreach (var size in lotsSizes)
                    {
                        var lot = new Lot(this);
                        lot.points.AddRange(street.GetRange(total, size + 1));
                        //lot.points.AddRange(backYard.GetRange(total, size + 1).Reversed());
                        lot.points.Add(center);

                        //var circ = lot.GetCircumference();
                        //if (circ > 25f)
                        //{

                        //}
                        streets[i].Lots.Add(lot);
                        lot.streets.Add(streets[i]);
                        total += size;
                        this.lots.Add(lot);
                    }
                }
            }

            sModel.Lots.AddRange(this.lots);
        }

        public (List<Block>, List<Street>) DivideIntoBlocks(District parent, float MaxBlockSize)
        {
            var center = this.FindCenter();
            var (closestPoint, dic) = GetPointClosestToCenterAndDistDic(center);
           
            var centerP = new PtWSgmnts(center);
            var pCount = points.Count;
            var oppIndex = points.IndexOf(closestPoint).WrapIndex(pCount / 2, points);
            var range = new List<PtWSgmnts>() { points[oppIndex.WrapIndex(-1, points)], points[oppIndex], points[oppIndex.WrapIndex(1, points)] };
            
            var closestOpp = range.OrderByDescending(p => points.IndexDiff(centerP, p)).First();
            if (points.IndexDiff(closestPoint, closestOpp) <= 3)
            {
                return (new List<Block>() { this }, new List<Street>());
            }

            var (blockPoints1, blockPoints2) = DividePolygonPoints(closestPoint, closestOpp);
            var street = new Street();
            street.AddCheckPoints(closestPoint);
            var addCenterP = closestPoint.DistanceTo(closestOpp) > 2f;
            if (addCenterP)
            {
                street.AddCheckPoints(centerP);
            }
            street.AddCheckPoints(closestOpp);
            street.CalculateLength();
            street.Split(RoadGraphGenChaos.GetRandom(), 1f);
            blockPoints1.AddRange(street.points.Reversed());
            blockPoints2.AddRange(street.points.Reversed());
            this.streets.Add(street);

            var newBlock = new Block(this.streets, parent, blockPoints1);
            newBlock.fullDistrict = false;
            var newBlock2 = new Block(this.streets, parent, blockPoints2);
            newBlock2.fullDistrict = false;

            var totalBlocks = new List<Block>() { newBlock, newBlock2 };
            var totalStreets = new List<Street>() { street };

            if(newBlock.CalculateArea() > MaxBlockSize)
            {                
                var (newBlocks, newStreets) = newBlock.DivideIntoBlocks(parent, MaxBlockSize);
                totalBlocks.Remove(newBlock);
                totalBlocks.AddRange(newBlocks);
                totalStreets.AddRange(newStreets);
            }
            if (newBlock2.CalculateArea() > MaxBlockSize)
            {
                var (newBlocks, newStreets) = newBlock2.DivideIntoBlocks(parent, MaxBlockSize);
                totalBlocks.Remove(newBlock2);
                totalBlocks.AddRange(newBlocks);
                totalStreets.AddRange(newStreets);
            }
            return (totalBlocks, totalStreets);
        }

        internal PtWSgmnts GetClosestPoint(PtWSgmnts pt) => this.points.OrderBy(p => p.DistanceTo(pt)).First();
    }
}
