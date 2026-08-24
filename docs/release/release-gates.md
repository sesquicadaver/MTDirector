# Release gates checklist (M6-09 + M7 acceptance)

Execute every gate before requesting acceptance review. Checkboxes are the operator record; Living Spec asserts this checklist document stays present and complete.

**Acceptance review:** signed off **2026-08-24**. Release tag: **`v0.2.0`** on `main` (`4347bee` lineage).

## Engineering gates

- [x] `dotnet restore MikroTikFirewallController.sln --locked-mode`
- [x] `dotnet format MikroTikFirewallController.sln --verify-no-changes --no-restore`
- [x] `dotnet build MikroTikFirewallController.sln -c Release --no-restore`
- [x] Unit + architecture: `dotnet test tests/Mfc.UnitTests -c Release --no-build`
- [x] Integration (Postgres Testcontainers): `dotnet test tests/Mfc.IntegrationTests -c Release --no-build`
- [x] Working tree clean after build/test (CI job `Verify working tree unchanged`)

## Acceptance suites (Live CHR OFF)

- [x] Standalone / dual-stack E2E Living Spec (`StandaloneDualStackE2ELivingSpecTests`) — CHR DoD substitute
- [x] Multi-WAN E2E Living Spec (`MultiWanE2ELivingSpecTests`) — CHR DoD substitute
- [x] VRRP / CRS E2E Living Spec (`VrrpCrsE2ELivingSpecTests`) — CHR + physical CRS DoD substitute
- [x] Fault-injection (`FullyQualifiedName~FaultInjection`)
- [x] Security Living Spec (`SecurityBackupRestoreLivingSpecTests`)
- [x] Backup/restore Integration (`SecurityBackupRestoreAcceptanceTests`)
- [x] MVP release Living Spec (`MvpReleaseAcceptanceLivingSpecTests`)
- [x] Incident response E2E Living Spec (`IncidentResponseE2ELivingSpecTests`) — M7.4-06

## Supply-chain / packaging

- [x] Dependency scan: `OUT_DIR=… ./scripts/release/run-dependency-scan.sh` (no `Severity:` lines)
- [x] Controller package: `./scripts/release/package-controller.sh`
- [x] Desktop publish archive: `./scripts/release/package-desktop.sh`
- [x] EF migrations bundle: `./scripts/release/create-migration-bundle.sh`
- [x] SBOM + `SHA256SUMS`: `./scripts/release/generate-sbom-and-checksums.sh`
- [x] Signing policy reviewed: [`RELEASE_SIGNING.md`](RELEASE_SIGNING.md)

## Tracker / docs

- [x] ROADMAP: **MVP CLOSED**, **M7.1 CLOSED**, **M7.2 CLOSED**, **M7.3 CLOSED**, **M7.4 CLOSED**; Post-MVP M7 = **0** open
- [x] CHANGELOG `[0.2.0]` entry
- [x] Known limitations match scope ([`known-limitations.md`](known-limitations.md))
- [x] GitHub issues M0–M6 + N1-07 + M7.1…M7.4 closed (#1–#136)
- [x] Git release tag **`v0.2.0`** created after acceptance review (`git tag v0.2.0`; AC16)

## Residual (optional — not MVP/M7 DoD blockers)

- [ ] Live CHR matrix on isolated self-hosted runner (`MFC_CHR_*`)
- [ ] Live physical CRS lab against `testlab/chr/topologies/crs-switch`
- [ ] CI cryptographic signing with production GPG/Sigstore key
