# PLAN-44 — Controller OpenTelemetry tracing beyond metrics scrape

**Date:** 2026-09-17 (inventory **DONE** @ `0929ef8d`)  
**Status:** **PLAN-44 COMPLETE** — Inventory **DONE** (W7-320); seed **W7-321 (#1048) DONE**; implement **W7-322 (#1050) DONE**; COMPLETE seed **W7-323 (#1052) DONE**; successor **PLAN-45** inventory **W7-324 (#1055) DONE**; seed **W7-325 (#1056) DONE**; implement **W7-326 (#1058) DONE**; COMPLETE **W7-327 (#1060) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-320 / PLAN-44 #1047](https://github.com/sesquicadaver/MTDirector/issues/1047) **DONE**  
**Predecessor:** PLAN-43 Controller metrics / OpenTelemetry scrape **COMPLETE** (CTRL-HTTP-METRICS-01)  
**Normative files:** [`Program.cs`](../../src/Mfc.Controller/Program.cs), [`installation.md`](../operations/installation.md), [`packaging/doc/mfc/README.md`](../../packaging/doc/mfc/README.md)  
**Normative prior locks:** HTTP `/health/live` + `/health/ready`; opt-in `/metrics`; gRPC health; QG-SIGN-01/02; PLAN-32…43 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-43 shipped opt-in Prometheus metrics: operators can scrape meters, but still lack bounded **distributed tracing** (OTLP or equivalent) for correlating gRPC/HTTP request latency and failures.

## Principles

1. Installed Controller hosts should expose **minimal correct opt-in tracing** usable by ops tooling alongside existing HTTP + gRPC health and metrics.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-43 METRICS / packaging host-unit polish — those waves are **COMPLETE** / saturating.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer operator observability (tracing) over systemd Type=notify vanity polish.

## Out of scope (do not seed)

- Re-opening PLAN-43 CTRL-HTTP-METRICS-01  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Full APM productization / vanity dashboards  
- systemd `Type=notify` / `WatchdogSec` packaging polish (explicitly deferred / saturating)

## Inventory evidence (W7-320 @ `main` `0929ef8d`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` | Opt-in `AddOpenTelemetry().WithMetrics` + `MapPrometheusScrapingEndpoint` `/metrics` | No `WithTracing` / OTLP exporter / ASP.NET activity sources for traces |
| `ControllerOptions` | `MetricsHostOptions` (`Mfc:Metrics:Enabled` default false) | No `TracingHostOptions` |
| `installation.md` / packaging doc | Health + metrics scrape guidance | No tracing exporter guidance |
| Glob / rg | `WithTracing` / `AddOtlpExporter` / `ActivitySource` absent under Controller tracing path @ `0929ef8d` | Confirmed |
| Packages | `OpenTelemetry.Extensions.Hosting` + Prometheus + AspNetCore/Runtime **metrics** instrumentation | No `OpenTelemetry.Exporter.OpenTelemetryProtocol` / Console exporter / GrpcNetClient instrumentation |
| Packaging unit | `Type=simple` + journald + health + opt-in metrics | Tracing residual; Type=notify deferred |

**Ranking decision:** Prefer **ONE atomic row** (**CTRL-HTTP-OTEL-TRACE-01**) covering minimal correct opt-in tracing:

- `AddOpenTelemetry().WithTracing` + ASP.NET Core instrumentation (covers inbound HTTP + gRPC on Kestrel); add `OpenTelemetry.Instrumentation.GrpcNetClient` when package available for outbound gRPC/HTTP client spans
- Exporters (inventory lock): **OTLP** when `Mfc:Tracing:OtlpEndpoint` (or equivalent) is non-empty **and/or** **console** exporter when `Mfc:Tracing:ConsoleExporter=true` for local ops — both may coexist
- Default **fail-closed / disabled** like Metrics: `Mfc:Tracing:Enabled=false` — no scrape/export surface and no org secrets required for default PR CI
- Docs + Living Spec alongside existing HTTP/gRPC health + metrics

Splitting OTLP vs console into two ranks would be vanity; Type=notify and Desktop a11y remain deferred adjacent residuals.

## Ranked Controller tracing tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-HTTP-OTEL-TRACE-01** | Author minimal correct opt-in tracing export (+ docs/Living Spec) alongside existing HTTP/gRPC health + metrics; keep MSI/AppImage locked | Metrics-only OTel @ `0929ef8d` | implement **W7-322 (#1050)** after seed **W7-321 (#1048)** |

Inventory (**W7-320 DONE**) confirmed sole rank. Seed **W7-321** advances NEXT to CTRL-HTTP-OTEL-TRACE-01 implement; COMPLETE seed opens after TRACE-01.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-43 CLOSED)

PLAN-43 sole ranked row (**CTRL-HTTP-METRICS-01**) is **DONE**. No further PLAN-43 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Controller log↔trace correlation (TraceId/SpanId on JSON console logs) — seeded as **PLAN-45** [`plan-45-controller-log-trace-correlation.md`](plan-45-controller-log-trace-correlation.md)
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-43 COMPLETE** (W7-318 CTRL-HTTP-METRICS-01; seed **W7-319 DONE**).  
2. **W7-320 DONE** — PLAN-44 inventory; opened **W7-322 (#1050)** CTRL-HTTP-OTEL-TRACE-01 implement.  
3. **W7-321 DONE** — seed advanced NEXT to CTRL-HTTP-OTEL-TRACE-01; opened COMPLETE **W7-323 (#1052)**.  
4. **W7-322 DONE** — sole CTRL-HTTP-OTEL-TRACE-01 shipped (opt-in WithTracing + OTLP/console exporters + docs/Living Spec).
5. **W7-323 DONE** — PLAN-44 COMPLETE; seeded PLAN-45 inventory **W7-324**.

## §3.C NEXT

**§3.C NEXT = W7-328 (#1063)** — Seed first PLAN-45 atomic row after inventory → CTRL-LOG-OTEL-CORRELATE-01.
