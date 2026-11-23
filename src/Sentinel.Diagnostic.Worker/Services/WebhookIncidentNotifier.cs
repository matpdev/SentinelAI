using System.Net.Http.Json;
using Sentinel.Shared.Models;

namespace Sentinel.Diagnostic.Worker.Services;

public interface IIncidentNotifier
{
    Task NotifyAsync(Guid incidentId, IncidentEvent evt, IncidentDiagnosis diagnosis, CancellationToken ct);
}

public sealed class WebhookIncidentNotifier(
    HttpClient httpClient,
    IConfiguration config,
    ILogger<WebhookIncidentNotifier> logger) : IIncidentNotifier
{
    public async Task NotifyAsync(Guid incidentId, IncidentEvent evt, IncidentDiagnosis diagnosis, CancellationToken ct)
    {
        var webhookUrl = config["Sentinel:WebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            logger.LogDebug("Sentinel:WebhookUrl not configured; skipping webhook notification for {IncidentId}.", incidentId);
            return;
        }

        var summary = $"🚨 **[{diagnosis.Severity}] {evt.ServiceName} ({evt.Environment})**\n" +
                      $"**Incident:** `{incidentId}` | **Endpoint:** `{evt.Endpoint ?? "n/a"}`\n" +
                      $"**Cause:** {diagnosis.RootCauseHypothesis}\n" +
                      $"**Suggested Fix:** {diagnosis.SuggestedFix}";

        var payload = new
        {
            content = summary,
            text = summary,
            incidentId,
            service = evt.ServiceName,
            environment = evt.Environment,
            severity = diagnosis.Severity,
            diagnosis
        };

        try
        {
            using var response = await httpClient.PostAsJsonAsync(webhookUrl, payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Webhook notification returned {StatusCode} for incident {IncidentId}", response.StatusCode, incidentId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send webhook notification for incident {IncidentId}", incidentId);
        }
    }
}
