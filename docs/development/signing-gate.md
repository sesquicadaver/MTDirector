# Release signing residual gate (QG-SIGN-01) + opt-in crypto (QG-SIGN-02)

**Queue:** W7-56 / QG-SIGN-01 · W7-310 / QG-SIGN-02  
**Policy:** [`RELEASE_SIGNING.md`](../release/RELEASE_SIGNING.md)  
**Gates:** [`release-gates.md`](../release/release-gates.md)

## QG-SIGN-01 — cleartext MVP lock (do not weaken)

Operator checklist (Living Spec enforces durable invariants):

- [ ] MVP path remains **cleartext `SHA256SUMS`** (+ optional GPG when `MFC_RELEASE_GPG_KEY_ID` is set)
- [ ] `RELEASE_SIGNING.md` still documents **mandatory** CI cryptographic signing with a production org key as a **future** gate — not default-enabled production crypto
- [ ] `release-gates.md` references `RELEASE_SIGNING.md` and SBOM/`SHA256SUMS` scripts
- [ ] `known-limitations.md` keeps the signing residual explicit

Automated gate: `QgSign01ReleaseSigningLivingSpecTests` (`dotnet test --filter "FullyQualifiedName~QgSign01"`).

This gate **does not** turn on production cryptographic signing by default.

## QG-SIGN-02 — opt-in cryptographic signing path

Operator checklist (Living Spec enforces durable invariants):

- [ ] `scripts/release/sign-sha256sums-crypto.sh` exists and supports `MFC_RELEASE_SIGN_SELFTEST=1` without org secrets
- [ ] `RELEASE_SIGNING.md` documents the opt-in GPG / Sigstore/cosign path (QG-SIGN-02) beyond cleartext
- [ ] `.github/workflows/release-signing.yml` is `workflow_dispatch`-only (not `pull_request`) and gates real signing behind secrets
- [ ] Default PR CI (`ci.yml`) does **not** require GPG/cosign secrets
- [ ] QG-SIGN-01 cleartext `SHA256SUMS` / SBOM generation is not regressed

Automated gate: `QgSign02ReleaseSigningLivingSpecTests` (`dotnet test --filter "FullyQualifiedName~QgSign02"`).
