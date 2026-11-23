using Sentinel.Shared.Models;

namespace Sentinel.Core.Abstractions;

public interface IIncidentQueuePublisher
{
    Task PublishAsync(IncidentEvent evt, CancellationToken ct);
}

public interface IIncidentQueueConsumer
{
    Task ConsumeAsync(Func<IncidentEvent, CancellationToken, Task> handler, CancellationToken ct);
}
