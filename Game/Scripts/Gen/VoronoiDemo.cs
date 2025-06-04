using UnityEngine;
using System.Collections.Generic;
using Delaunay;
using Delaunay.Geo;
using UnityEngine.UI;
using Unity.VisualScripting;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Assets.Game.Scripts.Utility;
using UnityEditor.Analytics;
using Assets.Game.Scripts.Gen.Models;

public class VoronoiDemo : MonoBehaviour
{
    public static float DistortionDecay = 2f;
    public float avgDensity = 0.5f;
    List<Vector2> pathDirs = new();

    [SerializeField]
    public Material mat;

    public List<PtWSgmnts> points;
    //seamless
    public List<LineSegment> Seams = new();
    //need to update main roads and minorroads to include map edges -> MAKE BACKUP FIRST

    public List<LineSegment> edges = new();
    public List<LineSegment> spanningTree = new();
    public List<LineSegment> roads = new();
    public List<LineSegment> delaunayTriangulation = new();
    public List<GameObject> roadsObjects = new();
    public List<LineSegment> removedTriangulation = new();
    public bool DrawBoundaries = true;
    public bool DrawTriangles = true;
    public bool DrawRemovedTriangles = true;
    public bool DrawSpanningTree = true;
    public bool DrawEdges = true;
    public string Seed = "0";
    public bool DrawVillageGizmos { get; set; } = true;
    public string VillagesSeed = "0";

    public bool RandomizeSeed = false;
    public bool RandomizeVillagesSeed = false;

    public int mapSize = 100;
    public float minCitiesDistance = 3f;
    public float maxRoadSegmentLength = 3f;

    public float MinPathTriangleAngle = 70f;
    public float MinorPathMinAngle = 45f;

    public float MaxIntersecDist = 0.1f;
    public bool ExperimentalDistort = false;
    System.Random rnd;
    void Awake()
    {
        RunDiagram();
    }

    void Update()
    {
        if (Input.anyKeyDown)
        {
            //ToFIXLATER:
            //Seed 98548955 -> path neighbouring to main road
            //Seed 313880252 -> right side contains minor paths curving in strange way
            //SEED black -> left side contains weird minor paths overlapping
            RunDiagram();
        }
    }

    public void RunDiagram()
    {
        if (RandomizeSeed)
            Seed = new System.Random().Next().ToString();
        rnd = new System.Random(Seed.GetHashCode());

        var rect = new Rect(0, 0, mapSize, mapSize);
        var m_points = GenerateRandomPoints(rnd, rect, avgDensity);
        if (m_points.Count > 100)
        {
            Debug.LogError($"Too many points {m_points.Count}");
            return;
        }
        Voronoi v = new Voronoi(m_points, rect);

        edges = v.VoronoiDiagram();
        spanningTree = v.SpanningTree();
        points = spanningTree.SelectMany(s => s.EdgePoints).ToList();
        points = MergePoints(points, spanningTree);

        delaunayTriangulation = v.DelaunayTriangulation();
        delaunayTriangulation = RemoveExistingSegments(delaunayTriangulation, spanningTree);
        points = MergePoints(points, delaunayTriangulation);

        //remove too long edges
        delaunayTriangulation = RemoveTooLongEdges(delaunayTriangulation);

        //assign triangulation to points
        removedTriangulation = AssignTriangulationEdgesToPointsRelations(delaunayTriangulation, points);
        delaunayTriangulation = delaunayTriangulation.Where(s => !removedTriangulation.Contains(s)).ToList();

        //assign remaining triangulation as secondary paths
        AssignRemainingTriangulationAsMainPaths(delaunayTriangulation);
        UseRemovedTrianglesForMajorPaths(removedTriangulation);

        //from edges having angle smaller than 15deg remove longer one
        spanningTree.ForEach((LineSegment line) => { line.ExtendIfBelow(minCitiesDistance); });
        spanningTree.ForEach((LineSegment line) => 
        { 
            //line.SplitAndDistortOnWorldMap(rnd, maxRoadSegmentLength);
            //line.Split(rnd, maxRoadSegmentLength);
            //line.Distort(rnd, maxRoadSegmentLength);
        });
        int a = 5;
        spanningTree.ForEach((LineSegment line) =>
        {
            if(ExperimentalDistort)
            {
                line.Split(rnd, maxRoadSegmentLength);
                line.Distort(rnd, maxRoadSegmentLength);
            }
            else
            {
                line.SplitAndDistortOnWorldMap(rnd, maxRoadSegmentLength);
            }
        });

        var totalMainPaths = points.SelectMany(p => p.majorPaths).Distinct(new SegmentComparer()).ToList();
        var totalMinorPaths = points.SelectMany(p => p.minorPaths).Distinct(new SegmentComparer()).ToList();
        //var villagesRects = GenerateRandomVillagesUsingVoronoi(points);

        GenerateVillagesOnSphere(points);
        GenerantePoisUsingEdges(edges, rnd);
        //Generate free/hidden/outcast villages
    }

