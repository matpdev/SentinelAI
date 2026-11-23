using Amazon.Runtime;
using Amazon.SQS;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Sentinel.Diagnostic.Worker;
using Sentinel.Diagnostic.Worker.Data;
using Sentinel.Diagnostic.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

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

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

builder.Services.AddSingleton(new IncidentRepository(connectionString));
builder.Services.AddSingleton<FallbackHeuristicDiagnosticService>();

var ollamaEndpoint = builder.Configuration["Sentinel:Ai:OllamaEndpoint"] ?? "http://localhost:11434";
var ollamaModel = builder.Configuration["Sentinel:Ai:Model"] ?? "llama3.2";

builder.Services.AddSingleton<IChatClient>(_ => new OllamaApiClient(new Uri(ollamaEndpoint), ollamaModel));
builder.Services.AddSingleton<IIncidentDiagnosticService, AiIncidentDiagnosticService>();

builder.Services.AddHttpClient<IIncidentNotifier, WebhookIncidentNotifier>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
