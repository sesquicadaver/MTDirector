# PLAN-57 — VRRP capture-progress fault text after pair-status fault text

**Date:** 2026-09-19 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-372); seed **W7-373 (#1152) OPEN**; predecessor **PLAN-56 COMPLETE**  
**PLAN issue / queue:** [W7-372 / PLAN-57 #1151](https://github.com/sesquicadaver/MTDirector/issues/1151) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-56 VRRP pair status fault text **COMPLETE** (DESK-VRRP-FAULT-01)  
**Normative files:** `NodeDetailViewModel`, `SnapshotViewerViewModel`, operator docs  
**Normative prior locks:** DESK-VRRP-FAULT-01; SNAP-FAULT-CORR-01; DESK-CONN-FAULT-01; CTRL-ERRDETAIL-LOG-01 (event 5301); DESK-RPC-FAULT-01; unary deadline; Watch streams unbounded; MSGSIZE / BODY / KEEPALIVE / MINRATE; HTTP health; metrics; tracing; log↔trace correlation — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-56 put `DesktopRpcFaultText` on the VRRP pair status line for `RpcException`: the Watch loop still drops the capture-progress correlation id. `ValidateVrrpPairInternalAsync` writes `{memberName}: {progress.Stage}` and never reads `progress.Error`. When the stream does not complete, it throws `InvalidOperationException` with a static message, so both `ErrorText` and the pair status lose the id SNAP-FAULT-CORR-01 already published. Snapshots already show `(correlation {id})`. Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. The VRRP pair status line operators watch during capture should show the progress correlation id Snapshots already show.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not re-open DESK-VRRP-FAULT-01, SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, DESK-RPC-FAULT-01, or PLAN-47…51 transport / deadline polish.  
5. Avoid vanity Desktop a11y and systemd Type=notify.

## Out of scope (do not seed)

- Re-opening DESK-VRRP-FAULT-01 / SNAP-FAULT-CORR-01 / DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01 / DESK-GRPC-DEADLINE-01 / MSGSIZE / BODY / KEEPALIVE / MINRATE  
- Native MSI / AppImage (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- systemd `Type=notify` / `WatchdogSec`  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Changing the `mfc-error-detail-bin` trailer contract or the event 5301 log line  
- Onboarding / Deployment progress (`ErrorCode` only; proto has no `ErrorDetail`)  
- Health-timeout and TLS connection text  
- Generic capture `catch` rethrows and `Unwrap` (no pair-status line)  
- Other panels' static `StatusText` (`Drift` / `Audit` / `Incident` / `Routing assurance` / `GetNodeWorkflow`) — `ErrorText` already has the RPC id; smaller than a capture line that drops the id from both status and `ErrorText`

## Inventory evidence (seed baseline @ `218cdba7`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Watch loop | `VrrpPairStatusText = $"{memberName}: {progress.Stage}"` | `progress.Error` is not read (**0** `progress.Error` mentions) |
| Incomplete capture | `throw new InvalidOperationException("Node capture did not complete…")` | Static message; no progress correlation id |
| `Exception` catch | `ErrorText = ex.Message` and static pair status | Both lines lose the id |
| `RpcException` catch | `VrrpPairStatusText = $"VRRP pair consistency failed. {fault}"` | DESK-VRRP-FAULT-01 already joined RPC faults |
| `FormatCaptureProgress` | `(correlation {correlation})` when the progress detail has a 16-byte id | Snapshots already show the id |

## Ranked tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-VRRP-PROG-01** | Show the capture-progress correlation id on the VRRP pair status line when Watch reports `ErrorDetail`, including the incomplete-capture path + Living Spec | **1** stage-only status assignment; **0** `progress.Error` reads; **1** correlation-less `InvalidOperationException` @ `218cdba7` | after inventory **W7-372**; seed **W7-373 (#1152)** |

Inventory (**W7-372**) may refine ranking and open the implement issue; seed **W7-373** advances NEXT to that implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-56 CLOSED)

PLAN-56 sole ranked row (**DESK-VRRP-FAULT-01**) is **DONE**. No further PLAN-56 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl / nested ListBox a11y — deferred vanity  
- Native MSI / AppImage — W7-22 lock  
- systemd Type=notify — deferred packaging polish  
- Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail`  
- Health-timeout and TLS connection text are not `ErrorDetail` trailers  
- Generic capture `catch` rethrows — no `ToRpcException`, so no event 5301  
- `Unwrap` mints its own id — no capture-progress publish  
- Other Desktop panels set a static `StatusText` / `DeploymentReadinessText` beside `DesktopRpcFaultText.Format` (`Drift`, `Audit`, `Incident`, `Routing assurance`, `GetNodeWorkflow`) — `ErrorText` already carries the id

## §3.C ordering

1. **PLAN-56 COMPLETE** (W7-370 DESK-VRRP-FAULT-01; seed **W7-371 DONE**).  
2. **W7-372 OPEN** — PLAN-57 inventory (**§3.C NEXT**).  
3. **W7-373 OPEN** — seed first PLAN-57 implement after inventory.  
4. Execute ranked DESK-VRRP-PROG-01 atomically.

## §3.C NEXT

**§3.C NEXT = W7-372 (#1151)** — PLAN-57 Inventory VRRP capture-progress fault text.
