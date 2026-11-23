using System.Text.RegularExpressions;
using Sentinel.Core.Abstractions;

namespace Sentinel.Core.Security;

public sealed class CompositePiiSanitizer(IEnumerable<IPiiRedactionRule> rules) : IPiiSanitizer
{
    private readonly IReadOnlyList<IPiiRedactionRule> _rules = rules.ToList();

    public string Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var current = input;
        foreach (var rule in _rules)
        {
            current = rule.Apply(current);
        }

        return current;
    }
}

public sealed partial class BearerTokenRedactionRule : IPiiRedactionRule
{
    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();

    public string Apply(string input) => Pattern().Replace(input, "Bearer [REDACTED]");
}

public sealed partial class CpfRedactionRule : IPiiRedactionRule
{
    [GeneratedRegex(@"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b")]
    private static partial Regex Pattern();

    public string Apply(string input) => Pattern().Replace(input, "[CPF_REDACTED]");
}

public sealed partial class EmailRedactionRule : IPiiRedactionRule
{
    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b")]
    private static partial Regex Pattern();

    public string Apply(string input) => Pattern().Replace(input, "[EMAIL_REDACTED]");
}

public sealed partial class ConnectionPasswordRedactionRule : IPiiRedactionRule
{
    [GeneratedRegex(@"(Password|Pwd)\s*=\s*[^;]+", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();

    public string Apply(string input) => Pattern().Replace(input, "$1=[REDACTED]");
}
