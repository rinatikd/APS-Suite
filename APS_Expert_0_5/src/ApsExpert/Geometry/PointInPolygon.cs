using Autodesk.AutoCAD.Geometry;

namespace ApsExpert.Geometry;

public static class PointInPolygon
{
    public static bool Contains(Point2d point, IReadOnlyList<Point2d> polygon)
    {
        if (polygon.Count < 3) return false;
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            var intersects = ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                point.X < (pj.X - pi.X) * (point.Y - pi.Y) / ((pj.Y - pi.Y) == 0 ? double.Epsilon : (pj.Y - pi.Y)) + pi.X;
            if (intersects) inside = !inside;
        }
        return inside;
    }
}
