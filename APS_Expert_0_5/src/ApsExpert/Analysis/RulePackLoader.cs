using System.Text.Json;

namespace ApsExpert.Analysis;

public static class RulePackLoader
{
    public static RulePack Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new RulePack();
        try
        {
            return JsonSerializer.Deserialize<RulePack>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new RulePack();
        }
        catch { return new RulePack(); }
    }
}
