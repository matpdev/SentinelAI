using Sentinel.Shared.Models;

namespace Sentinel.Core.Abstractions;

public interface IIncidentDiagnosticService
{
    Task<IncidentDiagnosis> DiagnoseAsync(
        IncidentEvent evt,
        string sanitizedMessage,
        string? sanitizedStackTrace,
        CancellationToken ct);
}
