# PLAN-61 — Desktop connection Disconnected RPC fault text after service Error correlation

**Date:** 2026-09-19 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-388); seed **W7-389 (#1184) OPEN**; predecessor **PLAN-60 COMPLETE**  
**PLAN issue / queue:** [W7-388 / PLAN-61 #1183](https://github.com/sesquicadaver/MTDirector/issues/1183) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-60 Desktop service RPC fault text **COMPLETE** (DESK-SVC-FAULT-01)  
**Normative files:** `ControllerConnectionService`, operator docs  
**Normative prior locks:** DESK-SVC-FAULT-01; SNAP-ERRTEXT-CORR-01; DESK-PANEL-FAULT-01; DESK-VRRP-PROG-01; DESK-VRRP-FAULT-01; SNAP-FAULT-CORR-01; DESK-CONN-FAULT-01; CTRL-ERRDETAIL-LOG-01 (event 5301); DESK-RPC-FAULT-01 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-60 stored `DesktopRpcFaultText.Format` on snapshot, diff, and inventory service `Error`: connect and reconnect still set `Disconnected` from `ex.Message`. An `RpcException` that is not `AuthenticationFailed` and not a TLS failure therefore reaches shell status as `RpcException.Message`, which does not include the code and correlation id `DesktopRpcFaultText.Format` already renders. `AuthenticationFailed` already uses that helper (DESK-CONN-FAULT-01). Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. When the connect or reconnect catch that sets `Disconnected` is an `RpcException`, shell status should be `DesktopRpcFaultText.Format` (code + correlation id, or the status fallback). Non-RPC exceptions keep `ex.Message`.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not re-open DESK-SVC-FAULT-01, SNAP-ERRTEXT-CORR-01, DESK-PANEL-FAULT-01, DESK-VRRP-PROG-01, DESK-VRRP-FAULT-01, SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, or DESK-RPC-FAULT-01.  
5. Avoid vanity Desktop a11y and systemd Type=notify.

## Out of scope (do not seed)

- Re-opening DESK-SVC-FAULT-01 / SNAP-ERRTEXT-CORR-01 / DESK-PANEL-FAULT-01 / DESK-VRRP-PROG-01 / DESK-VRRP-FAULT-01 / SNAP-FAULT-CORR-01 / DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01  
- Native MSI / AppImage (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- systemd `Type=notify` / `WatchdogSec`  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Changing the `mfc-error-detail-bin` trailer contract or the event 5301 log line  
- Onboarding / Deployment progress (`ErrorCode` only; proto has no `ErrorDetail`)  
- TLS `TlsError` text (`ex.Message` on certificate failures)  
- The static `Health check timed out.` sentence  
- `Health status:` non-serving text (not an `RpcException`)  

## Inventory evidence (seed baseline @ `33682bb6`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Connect `Disconnected` | `SetState(..., ex.Message)` | non-auth `RpcException` drops Format |
| Reconnect health `Disconnected` | `SetState(..., ex.Message)` | same swallow |
| `AuthenticationFailed` | `DesktopRpcFaultText.Format` | DESK-CONN-FAULT-01 already joined |
| Service `Error` | `DesktopRpcFaultText.Format` | DESK-SVC-FAULT-01 already joined |
| TLS / health timeout | `ex.Message` / static sentence | not this row |

**2** `SetState(ControllerConnectionState.Disconnected, ex.Message)` assignments still swallow `RpcException` @ `33682bb6`.

## Ranked tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-CONN-DISC-01** | Store `DesktopRpcFaultText.Format` when those `Disconnected` catches are `RpcException`; keep `ex.Message` otherwise + Living Spec | **2** `Disconnected` `ex.Message` assignments @ `33682bb6` | after inventory **W7-388**; seed **W7-389 (#1184)** |

Inventory (**W7-388**) may refine ranking and open the implement issue; seed **W7-389** advances NEXT to that implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-60 CLOSED)

PLAN-60 sole ranked row (**DESK-SVC-FAULT-01**) is **DONE**. No further PLAN-60 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl / nested ListBox a11y — deferred vanity  
- Native MSI / AppImage — W7-22 lock  
- systemd Type=notify — deferred packaging polish  
- Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail`  
- TLS connection text and the static health-timeout sentence are not this `Disconnected` `RpcException` path  

## §3.C ordering

1. **PLAN-60 COMPLETE** (W7-386 DESK-SVC-FAULT-01; seed **W7-387 DONE**).  
2. **W7-388 OPEN** — PLAN-61 inventory (**§3.C NEXT**).  
3. **W7-389 OPEN** — seed first PLAN-61 implement after inventory.  
4. Execute ranked DESK-CONN-DISC-01 atomically.

## §3.C NEXT

**§3.C NEXT = W7-388 (#1183)** — PLAN-61 Inventory Desktop connection Disconnected RPC fault text.
