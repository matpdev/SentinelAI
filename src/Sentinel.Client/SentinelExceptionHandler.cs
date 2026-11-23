using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Sentinel.Shared.Models;

namespace Sentinel.Client;

/// <summary>
/// Captures unhandled ASP.NET Core exceptions non-blockingly and queues them for SentinelAI.
/// </summary>
public sealed class SentinelExceptionHandler(
    SentinelEventQueue queue,
    SentinelClientOptions options) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        var evt = new IncidentEvent(
            ServiceName: options.ServiceName,
            Environment: options.Environment,
            ExceptionType: exception.GetType().FullName ?? exception.GetType().Name,
            Message: exception.Message,
            StackTrace: exception.StackTrace,
            Endpoint: httpContext.Request.Path.Value,
            TraceId: traceId,
            OccurredAt: DateTimeOffset.UtcNow);

        queue.TryEnqueue(evt);

        // Return false so ASP.NET Core's ProblemDetails / default exception pipeline still formats the HTTP error response.
        return ValueTask.FromResult(false);
    }
}