    void GenerateVillagesOnSphere(List<PtWSgmnts> cities)
    {
        var minDist = minCitiesDistance / 4f;
        var maxDist = minCitiesDistance / 1.8f;

        foreach (var city in cities)
        {
            var villagesCount = city.majorPaths.Count + rnd.Next(city.majorPaths.Count + 3);

            var itAngle = 360f / villagesCount;
            var angles = new List<float>() { itAngle };
            var distances = new List<float>();

            for (int i = 0; i < villagesCount; i++)
            {
                angles.Add(angles.Last() + 360f / villagesCount * i + rnd.NextFloat(itAngle) - itAngle /2f);
                distances.Add(rnd.NextFloat(minDist, maxDist));                
                var pos = RotatePointAroundPivot(city.pos + new Vector2(distances.Last(), 0), city.pos, angles.Last());

                //distance to major road
                var village = new PtWSgmnts(pos);
                city.neighbourVillages.Add(village);
                village.neighbourCities.Add(city);
            }
        }

        //villages around roads
        foreach (var majorPath in spanningTree)
        {
            break;
            if (majorPath.CptsNoEdges.Any())
            {
                maxDist = majorPath.Length / 2f;
                var maxDistToRoad = minCitiesDistance;
                var minDistToRoad = 1f;
                var minDistToNextVillage = 1f;
                var cpts = majorPath.CptsNoEdges.TakeFirstHalf();

                for (int i = 0; i < cpts.Count - 2; i++)
                {
                    var checkPointLen = Vector2.Distance(cpts[i].pos, cpts[i + 1].pos);
                    var villagesCount = Mathf.Max(1, checkPointLen / minDistToNextVillage - 1);

                    for (int j = 0; j < villagesCount; j++)
                    {
                        var point = Vector2.Lerp(cpts[i].pos, cpts[i + 1].pos, j / (float)villagesCount);
                        var distToRoad = rnd.NextFloat(minDistToRoad, minDistToRoad * 3);

                        var villagePos = RotatePointAroundPivot(point + new Vector2(distToRoad, 0), point, 90f);
                        var village = new PtWSgmnts(villagePos);
                        village.AddNeighbourCity(majorPath.EdgePoints[0]);
                        majorPath.EdgePoints[0].neighbourVillages.Add(village);
                    }
                }
            }
        }

        //villages in the wild
        return;
        minDist = maxDist;
        foreach (var city in cities)
        {
            maxDist = city.neighbourCities.Min(c => c.DistanceTo(city)) / 2f;
            var villagesCount = 2 * (maxDist - minDist);

            var itAngle = 360f / villagesCount;
            var angles = new List<float>() { itAngle };
            var distances = new List<float>();

            for (int i = 0; i < villagesCount; i++)
            {
                angles.Add(angles.Last() + 360f / villagesCount * i + rnd.NextFloat(itAngle) - itAngle / 2f);
                distances.Add(rnd.NextFloat(minDist, maxDist));
                var pos = RotatePointAroundPivot(city.pos + new Vector2(distances.Last(), 0), city.pos, angles.Last());

                //distance to major road
                var village = new PtWSgmnts(pos);
                city.neighbourVillages.Add(village);
                village.neighbourCities.Add(city);
            }
        }
    }

    public static List<Vector2> GenerateRandomPoints(System.Random rnd, Rect bounds, float density)
    {
        List<Vector2> points = new List<Vector2>();
        var count = bounds.width * bounds.height * density / 100f;
        for (uint i = 0; i < count; i++)
        {
            points.Add(new Vector2(rnd.NextFloat(bounds.x, bounds.width), rnd.NextFloat(bounds.y, bounds.height)));
        }   
        return points;
    }

    List<LineSegment> villageSpanningTree = new();
    List<LineSegment> villagesEdges = new();
    List<LineSegment> villagesTriangles = new();

