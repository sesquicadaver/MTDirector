# PLAN-26 — Code-audit remediation tranche (`11cb746`)

**Date:** 2026-09-11 (inventory **DONE** 2026-09-11; **COMPLETE** 2026-09-15)  
**Status:** **PLAN-26 COMPLETE** — seed **W7-205 DONE**; Inventory **DONE** (W7-206); ranks 1…14 **DONE** (AUDIT-RULE-01…AUDIT-INT-01); seed **W7-237 DONE**; successor **PLAN-27 COMPLETE**; **PLAN-28 COMPLETE**; successor **PLAN-29** inventory **W7-250 OPEN** / seed **W7-251 OPEN**
**Audit SHA:** `11cb746de60191e6eb83e52013f7f544306d5c9d`  
**Normative audit:** [`docs/audits/MTDirector-audit-11cb746-20260911.md`](../audits/MTDirector-audit-11cb746-20260911.md)  
**Predecessor:** PLAN-25 Desktop Inventory/Zones/Add-router AutomationProperties **COMPLETE**  
**Successor:** [`plan-27-desktop-snapshot-panel-automation.md`](plan-27-desktop-snapshot-panel-automation.md) (deferred DESK-A11Y-SNAP-01 / DESK-A11Y-PANEL-01)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Static code audit (2026-09-11) found material P1 defects in capture projection, policy update/analysis, deployment recovery/watchdog, and GUI synthetic plans. Green CI does not prove GUI→Controller→CHR. Inventory of remediation into atomic §3 rows is **DONE**; all ranked remediations are **DONE**; lab/CHR/`WriteEnabled` are **not** stop-gates.

## Principles

1. Prefer server-side truth (facts, fingerprints, analysis, sealed artifacts) over Desktop-fabricated payloads.  
2. One atomic PR per ranked ID; Living Spec + tests in the same cycle.  
3. Do not delete unused algorithms solely because callers are missing — wire or gate them explicitly.  
4. GUI E2E rates from a parallel lab run are **out of scope** as proof for these static findings (audit §«Що не оголошено»).

## Out of scope (do not seed in AUDIT-RULE-01)

- PLAN-25 a11y Inventory/Zones Names (COMPLETE)  
- Declaring zone-key whitespace a defect without normative ban  
- Treating `NotConfigured*` fail-closed adapters as bugs  
- Re-running GUI E2E as proof of these static findings  

## Inventory evidence (2026-09-11)

Normative audit §§01–19 mapped to ranked IDs below. First wave (issue body): rule predicate round-trip + GUI mutation owner context.

## Ranked remediation tranche (audit fix sequence)

