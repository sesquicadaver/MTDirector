# PLAN-29 — Desktop connection health / reconnect after Controller stop

**Date:** 2026-09-15  
**Status:** Inventory **OPEN** (W7-250); seeded by **W7-249 DONE** after **PLAN-28 COMPLETE**  
**PLAN issue / queue:** [W7-250 / PLAN-29 #907](https://github.com/sesquicadaver/MTDirector/issues/907)  
**Predecessor:** PLAN-28 Desktop residual field/control AutomationProperties **COMPLETE**; AUDIT-INT-01 deferred connection-health residual from audit `11cb746` §18  
**Normative files:** [`ControllerConnectionService.cs`](../../src/Mfc.Desktop/Services/ControllerConnectionService.cs), [`ShellViewModel.cs`](../../src/Mfc.Desktop/ViewModels/ShellViewModel.cs), [`DesktopOptions.cs`](../../src/Mfc.Desktop/Configuration/DesktopOptions.cs)  
**Normative audit:** [`docs/audits/MTDirector-audit-11cb746-20260911.md`](../audits/MTDirector-audit-11cb746-20260911.md) §18  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the **product-critical** Desktop connection residual left after AUDIT-INT-01: while `Connected`, `RunReconnectLoopAsync` only delays and does **not** re-probe gRPC health. After Controller stop the GUI can remain Connected; transport retries do not drive correct shell status. PLAN-09 DESK-CONN/MTLS/AUTH Living Spec locks remain — **do not regress**.

## Principles

1. Connected shell state must reflect Controller liveness via bounded health probes (not idle delay-only loops).  
2. Living Spec locks health/reconnect behavior without inventing new RPCs or RouterOS writes.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-28 DESK-A11Y-FIELD/CTRL rows — that tranche is **COMPLETE**.

## Out of scope (do not seed)

- Re-opening PLAN-28 DESK-A11Y-FIELD/CTRL product rows  
- PLAN-28 deferred ListBox hosts / Drift–Audit read-only JSON TextBoxes (remain deferred a11y)  
- Replacing PLAN-09 DESK-CONN / DESK-MTLS / DESK-AUTH Living Spec locks  
- Operation-owner ACL / slow-subscriber hub backpressure (audit §19 adjacent residuals — note only; not PLAN-29 vanity rows unless inventory evidence elevates them)

## Inventory evidence (seed baseline after PLAN-28)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Connect health check | Health.Check on ConnectCore | OK |
| Reconnect loop while Connected | `Task.Delay` only (`ControllerConnectionService` ~165–168) | No periodic health; Controller stop can leave shell Connected |
| Shell status sync | `ShellViewModel` formats from connection StateChanged | Depends on connection service detecting drop |
| AuthFailed / TlsError paths | Loop breaks; DESK-AUTH Living Spec | Locked — do not regress |
| MaxReconnectAttempts / delay options | `DesktopOptions` | May need probe interval distinct from reconnect delay (inventory refines) |

## Ranked Desktop connection health tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-CONN-HEALTH-01** | Connected-state periodic health probe; leave Connected when Controller stops | `RunReconnectLoopAsync` Connected idle branch; audit §18 | queued after inventory **W7-250**; seed **W7-251 (#908)** |
| 2 | **DESK-CONN-RECONNECT-01** | Bounded reconnect + shell StatusText/LastError after health-fail drop | `ControllerConnectionService` reconnect attempts; `ShellViewModel` / `DesktopConnectionStatusText` | after HEALTH (inventory may refine / split / add regression)

Inventory (**W7-250**) may refine ranking, split probe vs reconnect vs shell chrome, add a regression lock row, and open implement issues; seed **W7-251** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-28 CLOSED)

PLAN-28 ranks 1…2 (**DESK-A11Y-FIELD-01**, **DESK-A11Y-CTRL-01**) are **DONE**. Deferred ListBox hosts and Drift/Audit read-only JSON TextBoxes remain intentional a11y residuals — not PLAN-29 rows. No further PLAN-28 product rows.

## Adjacent residuals (not seeded here)

- Operation-owner ACL on Watch/read paths beyond AUDIT-INT-01 Read permissions (audit §19 ownership nuance).  
- Slow-subscriber / unbounded hub backpressure beyond terminal prune from AUDIT-INT-01.  

These stay documented for a later continuous tranche unless inventory elevates them with concrete implement evidence.

## §3.C ordering

1. **PLAN-28 COMPLETE** (W7-248 DESK-A11Y-CTRL-01; seed **W7-249 DONE**).  
2. **W7-250 OPEN** — PLAN-29 inventory → open first health/reconnect implement + follow-up seeds.  
3. **W7-251 OPEN** — seed first PLAN-29 implement after inventory.  
4. Execute ranked HEALTH/RECONNECT rows atomically.

## §3.C NEXT

**§3.C NEXT = W7-250 (#907)** — PLAN-29 Inventory Desktop connection health / reconnect after Controller stop (AUDIT §18 residual) after PLAN-28.
