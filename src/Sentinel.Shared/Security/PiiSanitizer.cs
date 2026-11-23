using System.Text.RegularExpressions;

namespace Sentinel.Shared.Security;

/// <summary>
/// Masks sensitive data (tokens, CPFs, emails, connection passwords) before logs are persisted or sent to an LLM.
/// </summary>
public static partial class PiiSanitizer
{
    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b")]
    private static partial Regex CpfRegex();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(Password|Pwd)\s*=\s*[^;]+", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionPasswordRegex();

    public static string Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var result = BearerTokenRegex().Replace(input, "Bearer [REDACTED]");
        result = CpfRegex().Replace(result, "[CPF_REDACTED]");
        result = EmailRegex().Replace(result, "[EMAIL_REDACTED]");
        result = ConnectionPasswordRegex().Replace(result, "$1=[REDACTED]");

        return result;
    }
}
