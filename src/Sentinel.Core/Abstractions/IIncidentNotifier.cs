using Sentinel.Shared.Models;

namespace Sentinel.Core.Abstractions;

public interface IIncidentNotifier
{
    Task NotifyAsync(Guid incidentId, IncidentEvent evt, IncidentDiagnosis diagnosis, CancellationToken ct);
}
