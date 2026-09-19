# PLAN-40 — Controller host journald/syslog identity (SyslogIdentifier)

**Date:** 2026-09-17 (inventory **DONE** @ `30bee1c0`; implement **DONE**; **COMPLETE**)  
**Status:** **PLAN-40 COMPLETE** — Inventory **DONE** (W7-304); seed **W7-305 (#1016) DONE**; implement **W7-306 (#1018) DONE**; COMPLETE seed **W7-307 (#1020) DONE**; successor **PLAN-41** inventory **W7-308 (#1023) DONE**; seed **W7-309 (#1024) DONE**; implement **W7-310 (#1026) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-304 / PLAN-40 #1015](https://github.com/sesquicadaver/MTDirector/issues/1015) **DONE**  
**Predecessor:** PLAN-39 Controller host operator doc packaging **COMPLETE** (OPS-HOST-DOC-01)  
**Normative files:** [`mfc-controller.service`](../../packaging/systemd/mfc-controller.service), [`installation.md`](../operations/installation.md), [`packaging.md`](../release/packaging.md)  
**Normative prior locks:** PLAN-32…39 host templates + BUNDLE + ENV + SYSUSERS + DOC; W7-22 MSI/AppImage — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **Controller host observability** continuous-queue gap after PLAN-39 shipped operator doc packaging: `mfc-controller.service` still has **no** `SyslogIdentifier=` / `StandardOutput=journal` (or equivalent), so operators cannot reliably filter `journalctl` for the Controller host process despite a complete systemd unit + Documentation= README.

## Principles

1. Host-process packaging should include **journald/syslog identity** so operators can diagnose the running Controller (`journalctl -t mfc-controller` / unit logs).  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-39 OPS-HOST-DOC product rows — that tranche is **COMPLETE**.  
5. Inventory **confirmed** sole rank **OPS-HOST-LOG-01** (author `SyslogIdentifier=mfc-controller` + `StandardOutput=journal` + `StandardError=journal` + docs/Living Spec; bundled unit copy updates via existing `package-controller.sh`; no MSI/AppImage vanity rank).  
6. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
7. Prefer observability over another publish-tree copy vanity row (packaging artifact-shipping wave PLAN-32…39 is saturating).

## Out of scope (do not seed)

- Re-opening PLAN-39 OPS-HOST-DOC / SYSUSERS / ENV authoring  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Bundling WinSW **binary** (licensing / third-party; stay XML-only)  
- Full centralized logging/SIEM (out of MVP scope)

## Inventory evidence (W7-304 @ `main` `30bee1c0`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `mfc-controller.service` | Unit + User/Group + Documentation= + Restart | **No** `SyslogIdentifier=` / `StandardOutput=journal` / `StandardError=journal` |
| Docs / HOWTO | Install + enable unit | No `journalctl -t mfc-controller` identity guidance |
| Glob / rg | `SyslogIdentifier` absent under `packaging/` @ `30bee1c0` | Confirmed |
| `package-controller.sh` | Copies unit into `$OUT_DIR/controller/` | Bundled copy will inherit LOG-01 once unit is authored |

## Ranked Controller host journald/syslog tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **OPS-HOST-LOG-01** | Author systemd journald/syslog identity (`SyslogIdentifier=mfc-controller` + `StandardOutput=journal` + `StandardError=journal`) + docs/Living Spec; bundled unit updates via existing `package-controller.sh` | Unit missing identity @ `30bee1c0` | implement **W7-306 (#1018) DONE** after seed **W7-305 (#1016) DONE** |

Inventory (**W7-304 DONE**) confirmed sole rank. Seed **W7-305 DONE** advanced NEXT to OPS-HOST-LOG-01 implement; COMPLETE seed **W7-307** opens after LOG-01.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-39 CLOSED)

PLAN-39 sole ranked row (**OPS-HOST-DOC-01**) is **DONE**. No further PLAN-39 product rows.

## Adjacent residuals (seeded as PLAN-40 COMPLETE / PLAN-41)

- Release signing crypto (GPG/Sigstore beyond QG-SIGN-01) — **PLAN-41** [`plan-41-release-signing-crypto-gpg-sigstore.md`](plan-41-release-signing-crypto-gpg-sigstore.md)

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- WinSW binary redistribution — not §3 (operator-supplied)  
- Optional GPG/Sigstore CI crypto beyond QG-SIGN-01 cleartext — quality residual (not this tranche)  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates  
- After PLAN-40 COMPLETE, prefer **non-packaging** product gaps (Controller/Desktop/ops docs) — packaging host-unit polish saturating once LOG-01 ships

## §3.C ordering

1. **PLAN-39 COMPLETE** (W7-302 OPS-HOST-DOC-01; seed **W7-303 DONE**).  
2. **W7-304 DONE** — PLAN-40 inventory; opened **W7-306 (#1018)** LOG implement.  
3. **W7-305 DONE** — seed first PLAN-40 implement → OPS-HOST-LOG-01; opened COMPLETE **W7-307 (#1020)**.  
4. **W7-306 DONE** — sole OPS-HOST-LOG-01 shipped (SyslogIdentifier + journal stdout/stderr + docs; bundled unit via package-controller).
5. **W7-307 DONE** — PLAN-40 COMPLETE; seeded PLAN-41 inventory **W7-308**.

## §3.C NEXT

**§3.C NEXT = W7-351 (#1107)** — QG-SIGN-02 opt-in cryptographic signing gate.
