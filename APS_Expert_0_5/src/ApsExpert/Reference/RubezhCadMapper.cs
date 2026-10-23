using Autodesk.AutoCAD.DatabaseServices;
using ApsExpert.Domain;

namespace ApsExpert.Reference;

/// <summary>
/// Maps the first part of a RubezhCAD handle (e.g. 103512#...) to an AutoCAD entity handle.
/// The mapping is treated as a hypothesis and is reported with a match ratio; it must be
/// validated against the real drawing before being used for automatic edits.
/// </summary>
public sealed class RubezhCadMapper
{
    public RubezhCadMappingResult Map(Database db, RubezhProjectSnapshot snapshot)
    {
        var result = new RubezhCadMappingResult();
        foreach (var eq in snapshot.Equipment)
        {
            var token = eq.Handle.Split('#', 2)[0].Trim();
            if (string.IsNullOrWhiteSpace(token)) { result.Unmapped.Add(eq); continue; }
            if (!long.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out var value))
            {
                result.Unmapped.Add(eq); continue;
            }

            try
            {
                var id = db.GetObjectId(false, new Handle(value), 0);
                if (id.IsNull || !id.IsValid)
                {
                    result.Unmapped.Add(eq);
                    continue;
                }
                result.Mapped.Add(new RubezhCadMappedEquipment(eq, id));
            }
            catch
            {
                result.Unmapped.Add(eq);
            }
        }
        return result;
    }
}

public sealed class RubezhCadMappingResult
{
    public List<RubezhCadMappedEquipment> Mapped { get; } = [];
    public List<RubezhEquipment> Unmapped { get; } = [];
    public double MatchRatio => Mapped.Count + Unmapped.Count == 0 ? 0 : (double)Mapped.Count / (Mapped.Count + Unmapped.Count);
}

public sealed record RubezhCadMappedEquipment(RubezhEquipment Equipment, ObjectId CadObjectId);
