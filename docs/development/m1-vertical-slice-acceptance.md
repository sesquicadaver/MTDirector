# M1 read-only vertical-slice acceptance report

**Milestone:** M1 — Read-Only Vertical Slice  
**Issue:** [M1-34 / #44](https://github.com/sesquicadaver/MTDirector/issues/44)  
**PR title:** `docs(read-path): complete vertical-slice acceptance`  
**Status:** M1 CLOSED (formal acceptance package)

This document is the Living Specification index for M1 DoD: operator can register a device, capture stable snapshots over API-SSL, view inventory/snapshot/diff in Desktop, and prove topology suites without RouterOS write commands in product code.

## Operator documentation map

| Topic | Document |
|-------|----------|
| Local build / run Controller + Desktop | [`local-environment.md`](local-environment.md) |
| Connection profiles (trust, secrets, Desktop-safe summaries) | [`connection-profiles.md`](connection-profiles.md) |
| Synthetic CHR device + topologies | [`chr-lab.md`](chr-lab.md), [`../../testlab/chr/README.md`](../../testlab/chr/README.md) |
| Snapshot schema, canonicalization, semantic diff | [`snapshots-and-diff.md`](snapshots-and-diff.md) |
| Support / compatibility manifest | [`support-manifest.md`](support-manifest.md) |
| Troubleshooting read-path | [`troubleshooting-read-path.md`](troubleshooting-read-path.md) |
| Test filters / Living Spec matrices | [`testing.md`](testing.md) |
| Recovery / DB restore | [`../operations/recovery.md`](../operations/recovery.md) |

Normative specs remain in the repository root — index: [`docs/specs/README.md`](specs/README.md) (`Read-Only Vertical Slice — Technical Design v0.1.md`, Canonical Snapshot Spec, RouterOS Read Adapter Spec, Initial Issue Set).

## Acceptance criteria → evidence

| # | Criterion | Evidence |
|--:|-----------|----------|
| 1 | Standalone matrix | `dotnet test --filter FullyQualifiedName~StandaloneVerticalSlice` + Living Spec in testing.md |
| 2 | Multi-WAN matrix | `FullyQualifiedName~MultiWan` |
| 3 | VRRP matrix | `FullyQualifiedName~VrrpVerticalSlice` |
| 4 | Fault-injection suite | `FullyQualifiedName~FaultInjection` |
| 5 | Identical snapshot → same configuration hash | Standalone AC#4 (dedupe by snapshot hash) |
| 6 | Runtime role/route changes ≠ config drift | Multi-WAN active-state / VRRP role switch obs-only hashes |
| 7 | Desktop shows server data | `DesktopVerticalSliceWiringTests` + inventory/snapshot/diff ViewModels |
| 8 | No RouterOS write commands in production assemblies | `ArchitectureBoundaryTests.RouterOsMustNotExposeForbiddenWriteNamespaces` + `RegistryRejectsWriteAndNonPrintPaths` |
| 9 | Desktop has no RouterOS credentials | ADR 0005; connection summaries omit password; Desktop uses Contracts-only clients |
| 10 | Fixtures have no production data | `testlab/chr/fixtures/README.md` + skeleton forbidden-extension checks |
| 11 | Dependency scan | `dotnet list MikroTikFirewallController.sln package --vulnerable --include-transitive` |
| 12 | Architecture tests | `FullyQualifiedName~ArchitectureBoundary` |
| 13 | Database restore smoke | `--migrate-only` + [`recovery.md`](../operations/recovery.md); `BootstrapPersistenceTests` / schema migrate tests |
| 14 | CHANGELOG milestone entry | `CHANGELOG.md` — M1 Closed section |
| 15 | Known limitations | § Known limitations below |
| 16 | Issues M1-01—M1-33 closed | GitHub milestone / ISSUES map |
| 17 | No open blockers | Open blockers tracked in ISSUES/ROADMAP only when filed |
| 18 | RC on clean environment | § Clean-environment RC below |

## Clean-environment release candidate

On a machine without prior MFC state:

```bash
export PATH="$HOME/.dotnet:$PATH"
git clone https://github.com/sesquicadaver/MTDirector.git
cd MTDirector
dotnet tool restore
dotnet restore MikroTikFirewallController.sln --locked-mode
dotnet build MikroTikFirewallController.sln -c Release --no-restore
dotnet test MikroTikFirewallController.sln -c Release --no-build \
  --filter "FullyQualifiedName~StandaloneVerticalSlice\
|FullyQualifiedName~MultiWan\
|FullyQualifiedName~VrrpVerticalSlice\
|FullyQualifiedName~FaultInjection\
|FullyQualifiedName~ArchitectureBoundary\
|FullyQualifiedName~RegistryRejectsWrite\
|FullyQualifiedName~DesktopVerticalSlice\
|FullyQualifiedName~M1VerticalSliceAcceptance"
docker compose -f testlab/postgres/compose.yml up -d
dotnet run --project src/Mfc.Controller -- --environment Development --migrate-only
dotnet list MikroTikFirewallController.sln package --vulnerable --include-transitive
```

Live CHR is optional and env-gated (`MFC_CHR_*_HOST`); always-on acceptance uses in-process Controller + Testcontainers Postgres.

## Known limitations (M1)

- Packet-path blockers **N1-04** and remaining N1 weave items are not part of M1 CLOSED (queued after M1 in ROADMAP).
- Live CHR golden hashes remain placeholders until a self-hosted isolated runner captures them.
- Desktop observation fields (reachability / VRRP role labels) stay unset until probe wiring beyond M1-27 defaults.
- `GetDiscoveryStatus` from Issue Set is deferred; discovery status is observed via `StartCapture` / `WatchCapture`.
- GitHub Actions validate workflows may run with empty `steps` when billing blocks runners; local gates above are authoritative for M1 DoD.
- No policy compile/deploy (M2–M4), no write-path RouterOS mutations, no multi-tenant / web UI.

## Milestone close statement

With M1-01…M1-34 delivered and the matrices above green locally, **M1 is CLOSED** and ready for release review. Linear queue continues at **M2-01 (#48)** per [`ROADMAP.md`](../../ROADMAP.md).
