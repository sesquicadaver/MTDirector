# PLAN-55 — Capture progress fault correlation after connection-status fault text

**Date:** 2026-09-19 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-364); seed **W7-365 (#1136) OPEN**; predecessor **PLAN-54 COMPLETE**  
**PLAN issue / queue:** [W7-364 / PLAN-55 #1135](https://github.com/sesquicadaver/MTDirector/issues/1135) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-54 Desktop connection-status fault text **COMPLETE** (DESK-CONN-FAULT-01)  
**Normative files:** `SnapshotGrpcService`, `SnapshotViewerViewModel`, `GrpcApplicationErrorMapper`, operator docs  
**Normative prior locks:** DESK-CONN-FAULT-01; CTRL-ERRDETAIL-LOG-01 (event 5301); DESK-RPC-FAULT-01; unary deadline; Watch streams unbounded; MSGSIZE / BODY / KEEPALIVE / MINRATE; HTTP health; metrics; tracing; log↔trace correlation — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-54 joined shell auth status to the fault correlation id: capture progress still mints a different id. `SnapshotGrpcService` publishes `ErrorDetail.CorrelationId = Guid.NewGuid()` and then `ToRpcException` mints another, so Watch progress cannot be joined to journald or to Desktop `ErrorText`. `FormatCaptureProgress` drops the id entirely. Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. One capture failure should have one correlation id in progress, the RPC trailer, and event 5301.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not re-open DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, DESK-RPC-FAULT-01, or PLAN-47…51 transport / deadline polish.  
5. Avoid vanity Desktop a11y and systemd Type=notify.

## Out of scope (do not seed)

- Re-opening DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01 / DESK-GRPC-DEADLINE-01 / MSGSIZE / BODY / KEEPALIVE / MINRATE  
- Native MSI / AppImage (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- systemd `Type=notify` / `WatchdogSec`  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Changing the `mfc-error-detail-bin` trailer contract or the event 5301 log line  
- Onboarding / Deployment progress (`ErrorCode` only; no `ErrorDetail` correlation)  
- Health-timeout and TLS connection text

## Inventory evidence (seed baseline @ `d848a58c`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `SnapshotGrpcService` | **4** `CorrelationId = ProtoUuid.FromGuid(Guid.NewGuid())` on progress `ErrorDetail` | Progress id is new |
| Same file | **2** `ToRpcException(result.Error!)` after those publishes | Mapper mints a second id |
| Same file | **2** generic `catch` publishes an id then `throw` | Progress id never reaches the trailer |
| `FormatCaptureProgress` | `stage: {SanitizedDetail}` | Correlation id not shown |
| Controller log | Event **5301** uses the mapper id | Journald cannot join progress |

## Ranked tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **SNAP-FAULT-CORR-01** | Share one correlation id between capture progress `ErrorDetail` and `ToRpcException`, and show it on the progress line + Living Spec | **4** `Guid.NewGuid()` progress ids; **0** passed into `ToRpcException` @ `d848a58c` | after inventory **W7-364**; seed **W7-365 (#1136)** |

Inventory (**W7-364**) may refine ranking and open the implement issue; seed **W7-365** advances NEXT to that implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-54 CLOSED)

PLAN-54 sole ranked row (**DESK-CONN-FAULT-01**) is **DONE**. No further PLAN-54 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl / nested ListBox a11y — deferred vanity  
- Native MSI / AppImage — W7-22 lock  
- systemd Type=notify — deferred packaging polish  
- Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail` correlation — smaller than the capture split  
- Health-timeout and TLS connection text are not `ErrorDetail` trailers

## §3.C ordering

1. **PLAN-54 COMPLETE** (W7-362 DESK-CONN-FAULT-01; seed **W7-363 DONE**).  
2. **W7-364 OPEN** — PLAN-55 inventory (**§3.C NEXT**).  
3. **W7-365 OPEN** — seed first PLAN-55 implement after inventory.  
4. Execute ranked SNAP-FAULT-CORR-01 atomically.

## §3.C NEXT

**§3.C NEXT = W7-364 (#1135)** — PLAN-55 Inventory capture progress fault correlation.
