# Installation (v0.2.0)

Повний HOWTO зі збірки та запуску на **Linux / Windows**: [`../howto/build-and-run.md`](../howto/build-and-run.md).

Release **`v0.2.0`** ships MVP + Post-MVP M7 feature-complete **code**. P2 pilot wiring is **CLOSED** in code:

- Live read/capture: **`Mfc:RouterOs:Enabled=true`**
- Live onboarding/deploy: **`Mfc:RouterOs:WriteEnabled=true`** (lab only)

See [`pilot-runbook.md`](pilot-runbook.md), [`controller-configuration.md`](controller-configuration.md), and [`../release/known-limitations.md`](../release/known-limitations.md).

## Prerequisites

- .NET runtime/SDK matching [`global.json`](../../global.json) on the Controller host (publish is framework-dependent by default).
- PostgreSQL (only supported database).
- TLS certificates for non-Development Controller binds.
- Operator workstation for Desktop (Avalonia publish archive). Launch templates are **bundled** into `OUT_DIR/desktop/` by `package-desktop.sh` (DESK-HOST-BUNDLE-01). Linux menus: install `mfc-desktop.desktop` from the extract (or [`../../packaging/linux/mfc-desktop.desktop`](../../packaging/linux/mfc-desktop.desktop), DESK-HOST-LINUX-01) after extracting to `/opt/mfc/desktop`. Windows Start Menu: run `mfc-desktop-start-menu.ps1` from the extract (or [`../../packaging/windows/mfc-desktop-start-menu.ps1`](../../packaging/windows/mfc-desktop-start-menu.ps1), DESK-HOST-WIN-01) after extracting to `C:\mfc\desktop`.

See also [`prerequisite-checklist.md`](prerequisite-checklist.md) for RouterOS device gates.

## Controller

1. Obtain the Controller package from release packaging (`scripts/release/package-controller.sh` → `OUT_DIR/controller`). Use `MFC_RELEASE_RID=linux-x64` (default) or `win-x64`.
2. Configure `Mfc` settings / env (`MFC__…`) per [`controller-configuration.md`](controller-configuration.md).
3. Apply schema with the migrations bundle (`OUT_DIR/migrations/mfc-ef-migrations`) **or** Development `--migrate-only`.
4. Start `Mfc.Controller` and verify health: gRPC health **and** HTTP probes `GET /health/live` (process) + `GET /health/ready` (fail-closed DB) on the same ListenAddress. Production `https://` uses Kestrel `Http1AndHttp2` (classic HTTP/1.1 curl OK); cleartext Development `http://` stays HTTP/2-only for h2c gRPC compatibility.
5. Optional metrics (CTRL-HTTP-METRICS-01): scrapeable Prometheus text at `GET /metrics` is **opt-in** via `Mfc:Metrics:Enabled=true` (default **false** / fail-closed — no scrape surface). Keep health probes intact regardless of metrics.
6. Optional tracing (CTRL-HTTP-OTEL-TRACE-01): OpenTelemetry distributed tracing is **opt-in** via `Mfc:Tracing:Enabled=true` (default **false** / fail-closed). When enabled, configure at least one exporter: `Mfc:Tracing:OtlpEndpoint` (OTLP collector URL) and/or `Mfc:Tracing:ConsoleExporter=true` for local ops. ASP.NET Core instrumentation covers inbound HTTP + gRPC on Kestrel. Keep health + metrics intact regardless of tracing.
7. Log↔trace correlation (CTRL-LOG-OTEL-CORRELATE-01): Controller redacted JSON console / journald lines include W3C **`traceId`** / **`spanId`** when `Activity.Current` is present (e.g. during an instrumented request). Omit those fields when no Activity is active. Join journald lines to OTLP/console spans via the same hex TraceId; health/metrics/tracing opt-in defaults are unchanged.
8. OpenTelemetry resource identity (CTRL-HTTP-OTEL-RESOURCE-01): when metrics and/or tracing are opted in, exports include Resource attributes **`service.name=Mfc.Controller`**, **`service.version`** (assembly informational/file version), and **`service.instance.id`** (hostname). Health/metrics/tracing/correlation opt-in fail-closed defaults are unchanged.
9. gRPC message-size limits (CTRL-GRPC-MSGSIZE-01): Controller `AddGrpc` and Desktop `GrpcChannelOptions` set **MaxReceiveMessageSize** / **MaxSendMessageSize** to shared `GrpcTransportLimits.MaxMessageBytes` (**256 MiB** / **268435456**), aligned with `RawSnapshotLimits.MaxSnapshotBytes`. Finite fail-closed ceiling — never unlimited. Health/metrics/tracing/correlation/resource unchanged.
10. Kestrel request-body limit (CTRL-KESTREL-BODY-01): Controller `ConfigureKestrel` sets **Limits.MaxRequestBodySize** to the same `GrpcTransportLimits.MaxMessageBytes` (**256 MiB** / **268435456**), so the ASP.NET Core default (~30 MiB) cannot reject large snapshot/diff RPCs before gRPC framing. Finite fail-closed — never unlimited / `null`. MSGSIZE/health/metrics/tracing/correlation/resource unchanged.

