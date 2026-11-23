using Microsoft.Extensions.Logging;
using Sentinel.Core.Abstractions;
using Sentinel.Shared.Diagnostics;
using Sentinel.Shared.Models;

namespace Sentinel.Core.UseCases;

public sealed record ProcessIncidentResult(
    Guid IncidentId,
    bool IsNewIncident,
    int OccurrenceCount,
    IncidentDiagnosis? Diagnosis);

public sealed class ProcessIncidentUseCase(
    IPiiSanitizer piiSanitizer,
    IIncidentRepository repository,
    IIncidentDiagnosticService diagnosticService,
    IEnumerable<IIncidentNotifier> notifiers,
    ILogger<ProcessIncidentUseCase> logger)
{
    private readonly IReadOnlyList<IIncidentNotifier> _notifiers = notifiers.ToList();

    public async Task<ProcessIncidentResult> ExecuteAsync(IncidentEvent evt, CancellationToken ct)
    {
        var sanitizedMessage = piiSanitizer.Sanitize(evt.Message);
        var sanitizedStack = piiSanitizer.Sanitize(evt.StackTrace);
        var fingerprint = IncidentFingerprinter.Compute(evt);

        var upsert = await repository.UpsertIncidentAndRecordEventAsync(
            fingerprint, evt, sanitizedMessage, sanitizedStack, ct);

        if (!upsert.IsNewIncident)
        {
            logger.LogInformation(
                "Deduplicated incident {IncidentId} (occurrences: {Count})",
                upsert.IncidentId, upsert.OccurrenceCount);

            return new ProcessIncidentResult(upsert.IncidentId, false, upsert.OccurrenceCount, null);
        }

        var diagnosis = await diagnosticService.DiagnoseAsync(evt, sanitizedMessage, sanitizedStack, ct);
        await repository.SaveDiagnosisAsync(upsert.IncidentId, diagnosis, ct);

        foreach (var notifier in _notifiers)
        {
            try
            {
                await notifier.NotifyAsync(upsert.IncidentId, evt, diagnosis, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Notifier {NotifierType} failed for incident {IncidentId}",
                    notifier.GetType().Name, upsert.IncidentId);
            }
        }

        return new ProcessIncidentResult(upsert.IncidentId, true, upsert.OccurrenceCount, diagnosis);
    }
}
