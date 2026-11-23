using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sentinel.Client;

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

builder.Services.AddSentinelClient(options =>
{
    options.ServiceName = "SampleTarget.Orders.Api";
    options.Environment = builder.Environment.EnvironmentName;
    options.IngestApiUrl = builder.Configuration["Sentinel:IngestApiUrl"] ?? "http://localhost:5000";
});

var app = builder.Build();

app.UseExceptionHandler();
app.MapPrometheusScrapingEndpoint();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Sentinel.SampleTarget.Api" }));

app.MapPost("/simulate/slow-query", () =>
{
    throw new TimeoutException("PostgreSQL command timeout after 30000ms on table orders (SELECT * FROM orders JOIN items...)");
});

app.MapPost("/simulate/null-ref", () =>
{
    throw new NullReferenceException("Object reference not set to an instance of an object at CheckoutService.CalculateDiscount(Order order)");
});

app.MapPost("/simulate/external-timeout", () =>
{
    throw new HttpRequestException("PaymentGateway timed out after 5000ms for customer joao@empresa.com (CPF 123.456.789-00)");
});

app.Run();
