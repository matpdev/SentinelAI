using System.Diagnostics.Metrics;

namespace Sentinel.Diagnostic.Worker.Diagnostics;

public sealed class SentinelMetrics
{
    public const string MeterName = "Sentinel.Diagnostics";

    private readonly Counter<long> _incidentsCreated;
    private readonly Counter<long> _incidentsDeduplicated;
    private readonly Histogram<double> _diagnosisDurationMs;

    public SentinelMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _incidentsCreated = meter.CreateCounter<long>("sentinel_incidents_created_total");
        _incidentsDeduplicated = meter.CreateCounter<long>("sentinel_incidents_deduplicated_total");
        _diagnosisDurationMs = meter.CreateHistogram<double>("sentinel_diagnosis_duration_ms", "ms");
    }

    public void RecordNewIncident(string serviceName, string severity) =>
        _incidentsCreated.Add(1, new KeyValuePair<string, object?>("service", serviceName),
            new KeyValuePair<string, object?>("severity", severity));

    public void RecordDeduplicatedIncident(string serviceName) =>
        _incidentsDeduplicated.Add(1, new KeyValuePair<string, object?>("service", serviceName));

    public void RecordDiagnosisDuration(double elapsedMs, string serviceName) =>
        _diagnosisDurationMs.Record(elapsedMs, new KeyValuePair<string, object?>("service", serviceName));
}
