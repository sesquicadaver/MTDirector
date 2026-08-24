#!/usr/bin/env bash
# Build EF Core migrations bundle into OUT_DIR/migrations.
# Usage: OUT_DIR=/tmp/mfc-rel ./scripts/release/create-migration-bundle.sh
# Dry-run: MFC_RELEASE_DRY_RUN=1 OUT_DIR=... ./scripts/release/create-migration-bundle.sh
set -euo pipefail
# shellcheck source=_common.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_common.sh"

REPO_ROOT="$(mfc_release_repo_root)"
mfc_release_require_out_dir

DEST="$OUT_DIR/migrations"
BUNDLE="$DEST/mfc-ef-migrations"
CONFIG="${MFC_RELEASE_CONFIG:-Release}"

mkdir -p "$DEST"

if mfc_release_is_dry_run; then
  cat >"$BUNDLE" <<'EOF'
#!/usr/bin/env bash
echo "MFC EF migrations bundle dry-run placeholder"
exit 0
EOF
  chmod +x "$BUNDLE"
  printf '%s\n' "$BUNDLE" >"$OUT_DIR/migrations.artifact-path.txt"
  echo "dry-run: migration bundle written to $BUNDLE"
  exit 0
fi

export PATH="${HOME}/.dotnet:${PATH}"
cd "$REPO_ROOT"
dotnet tool restore
# Framework-dependent bundle (requires .NET runtime on target). EF CLI has no `--self-contained false`.
dotnet ef migrations bundle \
  --project src/Mfc.Infrastructure/Mfc.Infrastructure.csproj \
  --startup-project src/Mfc.Controller/Mfc.Controller.csproj \
  --configuration "$CONFIG" \
  --output "$BUNDLE" \
  --force

printf '%s\n' "$BUNDLE" >"$OUT_DIR/migrations.artifact-path.txt"
echo "migration bundle: $BUNDLE"
