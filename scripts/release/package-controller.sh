#!/usr/bin/env bash
# Publish Controller self-contained / framework-dependent package into OUT_DIR/controller.
# OPS-HOST-BUNDLE-01: also copies PLAN-32 host-process templates into OUT_DIR/controller/
# (mfc-controller.service + mfc-controller.winsw.xml) so release trees carry them.
# OPS-HOST-ENV-01: also copies packaging/systemd/mfc-controller.env.example into OUT_DIR/controller/
# so operators have a documented EnvironmentFile sample beside the unit.
# OPS-HOST-SYSUSERS-01: also copies mfc-controller.sysusers + mfc-controller.tmpfiles into
# OUT_DIR/controller/ so operators can bootstrap User=mfc and /etc/mfc + /var/lib/mfc paths.
# OPS-HOST-DOC-01: also copies packaging/doc/mfc/README.md → OUT_DIR/controller/README.md
# so Documentation=file:///usr/share/doc/mfc/README.md has a matching publish artifact.
# Usage: OUT_DIR=/tmp/mfc-rel ./scripts/release/package-controller.sh
# Dry-run (Living Spec): MFC_RELEASE_DRY_RUN=1 OUT_DIR=... ./scripts/release/package-controller.sh
set -euo pipefail
# shellcheck source=_common.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_common.sh"

REPO_ROOT="$(mfc_release_repo_root)"
mfc_release_require_out_dir

RID="${MFC_RELEASE_RID:-linux-x64}"
CONFIG="${MFC_RELEASE_CONFIG:-Release}"
DEST="$OUT_DIR/controller"

# Copy PLAN-32 Controller host-process templates + PLAN-37 env sample + PLAN-38
# sysusers/tmpfiles + PLAN-39 operator doc into the publish tree (OPS-HOST-BUNDLE-01 +
# OPS-HOST-ENV-01 + OPS-HOST-SYSUSERS-01 + OPS-HOST-DOC-01). Sources remain under packaging/.
mfc_controller_bundle_host_templates() {
  local dest="$1"
  mkdir -p "$dest"
  cp -f "$REPO_ROOT/packaging/systemd/mfc-controller.service" "$dest/mfc-controller.service"
  cp -f "$REPO_ROOT/packaging/windows/mfc-controller.winsw.xml" "$dest/mfc-controller.winsw.xml"
  cp -f "$REPO_ROOT/packaging/systemd/mfc-controller.env.example" "$dest/mfc-controller.env.example"
  cp -f "$REPO_ROOT/packaging/systemd/mfc-controller.sysusers" "$dest/mfc-controller.sysusers"
  cp -f "$REPO_ROOT/packaging/systemd/mfc-controller.tmpfiles" "$dest/mfc-controller.tmpfiles"
  cp -f "$REPO_ROOT/packaging/doc/mfc/README.md" "$dest/README.md"
}

mkdir -p "$DEST"

if mfc_release_is_dry_run; then
  cat >"$DEST/Mfc.Controller.runtimeconfig.json" <<EOF
{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.AspNetCore.App","version":"10.0.0"},"dryRun":true}}
EOF
  printf 'MFC Controller dry-run package (%s)\n' "$RID" >"$DEST/Mfc.Controller"
  chmod +x "$DEST/Mfc.Controller"
  mfc_controller_bundle_host_templates "$DEST"
  printf '%s\n' "$DEST" >"$OUT_DIR/controller.artifact-path.txt"
  echo "dry-run: controller package written to $DEST"
  exit 0
fi

export PATH="${HOME}/.dotnet:${PATH}"
dotnet publish "$REPO_ROOT/src/Mfc.Controller/Mfc.Controller.csproj" \
  -c "$CONFIG" \
  -r "$RID" \
  --self-contained false \
  -o "$DEST" \
  --nologo

mfc_controller_bundle_host_templates "$DEST"

printf '%s\n' "$DEST" >"$OUT_DIR/controller.artifact-path.txt"
echo "controller package: $DEST"
