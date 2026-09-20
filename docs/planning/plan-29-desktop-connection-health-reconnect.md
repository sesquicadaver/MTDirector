# PLAN-29 — Desktop connection health / reconnect after Controller stop

**Date:** 2026-09-15 (inventory **DONE** 2026-09-15; **COMPLETE** 2026-09-15)  
**Status:** **PLAN-29 COMPLETE** — Inventory **DONE** (W7-250); seed **W7-251 (#908) DONE**; **DESK-CONN-HEALTH-01 W7-252 (#910) DONE**; seed **W7-253 (#911) DONE**; **DESK-CONN-RECONNECT-01 W7-254 (#915) DONE**; seed **W7-255 (#916) DONE**; successor **PLAN-30 COMPLETE / PLAN-31** (inventory **W7-256 (#919) DONE**; seed **W7-257 (#920) DONE**; implement **W7-258 (#922) DONE**; seed **W7-259 (#923) DONE**; implement **W7-260 (#927) DONE**; seed **W7-261 (#928) DONE**; **PLAN-30 COMPLETE**; successor **PLAN-31** inventory **W7-262 (#931) OPEN** (**§3.C NEXT**))  
**PLAN issue / queue:** [W7-250 / PLAN-29 #907](https://github.com/sesquicadaver/MTDirector/issues/907) **DONE**  
**Predecessor:** PLAN-28 Desktop residual field/control AutomationProperties **COMPLETE**; AUDIT-INT-01 deferred connection-health residual from audit `11cb746` §18  
**Successor:** [`plan-30-watch-owner-acl-hub-backpressure.md`](plan-30-watch-owner-acl-hub-backpressure.md) (**COMPLETE**); [`plan-31-desktop-residual-listbox-readonly-a11y.md`](plan-31-desktop-residual-listbox-readonly-a11y.md) (Desktop residual ListBox / Drift–Audit read-only a11y)  
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
- Operation-owner ACL / slow-subscriber hub backpressure (audit §19 — successor **PLAN-30 COMPLETE / PLAN-31**)

## Inventory evidence (2026-09-15 `ControllerConnectionService` @ `c5aebcd`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Connect health check | `Health.Check` in `ConnectCoreAsync` (~101–114); timeout via `HealthCheckTimeoutSeconds` | OK |
| Reconnect loop while Connected | `ProbeConnectedHealthOrLeaveAsync` after `ConnectedHealthProbeIntervalMilliseconds` (**W7-252 DONE**) | Was delay-only; now probes and leaves Connected on failure |
| Leave Connected on probe fail | Implemented in `ProbeConnectedHealthOrLeaveAsync` (**W7-252 DONE**) | Disconnected + LastError, dispose channel, `StateChanged` |
| AuthFailed / TlsError paths | Loop `break` (~171–174); DESK-AUTH Living Spec | Locked — do not regress |
| Reconnect after non-Connected | `ConnectCoreAsync` under gate; `MaxReconnectAttempts` + reconnect delay (~176–195) | Entered after HEALTH-01 drop; attempts reset + LastError preserved on reconnect Connecting (**W7-254 DONE**) |
| `DesktopOptions` | `HealthCheckTimeoutSeconds`, `MaxReconnectAttempts`, `ReconnectDelayMilliseconds`, `ConnectedHealthProbeIntervalMilliseconds` | HEALTH-01 added distinct Connected probe interval |
| Shell status sync | `ShellViewModel.OnConnectionStateChanged` → `DesktopConnectionStatusText.Format` | Sync via StateChanged; LastError preserved across reconnect Connecting (**W7-254 DONE**) |
| `Channel` property | Non-null only while `Connected` | Stale Connected keeps channel exposed to callers |

## Ranked Desktop connection health tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-CONN-HEALTH-01** | Connected-state periodic health probe; leave Connected when Controller stops | `RunReconnectLoopAsync` Connected probe via `ProbeConnectedHealthOrLeaveAsync`; audit §18; `ConnectedHealthProbeIntervalMilliseconds` | implement **W7-252 (#910) DONE**; seed **W7-251 (#908) DONE** |
| 2 | **DESK-CONN-RECONNECT-01** | Bounded reconnect + shell StatusText/LastError after health-fail drop | Reconnect attempts; `ShellViewModel` / `DesktopConnectionStatusText` | implement **W7-254 (#915) DONE**; seed **W7-253 (#911) DONE**; follow-up **W7-255 (#916) DONE** |

Inventory (**W7-250 DONE**) locked ranking and opened HEALTH implement (**W7-252**) + RECONNECT seed (**W7-253**). Seed **W7-251** advanced §3.C NEXT to the first implement after inventory DONE. No third vanity rank — PLAN-09 connection/TLS/auth locks remain the regression corpus.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-28 CLOSED)

PLAN-28 ranks 1…2 (**DESK-A11Y-FIELD-01**, **DESK-A11Y-CTRL-01**) are **DONE**. Deferred ListBox hosts and Drift/Audit read-only JSON TextBoxes remain intentional a11y residuals — not PLAN-29 rows. No further PLAN-28 product rows.

## Residual notes (COMPLETE)

- All ranked HEALTH/RECONNECT remediations 1…2 closed on `main`.  
- No further PLAN-29 product rows — continuous queue advances to **PLAN-30 COMPLETE / PLAN-31** (Watch operation-owner ACL / hub slow-subscriber backpressure; AUDIT §19 residual).  
- PLAN-28 deferred ListBox hosts and Drift/Audit JSON TextBoxes remain intentional a11y residuals (not §3 stop-gates).  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates.

## Adjacent residuals (seeded as PLAN-30 COMPLETE / PLAN-31)

- Operation-owner ACL on Watch/read paths beyond AUDIT-INT-01 Read permissions (audit §19 ownership nuance) — **WATCH-OWN-01**.  
- Slow-subscriber / unbounded hub backpressure beyond terminal prune from AUDIT-INT-01 — **WATCH-BP-01**.  

Evidence at PLAN-29 COMPLETE: `EnsureWatchAuthorizedAsync` still Read-only; ProgressHubs still `Channel.CreateUnbounded`. Seeded as successor inventory **W7-256**.

## §3.C ordering

1. **PLAN-28 COMPLETE** (W7-248 DESK-A11Y-CTRL-01; seed **W7-249 DONE**).  
2. **W7-250 DONE** — PLAN-29 inventory; opened **W7-252** / **W7-253**.  
3. **W7-251 DONE** — seed advanced NEXT to **DESK-CONN-HEALTH-01** (**W7-252**).  
4. **W7-252 DONE** — DESK-CONN-HEALTH-01 Connected-state periodic `Health.Check`; leave Connected on Controller stop.  
5. **W7-253 DONE** — seed advanced NEXT to **DESK-CONN-RECONNECT-01** (**W7-254**); opened **W7-255** PLAN-29 COMPLETE follow-up.  
6. **W7-254 DONE** — DESK-CONN-RECONNECT-01 bounded reconnect + shell StatusText/LastError.  
7. **W7-255 DONE** — PLAN-29 COMPLETE; seeded PLAN-30 COMPLETE / PLAN-31 (**W7-256** / **W7-257**).

## §3.C NEXT

**PLAN-29 COMPLETE.** Successor **PLAN-30 COMPLETE / PLAN-31** inventory **DONE** (W7-256). **§3.C NEXT = none (queue exhausted; W7-392 #1191 DONE)** — Seed next after WATCH-BP-01 (PLAN-30 COMPLETE / PLAN-31 COMPLETE).
