using System.Diagnostics;
using Sentinel.Core.Abstractions;
using Sentinel.Core.UseCases;
using Sentinel.Diagnostic.Worker.Diagnostics;

namespace Sentinel.Diagnostic.Worker;

public sealed class Worker(
    IIncidentQueueConsumer consumer,
    ProcessIncidentUseCase useCase,
    SentinelMetrics metrics) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync(async (evt, ct) =>
        {
            var sw = Stopwatch.StartNew();
            var result = await useCase.ExecuteAsync(evt, ct);
            sw.Stop();

            if (result.IsNewIncident && result.Diagnosis is not null)
            {
                metrics.RecordDiagnosisDuration(sw.Elapsed.TotalMilliseconds, evt.ServiceName);
                metrics.RecordNewIncident(evt.ServiceName, result.Diagnosis.Severity);
            }
            else
            {
                metrics.RecordDeduplicatedIncident(evt.ServiceName);
            }
        }, stoppingToken);
}
