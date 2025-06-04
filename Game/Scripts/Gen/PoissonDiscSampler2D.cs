using Assets.Game.Scripts.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;

public static class PoissonDiscSampler2D
{
    public static List<Vector2> GeneratePoints(System.Random rnd, float radius, Rect region, int rejectionSamples = 30)
    {
        float cellSize = radius / Mathf.Sqrt(2);
        int gridWidth = Mathf.CeilToInt(region.width / cellSize);
        int gridHeight = Mathf.CeilToInt(region.height / cellSize);

        Vector2[,] grid = new Vector2[gridWidth, gridHeight];
        List<Vector2> points = new List<Vector2>();
        List<Vector2> spawnPoints = new List<Vector2>();

        Vector2 regionCenter = new Vector2(region.x + region.width / 2f, region.y + region.height / 2f);
        spawnPoints.Add(regionCenter);

        while (spawnPoints.Count > 0)
        {
            int spawnIndex = rnd.Next(0, spawnPoints.Count);
            Vector2 spawnCentre = spawnPoints[spawnIndex];
            bool accepted = false;

            for (int i = 0; i < rejectionSamples; i++)
            {
                float angle = rnd.NextFloat() * Mathf.PI * 2;
                float dist = rnd.NextFloat(radius, 2 * radius + 1);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 candidate = spawnCentre + dir * dist;

                if (IsValid(candidate, region, cellSize, radius, grid))
                {
                    points.Add(candidate);
                    spawnPoints.Add(candidate);
                    int cellX = (int)((candidate.x - region.x) / cellSize);
                    int cellY = (int)((candidate.y - region.y) / cellSize);
                    grid[cellX, cellY] = candidate;
                    accepted = true;
                    break;
                }
            }

            if (!accepted)
                spawnPoints.RemoveAt(spawnIndex);
        }

        return points;
    }

    private static bool IsValid(Vector2 candidate, Rect region, float cellSize, float radius, Vector2[,] grid)
    {
        if (!region.Contains(candidate))
            return false;

        int cellX = (int)((candidate.x - region.x) / cellSize);
        int cellY = (int)((candidate.y - region.y) / cellSize);

        int searchStartX = Mathf.Max(0, cellX - 2);
        int searchEndX = Mathf.Min(cellX + 2, grid.GetLength(0) - 1);
        int searchStartY = Mathf.Max(0, cellY - 2);
        int searchEndY = Mathf.Min(cellY + 2, grid.GetLength(1) - 1);

        for (int x = searchStartX; x <= searchEndX; x++)
        {
            for (int y = searchStartY; y <= searchEndY; y++)
            {
                if (grid[x, y] != Vector2.zero)
                {
                    float sqrDist = (grid[x, y] - candidate).sqrMagnitude;
                    if (sqrDist < radius * radius)
                        return false;
                }
            }
        }

        return true;
    }
}
