using System.Text.Json;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sentinel.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Sentinel.Ingest.Api"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddSingleton<IAmazonSQS>(_ =>
{
    var serviceUrl = builder.Configuration["Sentinel:SqsServiceUrl"];
    if (!string.IsNullOrWhiteSpace(serviceUrl))
    {
        var config = new AmazonSQSConfig { ServiceURL = serviceUrl };
        return new AmazonSQSClient(new BasicAWSCredentials("test", "test"), config);
    }

    return new AmazonSQSClient();
});

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Sentinel.Ingest.Api" }));

app.MapPost("/api/v1/events", async (
    IncidentEvent evt,
    IAmazonSQS sqs,
    IConfiguration config,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(evt.ServiceName) ||
        string.IsNullOrWhiteSpace(evt.ExceptionType) ||
        string.IsNullOrWhiteSpace(evt.Message))
    {
        return Results.BadRequest(new { error = "ServiceName, ExceptionType and Message are required." });
    }

    var queueUrl = config["Sentinel:SqsQueueUrl"]
        ?? throw new InvalidOperationException("Sentinel:SqsQueueUrl is not configured.");

    var normalized = evt with { OccurredAt = evt.OccurredAt ?? DateTimeOffset.UtcNow };
    var payload = JsonSerializer.Serialize(normalized);

    await sqs.SendMessageAsync(new SendMessageRequest
    {
        QueueUrl = queueUrl,
        MessageBody = payload
    }, ct);

    return Results.Accepted(value: new { status = "queued", traceId = normalized.TraceId });
});

app.Run();