    public List<Rect> GenerateRandomVillagesUsingVoronoi(List<PtWSgmnts> cities)
    {
        intersections = new();
        villagesTriangles = new();
        villageSpanningTree = new();
        villagesEdges = new();
        var boundsList = new List<Rect>();
        var range = minCitiesDistance / 2f;
        if (RandomizeVillagesSeed)
            VillagesSeed = new System.Random().Next().ToString();
        System.Random rnd = new System.Random(VillagesSeed.GetHashCode());
        
        foreach (var city in cities)
        {
            var bounds = new Rect(0, 0, range * 2, range * 2); //
            List<Vector2> points = GenerateRandomPoints(rnd, bounds, 10 + 5 * city.majorPaths.Count);
            Voronoi v = new Voronoi(points, bounds);
            
            var vSpanningTree = v.SpanningTree();
            var villagesFromSpanningTree = vSpanningTree.SelectMany(s => s.EdgePoints).ToList();
            var triangulation = v.DelaunayTriangulation();
            var vVoronoi = v.VoronoiDiagram();
            villagesFromSpanningTree = MergePoints(villagesFromSpanningTree, triangulation);
            villagesFromSpanningTree = MergePoints(villagesFromSpanningTree, vSpanningTree);
            villagesFromSpanningTree = MergePoints(villagesFromSpanningTree, vVoronoi);
            
            foreach (var village in villagesFromSpanningTree)
            {
                village.pos += new Vector2(city.pos.x - range, city.pos.y - range);
            }           
            villageSpanningTree.AddRange(vSpanningTree);

            foreach (var edge in vVoronoi)
            {
                edge.p0.pos += new Vector2(city.pos.x - range, city.pos.y - range);
                edge.p1.pos += new Vector2(city.pos.x - range, city.pos.y - range);
            }
            villagesEdges.AddRange(vVoronoi);
            villagesTriangles.AddRange(triangulation);

            city.neighbourVillages.AddRange(villagesFromSpanningTree);

            foreach(var village in villagesFromSpanningTree)
            {
                village.AddMajorPaths(spanningTree.Where(s => s.ContainsEdgePointPos(village)).ToArray());
            }

            //move villages away from main paths
            foreach(var village in villagesFromSpanningTree)
            {
                var majorPathTooClose = city.majorPaths.FirstOrDefault(p => village.DistanceToSegment(p) < 0.5f);
                if (majorPathTooClose != null)
                {
                    //change distance here
                }

                //if village is closer to main path add new path
                var closestpath = city.majorPaths.OrderBy(p => village.DistanceToSegment(p)).First();
                if (village.DistanceToSegment(closestpath) < village.majorPaths.OrderBy(p => p.Length).First().Length)
                {
                    //add path
                    //get intersection point
                    //move it towards the city
                    //add checkpoint, make connection, distort
                }                                
            }

            //deal with undesired intersections
            foreach (var st in vSpanningTree)
            {
                st.SplitAndDistortOnWorldMap(rnd, 0.2f);
            }

            
            foreach (var path in vSpanningTree)
            {
                (var cityMajorPath, var intersPath, var villageSubsegment) = city.GetSegmentsIntersecting(path);
                var angle = Vector2.Angle(path.p1.pos - path.p0.pos, path.p0.pos - city.pos);
                //if intersection distance is bigger than major road distance                
               
                if (intersPath != null && angle > 45f)
                {
                    var villages = path.EdgePoints;

                    var village = villagesFromSpanningTree[0];
                    var intersectionPoint = intersPath.GetIntersectionPoint(villageSubsegment);
                    var distanceToMainRoadBigEnough = Vector2.Distance(intersectionPoint.Value, village.pos) < village.DistanceToSegment(cityMajorPath);
                    if(distanceToMainRoadBigEnough)
                    {
                        var isMajorRoadCloser = Vector2.Distance(intersectionPoint.Value, village.pos) > village.DistanceToSegment(cityMajorPath);
                        var checkPoint = cityMajorPath.IntersectCheckpoint(intersPath, intersectionPoint.Value, true);
                        path.IntersectCheckpoint(villageSubsegment, checkPoint);
                        intersections.Add(checkPoint);
                    }
                    else
                    {
                        var closestRoadPointToVillage = 0;
                        //village, citymajorPath
                    }

                    village = villagesFromSpanningTree[1];
                    intersectionPoint = intersPath.GetIntersectionPoint(villageSubsegment);
                    distanceToMainRoadBigEnough = Vector2.Distance(intersectionPoint.Value, village.pos) < village.DistanceToSegment(cityMajorPath);
                    if (distanceToMainRoadBigEnough)
                    {
                        var isMajorRoadCloser = Vector2.Distance(intersectionPoint.Value, village.pos) > village.DistanceToSegment(cityMajorPath);
                        var checkPoint = cityMajorPath.IntersectCheckpoint(intersPath, intersectionPoint.Value, true);
                        path.IntersectCheckpoint(villageSubsegment, checkPoint);
                        intersections.Add(checkPoint);
                    }
                    else
                    {
                        var closestRoadPointToVillage = 0;
                        
                        //village, citymajorPath
                    }
                }
            }


            foreach (var village in villagesFromSpanningTree)
            {
                var shortestPath = village.majorPaths.OrderBy(p => p.Length).First();

                //shortest path to major road

                if (village.DistanceTo(city) < shortestPath.Length  && !shortestPath.HasConnectionWithCity)
                {
                    var newLineSegment = new LineSegment(village, city, false);
                    village.MakeMajor(newLineSegment);
                    newLineSegment.SplitAndDistortOnWorldMap(rnd, 0.2f);
                }
            }



            //if village road is very close to city
            foreach (var village in villagesFromSpanningTree)
            {
                break;
                var shortestPath = village.majorPaths.OrderBy(p => p.Length).First();// OrDefault(p => p.TheOtherPoint(village).DistanceTo(city) > village.DistanceTo(city));                                
                var villageToCityDist = village.DistanceTo(city);
                if (villageToCityDist < shortestPath.Length || shortestPath.TheOtherPoint(village).DistanceTo(city) > villageToCityDist)
                {
                    //split shortest path and make both leading to city
                    var angle = Vector2.Angle(shortestPath.p1.pos - shortestPath.p0.pos, village.pos - city.pos);
                    if(angle < Mathf.Abs(30f) && !shortestPath.HasConnectionWithCity)
                    {
                        var closestCheckPoint = shortestPath.points.OrderBy(p => p.DistanceTo(city)).First();
                        var indexOfShortest = shortestPath.points.IndexOf(closestCheckPoint);
                        if (indexOfShortest == 0 || indexOfShortest == shortestPath.points.Count - 1)
                        {
                            Debug.LogWarning("Closest point is edge point");
                            continue;
                        }
                        shortestPath.points[indexOfShortest] = city;
                        var segmentsToCity = shortestPath.points.TakeRangeBetween(city, village);
                        var newPath = new LineSegment(segmentsToCity, false);
                        village.MakeMajor(newPath);
                        newPath.HasConnectionWithCity = true;
                        var theOtherVillage = shortestPath.TheOtherPoint(village);                        
                        var segmentsToTheOtherVillage = shortestPath.points.TakeRangeBetween(theOtherVillage, city);
                        var remainingPath = new LineSegment(segmentsToTheOtherVillage, false);
                        
                        theOtherVillage.MakeMajor(remainingPath);
                        remainingPath.HasConnectionWithCity = true;
                        village.majorPaths.Remove(shortestPath);
                        theOtherVillage.majorPaths.Remove(shortestPath);
                    }
                    
                    //depending on angle distort existing
                    //or add new one
                }
                if (village.majorPaths.Count == 1 && village.majorPaths[0].Length > village.DistanceTo(city))
                {
                    if (!village.majorPaths.Any(v => v.HasConnectionWithCity))
                    {
                        var newPath = new LineSegment(village, city, false);
                        newPath.SplitAndDistortOnWorldMap(rnd, 0.2f);
                    }
                    else
                    {

                    }
                }
            }

            boundsList.Add(bounds);
        }
        return boundsList;
    }

