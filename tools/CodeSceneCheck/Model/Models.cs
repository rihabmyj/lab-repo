namespace CodeSceneCheck.Model;

// Only the fields the checker needs; the rest of referentiel.json (recipes, examples) is for the agent.
public sealed record Referentiel(int Version, List<Rule> Rules);

public sealed record Rule(
    string Id,
    string CodesceneName,
    List<string>? Aliases,
    bool QualityGate,
    string? Metric,
    Threshold? Threshold,
    string Scope)
{
    public bool IsFileScope => string.Equals(Scope, "file", StringComparison.OrdinalIgnoreCase);

    public bool Matches(string smell) =>
        string.Equals(CodesceneName, smell, StringComparison.OrdinalIgnoreCase)
        || (Aliases ?? []).Any(a => string.Equals(a, smell, StringComparison.OrdinalIgnoreCase));
}

public sealed record Threshold(double Max);

public sealed record Report(string? ExportedAt, double? CodeHealthGoal, List<Finding> Findings);

public sealed record Finding(string Id, string File, string? Function, string Smell, int? StartLine, int? EndLine);

public sealed record Blocker(
    string? FindingId,
    string RuleId,
    string File,
    string? Function,
    string? Metric,
    double? Value,
    double? Max,
    string Status,
    string? Note = null);

public sealed record Summary(int Open, int New, int Fixed, int Unverifiable, int Missing);

public sealed record CheckResult(List<Blocker> Blockers, Summary Summary);

public static class Status
{
    public const string Open = "open";
    public const string Fixed = "fixed";
    public const string New = "new";
    public const string Unverifiable = "unverifiable";
    public const string Missing = "missing";
}

/// <summary>Invalid input (bad JSON, unknown smell or metric, unreadable file): exit code 2.</summary>
public sealed class CheckException(string message) : Exception(message);
