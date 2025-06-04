using ClipperLib;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;

public static class PolygonUtils
{
    const double SCALE = 100000.0;

    public static List<Vector2> PolygonClip(List<Vector2> subject, List<Vector2> clip)
    {
        var subjPath = ToClipperPath(subject);
        var clipPath = ToClipperPath(clip);

        var clipper = new Clipper();
        clipper.AddPath(subjPath, PolyType.ptSubject, true);
        clipper.AddPath(clipPath, PolyType.ptClip, true);

        var solution = new List<List<IntPoint>>();
        clipper.Execute(ClipType.ctDifference, solution); //, PolyFillType.pftNonZero

        // Zak³adamy ¿e zostaje tylko jeden polygon (dziedziniec)
        if (solution.Count == 0) return new List<Vector2>();

        return ToVectorPath(solution[0]);
    }

    private static List<IntPoint> ToClipperPath(List<Vector2> input)
    {
        return input.Select(v => new IntPoint(
        (long)(v.x * SCALE),
            (long)(v.y * SCALE)
        )).ToList();
    }

    private static List<Vector2> ToVectorPath(List<IntPoint> path)
    {
        return path.Select(p => new Vector2(
            (float)(p.X / SCALE),
            (float)(p.Y / SCALE)
        )).ToList();
    }
}