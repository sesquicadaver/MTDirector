# PLAN-61 — Desktop connection Disconnected RPC fault text after service Error correlation

**Date:** 2026-09-19 (**PLAN-61 COMPLETE**)  
**Status:** **PLAN-61 COMPLETE** — Inventory **DONE** (W7-388); seed **W7-389 (#1184) DONE**; implement **W7-390 (#1186) DONE**; COMPLETE seed **W7-391 (#1187) DONE**; operator fault-correlation wave **PLAN-52…61 CLOSED**; freeze **W7-392 (#1191) DONE**; successor **PLAN-62** (audit TOR); predecessor **PLAN-60 COMPLETE**  
**PLAN issue / queue:** [W7-388 / PLAN-61 #1183](https://github.com/sesquicadaver/MTDirector/issues/1183) **DONE**  
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

## Inventory evidence (W7-388 @ `main` `994863c1`; seed baseline @ `33682bb6`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Connect `Disconnected` | `SetState(..., ex.Message)` | non-auth `RpcException` drops Format |
| Reconnect health `Disconnected` | `SetState(..., ex.Message)` | same swallow |
| `AuthenticationFailed` | `DesktopRpcFaultText.Format` | DESK-CONN-FAULT-01 already joined |
| Service `Error` | `DesktopRpcFaultText.Format` | DESK-SVC-FAULT-01 already joined |
| TLS / health timeout | `ex.Message` / static sentence | not this row |

**2** `SetState(ControllerConnectionState.Disconnected, ex.Message)` assignments still swallow `RpcException` @ `33682bb6`.

Confirmed on `main` `994863c1` (seed baseline `33682bb6`): **1** assignment in `ConnectCoreAsync` and **1** in `ProbeConnectedHealthOrLeaveAsync`. `AuthenticationFailed` already calls `DesktopRpcFaultText.Format` (DESK-CONN-FAULT-01). `RpcException.Message` is the gRPC status text and does not include the trailer correlation id that `DesktopRpcFaultText.Format` already renders. TLS `TlsError` stays `ex.Message`. The static `Health check timed out.` sentence and `Health status:` non-serving text stay out. Service `Error` already uses `DesktopRpcFaultText.Format` (DESK-SVC-FAULT-01).

**Ranking decision:** Prefer **ONE atomic row** (**DESK-CONN-DISC-01**), sole rank:

- When those `Disconnected` catches are `RpcException`, set the error from `DesktopRpcFaultText.Format` (code + correlation id)
- Non-RPC exceptions keep `ex.Message`
- Do **not** change `AuthenticationFailed` (DESK-CONN-FAULT-01)
- Do **not** change TLS `TlsError` text or the static health-timeout sentence
- Do **not** change the `mfc-error-detail-bin` trailer contract or the event 5301 log template
- Do **not** regress DESK-SVC-FAULT-01, SNAP-ERRTEXT-CORR-01, DESK-PANEL-FAULT-01, DESK-VRRP-PROG-01, DESK-VRRP-FAULT-01, SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, or DESK-RPC-FAULT-01
- Living Spec + operator docs

Splitting connect and reconnect into separate rows would be vanity: the same `SetState(Disconnected, ex.Message)` swallow is the whole gap. Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail`.

## Ranked tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-CONN-DISC-01** | Store `DesktopRpcFaultText.Format` when those `Disconnected` catches are `RpcException`; keep `ex.Message` otherwise + Living Spec | **2** `Disconnected` `ex.Message` assignments @ `994863c1` (seed baseline `33682bb6`); **DONE** — both catches call `DesktopRpcFaultText.Format` | after inventory **W7-388 DONE**; seed **W7-389 (#1184) DONE**; implement **W7-390 (#1186) DONE**; COMPLETE **W7-391 (#1187) DONE** |

Inventory (**W7-388 DONE**) confirmed sole rank. Seed **W7-389** advances NEXT to DESK-CONN-DISC-01 after inventory DONE.

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
2. **W7-388 DONE** — PLAN-61 inventory; opened **W7-390 (#1186)** DESK-CONN-DISC-01 implement + **W7-391 (#1187)** COMPLETE follow-up.  
3. **W7-389 (#1184) DONE** — seed advanced NEXT to DESK-CONN-DISC-01; keep COMPLETE **W7-391** open.  
4. **W7-390 (#1186) DONE** — DESK-CONN-DISC-01 stores `DesktopRpcFaultText.Format` on those `Disconnected` catches.  
5. **W7-391 (#1187) DONE** — PLAN-61 COMPLETE; operator fault-correlation wave **PLAN-52…61 CLOSED**; freeze **W7-392 (#1191)**.

## Wave close (no PLAN-62)

**PLAN-61 COMPLETE.** Sole ranked row **DESK-CONN-DISC-01** is **DONE**. The operator fault-correlation wave (**PLAN-52…61**) is **CLOSED**.

`/autopilot` must **not** open PLAN-62+ by grepping for another `ErrorText` / `ex.Message` / correlation-id site. Further product rows require an existing technical specification (TOR / already-open issue that predates freeze **W7-392**), not an agent-invented residual.

**Not seeded:** another DESK-*-FAULT; another SNAP-*-CORR; Onboarding / Deployment `ErrorCode` proto change; Desktop a11y vanity; MSI / AppImage; systemd Type=notify.

Freeze **W7-392 (#1191) DONE** closed the correlation-id / fault-text wave without inventing another DESK-*-FAULT. Later **PLAN-62** was seeded explicitly from audit TOR [`plan-62-audit-remediation-acd0759.md`](plan-62-audit-remediation-acd0759.md) / [`MTDirector-audit-acd0759-20260923.md`](../audits/MTDirector-audit-acd0759-20260923.md) — not from ErrorText grepping.

## §3.C NEXT

**§3.C NEXT = W7-415 (#1229)** — AUDIT-STATUS-01 (PLAN-62).
