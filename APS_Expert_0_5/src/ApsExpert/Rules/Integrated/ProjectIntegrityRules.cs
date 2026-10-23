using ApsExpert.Analysis;
using ApsExpert.Domain;
using APS.Core.Electrical;
using APS.Core.Geometry;

namespace ApsExpert.Rules.Integrated;

public sealed class MappingIntegrityRule : IProjectRule
{
    public string Id => "INT-MAP-001";
    public IEnumerable<AuditIssue> Evaluate(ProjectAnalysisContext c)
    {
        if (c.Rubezh == null) yield break;
        if (c.Mapping == null)
        {
            yield return new AuditIssue
            {
                RuleId = Id,
                Title = "R-CAD модель не сопоставлена с DWG",
                Description = "Невозможно безопасно связать инженерные данные R-CAD с геометрией текущего DWG.",
                Severity = Severity.Warning,
                Source = "R-CAD + DWG",
                SuggestedAction = "Выполнить APS_MAP_RUBEZH и проверить процент сопоставления."
            };
            yield break;
        }

        var total = c.Mapping.Mapped.Count + c.Mapping.Unmapped.Count;
        var pct = total == 0 ? 0 : 100.0 * c.Mapping.Mapped.Count / total;
        var sev = c.Mapping.Unmapped.Count == 0 ? Severity.Info : Severity.Warning;

        yield return new AuditIssue
        {
            RuleId = Id,
            Title = $"Сопоставление R-CAD ↔ DWG: {pct:F1}%",
            Description = $"Сопоставлено {c.Mapping.Mapped.Count} из {total} элементов. Несопоставлено: {c.Mapping.Unmapped.Count}.",
            Severity = sev,
            Source = "R-CAD handle → AutoCAD Handle",
            SuggestedAction = c.Mapping.Unmapped.Count == 0
                ? "Связка подтверждена для всех прочитанных элементов."
                : "Проверить XREF, удалённые объекты и элементы без графического представления."
        };
    }
}

public sealed class AddressLineIntegrityRule : IProjectRule
{
    public string Id => "INT-ALS-001";
    public IEnumerable<AuditIssue> Evaluate(ProjectAnalysisContext c)
    {
        if (c.Rubezh == null || !c.Criteria.AddressLine.Enabled) yield break;

        foreach (var line in c.Rubezh.Lines)
        {
            var count = c.Rubezh.Equipment.Count(e => e.Lines.Any(x => string.Equals(x, line.Name, StringComparison.OrdinalIgnoreCase)));
            var length = line.Segments.Sum(s => s.Length);

            if (count > c.Criteria.AddressLine.MaxDevices)
            {
                yield return new AuditIssue
                {
                    RuleId = Id,
                    Title = $"АЛС {line.Name}: превышено количество устройств",
                    Description = $"Фактически {count}, критерий {c.Criteria.AddressLine.MaxDevices}.",
                    Severity = Severity.Error,
                    Source = "R-CAD topology",
                    SourceClause = "RulePack.AddressLine.MaxDevices",
                    SuggestedAction = "Разделить нагрузку между линиями либо проверить модель линии."
                };
            }
            else if (count > 0)
            {
                yield return new AuditIssue
                {
                    RuleId = Id,
                    Title = $"АЛС {line.Name}: {count} устройств",
                    Description = $"Количество устройств не превышает заданный критерий {c.Criteria.AddressLine.MaxDevices}.",
                    Severity = Severity.Info,
                    Source = "R-CAD topology",
                    SourceClause = "RulePack.AddressLine.MaxDevices"
                };
            }

            if (length > c.Criteria.AddressLine.MaxLengthM)
            {
                yield return new AuditIssue
                {
                    RuleId = Id,
                    Title = $"АЛС {line.Name}: превышена длина",
                    Description = $"Расчётная длина сегментов {length:F2} м, критерий {c.Criteria.AddressLine.MaxLengthM:F2} м.",
                    Severity = Severity.Error,
                    Source = "R-CAD topology",
                    SourceClause = "RulePack.AddressLine.MaxLengthM",
                    SuggestedAction = "Проверить трассу и разделение линии."
                };
            }

            if (line.Segments.Count > 0 && count > 0)
            {
                var segs = line.Segments.Select(s => s.Length).ToList();
                var curStandby = Enumerable.Repeat(0.35, count).ToList();
                var curMax = Enumerable.Repeat(0.46, count).ToList();

                var radialResult = ElectricalCalculator.CalculateRadial(
                    segs, curStandby, curMax,
                    rOhmKm: 24.5,
                    u0: 24.0,
                    uMin: 20.0,
                    iMaxMa: 220.0,
                    rMaxOhm: 300.0);

                if (!radialResult.Ok)
                {
                    yield return new AuditIssue
                    {
                        RuleId = "INT-ELEC-001",
                        Title = $"АЛС {line.Name}: нарушение электрических параметров",
                        Description = string.Join("; ", radialResult.Checks),
                        Severity = Severity.Error,
                        Source = "APS.Core.Electrical",
                        SourceClause = "СП 484.1311500.2020 / Паспорт оборудования",
                        SuggestedAction = "Увеличить сечение кабеля или разделить линию."
                    };
                }
            }
        }
    }
}