### Linux systemd (OPS-HOST-SYSTEMD-01)

Framework-dependent Controller can run under systemd using the repo template [`../../packaging/systemd/mfc-controller.service`](../../packaging/systemd/mfc-controller.service) (also bundled into publish tree, OPS-HOST-BUNDLE-01) (matches `package-controller.sh` layout: `/opt/mfc/controller/Mfc.Controller`).

```bash
# OPS-HOST-SYSUSERS-01 — declarative mfc user/group + host dirs (also in $OUT_DIR/controller/):
sudo install -m 0644 packaging/systemd/mfc-controller.sysusers /usr/lib/sysusers.d/mfc-controller.conf
sudo install -m 0644 packaging/systemd/mfc-controller.tmpfiles /usr/lib/tmpfiles.d/mfc-controller.conf
sudo systemd-sysusers mfc-controller.conf
sudo systemd-tmpfiles --create /usr/lib/tmpfiles.d/mfc-controller.conf
sudo rsync -a "$OUT_DIR/controller/" /opt/mfc/controller/
# OPS-HOST-DOC-01 — install operator README to Documentation= path (also in $OUT_DIR/controller/README.md):
sudo install -D -m 0644 /opt/mfc/controller/README.md /usr/share/doc/mfc/README.md
sudo install -m 0644 packaging/systemd/mfc-controller.service /etc/systemd/system/mfc-controller.service
# copy OPS-HOST-ENV-01 sample (also in $OUT_DIR/controller/ after package-controller):
sudo cp packaging/systemd/mfc-controller.env.example /etc/mfc/controller.env
# edit /etc/mfc/controller.env — MFC__Database__ConnectionString, TLS, etc. (no secrets in the example)
sudo systemctl daemon-reload
sudo systemctl enable --now mfc-controller.service
sudo systemctl status mfc-controller.service
# OPS-HOST-LOG-01 — unit sets SyslogIdentifier=mfc-controller + StandardOutput/Error=journal:
sudo journalctl -u mfc-controller.service -e
sudo journalctl -t mfc-controller -e
```

### Windows Service / WinSW (OPS-HOST-WINSVC-01)

Framework-dependent Controller (`MFC_RELEASE_RID=win-x64`) can run as a Windows Service using the repo WinSW template [`../../packaging/windows/mfc-controller.winsw.xml`](../../packaging/windows/mfc-controller.winsw.xml) (also bundled into publish tree, OPS-HOST-BUNDLE-01) (matches `package-controller.sh` layout: `%BASE%\Mfc.Controller.exe`).

```powershell
# After package-controller.sh with MFC_RELEASE_RID=win-x64:
New-Item -ItemType Directory -Force -Path C:\mfc\controller | Out-Null
Copy-Item -Recurse -Force "$env:OUT_DIR\controller\*" C:\mfc\controller\
# Place WinSW as mfc-controller.exe beside the publish tree, then:
Copy-Item packaging\windows\mfc-controller.winsw.xml C:\mfc\controller\mfc-controller.xml
# Set MFC__* machine env (or edit <env> in the XML), then:
.\mfc-controller.exe install
.\mfc-controller.exe start
.\mfc-controller.exe status
```

Native MSI/setup remains out of scope (W7-22). Do not regress the Linux systemd unit.

## Desktop

1. Obtain `Mfc.Desktop-<rid>.zip` (or `.tar.gz`) from `scripts/release/package-desktop.sh` (`linux-x64` or `win-x64`).
2. Extract and run `Mfc.Desktop` / `Mfc.Desktop.exe`.
3. Point `Desktop:ControllerEndpoint` at the Controller URL.
4. Connect → Inventory → **Add router** to register Site/Node/Device and connection profile (see [`../development/connection-profiles.md`](../development/connection-profiles.md)).

Native MSI/setup installers are out of MVP scope (zip publish is the installer substitute).

Desktop and Controller are **separate processes** — closing Desktop does not stop Controller.

## Verify integrity

```bash
cd "$OUT_DIR"
sha256sum -c SHA256SUMS
```

Signing policy: [`../release/RELEASE_SIGNING.md`](../release/RELEASE_SIGNING.md).
