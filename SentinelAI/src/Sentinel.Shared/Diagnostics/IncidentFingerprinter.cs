using System.Security.Cryptography;
using System.Text;
using Sentinel.Shared.Models;

namespace Sentinel.Shared.Diagnostics;

/// <summary>
/// Computes a deterministic SHA-256 fingerprint for deduplicating recurring incidents.
/// </summary>
public static class IncidentFingerprinter
{
    public static string Compute(IncidentEvent evt)
    {
        var topFrame = ExtractTopStackFrame(evt.StackTrace);
        var rawKey = string.Join("|",
            evt.ServiceName.Trim().ToLowerInvariant(),
            evt.Environment.Trim().ToLowerInvariant(),
            evt.ExceptionType.Trim(),
            evt.Endpoint?.Trim().ToLowerInvariant() ?? string.Empty,
            topFrame);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ExtractTopStackFrame(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return string.Empty;

        var lines = stackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("at ", StringComparison.Ordinal))
                return line;
        }

        return lines.Length > 0 ? lines[0] : string.Empty;
    }
}
