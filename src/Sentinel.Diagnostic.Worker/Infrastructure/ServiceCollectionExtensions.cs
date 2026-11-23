using Amazon.Runtime;
using Amazon.SQS;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Sentinel.Core.Abstractions;
using Sentinel.Core.Security;
using Sentinel.Core.UseCases;
using Sentinel.Diagnostic.Worker.Data;
using Sentinel.Diagnostic.Worker.Services;

namespace Sentinel.Diagnostic.Worker.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSentinelCore(this IServiceCollection services)
    {
        services.AddSingleton<IPiiRedactionRule, BearerTokenRedactionRule>();
        services.AddSingleton<IPiiRedactionRule, CpfRedactionRule>();
        services.AddSingleton<IPiiRedactionRule, EmailRedactionRule>();
        services.AddSingleton<IPiiRedactionRule, ConnectionPasswordRedactionRule>();
        services.AddSingleton<IPiiSanitizer, CompositePiiSanitizer>();
        services.AddSingleton<ProcessIncidentUseCase>();
        return services;
    }

    public static IServiceCollection AddSentinelPostgresStore(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddSingleton<IIncidentRepository>(new IncidentRepository(connectionString));
        return services;
    }

    public static IServiceCollection AddSentinelAiDiagnostic(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<FallbackHeuristicDiagnosticService>();

        var provider = config["Sentinel:Ai:Provider"] ?? "Ollama";
        if (provider.Equals("Heuristic", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IIncidentDiagnosticService>(sp =>
                sp.GetRequiredService<FallbackHeuristicDiagnosticService>());
            return services;
        }

        var ollamaEndpoint = config["Sentinel:Ai:OllamaEndpoint"] ?? "http://localhost:11434";
        var ollamaModel = config["Sentinel:Ai:Model"] ?? "llama3.2";

        services.AddSingleton<IChatClient>(_ => new OllamaApiClient(new Uri(ollamaEndpoint), ollamaModel));
        services.AddSingleton<IIncidentDiagnosticService, AiIncidentDiagnosticService>();
        return services;
    }

    public static IServiceCollection AddSentinelQueueConsumer(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Sentinel:Queue:Provider"] ?? "Sqs";
        if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IIncidentQueueConsumer, InMemoryIncidentQueueConsumer>();
            return services;
        }

        services.AddSingleton<IAmazonSQS>(_ =>
        {
            var serviceUrl = config["Sentinel:SqsServiceUrl"];
            if (!string.IsNullOrWhiteSpace(serviceUrl))
            {
                var sqsConfig = new AmazonSQSConfig { ServiceURL = serviceUrl };
                return new AmazonSQSClient(new BasicAWSCredentials("test", "test"), sqsConfig);
            }

            return new AmazonSQSClient();
        });
        services.AddSingleton<IIncidentQueueConsumer, SqsIncidentQueueConsumer>();
        return services;
    }

    public static IServiceCollection AddSentinelNotifiers(this IServiceCollection services)
    {
        services.AddSingleton<IIncidentNotifier, LoggingIncidentNotifier>();
        services.AddHttpClient<WebhookIncidentNotifier>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<IIncidentNotifier>(sp => sp.GetRequiredService<WebhookIncidentNotifier>());
        return services;
    }
}
