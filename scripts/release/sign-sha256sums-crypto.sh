#!/usr/bin/env bash
# QG-SIGN-02: opt-in cryptographic signing gate for SHA256SUMS (GPG and/or Sigstore/cosign).
# Does not replace QG-SIGN-01 cleartext SHA256SUMS generation.
# Default (no crypto env): skip with exit 0 — never fails PR CI for missing org secrets.
#
# Usage:
#   OUT_DIR=/tmp/mfc-rel ./scripts/release/sign-sha256sums-crypto.sh
# Self-test (no secrets; Living Spec / local gate proof):
#   MFC_RELEASE_SIGN_SELFTEST=1 OUT_DIR=/tmp/mfc-rel ./scripts/release/sign-sha256sums-crypto.sh
# GPG (optional):
#   MFC_RELEASE_GPG_KEY_ID=<keyid> OUT_DIR=... ./scripts/release/sign-sha256sums-crypto.sh
# Cosign blob sign (optional):
#   MFC_RELEASE_COSIGN=1 COSIGN_KEY=/path/to/cosign.key OUT_DIR=... ./scripts/release/sign-sha256sums-crypto.sh
set -euo pipefail
# shellcheck source=_common.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_common.sh"

mfc_release_require_out_dir

SUMS="$OUT_DIR/SHA256SUMS"
GATE_STATUS="$OUT_DIR/SHA256SUMS.crypto-gate.json"
GPG_ASC="$OUT_DIR/SHA256SUMS.asc"
COSIGN_SIG="$OUT_DIR/SHA256SUMS.sig"
COSIGN_BUNDLE="$OUT_DIR/SHA256SUMS.cosign-bundle.json"

stamp="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
modes=()
notes=()

write_gate_status() {
  local mode="$1"
  local detail="$2"
  cat >"$GATE_STATUS" <<EOF
{
  "bomFormat": "MfcCryptoGate",
  "specVersion": "qg-sign-02",
  "timestamp": "$stamp",
  "artifact": "SHA256SUMS",
  "mode": "$mode",
  "detail": "$detail",
  "optIn": true,
  "defaultPrCiRequiresSecrets": false
}
EOF
}

if [[ ! -f "$SUMS" ]]; then
  echo "error: missing $SUMS — run generate-sbom-and-checksums.sh first (QG-SIGN-01 cleartext lock)" >&2
  exit 1
fi

# Self-test: prove the crypto gate path exists without org secrets or real keys.
if [[ "${MFC_RELEASE_SIGN_SELFTEST:-0}" == "1" ]]; then
  modes+=("selftest")
  notes+=("selftest-ok")
  if command -v gpg >/dev/null 2>&1; then
    notes+=("gpg-tool-present")
  else
    notes+=("gpg-tool-absent")
  fi
  if command -v cosign >/dev/null 2>&1; then
    notes+=("cosign-tool-present")
  else
    notes+=("cosign-tool-absent")
  fi
  write_gate_status "selftest" "QG-SIGN-02 crypto gate self-test; no secrets required; SHA256SUMS untouched"
  echo "QG-SIGN-02 self-test: $GATE_STATUS (${notes[*]})"
  exit 0
fi

signed=0

if [[ -n "${MFC_RELEASE_GPG_KEY_ID:-}" ]]; then
  if ! command -v gpg >/dev/null 2>&1; then
    echo "error: MFC_RELEASE_GPG_KEY_ID set but gpg not found on PATH" >&2
    exit 1
  fi
  gpg --batch --yes --detach-sign --armor -u "$MFC_RELEASE_GPG_KEY_ID" -o "$GPG_ASC" "$SUMS"
  modes+=("gpg-detach-sign")
  signed=1
  echo "QG-SIGN-02 GPG: wrote $GPG_ASC"
fi

if [[ "${MFC_RELEASE_COSIGN:-0}" == "1" ]]; then
  if ! command -v cosign >/dev/null 2>&1; then
    echo "error: MFC_RELEASE_COSIGN=1 but cosign not found on PATH" >&2
    exit 1
  fi
  if [[ -n "${COSIGN_KEY:-}" ]]; then
    cosign sign-blob --yes --key "$COSIGN_KEY" --output-signature "$COSIGN_SIG" --output-bundle "$COSIGN_BUNDLE" "$SUMS"
    modes+=("cosign-sign-blob-key")
  else
    # Keyless/OIDC path when COSIGN_KEY unset — still opt-in via MFC_RELEASE_COSIGN=1.
    cosign sign-blob --yes --output-signature "$COSIGN_SIG" --output-bundle "$COSIGN_BUNDLE" "$SUMS"
    modes+=("cosign-sign-blob-keyless")
  fi
  signed=1
  echo "QG-SIGN-02 cosign: wrote $COSIGN_SIG / $COSIGN_BUNDLE"
fi

if [[ "$signed" -eq 0 ]]; then
  write_gate_status "skipped" "No MFC_RELEASE_GPG_KEY_ID / MFC_RELEASE_COSIGN; opt-in crypto gate idle (QG-SIGN-01 cleartext SHA256SUMS remains)"
  echo "QG-SIGN-02: skipped (opt-in idle) — $GATE_STATUS"
  exit 0
fi

mode_joined="$(IFS=,; echo "${modes[*]}")"
write_gate_status "$mode_joined" "Cryptographic signature(s) produced for SHA256SUMS"
echo "QG-SIGN-02: done ($mode_joined) — $GATE_STATUS"
