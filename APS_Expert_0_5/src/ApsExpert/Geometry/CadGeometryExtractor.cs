using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ApsExpert.Domain;
using APS.Core.Geometry;

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
                    var points = new List<Point2D>();
                    for (var i = 0; i < pl.NumberOfVertices; i++)
                    {
                        var p = pl.GetPoint2dAt(i);
                        points.Add(new Point2D(p.X, p.Y));
                    }

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

        if (result.CandidateRooms.Any(x => x.IsRoomLayer))
            result.Rooms.AddRange(result.CandidateRooms.Where(x => x.IsRoomLayer));
        else
        {
            result.Rooms.AddRange(result.CandidateRooms);
            result.UsedRoomFallback = true;
        }

        foreach (var d in result.Detectors)
        {
            var p = new Point2D(d.Position.X, d.Position.Y);
            var room = result.Rooms.FirstOrDefault(r => Geometry2DUtils.PointInPolygon(p, r.Boundary));
            d.RoomObjectId = room?.ObjectId;
            d.RoomAreaM2 = room?.AreaM2;
            d.DistanceToRoomBoundaryM = room == null ? null : Geometry2DUtils.DistanceToEdges(p, room.Boundary, null);
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

    private static Point2D TryCentroid(IReadOnlyList<Point2D> p)
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
        return new Point2D(cx / (3 * a2), cy / (3 * a2));
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
    public Point2D Centroid { get; init; }
    public List<Point2D> Boundary { get; init; } = [];
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
