# Project readiness assessment

**As of:** 2026-08-31  
**Baseline commit:** `main` @ `877a529` — W2.2 Routing assurance next-hop/subject ([#338](https://github.com/sesquicadaver/MTDirector/pull/338))  
**Release tag:** [`v0.2.0`](https://github.com/sesquicadaver/MTDirector/releases/tag/v0.2.0) (2026-08-24)  
**Queue plan:** [`../planning/continuous-queue-plan.md`](../planning/continuous-queue-plan.md)

This document summarizes **code + documentation readiness** against the normative queue in [`ROADMAP.md`](../../ROADMAP.md). It is not a substitute for operator acceptance ([`mvp-acceptance.md`](mvp-acceptance.md)) or release gates ([`release-gates.md`](release-gates.md)).

## Executive summary

| Layer | Status | Notes |
|-------|--------|-------|
| MVP (M0–M6 + N1) | **100% CLOSED** | 109/109 issues in code audit |
| Post-MVP M7 (M7.1–M7.4) | **100% CLOSED** | 27/27 issues in code audit |
| P2 read path (P2-04…P2-06) | **100% CLOSED** | Production probe + capture + DI gate |
| P2 write path (P2-07…P2-11) | **100% CLOSED** | Runtimes + WriteEnabled gate + pilot runbook |
| Desktop alignment P0–P2 | **CLOSED** | W1.1–W4.4 + W2.1–W2.2 |
| Linear queue (§3.C) | **ACTIVE** | **NEXT = W5-03** ([#344](https://github.com/sesquicadaver/MTDirector/issues/344)) |

**Overall code readiness (milestones):** all 139 mapped product issues are **DONE in code**. Alignment P0–P2 is **DONE**. Remaining work is **glue + P3 Contracts** on a seeded queue — not a phase-stop.  
**Queue integrity:** **TRACKER-01 DONE** (#289). **PLAN-01 DONE** (#290). **PLAN-02** (#339) seeds continuous §3.C so `/autopilot` does not idle.  
**Production pilot readiness (read-only):** **ready** when `Mfc:RouterOs:Enabled=true` + PostgreSQL + device connection profiles — see [`pilot-runbook.md`](../operations/pilot-runbook.md).  
**Production pilot readiness (write path):** **ready (lab)** — set `Mfc:RouterOs:WriteEnabled=true`; checklist in [`pilot-runbook.md`](../operations/pilot-runbook.md). Lab phases **do not** block §3.  
**Desktop inventory registration:** **ready** — Inventory **Add router** wizard (Site→Node→Device + credentials). Neighbor apply fills VRRP member b (**CONT-02 DONE**).

## Milestone matrix (code audit §2.2)

| Segment | Closed | Open (code) | Evidence |
|---------|-------:|------------:|----------|
| M0 Bootstrap | 10 | 0 | ROADMAP §2.2, architecture unit tests |
| M1 Read-only | 34 | 0 | Living Spec M1-01…M1-34 |
| N1 Packet-path | 7 | 0 | N1-01…N1-07 Living Specs |
| M2 Policy | 18 | 0 | M2 Living Specs + Desktop authoring |
| M3 Compiler | 8 | 0 | M3-08 acceptance matrix |
| M5 Onboarding | 10 | 0 | Domain + API; production runtime via P2 write |
| M4 Safe deploy | 13 | 0 | Domain + API; production runtime via P2 write |
| M6 E2E / drift | 9 | 0 | M6-09 acceptance package |
| M7 Post-MVP | 27 | 0 | M7.1…M7.4 Living Specs |
| P2 read path | 3 | 0 | P2-04…P2-06 + `PilotReadinessLivingSpecTests` |
| **Product total** | **139** | **0** | ROADMAP §2.2 |

Desktop alignment W1–W4 / W2.1–W2.2 is **DONE** on top of that baseline (not additional §2.2 IDs).

## Linear queue (§3.C) — current

| # | ID | GitHub | Status |
|--:|----|-------:|--------|
| 133 | PLAN-02 | [#339](https://github.com/sesquicadaver/MTDirector/issues/339) | **DONE** ([#345](https://github.com/sesquicadaver/MTDirector/pull/345)) |
| 134 | CONT-01 | [#340](https://github.com/sesquicadaver/MTDirector/issues/340) | **DONE** |
| 135 | CONT-02 | [#341](https://github.com/sesquicadaver/MTDirector/issues/341) | **DONE** |
| 136 | W5-01 | [#342](https://github.com/sesquicadaver/MTDirector/issues/342) | **DONE** |
| 137 | W5-02 | [#343](https://github.com/sesquicadaver/MTDirector/issues/343) | **DONE** |
| 138 | W5-03 | [#344](https://github.com/sesquicadaver/MTDirector/issues/344) | **NEXT** |

Closed history (P2 + PLAN-01): TRACKER-01 [#289](https://github.com/sesquicadaver/MTDirector/issues/289) … P2-11 [#297](https://github.com/sesquicadaver/MTDirector/issues/297); PLAN-NBR-01 [#314](https://github.com/sesquicadaver/MTDirector/issues/314).

Product §3 is linear. **Lab/GNS3/CHR/`WriteEnabled` is a parallel ops track** and must not empty or pause this table.

## What is production-ready today

### Read-only RouterOS pilot (P2 read path)

Enable with `Mfc:RouterOs:Enabled=true`:

| Capability | Port / entry | Status |
|------------|--------------|--------|
| Device identity probe | `RouterOsReadPort` | Implemented (P2-04) |
| Snapshot capture | `RouterOsSnapshotCapturePort` | Implemented (P2-05) |
| DI registration | `AddMfcRouterOs` | Implemented (P2-06) |
| Stable-read pipeline | `RouterOsStableReadCoordinatorPort` | Wired when enabled |
| Fail-closed CI default | `ProbeOnly` / `NotConfigured` stubs | `Enabled=false` default |

Operator checklist: [`pilot-runbook.md`](../operations/pilot-runbook.md).

### Controller + Desktop (MVP/M7 + alignment P0–P2)

- gRPC services: Inventory, Snapshot, Policy, Zone, Onboarding, Deployment, Drift, Audit, Routing assurance, Incident APIs.
- PostgreSQL persistence with EF migrations bundle.
- Avalonia Desktop: seven MVP modules + routing assurance viewers + Inventory **Add router** + neighbor pre-fill + VRRP pair UX + Capture/Watch/Probe/policy mutate glue.
- Bounded operational jobs, drift detection, incident response pipeline (domain + application complete).

### Known remaining product work (queued, not blockers of “code MVP”)

| Item | Queue |
|------|-------|
| Rollback without Watch stream | CONT-01 **DONE** |
| Neighbor apply does not fill VRRP member b | CONT-02 **DONE** |
| No `ListPolicies` catalog RPC | W5-01 **DONE** |
| No ManagementPath / FastTrack Desktop RPC | W5-02 **DONE** |
| Deployment semantic policy diff is `repeated string` | W5-03 |

## Intentional residuals (not defects)

Documented in [`known-limitations.md`](known-limitations.md):

| Residual | Impact |
|----------|--------|
| Write-path DI fail-closed by default | Opt-in via `Mfc:RouterOs:WriteEnabled=true`; pilot checklist in `pilot-runbook.md` (P2-11 **DONE**) |
| Live CHR matrix OFF | Scripted E2E Living Specs are DoD substitute |
| Physical CRS lab OFF | Scripted CRS fixture is DoD substitute; **not** a §3 row |
| Desktop packaging | zip/tar publish, not MSI |
| Signing | SHA256SUMS attestation; GPG/Sigstore optional |
| Process lifecycle | Desktop and Controller are separate processes; closing Desktop does not stop Controller |
| Scope lock | No NAT/routing/VRRP writes beyond managed allowlists; no auto-fix drift; no Save and Deploy |

## Verification snapshot (2026-08-31)

Local / CI checks on `main` @ `877a529` (W2.2 [#338](https://github.com/sesquicadaver/MTDirector/pull/338)):

- Alignment P0–P2 merged: W1.1–W4.4, W2.1–W2.2
- CI: Linux validate, Windows Desktop build, GitGuardian — **pass** on [#338](https://github.com/sesquicadaver/MTDirector/pull/338)

Full release gate checklist: [`release-gates.md`](release-gates.md).
