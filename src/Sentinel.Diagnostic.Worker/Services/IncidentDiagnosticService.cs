using Sentinel.Core.Abstractions;
using Sentinel.Shared.Models;

namespace Sentinel.Diagnostic.Worker.Services;

/// <summary>
/// Deterministic baseline diagnostic service used as fallback or when AI provider is set to Heuristic.
/// </summary>
public sealed class FallbackHeuristicDiagnosticService : IIncidentDiagnosticService
{
    public Task<IncidentDiagnosis> DiagnoseAsync(
        IncidentEvent evt,
        string sanitizedMessage,
        string? sanitizedStackTrace,
        CancellationToken ct)
    {
        var severity = evt.ExceptionType.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
                       evt.ExceptionType.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
            ? "High"
            : "Medium";

        var diagnosis = new IncidentDiagnosis(
            RootCauseHypothesis: $"Unhandled {evt.ExceptionType} in {evt.ServiceName} ({evt.Endpoint ?? "unknown endpoint"}): {sanitizedMessage}",
            Severity: severity,
            AffectedComponent: evt.ServiceName,
            SuggestedFix: "Inspect correlated TraceId logs and verify downstream dependency health or query execution plan.");

        return Task.FromResult(diagnosis);
    }
}
