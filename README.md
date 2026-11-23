# SentinelAI

Copiloto de **AIOps & Diagnóstico Automatizado de Incidentes** construído em **.NET** com arquitetura orientada a eventos (**AWS SQS + S3 + PostgreSQL/Dapper**).

## Arquitetura (Fase 1 - Base)

- **`Sentinel.Ingest.Api`**: Minimal API que recebe eventos/alertas de erro (`POST /api/v1/events`) e enfileira de forma assíncrona no **SQS**.
- **`Sentinel.Diagnostic.Worker`**: Worker Service que consome o SQS, sanitiza PII (`PiiSanitizer`), calcula fingerprint (`IncidentFingerprinter`), deduplica no **PostgreSQL** via **Dapper** e gera diagnóstico inicial.
- **`Sentinel.Shared`**: Contratos, sanitizador de PII (tokens, CPFs, e-mails, senhas de connection string) e algoritmo de fingerprinting.
- **`infra/`**: `docker-compose.yml` com PostgreSQL 16 + LocalStack (SQS com DLQ + S3).

## Como rodar localmente

1. Subir a infraestrutura (PostgreSQL + LocalStack):
   ```bash
   cd infra && docker compose up -d
   ```

2. Rodar os testes unitários:
   ```bash
   dotnet test
   ```

3. Iniciar o Worker e a API (em terminais separados):
   ```bash
   dotnet run --project src/Sentinel.Diagnostic.Worker
   dotnet run --project src/Sentinel.Ingest.Api
   ```

4. Enviar um evento de teste:
   ```bash
   curl -X POST http://localhost:5000/api/v1/events \
     -H "Content-Type: application/json" \
     -d '{
       "serviceName": "Orders.Api",
       "environment": "Production",
       "exceptionType": "Npgsql.NpgsqlException",
       "message": "Timeout for user joao@empresa.com (CPF 123.456.789-00)",
       "endpoint": "/api/orders/42",
       "traceId": "trace-abc-123"
     }'
   ```

## Roadmap

- [x] **Fase 1:** Solution .NET, Ingest API, SQS (LocalStack), Worker, Deduplicação no Postgres (Dapper), Sanitizador de PII e testes unitários.
- [ ] **Fase 2:** Integração `Microsoft.Extensions.AI` (Ollama / OpenAI) + Polly Circuit Breaker + Webhook Discord/Slack.
- [ ] **Fase 3:** `SampleTarget.Api` com falhas simuladas + OpenTelemetry + Grafana.
- [ ] **Fase 4:** Testcontainers, BDD (Reqnroll), CloudFormation e GitHub Actions + SonarCloud.
