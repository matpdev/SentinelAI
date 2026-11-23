using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Sentinel.Diagnostic.Worker;
using Sentinel.Diagnostic.Worker.Diagnostics;
using Sentinel.Diagnostic.Worker.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Sentinel.Diagnostic.Worker"))
    .WithMetrics(m => m.AddMeter(SentinelMetrics.MeterName));

builder.Services.AddSingleton<SentinelMetrics>();
builder.Services
    .AddSentinelCore()
    .AddSentinelPostgresStore(builder.Configuration)
    .AddSentinelAiDiagnostic(builder.Configuration)
    .AddSentinelQueueConsumer(builder.Configuration)
    .AddSentinelNotifiers();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
