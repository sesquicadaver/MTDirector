# PLAN-41 — Release signing crypto (GPG/Sigstore beyond QG-SIGN-01)

**Date:** 2026-09-17 (inventory **DONE** @ `190980c0`)  
**Status:** **PLAN-41 COMPLETE** — Inventory **DONE** (W7-308); seed **W7-309 (#1024) DONE**; implement **W7-310 (#1026) DONE**; COMPLETE seed **W7-311 (#1028) DONE**; successor **PLAN-42** inventory **W7-312 (#1031) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-308 / PLAN-41 #1023](https://github.com/sesquicadaver/MTDirector/issues/1023) **DONE**  
**Predecessor:** PLAN-40 Controller host journald/syslog identity **COMPLETE** (OPS-HOST-LOG-01)  
**Normative files:** [`RELEASE_SIGNING.md`](../release/RELEASE_SIGNING.md), [`signing-gate.md`](../development/signing-gate.md), [`generate-sbom-and-checksums.sh`](../../scripts/release/generate-sbom-and-checksums.sh), [`.github/workflows/`](../../.github/workflows/), [`QgSign01ReleaseSigningLivingSpecTests`](../../tests/Mfc.UnitTests/Documentation/QgSign01ReleaseSigningLivingSpecTests.cs)  
**Normative prior locks:** QG-SIGN-01 cleartext `SHA256SUMS` attestation; W7-22…24 packaging; PLAN-32…40 host packaging — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging** continuous-queue gap after PLAN-40 shipped Controller journald/syslog identity: MVP release signing remains **cleartext `SHA256SUMS`** (+ optional ad-hoc GPG when `MFC_RELEASE_GPG_KEY_ID` is set). Cryptographic GPG/Sigstore is still documented as a **future CI gate** (`QG-SIGN-01` / `RELEASE_SIGNING.md`), not a Living-Spec-locked production path operators can trust by default.

## Principles

1. Release integrity should progress beyond cleartext checksum attestation toward a **CI/crypto signing gate** (GPG and/or Sigstore) with Living Spec + docs.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-40 OPS-HOST-LOG / packaging host-unit polish — that wave is **COMPLETE** / saturating.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer release integrity over another publish-tree packaging vanity row.  
7. Prefer an **opt-in / documented** crypto path that does **not** require org secrets on every PR. Do not weaken QG-SIGN-01 cleartext `SHA256SUMS`.

## Out of scope (do not seed)

- Re-opening PLAN-40 OPS-HOST-LOG / systemd/WinSW packaging authoring  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Requiring org secrets in every PR CI (opt-in crypto only)  
- Splitting docs-gate vs CI opt-in cosign into separate vanity ranks when one atomic row covers both

## Inventory evidence (W7-308 @ `main` `190980c0`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `generate-sbom-and-checksums.sh` | Always writes cleartext `SHA256SUMS`; GPG detach-sign only if `MFC_RELEASE_GPG_KEY_ID` + `gpg` present; else MVP cleartext attestation placeholder in `SHA256SUMS.asc` | No Living-Spec-locked crypto gate; no Sigstore/cosign helper |
| `RELEASE_SIGNING.md` | MVP = cleartext `SHA256SUMS`; section **“CI signing gate (future / production)”** lists GPG or Sigstore as pre-Release steps | Crypto still “future”, not an authored opt-in gate operators can run/CI can enable |
| `signing-gate.md` / QG-SIGN-01 | Checklist keeps crypto as unchecked residual; `QgSign01*` asserts “future”, fail-closed against default-enabled production crypto | Confirmed intentional QG-SIGN-01 lock — **do not weaken** |
| `.github/workflows/` (`ci.yml`, `routeros-integration.yml`) | Build/test Release; **no** `SHA256SUMS` / GPG / cosign / Sigstore jobs; no `if: secrets…` signing path | Confirmed: no CI crypto path (opt-in or otherwise) |
| Glob / rg | `cosign` / `sigstore` absent under workflows/scripts as a gated path @ `190980c0` | Confirmed |
| Docs matrix | `release-gates.md` unchecked “CI cryptographic signing…”; `known-limitations` keeps GPG/Sigstore residual; `testing.md` QG-SIGN-01 matrix only | QG-SIGN-02 surface not Living-Spec-locked |

**Ranking decision:** Prefer **ONE atomic row** (**QG-SIGN-02**) covering docs + Living Spec + opt-in script/workflow crypto path. A docs-only gate without an executable opt-in path would leave the same “future residual” hole; a required-secrets CI job would violate the no-org-secrets-on-every-PR constraint. No natural second product rank without vanity split.

## Ranked release-signing crypto tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **QG-SIGN-02** | Author opt-in CI/release cryptographic signing gate (GPG detach-sign and/or Sigstore/cosign) + docs/Living Spec beyond QG-SIGN-01 cleartext; default PR CI stays secret-free; do not regress `SHA256SUMS`/SBOM | Future-gate docs + optional-only GPG; no cosign/Sigstore CI @ `190980c0` | implement **W7-310 (#1026) DONE** after seed **W7-309 (#1024) DONE**; COMPLETE **W7-311 (#1028)** |

Inventory (**W7-308 DONE**) confirmed sole rank. Seed **W7-309 DONE** advanced NEXT to QG-SIGN-02 implement; COMPLETE seed **W7-311** opens after SIGN-02.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-40 CLOSED)

PLAN-40 sole ranked row (**OPS-HOST-LOG-01**) is **DONE**. No further PLAN-40 product rows. Packaging host-unit polish is saturated (WinSW already has rolled logs).

## Adjacent residuals (seeded as PLAN-41 COMPLETE / PLAN-42)

- Controller HTTP liveness/readiness probes beyond gRPC health — **PLAN-42** [`plan-42-controller-http-health-probes.md`](plan-42-controller-http-health-probes.md)

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- WinSW binary redistribution — not §3 (operator-supplied)  
- Further systemd Type=notify/WatchdogSec polish — deferred (packaging saturating; not higher than release integrity)  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-40 COMPLETE** (W7-306 OPS-HOST-LOG-01; seed **W7-307 DONE**).  
2. **W7-308 DONE** — PLAN-41 inventory; opened **W7-310 (#1026)** QG-SIGN-02 implement.  
3. **W7-309 DONE** — seed advanced NEXT to QG-SIGN-02; opened COMPLETE **W7-311 (#1028)**.  
4. **W7-310 DONE** — sole QG-SIGN-02 shipped (opt-in crypto gate + docs/Living Spec).
5. **W7-311 DONE** — PLAN-41 COMPLETE; seeded PLAN-42 inventory **W7-312**.

## §3.C NEXT

**§3.C NEXT = W7-312 (#1031)** — PLAN-42 Inventory Controller HTTP health probes after PLAN-41.
