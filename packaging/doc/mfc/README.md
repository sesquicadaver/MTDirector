# MTDirector Controller — operator host notes (OPS-HOST-DOC-01 / W7-302)

This file is the packaging doc artifact referenced by the systemd unit:

```
Documentation=file:///usr/share/doc/mfc/README.md
```

Canonical source in the repo: `packaging/doc/mfc/README.md`.  
`scripts/release/package-controller.sh` also copies it into `$OUT_DIR/controller/README.md`.

## Install sketch (Linux)

After `package-controller.sh` (and OPS-HOST-SYSUSERS-01 bootstrap):

```bash
sudo rsync -a "$OUT_DIR/controller/" /opt/mfc/controller/
sudo install -D -m 0644 /opt/mfc/controller/README.md /usr/share/doc/mfc/README.md
# or from the repo tree:
# sudo install -D -m 0644 packaging/doc/mfc/README.md /usr/share/doc/mfc/README.md
sudo install -m 0644 packaging/systemd/mfc-controller.service /etc/systemd/system/mfc-controller.service
sudo systemctl daemon-reload
sudo systemctl enable --now mfc-controller.service
```

`systemctl status mfc-controller` / `systemctl help` will then resolve the unit `Documentation=` URI to this README.

## Related host templates

| Artifact | Role |
|----------|------|
| `mfc-controller.service` | systemd unit (OPS-HOST-SYSTEMD-01) |
| `mfc-controller.env.example` | EnvironmentFile sample (OPS-HOST-ENV-01) |
| `mfc-controller.sysusers` / `.tmpfiles` | mfc user + dirs (OPS-HOST-SYSUSERS-01) |
| `mfc-controller.winsw.xml` | Windows Service (OPS-HOST-WINSVC-01) |

See also: `docs/operations/installation.md`, `docs/howto/build-and-run.md`, `docs/release/packaging.md`.

## Out of scope

- Native MSI / AppImage (W7-22 lock).
- Secrets — never put production credentials in this README.

## Logs (OPS-HOST-LOG-01)

The systemd unit sets `SyslogIdentifier=mfc-controller` with stdout/stderr to the journal. Filter with:

```bash
journalctl -u mfc-controller.service -e
journalctl -t mfc-controller -e
```

## Health probes (CTRL-HTTP-HEALTH-01)

On the Controller `ListenAddress` (same port as gRPC). Prefer `https://` so classic HTTP/1.1 probes work (`Http1AndHttp2`); cleartext `http://` is HTTP/2-only:

```bash
# Liveness — process up (HTTP/1.1 OK)
curl -fsS "$LISTEN/health/live"
# Readiness — fail-closed when PostgreSQL is unreachable
curl -fsS "$LISTEN/health/ready"
```

gRPC health (`grpc.health.v1.Health/Check`) remains available for Desktop/gRPC clients.

## Metrics scrape (CTRL-HTTP-METRICS-01)

Prometheus text exposition is **opt-in** (default off). Set `Mfc:Metrics:Enabled=true` / `MFC__Metrics__Enabled=true`, then scrape:

```bash
curl -fsS "$LISTEN/metrics"
```

Optional path override: `Mfc:Metrics:ScrapePath` (default `/metrics`). Health probes stay registered when metrics are disabled.

## Tracing export (CTRL-HTTP-OTEL-TRACE-01)

OpenTelemetry tracing is **opt-in** (default off). Set `Mfc:Tracing:Enabled=true` / `MFC__Tracing__Enabled=true`, then configure exporters:

- OTLP: `Mfc:Tracing:OtlpEndpoint` / `MFC__Tracing__OtlpEndpoint` (e.g. `http://127.0.0.1:4317`)
- Console (local ops): `Mfc:Tracing:ConsoleExporter=true` / `MFC__Tracing__ConsoleExporter=true`

Both exporters may be enabled together. Enabling tracing without either exporter fails closed at startup. Health probes and optional `/metrics` stay registered independently.

## Log↔trace correlation (CTRL-LOG-OTEL-CORRELATE-01)

Controller JSON console logs (journald via `StandardOutput=journal`) include W3C **`traceId`** and **`spanId`** when `System.Diagnostics.Activity.Current` is set (typical during ASP.NET/gRPC request spans with tracing enabled). Fields are omitted when no Activity is present. Operators join journal lines to OTLP/console spans by TraceId. Secret redaction is unchanged; health/metrics/tracing opt-in defaults are unchanged.


