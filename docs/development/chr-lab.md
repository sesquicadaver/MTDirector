# CHR lab isolation (M0-09)

## Network isolation checklist

- [ ] Self-hosted runner labeled for CHR has **no default route** toward corporate/production networks.
- [ ] DNS resolvers on the lab host are lab-local only (not production resolvers).
- [ ] Management NICs for CHR VMs use a private RFC1918 / documentation-range subnet dedicated to the lab.
- [ ] Outbound Internet for package download is either blocked or proxied through an allow-listed lab gateway (never production edge).

## PKI and credentials

- Generate a **fresh test CA** per environment (or per CI job). Do not import production CA material.
- Device API/SSL credentials are generated per run and deleted in cleanup.
- Credentials must never be copied between topologies or persisted in Git.

## Reset procedure (global)

1. Power off all topology VMs.
2. Restore each VM disk to the clean snapshot named in `topology.json` → `reset.snapshotName`.
3. Delete ephemeral credential files under `testlab/chr/private/run-*`.
4. Recreate the test CA if the job requires a new environment.
5. Verify `manifest.local.json` `imageSha256` still matches the local image.

## Cleanup procedure (global)

1. Collect logs needed for the CI artifact (redact secrets).
2. Destroy ephemeral VMs / disks created for the job.
3. Wipe `private/run-*` credential and certificate material.
4. Confirm no CHR image or license was staged for commit (`git status` under `testlab/chr`).

## Runner policy

RouterOS integration workflow (when enabled) must target only an isolated self-hosted runner. Untrusted PR code must not run with production network access.

## Standalone vertical-slice acceptance (M1-30)

1. Provision with `testlab/chr/scripts/provision-standalone.sh` (outside `Mfc.RouterOs`).
2. Export `MFC_CHR_STANDALONE_HOST` (optional `MFC_CHR_STANDALONE_PORT`, default 8729).
3. Run live TLS gate: `dotnet test tests/Mfc.RouterOs.IntegrationTests --filter FullyQualifiedName~LiveChrApiSsl`.
4. Always-on (no CHR image) path: `dotnet test tests/Mfc.IntegrationTests --filter FullyQualifiedName~StandaloneVerticalSlice` (Postgres Testcontainers).

## Multi-WAN vertical-slice acceptance (M1-31)

1. Provision with `testlab/chr/scripts/provision-multi-wan.sh failover|balanced`.
2. Optional live hosts: `MFC_CHR_MULTIWAN_FAILOVER_HOST` / `MFC_CHR_MULTIWAN_BALANCED_HOST`.
3. Always-on path: `dotnet test tests/Mfc.IntegrationTests --filter FullyQualifiedName~MultiWan`.
