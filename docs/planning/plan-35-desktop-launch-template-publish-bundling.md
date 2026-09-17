# PLAN-35 — Desktop launch-template publish bundling (package-desktop → OUT_DIR/desktop)

**Date:** 2026-09-15 (inventory **DONE** @ `d461b82`)  
**Status:** **PLAN-35 COMPLETE** — Inventory **DONE** (W7-284); seed **W7-285 (#976) DONE**; implement **W7-286 (#978) DONE**; COMPLETE seed **W7-287 (#979) DONE**; successor **PLAN-36** inventory **W7-288 (#983) DONE**; seed **W7-289 (#984) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-284 / PLAN-35 #975](https://github.com/sesquicadaver/MTDirector/issues/975) **DONE**  
**Predecessor:** PLAN-34 Desktop operator launch packaging templates **COMPLETE** (DESK-HOST-LINUX-01 + DESK-HOST-WIN-01)  
**Normative files:** [`package-desktop.sh`](../../scripts/release/package-desktop.sh), [`mfc-desktop.desktop`](../../packaging/linux/mfc-desktop.desktop), [`mfc-desktop-start-menu.ps1`](../../packaging/windows/mfc-desktop-start-menu.ps1), [`packaging.md`](../release/packaging.md)  
**Normative prior locks:** PLAN-34 DESK-HOST-LINUX/WIN templates; PLAN-32 Controller host packaging; W7-22 MSI/AppImage — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **product** continuous-queue packaging gap after PLAN-34 shipped in-repo Desktop launch templates: `package-desktop.sh` still publishes only the framework-dependent Avalonia tree to `$OUT_DIR/desktop/` and archives it — operators who unzip a release artifact do **not** receive `.desktop` / Start Menu sketch unless they also have a git checkout of `packaging/`.

## Principles

1. Release zip/tar for Desktop should carry the same launch affordances documented under `packaging/linux/` and `packaging/windows/`.  
2. Bundling must preserve framework-dependent layout (`Mfc.Desktop` / `Mfc.Desktop.exe`) and must not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-34 DESK-HOST product rows — that tranche is **COMPLETE**.  
5. Inventory **confirmed** sole rank **DESK-HOST-BUNDLE-01** (no second vanity rank; MSI/AppImage stay locked).

## Out of scope (do not seed)

- Re-opening PLAN-34 DESK-HOST-LINUX/WIN template authoring  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Controller systemd/WinSW publish bundling (successor PLAN-36 candidate)

## Inventory evidence (W7-284 @ `main` `d461b82`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `package-desktop.sh` | `DEST="$OUT_DIR/desktop"`; publish + zip/tar `desktop/` | Does **not** copy `packaging/linux/mfc-desktop.desktop` or `packaging/windows/mfc-desktop-start-menu.ps1` into `$DEST` |
| PLAN-34 templates | Shipped under `packaging/linux/` + `packaging/windows/` | Available only with git checkout |
| Dry-run path | Writes stub `Mfc.Desktop` + zip | Same missing launch-template copy |
| `package-controller.sh` | Publishes Controller only | Adjacent residual: does **not** copy systemd/WinSW into `$OUT_DIR/controller/` (not PLAN-35; PLAN-36 candidate) |

## Ranked Desktop launch-template publish-bundling tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-HOST-BUNDLE-01** | `package-desktop.sh` copies Linux `.desktop` + Windows Start Menu sketch into `$OUT_DIR/desktop/` (dry-run + real publish) + docs/Living Spec | PLAN-34 artifacts; package script omits copy @ `d461b82` | implement **W7-286 (#978)** implement **W7-286 (#978) DONE**; after seed **W7-285 (#976)** |

Inventory (**W7-284 DONE**) confirmed sole rank (BUNDLE-01 kept; no MSI/AppImage vanity rank). Seed **W7-285** advances NEXT to DESK-HOST-BUNDLE-01 implement; COMPLETE seed **W7-287** opens after BUNDLE-01.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-34 CLOSED)

PLAN-34 ranks 1…2 (**DESK-HOST-LINUX-01**, **DESK-HOST-WIN-01**) are **DONE**. No further PLAN-34 product rows.

## Adjacent residuals (seeded as PLAN-35 COMPLETE / PLAN-36)

- Controller host-template publish bundling (`package-controller.sh` → `OUT_DIR/controller/` for systemd + WinSW) — **PLAN-36** [`plan-36-controller-host-template-publish-bundling.md`](plan-36-controller-host-template-publish-bundling.md)

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-34 COMPLETE** (W7-282 DESK-HOST-WIN-01; seed **W7-283 DONE**).  
2. **W7-284 DONE** — PLAN-35 inventory; opened **W7-286 (#978)** BUNDLE implement + **W7-287 (#979)** COMPLETE seed.  
3. **W7-285 DONE** — seed first PLAN-35 implement → DESK-HOST-BUNDLE-01.  
4. Execute sole DESK-HOST-BUNDLE-01 row atomically; then W7-287 → PLAN-36.

## §3.C NEXT

**§3.C NEXT = W7-302 (#1010)** — PLAN-36 Inventory Controller host-template publish bundling after PLAN-35.
