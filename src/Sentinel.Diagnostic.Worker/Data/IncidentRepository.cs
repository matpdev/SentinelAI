using System.Text.Json;
using Dapper;
using Npgsql;
using Sentinel.Shared.Models;

namespace Sentinel.Diagnostic.Worker.Data;

public sealed record UpsertIncidentResult(Guid IncidentId, bool IsNewIncident, int OccurrenceCount);

public sealed class IncidentRepository(string connectionString)
{
    public async Task<UpsertIncidentResult> UpsertIncidentAndRecordEventAsync(
        string fingerprint,
        IncidentEvent evt,
        string sanitizedMessage,
        string? sanitizedStackTrace,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string findOpenSql = """
            SELECT id, occurrence_count
            FROM incidents
            WHERE fingerprint = @Fingerprint
              AND status = 'Open'
              AND last_seen_at >= NOW() - INTERVAL '30 minutes'
            ORDER BY last_seen_at DESC
            LIMIT 1
            FOR UPDATE;
            """;

        var existing = await conn.QuerySingleOrDefaultAsync<(Guid Id, int OccurrenceCount)>(
            new CommandDefinition(findOpenSql, new { Fingerprint = fingerprint }, tx, cancellationToken: ct));

        Guid incidentId;
        bool isNew;
        int count;

        if (existing.Id == Guid.Empty)
        {
            incidentId = Guid.NewGuid();
            isNew = true;
            count = 1;

            const string insertIncidentSql = """
                INSERT INTO incidents (
                    id, fingerprint, service_name, environment, exception_type,
                    endpoint, status, occurrence_count, first_seen_at, last_seen_at, last_trace_id
                )
                VALUES (
                    @Id, @Fingerprint, @ServiceName, @Environment, @ExceptionType,
                    @Endpoint, 'Open', 1, @OccurredAt, @OccurredAt, @TraceId
                );
                """;

            await conn.ExecuteAsync(new CommandDefinition(insertIncidentSql, new
            {
                Id = incidentId,
                Fingerprint = fingerprint,
                evt.ServiceName,
                evt.Environment,
                evt.ExceptionType,
                evt.Endpoint,
                OccurredAt = evt.OccurredAt ?? DateTimeOffset.UtcNow,
                evt.TraceId
            }, tx, cancellationToken: ct));
        }
        else
        {
            incidentId = existing.Id;
            isNew = false;
            count = existing.OccurrenceCount + 1;

            const string updateIncidentSql = """
                UPDATE incidents
                SET occurrence_count = occurrence_count + 1,
                    last_seen_at = @OccurredAt,
                    last_trace_id = COALESCE(@TraceId, last_trace_id)
                WHERE id = @Id;
                """;

            await conn.ExecuteAsync(new CommandDefinition(updateIncidentSql, new
            {
                Id = incidentId,
                OccurredAt = evt.OccurredAt ?? DateTimeOffset.UtcNow,
                evt.TraceId
            }, tx, cancellationToken: ct));
        }

        const string insertEventSql = """
            INSERT INTO incident_events (id, incident_id, trace_id, sanitized_message, stack_trace, occurred_at)
            VALUES (@Id, @IncidentId, @TraceId, @SanitizedMessage, @StackTrace, @OccurredAt);
            """;

        await conn.ExecuteAsync(new CommandDefinition(insertEventSql, new
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            evt.TraceId,
            SanitizedMessage = sanitizedMessage,
            StackTrace = sanitizedStackTrace,
            OccurredAt = evt.OccurredAt ?? DateTimeOffset.UtcNow
        }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return new UpsertIncidentResult(incidentId, isNew, count);
    }

    public async Task SaveDiagnosisAsync(Guid incidentId, IncidentDiagnosis diagnosis, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        const string sql = """
            UPDATE incidents
            SET diagnosis_json = @DiagnosisJson::jsonb
            WHERE id = @IncidentId;
            """;

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            IncidentId = incidentId,
            DiagnosisJson = JsonSerializer.Serialize(diagnosis)
        }, cancellationToken: ct));
    }
}
