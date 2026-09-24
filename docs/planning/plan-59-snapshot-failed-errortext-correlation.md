# PLAN-59 — Snapshot failed-stage ErrorText correlation after panel status fault text

**Date:** 2026-09-19 (**PLAN-59 COMPLETE**)  
**Status:** **PLAN-59 COMPLETE** — Inventory **DONE** (W7-380); seed **W7-381 (#1168) DONE**; implement **W7-382 (#1170) DONE**; COMPLETE seed **W7-383 (#1171) DONE**; successor **PLAN-60** inventory **W7-384 (#1175) DONE**; seed **W7-385 (#1176) DONE**; implement **W7-386 (#1178) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-380 / PLAN-59 #1167](https://github.com/sesquicadaver/MTDirector/issues/1167) **DONE**  
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

## Inventory evidence (W7-380 @ `main` `fc4fb991`; seed baseline @ `6b0b3f95`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Snapshot Failed stage | `ErrorText = outcome.LastProgress.Error?.SanitizedDetail ?? "Capture failed."` | `ErrorText` drops the correlation id |
| `FormatCaptureProgress` | `(correlation {correlation})` when the progress detail has a 16-byte id | Progress line already shows the id |
| `RpcException` catch | `CaptureProgressText = $"Failed: {fault}"` and `ErrorText = fault` | RPC faults already use `DesktopRpcFaultText` |
| Panel `RpcException` | status line includes `DesktopRpcFaultText.Format` | DESK-PANEL-FAULT-01 already joined |
| VRRP Watch | `FormatCaptureProgress` | DESK-VRRP-PROG-01 already joined |

**1** Failed-stage `ErrorText` assignment still uses `SanitizedDetail` only @ `6b0b3f95`.

Confirmed on `main` `fc4fb991` (seed baseline `6b0b3f95`): **1** Failed-stage assignment `ErrorText = outcome.LastProgress.Error?.SanitizedDetail ?? "Capture failed."` drops the correlation id. `FormatCaptureProgress` already returns `(correlation {correlation})` when the progress `ErrorDetail` has a 16-byte id, and `RunCaptureAsync` stores that line on `CaptureProgressText`. The `RpcException` catch already sets `ErrorText = DesktopRpcFaultText.Format(ex)` (DESK-RPC-FAULT-01) — leave it. Panel status lines already copy that fault text (DESK-PANEL-FAULT-01). VRRP Watch already uses `FormatCaptureProgress` (DESK-VRRP-PROG-01). VRRP pair `RpcException` already copies `DesktopRpcFaultText.Format` (DESK-VRRP-FAULT-01).

**Ranking decision:** Prefer **ONE atomic row** (**SNAP-ERRTEXT-CORR-01**), sole rank:

- When the capture stage is `Failed`, set `ErrorText` so it shows the same correlation id the progress line already shows (reuse `FormatCaptureProgress` or the same `(correlation {id})` suffix)
- Do **not** change the `RpcException` path (`DesktopRpcFaultText`)
- Do **not** change the `mfc-error-detail-bin` trailer contract or the event 5301 log template
- Do **not** regress DESK-PANEL-FAULT-01, DESK-VRRP-PROG-01, DESK-VRRP-FAULT-01, SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, or DESK-RPC-FAULT-01
- Living Spec + operator docs

Splitting the suffix from the progress formatter would be vanity: one Failed-stage assignment drops the id the progress line already has. Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail`. Health-timeout and TLS connection text are not `ErrorDetail` trailers.

## Ranked tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **SNAP-ERRTEXT-CORR-01** | Show the capture-progress correlation id on Snapshot Failed-stage `ErrorText` (reuse `FormatCaptureProgress` or the same suffix) + Living Spec | **1** `SanitizedDetail`-only `ErrorText` assignment @ `fc4fb991` (seed baseline `6b0b3f95`) | after inventory **W7-380 DONE**; seed **W7-381 (#1168) DONE**; implement **W7-382 (#1170) DONE**; COMPLETE **W7-383 (#1171) OPEN** |

Inventory (**W7-380 DONE**) confirmed sole rank. Seed **W7-381** advances NEXT to SNAP-ERRTEXT-CORR-01 after inventory DONE.

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
2. **W7-380 DONE** — PLAN-59 inventory; opened **W7-382 (#1170)** SNAP-ERRTEXT-CORR-01 implement + **W7-383 (#1171)** COMPLETE follow-up.  
3. **W7-381 (#1168) DONE** — seed advanced NEXT to SNAP-ERRTEXT-CORR-01; keep COMPLETE **W7-383** open.  
4. **W7-382 (#1170) DONE** — SNAP-ERRTEXT-CORR-01 sets Failed-stage `ErrorText` from `FormatCaptureProgress`.  
5. **W7-383 (#1171) DONE** — PLAN-59 COMPLETE; successor **PLAN-60** inventory **W7-384 (#1175) DONE**; seed **W7-385 (#1176)**.

## Delivery notes (W7-382)

When Watch ends in `CaptureStage.Failed`, shell `ErrorText` is `FormatCaptureProgress` of that progress frame — the same line already stored on `CaptureProgressText`, including `(correlation {id})` when the progress `ErrorDetail` has a 16-byte id. The `RpcException` catch still uses `DesktopRpcFaultText.Format`. Trailer contract, event 5301 template, and DESK-PANEL-FAULT-01 / DESK-VRRP-PROG-01 / DESK-VRRP-FAULT-01 / SNAP-FAULT-CORR-01 / DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01 locks are unchanged.

## §3.C NEXT

**§3.C NEXT = W7-411 (#1221)** — PLAN-60 Inventory Desktop service RPC fault text.
