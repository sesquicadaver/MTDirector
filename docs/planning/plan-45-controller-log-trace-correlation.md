# PLAN-45 — Controller log↔trace correlation after OpenTelemetry tracing

**Date:** 2026-09-17 (inventory **DONE** @ `2da9d508`; **PLAN-45 COMPLETE**)  
**Status:** **PLAN-45 COMPLETE** — Inventory **DONE** (W7-324); seed **W7-325 (#1056) DONE**; implement **W7-326 (#1058) DONE**; COMPLETE seed **W7-327 (#1060) DONE**; successor **PLAN-46** inventory **W7-328 (#1063) DONE**; seed **W7-329 (#1064) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-324 / PLAN-45 #1055](https://github.com/sesquicadaver/MTDirector/issues/1055) **DONE**  
**Predecessor:** PLAN-44 Controller OpenTelemetry tracing **COMPLETE** (CTRL-HTTP-OTEL-TRACE-01)  
**Normative files:** [`RedactingJsonConsoleLoggerProvider.cs`](../../src/Mfc.Infrastructure/Persistence/Logging/RedactingJsonConsoleLoggerProvider.cs), [`Program.cs`](../../src/Mfc.Controller/Program.cs), [`installation.md`](../operations/installation.md), [`packaging/doc/mfc/README.md`](../../packaging/doc/mfc/README.md)  
**Normative prior locks:** HTTP `/health/live` + `/health/ready`; opt-in `/metrics`; opt-in `WithTracing`; gRPC health; QG-SIGN-01/02; PLAN-32…44 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-44 shipped opt-in OTel tracing: operators can export OTLP/console spans, but Controller **JSON console / journald logs** still lack W3C **TraceId/SpanId** fields to join log lines with traces.

## Principles

1. Installed Controller hosts should emit **minimal correct log↔trace correlation** usable with existing journald identity + opt-in tracing.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-44 TRACE / packaging host-unit polish — those waves are **COMPLETE** / saturating.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer operator observability (log correlation) over systemd Type=notify vanity polish.

## Out of scope (do not seed)

- Re-opening PLAN-44 CTRL-HTTP-OTEL-TRACE-01  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Full logging-backend productization / vanity dashboards  
- systemd `Type=notify` / `WatchdogSec` packaging polish (explicitly deferred / saturating)

## Inventory evidence (W7-324 @ `main` `2da9d508`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` | Opt-in `WithTracing` + OTLP/console exporters | Traces exist without log join keys |
| `RedactingJsonConsoleLoggerProvider` | JSON payload: timestamp/level/category/eventId/message (+exception) | No `traceId` / `spanId` from `Activity.Current` |
| Glob / rg | `Activity.Current` / `TraceId` absent under redacting logger @ `2da9d508` | Confirmed |
| Packaging unit | `Type=simple` + journald + health + metrics + opt-in tracing | Log↔trace residual; Type=notify deferred |

**Ranking decision:** Prefer **ONE atomic row** (**CTRL-LOG-OTEL-CORRELATE-01**) covering minimal correct Activity-based enrichment:

- When `System.Diagnostics.Activity.Current` is non-null, add JSON fields `traceId` and `spanId` using W3C hex forms (`Activity.TraceId` / `Activity.SpanId`)
- When no Activity is present, omit those fields (do not invent zero/empty placeholders)
- Keep secret redaction unchanged; do not change health/metrics/tracing opt-in defaults
- Docs + Living Spec alongside existing HTTP/gRPC health + metrics + tracing

Splitting console vs JSON enrichment or inventing a second logging backend would be vanity; Type=notify and Desktop a11y remain deferred adjacent residuals.

## Ranked Controller log-correlation tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-LOG-OTEL-CORRELATE-01** | Author minimal correct TraceId/SpanId enrichment on redacted JSON console logs (+ docs/Living Spec) alongside existing health/metrics/tracing; keep MSI/AppImage locked | Logger without Activity fields @ `2da9d508` | implement **W7-326 (#1058) DONE** |

Inventory (**W7-324 DONE**) confirmed sole rank. Seed **W7-325 DONE**; CORRELATE-01 **W7-326 DONE**; COMPLETE **W7-327 DONE**.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-44 CLOSED)

PLAN-44 sole ranked row (**CTRL-HTTP-OTEL-TRACE-01**) is **DONE**. No further PLAN-44 product rows.

## Delivery notes (W7-326)

`RedactingJsonConsoleLoggerProvider` now reads `Activity.Current` and, when present, emits `traceId`/`spanId` (W3C hex). Omission when no Activity. Docs: `installation.md` §7 + packaging operator README. Living Spec + unit correlate tests. Health/metrics/`WithTracing` opt-in unchanged.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Controller OTel resource identity (`service.name`) — seeded as **PLAN-46** [`plan-46-controller-otel-resource-identity.md`](plan-46-controller-otel-resource-identity.md)
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-44 COMPLETE** (W7-322 CTRL-HTTP-OTEL-TRACE-01; seed **W7-323 DONE**).  
2. **W7-324 DONE** — PLAN-45 inventory; opened **W7-326 (#1058)** CTRL-LOG-OTEL-CORRELATE-01 implement.  
3. **W7-325 DONE** — seed advanced NEXT to CTRL-LOG-OTEL-CORRELATE-01; opened COMPLETE **W7-327 (#1060)**.  
4. **W7-326 DONE** — sole CTRL-LOG-OTEL-CORRELATE-01 shipped.
5. **W7-327 DONE** — PLAN-45 COMPLETE; seeded PLAN-46 inventory **W7-328**.

## §3.C NEXT

**§3.C NEXT = W7-387 (#1179)** — Seed first PLAN-46 atomic row after inventory → CTRL-HTTP-OTEL-RESOURCE-01.
