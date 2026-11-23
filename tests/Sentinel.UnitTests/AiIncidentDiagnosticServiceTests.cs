using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sentinel.Diagnostic.Worker.Services;
using Sentinel.Shared.Models;

namespace Sentinel.UnitTests;

public class AiIncidentDiagnosticServiceTests
{
    private static readonly IncidentEvent SampleEvent = new(
        ServiceName: "Orders.Api",
        Environment: "Production",
        ExceptionType: "Npgsql.NpgsqlException",
        Message: "Connection pool exhausted",
        Endpoint: "/api/orders");

    [Fact]
    public async Task DiagnoseAsync_ShouldParseStructuredJson_WhenLlmReturnsMarkdownFencedJson()
    {
        var chatClient = Substitute.For<IChatClient>();
        var json = """
            ```json
            {
              "rootCauseHypothesis": "PostgreSQL connection pool exhausted under high concurrency.",
              "severity": "Critical",
              "affectedComponent": "Orders.Api -> PostgreSQL",
              "suggestedFix": "Increase MaxPoolSize or optimize slow queries."
            }
            ```
            """;

        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));

        var sut = new AiIncidentDiagnosticService(
            chatClient,
            new FallbackHeuristicDiagnosticService(),
            NullLogger<AiIncidentDiagnosticService>.Instance);

        var result = await sut.DiagnoseAsync(SampleEvent, SampleEvent.Message, null, CancellationToken.None);

        Assert.Equal("Critical", result.Severity);
        Assert.Equal("Orders.Api -> PostgreSQL", result.AffectedComponent);
        Assert.Contains("connection pool", result.RootCauseHypothesis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiagnoseAsync_ShouldFallbackToDeterministicDiagnosis_WhenLlmThrows()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Ollama unreachable"));

        var sut = new AiIncidentDiagnosticService(
            chatClient,
            new FallbackHeuristicDiagnosticService(),
            NullLogger<AiIncidentDiagnosticService>.Instance);

        var result = await sut.DiagnoseAsync(SampleEvent, SampleEvent.Message, null, CancellationToken.None);

        Assert.Equal("High", result.Severity);
        Assert.Equal("Orders.Api", result.AffectedComponent);
        Assert.Contains("Npgsql.NpgsqlException", result.RootCauseHypothesis);
    }
}
