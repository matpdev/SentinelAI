using Sentinel.Shared.Models;

namespace Sentinel.Core.Abstractions;

public sealed record UpsertIncidentResult(Guid IncidentId, bool IsNewIncident, int OccurrenceCount);

public interface IIncidentRepository
{
    Task<UpsertIncidentResult> UpsertIncidentAndRecordEventAsync(
        string fingerprint,
        IncidentEvent evt,
        string sanitizedMessage,
        string? sanitizedStackTrace,
        CancellationToken ct);

    Task SaveDiagnosisAsync(Guid incidentId, IncidentDiagnosis diagnosis, CancellationToken ct);
}
