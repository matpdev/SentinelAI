using Sentinel.Shared.Models;

namespace Sentinel.Diagnostic.Worker.Services;

public interface IIncidentDiagnosticService
{
    Task<IncidentDiagnosis> DiagnoseAsync(
        IncidentEvent evt,
        string sanitizedMessage,
        string? sanitizedStackTrace,
        CancellationToken ct);
}

/// <summary>
/// Deterministic baseline diagnostic service.
/// Phase 2 replaces/wraps this with Microsoft.Extensions.AI (Ollama / OpenAI) + Polly fallback.
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
