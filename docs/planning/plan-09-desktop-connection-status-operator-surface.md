# PLAN-09 — Desktop connection-status operator-surface Living Spec product tranche

**Date:** 2026-09-08  
**PLAN issue / queue:** [W7-98 / PLAN-09 #594](https://github.com/sesquicadaver/MTDirector/issues/594)  
**Predecessor:** PLAN-08 Desktop secondary operator-surface COMPLETE (DESK-NODE…DESK-POLICY-02); product seed W7-97  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

PLAN-05…08 closed host-aligned and secondary Desktop Living Specs for operator panels. PLAN-09 keeps `/autopilot` from idling by inventoring **Desktop connection-status operator-surface** rows: Connect/Disconnect orchestration, mTLS actor status presentation, and AuthenticationFailed / TlsError fail-closed status paths that today exist as residual W7-05/08/12 Living Specs rather than a dedicated DESK-CONN / DESK-MTLS / DESK-AUTH product tranche matrix.

## Out of scope (do not seed)

- Lab / CHR / physical CRS / `WriteEnabled` flip — ops-parallel, not §3 stop-gates  
- Anti-goals: local Desktop `SemanticDiffEngine`, auto-fix drift, fake VRRP roles  
- Replacing Controller mTLS TrustedCa / HttpContext.User Living Specs (W7-04…W7-07) — Desktop **adds** operator-surface depth  
- Replacing packaging / signing residual locks

## Evidence baseline (connection-status)

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Connect / Disconnect / state machine | `DesktopConnectionLivingSpecTests` (DESK-CONN-01) | PLAN-09 **DONE** row |
| mTLS actor on Connected status | `DesktopMtlsActorLivingSpecTests` (DESK-MTLS-01) | PLAN-09 **DONE** row |
| AuthenticationFailed / TlsError status | `DesktopAuthLivingSpecTests` (DESK-AUTH-01) | PLAN-09 **DONE** row |

## Ranked Desktop connection-status operator-surface tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-CONN-01** | Connect/Disconnect + status shell lacks dedicated Desktop Living Spec depth vs `IControllerConnectionService` | `ShellViewModel` Connect/Disconnect; `DesktopConnectionStatusText`; `IControllerConnectionService` | **W7-99 DONE** (#597) |
| 2 | **DESK-MTLS-01** | Connected status mTLS actor presentation lacks dedicated DESK-* Living Spec beyond W7-08 residual | `DesktopGrpcActorResolver`; `DesktopConnectionStatusText.Format` actor suffix | **W7-101 DONE** (#600) |
| 3 | **DESK-AUTH-01** | AuthenticationFailed / TlsError fail-closed status lacks dedicated DESK-* Living Spec beyond W7-12 residual | `ControllerConnectionState.AuthenticationFailed` / `TlsError`; status bindings | **W7-103 DONE** (#604) |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## §3.C NEXT

**Status:** PLAN-09 **COMPLETE** (DESK-CONN…DESK-AUTH).

**§3.C NEXT = W7-129 (#659)** — DESK-COMPOSE-01 Desktop Policies Compose+RecordAnalysis Living Spec depth (PLAN-11).

**Successor:** PLAN-10 **COMPLETE**; PLAN-11 inventory **DONE** (W7-112); first atomic row **DESK-SUBMIT-01** (W7-114).
