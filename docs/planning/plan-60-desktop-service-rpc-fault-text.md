# PLAN-60 — Desktop service RPC fault text after snapshot ErrorText correlation

**Date:** 2026-09-19 (**PLAN-60 COMPLETE**)  
**Status:** **PLAN-60 COMPLETE** — Inventory **DONE** (W7-384); seed **W7-385 (#1176) DONE**; implement **W7-386 (#1178) DONE**; COMPLETE seed **W7-387 (#1179) DONE**; successor **PLAN-61** inventory **W7-388 (#1183) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-384 / PLAN-60 #1175](https://github.com/sesquicadaver/MTDirector/issues/1175) **DONE**  
**Predecessor:** PLAN-59 Snapshot failed-stage ErrorText correlation **COMPLETE** (SNAP-ERRTEXT-CORR-01)  
**Normative files:** `SnapshotViewerService`, `SnapshotDiffService`, `InventoryTreeService`, operator docs  
**Normative prior locks:** SNAP-ERRTEXT-CORR-01; DESK-PANEL-FAULT-01; DESK-VRRP-PROG-01; DESK-VRRP-FAULT-01; SNAP-FAULT-CORR-01; DESK-CONN-FAULT-01; CTRL-ERRDETAIL-LOG-01 (event 5301); DESK-RPC-FAULT-01 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-59 put `FormatCaptureProgress` on Failed-stage Snapshot `ErrorText`: Desktop services that load snapshots, compare snapshots, and refresh inventory still store `Error = ex.Message`. An `RpcException` therefore reaches shell `ErrorText` as `RpcException.Message`, which does not include the `mfc-error-detail-bin` correlation id. ViewModel `RpcException` catches already use `DesktopRpcFaultText.Format`. Operators reading those service-fed errors cannot join journald event **5301**. Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. When a service catch is an `RpcException`, the `Error` string operators read should be `DesktopRpcFaultText.Format` (code + correlation id). Non-RPC exceptions keep `ex.Message`.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not re-open SNAP-ERRTEXT-CORR-01, DESK-PANEL-FAULT-01, DESK-VRRP-PROG-01, DESK-VRRP-FAULT-01, SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, or DESK-RPC-FAULT-01.  
5. Avoid vanity Desktop a11y and systemd Type=notify.

## Out of scope (do not seed)

- Re-opening SNAP-ERRTEXT-CORR-01 / DESK-PANEL-FAULT-01 / DESK-VRRP-PROG-01 / DESK-VRRP-FAULT-01 / SNAP-FAULT-CORR-01 / DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01  
- Native MSI / AppImage (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- systemd `Type=notify` / `WatchdogSec`  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Changing the `mfc-error-detail-bin` trailer contract or the event 5301 log line  
- Onboarding / Deployment progress (`ErrorCode` only; proto has no `ErrorDetail`)  
- Health-timeout and TLS connection text  
- ViewModel `RpcException` catches that already call `DesktopRpcFaultText.Format`  

## Inventory evidence (W7-384 @ `main` `68302273`; seed baseline @ `10adc5ba`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `SnapshotViewerService` | **3** `Error = ex.Message` (device load, capture load, section load) | `RpcException` drops the correlation id |
| `SnapshotDiffService` | **2** `Error = ex.Message` (capture list, compare) | `RpcException` drops the correlation id |
| `InventoryTreeService` | **1** `Error = ex.Message` (refresh) | `RpcException` drops the correlation id |
| ViewModel `RpcException` | `DesktopRpcFaultText.Format` | DESK-RPC-FAULT-01 / DESK-PANEL-FAULT-01 already joined |
| Failed-stage Snapshot `ErrorText` | `FormatCaptureProgress` | SNAP-ERRTEXT-CORR-01 already joined |

**6** service `Error = ex.Message` assignments still swallow `RpcException` @ `10adc5ba`.

Confirmed on `main` `68302273` (seed baseline `10adc5ba`): **3** assignments in `SnapshotViewerService`, **2** in `SnapshotDiffService`, and **1** in `InventoryTreeService`. None of those services call `DesktopRpcFaultText`. `RpcException.Message` is the gRPC status text and does not include the trailer correlation id that `DesktopRpcFaultText.Format` already renders for ViewModels. Failed-stage Snapshot `ErrorText` already uses `FormatCaptureProgress` (SNAP-ERRTEXT-CORR-01). Panel status lines, VRRP pair status, capture progress, connection `AuthenticationFailed`, and event 5301 stay locked.

**Ranking decision:** Prefer **ONE atomic row** (**DESK-SVC-FAULT-01**), sole rank:

- When those service catches are `RpcException`, set `Error` from `DesktopRpcFaultText.Format` (code + correlation id)
- Non-RPC exceptions keep `ex.Message`
- Do **not** change ViewModel `RpcException` paths that already call `DesktopRpcFaultText.Format`
- Do **not** change the `mfc-error-detail-bin` trailer contract or the event 5301 log template
- Do **not** regress SNAP-ERRTEXT-CORR-01, DESK-PANEL-FAULT-01, DESK-VRRP-PROG-01, DESK-VRRP-FAULT-01, SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, or DESK-RPC-FAULT-01
- Living Spec + operator docs

Splitting the three services into separate rows would be vanity: the same `Error = ex.Message` swallow is the whole gap. Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail`. Health-timeout and TLS connection text are not `ErrorDetail` trailers.

## Ranked tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-SVC-FAULT-01** | Store `DesktopRpcFaultText.Format` when those service catches are `RpcException`; keep `ex.Message` otherwise + Living Spec | **6** `Error = ex.Message` assignments @ `68302273` (seed baseline `10adc5ba`) | after inventory **W7-384 DONE**; seed **W7-385 (#1176) DONE**; implement **W7-386 (#1178) DONE**; COMPLETE **W7-387 (#1179) OPEN** |

Inventory (**W7-384 DONE**) confirmed sole rank. Seed **W7-385** advances NEXT to DESK-SVC-FAULT-01 after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-59 CLOSED)

PLAN-59 sole ranked row (**SNAP-ERRTEXT-CORR-01**) is **DONE**. No further PLAN-59 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl / nested ListBox a11y — deferred vanity  
- Native MSI / AppImage — W7-22 lock  
- systemd Type=notify — deferred packaging polish  
- Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail`  
- Health-timeout and TLS connection text are not `ErrorDetail` trailers  

## §3.C ordering

1. **PLAN-59 COMPLETE** (W7-382 SNAP-ERRTEXT-CORR-01; seed **W7-383 DONE**).  
2. **W7-384 DONE** — PLAN-60 inventory; opened **W7-386 (#1178)** DESK-SVC-FAULT-01 implement + **W7-387 (#1179)** COMPLETE follow-up.  
3. **W7-385 (#1176) DONE** — seed advanced NEXT to DESK-SVC-FAULT-01; keep COMPLETE **W7-387** open.  
4. **W7-386 (#1178) DONE** — DESK-SVC-FAULT-01 sets service `Error` from `DesktopRpcFaultText.Format` on `RpcException`.  
5. **W7-387 (#1179) DONE** — PLAN-60 COMPLETE; successor **PLAN-61** inventory **W7-388 (#1183)**.

## Delivery notes (W7-386)

`SnapshotViewerService` (device load, capture load, section load), `SnapshotDiffService` (capture list, compare), and `InventoryTreeService` (refresh) set `Error` from `DesktopRpcFaultText.Format`. When the caught exception is an `RpcException`, that text includes the code and correlation id from `mfc-error-detail-bin`. Non-RPC exceptions keep `Exception.Message`. ViewModel `RpcException` paths, Failed-stage Snapshot `ErrorText` (`FormatCaptureProgress`), panel status, VRRP pair status, capture progress, connection `AuthenticationFailed`, the trailer contract, and event 5301 are unchanged.

## §3.C NEXT

**§3.C NEXT = W7-390 (#1186)** — PLAN-61 Inventory Desktop connection Disconnected RPC fault text.
