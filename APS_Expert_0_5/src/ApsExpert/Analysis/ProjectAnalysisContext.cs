using ApsExpert.Domain;
using ApsExpert.Reference;

namespace ApsExpert.Analysis;

/// <summary>Unified evidence available to engineering rules. Rules must distinguish facts from assumptions.</summary>
public sealed class ProjectAnalysisContext
{
    public required EngineeringModel CadModel { get; init; }
    public RubezhProjectSnapshot? Rubezh { get; init; }
    public RubezhCadMappingResult? Mapping { get; init; }
    public RulePack Criteria { get; init; } = new();
}

public sealed class RulePack
{
    public string Name { get; set; } = "APS Expert Design Criteria (non-normative baseline)";
    public string Version { get; set; } = "0.5";
    public AddressLineCriteria AddressLine { get; set; } = new();
    public DetectorGeometryCriteria DetectorGeometry { get; set; } = new();
}

public sealed class AddressLineCriteria
{
    public int MaxDevices { get; set; } = 250;
    public double MaxLengthM { get; set; } = 3000;
    public bool Enabled { get; set; } = true;
}

public sealed class DetectorGeometryCriteria
{
    public bool Enabled { get; set; } = false;
    public double? MaxDetectorSpacingM { get; set; }
    public double? MaxBoundaryDistanceM { get; set; }
}