    List<PtWSgmnts> intersections = new();

    public void GenerantePoisUsingEdges(List<LineSegment> edgePoints, System.Random rnd)
    {
        //occurs during game
        //take random point
        //check boundaries -> distance to closest neighbour

                
        //connection between cities and edges occurs during the game
        //when building intelligence, map awareness
        var randomPoint = edgePoints.GetRandom(rnd).EdgePoints.GetRandom(rnd);
    }

    public void GenerateRandomPois(List<LineSegment> mainPaths, List<LineSegment> minorPaths)
    {
        if (RandomizeVillagesSeed)
            VillagesSeed = new System.Random().Next().ToString();
        System.Random rnd = new System.Random(VillagesSeed.GetHashCode());

        var totalVillgaes = new List<PtWSgmnts>();
        var angleSum = 0f;
        var distSum = 0f;
        var minDist = minCitiesDistance / 2f;
        foreach (var mainPath in mainPaths)
        {
            var maxDist = Mathf.Min(mainPath.Length / 2f, minCitiesDistance);
            var villageCount = mainPath.points.Count + 1;
            var pathVillages = new List<PtWSgmnts>();
            for (int i = 0; i < villageCount; i++)
            {
                var currPoint = mainPath.points.GetRandom(rnd);
                var dist = rnd.NextFloat() * minDist * 2 + minDist;
                if(mainPath.EdgePoints.Contains(currPoint))
                {
                   dist += minDist;
                }

                var angle = rnd.NextFloat() * 360;
                var location = currPoint.pos + new Vector2(dist, 0);
                angleSum += angle;
                distSum += dist;
                var rotatedLoc = RotatePointAroundPivot(location, currPoint.pos, angle);
                var village = new PtWSgmnts(rotatedLoc);

                pathVillages.Add(village);
                if (Vector2.Distance(village.pos, mainPath.p0.pos) < Vector2.Distance(village.pos, mainPath.p1.pos))
                {
                    mainPath.p0.neighbourVillages.Add(village);
                }
                else
                {
                    mainPath.p1.neighbourVillages.Add(village);
                }
            }

            for (int i = 0; i < pathVillages.Count; i++)
            {
                var closestCheckPoint = mainPath.points.OrderBy(p => Vector2.Distance(p.pos, pathVillages[i].pos)).First();
                var closestVillage = pathVillages.OrderBy(p => Vector2.Distance(p.pos, pathVillages[i].pos)).First(v => v.Id != pathVillages[i].Id);

                LineSegment newPath = null;
                if (closestVillage.DistanceTo(pathVillages[i]) < closestCheckPoint.DistanceTo(pathVillages[i]) && !pathVillages[i].ContainsMinorPathTo(closestVillage))
                {
                    newPath = new LineSegment(pathVillages[i], closestVillage, false);
                }
                else
                {
                    newPath = new LineSegment(pathVillages[i], closestCheckPoint, false);
                }
                //newPath.SplitAndDistort(rnd, maxRoadSegmentLength / 10f);
            }
            totalVillgaes.AddRange(pathVillages);


            foreach(var point in points)
            {
                for(int i = 0; i < point.neighbourVillages.Count - 1; i++)
                {
                    var currV = point.neighbourVillages[i];
                    var nextV = point.neighbourVillages[i + 1];
                    if (currV.DistanceTo(nextV) < minCitiesDistance && !currV.IsConnectedTo(nextV))
                    {
                        var newPat = new LineSegment(currV, nextV, false);
                        //newPat.SplitAndDistort(rnd, maxRoadSegmentLength / 10f);
                    }
                }
            }

            //distort minor paths to reduce crossroads on checkpoints only
        }
    }

    public Vector2 RotatePointByAngle(Vector2 origin, float radius, float angleRad)
    {
        float x = Mathf.Abs(origin.x) + radius * Mathf.Cos(angleRad);
        float y = Mathf.Abs(origin.y) + radius * Mathf.Sin(angleRad);
        return new Vector2(x, y);
    }

