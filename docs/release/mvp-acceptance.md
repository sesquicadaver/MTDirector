# MVP production acceptance report (M6-09)

**Milestone:** M6 — End-to-End Integration (+ N1 weave)  
**Issue:** [M6-09 / #108](https://github.com/sesquicadaver/MTDirector/issues/108)  
**PR title:** `docs(release): complete MVP production acceptance`  
**Status:** **M6 CLOSED** (formal release acceptance package).  
**MVP CLOSED:** **yes** — completed by **N1-07 (#109)** per ROADMAP spine `M6(+N1-07) → MVP CLOSED`.

This document is the Living Specification index for M6-09 AC 1–16 and the milestone gate: M6 closes only after this acceptance package is green.

## Operator / release documentation map

| Topic | Document |
|-------|----------|
| Release gates checklist | [`release-gates.md`](release-gates.md) |
| Known limitations (actual MVP scope) | [`known-limitations.md`](known-limitations.md) |
| Packaging (Controller / Desktop / migrations / SBOM) | [`packaging.md`](packaging.md) |
| Artifact signing policy | [`RELEASE_SIGNING.md`](RELEASE_SIGNING.md) |
| Installation | [`../operations/installation.md`](../operations/installation.md) |
| RouterOS prerequisite checklist | [`../operations/prerequisite-checklist.md`](../operations/prerequisite-checklist.md) |
| Operations manual | [`../operations/operations-manual.md`](../operations/operations-manual.md) |
| Recovery / backup restore | [`../operations/recovery.md`](../operations/recovery.md) |
| Support / hardware profiles | [`../development/support-manifest.md`](../development/support-manifest.md) |
| Test matrices / Living Spec | [`../development/testing.md`](../development/testing.md) |
| Linear queue | [`../../ROADMAP.md`](../../ROADMAP.md) |

## Closed-issue matrix (AC1)

M0–M6 logical IDs map to GitHub via [`ISSUES.md`](../../ISSUES.md). Acceptance asserts ROADMAP §2.2 DONE markers for M0–M5 closed milestones and M6-01…M6-09.

| Block | IDs | GitHub | Status |
|-------|-----|--------|--------|
| M0 Bootstrap | M0-01…M0-10 | #1–#10 | CLOSED |
| M1 Read-only | M1-01…M1-34 | #11–#44 | CLOSED |
| M2 Policy | M2-01…M2-18 | #48–#65 | CLOSED |
| M3 Compiler | M3-01…M3-08 | #68–#75 | CLOSED |
| M5 Onboarding | M5-01…M5-10 | #76–#85 | CLOSED |
| M4 Safe deploy | M4-01…M4-13 | #86–#98 | CLOSED |
| M6 E2E | M6-01…M6-08 | #100–#107 | CLOSED |
| M6-09 acceptance | M6-09 | #108 | THIS PACKAGE |

N1 weave items N1-01…N1-07 are DONE; **MVP CLOSED**. **M7.1 CLOSED** (M7.1-01…M7.1-11 DONE). **M7.2 CLOSED** (M7.2-01…M7.2-04 DONE). **M7.3 CLOSED** (M7.3-01…M7.3-06 DONE). **M7.4 CLOSED** (M7.4-01…M7.4-06 DONE). Post-MVP M7 = **0** open.

Post-MVP M7 delivered: routing assurance (M7.1), endpoint mobility (M7.2), external correlation (M7.3), incident enforcement + E2E (M7.4).

Verify closed M6-01…M6-08 (example):

```bash
gh issue view 100 --json state -q .state   # CLOSED
# … through 107
gh issue list --search "M6-0 in:title is:closed" --limit 20
```

## Acceptance criteria → evidence

| # | Criterion | Evidence |
|--:|-----------|----------|
| 1 | All M0–M6 issues closed | ROADMAP §2.2 + matrix above; Living Spec `Ac1M0ThroughM6IssuesAreClosedInRoadmap` |
| 2 | All release gates executed | [`release-gates.md`](release-gates.md); `Ac2ReleaseGatesChecklistExists` |
| 3 | CHR test matrix green | Live CHR OFF — DoD substitute: `StandaloneDualStackE2ELivingSpecTests`, `MultiWanE2ELivingSpecTests`, `VrrpCrsE2ELivingSpecTests`, `RoutingAssuranceChrAcceptanceLivingSpecTests` (M7.1-11); residual live CHR optional only |
| 4 | Physical CRS test green | Same substitute: `VrrpCrsE2ELivingSpecTests` AC11 + `testlab/chr/topologies/crs-switch` |
| 5 | Fault-injection suite green | `FullyQualifiedName~FaultInjection` (+ M4-13 fault Living Spec) |
| 6 | Security suite green | `SecurityBackupRestoreLivingSpecTests` (M6-08 AC 1–10) |
| 7 | Backup/restore suite green | `SecurityBackupRestoreAcceptanceTests` (M6-08 AC 11–14) |
| 8 | Dependency scan no unresolved Critical | `scripts/release/run-dependency-scan.sh` + CI `Package vulnerability scan`; `Ac8DependencyScanPolicyAndScriptExist` |
| 9 | Controller package created | `scripts/release/package-controller.sh` → `OUT_DIR/controller` |
| 10 | Desktop installer created | `scripts/release/package-desktop.sh` → zip/tar publish dir (MVP installer substitute) |
| 11 | Migration bundle created | `scripts/release/create-migration-bundle.sh` |
| 12 | SBOM + SHA-256 checksums | `scripts/release/generate-sbom-and-checksums.sh` |
| 13 | Release artifacts “signed” | Cleartext `SHA256SUMS` + attestation / optional GPG; [`RELEASE_SIGNING.md`](RELEASE_SIGNING.md) |
| 14 | Known limitations match scope | [`known-limitations.md`](known-limitations.md) |
| 15 | Git working tree clean | CI gate + Living Spec script isolation; clean after this PR merges |
| 16 | Release tag only after acceptance review | **No tag in this PR**; gate documented in [`RELEASE_SIGNING.md`](RELEASE_SIGNING.md) / gates checklist |

## Clean-environment release candidate

```bash
export PATH="$HOME/.dotnet:$PATH"
git clone https://github.com/sesquicadaver/MTDirector.git
cd MTDirector
dotnet tool restore
dotnet restore MikroTikFirewallController.sln --locked-mode
dotnet build MikroTikFirewallController.sln -c Release --no-restore
dotnet test MikroTikFirewallController.sln -c Release --no-build \
  --filter "FullyQualifiedName~MvpReleaseAcceptance\
|FullyQualifiedName~StandaloneDualStackE2ELivingSpecTests\
|FullyQualifiedName~MultiWanE2ELivingSpecTests\
|FullyQualifiedName~VrrpCrsE2ELivingSpecTests\
|FullyQualifiedName~FaultInjection\
|FullyQualifiedName~SecurityBackupRestore\
|FullyQualifiedName~ArchitectureBoundary"
OUT_DIR="$(mktemp -d)" MFC_RELEASE_DRY_RUN=0 ./scripts/release/run-dependency-scan.sh
OUT_DIR="$(mktemp -d)" ./scripts/release/package-controller.sh
# … desktop, migrations, sbom (see packaging.md)
```

Live CHR / live physical CRS remain **OFF**. Optional residual: env-gated `MFC_CHR_*` on an isolated runner.

## Milestone close statement

With M6-01…M6-09 and N1-07 delivered, **M6 is CLOSED** and **MVP CLOSED**. Post-MVP **M7.1…M7.4 CLOSED** (issues #110–#136). **TRACKER-01 DONE** (#289); **PLAN-01 DONE** (#290); **P2-07…P2-11 DONE** (#293–#297) — **P2 write-path CLOSED**. Post-acceptance: alignment P0–P2 **DONE**; **CONT-01…02 DONE**; **W5-01…03 DONE**; **W6-01…W6-02 DONE**; **§3.C NEXT = W7-02 (#402)** (W7-01 DONE; SEC-01…15 DONE).  
Post-acceptance Desktop UX: Inventory **Add router** ([#309](https://github.com/sesquicadaver/MTDirector/pull/309), 2026-08-28) — see [`../development/connection-profiles.md`](../development/connection-profiles.md).

## Acceptance review (AC16)

| Field | Value |
|-------|-------|
| Review date | **2026-08-24** |
| Scope | MVP (M0–M6 + N1) + Post-MVP M7 (M7.1–M7.4) |
| Release tag | **`v0.2.0`** |
| Evidence | [`release-gates.md`](release-gates.md) (all required gates checked) |
| CI | Green on acceptance branch before tag |

Git tag **`v0.2.0`** marks the first production acceptance baseline covering MVP CLOSED and M7 CLOSED.
