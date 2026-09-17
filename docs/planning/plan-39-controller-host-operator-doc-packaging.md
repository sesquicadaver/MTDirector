# PLAN-39 — Controller host operator doc packaging (Documentation=/usr/share/doc/mfc)

**Date:** 2026-09-17 (inventory **DONE** @ `7348ba5e`)  
**Status:** Inventory **DONE** (W7-300); seed **W7-301 (#1008) OPEN** (**§3.C NEXT**); implement **W7-302 (#1010) OPEN**; predecessor **PLAN-38 COMPLETE**  
**PLAN issue / queue:** [W7-300 / PLAN-39 #1007](https://github.com/sesquicadaver/MTDirector/issues/1007) **DONE**  
**Predecessor:** PLAN-38 Controller host sysusers/tmpfiles packaging **COMPLETE** (OPS-HOST-SYSUSERS-01)  
**Normative files:** [`mfc-controller.service`](../../packaging/systemd/mfc-controller.service), [`installation.md`](../operations/installation.md), [`package-controller.sh`](../../scripts/release/package-controller.sh), [`packaging.md`](../release/packaging.md)  
**Normative prior locks:** PLAN-32/36/37/38 host templates + BUNDLE + ENV + SYSUSERS; W7-22 MSI/AppImage — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **product** continuous-queue packaging gap after PLAN-38 shipped Controller host sysusers/tmpfiles packaging: the systemd unit declares `Documentation=file:///usr/share/doc/mfc/README.md`, but the repo ships **no** packaging doc artifact under `packaging/` and `package-controller.sh` does not place any operator README/INSTALL into `$OUT_DIR/controller/`.

## Principles

1. Host-process packaging should include an operator-facing **doc** artifact that matches the unit `Documentation=` path (or a clear install sketch to `/usr/share/doc/mfc/`).  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-38 OPS-HOST-SYSUSERS product rows — that tranche is **COMPLETE**.  
5. Inventory **confirmed** sole rank **OPS-HOST-DOC-01** (author packaging doc + docs + bundle into `$OUT_DIR/controller/`; no second vanity rank; MSI/AppImage stay locked).  
6. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).

## Out of scope (do not seed)

- Re-opening PLAN-38 OPS-HOST-SYSUSERS / ENV / systemd/WinSW authoring  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Bundling WinSW **binary** (licensing / third-party; stay XML-only)

## Inventory evidence (W7-300 @ `main` `7348ba5e`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `mfc-controller.service` | `Documentation=file:///usr/share/doc/mfc/README.md` | No sibling packaging doc under `packaging/` for that path |
| `package-controller.sh` | Bundles unit + WinSW + env.example + sysusers/tmpfiles | Does **not** copy an operator README/INSTALL into `$OUT_DIR/controller/` |
| `installation.md` / HOWTO | Document install order in-repo | Publish tree lacks in-artifact install/doc pointer |
| Glob check | `packaging/doc/mfc/README.md` **absent** @ `7348ba5e` | Confirmed |

## Ranked Controller host operator-doc tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **OPS-HOST-DOC-01** | Author `packaging/doc/mfc/README.md` (operator README/INSTALL matching `Documentation=`) + docs/Living Spec **and** bundle into `$OUT_DIR/controller/` via `package-controller.sh` (with install sketch to `/usr/share/doc/mfc/README.md`) | Unit Documentation= + missing packaging doc @ `7348ba5e`; package script omits copy | implement **W7-302 (#1010)** after seed **W7-301 (#1008)** |

Inventory (**W7-300 DONE**) confirmed sole rank (DOC-01 kept with required publish bundling; no MSI/AppImage vanity rank). Seed **W7-301** advances NEXT to OPS-HOST-DOC-01 implement; COMPLETE seed opens after DOC-01.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-38 CLOSED)

PLAN-38 sole ranked row (**OPS-HOST-SYSUSERS-01**) is **DONE**. No further PLAN-38 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- WinSW binary redistribution — not §3 (operator-supplied)  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates  
- After PLAN-39 COMPLETE, prefer **non-packaging** product gaps (audit residuals / Desktop / Controller) — packaging tranche saturating

## §3.C ordering

1. **PLAN-38 COMPLETE** (W7-298 OPS-HOST-SYSUSERS-01; seed **W7-299 DONE**).  
2. **W7-300 DONE** — PLAN-39 inventory; opened **W7-302 (#1010)** DOC implement.  
3. **W7-301 OPEN** — seed first PLAN-39 implement → OPS-HOST-DOC-01.  
4. Execute ranked OPS-HOST-DOC-01 atomically (doc artifact + docs + bundle).

## §3.C NEXT

**§3.C NEXT = W7-301 (#1008)** — Seed first PLAN-39 atomic row → OPS-HOST-DOC-01 after inventory.
