# PLAN-49 — Controller/Desktop gRPC HTTP/2 keepalive after Kestrel body limits

**Date:** 2026-09-17 (inventory **DONE** @ `be206f6c`)  
**Status:** **PLAN-49 COMPLETE** — Inventory **DONE** (W7-340); seed **W7-341 (#1088) DONE**; implement **W7-342 (#1090) DONE**; COMPLETE seed **W7-343 (#1092) DONE**; successor **PLAN-50** inventory **W7-344 (#1095) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-340 / PLAN-49 #1087](https://github.com/sesquicadaver/MTDirector/issues/1087) **DONE**  
**Predecessor:** PLAN-48 Controller Kestrel request-body / HTTP2 limits **COMPLETE** (CTRL-KESTREL-BODY-01)  
**Normative files:** [`Program.cs`](../../src/Mfc.Controller/Program.cs) (`ConfigureKestrel`), [`DesktopGrpcHttpHandlerFactory.cs`](../../src/Mfc.Desktop/Services/DesktopGrpcHttpHandlerFactory.cs), [`ControllerConnectionService.cs`](../../src/Mfc.Desktop/Services/ControllerConnectionService.cs), [`installation.md`](../operations/installation.md) / [`controller-configuration.md`](../operations/controller-configuration.md)  
**Normative prior locks:** HTTP health; opt-in `/metrics`; opt-in tracing; log correlation; OTel resource identity; gRPC MaxReceive/SendMessageSize (256 MiB); Kestrel MaxRequestBodySize (256 MiB); gRPC health; QG-SIGN-01/02; PLAN-32…48 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-48 shipped Kestrel MaxRequestBodySize aligned with `GrpcTransportLimits.MaxMessageBytes`: size ceilings are complete, but long-lived Capture/Deployment/Onboarding **Watch** streams still rely on default HTTP/2 idle behavior. Desktop `SocketsHttpHandler` sets `EnableMultipleHttp2Connections` + connect timeout only — no `KeepAlivePingDelay` / `KeepAlivePingTimeout`. Controller Kestrel leaves HTTP/2 keep-alive defaults untouched (`KeepAlivePingDelay` = `TimeSpan.MaxValue` / effectively off; Sockets = `InfiniteTimeSpan`). Idle NAT/LB paths can drop watches silently. Observability PLAN-42…46 + MSGSIZE/BODY remain saturating for size/limits; Type=notify and Desktop a11y remain deferred vanity.

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
- Tuning `MinRequestBodyDataRate` / connection idle as a substitute for HTTP/2 PING (not the Watch idle gap)

## Inventory evidence (W7-340 @ `main` `be206f6c`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Desktop `DesktopGrpcHttpHandlerFactory` | `EnableMultipleHttp2Connections` + `ConnectTimeout` only | No KeepAlivePingDelay/Timeout |
| Desktop `SocketsHttpHandler` defaults | `KeepAlivePingDelay` = InfiniteTimeSpan (off); Timeout = 20s | Ping never sent |
| Controller `ConfigureKestrel` | Protocols + mTLS + MaxRequestBodySize | No HTTP/2 keep-alive / ping limits |
| Controller `Http2Limits` defaults | `KeepAlivePingDelay` = TimeSpan.MaxValue (off); Timeout = 20s | Server ping never sent |
| Watch hubs | Long-lived Capture/Deployment/Onboarding streams | Idle path drop risk |
| Glob / rg | KeepAlivePing absent under Desktop/Controller gRPC path @ `be206f6c` | Confirmed |

**Ranking decision:** Prefer **ONE atomic row** (**CTRL-GRPC-KEEPALIVE-01**) covering minimal correct HTTP/2 keepalive on both sides:

- Shared Contracts constants: **KeepAlivePingDelay = 60s**, **KeepAlivePingTimeout = 30s** (finite fail-closed; not MaxValue/Infinite)
- Desktop: set on `SocketsHttpHandler` in `DesktopGrpcHttpHandlerFactory`
- Controller: set on `kestrel.Limits.Http2.KeepAlivePingDelay` / `KeepAlivePingTimeout`
- Do **not** rely on `MinRequestBodyDataRate` as the Watch idle fix (HTTP/2 PING is the correct layer)
- Keep MSI/AppImage locked; do not regress MSGSIZE/BODY/health/metrics/tracing/correlation/resource
- Docs (`installation.md` / `controller-configuration.md`) + Living Spec locking the configured intervals

Splitting Controller vs Desktop keepalive into two ranks would be vanity; Type=notify and Desktop a11y remain deferred adjacent residuals.

## Ranked Controller/Desktop gRPC keepalive tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-GRPC-KEEPALIVE-01** | Author minimal correct HTTP/2 keepalive (Controller + Desktop) for Watch longevity + docs/Living Spec; keep MSI/AppImage and Type=notify locked | No KeepAlivePing @ `be206f6c` (Kestrel MaxValue / Sockets Infinite) | implement **W7-342 (#1090) DONE**; COMPLETE **W7-343 (#1092) DONE**; seed **W7-341 (#1088) DONE** |

Inventory (**W7-340 DONE**) confirmed sole rank. Seed **W7-341 DONE** advanced NEXT to the KEEPALIVE implement; COMPLETE follow-up **W7-343 DONE**.

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
2. **W7-340 DONE** — PLAN-49 inventory; opened **W7-342 (#1090)** CTRL-GRPC-KEEPALIVE-01 implement.  
3. **W7-341 DONE** — seed advanced NEXT to CTRL-GRPC-KEEPALIVE-01; opened COMPLETE **W7-343 (#1092)**.  
4. **W7-342 DONE** — sole CTRL-GRPC-KEEPALIVE-01 shipped.  
5. **W7-343 DONE** — PLAN-49 COMPLETE; seeded PLAN-50 inventory **W7-344**.

## Delivery notes (W7-342)

Controller `ConfigureKestrel` sets `Limits.Http2.KeepAlivePingDelay` / `KeepAlivePingTimeout` and Desktop `SocketsHttpHandler` uses shared `GrpcHttp2KeepAlive` (**PingDelay = 60s**, **PingTimeout = 30s**). Finite fail-closed; never MaxValue/Infinite. Docs: `installation.md` §11 + `controller-configuration.md`. MSGSIZE/BODY/health/metrics/tracing/correlation/resource unchanged.

## Adjacent residual seeded as PLAN-50

- Controller Kestrel MinRequestBodyDataRate / MinResponseDataRate for quiet Watch streams — seeded as **PLAN-50** [`plan-50-controller-kestrel-min-data-rate.md`](plan-50-controller-kestrel-min-data-rate.md)

## §3.C NEXT

**§3.C NEXT = W7-384 (#1175)** — PLAN-50 Inventory Controller Kestrel min request/response data-rate after PLAN-49.
