# PLAN-58 — Desktop panel status fault text after VRRP capture-progress fault text

**Date:** 2026-09-19 (**PLAN-58 COMPLETE**)  
**Status:** **PLAN-58 COMPLETE** — Inventory **DONE** (W7-376); seed **W7-377 (#1160) DONE**; implement **W7-378 (#1162) DONE**; COMPLETE seed **W7-379 (#1163) DONE**; successor **PLAN-59** inventory **W7-380 (#1167) DONE**; seed **W7-381 (#1168) DONE**; implement **W7-382 (#1170) DONE**; COMPLETE seed **W7-383 (#1171) DONE**; successor **PLAN-60** inventory **W7-384 (#1175) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-376 / PLAN-58 #1159](https://github.com/sesquicadaver/MTDirector/issues/1159) **DONE**  
**Predecessor:** PLAN-57 VRRP capture-progress fault text **COMPLETE** (DESK-VRRP-PROG-01)  
**Normative files:** `DriftViewModel`, `AuditViewModel`, `IncidentViewModel`, `RoutingAssuranceViewModel`, `NodeDetailViewModel`, operator docs  
**Normative prior locks:** DESK-VRRP-PROG-01; DESK-VRRP-FAULT-01; SNAP-FAULT-CORR-01; DESK-CONN-FAULT-01; CTRL-ERRDETAIL-LOG-01 (event 5301); DESK-RPC-FAULT-01 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-57 put the capture-progress correlation id on the VRRP pair status line: other Desktop panels still set a static status sentence while `ErrorText` already has `DesktopRpcFaultText.Format`. Operators watching Drift, Audit, Incident, Routing assurance, or GetNodeWorkflow cannot join that line to journald event **5301**. Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. The status line operators watch should show the same code and correlation id already on `ErrorText`.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not re-open DESK-VRRP-PROG-01, DESK-VRRP-FAULT-01, SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, or DESK-RPC-FAULT-01.  
5. Avoid vanity Desktop a11y and systemd Type=notify.

## Out of scope (do not seed)

- Re-opening DESK-VRRP-PROG-01 / DESK-VRRP-FAULT-01 / SNAP-FAULT-CORR-01 / DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01  
- Native MSI / AppImage (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- systemd `Type=notify` / `WatchdogSec`  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Changing the `mfc-error-detail-bin` trailer contract or the event 5301 log line  
- Onboarding / Deployment progress (`ErrorCode` only; proto has no `ErrorDetail`)  
- Health-timeout and TLS connection text  
- Snapshot Failed-stage `ErrorText` — the progress line already shows `(correlation {id})`  
- Policies / Zones / Add router / Deployment / Onboarding surfaces that set `ErrorText` only (no second static status sentence)

## Inventory evidence (W7-376 @ `main` `c665a34c`; seed baseline @ `55765d52`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `DriftViewModel` `RpcException` | `ErrorText = DesktopRpcFaultText.Format(ex)`; `StatusText = "Drift load failed."` and `"GetDriftEvent failed; showing list payload."` | Status drops the id |
| `AuditViewModel` `RpcException` | `StatusText = "Audit load failed."` | Status drops the id |
| `IncidentViewModel` `RpcException` | `"Incident ingest failed."` and `"Incident assessment bind failed."` | Status drops the id |
| `RoutingAssuranceViewModel` `RpcException` | `StatusText = "Routing assurance load failed."` | Status drops the id |
| `NodeDetailViewModel` `GetNodeWorkflow` `RpcException` | `DeploymentReadinessText = "GetNodeWorkflow failed."` | Readiness line drops the id |
| VRRP pair `RpcException` | `VrrpPairStatusText = $"VRRP pair consistency failed. {fault}"` | DESK-VRRP-FAULT-01 already joined |
| VRRP Watch | `FormatCaptureProgress` | DESK-VRRP-PROG-01 already joined |

**7** `RpcException` catches still assign a static panel status / readiness sentence beside `DesktopRpcFaultText.Format` @ `55765d52`.

Confirmed on `main` `c665a34c` (seed baseline `55765d52`): **7** `ErrorText = DesktopRpcFaultText.Format(ex)` sites still sit next to a static sentence — Drift load, GetDriftEvent, Audit load, Incident ingest, Incident assessment bind, Routing assurance load, and GetNodeWorkflow. The matching non-RPC `Exception` catches keep the static sentence (no `ErrorDetail`). VRRP pair `RpcException` already copies `DesktopRpcFaultText.Format` onto `VrrpPairStatusText` (DESK-VRRP-FAULT-01). VRRP Watch already uses `FormatCaptureProgress` (DESK-VRRP-PROG-01).

**Ranking decision:** Prefer **ONE atomic row** (**DESK-PANEL-FAULT-01**), sole rank:

- When `RpcException` is caught on those seven sites, set the panel status / readiness line to the same `DesktopRpcFaultText.Format` text already stored in `ErrorText` (code + correlation id), matching DESK-VRRP-FAULT-01, so operators can join journald event **5301**
- Do **not** change the `mfc-error-detail-bin` trailer contract or the event 5301 log template
- Do **not** regress DESK-VRRP-PROG-01, DESK-VRRP-FAULT-01, SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, or DESK-RPC-FAULT-01
- Living Spec + operator docs

Splitting Drift from Audit / Incident / Routing / GetNodeWorkflow would be vanity: each drops the same id that `ErrorText` already has. Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail`. Snapshot Failed-stage `ErrorText` stays `SanitizedDetail`; the progress line already shows the id.

## Ranked tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-PANEL-FAULT-01** | Show `DesktopRpcFaultText.Format` on the panel status line for those `RpcException` catches + Living Spec | **7** static status sentences @ `c665a34c` (seed baseline `55765d52`) | after inventory **W7-376 DONE**; seed **W7-377 (#1160) DONE**; implement **W7-378 (#1162) DONE**; COMPLETE **W7-379 (#1163) DONE** |

Inventory (**W7-376 DONE**) confirmed sole rank. Seed **W7-377** advances NEXT to DESK-PANEL-FAULT-01 after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-57 CLOSED)

PLAN-57 sole ranked row (**DESK-VRRP-PROG-01**) is **DONE**. No further PLAN-57 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl / nested ListBox a11y — deferred vanity  
- Native MSI / AppImage — W7-22 lock  
- systemd Type=notify — deferred packaging polish  
- Snapshot Failed-stage `ErrorText` uses `SanitizedDetail` only; the progress line already has the correlation id  
- Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail`  
- Health-timeout and TLS connection text are not `ErrorDetail` trailers

## §3.C ordering

1. **PLAN-57 COMPLETE** (W7-374 DESK-VRRP-PROG-01; seed **W7-375 DONE**).  
2. **W7-376 DONE** — PLAN-58 inventory; opened **W7-378 (#1162)** DESK-PANEL-FAULT-01 implement + **W7-379 (#1163)** COMPLETE follow-up.  
3. **W7-377 (#1160) DONE** — seed advanced NEXT to DESK-PANEL-FAULT-01; keep COMPLETE **W7-379** open.  
4. **W7-378 (#1162) DONE** — DESK-PANEL-FAULT-01 copies `DesktopRpcFaultText.Format` onto the seven panel status / readiness lines.  
5. **W7-379 (#1163) DONE** — PLAN-58 COMPLETE; successor **PLAN-59** inventory **W7-380 (#1167)**.

## Delivery notes (W7-378)

`RpcException` catches in Drift (load and GetDriftEvent), Audit, Incident (ingest and assessment bind), Routing assurance, and GetNodeWorkflow assign `DesktopRpcFaultText.Format` once and use that string for both `ErrorText` and the panel status or readiness line (`{sentence} {fault}`). When `mfc-error-detail-bin` is present, the status line shows the same code and correlation id as `ErrorText` and journald event **5301**. Non-RPC `Exception` catches keep the static sentence. VRRP pair status (DESK-VRRP-FAULT-01) and capture progress (DESK-VRRP-PROG-01) are unchanged. Trailer contract, event 5301 template, and SNAP-FAULT-CORR-01 / DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01 locks are unchanged.

## §3.C NEXT

**§3.C NEXT = W7-389 (#1184)** — PLAN-59 Inventory Snapshot failed-stage ErrorText correlation.
