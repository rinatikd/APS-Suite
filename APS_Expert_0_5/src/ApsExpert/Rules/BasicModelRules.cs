using ApsExpert.Domain;

namespace ApsExpert.Rules;

/// <summary>
/// Технические sanity-checks MVP. Это НЕ нормативные правила.
/// Нормативные правила РК подключаются отдельным versioned rule-pack.
/// </summary>
public sealed class DuplicateDetectorTagRule : IAuditRule
{
    public string Id => "MODEL-001";
    public string Name => "Duplicate detector tags";

    public IEnumerable<AuditIssue> Evaluate(EngineeringModel model)
    {
        foreach (var group in model.Detectors
                     .Where(d => !string.IsNullOrWhiteSpace(d.Tag))
                     .GroupBy(d => d.Tag, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            foreach (var detector in group)
            {
                yield return new AuditIssue
                {
                    RuleId = Id,
                    Title = "Дублируется обозначение извещателя",
                    Description = $"Обозначение {group.Key} встречается {group.Count()} раз.",
                    Severity = Severity.Error,
                    Source = "APS Expert — model integrity",
                    CadObjectId = detector.CadObjectId,
                    Location = detector.Position,
                    AutoFixAvailable = false,
                    SuggestedAction = "Проверить маркировку и уникальность адреса/позиционного обозначения."
                };
            }
        }
    }
}
