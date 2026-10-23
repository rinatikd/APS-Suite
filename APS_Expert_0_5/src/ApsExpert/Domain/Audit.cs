namespace ApsExpert.Domain;

public enum Severity { Critical, Error, Warning, Review, Info, Pass }

public sealed class AuditIssue
{
    public string RuleId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public Severity Severity { get; init; }
    public string Source { get; init; } = "";
    public string? SourceClause { get; init; }
    public Autodesk.AutoCAD.DatabaseServices.ObjectId? CadObjectId { get; init; }
    public Autodesk.AutoCAD.Geometry.Point3d? Location { get; init; }
    public bool AutoFixAvailable { get; init; }
    public string? SuggestedAction { get; init; }
}

public sealed class ProposedChange
{
    public string Description { get; init; } = "";
    public string RuleId { get; init; } = "";
    public List<Autodesk.AutoCAD.DatabaseServices.ObjectId> RelatedObjects { get; } = [];
}
