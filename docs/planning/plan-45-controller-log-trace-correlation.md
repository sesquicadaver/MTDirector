# PLAN-45 — Controller log↔trace correlation after OpenTelemetry tracing

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-324); seed **W7-325 (#1056) OPEN**; predecessor **PLAN-44 COMPLETE**  
**PLAN issue / queue:** [W7-324 / PLAN-45 #1055](https://github.com/sesquicadaver/MTDirector/issues/1055) **OPEN** (**§3.C NEXT**)  
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

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-44 COMPLETE / `1e47d2f9`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` | Opt-in `WithTracing` + OTLP/console exporters | Traces exist without log join keys |
| `RedactingJsonConsoleLoggerProvider` | JSON payload: timestamp/level/category/eventId/message (+exception) | No `traceId` / `spanId` from `Activity.Current` |
| Glob / rg | `Activity.Current` / `TraceId` absent under redacting logger @ `1e47d2f9` | Confirmed |
| Packaging unit | `Type=simple` + journald + health + metrics + opt-in tracing | Log↔trace residual; Type=notify deferred |

## Ranked Controller log-correlation tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-LOG-OTEL-CORRELATE-01** | Author minimal correct TraceId/SpanId enrichment on redacted JSON console logs (+ docs/Living Spec) alongside existing health/metrics/tracing; keep MSI/AppImage locked | Logger without Activity fields @ `1e47d2f9` | after inventory **W7-324**; seed **W7-325 (#1056)** |

Inventory (**W7-324**) may refine ranking and open implement issues; seed **W7-325** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-44 CLOSED)

PLAN-44 sole ranked row (**CTRL-HTTP-OTEL-TRACE-01**) is **DONE**. No further PLAN-44 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-44 COMPLETE** (W7-322 CTRL-HTTP-OTEL-TRACE-01; seed **W7-323 DONE**).  
2. **W7-324 OPEN** — PLAN-45 inventory → open first log-correlation implement + follow-up seeds.  
3. **W7-325 OPEN** — seed first PLAN-45 implement after inventory.  
4. Execute ranked CTRL-LOG-OTEL-CORRELATE row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-324 (#1055)** — PLAN-45 Inventory Controller log↔trace correlation after PLAN-44.
