using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Sentinel.Core.Abstractions;
using Sentinel.Shared.Models;

namespace Sentinel.Ingest.Api.Infrastructure;

public sealed class SqsIncidentQueuePublisher(IAmazonSQS sqs, IConfiguration config) : IIncidentQueuePublisher
{
    public async Task PublishAsync(IncidentEvent evt, CancellationToken ct)
    {
        var queueUrl = config["Sentinel:SqsQueueUrl"]
            ?? throw new InvalidOperationException("Sentinel:SqsQueueUrl is not configured.");

        var payload = JsonSerializer.Serialize(evt);
        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = payload
        }, ct);
    }
}

public sealed class InMemoryIncidentQueuePublisher(ILogger<InMemoryIncidentQueuePublisher> logger) : IIncidentQueuePublisher
{
    public Task PublishAsync(IncidentEvent evt, CancellationToken ct)
    {
        logger.LogInformation("[InMemoryQueue] Published IncidentEvent {ExceptionType} for {ServiceName} (TraceId: {TraceId})",
            evt.ExceptionType, evt.ServiceName, evt.TraceId);
        return Task.CompletedTask;
    }
}
