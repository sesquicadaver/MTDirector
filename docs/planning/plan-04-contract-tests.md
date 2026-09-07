# PLAN-04 — Contract-test / API Living Spec product tranche

**Date:** 2026-09-07  
**PLAN issue / queue:** [W7-58 / PLAN-04 #514](https://github.com/sesquicadaver/MTDirector/issues/514)  
**Predecessor:** PLAN-03 quality gates COMPLETE (W7-51…W7-56); product seed W7-57  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

PLAN-03 exhausted operator/docs quality gates. PLAN-04 keeps `/autopilot` from idling by seeding **gRPC / proto contract Living Spec** gaps: services that already have unit Living Specs or proto shape tests but lack (or only partially have) **Controller host** contract coverage comparable to Inventory / Policy / Onboarding / Snapshot.

## Out of scope (do not seed)

- Lab / CHR / physical CRS / `WriteEnabled` flip — ops-parallel, not §3 stop-gates  
- Anti-goals: local Desktop `SemanticDiffEngine`, auto-fix drift, fake VRRP roles  
- Replacing existing unit Living Specs with host tests (host **adds** surface coverage)

## Evidence baseline (already covered)

| Surface | Proto contract | GrpcHost / integration |
|---------|----------------|------------------------|
| InventoryService | `InventoryProtoContractTests` | `InventoryGrpcHostTests` |
| PolicyService | `PolicyProtoContractTests` | `PolicyGrpcHostTests` |
| OnboardingService | `OnboardingProtoContractTests` | `OnboardingGrpcHostTests` |
| SnapshotService | `SnapshotProtoContractTests` | `SnapshotGrpcHostTests` |
| DeploymentService | `DeploymentProtoContractTests` | `DeploymentGrpcHostTests` (W7-59) |
| ZoneService | `ZoneProtoContractTests` | `ZoneGrpcHostTests` (W7-60) |
| DriftService | `DriftProtoContractTests` | **missing GrpcHost** |
| AuditService | `AuditProtoContractTests` | **missing GrpcHost** |
| RoutingAssuranceService | **no ProtoContractTests** | **missing GrpcHost** (Desktop Living Spec only) |
| IncidentService | **no ProtoContractTests** | SEC-06 unit Living Spec only — **missing GrpcHost** |

## Ranked contract-test tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CT-DEPLOY-01** | DeploymentService has proto tests + unit Living Specs but no Controller GrpcHost contract | Critical write path RPCs; pattern: `OnboardingGrpcHostTests` | **W7-59 DONE** (#516) |
| 2 | **CT-ZONE-01** | ZoneService mutations lack GrpcHost contract | `ZoneProtoContractTests` only | **W7-60 DONE** (#518) |
| 3 | **CT-DRIFT-01** | DriftService read path lacks GrpcHost contract | `DriftProtoContractTests` only | **W7-61 OPEN** (#520) |
| 4 | **CT-AUDIT-01** | AuditService list path lacks GrpcHost contract | `AuditProtoContractTests` only | seed after CT-DRIFT-01 |
| 5 | **CT-ROUTING-01** | RoutingAssuranceService lacks ProtoContract + GrpcHost | Desktop Living Spec only | seed after CT-AUDIT-01 |
| 6 | **CT-INCIDENT-01** | IncidentService lacks ProtoContract + GrpcHost | `IncidentGrpcSec06LivingSpecTests` unit only | seed after CT-ROUTING-01 |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## §3.C NEXT

**§3.C NEXT = W7-61 (#520)** — CT-DRIFT-01 DriftService GrpcHost contract Living Spec.
