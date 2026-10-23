using System;
using System.Collections.Generic;

namespace APS.Core.Geometry;

public readonly record struct Point2D(double X, double Y)
{
    public double DistanceTo(Point2D other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
}

public readonly record struct BoundingBox2D(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
}

public static class Geometry2DUtils
{
    public static BoundingBox2D ComputeBoundingBox(IReadOnlyList<Point2D> points)
    {
        if (points.Count == 0) return new BoundingBox2D(0, 0, 0, 0);
        double minX = points[0].X, maxX = points[0].X;
        double minY = points[0].Y, maxY = points[0].Y;

        for (int i = 1; i < points.Count; i++)
        {
            var p = points[i];
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        return new BoundingBox2D(minX, minY, maxX, maxY);
    }

    public static double PolygonArea(IReadOnlyList<Point2D> polygon)
    {
        double area = 0.0;
        int n = polygon.Count;
        for (int i = 0; i < n; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % n];
            area += (p1.X * p2.Y) - (p2.X * p1.Y);
        }
        return Math.Abs(area) / 2.0;
    }

    public static bool PointInPolygon(Point2D pt, IReadOnlyList<Point2D> polygon)
    {
        bool inside = false;
        int n = polygon.Count;
        int j = n - 1;

        for (int i = 0; i < n; i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            if ((pi.Y > pt.Y) != (pj.Y > pt.Y))
            {
                double xc = (pj.X - pi.X) * (pt.Y - pi.Y) / ((pj.Y - pi.Y == 0) ? 1e-12 : (pj.Y - pi.Y)) + pi.X;
                if (pt.X < xc) inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    public static bool InsideRoomWithHoles(Point2D pt, IReadOnlyList<Point2D> outerPoly, IReadOnlyList<IReadOnlyList<Point2D>>? holes)
    {
        if (!PointInPolygon(pt, outerPoly)) return false;
        if (holes != null)
        {
            foreach (var hole in holes)
            {
                if (PointInPolygon(pt, hole)) return false;
            }
        }
        return true;
    }

    public static double DistancePointToSegment(Point2D pt, Point2D p1, Point2D p2)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double l2 = dx * dx + dy * dy;
        if (l2 < 1e-9) return pt.DistanceTo(p1);

        double t = Math.Max(0.0, Math.Min(1.0, ((pt.X - p1.X) * dx + (pt.Y - p1.Y) * dy) / l2));
        Point2D proj = new Point2D(p1.X + t * dx, p1.Y + t * dy);
        return pt.DistanceTo(proj);
    }

    public static double DistanceToEdges(Point2D pt, IReadOnlyList<Point2D> outerPoly, IReadOnlyList<IReadOnlyList<Point2D>>? holes)
    {
        double best = double.MaxValue;
        best = UpdateMinDistance(pt, outerPoly, best);

        if (holes != null)
        {
            foreach (var h in holes)
            {
                best = UpdateMinDistance(pt, h, best);
            }
        }
        return best;
    }

    private static double UpdateMinDistance(Point2D pt, IReadOnlyList<Point2D> poly, double currentMin)
    {
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            double d = DistancePointToSegment(pt, poly[i], poly[(i + 1) % n]);
            if (d < currentMin) currentMin = d;
        }
        return currentMin;
    }
}
