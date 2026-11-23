using Amazon.Runtime;
using Amazon.SQS;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sentinel.Core.Abstractions;
using Sentinel.Ingest.Api.Infrastructure;
using Sentinel.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Sentinel.Ingest.Api"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

var queueProvider = builder.Configuration["Sentinel:Queue:Provider"] ?? "Sqs";
if (queueProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IIncidentQueuePublisher, InMemoryIncidentQueuePublisher>();
}
else
{
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
    builder.Services.AddSingleton<IIncidentQueuePublisher, SqsIncidentQueuePublisher>();
}

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Sentinel.Ingest.Api", queueProvider }));

app.MapPost("/api/v1/events", async (
    IncidentEvent evt,
    IIncidentQueuePublisher publisher,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(evt.ServiceName) ||
        string.IsNullOrWhiteSpace(evt.ExceptionType) ||
        string.IsNullOrWhiteSpace(evt.Message))
    {
        return Results.BadRequest(new { error = "ServiceName, ExceptionType and Message are required." });
    }

    var normalized = evt with { OccurredAt = evt.OccurredAt ?? DateTimeOffset.UtcNow };
    await publisher.PublishAsync(normalized, ct);

    return Results.Accepted(value: new { status = "queued", traceId = normalized.TraceId });
});

app.Run();