    void UseRemovedTrianglesForMajorPaths(List<LineSegment> segments)
    {
        //add minor path to segments having at both points with single main road
        foreach (var s in segments)
        {
            if (s.p0.majorPaths.Count == 1 && s.p1.majorPaths.Count == 1 && AngleWithMainPathsAbove(s, MinorPathMinAngle))
            {
                s.p0.AddMajorPaths(s);
                s.p1.AddMajorPaths(s);
                //s.SplitAndDistortOnWorldMap(rnd, maxRoadSegmentLength / 2f);
            }
        }

        //add minor path to segments having at least one point with single main road
        foreach (var s in segments)
        {
            var ptWith1MainRoad = s.EdgePoints.FirstOrDefault(p => p.majorPaths.Count == 1);
            if (ptWith1MainRoad != null)
            {
                var pWithMoreThan1MainRoad = s.TheOtherPoint(ptWith1MainRoad);
                if (!pWithMoreThan1MainRoad.minorPaths.Any() && ptWith1MainRoad.minorPaths.Count < 3 && AngleWithMainPathsAbove(s, MinorPathMinAngle))
                {
                    s.p0.AddMajorPaths(s);
                    s.p1.AddMajorPaths(s);
                    //s.SplitAndDistortOnWorldMap(rnd, maxRoadSegmentLength / 2f);
                }
            }
        }

        //from minor paths having angle < 30 remove longer one
        var totalRemovedPaths = 0;
        foreach(var p in points)
        {
            var removedPaths = RemoveLongerRelationsBelowAngle(p.minorPaths, p, MinorPathMinAngle).Distinct(new SegmentComparer()).ToList();
            totalRemovedPaths += removedPaths.Count();
            foreach (var removedPath in removedPaths)
            {
                removedPath.p0.minorPaths = removedPath.p0.minorPaths.Where(mp => !removedPaths.Contains(mp)).ToList();
                removedPath.p1.minorPaths = removedPath.p1.minorPaths.Where(mp => !removedPaths.Contains(mp)).ToList();
            }
        }

        foreach (var p in points)
        {
            foreach(var path in p.minorPaths)
            {
                path.SplitAndDistortOnWorldMap(rnd, maxRoadSegmentLength);
            }
        }
    }

    bool AngleWithMainPathsAbove(LineSegment minorCadidate, float minAngle)
    {
        return !minorCadidate.p0.majorPaths.Any(main => main.AngleBetweenLine(minorCadidate) < minAngle)
            && !minorCadidate.p1.majorPaths.Any(main => main.AngleBetweenLine(minorCadidate) < minAngle);
    }

    void AssignRemainingTriangulationAsMainPaths(List<LineSegment> segments)
    {
        foreach(var s in segments)
        {
            s.p0.majorPaths.Add(s);
            s.p1.majorPaths.Add(s);
            s.SplitAndDistortOnWorldMap(rnd,maxRoadSegmentLength);
        }
    }

    List<LineSegment> AssignTriangulationEdgesToPointsRelations(List<LineSegment> triangulation, List<PtWSgmnts> points)
    {
        var removedSegments = new List<LineSegment>();
        foreach (var point in points)
        {
            removedSegments.AddRange(RemoveLongerRelationsBelowAngle(triangulation, point, MinPathTriangleAngle));
        }

        foreach (var point in points)
        {
            removedSegments.AddRange(RemoveLongerRelationsBelowAngleWithPaths(triangulation, point, MinPathTriangleAngle));
        }

        foreach (var point in points)
        {
            point.AddRelations(triangulation.Where(t => t.ContainsEdgePointPos(point)).ToArray());
        }
        return removedSegments;
    }

    List<LineSegment> RemoveLongerRelationsBelowAngleWithPaths(List<LineSegment> segments, PtWSgmnts mutualOrigin, float minAngle)
    {
        List<LineSegment> removedSegments = new List<LineSegment>();
        var segmentsOfMutualOrigin = segments.Where(s => s.ContainsEdgePointPos(mutualOrigin)).ToList();
        for (int i = 0; i < mutualOrigin.majorPaths.Count; i++)
        {
            for (int j = 0; j < segmentsOfMutualOrigin.Count; j++)
            {
                var s1 = mutualOrigin.majorPaths[i];
                var s2 = segmentsOfMutualOrigin[j];
                var angle1 = s1.AngleBetweenLine(s2);
                if (angle1 < minAngle)
                {
                    removedSegments.Add(s2);
                }
            }
        }
        return removedSegments;
    }

    List<LineSegment> RemoveLongerRelationsBelowAngle(List<LineSegment> segments, PtWSgmnts mutualOrigin, float minAngle)
    {
        List<LineSegment> removedSegments = new List<LineSegment>();
        var segmentsOfMutualOrigin = segments.Where(s => s.ContainsEdgePointPos(mutualOrigin)).ToList();
        for(int i = 0; i < segmentsOfMutualOrigin.Count; i++)
        {
            for(int j = 0; j < segmentsOfMutualOrigin.Count; j++)
            {
                if(i != j)
                {
                    var s1 = segmentsOfMutualOrigin[i];
                    var s2 = segmentsOfMutualOrigin[j];
                    var angle1 = s1.AngleBetweenLine(s2); 
                    if (angle1 < minAngle)
                    {
                        var distToI = s1.Length;
                        var distToIPlus1 = s2.Length;

                        var segmentToRemove = distToI > distToIPlus1 ? s1 : s2;
                        if (!removedSegments.Contains(segmentToRemove))
                            removedSegments.Add(segmentToRemove);
                    }
                }
            }
        }
        return removedSegments;
    }

