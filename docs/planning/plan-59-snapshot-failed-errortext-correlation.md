# PLAN-59 — Snapshot failed-stage ErrorText correlation after panel status fault text

**Date:** 2026-09-19 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-380); seed **W7-381 (#1168) OPEN**; predecessor **PLAN-58 COMPLETE**  
**PLAN issue / queue:** [W7-380 / PLAN-59 #1167](https://github.com/sesquicadaver/MTDirector/issues/1167) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-58 Desktop panel status fault text **COMPLETE** (DESK-PANEL-FAULT-01)  
**Normative files:** `SnapshotViewerViewModel`, operator docs  
**Normative prior locks:** DESK-PANEL-FAULT-01; DESK-VRRP-PROG-01; DESK-VRRP-FAULT-01; SNAP-FAULT-CORR-01; DESK-CONN-FAULT-01; CTRL-ERRDETAIL-LOG-01 (event 5301); DESK-RPC-FAULT-01 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-58 put `DesktopRpcFaultText` on Drift, Audit, Incident, Routing assurance, and GetNodeWorkflow status lines: a failed Snapshot capture still writes `ErrorText` from `SanitizedDetail` only. `FormatCaptureProgress` already appends `(correlation {id})` on the progress line. Operators reading shell `ErrorText` cannot join journald event **5301**. Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. The shell `ErrorText` operators read after a failed capture should show the same correlation id the progress line already shows.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not re-open DESK-PANEL-FAULT-01, DESK-VRRP-PROG-01, DESK-VRRP-FAULT-01, SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, or DESK-RPC-FAULT-01.  
5. Avoid vanity Desktop a11y and systemd Type=notify.

## Out of scope (do not seed)

- Re-opening DESK-PANEL-FAULT-01 / DESK-VRRP-PROG-01 / DESK-VRRP-FAULT-01 / SNAP-FAULT-CORR-01 / DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01  
- Native MSI / AppImage (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- systemd `Type=notify` / `WatchdogSec`  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Changing the `mfc-error-detail-bin` trailer contract or the event 5301 log line  
- Onboarding / Deployment progress (`ErrorCode` only; proto has no `ErrorDetail`)  
- Health-timeout and TLS connection text  
- Panel status lines already joined by DESK-PANEL-FAULT-01  

## Inventory evidence (seed baseline @ `6b0b3f95`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Snapshot Failed stage | `ErrorText = outcome.LastProgress.Error?.SanitizedDetail ?? "Capture failed."` | `ErrorText` drops the correlation id |
| `FormatCaptureProgress` | `(correlation {correlation})` when the progress detail has a 16-byte id | Progress line already shows the id |
| `RpcException` catch | `CaptureProgressText = $"Failed: {fault}"` and `ErrorText = fault` | RPC faults already use `DesktopRpcFaultText` |
| Panel `RpcException` | status line includes `DesktopRpcFaultText.Format` | DESK-PANEL-FAULT-01 already joined |
| VRRP Watch | `FormatCaptureProgress` | DESK-VRRP-PROG-01 already joined |

**1** Failed-stage `ErrorText` assignment still uses `SanitizedDetail` only @ `6b0b3f95`.

## Ranked tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **SNAP-ERRTEXT-CORR-01** | Show the capture-progress correlation id on Snapshot Failed-stage `ErrorText` (reuse `FormatCaptureProgress` or the same suffix) + Living Spec | **1** `SanitizedDetail`-only `ErrorText` assignment @ `6b0b3f95` | after inventory **W7-380**; seed **W7-381 (#1168)** |

Inventory (**W7-380**) may refine ranking and open the implement issue; seed **W7-381** advances NEXT to that implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-58 CLOSED)

PLAN-58 sole ranked row (**DESK-PANEL-FAULT-01**) is **DONE**. No further PLAN-58 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl / nested ListBox a11y — deferred vanity  
- Native MSI / AppImage — W7-22 lock  
- systemd Type=notify — deferred packaging polish  
- Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail`  
- Health-timeout and TLS connection text are not `ErrorDetail` trailers  

## §3.C ordering

1. **PLAN-58 COMPLETE** (W7-378 DESK-PANEL-FAULT-01; seed **W7-379 DONE**).  
2. **W7-380 OPEN** — PLAN-59 inventory (**§3.C NEXT**).  
3. **W7-381 OPEN** — seed first PLAN-59 implement after inventory.  
4. Execute ranked SNAP-ERRTEXT-CORR-01 atomically.

## §3.C NEXT

**§3.C NEXT = W7-380 (#1167)** — PLAN-59 Inventory Snapshot failed-stage ErrorText correlation.
