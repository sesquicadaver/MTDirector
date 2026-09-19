# PLAN-54 — Desktop connection-status fault text after Controller fault-correlation logging

**Date:** 2026-09-19 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-360); seed **W7-361 (#1128) OPEN**; predecessor **PLAN-53 COMPLETE**  
**PLAN issue / queue:** [W7-360 / PLAN-54 #1127](https://github.com/sesquicadaver/MTDirector/issues/1127) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-53 Controller fault-correlation logging **COMPLETE** (CTRL-ERRDETAIL-LOG-01)  
**Normative files:** `ControllerConnectionService`, `DesktopRpcFaultText`, operator docs  
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

## Inventory evidence (seed baseline @ `470696e9`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `ControllerConnectionService` | **2** `SetState(AuthenticationFailed, ex.Status.Detail)` (connect + reconnect) | Correlation id dropped |
| Same file | **0** `DesktopRpcFaultText` references | Helper not used |
| ViewModels | **14** `DesktopRpcFaultText.Format` | Operator panels already joined |
| Controller log | Event **5301** `correlation_id` | Journald has the id; shell status does not |

## Ranked tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-CONN-FAULT-01** | Map connection `AuthenticationFailed` `RpcException`s through `DesktopRpcFaultText` + Living Spec | **2** `Status.Detail` sites; **0** helper uses @ `470696e9` | after inventory **W7-360**; seed **W7-361 (#1128)** |

Inventory (**W7-360**) may refine ranking and open the implement issue; seed **W7-361** advances NEXT to that implement after inventory DONE.

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
2. **W7-360 OPEN** — PLAN-54 inventory (**§3.C NEXT**).  
3. **W7-361 OPEN** — seed first PLAN-54 implement after inventory.  
4. Execute ranked DESK-CONN-FAULT-01 atomically.

## §3.C NEXT

**§3.C NEXT = W7-360 (#1127)** — PLAN-54 Inventory Desktop connection-status fault text.
