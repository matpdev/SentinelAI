using System.Text.Json;
using Microsoft.Extensions.AI;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Sentinel.Core.Abstractions;
using Sentinel.Shared.Models;

namespace Sentinel.Diagnostic.Worker.Services;

public sealed class AiIncidentDiagnosticService : IIncidentDiagnosticService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IChatClient _chatClient;
    private readonly FallbackHeuristicDiagnosticService _fallback;
    private readonly ILogger<AiIncidentDiagnosticService> _logger;
    private readonly ResiliencePipeline<IncidentDiagnosis> _pipeline;

    public AiIncidentDiagnosticService(
        IChatClient chatClient,
        FallbackHeuristicDiagnosticService fallback,
        ILogger<AiIncidentDiagnosticService> logger)
    {
        _chatClient = chatClient;
        _fallback = fallback;
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder<IncidentDiagnosis>()
            .AddTimeout(TimeSpan.FromSeconds(20))
            .AddRetry(new Polly.Retry.RetryStrategyOptions<IncidentDiagnosis>
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.FromMilliseconds(300),
                ShouldHandle = new PredicateBuilder<IncidentDiagnosis>().Handle<Exception>()
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<IncidentDiagnosis>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 4,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<IncidentDiagnosis>().Handle<Exception>()
            })
            .Build();
    }

    public async Task<IncidentDiagnosis> DiagnoseAsync(
        IncidentEvent evt,
        string sanitizedMessage,
        string? sanitizedStackTrace,
        CancellationToken ct)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async token => await CallLlmAsync(evt, sanitizedMessage, sanitizedStackTrace, token),
                ct);
        }
        catch (Exception ex) when (ex is BrokenCircuitException or TimeoutRejectedException or Exception)
        {
            _logger.LogWarning(ex, "AI diagnosis failed or circuit is open. Using deterministic fallback.");
            return await _fallback.DiagnoseAsync(evt, sanitizedMessage, sanitizedStackTrace, ct);
        }
    }

    private async Task<IncidentDiagnosis> CallLlmAsync(
        IncidentEvent evt,
        string sanitizedMessage,
        string? sanitizedStackTrace,
        CancellationToken ct)
    {
        var prompt = $$"""
            You are a senior SRE / .NET incident responder. Analyze this production error and respond ONLY with a valid JSON object matching:
            {
              "rootCauseHypothesis": "concise root cause",
              "severity": "Low | Medium | High | Critical",
              "affectedComponent": "service or dependency",
              "suggestedFix": "actionable next step"
            }

            Service: {{evt.ServiceName}}
            Environment: {{evt.Environment}}
            Endpoint: {{evt.Endpoint ?? "unknown"}}
            Exception: {{evt.ExceptionType}}
            Message: {{sanitizedMessage}}
            StackTrace: {{sanitizedStackTrace ?? "none"}}
            """;

        var response = await _chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)],
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
            ct);

        var rawJson = StripMarkdownFences(response.Text);
        var diagnosis = JsonSerializer.Deserialize<IncidentDiagnosis>(rawJson, JsonOptions);

        return diagnosis is not null && !string.IsNullOrWhiteSpace(diagnosis.RootCauseHypothesis)
            ? diagnosis
            : throw new InvalidOperationException("LLM returned an empty or invalid IncidentDiagnosis JSON.");
    }

    private static string StripMarkdownFences(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```"))
                trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }
}