    List<LineSegment> RemoveTooLongEdges(List<LineSegment> list)
    {
        var maxPath = spanningTree.Max(s => s.Length);
        list = list.OrderBy(t => t.Length).ToList();
        list = list.Where(dt => dt.Length < 2 * maxPath).ToList();
        return list;
    }

    public List<LineSegment> RemoveExistingSegments(List<LineSegment> removeFrom, List<LineSegment> itemsToRemove)
    {
        removeFrom = removeFrom.Where(i => !ItemFound(itemsToRemove, i)).ToList();
        return removeFrom;
    }
    public bool ItemFound(List<LineSegment> list, LineSegment item)
    {
        return list.Any(s => SegmentComparer.SameCoords(s, item));
    }

    public PtWSgmnts MergePoint(PtWSgmnts pt, params PtWSgmnts[] pts)
    {
        pt.AddMajorPaths(pts.SelectMany(d => d.majorPaths).ToArray());
        for (int j = 0; j < pts.Length; j++)
        {
            pts[j].majorPaths.Clear();
        }
        return pt;
    }
    public List<PtWSgmnts> MergePoints(List<PtWSgmnts> pts, List<LineSegment> ss)
    {
        for (int i = 0; i < pts.Count; i++)
        {
            var duplicates = pts.Where(p => PointsComparer.SameCoords(pts[i], p) && PointsComparer.DifferentId(pts[i], p)).ToList();
            if(duplicates.Any())
            {                
                pts[i] = MergePoint(pts[i], duplicates.ToArray());
            }
            var allSegments = ss.Where(s => s.ContainsEdgePointPos(pts[i])).ToList();
            foreach (var seg in allSegments)
            {
                if(PointsComparer.SameCoords(seg.p0, pts[i]))
                {
                    seg.p0 = pts[i];
                }
                if (PointsComparer.SameCoords(seg.p1, pts[i]))
                {
                    seg.p1 = pts[i];
                }
            }
        }
        var points =  pts.Where(p => p.majorPaths.Count > 0).ToList();
        return points;
    }
    
    class Triangle
    {
        List<LineSegment> segments = new List<LineSegment>();
        public Triangle(params LineSegment[] s)
        {
            segments.AddRange(s);
        }
    }
    public void DrawPoint(Vector2 Pos, string name, float size = 2f)
	{
        var go = CreateCubeObjectWithMesh();
        go.name = name;
        var rectT = go.AddComponent<RectTransform>();
        rectT.localScale = new Vector3(size, size, size);
        rectT.position = new Vector3(Pos.x, Pos.y);
        roadsObjects.Add(go);
    }

