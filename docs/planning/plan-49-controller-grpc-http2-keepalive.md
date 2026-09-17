# PLAN-49 — Controller/Desktop gRPC HTTP/2 keepalive after Kestrel body limits

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-340); seed **W7-341 (#1088) OPEN**; predecessor **PLAN-48 COMPLETE**  
**PLAN issue / queue:** [W7-340 / PLAN-49 #1087](https://github.com/sesquicadaver/MTDirector/issues/1087) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-48 Controller Kestrel request-body / HTTP2 limits **COMPLETE** (CTRL-KESTREL-BODY-01)  
**Normative files:** [`Program.cs`](../../src/Mfc.Controller/Program.cs) (`ConfigureKestrel`), [`DesktopGrpcHttpHandlerFactory.cs`](../../src/Mfc.Desktop/Services/DesktopGrpcHttpHandlerFactory.cs), [`ControllerConnectionService.cs`](../../src/Mfc.Desktop/Services/ControllerConnectionService.cs), [`installation.md`](../operations/installation.md) / [`controller-configuration.md`](../operations/controller-configuration.md)  
**Normative prior locks:** HTTP health; opt-in `/metrics`; opt-in tracing; log correlation; OTel resource identity; gRPC MaxReceive/SendMessageSize (256 MiB); Kestrel MaxRequestBodySize (256 MiB); gRPC health; QG-SIGN-01/02; PLAN-32…48 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-48 shipped Kestrel MaxRequestBodySize aligned with `GrpcTransportLimits.MaxMessageBytes`: size ceilings are complete, but long-lived Capture/Deployment/Onboarding **Watch** streams still rely on default HTTP/2 idle behavior. Desktop `SocketsHttpHandler` sets `EnableMultipleHttp2Connections` + connect timeout only — no `KeepAlivePingDelay` / `KeepAlivePingTimeout`. Controller Kestrel leaves HTTP/2 keep-alive defaults untouched. Idle NAT/LB paths can drop watches silently. Observability PLAN-42…46 + MSGSIZE/BODY remain saturating for size/limits; Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. Installed Controller + Desktop should have **minimal correct HTTP/2 keepalive / ping** for long-lived gRPC Watch streams (fail-closed finite intervals).  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-48 BODY / MSGSIZE polish — those waves are **COMPLETE**.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer real transport reliability over systemd Type=notify vanity polish.

## Out of scope (do not seed)

- Re-opening PLAN-48 CTRL-KESTREL-BODY-01 / PLAN-47 MSGSIZE  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Full gRPC load-balancing / multi-instance HA productization  
- systemd `Type=notify` / `WatchdogSec` packaging polish (explicitly deferred / saturating)

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-48 COMPLETE / BODY shipped)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Desktop `DesktopGrpcHttpHandlerFactory` | `EnableMultipleHttp2Connections` + `ConnectTimeout` only | No KeepAlivePingDelay/Timeout |
| Controller `ConfigureKestrel` | Protocols + mTLS + MaxRequestBodySize | No HTTP/2 keep-alive / ping limits |
| Watch hubs | Long-lived Capture/Deployment/Onboarding streams | Idle path drop risk |
| Glob / rg | KeepAlivePing absent under Desktop/Controller gRPC path @ `56093cfa` | Confirmed |

## Ranked Controller gRPC keepalive tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-GRPC-KEEPALIVE-01** | Author minimal correct HTTP/2 keepalive (Controller + Desktop) for Watch longevity + docs/Living Spec; keep MSI/AppImage and Type=notify locked | No KeepAlivePing @ PLAN-48 COMPLETE | after inventory **W7-340**; seed **W7-341 (#1088)** |

Inventory (**W7-340**) may refine ranking and open implement issues; seed **W7-341** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-48 CLOSED)

PLAN-48 sole ranked row (**CTRL-KESTREL-BODY-01**) is **DONE**. No further PLAN-48 product rows. Transport size/limits tranche (MSGSIZE + BODY) is saturating.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-48 COMPLETE** (W7-338 CTRL-KESTREL-BODY-01; seed **W7-339 DONE**).  
2. **W7-340 OPEN** — PLAN-49 inventory → open first keepalive implement + follow-up seeds.  
3. **W7-341 OPEN** — seed first PLAN-49 implement after inventory.  
4. Execute ranked CTRL-GRPC-KEEPALIVE row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-340 (#1087)** — PLAN-49 Inventory Controller/Desktop gRPC HTTP/2 keepalive after PLAN-48.
