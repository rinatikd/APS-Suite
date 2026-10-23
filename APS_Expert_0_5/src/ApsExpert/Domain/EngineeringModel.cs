namespace ApsExpert.Domain;

public sealed class EngineeringModel
{
    public string DrawingName { get; set; } = "";
    public List<Room> Rooms { get; } = [];
    public List<FireDetector> Detectors { get; } = [];
    public List<FireAlarmPanel> Panels { get; } = [];
    public List<CableRun> CableRuns { get; } = [];
}

public sealed class Room
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Number { get; set; } = "";
    public string Name { get; set; } = "";
    public double AreaM2 { get; set; }
    public double HeightM { get; set; }
    public Autodesk.AutoCAD.Geometry.Point2d Centroid { get; set; }
    public List<Autodesk.AutoCAD.Geometry.Point2d> Boundary { get; } = [];
}

public enum DetectorType { Smoke, Heat, Flame, ManualCallPoint, Unknown }

public sealed class FireDetector
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Tag { get; set; } = "";
    public DetectorType Type { get; set; } = DetectorType.Unknown;
    public Autodesk.AutoCAD.DatabaseServices.ObjectId CadObjectId { get; set; }
    public Autodesk.AutoCAD.Geometry.Point3d Position { get; set; }
    public Guid? RoomId { get; set; }
    public string? RubezhUid { get; set; }
    public string? RubezhHandle { get; set; }
    public string? RubezhArticle { get; set; }
    public IReadOnlyList<string> RubezhLines { get; set; } = [];
}

public sealed class FireAlarmPanel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Tag { get; set; } = "";
    public Autodesk.AutoCAD.DatabaseServices.ObjectId CadObjectId { get; set; }
}

public sealed class CableRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Tag { get; set; } = "";
    public double LengthM { get; set; }
    public string CableType { get; set; } = "";
}
