# Release signing residual gate (QG-SIGN-01)

**Queue:** W7-56 / QG-SIGN-01  
**Policy:** [`RELEASE_SIGNING.md`](../release/RELEASE_SIGNING.md)  
**Gates:** [`release-gates.md`](../release/release-gates.md)

Operator checklist (Living Spec enforces durable invariants):

- [ ] MVP path remains **cleartext `SHA256SUMS`** (+ optional GPG when `MFC_RELEASE_GPG_KEY_ID` is set)
- [ ] `RELEASE_SIGNING.md` still documents CI cryptographic signing (GPG/Sigstore) as a **future** gate — not default-enabled production crypto
- [ ] `release-gates.md` references `RELEASE_SIGNING.md` and SBOM/`SHA256SUMS` scripts
- [ ] `known-limitations.md` keeps the signing residual explicit

Automated gate: `QgSign01ReleaseSigningLivingSpecTests` (`dotnet test --filter "FullyQualifiedName~QgSign01"`).

This gate **does not** turn on production cryptographic signing.
