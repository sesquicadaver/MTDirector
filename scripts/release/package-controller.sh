#!/usr/bin/env bash
# Publish Controller self-contained / framework-dependent package into OUT_DIR/controller.
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

mkdir -p "$DEST"

if mfc_release_is_dry_run; then
  cat >"$DEST/Mfc.Controller.runtimeconfig.json" <<EOF
{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.AspNetCore.App","version":"10.0.0"},"dryRun":true}}
EOF
  printf 'MFC Controller dry-run package (%s)\n' "$RID" >"$DEST/Mfc.Controller"
  chmod +x "$DEST/Mfc.Controller"
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

printf '%s\n' "$DEST" >"$OUT_DIR/controller.artifact-path.txt"
echo "controller package: $DEST"
