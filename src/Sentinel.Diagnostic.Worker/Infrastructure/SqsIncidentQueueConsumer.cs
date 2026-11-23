using System.Text.Json;
using System.Threading.Channels;
using Amazon.SQS;
using Amazon.SQS.Model;
using Sentinel.Core.Abstractions;
using Sentinel.Shared.Models;

namespace Sentinel.Diagnostic.Worker.Infrastructure;

public sealed class SqsIncidentQueueConsumer(
    IAmazonSQS sqs,
    IConfiguration config,
    ILogger<SqsIncidentQueueConsumer> logger) : IIncidentQueueConsumer
{
    public async Task ConsumeAsync(Func<IncidentEvent, CancellationToken, Task> handler, CancellationToken ct)
    {
        var queueUrl = config["Sentinel:SqsQueueUrl"]
            ?? throw new InvalidOperationException("Sentinel:SqsQueueUrl is not configured.");

        logger.LogInformation("SqsIncidentQueueConsumer listening on {QueueUrl}", queueUrl);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 5,
                    WaitTimeSeconds = 10
                }, ct);

                foreach (var message in response.Messages ?? [])
                {
                    var evt = JsonSerializer.Deserialize<IncidentEvent>(message.Body);
                    if (evt is null)
                    {
                        logger.LogWarning("Discarding unparseable SQS message {MessageId}", message.MessageId);
                        await sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, ct);
                        continue;
                    }

                    await handler(evt, ct);
                    await sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error polling SQS queue. Retrying in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}

public sealed class InMemoryIncidentQueueConsumer(ILogger<InMemoryIncidentQueueConsumer> logger) : IIncidentQueueConsumer
{
    private readonly Channel<IncidentEvent> _channel = Channel.CreateUnbounded<IncidentEvent>();

    public ValueTask EnqueueAsync(IncidentEvent evt, CancellationToken ct) => _channel.Writer.WriteAsync(evt, ct);

    public async Task ConsumeAsync(Func<IncidentEvent, CancellationToken, Task> handler, CancellationToken ct)
    {
        logger.LogInformation("InMemoryIncidentQueueConsumer listening...");
        await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
        {
            await handler(evt, ct);
        }
    }
}
