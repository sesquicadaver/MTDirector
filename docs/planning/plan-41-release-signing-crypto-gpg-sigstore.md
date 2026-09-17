# PLAN-41 — Release signing crypto (GPG/Sigstore beyond QG-SIGN-01)

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-308); seed **W7-309 (#1024) OPEN**; predecessor **PLAN-40 COMPLETE**  
**PLAN issue / queue:** [W7-308 / PLAN-41 #1023](https://github.com/sesquicadaver/MTDirector/issues/1023) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-40 Controller host journald/syslog identity **COMPLETE** (OPS-HOST-LOG-01)  
**Normative files:** [`RELEASE_SIGNING.md`](../release/RELEASE_SIGNING.md), [`signing-gate.md`](../development/signing-gate.md), [`generate-sbom-and-checksums.sh`](../../scripts/release/generate-sbom-and-checksums.sh)  
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

## Out of scope (do not seed)

- Re-opening PLAN-40 OPS-HOST-LOG / systemd/WinSW packaging authoring  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Requiring org secrets in every PR CI (inventory may stage optional/opt-in crypto first)

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-40 COMPLETE / `2bf1bca5`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `generate-sbom-and-checksums.sh` | Always writes cleartext `SHA256SUMS`; GPG only if `MFC_RELEASE_GPG_KEY_ID` + `gpg` present | No default CI crypto / Sigstore path |
| `RELEASE_SIGNING.md` | Documents CI crypto as **future** | Not Living-Spec-locked as production gate |
| `signing-gate.md` / QG-SIGN-01 | Explicitly keeps crypto unchecked residual | Confirmed intentional residual |
| Glob / rg | Sigstore/cosign absent as required CI gate @ `2bf1bca5` | Confirmed |

## Ranked release-signing crypto tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **QG-SIGN-02** | Author CI/release cryptographic signing gate (GPG and/or Sigstore) + docs/Living Spec beyond QG-SIGN-01 cleartext | Future-gate docs + optional-only GPG @ `2bf1bca5` | after inventory **W7-308**; seed **W7-309 (#1024)** |

Inventory (**W7-308**) may refine ranking and open implement issues; seed **W7-309** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-40 CLOSED)

PLAN-40 sole ranked row (**OPS-HOST-LOG-01**) is **DONE**. No further PLAN-40 product rows. Packaging host-unit polish is saturated (WinSW already has rolled logs).

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- WinSW binary redistribution — not §3 (operator-supplied)  
- Further systemd Type=notify/WatchdogSec polish — deferred (packaging saturating; not higher than release integrity)  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-40 COMPLETE** (W7-306 OPS-HOST-LOG-01; seed **W7-307 DONE**).  
2. **W7-308 OPEN** — PLAN-41 inventory → open first signing-crypto implement + follow-up seeds.  
3. **W7-309 OPEN** — seed first PLAN-41 implement after inventory.  
4. Execute ranked QG-SIGN row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-308 (#1023)** — PLAN-41 Inventory release signing crypto after PLAN-40.
