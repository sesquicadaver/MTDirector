# PLAN-56 — VRRP pair status fault text after capture progress fault correlation

**Date:** 2026-09-19 (**PLAN-56 COMPLETE**)  
**Status:** **PLAN-56 COMPLETE** — Inventory **DONE** (W7-368); seed **W7-369 (#1144) DONE**; implement **W7-370 (#1146) DONE**; COMPLETE seed **W7-371 (#1147) DONE**; successor **PLAN-57 COMPLETE**; **PLAN-58** inventory **W7-376 (#1159) DONE**; seed **W7-377 (#1160) DONE**; implement **W7-378 (#1162) DONE**; COMPLETE seed **W7-379 (#1163) DONE**; successor **PLAN-59** inventory **W7-380 (#1167) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-368 / PLAN-56 #1143](https://github.com/sesquicadaver/MTDirector/issues/1143) **DONE**  
**Predecessor:** PLAN-55 Capture progress fault correlation **COMPLETE** (SNAP-FAULT-CORR-01)  
**Normative files:** `NodeDetailViewModel`, `DesktopRpcFaultText`, operator docs  
**Normative prior locks:** SNAP-FAULT-CORR-01; DESK-CONN-FAULT-01; CTRL-ERRDETAIL-LOG-01 (event 5301); DESK-RPC-FAULT-01; unary deadline; Watch streams unbounded; MSGSIZE / BODY / KEEPALIVE / MINRATE; HTTP health; metrics; tracing; log↔trace correlation — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-55 joined capture progress to the RPC fault correlation id: VRRP pair status still drops it. `NodeDetailViewModel.ValidateVrrpPairInternalAsync` sets `ErrorText` from `DesktopRpcFaultText.Format` on `RpcException`, then sets `VrrpPairStatusText` to the static sentence `VRRP pair consistency failed.`. The Watch loop writes `{member}: {stage}` and never reads `ErrorDetail`. Operators watching the pair status line cannot join a failed node capture to journald even though ErrorText already has the code and correlation id. Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. The VRRP pair status line operators watch should show the same code and correlation id already on `ErrorText`.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not re-open SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, DESK-RPC-FAULT-01, or PLAN-47…51 transport / deadline polish.  
5. Avoid vanity Desktop a11y and systemd Type=notify.

## Out of scope (do not seed)

- Re-opening SNAP-FAULT-CORR-01 / DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01 / DESK-GRPC-DEADLINE-01 / MSGSIZE / BODY / KEEPALIVE / MINRATE  
- Native MSI / AppImage (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- systemd `Type=notify` / `WatchdogSec`  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Changing the `mfc-error-detail-bin` trailer contract or the event 5301 log line  
- Onboarding / Deployment progress (`ErrorCode` only; proto has no `ErrorDetail`)  
- Health-timeout and TLS connection text  
- Generic capture `catch` rethrows and `Unwrap` (no pair-status line)  
- Other panels' static `StatusText` (`Drift` / `Audit` / `Incident` / `Routing assurance` / `GetNodeWorkflow`) — same class of drop, but not the pair status line this tranche ranked

## Inventory evidence (W7-368 @ `main` `dcde8dca`; seed baseline @ `f5560b4c`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `ValidateVrrpPairInternalAsync` `RpcException` | `ErrorText = DesktopRpcFaultText.Format(ex)` | Correlation id is on ErrorText |
| Same catch | `VrrpPairStatusText = "VRRP pair consistency failed."` | Status line drops code and correlation id |
| Same method `Exception` catch | Same static status sentence; `ErrorText = ex.Message` | Non-RPC failures have no trailer / event 5301 |
| Watch loop | `{memberName}: {progress.Stage}` | `ErrorDetail` (shared capture id after SNAP-FAULT-CORR-01) is not shown |
| `NodeDetailViewModel` | **0** `correlation` mentions | Pair status cannot be joined to journald |

Confirmed on `main` `dcde8dca`: **2** assignments of `VrrpPairStatusText = "VRRP pair consistency failed."`; the `RpcException` catch already calls `DesktopRpcFaultText.Format`; a case-insensitive search of `NodeDetailViewModel` finds **0** `correlation` mentions. `GetNodeWorkflow` uses the same helper for `ErrorText` and a separate static `DeploymentReadinessText` — not this row.

**Ranking decision:** Prefer **ONE atomic row** (**DESK-VRRP-FAULT-01**):

- On the `RpcException` catch, set `VrrpPairStatusText` from the same `DesktopRpcFaultText.Format` text already assigned to `ErrorText` (code, correlation id, retryable)
- Do **not** change the `mfc-error-detail-bin` trailer contract or the event 5301 log template
- Do **not** regress SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, DESK-RPC-FAULT-01, unary deadlines, MSGSIZE / BODY / KEEPALIVE / MINRATE, health, metrics, or tracing
- Living Spec + operator docs

Splitting the two catch blocks would be vanity: only the `RpcException` path has a trailer and event 5301. The non-RPC `Exception` catch has no correlation id to show. The Watch loop `{member}: {stage}` drop is real but smaller than the consistency-failure line that already has the id on `ErrorText` in the same method; it stays an adjacent residual.

## Ranked tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-VRRP-FAULT-01** | Show the RPC fault code and correlation id on the VRRP pair status line + Living Spec | **2** static `VRRP pair consistency failed.` assignments; **0** `correlation` mentions in `NodeDetailViewModel` @ `dcde8dca` | after inventory **W7-368 DONE**; seed **W7-369 (#1144) DONE**; implement **W7-370 (#1146) DONE**; COMPLETE **W7-371 (#1147) DONE** |

Inventory (**W7-368 DONE**) confirmed sole rank. Seed **W7-369** advances NEXT to DESK-VRRP-FAULT-01 after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-55 CLOSED)

PLAN-55 sole ranked row (**SNAP-FAULT-CORR-01**) is **DONE**. No further PLAN-55 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl / nested ListBox a11y — deferred vanity  
- Native MSI / AppImage — W7-22 lock  
- systemd Type=notify — deferred packaging polish  
- Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail` — adding a correlation id would change the progress proto; smaller than the pair-status drop, which already has the id on `ErrorText`  
- Health-timeout and TLS connection text are not `ErrorDetail` trailers  
- Generic capture `catch` rethrows — no `ToRpcException`, so no event 5301  
- `Unwrap` mints its own id — no capture-progress publish  
- VRRP Watch loop writes `{member}: {stage}` and the incomplete-capture throw is a correlation-less `InvalidOperationException`  
- Other Desktop panels set a static `StatusText` / `DeploymentReadinessText` beside `DesktopRpcFaultText.Format` (`Drift`, `Audit`, `Incident`, `Routing assurance`, `GetNodeWorkflow`)

## §3.C ordering

1. **PLAN-55 COMPLETE** (W7-366 SNAP-FAULT-CORR-01; seed **W7-367 DONE**).  
2. **W7-368 DONE** — PLAN-56 inventory; opened **W7-370 (#1146)** DESK-VRRP-FAULT-01 implement + **W7-371 (#1147)** COMPLETE follow-up.  
3. **W7-369 (#1144) DONE** — seed advanced NEXT to DESK-VRRP-FAULT-01; keep COMPLETE **W7-371** open.  
4. **W7-370 (#1146) DONE** — DESK-VRRP-FAULT-01 copies `DesktopRpcFaultText.Format` onto the VRRP pair status line.  
5. **W7-371 (#1147) DONE** — PLAN-56 COMPLETE; successor **PLAN-57** inventory **W7-372 (#1151)**.

## Delivery notes (W7-370)

`ValidateVrrpPairInternalAsync` assigns `DesktopRpcFaultText.Format` once on `RpcException` and uses that string for both `ErrorText` and `VrrpPairStatusText` (`VRRP pair consistency failed. {fault}`). When `mfc-error-detail-bin` is present, the pair status line shows the same code and correlation id as `ErrorText` and journald event **5301**. The non-RPC `Exception` catch still uses the static sentence because it has no trailer. The Watch loop, trailer contract, event 5301 template, unary deadlines, and SNAP-FAULT-CORR-01 / DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01 locks are unchanged.

## §3.C NEXT

**§3.C NEXT = W7-409 (#1218)** — PLAN-57 Inventory VRRP capture-progress fault text.
