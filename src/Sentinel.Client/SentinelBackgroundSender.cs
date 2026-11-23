using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sentinel.Client;

public sealed class SentinelBackgroundSender(
    SentinelEventQueue queue,
    IHttpClientFactory httpClientFactory,
    ILogger<SentinelBackgroundSender> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = httpClientFactory.CreateClient(SentinelClientExtensions.HttpClientName);

        await foreach (var evt in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var response = await client.PostAsJsonAsync("/api/v1/events", evt, stoppingToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Sentinel.Ingest.Api returned {StatusCode} for TraceId {TraceId}", response.StatusCode, evt.TraceId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to ship IncidentEvent for TraceId {TraceId}", evt.TraceId);
            }
        }
    }
}
