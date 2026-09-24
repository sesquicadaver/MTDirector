# PLAN-55 — Capture progress fault correlation after connection-status fault text

**Date:** 2026-09-19 (**PLAN-55 COMPLETE**)  
**Status:** **PLAN-55 COMPLETE** — Inventory **DONE** (W7-364); seed **W7-365 (#1136) DONE**; implement **W7-366 (#1138) DONE**; COMPLETE seed **W7-367 (#1139) DONE**; successor **PLAN-56** inventory **W7-368 (#1143) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-364 / PLAN-55 #1135](https://github.com/sesquicadaver/MTDirector/issues/1135) **DONE**  
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
- Generic `catch` rethrows that do not call `ToRpcException` (no event 5301)

## Inventory evidence (W7-364 @ `main` `ded885b8`; seed baseline @ `d848a58c`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `SnapshotGrpcService` | **4** `CorrelationId = ProtoUuid.FromGuid(Guid.NewGuid())` on progress `ErrorDetail` | Progress id is new |
| Same file | **2** `ToRpcException(result.Error!)` after those publishes (device + node capture) | Mapper mints a second id |
| Same file | **2** generic `catch` publishes an id then bare `throw` | No trailer / event 5301 to join |
| `Unwrap` | **1** `ToRpcException(result.Error!)` with no progress publish | Not a capture-progress site |
| `FormatCaptureProgress` | `stage: {SanitizedDetail}` | Correlation id not shown |
| Controller log | Event **5301** uses the mapper id | Journald cannot join progress |

### Capture-progress sites (4)

| Method | Behavior |
|--------|----------|
| `StartDeviceCaptureAsync` failure | Publishes a new id, then `ToRpcException(result.Error!)` without it |
| `StartDeviceCaptureAsync` `catch` | Publishes a new id, then rethrows the original exception |
| `StartNodeCaptureAsync` failure | Same split as the device failure path |
| `StartNodeCaptureAsync` `catch` | Same bare rethrow as the device catch |

`GrpcApplicationErrorMapper.ToRpcException(ApplicationError, Guid? correlationId = null)` already accepts an optional id (`correlationId ?? Guid.NewGuid()`) and logs event **5301** with that id. Neither capture throw passes it. `FormatCaptureProgress` returns `stage: {SanitizedDetail}` and never reads `CorrelationId`. `Unwrap` also omits an id, but it does not publish capture progress.

**Ranking decision:** Prefer **ONE atomic row** (**SNAP-FAULT-CORR-01**):

- Mint one `Guid` per capture failure and pass it into both the progress `ErrorDetail` and `ToRpcException` on the two result-failure throws
- Show that correlation id on the Desktop capture progress line when `ErrorDetail.CorrelationId` is present
- Do **not** change the `mfc-error-detail-bin` trailer contract or the event 5301 log template
- Do **not** regress DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, DESK-RPC-FAULT-01, unary deadlines, MSGSIZE / BODY / KEEPALIVE / MINRATE, health, metrics, or tracing
- Living Spec + operator docs

Splitting device vs node would be vanity: both failure throws are the same assignment. The two generic `catch` rethrows do not call `ToRpcException`, so there is no event 5301 to join; wrapping them would change the RPC status and stays out of this row. Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail` correlation. Health-timeout and TLS text are not capture-progress trailers.

## Ranked tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **SNAP-FAULT-CORR-01** | Share one correlation id between capture progress `ErrorDetail` and `ToRpcException`, and show it on the progress line + Living Spec | **4** `Guid.NewGuid()` progress ids; **0** passed into `ToRpcException` @ `ded885b8` | after inventory **W7-364 DONE**; seed **W7-365 (#1136) DONE**; implement **W7-366 (#1138) DONE**; COMPLETE **W7-367 (#1139) DONE** |

Inventory (**W7-364 DONE**) confirmed sole rank. Seed **W7-365** advances NEXT to the capture-correlation implement after inventory DONE.

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
- Generic capture `catch` rethrows — no `ToRpcException`, so no event 5301  
- `Unwrap` mints its own id — no capture-progress publish

## §3.C ordering

1. **PLAN-54 COMPLETE** (W7-362 DESK-CONN-FAULT-01; seed **W7-363 DONE**).  
2. **W7-364 DONE** — PLAN-55 inventory; opened **W7-366 (#1138)** SNAP-FAULT-CORR-01 implement + **W7-367 (#1139)** COMPLETE follow-up.  
3. **W7-365 (#1136) DONE** — seed advanced NEXT to SNAP-FAULT-CORR-01; keep COMPLETE **W7-367** open.  
4. **W7-366 (#1138) DONE** — SNAP-FAULT-CORR-01 shares one correlation id between capture progress and `ToRpcException`, and shows it on the progress line.  
5. **W7-367 (#1139) DONE** — PLAN-55 COMPLETE; successor **PLAN-56** inventory **W7-368 (#1143)**.

## Delivery notes (W7-366)

`SnapshotGrpcService.NewCaptureFailureDetail` mints one `Guid` for each capture-progress `ErrorDetail`. The device and node result-failure throws pass that id into `ToRpcException`, so `mfc-error-detail-bin` and event **5301** use the same id as Watch progress. `FormatCaptureProgress` appends `(correlation {id})` when the progress detail carries a 16-byte id. If `StartCapture` throws, the Snapshots progress line uses `DesktopRpcFaultText.Format` so the operator line still shows that id. Generic `catch` rethrows, Onboarding/Deployment progress, health-timeout, TLS text, unary deadlines, and the trailer / 5301 templates are unchanged.

## §3.C NEXT

**§3.C NEXT = W7-415 (#1229)** — PLAN-56 Inventory VRRP pair status fault text.
