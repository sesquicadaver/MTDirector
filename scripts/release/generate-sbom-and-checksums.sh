#!/usr/bin/env bash
# Generate SBOM (+ SHA-256 checksums) for artifacts under OUT_DIR.
# Prefers CycloneDX `dotnet CycloneDX` when installed; otherwise emits a CycloneDX-lite JSON
# from `dotnet list package --include-transitive` inventory.
# Usage: OUT_DIR=/tmp/mfc-rel ./scripts/release/generate-sbom-and-checksums.sh
# Dry-run: MFC_RELEASE_DRY_RUN=1 OUT_DIR=... ./scripts/release/generate-sbom-and-checksums.sh
set -euo pipefail
# shellcheck source=_common.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_common.sh"

REPO_ROOT="$(mfc_release_repo_root)"
mfc_release_require_out_dir

SBOM="$OUT_DIR/sbom.cdx.json"
SUMS="$OUT_DIR/SHA256SUMS"
export PATH="${HOME}/.dotnet:${PATH}"

write_lite_sbom() {
  local stamp
  stamp="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  cat >"$SBOM" <<EOF
{
  "bomFormat": "CycloneDX",
  "specVersion": "1.5",
  "version": 1,
  "metadata": {
    "timestamp": "$stamp",
    "component": {
      "type": "application",
      "name": "MTDirector",
      "version": "mvp"
    },
    "tools": [{ "name": "scripts/release/generate-sbom-and-checksums.sh", "version": "m6-09" }],
    "properties": [
      { "name": "mfc:sbom:mode", "value": "${1}" },
      { "name": "mfc:repo", "value": "MTDirector" }
    ]
  },
  "components": []
}
EOF
}

if mfc_release_is_dry_run; then
  write_lite_sbom "dry-run"
else
  if dotnet tool run CycloneDX --help >/dev/null 2>&1 \
    || command -v cyclonedx-dotnet >/dev/null 2>&1 \
    || dotnet CycloneDX --help >/dev/null 2>&1; then
    # Best-effort CycloneDX CLI (local or global tool).
    if ! (
      cd "$REPO_ROOT"
      if command -v cyclonedx-dotnet >/dev/null 2>&1; then
        cyclonedx-dotnet "$REPO_ROOT/MikroTikFirewallController.sln" -o "$SBOM"
      else
        dotnet CycloneDX "$REPO_ROOT/MikroTikFirewallController.sln" -o "$SBOM" 2>/dev/null \
          || dotnet tool run CycloneDX -- "$REPO_ROOT/MikroTikFirewallController.sln" -o "$SBOM"
      fi
    ); then
      write_lite_sbom "fallback-list-package"
      {
        echo "# Package inventory (see also: dotnet list package --include-transitive)"
        (cd "$REPO_ROOT" && dotnet list MikroTikFirewallController.sln package --include-transitive 2>/dev/null | head -n 200) || true
      } >"$OUT_DIR/package-inventory.txt"
    fi
  else
    write_lite_sbom "lite-no-cyclonedx-tool"
    (
      cd "$REPO_ROOT"
      dotnet list MikroTikFirewallController.sln package --include-transitive >"$OUT_DIR/package-inventory.txt" || true
    )
  fi
fi

# Checksums over all regular files under OUT_DIR except the sums file itself.
(
  cd "$OUT_DIR"
  # shellcheck disable=SC2035
  find . -type f ! -name 'SHA256SUMS' ! -name 'SHA256SUMS.asc' -print0 \
    | sort -z \
    | xargs -0 sha256sum
) >"$SUMS"

# Detached cleartext "signature" placeholder for MVP (see RELEASE_SIGNING.md).
# Real GPG detach-sign only when MFC_RELEASE_GPG_KEY_ID is set and gpg is available.
SIG="$OUT_DIR/SHA256SUMS.asc"
if [[ -n "${MFC_RELEASE_GPG_KEY_ID:-}" ]] && command -v gpg >/dev/null 2>&1; then
  gpg --batch --yes --detach-sign --armor -u "$MFC_RELEASE_GPG_KEY_ID" -o "$SIG" "$SUMS"
else
  cat >"$SIG" <<EOF
-----BEGIN MFC MVP CHECKSUM ATTESTATION-----
# Not a cryptographic signature.
# Policy: CI signing gate documented in docs/release/RELEASE_SIGNING.md.
# Artifact: SHA256SUMS (cleartext detached checksums).
# Generated: $(date -u +%Y-%m-%dT%H:%M:%SZ)
-----END MFC MVP CHECKSUM ATTESTATION-----
EOF
fi

echo "SBOM: $SBOM"
echo "checksums: $SUMS"
echo "attestation: $SIG"
