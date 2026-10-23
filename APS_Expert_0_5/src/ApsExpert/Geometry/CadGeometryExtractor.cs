using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ApsExpert.Domain;
using System.Globalization;

namespace ApsExpert.Geometry;

public sealed class CadGeometryExtractor
{
    private static readonly string[] RoomLayerHints = ["ROOM", "ПОМ", "АРХ", "A-AREA", "A-ROOM", "ПОМЕЩ"];

    public GeometrySnapshot Extract()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var db = doc.Database;
        var result = new GeometrySnapshot(doc.Name);

        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in ms)
        {
            if (id.ObjectClass.IsDerivedFrom(RXClass.GetClass(typeof(Polyline))))
            {
                var pl = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                if (pl != null && pl.Closed && pl.NumberOfVertices >= 3 && pl.Area > 1.0)
                {
                    var points = new List<Point2d>();
                    for (var i = 0; i < pl.NumberOfVertices; i++)
                        points.Add(pl.GetPoint2dAt(i));
                    result.CandidateRooms.Add(new RoomGeometry
                    {
                        ObjectId = id,
                        Layer = pl.Layer,
                        Boundary = points,
                        AreaM2 = pl.Area,
                        Centroid = TryCentroid(points),
                        IsRoomLayer = IsRoomLayer(pl.Layer)
                    });
                }
            }
            else if (id.ObjectClass.IsDerivedFrom(RXClass.GetClass(typeof(BlockReference))))
            {
                var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                if (br == null) continue;
                var name = GetEffectiveBlockName(br, tr);
                var text = ReadAttributes(br, tr);
                var combined = (name + " " + text).ToUpperInvariant();
                var type = ClassifyDetector(combined);
                if (type != DetectorType.Unknown)
                {
                    result.Detectors.Add(new DetectorGeometry
                    {
                        ObjectId = id,
                        BlockName = name,
                        Layer = br.Layer,
                        Position = br.Position,
                        Tag = ExtractTag(br, tr) ?? name,
                        Type = type
                    });
                }
            }
        }

        // Prefer room-like layers; otherwise retain all candidates but mark the fallback.
        if (result.CandidateRooms.Any(x => x.IsRoomLayer))
            result.Rooms.AddRange(result.CandidateRooms.Where(x => x.IsRoomLayer));
        else
        {
            result.Rooms.AddRange(result.CandidateRooms);
            result.UsedRoomFallback = true;
        }

        foreach (var d in result.Detectors)
        {
            var room = result.Rooms.FirstOrDefault(r => PointInPolygon.Contains(new Point2d(d.Position.X, d.Position.Y), r.Boundary));
            d.RoomObjectId = room?.ObjectId;
            d.RoomAreaM2 = room?.AreaM2;
            d.DistanceToRoomBoundaryM = room == null ? null : DistanceToPolygon(new Point2d(d.Position.X, d.Position.Y), room.Boundary);
        }

        tr.Commit();
        return result;
    }

    private static bool IsRoomLayer(string layer)
        => RoomLayerHints.Any(h => layer.Contains(h, StringComparison.OrdinalIgnoreCase));

    private static string GetEffectiveBlockName(BlockReference br, Transaction tr)
    {
        var id = br.DynamicBlockTableRecord != ObjectId.Null ? br.DynamicBlockTableRecord : br.BlockTableRecord;
        var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
        return btr.Name;
    }

    private static string ReadAttributes(BlockReference br, Transaction tr)
    {
        if (!br.AttributeCollection.Cast<ObjectId>().Any()) return "";
        var parts = new List<string>();
        foreach (ObjectId id in br.AttributeCollection)
        {
            if (!id.IsValid) continue;
            var ar = tr.GetObject(id, OpenMode.ForRead) as AttributeReference;
            if (ar != null) parts.Add(ar.TextString);
        }
        return string.Join(" ", parts);
    }

    private static string? ExtractTag(BlockReference br, Transaction tr)
    {
        foreach (ObjectId id in br.AttributeCollection)
        {
            if (!id.IsValid) continue;
            var ar = tr.GetObject(id, OpenMode.ForRead) as AttributeReference;
            if (ar == null) continue;
            var t = ar.TextString?.Trim();
            if (!string.IsNullOrWhiteSpace(t) && (t.Contains("ИП") || t.Contains("ДП") || t.Contains("ИПР") || t.Contains("РВ") || t.Contains("МП")))
                return t;
        }
        return null;
    }

    private static DetectorType ClassifyDetector(string n)
    {
        if (n.Contains("ИПР") || n.Contains("РУЧН") || n.Contains("MANUAL") || n.Contains("УДП")) return DetectorType.ManualCallPoint;
        if (n.Contains("ИП212") || n.Contains("ДЫМ") || n.Contains("SMOKE")) return DetectorType.Smoke;
        if (n.Contains("ИП101") || n.Contains("ТЕПЛ") || n.Contains("HEAT")) return DetectorType.Heat;
        if (n.Contains("ПЛАМ") || n.Contains("FLAME")) return DetectorType.Flame;
        if (n.Contains("ИЗВЕЩ") || n.Contains("ДАТЧ") || n.Contains("DETECT")) return DetectorType.Unknown;
        return DetectorType.Unknown;
    }

    private static Point2d TryCentroid(IReadOnlyList<Point2d> p)
    {
        double a2 = 0, cx = 0, cy = 0;
        for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
        {
            var cross = p[j].X * p[i].Y - p[i].X * p[j].Y;
            a2 += cross;
            cx += (p[j].X + p[i].X) * cross;
            cy += (p[j].Y + p[i].Y) * cross;
        }
        if (Math.Abs(a2) < 1e-9) return p[0];
        return new Point2d(cx / (3 * a2), cy / (3 * a2));
    }

    private static double DistanceToPolygon(Point2d p, IReadOnlyList<Point2d> poly)
    {
        double min = double.MaxValue;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            min = Math.Min(min, DistancePointSegment(p, a, b));
        }
        return min;
    }

    private static double DistancePointSegment(Point2d p, Point2d a, Point2d b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12) return p.GetDistanceTo(a);
        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Max(0, Math.Min(1, t));
        return p.GetDistanceTo(new Point2d(a.X + t * dx, a.Y + t * dy));
    }
}

public sealed class GeometrySnapshot
{
    public GeometrySnapshot(string drawingName) => DrawingName = drawingName;
    public string DrawingName { get; }
    public List<RoomGeometry> CandidateRooms { get; } = [];
    public List<RoomGeometry> Rooms { get; } = [];
    public List<DetectorGeometry> Detectors { get; } = [];
    public bool UsedRoomFallback { get; set; }
}

public sealed class RoomGeometry
{
    public ObjectId ObjectId { get; init; }
    public string Layer { get; init; } = "";
    public bool IsRoomLayer { get; init; }
    public double AreaM2 { get; init; }
    public Point2d Centroid { get; init; }
    public List<Point2d> Boundary { get; init; } = [];
}

public sealed class DetectorGeometry
{
    public ObjectId ObjectId { get; init; }
    public string BlockName { get; init; } = "";
    public string Layer { get; init; } = "";
    public string Tag { get; init; } = "";
    public DetectorType Type { get; init; }
    public Point3d Position { get; init; }
    public ObjectId? RoomObjectId { get; set; }
    public double? RoomAreaM2 { get; set; }
    public double? DistanceToRoomBoundaryM { get; set; }
}
