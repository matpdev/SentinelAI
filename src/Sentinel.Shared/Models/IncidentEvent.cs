namespace Sentinel.Shared.Models;

public sealed record IncidentEvent(
    string ServiceName,
    string Environment,
    string ExceptionType,
    string Message,
    string? StackTrace = null,
    string? Endpoint = null,
    string? TraceId = null,
    DateTimeOffset? OccurredAt = null);

public sealed record IncidentDiagnosis(
    string RootCauseHypothesis,
    string Severity,
    string AffectedComponent,
    string SuggestedFix);
