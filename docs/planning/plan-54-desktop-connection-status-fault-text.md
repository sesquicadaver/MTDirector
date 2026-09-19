# PLAN-54 — Desktop connection-status fault text after Controller fault-correlation logging

**Date:** 2026-09-19 (inventory **DONE** @ `e51073f6`)  
**Status:** Inventory **DONE** (W7-360); seed **W7-361 (#1128) DONE**; implement **W7-362 (#1130) DONE**; COMPLETE seed **W7-363 (#1131) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-360 / PLAN-54 #1127](https://github.com/sesquicadaver/MTDirector/issues/1127) **DONE**  
**Predecessor:** PLAN-53 Controller fault-correlation logging **COMPLETE** (CTRL-ERRDETAIL-LOG-01)  
**Normative files:** `ControllerConnectionService`, `DesktopRpcFaultText`, `ShellViewModel`, operator docs  
**Normative prior locks:** CTRL-ERRDETAIL-LOG-01 (event 5301); DESK-RPC-FAULT-01; unary deadline; Watch streams unbounded; MSGSIZE / BODY / KEEPALIVE / MINRATE; HTTP health; metrics; tracing; log↔trace correlation — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-53 logged the fault correlation id: connect and reconnect still drop it. `ControllerConnectionService` sets `AuthenticationFailed` from `ex.Status.Detail`, so the shell status operators see first cannot be joined to journald even though ViewModels already use `DesktopRpcFaultText`. Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. Auth-failure shell status should show the same code + correlation id ViewModels already show, via the existing helper.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not re-open CTRL-ERRDETAIL-LOG-01, DESK-RPC-FAULT-01, or PLAN-47…51 transport / deadline polish.  
5. Avoid vanity Desktop a11y and systemd Type=notify.

## Out of scope (do not seed)

- Re-opening CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01 / DESK-GRPC-DEADLINE-01 / MSGSIZE / BODY / KEEPALIVE / MINRATE  
- Native MSI / AppImage (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- systemd `Type=notify` / `WatchdogSec`  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Changing the `mfc-error-detail-bin` trailer contract or the event 5301 log line  
- Health-timeout and TLS connection text (not `ErrorDetail` trailers)

## Inventory evidence (W7-360 @ `main` `e51073f6`; seed baseline @ `470696e9`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `ControllerConnectionService` | **2** `SetState(AuthenticationFailed, ex.Status.Detail)` (connect + reconnect/health probe) | Correlation id dropped |
| Same file | **0** `DesktopRpcFaultText` references | Helper not used |
| `ShellViewModel` | `ErrorText = _connection.LastError` | Shell shows the dropped detail |
| ViewModels | **14** `DesktopRpcFaultText.Format` | Operator panels already joined |
| Controller log | Event **5301** `correlation_id` | Journald has the id; shell status does not |

### `AuthenticationFailed` sites (2)

| Method | Behavior |
|--------|----------|
| `ConnectCoreAsync` | Unauthenticated / PermissionDenied → `SetState(..., ex.Status.Detail)` |
| `ProbeConnectedHealthOrLeaveAsync` | Same assignment on the reconnect/health path |

`DesktopRpcFaultText.Format` already reads `mfc-error-detail-bin` and falls back to a non-empty status code when `Status.Detail` is empty. Connection status does not call it, so an empty detail becomes an empty `LastError`.

**Ranking decision:** Prefer **ONE atomic row** (**DESK-CONN-FAULT-01**):

- Route both `AuthenticationFailed` `RpcException`s through `DesktopRpcFaultText.Format` (or equivalent) so `LastError` includes the correlation id when the trailer is present
- Never leave `LastError` empty when `Status.Detail` is empty
- Do **not** change the `mfc-error-detail-bin` trailer contract or event 5301
- Do **not** regress CTRL-ERRDETAIL-LOG-01, DESK-RPC-FAULT-01, unary deadlines, MSGSIZE / BODY / KEEPALIVE / MINRATE, health, metrics, or tracing
- Living Spec + operator docs

Splitting connect vs reconnect would be vanity: both sites are the same assignment. Health-timeout and TLS text are not `ErrorDetail` trailers and stay smaller than these two auth sites.

## Ranked tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-CONN-FAULT-01** | Map connection `AuthenticationFailed` `RpcException`s through `DesktopRpcFaultText` + Living Spec | **2** `Status.Detail` sites; **0** helper uses @ `e51073f6` | after inventory **W7-360 DONE**; seed **W7-361 (#1128) DONE**; implement **W7-362 (#1130) DONE**; COMPLETE **W7-363 (#1131) OPEN** (**§3.C NEXT**) |

Inventory (**W7-360 DONE**) confirmed sole rank. Seed **W7-361** advances NEXT to the connection-status implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-53 CLOSED)

PLAN-53 sole ranked row (**CTRL-ERRDETAIL-LOG-01**) is **DONE**. No further PLAN-53 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl / nested ListBox a11y — deferred vanity  
- Native MSI / AppImage — W7-22 lock  
- systemd Type=notify — deferred packaging polish  
- Health-timeout and TLS connection text are not `ErrorDetail` trailers — smaller than the auth sites that already carry the trailer

## §3.C ordering

1. **PLAN-53 COMPLETE** (W7-358 CTRL-ERRDETAIL-LOG-01; seed **W7-359 DONE**).  
2. **W7-360 DONE** — PLAN-54 inventory; opened **W7-362 (#1130)** DESK-CONN-FAULT-01 implement + **W7-363 (#1131)** COMPLETE follow-up.  
3. **W7-361 (#1128) DONE** — seed advanced NEXT to DESK-CONN-FAULT-01; keep COMPLETE **W7-363** open.  
4. **W7-362 (#1130) DONE** — DESK-CONN-FAULT-01 routes both `AuthenticationFailed` sites through `DesktopRpcFaultText.Format`.  
5. **W7-363 OPEN** — PLAN-54 COMPLETE → seed PLAN-55 (**§3.C NEXT**).

## Delivery notes (W7-362)

`ControllerConnectionService` sets `AuthenticationFailed` `LastError` via `DesktopRpcFaultText.Format` on connect and on the reconnect/health probe. When `mfc-error-detail-bin` is present, shell `ErrorText` includes the same code and correlation id ViewModels already show. An empty `Status.Detail` falls back to the status code and is never blank. Health-timeout text, TLS `ex.Message`, unary deadlines, event 5301, and the trailer contract are unchanged.

## §3.C NEXT

**§3.C NEXT = W7-363 (#1131)** — Seed next after DESK-CONN-FAULT-01 (PLAN-54 COMPLETE).
