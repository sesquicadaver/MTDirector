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
| `package-controller.sh` | `OUT_DIR/controller/` (includes bundled `mfc-controller.service` + `mfc-controller.winsw.xml`, OPS-HOST-BUNDLE-01, plus `mfc-controller.env.example`, OPS-HOST-ENV-01, plus `mfc-controller.sysusers` + `mfc-controller.tmpfiles`, OPS-HOST-SYSUSERS-01, plus `README.md` from `packaging/doc/mfc/`, OPS-HOST-DOC-01) + `controller.artifact-path.txt` |
| `package-desktop.sh` | `OUT_DIR/desktop/` (includes bundled `mfc-desktop.desktop` + `mfc-desktop-start-menu.ps1`, DESK-HOST-BUNDLE-01) + `Mfc.Desktop-<rid>.zip` (or `.tar.gz`) + `desktop.artifact-path.txt` |
| `create-migration-bundle.sh` | `OUT_DIR/migrations/mfc-ef-migrations` |
| `run-dependency-scan.sh` | `OUT_DIR/dependency-scan.txt` |
| `generate-sbom-and-checksums.sh` | `OUT_DIR/sbom.cdx.json`, `SHA256SUMS`, `SHA256SUMS.asc` |

## Host-process templates

| Template | Path | Notes |
|----------|------|-------|
| systemd (Linux) | [`../../packaging/systemd/mfc-controller.service`](../../packaging/systemd/mfc-controller.service) | OPS-HOST-SYSTEMD-01 — framework-dependent Controller; `WorkingDirectory`/`ExecStart` → `/opt/mfc/controller/Mfc.Controller`; also copied into `$OUT_DIR/controller/` by `package-controller.sh` (OPS-HOST-BUNDLE-01) |
| EnvironmentFile sample | [`../../packaging/systemd/mfc-controller.env.example`](../../packaging/systemd/mfc-controller.env.example) | OPS-HOST-ENV-01 — documented `MFC__…` keys (no secrets) for `EnvironmentFile=-/etc/mfc/controller.env`; also copied into `$OUT_DIR/controller/` by `package-controller.sh` |
| sysusers.d / tmpfiles.d | [`../../packaging/systemd/mfc-controller.sysusers`](../../packaging/systemd/mfc-controller.sysusers) · [`../../packaging/systemd/mfc-controller.tmpfiles`](../../packaging/systemd/mfc-controller.tmpfiles) | OPS-HOST-SYSUSERS-01 — `mfc` user/group + `/etc/mfc` + `/var/lib/mfc` (+ trusted-ca) + `/opt/mfc/controller`; also copied into `$OUT_DIR/controller/` by `package-controller.sh` |
| Windows Service (WinSW) | [`../../packaging/windows/mfc-controller.winsw.xml`](../../packaging/windows/mfc-controller.winsw.xml) | OPS-HOST-WINSVC-01 — framework-dependent Controller; `%BASE%\Mfc.Controller.exe`; also copied into `$OUT_DIR/controller/` by `package-controller.sh` (OPS-HOST-BUNDLE-01); do not invent MSI (W7-22) |
| Operator README (`Documentation=`) | [`../../packaging/doc/mfc/README.md`](../../packaging/doc/mfc/README.md) | OPS-HOST-DOC-01 — matches `Documentation=file:///usr/share/doc/mfc/README.md`; copied into `$OUT_DIR/controller/README.md` by `package-controller.sh`; install to `/usr/share/doc/mfc/README.md` |

## Desktop launch templates

| Template | Path | Notes |
|----------|------|-------|
| freedesktop `.desktop` (Linux) | [`../../packaging/linux/mfc-desktop.desktop`](../../packaging/linux/mfc-desktop.desktop) | DESK-HOST-LINUX-01 — framework-dependent Desktop; `Exec`/`Path` → `/opt/mfc/desktop/Mfc.Desktop`; also copied into `$OUT_DIR/desktop/` by `package-desktop.sh` (DESK-HOST-BUNDLE-01) |
| Windows Start Menu sketch | [`../../packaging/windows/mfc-desktop-start-menu.ps1`](../../packaging/windows/mfc-desktop-start-menu.ps1) | DESK-HOST-WIN-01 — framework-dependent Desktop; creates Start Menu `.lnk` → `C:\mfc\desktop\Mfc.Desktop.exe`; also copied into `$OUT_DIR/desktop/` by `package-desktop.sh` (DESK-HOST-BUNDLE-01); do not invent MSI/AppImage (W7-22) |

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
