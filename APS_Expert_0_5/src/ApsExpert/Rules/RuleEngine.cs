using ApsExpert.Domain;

namespace ApsExpert.Rules;

public sealed class RuleEngine
{
    private readonly List<IAuditRule> _rules = [];

    public void Register(IAuditRule rule) => _rules.Add(rule);

    public IReadOnlyList<AuditIssue> Evaluate(EngineeringModel model)
        => _rules.SelectMany(r => r.Evaluate(model)).ToList();
}
