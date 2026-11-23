CREATE TABLE IF NOT EXISTS incidents (
    id              UUID PRIMARY KEY,
    fingerprint     VARCHAR(64) NOT NULL,
    service_name    VARCHAR(120) NOT NULL,
    environment     VARCHAR(40) NOT NULL,
    exception_type  VARCHAR(255) NOT NULL,
    endpoint        VARCHAR(255),
    status          VARCHAR(30) NOT NULL DEFAULT 'Open',
    occurrence_count INT NOT NULL DEFAULT 1,
    first_seen_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_trace_id   VARCHAR(100),
    diagnosis_json  JSONB
);

-- Composite index for fast deduplication lookups (fingerprint + status + time window)
CREATE INDEX IF NOT EXISTS idx_incidents_dedup
    ON incidents (fingerprint, status, last_seen_at DESC);

CREATE TABLE IF NOT EXISTS incident_events (
    id              UUID PRIMARY KEY,
    incident_id     UUID NOT NULL REFERENCES incidents(id) ON DELETE CASCADE,
    trace_id        VARCHAR(100),
    sanitized_message TEXT NOT NULL,
    stack_trace     TEXT,
    occurred_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_incident_events_incident_id
    ON incident_events (incident_id, occurred_at DESC);
