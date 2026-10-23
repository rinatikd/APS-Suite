using ApsExpert.Analysis;
using ApsExpert.Domain;

namespace ApsExpert.Rules.Integrated;

public interface IProjectRule
{
    string Id { get; }
    IEnumerable<AuditIssue> Evaluate(ProjectAnalysisContext context);
}

public sealed class IntegratedRuleEngine
{
    private readonly List<IProjectRule> _rules = [];
    public void Register(IProjectRule rule) => _rules.Add(rule);
    public IReadOnlyList<AuditIssue> Evaluate(ProjectAnalysisContext context)
        => _rules.SelectMany(r => r.Evaluate(context)).ToList();
}
