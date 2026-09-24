# PLAN-38 — Controller host sysusers/tmpfiles packaging (mfc user + host dirs)

**Date:** 2026-09-17 (inventory **DONE** @ `7da14df5`)  
**Status:** **PLAN-38 COMPLETE** — Inventory **DONE** (W7-296); seed **W7-297 (#1000) DONE**; implement **W7-298 (#1002) DONE**; COMPLETE seed **W7-299 (#1004) DONE**; successor **PLAN-39** inventory **W7-300 (#1007) DONE**; seed **W7-301 (#1008) DONE**; implement **W7-302 (#1010) DONE**; COMPLETE seed **W7-303 (#1012) DONE**; successor **PLAN-40** inventory **W7-304 (#1015) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-296 / PLAN-38 #999](https://github.com/sesquicadaver/MTDirector/issues/999) **DONE**  
**Predecessor:** PLAN-37 Controller host env sample packaging **COMPLETE** (OPS-HOST-ENV-01)  
**Normative files:** [`mfc-controller.service`](../../packaging/systemd/mfc-controller.service), [`mfc-controller.env.example`](../../packaging/systemd/mfc-controller.env.example), [`installation.md`](../operations/installation.md), [`package-controller.sh`](../../scripts/release/package-controller.sh), [`packaging.md`](../release/packaging.md)  
**Normative prior locks:** PLAN-32/36/37 host templates + BUNDLE + ENV; W7-22 MSI/AppImage — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **product** continuous-queue packaging gap after PLAN-37 shipped Controller host env sample packaging: the systemd unit runs as `User=mfc` / `Group=mfc` and install sketches hand-create `/opt/mfc/controller`, `/etc/mfc`, and `/var/lib/mfc/trusted-ca`, but the repo ships **no** `sysusers.d` / `tmpfiles.d` templates under `packaging/` and `package-controller.sh` does not place any in `$OUT_DIR/controller/`.

## Principles

1. Host-process packaging should include declarative **account + directory** bootstrap matching the unit and env sample paths.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-37 OPS-HOST-ENV product rows — that tranche is **COMPLETE**.  
5. Inventory **confirmed** sole rank **OPS-HOST-SYSUSERS-01** (author sysusers + tmpfiles + docs + bundle into `$OUT_DIR/controller/`; no second vanity rank; MSI/AppImage stay locked).

## Out of scope (do not seed)

- Re-opening PLAN-37 OPS-HOST-ENV / systemd/WinSW authoring  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Bundling WinSW **binary** (licensing / third-party; stay XML-only)

## Inventory evidence (W7-296 @ `main` `7da14df5`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `mfc-controller.service` | `User=mfc` / `Group=mfc`; install sketch `install -d -o mfc -g mfc` | No sibling `sysusers.d` / `tmpfiles.d` under `packaging/systemd/` |
| `mfc-controller.env.example` | `MFC__Security__TrustedCa__ProfilesDirectory=/var/lib/mfc/trusted-ca` | Directory must exist; no tmpfiles fragment |
| `installation.md` | Manual `install -d` for `/opt/mfc/controller` + `/etc/mfc` | No declarative host-bootstrap templates |
| `package-controller.sh` | Bundles unit + WinSW + env.example | Does **not** copy sysusers/tmpfiles into `$OUT_DIR/controller/` |
| Glob check | `packaging/systemd/mfc-controller.sysusers` / `.tmpfiles` **absent** @ `7da14df5` | Confirmed |

## Ranked Controller host sysusers/tmpfiles tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **OPS-HOST-SYSUSERS-01** | Author `packaging/systemd/mfc-controller.sysusers` + `mfc-controller.tmpfiles` (mfc user/group + `/etc/mfc` + `/var/lib/mfc` (+ trusted-ca) + `/opt/mfc/controller`) + docs/Living Spec **and** bundle into `$OUT_DIR/controller/` via `package-controller.sh` (OPS-HOST-BUNDLE-01 pattern) | Unit User/Group + path sketches @ `7da14df5`; no sysusers/tmpfiles files; package script omits copy | implement **W7-298 (#1002) DONE** after seed **W7-297 (#1000)** |

Inventory (**W7-296 DONE**) confirmed sole rank (SYSUSERS-01 kept with required publish bundling; no MSI/AppImage vanity rank; no separate TMPFILES-only split). Seed **W7-297** advances NEXT to OPS-HOST-SYSUSERS-01 implement; COMPLETE seed opens after SYSUSERS-01.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-37 CLOSED)

PLAN-37 sole ranked row (**OPS-HOST-ENV-01**) is **DONE**. No further PLAN-37 product rows.

## Adjacent residuals (seeded as PLAN-38 COMPLETE / PLAN-39)

- Controller host operator doc packaging (`Documentation=/usr/share/doc/mfc`) — **PLAN-39** [`plan-39-controller-host-operator-doc-packaging.md`](plan-39-controller-host-operator-doc-packaging.md)

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- WinSW binary redistribution — not §3 (operator-supplied)  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates  

## §3.C ordering

1. **PLAN-37 COMPLETE** (W7-294 OPS-HOST-ENV-01; seed **W7-295 DONE**).  
2. **W7-296 DONE** — PLAN-38 inventory; opened **W7-298 (#1002)** SYSUSERS implement.  
3. **W7-297 DONE** — seed first PLAN-38 implement → OPS-HOST-SYSUSERS-01; opened COMPLETE **W7-299 (#1004)**.  
4. **W7-298 DONE** — sole OPS-HOST-SYSUSERS-01 shipped (templates + docs + bundle).
5. **W7-299 DONE** — PLAN-38 COMPLETE; seeded PLAN-39 inventory **W7-300**.

## §3.C NEXT

**§3.C NEXT = W7-414 (#1228)** — PLAN-40 Inventory Controller host journald/syslog identity after PLAN-39.