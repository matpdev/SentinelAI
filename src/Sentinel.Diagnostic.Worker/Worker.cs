using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Sentinel.Diagnostic.Worker.Data;
using Sentinel.Diagnostic.Worker.Services;
using Sentinel.Shared.Diagnostics;
using Sentinel.Shared.Models;
using Sentinel.Shared.Security;

namespace Sentinel.Diagnostic.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    IAmazonSQS sqs,
    IConfiguration config,
    IncidentRepository repository,
    IIncidentDiagnosticService diagnosticService,
    IIncidentNotifier notifier) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = config["Sentinel:SqsQueueUrl"]
            ?? throw new InvalidOperationException("Sentinel:SqsQueueUrl is not configured.");

        logger.LogInformation("Sentinel.Diagnostic.Worker listening on {QueueUrl}", queueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 5,
                    WaitTimeSeconds = 10
                }, stoppingToken);

                foreach (var message in response.Messages ?? [])
                {
                    await ProcessMessageAsync(queueUrl, message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error polling SQS queue. Retrying in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(string queueUrl, Message message, CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<IncidentEvent>(message.Body);
        if (evt is null)
        {
            logger.LogWarning("Discarding unparseable SQS message {MessageId}", message.MessageId);
            await sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, ct);
            return;
        }

        var sanitizedMessage = PiiSanitizer.Sanitize(evt.Message);
        var sanitizedStack = PiiSanitizer.Sanitize(evt.StackTrace);
        var fingerprint = IncidentFingerprinter.Compute(evt);

        var upsert = await repository.UpsertIncidentAndRecordEventAsync(
            fingerprint, evt, sanitizedMessage, sanitizedStack, ct);

        if (upsert.IsNewIncident)
        {
            var diagnosis = await diagnosticService.DiagnoseAsync(evt, sanitizedMessage, sanitizedStack, ct);
            await repository.SaveDiagnosisAsync(upsert.IncidentId, diagnosis, ct);
            await notifier.NotifyAsync(upsert.IncidentId, evt, diagnosis, ct);

            logger.LogWarning(
                "New incident {IncidentId} [{Severity}] in {ServiceName}: {Hypothesis}",
                upsert.IncidentId, diagnosis.Severity, evt.ServiceName, diagnosis.RootCauseHypothesis);
        }
        else
        {
            logger.LogInformation(
                "Deduplicated incident {IncidentId} (occurrences: {Count})",
                upsert.IncidentId, upsert.OccurrenceCount);
        }

        await sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, ct);
    }
}
