using ApsExpert.Domain;
using ApsExpert.Geometry;
using Autodesk.AutoCAD.DatabaseServices;

namespace ApsExpert.Rules;

public sealed class GeometryAudit
{
    public IReadOnlyList<AuditIssue> Evaluate(GeometrySnapshot g)
    {
        var issues = new List<AuditIssue>();

        if (g.UsedRoomFallback)
        {
            issues.Add(new AuditIssue
            {
                RuleId = "GEO-MODEL-001",
                Title = "Границы помещений определены эвристически",
                Description = $"На чертеже не найдено явного набора слоёв помещений; использованы {g.Rooms.Count} замкнутых полилиний-кандидатов. Результаты геометрических проверок требуют подтверждения.",
                Severity = Severity.Warning,
                Source = "APS Expert geometry parser",
                SuggestedAction = "Настроить слои помещений проекта или подтвердить найденные контуры."
            });
        }

        foreach (var d in g.Detectors)
        {
            if (d.RoomObjectId == null)
            {
                issues.Add(new AuditIssue
                {
                    RuleId = "GEO-004",
                    Title = "Извещатель не попал ни в одно помещение",
                    Description = $"{d.Tag} ({d.BlockName}) расположен в точке ({d.Position.X:F2}; {d.Position.Y:F2}), но геометрический парсер не нашёл охватывающего контура помещения.",
                    Severity = Severity.Warning,
                    Source = "APS Expert geometry parser",
                    CadObjectId = d.ObjectId,
                    Location = d.Position,
                    SuggestedAction = "Проверить границы помещения/XREF и принадлежность извещателя."
                });
            }
        }

        foreach (var room in g.Rooms)
        {
            var detectors = g.Detectors.Where(d => d.RoomObjectId == room.ObjectId && d.Type is DetectorType.Smoke or DetectorType.Heat or DetectorType.Flame).ToList();
            if (detectors.Count == 0)
            {
                issues.Add(new AuditIssue
                {
                    RuleId = "GEO-004",
                    Title = "В помещении не найден автоматический извещатель",
                    Description = $"Площадь геометрического контура: {room.AreaM2:F2} м². Это геометрический факт, а не окончательный вывод о необходимости защиты помещения.",
                    Severity = Severity.Warning,
                    Source = "APS Expert geometry parser",
                    CadObjectId = room.ObjectId,
                    Location = new Autodesk.AutoCAD.Geometry.Point3d(room.Centroid.X, room.Centroid.Y, 0),
                    SuggestedAction = "Проверить функциональное назначение помещения и применимость требований СПС."
                });
            }
        }

        foreach (var d in g.Detectors.Where(x => x.RoomObjectId != null && x.DistanceToRoomBoundaryM.HasValue))
        {
            issues.Add(new AuditIssue
            {
                RuleId = "GEO-MEASURE-001",
                Title = $"Геометрическое расстояние до границы: {d.Tag}",
                Description = $"Расстояние от точки установки до ближайшей границы распознанного помещения: {d.DistanceToRoomBoundaryM!.Value:F3} м. Нормативная оценка будет выполнена отдельным Rule Pack.",
                Severity = Severity.Info,
                Source = "APS Expert geometry parser",
                CadObjectId = d.ObjectId,
                Location = d.Position,
                SuggestedAction = "Использовать значение при нормативной проверке размещения."
            });
        }

        var detectors = g.Detectors.Where(d => d.Type is DetectorType.Smoke or DetectorType.Heat or DetectorType.Flame).ToList();
        for (var i = 0; i < detectors.Count; i++)
        for (var j = i + 1; j < detectors.Count; j++)
        {
            if (detectors[i].RoomObjectId != detectors[j].RoomObjectId || detectors[i].RoomObjectId == null) continue;
            var a = detectors[i].Position;
            var b = detectors[j].Position;
            var dist = a.DistanceTo(b);
            if (dist > 0)
            {
                issues.Add(new AuditIssue
                {
                    RuleId = "GEO-MEASURE-002",
                    Title = $"Между извещателями {detectors[i].Tag} и {detectors[j].Tag}: {dist:F3} м",
                    Description = "Фактическое геометрическое расстояние внутри одного распознанного помещения. Порог допустимого расстояния намеренно не задан до загрузки конкретного нормативного правила.",
                    Severity = Severity.Info,
                    Source = "APS Expert geometry parser",
                    SuggestedAction = "Проверить расстояние по применимому типу извещателя и условиям помещения."
                });
            }
        }

        return issues;
    }
}
