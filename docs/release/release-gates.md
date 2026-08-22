# Release gates checklist (M6-09)

Execute every gate before requesting acceptance review. Checkboxes are the operator record; Living Spec asserts this checklist document stays present and complete.

## Engineering gates

- [ ] `dotnet restore MikroTikFirewallController.sln --locked-mode`
- [ ] `dotnet format MikroTikFirewallController.sln --verify-no-changes --no-restore`
- [ ] `dotnet build MikroTikFirewallController.sln -c Release --no-restore`
- [ ] Unit + architecture: `dotnet test tests/Mfc.UnitTests -c Release --no-build`
- [ ] Integration (Postgres Testcontainers): `dotnet test tests/Mfc.IntegrationTests -c Release --no-build`
- [ ] Working tree clean after build/test (CI job `Verify working tree unchanged`)

## Acceptance suites (Live CHR OFF)

- [ ] Standalone / dual-stack E2E Living Spec (`StandaloneDualStackE2ELivingSpecTests`) — CHR DoD substitute
- [ ] Multi-WAN E2E Living Spec (`MultiWanE2ELivingSpecTests`) — CHR DoD substitute
- [ ] VRRP / CRS E2E Living Spec (`VrrpCrsE2ELivingSpecTests`) — CHR + physical CRS DoD substitute
- [ ] Fault-injection (`FullyQualifiedName~FaultInjection`)
- [ ] Security Living Spec (`SecurityBackupRestoreLivingSpecTests`)
- [ ] Backup/restore Integration (`SecurityBackupRestoreAcceptanceTests`)
- [ ] MVP release Living Spec (`MvpReleaseAcceptanceLivingSpecTests`)

## Supply-chain / packaging

- [ ] Dependency scan: `OUT_DIR=… ./scripts/release/run-dependency-scan.sh` (no `Severity:` lines)
- [ ] Controller package: `./scripts/release/package-controller.sh`
- [ ] Desktop publish archive: `./scripts/release/package-desktop.sh`
- [ ] EF migrations bundle: `./scripts/release/create-migration-bundle.sh`
- [ ] SBOM + `SHA256SUMS`: `./scripts/release/generate-sbom-and-checksums.sh`
- [ ] Signing policy reviewed: [`RELEASE_SIGNING.md`](RELEASE_SIGNING.md)

## Tracker / docs

- [ ] ROADMAP: N1-07 DONE, **MVP CLOSED**, M7.1-01 DONE, M7.1-02 DONE, M7.1-03 DONE, M7.1-04 DONE, M7.1-05 DONE, M7.1-06 DONE, M7.1-07 DONE, NEXT = M7.1-08 (#117)
- [ ] CHANGELOG Unreleased entry for N1-07 / MVP CLOSED
- [ ] Known limitations match scope ([`known-limitations.md`](known-limitations.md))
- [ ] GitHub issues M0–M6 + N1-07 closed (M6-01…M6-09 #100–#108; N1-07 #109)
- [ ] **Do not** create `git tag` / GitHub Release until acceptance review (AC16)

## Residual (optional — not MVP DoD blockers)

- [ ] Live CHR matrix on isolated self-hosted runner (`MFC_CHR_*`)
- [ ] Live physical CRS lab against `testlab/chr/topologies/crs-switch`
- [ ] CI cryptographic signing with production GPG/Sigstore key
