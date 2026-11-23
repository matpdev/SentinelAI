using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sentinel.Diagnostic.Worker.Services;
using Sentinel.Shared.Models;

namespace Sentinel.UnitTests;

public class WebhookIncidentNotifierTests
{
    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastBody = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;
            return new HttpResponseMessage(statusCode);
        }
    }

    [Fact]
    public async Task NotifyAsync_ShouldSkipHttpCall_WhenWebhookUrlIsNotConfigured()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sentinel:WebhookUrl"] = ""
        }).Build();

        var sut = new WebhookIncidentNotifier(new HttpClient(handler), config, NullLogger<WebhookIncidentNotifier>.Instance);
        var evt = new IncidentEvent("Orders.Api", "Prod", "Exception", "boom");
        var diag = new IncidentDiagnosis("cause", "High", "Orders.Api", "fix");

        await sut.NotifyAsync(Guid.NewGuid(), evt, diag, CancellationToken.None);

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task NotifyAsync_ShouldPostFormattedPayload_WhenWebhookUrlIsConfigured()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.NoContent);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sentinel:WebhookUrl"] = "https://discord.example/api/webhooks/123"
        }).Build();

        var sut = new WebhookIncidentNotifier(new HttpClient(handler), config, NullLogger<WebhookIncidentNotifier>.Instance);
        var evt = new IncidentEvent("Orders.Api", "Prod", "Exception", "boom", Endpoint: "/api/orders");
        var diag = new IncidentDiagnosis("DB timeout", "High", "Orders.Api", "Check indexes");

        await sut.NotifyAsync(Guid.NewGuid(), evt, diag, CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
        Assert.Contains("DB timeout", handler.LastBody);
        Assert.Contains("Orders.Api", handler.LastBody);
    }
}
