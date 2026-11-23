Feature: Incident Deduplication and PII Protection
  To avoid alert fatigue and protect sensitive customer data
  SentinelAI must compute deterministic fingerprints and redact PII before AI analysis.

  Scenario: Recurring errors across different trace IDs share the same fingerprint
    Given an error event from service "Orders.Api" on endpoint "/api/orders/10" with exception "Npgsql.NpgsqlException" and traceId "trace-001"
    And a second error event from service "Orders.Api" on endpoint "/api/orders/10" with exception "Npgsql.NpgsqlException" and traceId "trace-002"
    When I compute the fingerprints for both events
    Then both fingerprints must be identical

  Scenario: Sensitive customer data is redacted before persistence or LLM analysis
    Given a raw error message containing "CPF 123.456.789-00, email user@corp.com and Bearer secret.jwt.token"
    When the PII sanitizer processes the message
    Then the sanitized message must not contain "123.456.789-00"
    And the sanitized message must not contain "user@corp.com"
    And the sanitized message must not contain "secret.jwt.token"
