using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Gen.WorldGen;
using Assets.Game.Scripts.Utility;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoronoiDemo))]
public class VoronoiDemoEditor : Editor
{
    VoronoiDemo v;

    public void Start()
    {

    }
    
    public override void OnInspectorGUI()
    {
        v = (VoronoiDemo)target;
        v.avgDensity = EditorGUILayout.FloatField(nameof(v.avgDensity), v.avgDensity);

        v.DrawTriangles = EditorGUILayout.Toggle(nameof(v.DrawTriangles), v.DrawTriangles);
        v.DrawSpanningTree = EditorGUILayout.Toggle(nameof(v.DrawSpanningTree), v.DrawSpanningTree);
        v.DrawEdges = EditorGUILayout.Toggle(nameof(v.DrawEdges), v.DrawEdges);
        v.DrawBoundaries = EditorGUILayout.Toggle(nameof(v.DrawBoundaries), v.DrawBoundaries);
        v.DrawRemovedTriangles = EditorGUILayout.Toggle(nameof(v.DrawRemovedTriangles), v.DrawRemovedTriangles);
        v.MinPathTriangleAngle = EditorGUILayout.FloatField(nameof(v.MinPathTriangleAngle), v.MinPathTriangleAngle);
        v.MinorPathMinAngle = EditorGUILayout.FloatField(nameof(v.MinorPathMinAngle), v.MinorPathMinAngle);
        v.Seed = EditorGUILayout.TextField(nameof(v.Seed), v.Seed);
        v.RandomizeSeed = EditorGUILayout.Toggle(nameof(v.RandomizeSeed), v.RandomizeSeed);

        v.DrawVillageGizmos = EditorGUILayout.Toggle(nameof(v.DrawVillageGizmos), v.DrawVillageGizmos);
        v.VillagesSeed = EditorGUILayout.TextField(nameof(v.VillagesSeed), v.VillagesSeed);
        v.RandomizeVillagesSeed = EditorGUILayout.Toggle(nameof(v.RandomizeVillagesSeed), v.RandomizeVillagesSeed);

        v.mapSize = EditorGUILayout.IntField(nameof(v.mapSize), v.mapSize);
        v.minCitiesDistance = EditorGUILayout.FloatField(nameof(v.minCitiesDistance), v.minCitiesDistance);
        v.maxRoadSegmentLength = EditorGUILayout.FloatField(nameof(v.maxRoadSegmentLength), v.maxRoadSegmentLength);
        v.MaxIntersecDist = EditorGUILayout.FloatField(nameof(v.MaxIntersecDist), v.MaxIntersecDist);
        v.ExperimentalDistort = EditorGUILayout.Toggle(nameof(v.ExperimentalDistort), v.ExperimentalDistort);

        VoronoiDemo.DistortionDecay = EditorGUILayout.FloatField(nameof(VoronoiDemo.DistortionDecay), VoronoiDemo.DistortionDecay);
        if (GUILayout.Button("Generate"))
        {
            v.RunDiagram();
        }
    }
}
