using Npgsql;
using Sentinel.Diagnostic.Worker.Data;
using Sentinel.Shared.Diagnostics;
using Sentinel.Shared.Models;
using Testcontainers.PostgreSql;

namespace Sentinel.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class IncidentRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("sentinel_ai")
        .WithUsername("sentinel")
        .WithPassword("sentinel_test")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        const string schemaSql = """
            CREATE TABLE IF NOT EXISTS incidents (
                id               UUID PRIMARY KEY,
                fingerprint      VARCHAR(64) NOT NULL,
                service_name     VARCHAR(120) NOT NULL,
                environment      VARCHAR(40) NOT NULL,
                exception_type   VARCHAR(255) NOT NULL,
                endpoint         VARCHAR(255),
                status           VARCHAR(30) NOT NULL DEFAULT 'Open',
                occurrence_count INT NOT NULL DEFAULT 1,
                first_seen_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                last_seen_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                last_trace_id    VARCHAR(100),
                diagnosis_json   JSONB
            );
            CREATE INDEX IF NOT EXISTS idx_incidents_dedup
                ON incidents (fingerprint, status, last_seen_at DESC);

            CREATE TABLE IF NOT EXISTS incident_events (
                id                UUID PRIMARY KEY,
                incident_id       UUID NOT NULL REFERENCES incidents(id) ON DELETE CASCADE,
                trace_id          VARCHAR(100),
                sanitized_message TEXT NOT NULL,
                stack_trace       TEXT,
                occurred_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;

        await using var cmd = new NpgsqlCommand(schemaSql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task UpsertIncidentAndRecordEventAsync_ShouldDeduplicateWithinTimeWindow()
    {
        var repo = new IncidentRepository(_postgres.GetConnectionString());
        var evt = new IncidentEvent(
            ServiceName: "Orders.Api",
            Environment: "Production",
            ExceptionType: "Npgsql.NpgsqlException",
            Message: "Timeout expired",
            Endpoint: "/api/orders/1",
            TraceId: "trace-1");

        var fp = IncidentFingerprinter.Compute(evt);

        var first = await repo.UpsertIncidentAndRecordEventAsync(fp, evt, evt.Message, null, CancellationToken.None);
        var second = await repo.UpsertIncidentAndRecordEventAsync(
            fp, evt with { TraceId = "trace-2" }, evt.Message, null, CancellationToken.None);

        Assert.True(first.IsNewIncident);
        Assert.Equal(1, first.OccurrenceCount);

        Assert.False(second.IsNewIncident);
        Assert.Equal(first.IncidentId, second.IncidentId);
        Assert.Equal(2, second.OccurrenceCount);

        await repo.SaveDiagnosisAsync(
            first.IncidentId,
            new IncidentDiagnosis("DB pool exhausted", "High", "PostgreSQL", "Check connection pool"),
            CancellationToken.None);
    }
}
