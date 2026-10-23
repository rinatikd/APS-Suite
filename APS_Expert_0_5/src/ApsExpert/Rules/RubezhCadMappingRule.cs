using ApsExpert.Domain;
using ApsExpert.Reference;

namespace ApsExpert.Rules;

public sealed class RubezhCadMappingRule
{
    public IReadOnlyList<AuditIssue> Evaluate(RubezhCadMappingResult mapping)
    {
        var issues = new List<AuditIssue>();
        var pct = mapping.MatchRatio * 100.0;
        var severity = mapping.Mapped.Count == 0 ? Severity.Warning : Severity.Info;
        issues.Add(new AuditIssue
        {
            RuleId = "MAP-RUBEZH-HANDLE",
            Title = $"Связь R-CAD ↔ AutoCAD: {mapping.Mapped.Count}/{mapping.Mapped.Count + mapping.Unmapped.Count} ({pct:F1}%)",
            Description = mapping.Mapped.Count == 0
                ? "Не удалось подтвердить соответствие RubezhCAD handle объектам текущего DWG."
                : $"Первая часть handle R-CAD сопоставлена с AutoCAD entity handle для {mapping.Mapped.Count} элементов. Это техническая связка, а не доказательство нормативной корректности.",
            Severity = severity,
            Source = "RCPD/db_proj_equip.xml + AutoCAD entity handles",
            SuggestedAction = mapping.Unmapped.Count == 0
                ? "Можно использовать точное соответствие для геометрического анализа."
                : $"Проверить {mapping.Unmapped.Count} элементов, не найденных в текущем DWG; возможно, это XREF, удалённые или не-графические объекты."
        });
        return issues;
    }
}
