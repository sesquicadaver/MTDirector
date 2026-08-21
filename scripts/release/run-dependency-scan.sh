#!/usr/bin/env bash
# NuGet vulnerability scan — fails on any Severity line (matches CI).
# Writes report to OUT_DIR/dependency-scan.txt (or ./artifacts when OUT_DIR unset for CI docs).
# Usage: OUT_DIR=/tmp/mfc-rel ./scripts/release/run-dependency-scan.sh
set -euo pipefail
# shellcheck source=_common.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_common.sh"

REPO_ROOT="$(mfc_release_repo_root)"
OUT_DIR="${OUT_DIR:-$REPO_ROOT/artifacts/release-scan}"
mkdir -p "$OUT_DIR"
OUT_DIR="$(cd "$OUT_DIR" && pwd)"
REPORT="$OUT_DIR/dependency-scan.txt"

export PATH="${HOME}/.dotnet:${PATH}"

if mfc_release_is_dry_run; then
  cat >"$REPORT" <<'EOF'
# MFC dependency scan dry-run
# Policy: unresolved Critical (any Severity: line from `dotnet list … --vulnerable`) fails the gate.
# Live scan: unset MFC_RELEASE_DRY_RUN and re-run this script (same as CI Package vulnerability scan).
The given projects have no vulnerable packages given the current sources. (dry-run)
EOF
  echo "dry-run: dependency scan report at $REPORT"
  exit 0
fi

cd "$REPO_ROOT"
dotnet list MikroTikFirewallController.sln package --vulnerable --include-transitive | tee "$REPORT"

if grep -Eq 'Severity[[:space:]]*:' "$REPORT"; then
  echo "error: vulnerable NuGet packages detected (see $REPORT)" >&2
  exit 1
fi

echo "dependency scan clean: $REPORT"
