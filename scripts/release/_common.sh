#!/usr/bin/env bash
# Shared helpers for MVP release packaging scripts (M6-09).
set -euo pipefail

mfc_release_repo_root() {
  local here
  # BASH_SOURCE[0] is this file (scripts/release/_common.sh) even when called from a sourcing script.
  here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  local dir="$here"
  while [[ "$dir" != "/" ]]; do
    if [[ -f "$dir/MikroTikFirewallController.sln" ]]; then
      printf '%s\n' "$dir"
      return 0
    fi
    dir="$(dirname "$dir")"
  done
  echo "error: repository root (MikroTikFirewallController.sln) not found from $here" >&2
  return 1
}

mfc_release_require_out_dir() {
  if [[ -z "${OUT_DIR:-}" ]]; then
    echo "error: OUT_DIR is required (absolute path for release artifacts)" >&2
    return 1
  fi
  mkdir -p "$OUT_DIR"
  OUT_DIR="$(cd "$OUT_DIR" && pwd)"
}

mfc_release_is_dry_run() {
  [[ "${MFC_RELEASE_DRY_RUN:-0}" == "1" ]]
}
