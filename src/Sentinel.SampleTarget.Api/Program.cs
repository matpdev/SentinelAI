using System.Diagnostics;
using System.Net.Http.Json;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sentinel.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Sentinel.SampleTarget.Api"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());

var ingestUrl = builder.Configuration["Sentinel:IngestApiUrl"] ?? "http://localhost:5000";
builder.Services.AddHttpClient("SentinelIngest", client =>
{
    client.BaseAddress = new Uri(ingestUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Sentinel.SampleTarget.Api" }));

app.MapPost("/simulate/slow-query", async (IHttpClientFactory httpFactory, ILogger<Program> logger, CancellationToken ct) =>
{
    try
    {
        throw new TimeoutException("PostgreSQL command timeout after 30000ms on table orders (SELECT * FROM orders JOIN items...)");
    }
    catch (Exception ex)
    {
        return await ReportAndFailAsync(httpFactory, logger, ex, "/simulate/slow-query", StatusCodes.Status500InternalServerError, ct);
    }
});

app.MapPost("/simulate/null-ref", async (IHttpClientFactory httpFactory, ILogger<Program> logger, CancellationToken ct) =>
{
    try
    {
        throw new NullReferenceException("Object reference not set to an instance of an object at CheckoutService.CalculateDiscount(Order order)");
    }
    catch (Exception ex)
    {
        return await ReportAndFailAsync(httpFactory, logger, ex, "/simulate/null-ref", StatusCodes.Status500InternalServerError, ct);
    }
});

app.MapPost("/simulate/external-timeout", async (IHttpClientFactory httpFactory, ILogger<Program> logger, CancellationToken ct) =>
{
    try
    {
        throw new HttpRequestException("PaymentGateway timed out after 5000ms for customer joao@empresa.com (CPF 123.456.789-00)");
    }
    catch (Exception ex)
    {
        return await ReportAndFailAsync(httpFactory, logger, ex, "/simulate/external-timeout", StatusCodes.Status502BadGateway, ct);
    }
});

app.Run();

static async Task<IResult> ReportAndFailAsync(
    IHttpClientFactory httpFactory,
    ILogger logger,
    Exception ex,
    string endpoint,
    int statusCode,
    CancellationToken ct)
{
    var traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    var evt = new IncidentEvent(
        ServiceName: "SampleTarget.Orders.Api",
        Environment: "Production",
        ExceptionType: ex.GetType().FullName ?? ex.GetType().Name,
        Message: ex.Message,
        StackTrace: ex.StackTrace,
        Endpoint: endpoint,
        TraceId: traceId,
        OccurredAt: DateTimeOffset.UtcNow);

    try
    {
        var client = httpFactory.CreateClient("SentinelIngest");
        await client.PostAsJsonAsync("/api/v1/events", evt, ct);
    }
    catch (Exception reportEx)
    {
        logger.LogWarning(reportEx, "Failed to report incident to Sentinel.Ingest.Api");
    }

    return Results.Json(new { error = ex.Message, traceId, incidentReported = true }, statusCode: statusCode);
}
