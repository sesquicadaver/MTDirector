# PLAN-26 — Code-audit remediation tranche (`11cb746`)

**Date:** 2026-09-11  
**Status:** Queued after **PLAN-25 COMPLETE**; seed **W7-205 (#814)** OPEN (**§3.C NEXT**); inventory **W7-206 (#815)** OPEN  
**Audit SHA:** `11cb746de60191e6eb83e52013f7f544306d5c9d`  
**Normative audit:** [`docs/audits/MTDirector-audit-11cb746-20260911.md`](../audits/MTDirector-audit-11cb746-20260911.md)  
**Predecessor:** PLAN-25 Desktop Inventory/Zones/Add-router AutomationProperties **COMPLETE**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Static code audit (2026-09-11) found material P1 defects in capture projection, policy update/analysis, deployment recovery/watchdog, and GUI synthetic plans. Green CI does not prove GUI→Controller→CHR. This PLAN inventories remediation into atomic §3 rows; lab/CHR/`WriteEnabled` are **not** stop-gates.

## Principles

1. Prefer server-side truth (facts, fingerprints, analysis, sealed artifacts) over Desktop-fabricated payloads.  
2. One atomic PR per ranked ID; Living Spec + tests in the same cycle.  
3. Do not delete unused algorithms solely because callers are missing — wire or gate them explicitly.  
4. GUI E2E rates from a parallel lab run are **out of scope** as proof for these static findings (audit §«Що не оголошено»).

## Out of scope (do not seed here)

- PLAN-25 a11y Inventory/Zones Names (stays on W7-204…)  
- Declaring zone-key whitespace a defect without normative ban  
- Treating `NotConfigured*` fail-closed adapters as bugs  

## Ranked remediation tranche (audit fix sequence)

| Rank | ID | Gap (audit §) | Evidence (paths) | Queue |
|------|----|---------------|------------------|-------|
| 1 | **AUDIT-RULE-01** | Update rule drops predicate / hidden fields (§03) | `PoliciesViewModel`, `PolicyPanelService`, `PolicyRuleFactory` | seed after PLAN-26 inventory |
| 2 | **AUDIT-CTX-01** | Node switch does not invalidate mutation context (§10) | `ZonesViewModel`, `OnboardingViewModel`, `DeploymentViewModel` | seed after AUDIT-RULE-01 |
| 3 | **AUDIT-CAP-01** | Canonical filter omits firewall match fields (§01) | `DiscoveryCanonicalProjector.ProjectOrderedFilter` | seed after AUDIT-CTX-01 |
| 4 | **AUDIT-CAP-02** | Required-section read failure can complete snapshot (§02) | `RouterOsDiscoveryReader`, `SnapshotCaptureResultBuilder` | seed after AUDIT-CAP-01 |
| 5 | **AUDIT-AN-01** | Validate/Record skip full analysis + mandatory tests (§04); Compose→Record INFO (§13) | `ValidateRevisionUseCase`, `PolicyPanelService`, `PolicyApprovalGate` | seed after AUDIT-CAP-02 |
| 6 | **AUDIT-AN-02** | Analysis fingerprint CAS uses client value (§05) | `PoliciesViewModel`, `CompileNodeFilterArtifactsUseCase` | seed after AUDIT-AN-01 |
| 7 | **AUDIT-DIFF-01** | Semantic policy diff can hide reachability change (§06) | `PolicyRevisionDiffer` | seed after AUDIT-AN-02 |
| 8 | **AUDIT-GUARD-01** | ManagementPath incomplete guard contract (§14) | `ManagementPathAnalysis`, `ActualFilterMarker` | seed after AUDIT-DIFF-01 |
| 9 | **AUDIT-DEP-01** | Background recovery vs active deployment (§07); missing durable lock/journal wiring | `RecoverNonterminalOperationsJobUseCase`, `DeploymentWorkflowUseCases` | seed after AUDIT-GUARD-01 |
| 10 | **AUDIT-DEP-02** | Watchdog uses Controller clock + fixed TTL (§08); cleanup result ignored (§09) | `RouterOsDeploymentRuntime`, `RecoverDeploymentUseCase` | seed after AUDIT-DEP-01 |
| 11 | **AUDIT-DEP-03** | Fake VRRP reachability/traffic facts (§15) | `RouterOsVrrpMemberDeploymentRuntime` | seed after AUDIT-DEP-02 |
| 12 | **AUDIT-GUI-01** | Onboarding/Deployment synthetic payloads; Deploy never enables (§11) | `OnboardingViewModel.DefaultFacts`, `DeploymentViewModel`, `PoliciesViewModel.CanNeverDeploy` | seed after AUDIT-DEP-03 |
| 13 | **AUDIT-AUTH-01** | Production operator authorization DenyAll (§12) | `Program.cs`, `DenyAllAuthorizationBoundary` | seed after AUDIT-GUI-01 |
| 14 | **AUDIT-INT-01** | FastTrack topology not wired (§16); verification session disposal (§17); progress/Watch/auth/hubs (§18–19) | compile context, deployment sessions, gRPC Watch hubs | seed after AUDIT-AUTH-01 |

Disconnected DI/callers table in the audit (locks, evidence mappers, multi-WAN verifier, incident TTL job, watchdog residue) is absorbed into **AUDIT-DEP-*** / **AUDIT-INT-01** acceptance notes — not separate vanity deletes.

## Dual track

Product §3 never waits on GNS3. Controlled CHR verification is DoD for deploy/capture rows after unit/integration Living Specs.

## §3.C ordering

1. Finish **PLAN-25** (NEXT = W7-204).  
2. **W7-205** — seed PLAN-26.  
3. **W7-206** — PLAN-26 inventory → open **AUDIT-RULE-01** + follow-up seeds.  
4. Execute ranks 1…14 atomically.
