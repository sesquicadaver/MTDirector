# Release signing policy (MVP / M6-09 + QG-SIGN-02)

## MVP attestation model (QG-SIGN-01 — unchanged)

For MVP production acceptance:

1. Generate cleartext detached checksums: `OUT_DIR/SHA256SUMS` via `scripts/release/generate-sbom-and-checksums.sh`.
2. Emit `OUT_DIR/SHA256SUMS.asc` as either:
   - a **cleartext attestation placeholder** (default), or
   - a real `gpg --detach-sign --armor` signature when `MFC_RELEASE_GPG_KEY_ID` is set and a signing key is available.

Cleartext checksums + this policy document satisfy Issue Set M6-09 AC13 for MVP. Cryptographic signing is **not** enabled by default and is **not** required for MVP.

## Opt-in cryptographic signing gate (QG-SIGN-02)

Beyond QG-SIGN-01 cleartext, operators may enable a **documented opt-in crypto path** that does **not** require org secrets on every PR:

| Mode | How | Output |
|------|-----|--------|
| Self-test | `MFC_RELEASE_SIGN_SELFTEST=1 OUT_DIR=… ./scripts/release/sign-sha256sums-crypto.sh` | `SHA256SUMS.crypto-gate.json` (`mode=selftest`) — no secrets |
| GPG | `MFC_RELEASE_GPG_KEY_ID=<id> OUT_DIR=… ./scripts/release/sign-sha256sums-crypto.sh` | Real armored detach-sign `SHA256SUMS.asc` |
| Sigstore/cosign | `MFC_RELEASE_COSIGN=1` (+ `COSIGN_KEY` or keyless) | `SHA256SUMS.sig` + `SHA256SUMS.cosign-bundle.json` |
| Idle skip | No crypto env vars | `SHA256SUMS.crypto-gate.json` (`mode=skipped`) — exit 0 |

CI entrypoint: [`.github/workflows/release-signing.yml`](../../.github/workflows/release-signing.yml) — **`workflow_dispatch` only** (not `pull_request`). Self-test always runs on dispatch; GPG/cosign steps run only when the corresponding secrets are present (`if: secrets…`).

Checklist: [`signing-gate.md`](../development/signing-gate.md) (QG-SIGN-02). Living Spec: `QgSign02ReleaseSigningLivingSpecTests`.

## CI signing gate (future / production org key)

Before publishing a GitHub Release with a **mandatory** org release key (still a future ops hardening step — not default PR CI):

1. Run packaging scripts on a trusted runner.
2. Re-run vulnerability scan; fail on any `Severity:` line.
3. Sign `SHA256SUMS` with the org release key (GPG or Sigstore).
4. Attach `sbom.cdx.json`, `SHA256SUMS`, and the detached signature to the Release.

## Release tag gate (AC16)

Acceptance review signed off **2026-08-24**. Git tag **`v0.2.0`** on `main` marks MVP CLOSED + Post-MVP M7 CLOSED (M7.1…M7.4).

Future tags follow the same gate: green CI, checked [`release-gates.md`](release-gates.md), updated CHANGELOG.
