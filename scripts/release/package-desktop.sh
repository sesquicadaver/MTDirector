#!/usr/bin/env bash
# Publish Desktop Avalonia app into OUT_DIR/desktop (zip publish = MVP installer substitute).
# Full MSI/setup.exe is a post-MVP residual; see docs/release/packaging.md.
# Usage: OUT_DIR=/tmp/mfc-rel ./scripts/release/package-desktop.sh
# Dry-run: MFC_RELEASE_DRY_RUN=1 OUT_DIR=... ./scripts/release/package-desktop.sh
set -euo pipefail
# shellcheck source=_common.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_common.sh"

REPO_ROOT="$(mfc_release_repo_root)"
mfc_release_require_out_dir

RID="${MFC_RELEASE_RID:-linux-x64}"
CONFIG="${MFC_RELEASE_CONFIG:-Release}"
DEST="$OUT_DIR/desktop"
ZIP_NAME="Mfc.Desktop-${RID}.zip"

mkdir -p "$DEST"

if mfc_release_is_dry_run; then
  printf 'MFC Desktop dry-run package (%s)\n' "$RID" >"$DEST/Mfc.Desktop"
  chmod +x "$DEST/Mfc.Desktop"
  printf '{"dryRun":true,"rid":"%s"}\n' "$RID" >"$DEST/appsettings.json"
  (
    cd "$OUT_DIR"
    rm -f "$ZIP_NAME"
    # Prefer zip; fall back to tar.gz when zip(1) is absent.
    if command -v zip >/dev/null 2>&1; then
      zip -qr "$ZIP_NAME" desktop
    else
      tar -czf "${ZIP_NAME%.zip}.tar.gz" desktop
      ZIP_NAME="${ZIP_NAME%.zip}.tar.gz"
    fi
  )
  printf '%s\n' "$OUT_DIR/$ZIP_NAME" >"$OUT_DIR/desktop.artifact-path.txt"
  echo "dry-run: desktop package written to $OUT_DIR/$ZIP_NAME"
  exit 0
fi

export PATH="${HOME}/.dotnet:${PATH}"
dotnet publish "$REPO_ROOT/src/Mfc.Desktop/Mfc.Desktop.csproj" \
  -c "$CONFIG" \
  -r "$RID" \
  --self-contained false \
  -o "$DEST" \
  --nologo

(
  cd "$OUT_DIR"
  rm -f "$ZIP_NAME" "${ZIP_NAME%.zip}.tar.gz"
  if command -v zip >/dev/null 2>&1; then
    zip -qr "$ZIP_NAME" desktop
  else
    tar -czf "${ZIP_NAME%.zip}.tar.gz" desktop
    ZIP_NAME="${ZIP_NAME%.zip}.tar.gz"
  fi
)

printf '%s\n' "$OUT_DIR/$ZIP_NAME" >"$OUT_DIR/desktop.artifact-path.txt"
echo "desktop installer/publish archive: $OUT_DIR/$ZIP_NAME"
