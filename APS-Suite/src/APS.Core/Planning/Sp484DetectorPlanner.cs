using System;
using System.Collections.Generic;
using APS.Core.Geometry;

namespace APS.Core.Planning;

public sealed class DetectorNormCriteria
{
    public double MaxCeilingHeightMm { get; init; }
    public double ProtectedAreaM2 { get; init; }
    public double MaxSpacingMm { get; init; }
    public double MaxDistanceToWallMm { get; init; }
}

public sealed class PlanningConfig
{
    public double WallMinMm { get; init; } = 100.0;
    public double MaxShiftMm { get; init; } = 1500.0;
    public double SampleStepMm { get; init; } = 500.0;
    public int MinPerRoom { get; init; } = 1;
}

public sealed class PlannedRoomResult
{
    public List<Point2D> Points { get; } = [];
    public int ShiftedCount { get; set; }
    public int AddedCount { get; set; }
    public int RequiredMin { get; set; }
    public List<string> Issues { get; } = [];
}

public static class Sp484DetectorPlanner
{
    public static PlannedRoomResult Plan(
        IReadOnlyList<Point2D> outerPoly,
        IReadOnlyList<IReadOnlyList<Point2D>>? holes,
        DetectorNormCriteria criteria,
        PlanningConfig config)
    {
        var result = new PlannedRoomResult();
        double s = criteria.MaxSpacingMm;
        double w = criteria.MaxDistanceToWallMm;
        double a = criteria.ProtectedAreaM2 * 1e6;

        var box = Geometry2DUtils.ComputeBoundingBox(outerPoly);
        double l = box.Width;
        double b = box.Height;

        double roomAreaMm2 = Geometry2DUtils.PolygonArea(outerPoly);
        if (holes != null)
        {
            foreach (var h in holes) roomAreaMm2 -= Geometry2DUtils.PolygonArea(h);
        }

        int needByArea = a > 0 ? (int)Math.Ceiling(roomAreaMm2 / a - 1e-9) : 1;
        int need = Math.Max(config.MinPerRoom, Math.Max(needByArea, 1));
        result.RequiredMin = need;

        int nx = Math.Max(1, Math.Max((int)Math.Ceiling(l / s - 1e-9), (int)Math.Ceiling(l / (2.0 * w) - 1e-9)));
        int ny = Math.Max(1, Math.Max((int)Math.Ceiling(b / s - 1e-9), (int)Math.Ceiling(b / (2.0 * w) - 1e-9)));

        var candidates = new List<Point2D>();
        for (int iter = 0; iter < 50; iter++)
        {
            candidates.Clear();
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    var pt = new Point2D(box.MinX + l * (2.0 * i + 1.0) / (2.0 * nx),
                                         box.MinY + b * (2.0 * j + 1.0) / (2.0 * ny));
                    if (Geometry2DUtils.InsideRoomWithHoles(pt, outerPoly, holes))
                        candidates.Add(pt);
                }
            }

            if (candidates.Count >= need) break;
            if (l >= b) nx++; else ny++;
        }

        foreach (var cand in candidates)
        {
            if (Geometry2DUtils.DistanceToEdges(cand, outerPoly, holes) >= config.WallMinMm)
            {
                result.Points.Add(cand);
            }
            else
            {
                bool placed = false;
                for (double r = 100.0; r <= config.MaxShiftMm; r += 100.0)
                {
                    for (int angle = 0; angle < 16; angle++)
                    {
                        double rad = 2.0 * Math.PI * angle / 16.0;
                        var np = new Point2D(cand.X + r * Math.Cos(rad), cand.Y + r * Math.Sin(rad));
                        if (Geometry2DUtils.InsideRoomWithHoles(np, outerPoly, holes) &&
                            Geometry2DUtils.DistanceToEdges(np, outerPoly, holes) >= config.WallMinMm)
                        {
                            result.Points.Add(np);
                            result.ShiftedCount++;
                            placed = true;
                            break;
                        }
                    }
                    if (placed) break;
                }

                if (!placed)
                {
                    result.Issues.Add($"Точка ({cand.X:F0}; {cand.Y:F0}) не размещена: близко к стене");
                }
            }
        }

        if (result.Points.Count < need)
        {
            result.Issues.Add($"Расставлено {result.Points.Count} из требуемых {need} извещателей");
        }

        return result;
    }
}
