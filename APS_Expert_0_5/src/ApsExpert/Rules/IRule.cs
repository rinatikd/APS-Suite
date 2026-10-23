using ApsExpert.Domain;

namespace ApsExpert.Rules;

public interface IAuditRule
{
    string Id { get; }
    string Name { get; }
    IEnumerable<AuditIssue> Evaluate(EngineeringModel model);
}
