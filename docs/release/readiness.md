# Project readiness assessment

**As of:** 2026-08-26  
**Baseline commit:** `main` @ P2-06 (`65dd413`)  
**Release tag:** [`v0.2.0`](https://github.com/sesquicadaver/MTDirector/releases/tag/v0.2.0) (2026-08-24)

This document summarizes **code + documentation readiness** against the normative queue in [`ROADMAP.md`](../../ROADMAP.md). It is not a substitute for operator acceptance ([`mvp-acceptance.md`](mvp-acceptance.md)) or release gates ([`release-gates.md`](release-gates.md)).

## Executive summary

| Layer | Status | Notes |
|-------|--------|-------|
| MVP (M0–M6 + N1) | **100% CLOSED** | 109/109 issues in code audit |
| Post-MVP M7 (M7.1–M7.4) | **100% CLOSED** | 27/27 issues in code audit |
| P2 read path (P2-04…P2-06) | **100% CLOSED** | Production probe + capture + DI gate |
| P2 write path | **NOT STARTED** | Onboarding/deploy runtimes still default stubs |
| Linear queue (§3) | **0 open** | Next tranche = future P2 write-path issues |

**Overall code readiness:** all 139 mapped atomic issues in [`ISSUES.md`](../../ISSUES.md) are **DONE in code**.  
**Production pilot readiness (read-only):** **ready** when `Mfc:RouterOs:Enabled=true` + PostgreSQL + device connection profiles — see [`pilot-runbook.md`](../operations/pilot-runbook.md).  
**Production pilot readiness (write path):** **not ready** — `NotConfiguredOnboardingRuntime`, `NotConfiguredDeploymentRuntime`, and related ports remain fail-closed by default.

## Milestone matrix

| Segment | Closed | Open | Evidence |
|---------|-------:|-----:|----------|
| M0 Bootstrap | 10 | 0 | ROADMAP §2.2, architecture unit tests |
| M1 Read-only | 34 | 0 | Living Spec M1-01…M1-34 |
| N1 Packet-path | 7 | 0 | N1-01…N1-07 Living Specs |
| M2 Policy | 18 | 0 | M2 Living Specs + Desktop authoring |
| M3 Compiler | 8 | 0 | M3-08 acceptance matrix |
| M5 Onboarding | 10 | 0 | Domain + API; runtime stub until P2 write |
| M4 Safe deploy | 13 | 0 | Domain + API; runtime stub until P2 write |
| M6 E2E / drift | 9 | 0 | M6-09 acceptance package |
| M7 Post-MVP | 27 | 0 | M7.1…M7.4 Living Specs |
| P2 read path | 3 | 0 | P2-04…P2-06 + `PilotReadinessLivingSpecTests` |
| **Total** | **139** | **0** | ROADMAP §4 |

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

### Controller + Desktop (MVP/M7 feature code)

- gRPC services: Inventory, Snapshot, Policy, Zone, Onboarding, Deployment, Drift, Audit, Routing assurance, Incident APIs.
- PostgreSQL persistence with EF migrations bundle.
- Avalonia Desktop: seven MVP modules + routing assurance viewers.
- Bounded operational jobs, drift detection, incident response pipeline (domain + application complete).

## Intentional residuals (not defects)

Documented in [`known-limitations.md`](known-limitations.md):

| Residual | Impact |
|----------|--------|
| Write-path runtimes stubbed | Onboarding/deploy/feedback delivery require future P2 write-path wiring |
| Live CHR matrix OFF | Scripted E2E Living Specs are DoD substitute |
| Desktop packaging | zip/tar publish, not MSI |
| Signing | SHA256SUMS attestation; GPG/Sigstore optional |
| Scope lock | No NAT/routing/VRRP writes beyond managed allowlists; no auto-fix drift |

## Verification snapshot (2026-08-26)

Local checks on `main`:

- `dotnet build MikroTikFirewallController.sln -c Release` — **pass**
- `dotnet test tests/Mfc.UnitTests -c Release --filter FullyQualifiedName~LivingSpecTests` — **811 pass**
- CI (PR #287): Linux validate, Windows Desktop, CHR skeleton contracts, GitGuardian — **pass**

Full release gate checklist: [`release-gates.md`](release-gates.md).

## Tracker hygiene note

[`ISSUES.md`](../../ISSUES.md) maps **139/139 DONE**. Some GitHub issues in the M7 range (#127–#136) may still show **OPEN** in the GitHub UI while code and ROADMAP mark them **DONE** — reconcile the tracker in a dedicated hygiene PR (ROADMAP §6 wave 0 pattern). This does **not** block code readiness.

## Next delivery

**P2 write-path queue** (not yet filed in `ISSUES.md`):

- Wire `IOnboardingRuntime` / `IDeploymentRuntime` production adapters
- Replace remaining `NotConfigured*` ports in `Program.cs`
- Live pilot for onboarding → deploy → rollback on lab CHR

Until then, the linear atomic queue in ROADMAP §3 is **empty**.