| Rank | ID | Gap (audit §) | Evidence (paths) | Queue |
|------|----|---------------|------------------|-------|
| 1 | **AUDIT-RULE-01** | Update rule drops predicate / hidden fields (§03) | `PoliciesViewModel`, `PolicyPanelService`, `PolicyRuleFactory` | **W7-210 (#825) DONE** |
| 2 | **AUDIT-CTX-01** | Node switch does not invalidate mutation context (§10) | `ZonesViewModel`, `OnboardingViewModel`, `DeploymentViewModel` | **W7-212 (#829) DONE**; seed **W7-213 (#830) DONE** |
| 3 | **AUDIT-CAP-01** | Canonical filter omits firewall match fields (§01) | `DiscoveryCanonicalProjector.ProjectOrderedFilter` | **W7-214 (#833) DONE**; seed **W7-215 (#834) DONE** |
| 4 | **AUDIT-CAP-02** | Required-section read failure can complete snapshot (§02) | `RouterOsDiscoveryReader`, `SnapshotCaptureResultBuilder`, `RequiredSectionCaptureGate` | **W7-216 (#837) DONE**; seed **W7-217 (#838) DONE** |
| 5 | **AUDIT-AN-01** | Validate/Record skip full analysis + mandatory tests (§04); Compose→Record INFO (§13) | `ValidateRevisionUseCase`, `PolicyPanelService`, `PolicyApprovalGate` | **W7-218 DONE (#841)**; seed **W7-219 DONE (#842)** |
| 6 | **AUDIT-AN-02** | Analysis fingerprint CAS uses client value (§05) | `PoliciesViewModel`, `CompileNodeFilterArtifactsUseCase` | **W7-220 (#845) DONE**; seed **W7-221 (#846) DONE** |
| 7 | **AUDIT-DIFF-01** | Semantic policy diff can hide reachability change (§06) | `PolicyRevisionDiffer` | **W7-222 (#849) DONE**; seed **W7-223 (#850) DONE** |
| 8 | **AUDIT-GUARD-01** | ManagementPath incomplete guard contract (§14) | `ManagementPathAnalysis`, `ActualFilterMarker` | **W7-224 (#855) DONE**; seed **W7-225 (#856) DONE** |
| 9 | **AUDIT-DEP-01** | Background recovery vs active deployment (§07); missing durable lock/journal wiring | `RecoverNonterminalOperationsJobUseCase`, `DeploymentWorkflowUseCases` | **W7-226 (#859) DONE**; seed **W7-227 (#860)** |
| 10 | **AUDIT-DEP-02** | Watchdog uses Controller clock + fixed TTL (§08); cleanup result ignored (§09) | `RouterOsDeploymentRuntime`, `RecoverDeploymentUseCase` | **W7-228 (#863) DONE**; seed **W7-229 (#864)** |
| 11 | **AUDIT-DEP-03** | Fake VRRP reachability/traffic facts (§15) | `RouterOsVrrpMemberDeploymentRuntime` | **W7-230 (#867) DONE**; seed **W7-231 (#868)** |
| 12 | **AUDIT-GUI-01** | Onboarding/Deployment synthetic payloads; Deploy never enables (§11) | `OnboardingViewModel.DefaultFacts`, `DeploymentViewModel`, `PoliciesViewModel.CanNeverDeploy` | **W7-232 (#871) DONE**; seed **W7-233 (#872) DONE** |
| 13 | **AUDIT-AUTH-01** | Production operator authorization DenyAll (§12) | `Program.cs`, `AllowListedOperatorAuthorizationBoundary` | **W7-234 (#875) DONE**; seed **W7-235 (#876) DONE** |
| 14 | **AUDIT-INT-01** | FastTrack topology not wired (§16); verification session disposal (§17); progress/Watch/auth/hubs (§18–19) | compile context, deployment sessions, gRPC Watch hubs | **W7-236 (#879) DONE**; seed **W7-237 (#880) DONE** |

Disconnected DI/callers table in the audit (locks, evidence mappers, multi-WAN verifier, incident TTL job, watchdog residue) is absorbed into **AUDIT-DEP-*** / **AUDIT-INT-01** acceptance notes — not separate vanity deletes.

## Residual notes (COMPLETE)

- All ranked remediations 1…14 closed on `main`.  
- No further PLAN-26 product rows — continuous queue advances to **PLAN-27** (deferred Desktop Snapshot/Panel a11y from PLAN-25).  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates.

## Dual track

Product §3 never waits on GNS3. Controlled CHR verification is DoD for deploy/capture rows after unit/integration Living Specs.

## §3.C ordering

1. **PLAN-25 COMPLETE** (W7-209).  
2. **W7-205 DONE** — seed PLAN-26.  
3. **W7-206 DONE** — PLAN-26 inventory; seeded **W7-210** / **W7-211**.  
4. **W7-210 DONE** — AUDIT-RULE-01 predicate/logging/exceptionEligible round-trip.
5. **W7-211 DONE** — seeded **W7-212** / **W7-213**.
6. **W7-212 DONE** — AUDIT-CTX-01 node-switch mutation invalidation.
7. **W7-213 DONE** — seeded **W7-214** / **W7-215**.
8. **W7-214 DONE** — AUDIT-CAP-01 canonical match fields.
9. **W7-215 DONE** — seeded **W7-216** / **W7-217**.
10. **W7-216 DONE** — AUDIT-CAP-02 required-section fail-closed.
11. **W7-217 DONE** — seeded **W7-218** / **W7-219**.
12. **W7-218 DONE** — AUDIT-AN-01 closed.
13. **W7-219 DONE** — seeded **W7-220** / **W7-221**.
14. **W7-220 DONE** — AUDIT-AN-02 controller fingerprint CAS.
15. **W7-221 DONE** — seeded **W7-222** / **W7-223**.
16. **W7-222 DONE** — AUDIT-DIFF-01 semantic reachability diff.
17. **W7-223 DONE** — seeded **W7-224** / **W7-225**.
18. **W7-224 DONE** — AUDIT-GUARD-01 complete guard contract.
19. **W7-225 DONE** — seeded **W7-226** / **W7-227**.
20. **W7-226 DONE** — AUDIT-DEP-01 recovery ownership gate.
21. **W7-227 DONE** — seeded **W7-228** / **W7-229**.
22. **W7-228 DONE** — AUDIT-DEP-02.
23. **W7-229 DONE** — seeded **W7-230** / **W7-231**.
24. **W7-230 DONE** — AUDIT-DEP-03.
25. **W7-231 DONE** — seeded **W7-232** / **W7-233**.
26. **W7-232 DONE** — AUDIT-GUI-01.
27. **W7-233 DONE** — seeded **W7-234** / **W7-235**.
28. **W7-234 DONE** — AUDIT-AUTH-01 deny-by-default operator allowlist.
29. **W7-235 DONE** — seeded **W7-236** / **W7-237**.
30. **W7-236 DONE** — AUDIT-INT-01 closed (rank 14 last remediation).
31. **W7-237 DONE** — PLAN-26 COMPLETE; seeded PLAN-27 (**W7-238** / **W7-239**).

## §3.C NEXT

**PLAN-26 COMPLETE.** **PLAN-27 COMPLETE.** **PLAN-28 COMPLETE.** Successor **PLAN-29** inventory **OPEN** (W7-250); seed **W7-251 OPEN**. **§3.C NEXT = W7-250 (#907)** — PLAN-29 Inventory Desktop connection health / reconnect after Controller stop.
