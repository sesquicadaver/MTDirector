#!/usr/bin/env bash
# Generate SBOM (+ SHA-256 checksums) for artifacts under OUT_DIR.
# Prefers CycloneDX `dotnet CycloneDX` when installed; otherwise emits a CycloneDX-lite JSON
# from `dotnet list package --include-transitive` inventory.
# Usage: OUT_DIR=/tmp/mfc-rel ./scripts/release/generate-sbom-and-checksums.sh
# Dry-run: MFC_RELEASE_DRY_RUN=1 OUT_DIR=... ./scripts/release/generate-sbom-and-checksums.sh
#
# AUDIT-SBOM-01: real mode (MFC_RELEASE_DRY_RUN≠1) fail-closed —
# missing SDK, failed inventory, or empty components → exit ≠ 0.
set -euo pipefail
# shellcheck source=_common.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_common.sh"

REPO_ROOT="$(mfc_release_repo_root)"
mfc_release_require_out_dir

SBOM="$OUT_DIR/sbom.cdx.json"
SUMS="$OUT_DIR/SHA256SUMS"
INVENTORY="$OUT_DIR/package-inventory.txt"
export PATH="${HOME}/.dotnet:${PATH}"

write_lite_sbom() {
  local mode="$1"
  local stamp components_json
  stamp="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  components_json="${2:-[]}"
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
    "tools": [{ "name": "scripts/release/generate-sbom-and-checksums.sh", "version": "audit-sbom-01" }],
    "properties": [
      { "name": "mfc:sbom:mode", "value": "${mode}" },
      { "name": "mfc:repo", "value": "MTDirector" }
    ]
  },
  "components": ${components_json}
}
EOF
}

require_dotnet() {
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "error: AUDIT-SBOM-01: dotnet SDK not found in PATH (real mode requires SDK)" >&2
    return 1
  fi
  if ! dotnet --list-sdks 2>/dev/null | grep -q .; then
    echo "error: AUDIT-SBOM-01: dotnet is present but no SDKs are installed (real mode)" >&2
    return 1
  fi
}

has_cyclonedx() {
  command -v cyclonedx-dotnet >/dev/null 2>&1 \
    || dotnet tool run CycloneDX --help >/dev/null 2>&1 \
    || dotnet CycloneDX --help >/dev/null 2>&1
}

run_cyclonedx() {
  cd "$REPO_ROOT"
  if command -v cyclonedx-dotnet >/dev/null 2>&1; then
    cyclonedx-dotnet "$REPO_ROOT/MikroTikFirewallController.sln" -o "$SBOM"
  elif dotnet tool run CycloneDX --help >/dev/null 2>&1; then
    dotnet tool run CycloneDX -- "$REPO_ROOT/MikroTikFirewallController.sln" -o "$SBOM"
  else
    dotnet CycloneDX "$REPO_ROOT/MikroTikFirewallController.sln" -o "$SBOM"
  fi
}

write_inventory_and_lite_sbom() {
  local mode="$1"
  (
    cd "$REPO_ROOT"
    dotnet list MikroTikFirewallController.sln package --include-transitive >"$INVENTORY"
  )
  write_lite_sbom "$mode" "$(components_json_from_inventory "$INVENTORY")"
}

# Build a JSON array of CycloneDX components from `dotnet list package` inventory text.
components_json_from_inventory() {
  local inv="$1"
  python3 - "$inv" <<'PY'
import json, re, sys
path = sys.argv[1]
text = open(path, encoding="utf-8", errors="replace").read()
# Match lines like: "   > Package.Name      1.2.3      1.2.3"
pat = re.compile(r"^\s*>\s+(\S+)\s+(\S+)\s+(\S+)\s*$", re.M)
seen = {}
for m in pat.finditer(text):
    name, _requested, resolved = m.group(1), m.group(2), m.group(3)
    if name.startswith("["):
        continue
    seen[(name, resolved)] = {
        "type": "library",
        "name": name,
        "version": resolved,
        "purl": f"pkg:nuget/{name}@{resolved}",
    }
print(json.dumps(list(seen.values()), ensure_ascii=True))
PY
}

sbom_component_count() {
  python3 - "$SBOM" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
comps = doc.get("components") or []
print(len(comps))
PY
}

fail_if_empty_components_real() {
  if mfc_release_is_dry_run; then
    return 0
  fi
  local count
  count="$(sbom_component_count)"
  if [[ "$count" -lt 1 ]]; then
    echo "error: AUDIT-SBOM-01: SBOM components is empty in real mode (count=${count})" >&2
    return 1
  fi
}

if mfc_release_is_dry_run; then
  write_lite_sbom "dry-run" "[]"
else
  require_dotnet

  if has_cyclonedx; then
    if run_cyclonedx && [[ "$(sbom_component_count)" -ge 1 ]]; then
      :
    else
      echo "warning: CycloneDX unavailable or empty; falling back to dotnet list package" >&2
      write_inventory_and_lite_sbom "fallback-list-package"
    fi
  else
    write_inventory_and_lite_sbom "lite-no-cyclonedx-tool"
  fi

  fail_if_empty_components_real
fi

# Checksums over all regular files under OUT_DIR except the sums file itself.
(
  cd "$OUT_DIR"
  # shellcheck disable=SC2035
  find . -type f ! -name 'SHA256SUMS' ! -name 'SHA256SUMS.asc' -print0 \
    | sort -z \
    | xargs -0 sha256sum
) >"$SUMS"

# Detached cleartext attestation (QG-SIGN-01) or real GPG when configured.
# Never claim cryptographic success without GPG (AUDIT-SBOM-01).
SIG="$OUT_DIR/SHA256SUMS.asc"
if [[ -n "${MFC_RELEASE_GPG_KEY_ID:-}" ]]; then
  if ! command -v gpg >/dev/null 2>&1; then
    echo "error: AUDIT-SBOM-01: MFC_RELEASE_GPG_KEY_ID set but gpg is not available" >&2
    exit 1
  fi
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
if mfc_release_is_dry_run; then
  echo "mode: dry-run (empty components allowed)"
else
  echo "mode: real (components=$(sbom_component_count))"
fi
