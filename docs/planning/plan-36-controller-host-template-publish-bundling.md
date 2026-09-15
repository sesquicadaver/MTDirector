# PLAN-36 — Controller host-template publish bundling (package-controller → OUT_DIR/controller)

**Date:** 2026-09-15 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-288); seed **W7-289 (#984) OPEN**; predecessor **PLAN-35 COMPLETE**  
**PLAN issue / queue:** [W7-288 / PLAN-36 #983](https://github.com/sesquicadaver/MTDirector/issues/983) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-35 Desktop launch-template publish bundling **COMPLETE** (DESK-HOST-BUNDLE-01)  
**Normative files:** [`package-controller.sh`](../../scripts/release/package-controller.sh), [`mfc-controller.service`](../../packaging/systemd/mfc-controller.service), [`mfc-controller.winsw.xml`](../../packaging/windows/mfc-controller.winsw.xml), [`packaging.md`](../release/packaging.md)  
**Normative prior locks:** PLAN-32 OPS-HOST-SYSTEMD/WINSVC templates; PLAN-35 Desktop BUNDLE; W7-22 MSI/AppImage — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **product** continuous-queue packaging gap after PLAN-35 shipped Desktop launch-template publish bundling: `package-controller.sh` still publishes only the framework-dependent Controller tree to `$OUT_DIR/controller/` — operators who take a release Controller artifact do **not** receive systemd / WinSW templates unless they also have a git checkout of `packaging/`.

## Principles

1. Release Controller tree should carry the same host-process affordances documented under `packaging/systemd/` and `packaging/windows/`.  
2. Bundling must preserve framework-dependent layout (`Mfc.Controller` / `Mfc.Controller.exe`) and must not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-35 DESK-HOST-BUNDLE product rows — that tranche is **COMPLETE**.

## Out of scope (do not seed)

- Re-opening PLAN-35 DESK-HOST-BUNDLE / Desktop launch templates  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates

## Inventory evidence (seed baseline 2026-09-15 `main` @ PLAN-35 COMPLETE / `d84f8269`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `package-controller.sh` | `DEST="$OUT_DIR/controller"`; publish (no zip) | Does **not** copy `packaging/systemd/mfc-controller.service` or `packaging/windows/mfc-controller.winsw.xml` into `$DEST` |
| PLAN-32 templates | Shipped under `packaging/` | Available only with git checkout |
| Dry-run path | Writes stub `Mfc.Controller` | Same missing host-template copy |
| PLAN-35 Desktop BUNDLE | `package-desktop.sh` now copies launch templates | Controller parity gap remains |

## Ranked Controller host-template publish-bundling tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **OPS-HOST-BUNDLE-01** | `package-controller.sh` copies systemd unit + WinSW xml into `$OUT_DIR/controller/` (dry-run + real publish) + docs/Living Spec | PLAN-32 artifacts; package script omits copy @ `d84f8269` | after inventory **W7-288**; seed **W7-289 (#984)** |

Inventory (**W7-288**) may refine ranking and open implement issues; seed **W7-289** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-35 CLOSED)

PLAN-35 sole ranked row (**DESK-HOST-BUNDLE-01**) is **DONE**. No further PLAN-35 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-35 COMPLETE** (W7-286 DESK-HOST-BUNDLE-01; seed **W7-287 DONE**).  
2. **W7-288 OPEN** — PLAN-36 inventory → open first bundling implement + follow-up seeds.  
3. **W7-289 OPEN** — seed first PLAN-36 implement after inventory.  
4. Execute ranked OPS-HOST-BUNDLE row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-288 (#983)** — PLAN-36 Inventory Controller host-template publish bundling after PLAN-35.