    #region DrawTexture
    public void DrawRoads(List<PtWSgmnts> points)
	{		
        roads.Clear();
        Vector2 centerPoint = new Vector2(mapSize / 2, mapSize / 2);
        var roadCenter = points.OrderBy(p => Vector2.Distance(p.pos, centerPoint)).First();

        var color = Color.black;
        //from path directions go to towards center

        DrawPoint(roadCenter.pos, "Road");
		pathDirs.Clear();

		var pathCount = rnd.Next(4) + 2;

		var randomShift = rnd.Next(360);
        for (int i = 0; i < pathCount; i++)
		{
			var radians = (i * 360f / pathCount + randomShift + rnd.Next(10) - 20) % 360f * Mathf.Deg2Rad;
            var x = Mathf.Cos(radians) * mapSize * 2;
            var y = Mathf.Sin(radians) * mapSize * 2;
            pathDirs.Add(new Vector2(x, y));
            DrawPoint(pathDirs[i], $"dir{i}");
        }

        var prevDist = 999f;
		var prevPrevDist = 9999f;
		foreach(var pathDir in pathDirs)
		{			
            var point = roadCenter;
            LineSegment nextSegment = FindSegmentTowards(point, pathDir);
			roads.Add(nextSegment);
			var distance = 999f;
            do
			{
                prevPrevDist = prevDist;
                prevDist = distance;

                point = nextSegment.TheOtherPoint(point);
				nextSegment = FindSegmentTowards(point, pathDir);
                distance = Vector2.Distance(point.pos, pathDir);
				
                if (!roads.Contains(nextSegment))
					roads.Add(nextSegment);
            }
			while(distance > 5f && prevPrevDist != distance);
			Debug.Log($"Min distance to dest: {distance}");
        }

        var centerSize = rnd.Next(4) + 3;
        var closestPoints = points.OrderBy(p => Vector2.Distance(p.pos, roadCenter.pos)).Take(centerSize).ToList();
        var closestPointsAvgDist = closestPoints.Average(p => Vector2.Distance(p.pos, roadCenter.pos));
        var closestPointsMax = closestPoints.Max(p => Vector2.Distance(p.pos, roadCenter.pos));

        foreach (var closestPoint in closestPoints)
        {
            var newPoint = new PtWSgmnts(closestPoint.pos);
            DrawPoint(newPoint.pos, "DenserPoint", 1f);
            var closestExistingPoint = points.OrderBy(p => Vector2.Distance(p.pos, newPoint.pos)).First();
            var zone = new List<LineSegment>();

            var closestSegment = closestExistingPoint.majorPaths.Where(s => !zone.Contains(s)).OrderBy(s => Vector2.Distance(s.TheOtherPoint(closestExistingPoint).pos, newPoint.pos)).FirstOrDefault();
            while (closestSegment != null)
            {
                zone.Add(closestSegment);
                var filteredSegments = closestSegment
                                        .TheOtherPoint(closestSegment.p0)
                                        .majorPaths
                                        .Where(s => !zone.Contains(s) && !s.ContainsEdgePointPos(closestSegment.p0));
                var orderedSegements = filteredSegments
                                        .OrderBy(s => Vector2.Distance(s.TheOtherPoint(closestSegment.p1).pos - s.TheOtherPoint(s.TheOtherPoint(closestSegment.p1)).pos, newPoint.pos));

                closestSegment = orderedSegements.FirstOrDefault();
                if (closestSegment != null)
                {
                    Debug.Log("Added segment");
                    DrawSegment(closestSegment, 0.5f, "ZoneSegment");
                }
            }

            continue;
            var closestExistingPoints = points.OrderBy(p => Vector2.Distance(p.pos, newPoint.pos)).Take(11).ToList();

            var segmentList = new List<LineSegment>();
            foreach (var existingPoint in closestExistingPoints)
            {
                DrawPoint(existingPoint.pos, "Existing point", 1f);

                if (true || !segmentList.Any(s => s.ContainsEdgePointPos(newPoint) && s.ContainsEdgePointPos(existingPoint)))
                {
                    var segment = new LineSegment(newPoint, existingPoint);
                    var segments = new List<LineSegment>();
                    segments.AddRange(existingPoint.majorPaths);
                    segments.AddRange(segmentList);
                    segments.AddRange(roads);
                    var minAngle = segments.Min(s => s.AngleBetweenLine(segment));
                    if (minAngle > 30)
                    {
                        segmentList.Add(segment);
                        DrawSegment(segment, 0.25f, "DesnertSegment");
                    }
                    else
                    {
                        Debug.Log($"Segmnet discarded, angle = {minAngle}");
                        DrawSegment(segment, 0.1f, "DiscardedSegment");
                    }
                }
            }
            points.Add(newPoint);
        }

        foreach (var segment in roads)
        {
			//DrawSegment(segment);
        }
    }

	public void DrawSegment(LineSegment segment, float width = 0.5f, string name = "Road")
	{
        var go = CreateCubeObjectWithMesh();
        go.name = name;
        var rectT = go.AddComponent<RectTransform>();
        var length = segment.Length;
        rectT.localScale = new Vector3(width, width, length * 2);
        rectT.position = new Vector3((float)segment.p0.pos.x, (float)segment.p0.pos.y, 0);
        rectT.rotation = Quaternion.LookRotation(segment.p1.pos - segment.p0.pos, Vector2.up);
        roadsObjects.Add(go);
    }

    private GameObject CreateCubeObjectWithMesh()
    {
        Vector3[] vertices = {
            new Vector3 (0, 0, 0),
            new Vector3 (0.5f, 0, 0),
            new Vector3 (0.5f, 0.5f, 0),
            new Vector3 (0, 0.5f, 0),
            new Vector3 (0, 0.5f, 0.5f),
            new Vector3 (0.5f, 0.5f, 0.5f),
            new Vector3 (0.5f, 0, 0.5f),
            new Vector3 (0, 0, 0.5f),
        };

        int[] triangles = {
         0, 2, 1, //face front
         0, 3, 2,
         2, 3, 4, //face top
         2, 4, 5,
         1, 2, 5, //face right
         1, 5, 6,
         0, 7, 4, //face left
         0, 4, 3,
         5, 4, 7, //face back
         5, 7, 6,
         0, 6, 7, //face bottom
         0, 1, 6
         };


        var go = new GameObject();
        Mesh mesh = go.AddComponent<MeshFilter>().mesh;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        var material = go.AddComponent<MeshRenderer>().material = mat;
		//material.color = Color.green;
        mesh.RecalculateNormals();
		return go;
    }

    public LineSegment FindSegmentTowards(PtWSgmnts point, Vector2 dest)
	{
		var list = point.majorPaths.OrderBy(s => Vector2.Distance(s.TheOtherPoint(point).pos, dest));
		return list.First();
    }

    #endregion 
    void DrawLine(Vector2 p1, Vector2 p2, Vector2 shift)
    {
        Gizmos.DrawLine(p1 + shift, p2 + shift);
    }

    void DrawLine(Vector2 p1, Vector2 p2)
    {
        Gizmos.DrawLine(p1, p2);
    }

    void DrawAllGizmos(Vector2 globalShift)
    {

        foreach(var point in intersections)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(point.pos + globalShift, 0.1f);
        }

