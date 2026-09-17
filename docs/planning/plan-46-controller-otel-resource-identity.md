# PLAN-46 — Controller OpenTelemetry resource identity after log↔trace correlation

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-328); seed **W7-329 (#1064) OPEN**; predecessor **PLAN-45 COMPLETE**  
**PLAN issue / queue:** [W7-328 / PLAN-46 #1063](https://github.com/sesquicadaver/MTDirector/issues/1063) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-45 Controller log↔trace correlation **COMPLETE** (CTRL-LOG-OTEL-CORRELATE-01)  
**Normative files:** [`Program.cs`](../../src/Mfc.Controller/Program.cs), [`installation.md`](../operations/installation.md), [`packaging/doc/mfc/README.md`](../../packaging/doc/mfc/README.md)  
**Normative prior locks:** HTTP `/health/live` + `/health/ready`; opt-in `/metrics`; opt-in `WithTracing`; log `traceId`/`spanId`; gRPC health; QG-SIGN-01/02; PLAN-32…45 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-45 shipped log↔trace correlation: operators have health, metrics, tracing, and journald join keys, but OpenTelemetry **metrics/traces lack stable resource identity** (`service.name` / `service.version`) for backend filtering.

## Principles

1. Installed Controller hosts should emit **minimal correct OTel resource identity** usable with existing opt-in metrics + tracing + log correlation.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-45 CORRELATE / packaging host-unit polish — those waves are **COMPLETE** / saturating.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer operator observability (resource identity) over systemd Type=notify vanity polish.

## Out of scope (do not seed)

- Re-opening PLAN-45 CTRL-LOG-OTEL-CORRELATE-01  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Full APM productization / vanity dashboards  
- systemd `Type=notify` / `WatchdogSec` packaging polish (explicitly deferred / saturating)

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-45 COMPLETE / `f5d51d57`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` | Opt-in `AddOpenTelemetry` WithMetrics/WithTracing | No `ConfigureResource` / `ResourceBuilder` / `service.name` |
| Prometheus `/metrics` + OTLP/console traces | Export without stable service identity | Harder backend filter/join across hosts |
| Glob / rg | `ResourceBuilder` / `service.name` absent under Controller OTel path @ `f5d51d57` | Confirmed |
| Packaging unit | `Type=simple` + journald + health + metrics + tracing + log correlation | Resource residual; Type=notify deferred |

## Ranked Controller OTel resource tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-HTTP-OTEL-RESOURCE-01** | Author minimal correct OTel Resource (`service.name` + `service.version`) on metrics+traces (+ docs/Living Spec) alongside existing health/metrics/tracing/correlation; keep MSI/AppImage locked | No ResourceBuilder @ `f5d51d57` | after inventory **W7-328**; seed **W7-329 (#1064)** |

Inventory (**W7-328**) may refine ranking and open implement issues; seed **W7-329** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-45 CLOSED)

PLAN-45 sole ranked row (**CTRL-LOG-OTEL-CORRELATE-01**) is **DONE**. No further PLAN-45 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-45 COMPLETE** (W7-326 CTRL-LOG-OTEL-CORRELATE-01; seed **W7-327 DONE**).  
2. **W7-328 OPEN** — PLAN-46 inventory → open first resource-identity implement + follow-up seeds.  
3. **W7-329 OPEN** — seed first PLAN-46 implement after inventory.  
4. Execute ranked CTRL-HTTP-OTEL-RESOURCE row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-328 (#1063)** — PLAN-46 Inventory Controller OpenTelemetry resource identity after PLAN-45.
