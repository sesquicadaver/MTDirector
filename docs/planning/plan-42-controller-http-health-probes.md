# PLAN-42 — Controller HTTP liveness/readiness probes (beyond gRPC health)

**Date:** 2026-09-17 (inventory **DONE** @ `ad3718cb`)  
**Status:** **PLAN-42 COMPLETE** — Inventory **DONE** (W7-312); seed **W7-313 (#1032) DONE**; implement **W7-314 (#1034) DONE**; COMPLETE seed **W7-315 (#1036) DONE**; successor **PLAN-43** inventory **W7-316 (#1039) DONE**; seed **W7-317 (#1040) DONE**; implement **W7-318 (#1042) DONE**; COMPLETE seed **W7-319 (#1044) DONE**; successor **PLAN-44** inventory **W7-320 (#1047) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-312 / PLAN-42 #1031](https://github.com/sesquicadaver/MTDirector/issues/1031) **DONE**  
**Predecessor:** PLAN-41 Release signing crypto **COMPLETE** (QG-SIGN-02)  
**Normative files:** [`Program.cs`](../../src/Mfc.Controller/Program.cs), [`installation.md`](../operations/installation.md), [`packaging/doc/mfc/README.md`](../../packaging/doc/mfc/README.md)  
**Normative prior locks:** gRPC health (`MapGrpcHealthChecksService`); QG-SIGN-01/02; PLAN-32…41 packaging/signing — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-41 shipped opt-in release signing crypto: Controller process health is **gRPC-only** (`AddGrpcHealthChecks` + `MapGrpcHealthChecksService`). Operators, reverse proxies, and classic HTTP probes cannot verify liveness/readiness without a gRPC health client — despite a complete systemd host unit with journald identity.

## Principles

1. Installed Controller hosts should expose **HTTP liveness/readiness** probes usable by ops tooling without a gRPC client, while keeping existing gRPC health.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-41 QG-SIGN / packaging host-unit polish — those waves are **COMPLETE** / saturating.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer operator observability/probes over another publish-tree packaging vanity row.

## Out of scope (do not seed)

- Re-opening PLAN-41 QG-SIGN-02 / mandatory org-key CI  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Full OpenTelemetry/metrics stack (inventory may defer)  
- systemd `Type=notify` / `WatchdogSec` packaging polish (explicitly deferred / saturating)

## Inventory evidence (W7-312 @ `main` `ad3718cb`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` | `AddGrpcHealthChecks` + `MapGrpcHealthChecksService` only; Kestrel `HttpProtocols.Http2` | No ASP.NET `MapHealthChecks` / HTTP `/health/live` + `/health/ready`; classic HTTP/1.1 probes cannot speak Http2-only |
| `installation.md` | “verify gRPC health” | No HTTP probe guidance for operators/LBs |
| `packaging/doc/mfc/README.md` | journald identity docs; no HTTP probes | Operators lack curl/LB probe paths |
| Glob / rg | HTTP `/health` / `MapHealthChecks` absent under Controller @ `ad3718cb` | Confirmed |
| Packaging unit | `Type=simple` + journald identity | No HTTP probe docs; Type=notify deferred |

**Ranking decision:** Prefer **ONE atomic row** (**CTRL-HTTP-HEALTH-01**) covering `/health/live` + `/health/ready` (+ Http1AndHttp2 as needed) + docs/Living Spec alongside existing gRPC health. Splitting live vs ready into two ranks would be vanity; metrics/OTel and Type=notify remain deferred adjacent residuals.

## Ranked Controller HTTP health tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-HTTP-HEALTH-01** | Author HTTP liveness/readiness endpoints (+ docs/Living Spec) alongside existing gRPC health; keep MSI/AppImage locked; fail-closed readiness when required deps unavailable | gRPC-only health + Http2-only Kestrel @ `ad3718cb` | implement **W7-314 (#1034)** after seed **W7-313 (#1032)** |

Inventory (**W7-312 DONE**) confirmed sole rank. Seed **W7-313** advances NEXT to CTRL-HTTP-HEALTH-01 implement; COMPLETE seed opens after HEALTH-01.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-41 CLOSED)

PLAN-41 sole ranked row (**QG-SIGN-02**) is **DONE**. No further PLAN-41 product rows. Packaging host-unit polish remains saturated.

## Adjacent residuals (seeded as PLAN-42 COMPLETE / PLAN-43)

- Controller metrics / OpenTelemetry beyond HTTP health — **PLAN-43** [`plan-43-controller-http-metrics-otel.md`](plan-43-controller-http-metrics-otel.md)

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- Mandatory org-key CI signing on every GitHub Release — future ops (QG-SIGN-02 opt-in already shipped)  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-41 COMPLETE** (W7-310 QG-SIGN-02; seed **W7-311 DONE**).  
2. **W7-312 DONE** — PLAN-42 inventory; opened **W7-314 (#1034)** CTRL-HTTP-HEALTH-01 implement.  
3. **W7-313 DONE** — seed advanced NEXT to CTRL-HTTP-HEALTH-01; opened COMPLETE **W7-315 (#1036)**.  
4. **W7-314 DONE** — sole CTRL-HTTP-HEALTH-01 shipped (HTTP live/ready + docs/Living Spec).
5. **W7-315 DONE** — PLAN-42 COMPLETE; seeded PLAN-43 inventory **W7-316**.

## §3.C NEXT

**§3.C NEXT = W7-423 (#1242)** — PLAN-44 Inventory Controller OpenTelemetry tracing after PLAN-43.