        if (DrawVillageGizmos)
        {
            Gizmos.color = Color.white;
            for (int i = 0; i < villagesEdges.Count; i++)
            {
                break;
                Vector2 left = (Vector2)villagesEdges[i].p0.pos;
                Vector2 right = (Vector2)villagesEdges[i].p1.pos;
                Gizmos.DrawLine(left + globalShift, right + globalShift);
            }

            Gizmos.color = Color.black;
            for (int i = 0; i < villagesTriangles.Count; i++)
            {
                break;

                LineSegment seg = villagesTriangles[i];
                Vector2 left = (Vector2)seg.p0.pos;
                Vector2 right = (Vector2)seg.p1.pos;
                Gizmos.DrawLine(left + globalShift, right + globalShift);
            }

            Gizmos.color = Color.yellow;
            for (int i = 0; i < villageSpanningTree.Count; i++)
            {
                LineSegment seg = villageSpanningTree[i];
                for (int j = 0; j < seg.points.Count - 1; j++)
                {
                    Vector2 left = (Vector2)seg.points[j].pos;
                    Vector2 right = (Vector2)seg.points[j + 1].pos;
                    Gizmos.DrawLine(left + globalShift, right + globalShift);
                }

            }
        }


        if (points != null)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(points[i].pos + globalShift, points[i].majorPaths.Count / 10f + 0.1f);
                foreach (var village in points[i].neighbourVillages)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawSphere(village.pos + globalShift, 0.1f);
                }
            }

            for (int i = 0; i < points.Count; i++)
            {
                Gizmos.color = Color.green;
                foreach (var segment in points[i].majorPaths)
                {
                    for (int j = 0; j < segment.points.Count - 1; j++)
                    {
                        Gizmos.DrawLine(segment.points[j].pos + globalShift, segment.points[j + 1].pos + globalShift);
                    }
                }

                Gizmos.color = Color.white;
                foreach (var segment in points[i].minorPaths)
                {
                    for (int j = 0; j < segment.points.Count - 1; j++)
                    {
                        Gizmos.DrawLine(segment.points[j].pos + globalShift, segment.points[j + 1].pos + globalShift);
                    }
                }
            }

            Gizmos.color = Color.yellow;
            for (int i = 0; i < points.Count; i++)
            {
                foreach (var village in points[i].neighbourVillages)
                {
                    foreach (var villagePath in village.majorPaths)
                    {
                        for (int c = 0; c < villagePath.points.Count - 1; c++)
                        {
                            Gizmos.DrawLine(villagePath.points[c].pos + globalShift, villagePath.points[c + 1].pos + globalShift);
                        }
                    }
                }
            }
        }

        if (DrawEdges)
        {
            if (edges != null)
            {
                Gizmos.color = Color.white;
                for (int i = 0; i < edges.Count; i++)
                {
                    Vector2 left = (Vector2)edges[i].p0.pos;
                    Vector2 right = (Vector2)edges[i].p1.pos;
                    Gizmos.DrawLine(left + globalShift, right + globalShift);
                }
            }            
        }

        if (DrawTriangles)
        {
            Gizmos.color = Color.yellow;
            if (delaunayTriangulation != null)
            {
                for (int i = 0; i < delaunayTriangulation.Count; i++)
                {
                    Vector2 left = (Vector2)delaunayTriangulation[i].p0.pos;
                    Vector2 right = (Vector2)delaunayTriangulation[i].p1.pos;
                    Gizmos.DrawLine(left + globalShift, right + globalShift);
                }
            }
        }

        if (DrawRemovedTriangles)
        {
            for (int i = 0; i < removedTriangulation.Count; i++)
            {
                Vector2 left = (Vector2)removedTriangulation[i].p0.pos;
                Vector2 right = (Vector2)removedTriangulation[i].p1.pos;
                Gizmos.DrawLine(left + globalShift, right + globalShift);
            }
        }

        if (DrawSpanningTree)
        {
            if (spanningTree != null)
            {
                Gizmos.color = Color.blue;
                for (int i = 0; i < spanningTree.Count; i++)
                {
                    LineSegment seg = spanningTree[i];
                    Vector2 left = (Vector2)seg.p0.pos;
                    Vector2 right = (Vector2)seg.p1.pos;
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(left + globalShift, right + globalShift);
                }
            }            
        }

        if (DrawBoundaries)
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(new Vector2(0, 0) + globalShift, new Vector2(0, mapSize) + globalShift);
            Gizmos.DrawLine(new Vector2(0, 0) + globalShift, new Vector2(mapSize, 0) + globalShift);
            Gizmos.DrawLine(new Vector2(mapSize, 0) + globalShift, new Vector2(mapSize, mapSize) + globalShift);
            Gizmos.DrawLine(new Vector2(0, mapSize) + globalShift, new Vector2(mapSize, mapSize) + globalShift);
        }
    }
    
    public static Vector2 RotatePointAroundPivot(Vector3 point, Vector3 pivot, float angle)
    {
        var result = Quaternion.Euler(0, 0, angle) * (point - pivot) + pivot;
        return result;
    }

    void OnDrawGizmos ()
	{
        DrawAllGizmos(new Vector2());
        DrawAllGizmos(new Vector2(0, mapSize + minCitiesDistance));
        DrawAllGizmos(new Vector2(mapSize + minCitiesDistance, 0));
        DrawAllGizmos(new Vector2(mapSize + minCitiesDistance, mapSize + minCitiesDistance));
    }
}