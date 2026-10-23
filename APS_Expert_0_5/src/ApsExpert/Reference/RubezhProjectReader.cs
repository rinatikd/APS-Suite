using System.Xml.Linq;

namespace ApsExpert.Reference;

/// <summary>
/// Reads structured RubezhCAD/R-CAD sidecar data when an .rcad project
/// is located next to the DWG. This reader intentionally does not judge
/// correctness; it only reconstructs the source project's declared model.
/// </summary>
public sealed class RubezhProjectReader
{
    public RubezhProjectSnapshot? TryReadForDwg(string dwgPath)
    {
        var current = new DirectoryInfo(Path.GetDirectoryName(dwgPath) ?? "");
        FileInfo? projectFileInfo = null;
        DirectoryInfo? projectDir = null;

        // R-CAD commonly stores DWG under <project>/DWG and .rcad/RCPD beside DWG.
        for (var i = 0; current != null && i < 4; i++, current = current.Parent)
        {
            projectFileInfo = current.GetFiles("*.rcad", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (projectFileInfo != null) { projectDir = current; break; }
        }

        if (projectFileInfo == null || projectDir == null)
            return null;

        var projectFile = projectFileInfo.FullName;
        var project = XDocument.Load(projectFile);
        var projectName = project.Root?.Element("ProjectName")?.Value ?? Path.GetFileNameWithoutExtension(projectFile);

        var rcpd = Path.Combine(projectDir.FullName, "RCPD");
        var equipFile = Path.Combine(rcpd, "db_proj_equip.xml");
        var lsFile = Path.Combine(rcpd, "db_proj_ls.xml");

        if (!File.Exists(equipFile))
            return new RubezhProjectSnapshot(projectName, projectFile, [], []);

        var equipment = ReadEquipment(equipFile);
        var lines = File.Exists(lsFile) ? ReadLines(lsFile) : [];

        return new RubezhProjectSnapshot(projectName, projectFile, equipment, lines);
    }

    private static List<RubezhEquipment> ReadEquipment(string file)
    {
        var doc = XDocument.Load(file);

        return doc.Root?.Elements("Equipment")
            .Select(e => new RubezhEquipment
            {
                Uid = e.Attribute("uid")?.Value ?? "",
                Name = e.Attribute("schmEqGroupName")?.Value ?? "",
                Article = e.Attribute("cvt_article")?.Value ?? "",
                UgoName = e.Attribute("ugoName")?.Value ?? "",
                PositionName = e.Attribute("positName")?.Value ?? "",
                PositionText = e.Attribute("positText")?.Value ?? "",
                PositionType = e.Attribute("positType")?.Value ?? "",
                MountingHeight = ParseNullableDouble(e.Attribute("mountingHeight")?.Value),
                Handle = e.Attribute("handle")?.Value ?? "",
                DwgId = e.Attribute("dwgId")?.Value ?? "",
                Lines = e.Descendants("port")
                    .Select(p => p.Attribute("lsName")?.Value)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Ports = e.Descendants("port")
                    .Select(p => new RubezhPort
                    {
                        Name = p.Attribute("name")?.Value ?? "",
                        SupportedType = p.Attribute("supportedLsType")?.Value ?? "",
                        LineName = p.Attribute("lsName")?.Value ?? ""
                    }).ToList()
            })
            .ToList() ?? [];
    }

    private static List<RubezhLine> ReadLines(string file)
    {
        var doc = XDocument.Load(file);

        return doc.Root?.Elements("CommunicLine")
            .Select(e => new RubezhLine
            {
                Name = e.Attribute("lsName")?.Value ?? "",
                UserName = e.Attribute("userLsName")?.Value ?? "",
                Type = e.Attribute("type")?.Value ?? "",
                Segments = e.Elements("CableSegment")
                    .Select(s => new RubezhCableSegment
                    {
                        Handle = s.Attribute("handle")?.Value ?? "",
                        CableId = s.Attribute("id")?.Value ?? "",
                        Length = ParseNullableDouble(s.Attribute("length")?.Value) ?? 0
                    }).ToList()
            })
            .ToList() ?? [];
    }

    private static double? ParseNullableDouble(string? value)
        => double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result : null;
}

public sealed record RubezhProjectSnapshot(
    string ProjectName,
    string ProjectFile,
    IReadOnlyList<RubezhEquipment> Equipment,
    IReadOnlyList<RubezhLine> Lines);

public sealed class RubezhEquipment
{
    public string Uid { get; init; } = "";
    public string Name { get; init; } = "";
    public string Article { get; init; } = "";
    public string UgoName { get; init; } = "";
    public string PositionName { get; init; } = "";
    public string PositionText { get; init; } = "";
    public string PositionType { get; init; } = "";
    public double? MountingHeight { get; init; }
    public string Handle { get; init; } = "";
    public string DwgId { get; init; } = "";
    public IReadOnlyList<string> Lines { get; init; } = [];
    public IReadOnlyList<RubezhPort> Ports { get; init; } = [];
}

public sealed class RubezhPort
{
    public string Name { get; init; } = "";
    public string SupportedType { get; init; } = "";
    public string LineName { get; init; } = "";
    public bool RequiresLine => SupportedType.Contains("address", StringComparison.OrdinalIgnoreCase)
        || SupportedType.Contains("control", StringComparison.OrdinalIgnoreCase)
        || SupportedType.Contains("notif_voice", StringComparison.OrdinalIgnoreCase);
}

public sealed class RubezhLine
{
    public string Name { get; init; } = "";
    public string UserName { get; init; } = "";
    public string Type { get; init; } = "";
    public IReadOnlyList<RubezhCableSegment> Segments { get; init; } = [];
}

public sealed class RubezhCableSegment
{
    public string Handle { get; init; } = "";
    public string CableId { get; init; } = "";
    public double Length { get; init; }
}
