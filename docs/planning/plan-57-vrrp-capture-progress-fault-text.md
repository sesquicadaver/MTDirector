# PLAN-57 — VRRP capture-progress fault text after pair-status fault text

**Date:** 2026-09-19 (**PLAN-57 COMPLETE**)  
**Status:** **PLAN-57 COMPLETE** — Inventory **DONE** (W7-372); seed **W7-373 (#1152) DONE**; implement **W7-374 (#1154) DONE**; COMPLETE seed **W7-375 (#1155) DONE**; successor **PLAN-58** inventory **W7-376 (#1159) DONE**; seed **W7-377 (#1160) DONE**; implement **W7-378 (#1162) DONE**; COMPLETE seed **W7-379 (#1163) DONE**; successor **PLAN-59** inventory **W7-380 (#1167) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-372 / PLAN-57 #1151](https://github.com/sesquicadaver/MTDirector/issues/1151) **DONE**  
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

## Inventory evidence (W7-372 @ `main` `7dbfe132`; seed baseline @ `218cdba7`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Watch loop | `VrrpPairStatusText = $"{memberName}: {progress.Stage}"` | `progress.Error` is not read (**0** `progress.Error` mentions) |
| Incomplete capture | `throw new InvalidOperationException("Node capture did not complete…")` | Static message; no progress correlation id |
| `Exception` catch | `ErrorText = ex.Message` and static pair status | Both lines lose the id |
| `RpcException` catch | `VrrpPairStatusText = $"VRRP pair consistency failed. {fault}"` | DESK-VRRP-FAULT-01 already joined RPC faults |
| `FormatCaptureProgress` | `(correlation {correlation})` when the progress detail has a 16-byte id | Snapshots already show the id |

Confirmed on `main` `7dbfe132` (DESK-VRRP-FAULT-01 landed at `218cdba7`): **1** assignment of `VrrpPairStatusText = $"{memberName}: {progress.Stage}"`; **0** `progress.Error` reads in `NodeDetailViewModel`; **1** correlation-less `InvalidOperationException("Node capture did not complete successfully for all VRRP members.")`. The `RpcException` catch already copies `DesktopRpcFaultText.Format` onto the pair status line. `SnapshotViewerViewModel.FormatCaptureProgress` already appends `(correlation {correlation})`.

**Ranking decision:** Prefer **ONE atomic row** (**DESK-VRRP-PROG-01**), sole rank:

- When Watch reports `ErrorDetail`, show that progress correlation id on the VRRP pair status line, including the incomplete-capture path, using the same `(correlation {id})` suffix Snapshots use (`FormatCaptureProgress` or equivalent) so operators can join journald event **5301**
- Do **not** change the `mfc-error-detail-bin` trailer contract or the event 5301 log template
- Do **not** regress DESK-VRRP-FAULT-01, SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, or DESK-RPC-FAULT-01
- Living Spec + operator docs

Splitting the live Watch line from the incomplete-capture throw would be vanity: both drop the same id. Other panels' static `StatusText` already have the RPC id on `ErrorText`. Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail`.

## Ranked tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-VRRP-PROG-01** | Show the capture-progress correlation id on the VRRP pair status line when Watch reports `ErrorDetail`, including the incomplete-capture path + Living Spec | **1** stage-only status assignment; **0** `progress.Error` reads; **1** correlation-less `InvalidOperationException` @ `7dbfe132` (seed baseline `218cdba7`) | after inventory **W7-372 DONE**; seed **W7-373 (#1152) DONE**; implement **W7-374 (#1154) OPEN**; COMPLETE **W7-375 (#1155) OPEN** |

Inventory (**W7-372 DONE**) confirmed sole rank. Seed **W7-373** advances NEXT to DESK-VRRP-PROG-01 after inventory DONE.

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
2. **W7-372 DONE** — PLAN-57 inventory; opened **W7-374 (#1154)** DESK-VRRP-PROG-01 implement + **W7-375 (#1155)** COMPLETE follow-up.  
3. **W7-373 (#1152) DONE** — seed advanced NEXT to DESK-VRRP-PROG-01; keep COMPLETE **W7-375** open.  
4. **W7-374 (#1154) DONE** — DESK-VRRP-PROG-01 reuses `FormatCaptureProgress` on the VRRP pair status line, including incomplete capture.  
5. **W7-375 (#1155) DONE** — PLAN-57 COMPLETE; successor **PLAN-58** inventory **W7-376 (#1159)**.

## Delivery notes (W7-374)

`ValidateVrrpPairInternalAsync` sets the Watch pair status to `{member}: {SnapshotViewerViewModel.FormatCaptureProgress(progress)}`. That is the same line Snapshots already render, so a 16-byte progress `ErrorDetail` correlation id appears as `(correlation {id})` and can be joined to journald event **5301**. An incomplete capture throws `InvalidOperationException` whose message includes `FormatCaptureProgress(last)` when a progress frame arrived; the `Exception` catch copies that message onto `VrrpPairStatusText` and `ErrorText`. Other non-RPC failures still use the static sentence. The `RpcException` path remains `DesktopRpcFaultText.Format` (DESK-VRRP-FAULT-01). Trailer contract, event 5301 template, and SNAP-FAULT-CORR-01 / DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01 locks are unchanged.

## §3.C NEXT

**§3.C NEXT = W7-395 (#1197)** — Seed first PLAN-58 atomic row after inventory → DESK-PANEL-FAULT-01.
