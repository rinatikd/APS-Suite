using ApsExpert.Domain;

namespace ApsExpert.Reference;

/// <summary>
/// Deterministic audit of the structured RubezhCAD/R-CAD project data.
/// This layer checks model integrity and manufacturer-declared equipment limits;
/// it does not replace the Kazakhstan normative rule pack.
/// </summary>
public sealed class RubezhReferenceAudit
{
    public IReadOnlyList<AuditIssue> Evaluate(RubezhProjectSnapshot snapshot)
    {
        var issues = new List<AuditIssue>();
        var equipmentByLine = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var eq in snapshot.Equipment)
        {
            foreach (var line in eq.Lines.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!equipmentByLine.TryGetValue(line, out var set))
                    equipmentByLine[line] = set = new(StringComparer.OrdinalIgnoreCase);
                set.Add(eq.Handle);
            }
        }

        // R3-Рубеж-2ОП: 2 АЛС, up to 250 address devices per ALS, max 3000 m per ALS.
        foreach (var line in snapshot.Lines.Where(x => x.Type.Equals("address", StringComparison.OrdinalIgnoreCase)))
        {
            var count = equipmentByLine.TryGetValue(line.Name, out var set) ? set.Count : 0;
            var lengthM = line.Segments.Sum(x => x.Length) / 1000.0;

            if (count > 250)
            {
                issues.Add(new AuditIssue
                {
                    RuleId = "R3-ALS-COUNT",
                    Title = $"Превышена ёмкость {line.Name}",
                    Description = $"В линии обнаружено {count} адресных устройств при предельном значении 250.",
                    Severity = Severity.Error,
                    Source = "R-CAD project data / R3-Рубеж-2ОП",
                    SuggestedAction = "Разделить нагрузку между АЛС или проверить структуру проекта."
                });
            }
            else
            {
                issues.Add(new AuditIssue
                {
                    RuleId = "R3-ALS-COUNT",
                    Title = $"Ёмкость {line.Name} в норме",
                    Description = $"В линии {count} адресных устройств; предел 250.",
                    Severity = Severity.Info,
                    Source = "R-CAD project data / R3-Рубеж-2ОП",
                    SuggestedAction = "Действий не требуется."
                });
            }

            if (lengthM > 3000)
            {
                issues.Add(new AuditIssue
                {
                    RuleId = "R3-ALS-LENGTH",
                    Title = $"Превышена длина {line.Name}",
                    Description = $"Расчётная сумма сегментов {lengthM:F3} м при пределе 3000 м.",
                    Severity = Severity.Error,
                    Source = "R-CAD project data / R3-Рубеж-2ОП",
                    SuggestedAction = "Проверить трассу и разделить/перестроить АЛС."
                });
            }
            else
            {
                issues.Add(new AuditIssue
                {
                    RuleId = "R3-ALS-LENGTH",
                    Title = $"Длина {line.Name} в пределах лимита",
                    Description = $"Расчётная сумма сегментов {lengthM:F3} м; предел 3000 м.",
                    Severity = Severity.Info,
                    Source = "R-CAD project data / R3-Рубеж-2ОП",
                    SuggestedAction = "Действий не требуется."
                });
            }
        }

        // Empty line bindings are review items, not automatic errors: some ports are intentionally local/unwired.
        foreach (var eq in snapshot.Equipment)
        {
            if (eq.Ports.Any(p => p.RequiresLine && string.IsNullOrWhiteSpace(p.LineName)))
            {
                var ports = string.Join(", ", eq.Ports.Where(p => p.RequiresLine && string.IsNullOrWhiteSpace(p.LineName)).Select(p => p.Name));
                issues.Add(new AuditIssue
                {
                    RuleId = "MODEL-DANGLING-PORT",
                    Title = $"Порт без привязки: {eq.PositionName}",
                    Description = $"Оборудование {eq.Name}; неподключённые в проектных данных порты: {ports}.",
                    Severity = Severity.Warning,
                    Source = "RCPD/db_proj_equip.xml",
                    SuggestedAction = "Проверить по плану, структурной схеме и принципиальной схеме. Не считать ошибкой без контекстной проверки."
                });
            }
        }

        return issues;
    }
}
