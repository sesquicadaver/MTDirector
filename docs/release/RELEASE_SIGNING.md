# Release signing policy (MVP / M6-09)

## MVP attestation model

For MVP production acceptance:

1. Generate cleartext detached checksums: `OUT_DIR/SHA256SUMS` via `scripts/release/generate-sbom-and-checksums.sh`.
2. Emit `OUT_DIR/SHA256SUMS.asc` as either:
   - a **cleartext attestation placeholder** (default), or
   - a real `gpg --detach-sign --armor` signature when `MFC_RELEASE_GPG_KEY_ID` is set and a signing key is available.

Cleartext checksums + this policy document satisfy Issue Set M6-09 AC13 for MVP. Cryptographic signing is a **CI signing gate** for post-acceptance releases.

## CI signing gate (future / production)

Before publishing a GitHub Release:

1. Run packaging scripts on a trusted runner.
2. Re-run vulnerability scan; fail on any `Severity:` line.
3. Sign `SHA256SUMS` with the org release key (GPG or Sigstore).
4. Attach `sbom.cdx.json`, `SHA256SUMS`, and the detached signature to the Release.

## Release tag gate (AC16)

Acceptance review signed off **2026-08-24**. Git tag **`v0.2.0`** on `main` marks MVP CLOSED + Post-MVP M7 CLOSED (M7.1…M7.4).

Future tags follow the same gate: green CI, checked [`release-gates.md`](release-gates.md), updated CHANGELOG.
