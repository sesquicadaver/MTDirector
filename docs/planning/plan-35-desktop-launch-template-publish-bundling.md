# PLAN-35 — Desktop launch-template publish bundling (package-desktop → OUT_DIR/desktop)

**Date:** 2026-09-15 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-284); seed **W7-285 (#976) OPEN**; predecessor **PLAN-34 COMPLETE**  
**PLAN issue / queue:** [W7-284 / PLAN-35 #975](https://github.com/sesquicadaver/MTDirector/issues/975) **OPEN** (**§3.C NEXT**)  
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

## Out of scope (do not seed)

- Re-opening PLAN-34 DESK-HOST-LINUX/WIN template authoring  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates

## Inventory evidence (seed baseline 2026-09-15 `main` @ PLAN-34 COMPLETE)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `package-desktop.sh` | `DEST="$OUT_DIR/desktop"`; publish + zip/tar `desktop/` | Does **not** copy `packaging/linux/mfc-desktop.desktop` or `packaging/windows/mfc-desktop-start-menu.ps1` into `$DEST` |
| PLAN-34 templates | Shipped under `packaging/` | Available only with git checkout |
| Dry-run path | Writes stub `Mfc.Desktop` + zip | Same missing launch-template copy |

## Ranked Desktop launch-template publish-bundling tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-HOST-BUNDLE-01** | `package-desktop.sh` copies Linux `.desktop` + Windows Start Menu sketch into `$OUT_DIR/desktop/` (dry-run + real publish) + docs/Living Spec | PLAN-34 artifacts; package script omits copy | after inventory **W7-284**; seed **W7-285 (#976)** |

Inventory (**W7-284**) may refine ranking and open implement issues; seed **W7-285** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-34 CLOSED)

PLAN-34 ranks 1…2 (**DESK-HOST-LINUX-01**, **DESK-HOST-WIN-01**) are **DONE**. No further PLAN-34 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-34 COMPLETE** (W7-282 DESK-HOST-WIN-01; seed **W7-283 DONE**).  
2. **W7-284 OPEN** — PLAN-35 inventory → open first bundling implement + follow-up seeds.  
3. **W7-285 OPEN** — seed first PLAN-35 implement after inventory.  
4. Execute ranked DESK-HOST-BUNDLE row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-284 (#975)** — PLAN-35 Inventory Desktop launch-template publish bundling after PLAN-34.
