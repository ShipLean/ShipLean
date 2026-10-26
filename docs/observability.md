# Observability

ShipLean exports traces, metrics and logs over OpenTelemetry (OTLP) when you point it at a collector. Console logging and Seq are unchanged and work with or without it.

## Turning it on

Set the standard OpenTelemetry variables. Nothing is registered, and nothing leaves the process, until `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

| Variable | Meaning |
|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Where to send. Setting it turns telemetry on for all three signals. |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` or `http/protobuf`. Logs use the `/v1/logs` path with `http/protobuf`. |
| `OTEL_EXPORTER_OTLP_HEADERS` | `key=value` pairs, comma separated, for a backend's API key. |
| `OTEL_SERVICE_NAME` | Defaults to `shiplean`. |
| `OTEL_RESOURCE_ATTRIBUTES` | Extra resource attributes, for example `deployment.environment=prod`. |

Every signal carries the same resource: `service.name`, `service.version` (the build's informational version) and `service.instance.id` (host name plus a per-process id, so a restart is a new instance).

The tracer and meter use the one name `ShipLean` for custom spans and instruments.

## Trying it locally

The compose file has an `otel` profile that runs the Aspire dashboard:

```
docker compose -f src/docker-compose.yml --profile otel up --build
```

Uncomment `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889` (and the protocol line) on the `shiplean` service, then open <http://127.0.0.1:18888> for traces, metrics and logs. The dashboard runs unauthenticated and both ports are bound to 127.0.0.1; do not expose them.

For a real deployment point the endpoint at your collector (an OpenTelemetry Collector, Grafana Alloy, or a vendor's OTLP endpoint) and put its credentials in `OTEL_EXPORTER_OTLP_HEADERS`.

## Traces

- **ASP.NET Core**: one server span per request, named by route template (`GET /api/projects/{key}`), never the raw path. Static SPA assets and health checks are not traced. The query string is not recorded.
- **Npgsql**: a child span per database command (Dapper goes through Npgsql). Parameter values are not recorded.
- **StackExchange.Redis**: command spans, only when `SHIPLEAN_REDIS_CONNECTION` is set.
- **HttpClient**: outgoing calls (Jira import, link unfurl, OIDC discovery, webhooks). The `Authorization` header and URL credentials are redacted.
- **MCP**: one `mcp.tool_call` span per tool call, tagged `mcp.tool`, `shiplean.project`, `shiplean.agent_name` (when the caller is an agent that has one), `shiplean.outcome` and `shiplean.error_code`. The SDK's own ModelContextProtocol source is subscribed too.
- **Background work**: `import.job` and `search.index.round` spans from the `ShipLean` source. The presence heartbeat is sampled out (its spans and their Redis children are dropped), so the default sampler is replaced by a parent-based always-on one and `OTEL_TRACES_SAMPLER` is not honoured.
- Only `/api`, `/mcp`, `/oauth` and `/.well-known` requests are traced.

## Metrics

Built in: ASP.NET Core (`Microsoft.AspNetCore.Hosting`, Kestrel, `Microsoft.AspNetCore.Http.Connections` for SignalR), `System.Net.Http`, the .NET runtime and Npgsql.

The `ShipLean` meter:

| Instrument | Kind | Tags |
|---|---|---|
| `shiplean.mcp.tool_calls` | counter | `tool`, `outcome` |
| `shiplean.mcp.tool_duration` | histogram | `tool`, `outcome` |
| `shiplean.import.jobs` | counter | `source`, `outcome` |
| `shiplean.import.items` | counter | `source` |
| `shiplean.search.index_queue` | observable gauge | |
| `shiplean.search.indexed` | counter | |
| `shiplean.realtime.presence_screens` | observable gauge | |

Tag cardinality is kept bounded: no item keys, user ids or free text. The project key is the most specific tag allowed.

## Logs

Logs go over OTLP through Serilog, in addition to the console and Seq sinks, using the same endpoint, protocol, headers and resource. Each log record written during a request carries that request's `TraceId` and `SpanId`, so a log line links to its trace in the backend (and in Seq).

## Deliberately left out

- **PII**: no email addresses, names or user ids as span attributes or metric tags.
- **Secrets**: no tokens, passwords, API keys, webhook secrets or `Authorization` headers.
- **Query strings**: OAuth `code` and `state` and the token endpoints must not leak, so the query string is never recorded.
- **SQL parameter values**: statements only.
- **Item content**: titles, descriptions and comments are never attributes or tags.
