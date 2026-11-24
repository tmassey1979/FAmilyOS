# Observability (Prometheus, Grafana, client logs, PostHog)

## Prometheus + Grafana

- API exposes **Prometheus** metrics at `GET /metrics` (no auth; keep internal).
- `prometheus-net.AspNetCore` records HTTP via `UseHttpMetrics`.
- Client log batches increment `familyos_client_log_events_total{level,platform}`.

### Enable on the server

```bash
cd deploy/familyos
export GRAFANA_ADMIN_PASSWORD='strong-password'
docker compose --profile observability up -d
```

- Prometheus scrapes `api:5080/metrics`
- Grafana at `https://grafana.${DOMAIN}` (admin / `GRAFANA_ADMIN_PASSWORD`)

On ≤2GB RAM hosts, leave the profile off or use a larger instance.

### Env

```
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=change-me
```

## Client log shipping (MAUI)

`POST /api/client-logs` accepts batched entries from the app.

- Auth optional (JWT preferred; anonymous allowed for pre-login failures).
- Logs appear in API **Serilog** as `[client] …`.
- Counts show up in Prometheus / Grafana.

MAUI `ClientLogService` buffers and flushes every ~20s (`Info` / `Warn` / `Error`).

## PostHog

Used for feature flags, product analytics, and exception capture when keys are set.

```
PostHog__ApiKey=phc_...
PostHog__Host=https://us.i.posthog.com
EXPO_PUBLIC_POSTHOG_KEY=phc_...
```

Without keys, PostHog calls no-op.
