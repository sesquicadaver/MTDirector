# PLAN-43 — Controller metrics / OpenTelemetry beyond HTTP health probes

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-316); seed **W7-317 (#1040) OPEN**; predecessor **PLAN-42 COMPLETE**  
**PLAN issue / queue:** [W7-316 / PLAN-43 #1039](https://github.com/sesquicadaver/MTDirector/issues/1039) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-42 Controller HTTP health probes **COMPLETE** (CTRL-HTTP-HEALTH-01)  
**Normative files:** [`Program.cs`](../../src/Mfc.Controller/Program.cs), [`installation.md`](../operations/installation.md), [`packaging/doc/mfc/README.md`](../../packaging/doc/mfc/README.md)  
**Normative prior locks:** HTTP `/health/live` + `/health/ready`; gRPC health; QG-SIGN-01/02; PLAN-32…42 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-42 shipped HTTP liveness/readiness: operators can prove process/DB readiness, but still lack scrapeable **metrics / OpenTelemetry** (or equivalent Prometheus `/metrics`) for dashboards, SLOs, and capacity signals.

## Principles

1. Installed Controller hosts should expose **minimal correct metrics** usable by ops tooling alongside existing HTTP + gRPC health.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-42 HEALTH / packaging host-unit polish — those waves are **COMPLETE** / saturating.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer operator observability (metrics) over systemd Type=notify vanity polish.

## Out of scope (do not seed)

- Re-opening PLAN-42 CTRL-HTTP-HEALTH-01  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Full distributed-tracing productization beyond a bounded metrics first row (inventory may refine)  
- systemd `Type=notify` / `WatchdogSec` packaging polish (explicitly deferred / saturating)

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-42 COMPLETE / `ba2a0af6`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` | `MapHealthChecks` `/health/live` + `/health/ready` + gRPC health | No Prometheus `/metrics` / OTel meter export |
| `installation.md` / packaging doc | HTTP probe guidance | No metrics scrape guidance |
| Glob / rg | `OpenTelemetry` / `MapPrometheusScrapingEndpoint` / `/metrics` absent under Controller @ `ba2a0af6` | Confirmed |
| Packaging unit | `Type=simple` + journald + HTTP probes documented | Metrics residual; Type=notify deferred |

## Ranked Controller metrics tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-HTTP-METRICS-01** | Author minimal correct metrics endpoint(s) (+ docs/Living Spec) alongside existing HTTP/gRPC health; keep MSI/AppImage locked | Health probes only @ `ba2a0af6` | after inventory **W7-316**; seed **W7-317 (#1040)** |

Inventory (**W7-316**) may refine ranking and open implement issues; seed **W7-317** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-42 CLOSED)

PLAN-42 sole ranked row (**CTRL-HTTP-HEALTH-01**) is **DONE**. No further PLAN-42 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-42 COMPLETE** (W7-314 CTRL-HTTP-HEALTH-01; seed **W7-315 DONE**).  
2. **W7-316 OPEN** — PLAN-43 inventory → open first metrics implement + follow-up seeds.  
3. **W7-317 OPEN** — seed first PLAN-43 implement after inventory.  
4. Execute ranked CTRL-HTTP-METRICS row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-316 (#1039)** — PLAN-43 Inventory Controller metrics/OpenTelemetry after PLAN-42.
