# PLAN-29 — Desktop connection health / reconnect after Controller stop

**Date:** 2026-09-15 (inventory **DONE** 2026-09-15)  
**Status:** Inventory **DONE** (W7-250); seed **W7-251 (#908) OPEN** → **DESK-CONN-HEALTH-01**; implement **W7-252 (#910) OPEN**; seed **W7-253 (#911) OPEN** → **DESK-CONN-RECONNECT-01**  
**PLAN issue / queue:** [W7-250 / PLAN-29 #907](https://github.com/sesquicadaver/MTDirector/issues/907) **DONE**  
**Predecessor:** PLAN-28 Desktop residual field/control AutomationProperties **COMPLETE**; AUDIT-INT-01 deferred connection-health residual from audit `11cb746` §18  
**Normative files:** [`ControllerConnectionService.cs`](../../src/Mfc.Desktop/Services/ControllerConnectionService.cs), [`ShellViewModel.cs`](../../src/Mfc.Desktop/ViewModels/ShellViewModel.cs), [`DesktopOptions.cs`](../../src/Mfc.Desktop/Configuration/DesktopOptions.cs), [`DesktopConnectionStatusText.cs`](../../src/Mfc.Desktop/Services/DesktopConnectionStatusText.cs)  
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
- Operation-owner ACL / slow-subscriber hub backpressure (audit §19 adjacent residuals — note only; not PLAN-29 vanity rows)

## Inventory evidence (2026-09-15 `ControllerConnectionService` @ `c5aebcd`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Connect health check | `Health.Check` in `ConnectCoreAsync` (~101–114); timeout via `HealthCheckTimeoutSeconds` | OK |
| Reconnect loop while Connected | `Task.Delay(_options.ReconnectDelayMilliseconds)` only (`RunReconnectLoopAsync` ~165–168); `continue` without probe | **No periodic health**; Controller stop can leave shell Connected |
| Leave Connected on probe fail | Not implemented on Connected idle path | Must set Disconnected + LastError, dispose channel, raise `StateChanged` |
| AuthFailed / TlsError paths | Loop `break` (~171–174); DESK-AUTH Living Spec | Locked — do not regress |
| Reconnect after non-Connected | `ConnectCoreAsync` under gate; `MaxReconnectAttempts` + reconnect delay (~176–195) | Exists but never entered from Connected idle without a drop signal |
| `DesktopOptions` | `HealthCheckTimeoutSeconds`, `MaxReconnectAttempts`, `ReconnectDelayMilliseconds` | No distinct Connected probe interval — HEALTH-01 may add `ConnectedHealthProbeIntervalMilliseconds` or document reuse of reconnect delay |
| Shell status sync | `ShellViewModel.OnConnectionStateChanged` → `DesktopConnectionStatusText.Format` | Depends on connection service detecting drop; ErrorText from `LastError` |
| `Channel` property | Non-null only while `Connected` | Stale Connected keeps channel exposed to callers |

## Ranked Desktop connection health tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-CONN-HEALTH-01** | Connected-state periodic health probe; leave Connected when Controller stops | `RunReconnectLoopAsync` Connected idle branch (~165–168); audit §18; options probe interval | implement **W7-252 (#910)**; seed **W7-251 (#908) OPEN** |
| 2 | **DESK-CONN-RECONNECT-01** | Bounded reconnect + shell StatusText/LastError after health-fail drop | Reconnect attempts (~176–195); `ShellViewModel` / `DesktopConnectionStatusText` | seed **W7-253 (#911) OPEN** (opens RECONNECT implement after HEALTH) |

Inventory (**W7-250 DONE**) locked ranking and opened HEALTH implement (**W7-252**) + RECONNECT seed (**W7-253**). Seed **W7-251** advances §3.C NEXT to the first implement after inventory DONE. No third vanity rank — PLAN-09 connection/TLS/auth locks remain the regression corpus.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-28 CLOSED)

PLAN-28 ranks 1…2 (**DESK-A11Y-FIELD-01**, **DESK-A11Y-CTRL-01**) are **DONE**. Deferred ListBox hosts and Drift/Audit read-only JSON TextBoxes remain intentional a11y residuals — not PLAN-29 rows. No further PLAN-28 product rows.

## Adjacent residuals (not seeded here)

- Operation-owner ACL on Watch/read paths beyond AUDIT-INT-01 Read permissions (audit §19 ownership nuance).  
- Slow-subscriber / unbounded hub backpressure beyond terminal prune from AUDIT-INT-01.  

These stay documented for a later continuous tranche — inventory did **not** elevate them (no Desktop connection-health implement evidence).

## §3.C ordering

1. **PLAN-28 COMPLETE** (W7-248 DESK-A11Y-CTRL-01; seed **W7-249 DONE**).  
2. **W7-250 DONE** — PLAN-29 inventory; opened **W7-252** / **W7-253**.  
3. **W7-251 OPEN** — seed advances NEXT to **DESK-CONN-HEALTH-01** (**W7-252**).  
4. Execute ranked HEALTH/RECONNECT rows atomically.

## §3.C NEXT

**§3.C NEXT = W7-251 (#908)** — Seed first PLAN-29 atomic row after inventory → DESK-CONN-HEALTH-01.
