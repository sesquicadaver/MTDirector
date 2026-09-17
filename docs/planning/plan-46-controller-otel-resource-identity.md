# PLAN-46 — Controller OpenTelemetry resource identity after log↔trace correlation

**Date:** 2026-09-17 (inventory **DONE** @ `894cc4b8`)  
**Status:** **PLAN-46 COMPLETE** — Inventory **DONE** (W7-328); seed **W7-329 (#1064) DONE**; implement **W7-330 (#1066) DONE**; COMPLETE seed **W7-331 (#1068) DONE**; successor **PLAN-47 COMPLETE**; PLAN-48 inventory **W7-336 (#1079) DONE**; seed **W7-337 (#1080) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-328 / PLAN-46 #1063](https://github.com/sesquicadaver/MTDirector/issues/1063) **DONE**  
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

## Inventory evidence (W7-328 @ `main` `894cc4b8`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` | Opt-in `AddOpenTelemetry` WithMetrics/WithTracing | No `ConfigureResource` / `ResourceBuilder` / `service.name` |
| Prometheus `/metrics` + OTLP/console traces | Export without stable service identity | Harder backend filter/join across hosts |
| Glob / rg | `ResourceBuilder` / `service.name` absent under Controller OTel path @ `894cc4b8` | Confirmed |
| Packaging unit | `Type=simple` + journald + health + metrics + tracing + log correlation | Resource residual; Type=notify deferred |

**Ranking decision:** Prefer **ONE atomic row** (**CTRL-HTTP-OTEL-RESOURCE-01**) covering minimal correct OTel Resource attributes:

- When metrics and/or tracing OTel is registered, configure Resource with `service.name` + `service.version` (and related stable identity via `ResourceBuilder` / `AddService`)
- Keep MSI/AppImage locked; do not change health/metrics/tracing/correlation opt-in fail-closed defaults
- Docs + Living Spec alongside existing HTTP/gRPC health + metrics + tracing + log correlation

Splitting metrics vs traces resource into two ranks would be vanity; Type=notify and Desktop a11y remain deferred adjacent residuals.

## Ranked Controller OTel resource tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-HTTP-OTEL-RESOURCE-01** | Author minimal correct OTel Resource (`service.name` + `service.version`) on metrics+traces (+ docs/Living Spec) alongside existing health/metrics/tracing/correlation; keep MSI/AppImage locked | No ResourceBuilder @ `894cc4b8` | implement **W7-330 (#1066) DONE**; COMPLETE **W7-331 (#1068) DONE** |

Inventory (**W7-328 DONE**) confirmed sole rank. Seed **W7-329** advances NEXT to RESOURCE-01 implement after inventory.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-45 CLOSED)

PLAN-45 sole ranked row (**CTRL-LOG-OTEL-CORRELATE-01**) is **DONE**. No further PLAN-45 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Controller gRPC message-size / transport limits — seeded as **PLAN-47** [`plan-47-controller-grpc-message-size-limits.md`](plan-47-controller-grpc-message-size-limits.md)
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-45 COMPLETE** (W7-326 CTRL-LOG-OTEL-CORRELATE-01; seed **W7-327 DONE**).  
2. **W7-328 DONE** — PLAN-46 inventory; opened **W7-330 (#1066)** CTRL-HTTP-OTEL-RESOURCE-01 implement.  
3. **W7-329 DONE** — seed advanced NEXT to CTRL-HTTP-OTEL-RESOURCE-01; opened COMPLETE **W7-331 (#1068)**.  
4. **W7-330 DONE** — sole CTRL-HTTP-OTEL-RESOURCE-01 shipped.
5. **W7-331 DONE** — PLAN-46 COMPLETE; seeded PLAN-47 inventory **W7-332**.

## Delivery notes (W7-330)

`Program.cs` configures OTel Resource via `ConfigureResource`/`AddService` when metrics and/or tracing are opted in: `service.name=Mfc.Controller`, `service.version` from assembly informational/file version, `service.instance.id=Environment.MachineName`. Docs: `installation.md` §8 + packaging operator README. Living Spec + queue lock. Health/metrics/`WithTracing`/log correlation opt-in unchanged.

## §3.C NEXT

**§3.C NEXT = W7-344 (#1095)** — PLAN-48 Inventory after PLAN-47 COMPLETE.
