using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using ApsExpert.Analysis;
using ApsExpert.Domain;
using ApsExpert.Geometry;
using ApsExpert.Reference;
using ApsExpert.Rules;
using ApsExpert.Rules.Integrated;
using ApsExpert.Services;
using System.Text.Json;

namespace ApsExpert.Commands;

public sealed class ApsExpertCommandSet
{
    private static EngineeringModel? _model;
    private static IReadOnlyList<AuditIssue> _issues = [];
    private static RubezhProjectSnapshot? _rubezh;
    private static RubezhCadMappingResult? _mapping;

    [CommandMethod("APS_SCAN")]
    public void Scan()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var geometry = new CadGeometryExtractor().Extract();
        _model = new UnifiedModelBuilder().Build(geometry, _mapping);
        doc.Editor.WriteMessage($"\nAPS Expert: помещений {_model.Rooms.Count}, извещателей {_model.Detectors.Count}, ППКП {_model.Panels.Count}.\n");
    }

    [CommandMethod("APS_PROJECT_AUDIT")]
    public void ProjectAudit()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var geometry = new CadGeometryExtractor().Extract();
        _rubezh = new RubezhProjectReader().TryReadForDwg(doc.Database.Filename);
        _mapping = _rubezh == null ? null : new RubezhCadMapper().Map(doc.Database, _rubezh);
        _model = new UnifiedModelBuilder().Build(geometry, _mapping);

        var criteriaPath = Path.Combine(AppContext.BaseDirectory, "RulePack", "APS_Expert_Design_Criteria_0_5.json");
        var criteria = RulePackLoader.Load(File.Exists(criteriaPath) ? criteriaPath : null);
        var context = new ProjectAnalysisContext { CadModel = _model, Rubezh = _rubezh, Mapping = _mapping, Criteria = criteria };

        var engine = new IntegratedRuleEngine();
        engine.Register(new MappingIntegrityRule());
        engine.Register(new AddressLineIntegrityRule());
        engine.Register(new DetectorRoomIntegrityRule());
        engine.Register(new DetectorSpacingCriteriaRule());
        _issues = engine.Evaluate(context);

        // Keep the legacy model sanity rules in the same audit.
        _issues = _issues.Concat(new RuleEngineWithLegacy().Evaluate(_model)).ToList();

        ed.WriteMessage($"\nAPS Expert 0.5 PROJECT AUDIT\n");
        ed.WriteMessage($"DWG: {doc.Name}\n");
        ed.WriteMessage($"R-CAD: {(_rubezh == null ? "не найден" : $"{_rubezh.Equipment.Count} элементов / {_rubezh.Lines.Count} линий")}\n");
        if (_mapping != null) ed.WriteMessage($"Mapping: {_mapping.Mapped.Count}/{_mapping.Mapped.Count + _mapping.Unmapped.Count} ({_mapping.MatchRatio:P1})\n");
        ed.WriteMessage($"Rooms: {_model.Rooms.Count}; detectors: {_model.Detectors.Count}; results: {_issues.Count}\n");
        foreach (var group in _issues.GroupBy(x => x.Severity)) ed.WriteMessage($"  {group.Key}: {group.Count()}\n");
    }

    [CommandMethod("APS_AUDIT")]
    public void Audit() => ProjectAudit();

    [CommandMethod("APS_MAP_RUBEZH")]
    public void MapRubezh()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var snapshot = new RubezhProjectReader().TryReadForDwg(doc.Database.Filename);
        if (snapshot == null) { doc.Editor.WriteMessage("\nAPS Expert: рядом с DWG не найден проект R-CAD/RubezhCAD.\n"); return; }
        _rubezh = snapshot;
        _mapping = new RubezhCadMapper().Map(doc.Database, snapshot);
        _issues = new RubezhCadMappingRule().Evaluate(_mapping);
        doc.Editor.WriteMessage($"\nAPS Expert MAP: {_mapping.Mapped.Count} сопоставлено, {_mapping.Unmapped.Count} не сопоставлено ({_mapping.MatchRatio:P1}).\n");
    }

    [CommandMethod("APS_GEOMETRY_AUDIT")]
    public void GeometryAuditCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var g = new CadGeometryExtractor().Extract();
        var issues = new GeometryAudit().Evaluate(g);
        _issues = issues;
        doc.Editor.WriteMessage($"\nAPS Expert GEO: контуров помещений {g.Rooms.Count}, извещателей {g.Detectors.Count}, результатов {issues.Count}.\n");
        foreach (var i in issues.Take(40)) doc.Editor.WriteMessage($"[{i.Severity}] {i.RuleId}: {i.Title}\n");
    }

    [CommandMethod("APS_ISSUES")]
    public void Issues()
    {
        var ed = Application.DocumentManager.MdiActiveDocument.Editor;
        if (_issues.Count == 0) { ed.WriteMessage("\nAPS Expert: замечаний нет или аудит ещё не выполнен.\n"); return; }
        foreach (var i in _issues)
            ed.WriteMessage($"\n{i.RuleId} | {i.Severity} | {i.Title}\n{i.Description}\nИсточник: {i.Source}\nОснование: {i.SourceClause ?? "—"}\nРекомендация: {i.SuggestedAction ?? "—"}\n");
    }

    [CommandMethod("APS_MARK")]
    public void Mark() => Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nAPS Expert: visual QA layer подготовлен архитектурно; применение маркеров будет отдельным транзакционным этапом.\n");

    [CommandMethod("APS_FIX")]
    public void Fix() => Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nAPS Expert: автоматическое исправление отключено до появления ProposedChange + Preview + Rollback.\n");

    [CommandMethod("APS_EXPORT")]
    public void Export()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var path = Path.ChangeExtension(doc.Database.Filename, ".aps-expert.json");
        var report = new { Version = "0.5", Drawing = doc.Name, Rubezh = _rubezh, Mapping = _mapping, Model = _model, Issues = _issues };
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        doc.Editor.WriteMessage($"\nAPS Expert: интегрированный отчёт сохранён: {path}\n");
    }
}

internal sealed class RuleEngineWithLegacy
{
    public IReadOnlyList<AuditIssue> Evaluate(EngineeringModel model)
    {
        var engine = new RuleEngine();
        engine.Register(new DuplicateDetectorTagRule());
        return engine.Evaluate(model);
    }
}
