# PLAN-44 — Controller OpenTelemetry tracing beyond metrics scrape

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-320); seed **W7-321 (#1048) OPEN**; predecessor **PLAN-43 COMPLETE**  
**PLAN issue / queue:** [W7-320 / PLAN-44 #1047](https://github.com/sesquicadaver/MTDirector/issues/1047) **OPEN** (**§3.C NEXT**)  
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

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-43 COMPLETE / `6596cae1`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` | Opt-in `AddOpenTelemetry().WithMetrics` + `MapPrometheusScrapingEndpoint` `/metrics` | No `WithTracing` / OTLP exporter / ASP.NET+gRPC activity sources |
| `installation.md` / packaging doc | Health + metrics scrape guidance | No tracing exporter guidance |
| Glob / rg | `WithTracing` / `AddOtlpExporter` / `ActivitySource` absent under Controller metrics path @ `6596cae1` | Confirmed |
| Packaging unit | `Type=simple` + journald + health + opt-in metrics | Tracing residual; Type=notify deferred |

## Ranked Controller tracing tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-HTTP-OTEL-TRACE-01** | Author minimal correct opt-in tracing export (+ docs/Living Spec) alongside existing HTTP/gRPC health + metrics; keep MSI/AppImage locked | Metrics-only OTel @ `6596cae1` | after inventory **W7-320**; seed **W7-321 (#1048)** |

Inventory (**W7-320**) may refine ranking and open implement issues; seed **W7-321** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-43 CLOSED)

PLAN-43 sole ranked row (**CTRL-HTTP-METRICS-01**) is **DONE**. No further PLAN-43 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-43 COMPLETE** (W7-318 CTRL-HTTP-METRICS-01; seed **W7-319 DONE**).  
2. **W7-320 OPEN** — PLAN-44 inventory → open first tracing implement + follow-up seeds.  
3. **W7-321 OPEN** — seed first PLAN-44 implement after inventory.  
4. Execute ranked CTRL-HTTP-OTEL-TRACE row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-320 (#1047)** — PLAN-44 Inventory Controller OpenTelemetry tracing after PLAN-43.
