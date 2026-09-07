# PLAN-03 — Quality-gate product tranche

**Date:** 2026-09-07  
**PLAN issue / queue:** [W7-51 / PLAN-03 #500](https://github.com/sesquicadaver/MTDirector/issues/500)  
**Predecessor:** residual Living Spec corpus COMPLETE (W7-49); product seed W7-50  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

Desktop/Contracts product glue from PLAN-02 is **exhausted** (CONT / W5 / W6 / SEC / W7-01…49 **DONE**). PLAN-03 keeps `/autopilot` from idling by seeding **operator/docs/quality gates** that are already required by project DoD but not yet atomic §3 rows.

## Out of scope (do not seed)

- Lab / CHR / physical CRS / `WriteEnabled` flip — ops-parallel, not §3 stop-gates  
- Anti-goals: local Desktop `SemanticDiffEngine`, auto-fix drift, fake VRRP roles  
- MVP scope lock: Policies “Save and Deploy”

## Ranked quality-gate tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **QG-IMPORT-01** | No dedicated import-graph / cyclic-dependency anomaly gate beyond NetArch assembly edges | User DoD (import graph + cycles); `ArchitectureBoundaryTests` covers refs only; gate: `ProductionImportGraph` + `QgImport01ImportGraphCycleLivingSpecTests` | **W7-52 DONE** (#502) |
| 2 | **QG-DOCS-01** | Weekly README/index ↔ fact smoke is not a recurring §3 row | User DoD (weekly docs smoke); `release-gates.md` is one-shot acceptance | **W7-53 OPEN** (#504) |
| 3 | **QG-ANTISTUB-01** | Anti-stub is DoD text; no dedicated CI scanner for stub/`NotImplemented` | ROADMAP §6 / alignment DoD | after W7-53 |
| 4 | **QG-LIVESPEC-MATRIX-01** | Living Spec matrix update-in-PR is process text; optional gate | ROADMAP §5 | optional |
| 5 | **QG-SIGN-01** | CI cryptographic signing remains unchecked residual | `release-gates.md` / `RELEASE_SIGNING.md`; W7-23 = cleartext SHA256SUMS | optional / ops |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## §3.C NEXT

**§3.C NEXT = W7-53 (#504)** — QG-DOCS-01 weekly docs smoke Living Spec gate.