public sealed class DetectorRoomIntegrityRule : IProjectRule
{
    public string Id => "INT-GEO-001";
    public IEnumerable<AuditIssue> Evaluate(ProjectAnalysisContext c)
    {
        foreach (var d in c.CadModel.Detectors)
        {
            if (d.RoomId.HasValue) continue;
            yield return new AuditIssue
            {
                RuleId = Id,
                Title = $"Извещатель {d.Tag} вне распознанного помещения",
                Description = $"Координаты X={d.Position.X:F2}, Y={d.Position.Y:F2}.",
                Severity = Severity.Warning,
                Source = "DWG geometry",
                CadObjectId = d.CadObjectId,
                Location = d.Position,
                SuggestedAction = "Проверить контур помещения, XREF и положение блока."
            };
        }
    }
}

public sealed class DetectorSpacingCriteriaRule : IProjectRule
{
    public string Id => "INT-GEO-002";
    public IEnumerable<AuditIssue> Evaluate(ProjectAnalysisContext c)
    {
        var max = c.Criteria.DetectorGeometry.MaxDetectorSpacingM;
        if (!c.Criteria.DetectorGeometry.Enabled || !max.HasValue) yield break;

        foreach (var room in c.CadModel.Rooms)
        {
            var ds = c.CadModel.Detectors
                .Where(d => d.RoomId == room.Id && d.Type is DetectorType.Smoke or DetectorType.Heat or DetectorType.Flame)
                .ToList();

            for (var i = 0; i < ds.Count; i++)
            {
                for (var j = i + 1; j < ds.Count; j++)
                {
                    var p = new Point2D(ds[i].Position.X, ds[i].Position.Y);
                    var q = new Point2D(ds[j].Position.X, ds[j].Position.Y);
                    var dist = p.DistanceTo(q);

                    if (dist > max.Value)
                    {
                        yield return new AuditIssue
                        {
                            RuleId = Id,
                            Title = $"Большое расстояние между извещателями в помещении {room.Number}",
                            Description = $"{ds[i].Tag} ↔ {ds[j].Tag}: {dist:F2} м; критерий {max.Value:F2} м (СП 484 / СП РК).",
                            Severity = Severity.Warning,
                            Source = "APS.Core.Geometry",
                            SourceClause = "RulePack.DetectorGeometry.MaxDetectorSpacingM",
                            CadObjectId = ds[i].CadObjectId,
                            Location = ds[i].Position,
                            SuggestedAction = "Проверить тип извещателей, конфигурацию помещения и нормативную сетку расстановки."
                        };
                    }
                }
            }
        }
    }
}
