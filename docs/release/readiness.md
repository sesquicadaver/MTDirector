# Project readiness assessment

**As of:** 2026-08-26  
**Baseline commit:** `main` @ docs sync (`d73fcb4`)  
**Release tag:** [`v0.2.0`](https://github.com/sesquicadaver/MTDirector/releases/tag/v0.2.0) (2026-08-24)

This document summarizes **code + documentation readiness** against the normative queue in [`ROADMAP.md`](../../ROADMAP.md). It is not a substitute for operator acceptance ([`mvp-acceptance.md`](mvp-acceptance.md)) or release gates ([`release-gates.md`](release-gates.md)).

## Executive summary

| Layer | Status | Notes |
|-------|--------|-------|
| MVP (M0–M6 + N1) | **100% CLOSED** | 109/109 issues in code audit |
| Post-MVP M7 (M7.1–M7.4) | **100% CLOSED** | 27/27 issues in code audit |
| P2 read path (P2-04…P2-06) | **100% CLOSED** | Production probe + capture + DI gate |
| P2 write path | **NOT STARTED** | Onboarding/deploy runtimes still default stubs |
| Linear queue (§3) | **1 open** | **NEXT = PLAN-01** (#290) |

**Overall code readiness:** all 139 mapped product issues are **DONE in code**.  
**Queue integrity:** **TRACKER-01 DONE** (#289, 2026-08-26) — GitHub tracker aligned with ROADMAP §2.2.  
**Production pilot readiness (read-only):** **ready** when `Mfc:RouterOs:Enabled=true` + PostgreSQL + device connection profiles — see [`pilot-runbook.md`](../operations/pilot-runbook.md).  
**Production pilot readiness (write path):** **not ready** — `NotConfiguredOnboardingRuntime`, `NotConfiguredDeploymentRuntime`, and related ports remain fail-closed by default.

## Milestone matrix (code audit §2.2)

| Segment | Closed | Open (code) | Evidence |
|---------|-------:|------------:|----------|
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
| **Product total** | **139** | **0** | ROADMAP §2.2 |

## Linear queue (§3) — current

| # | ID | GitHub | Status |
|--:|----|-------:|--------|
| 126 | TRACKER-01 | [#289](https://github.com/sesquicadaver/MTDirector/issues/289) | **DONE** (2026-08-26) |
| 127 | PLAN-01 | [#290](https://github.com/sesquicadaver/MTDirector/issues/290) | **NEXT** — formalize P2-07+ in ROADMAP §3 |

No parallel work. P2 write-path **implementation** starts only after PLAN-01 creates atomic P2-07+ rows.

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
| Write-path runtimes stubbed | Onboarding/deploy/feedback delivery require P2-07+ (after PLAN-01) |
| Live CHR matrix OFF | Scripted E2E Living Specs are DoD substitute |
| Desktop packaging | zip/tar publish, not MSI |
| Signing | SHA256SUMS attestation; GPG/Sigstore optional |
| Scope lock | No NAT/routing/VRRP writes beyond managed allowlists; no auto-fix drift |

## Verification snapshot (2026-08-26)

Local checks on `main`:

- `dotnet build MikroTikFirewallController.sln -c Release` — **pass**
- `dotnet test tests/Mfc.UnitTests -c Release --filter FullyQualifiedName~LivingSpecTests` — **811 pass**
- CI: Linux validate, Windows Desktop, CHR skeleton contracts, GitGuardian — **pass**

Full release gate checklist: [`release-gates.md`](release-gates.md).
