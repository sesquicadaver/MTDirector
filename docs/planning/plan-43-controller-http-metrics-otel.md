# PLAN-43 — Controller metrics / OpenTelemetry beyond HTTP health probes

**Date:** 2026-09-17 (inventory **DONE** @ `94f04744`)  
**Status:** **PLAN-43 COMPLETE** — Inventory **DONE** (W7-316); seed **W7-317 (#1040) DONE**; implement **W7-318 (#1042) DONE**; COMPLETE seed **W7-319 (#1044) DONE**; successor **PLAN-44** inventory **W7-320 (#1047) DONE**; seed **W7-321 (#1048) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-316 / PLAN-43 #1039](https://github.com/sesquicadaver/MTDirector/issues/1039) **DONE**  
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

## Inventory evidence (W7-316 @ `main` `94f04744`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` | `MapHealthChecks` `/health/live` + `/health/ready` + gRPC health | No Prometheus `/metrics` / OTel meter export |
| `installation.md` / packaging doc | HTTP probe guidance | No metrics scrape guidance |
| Glob / rg | `OpenTelemetry` / `MapPrometheusScrapingEndpoint` / `/metrics` absent under Controller @ `94f04744` | Confirmed |
| Packaging unit | `Type=simple` + journald + HTTP probes documented | Metrics residual; Type=notify deferred |
| `Directory.Packages.props` / Controller csproj | Grpc + EF Core only — no OpenTelemetry / prometheus-net packages | Confirmed |

**Ranking decision:** Prefer **ONE atomic row** (**CTRL-HTTP-METRICS-01**) covering a minimal correct scrapeable metrics endpoint (prefer ASP.NET OpenTelemetry Prometheus `/metrics` via `MapPrometheusScrapingEndpoint`, opt-in / fail-closed default-off) + docs/Living Spec alongside existing HTTP/gRPC health. Splitting meters vs scrape path into two ranks would be vanity; full distributed tracing and Type=notify remain deferred adjacent residuals.

## Ranked Controller metrics tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-HTTP-METRICS-01** | Author minimal correct metrics endpoint(s) (+ docs/Living Spec) alongside existing HTTP/gRPC health; keep MSI/AppImage locked; prefer opt-in scrape surface | Health probes only @ `94f04744` | implement **W7-318 (#1042)** after seed **W7-317 (#1040)** |

Inventory (**W7-316 DONE**) confirmed sole rank. Seed **W7-317** advances NEXT to CTRL-HTTP-METRICS-01 implement; COMPLETE seed opens after METRICS-01.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-42 CLOSED)

PLAN-42 sole ranked row (**CTRL-HTTP-HEALTH-01**) is **DONE**. No further PLAN-42 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Full OpenTelemetry distributed tracing beyond metrics scrape — seeded as **PLAN-44** [`plan-44-controller-otel-tracing.md`](plan-44-controller-otel-tracing.md)  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-42 COMPLETE** (W7-314 CTRL-HTTP-HEALTH-01; seed **W7-315 DONE**).  
2. **W7-316 DONE** — PLAN-43 inventory; opened **W7-318 (#1042)** CTRL-HTTP-METRICS-01 implement.  
3. **W7-317 DONE** — seed advanced NEXT to CTRL-HTTP-METRICS-01; opened COMPLETE **W7-319 (#1044)**.  
4. **W7-318 DONE** — sole CTRL-HTTP-METRICS-01 shipped (opt-in `/metrics` + docs/Living Spec).
5. **W7-319 DONE** — PLAN-43 COMPLETE; seeded PLAN-44 inventory **W7-320**.

## §3.C NEXT

**§3.C NEXT = W7-351 (#1107)** — PLAN-44 Inventory Controller OpenTelemetry tracing after PLAN-43.
