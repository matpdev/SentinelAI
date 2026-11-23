using Microsoft.AspNetCore.Http;
using Sentinel.Client;

namespace Sentinel.UnitTests;

public class SentinelClientTests
{
    [Fact]
    public async Task TryHandleAsync_ShouldEnqueueIncidentEventAndReturnFalse()
    {
        var queue = new SentinelEventQueue(capacity: 10);
        var options = new SentinelClientOptions
        {
            ServiceName = "Orders.Api",
            Environment = "Staging"
        };
        var handler = new SentinelExceptionHandler(queue, options);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/orders/checkout";
        httpContext.TraceIdentifier = "trace-xyz-999";

        var ex = new InvalidOperationException("Order state invalid");

        var handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        Assert.False(handled);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var evt in queue.ReadAllAsync(cts.Token))
        {
            Assert.Equal("Orders.Api", evt.ServiceName);
            Assert.Equal("Staging", evt.Environment);
            Assert.Equal("System.InvalidOperationException", evt.ExceptionType);
            Assert.Equal("Order state invalid", evt.Message);
            Assert.Equal("/api/orders/checkout", evt.Endpoint);
            Assert.False(string.IsNullOrWhiteSpace(evt.TraceId));
            break;
        }
    }
}
