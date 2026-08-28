# Release packaging notes (M6-09)

All packaging helpers live under [`scripts/release/`](../../scripts/release/). Artifacts are written only under `OUT_DIR` (never into the git work tree unless `OUT_DIR` points there).

## Environment

| Variable | Default | Purpose |
|----------|---------|---------|
| `OUT_DIR` | *(required)* | Absolute output root |
| `MFC_RELEASE_DRY_RUN` | `0` | `1` = Living Spec layout without full publish/bundle |
| `MFC_RELEASE_RID` | `linux-x64` | `dotnet publish` RID |
| `MFC_RELEASE_CONFIG` | `Release` | Build configuration |
| `MFC_RELEASE_GPG_KEY_ID` | empty | Optional `gpg --detach-sign` for `SHA256SUMS` |

```bash
export PATH="$HOME/.dotnet:$PATH"
OUT_DIR="$(mktemp -d)"
export OUT_DIR
./scripts/release/package-controller.sh
./scripts/release/package-desktop.sh
./scripts/release/create-migration-bundle.sh
./scripts/release/run-dependency-scan.sh
./scripts/release/generate-sbom-and-checksums.sh
ls -la "$OUT_DIR"
```

## Artifacts

| Script | Output |
|--------|--------|
| `package-controller.sh` | `OUT_DIR/controller/` + `controller.artifact-path.txt` |
| `package-desktop.sh` | `OUT_DIR/desktop/` + `Mfc.Desktop-<rid>.zip` (or `.tar.gz`) + `desktop.artifact-path.txt` |
| `create-migration-bundle.sh` | `OUT_DIR/migrations/mfc-ef-migrations` |
| `run-dependency-scan.sh` | `OUT_DIR/dependency-scan.txt` |
| `generate-sbom-and-checksums.sh` | `OUT_DIR/sbom.cdx.json`, `SHA256SUMS`, `SHA256SUMS.asc` |

## Desktop installer (MVP)

Avalonia Desktop is packaged as a **framework-dependent publish directory archived as zip/tar**. That archive is the MVP **installer substitute** (not MSI/setup.exe). A native MSI/AppImage/setup.exe is a documented residual (see [`known-limitations.md`](known-limitations.md)).

Cross-platform build/run steps: [`../howto/build-and-run.md`](../howto/build-and-run.md) (`linux-x64` default, `win-x64` via `MFC_RELEASE_RID`).

## Migration bundle

Production schema apply uses the EF Core migrations bundle (`dotnet ef migrations bundle`) produced by `create-migration-bundle.sh`. Local Development may continue using:

```bash
dotnet run --project src/Mfc.Controller -- --environment Development --migrate-only
```

## Supported RouterOS / hardware

Capability evaluation and the embedded compatibility manifest remain the SoT — see [`../development/support-manifest.md`](../development/support-manifest.md). MVP topologies: standalone, dual-stack, multi-WAN failover/balanced, VRRP active/passive + split-master, CRS switch (INPUT/OUTPUT only).
