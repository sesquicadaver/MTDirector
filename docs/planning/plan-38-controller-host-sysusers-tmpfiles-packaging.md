# PLAN-38 — Controller host sysusers/tmpfiles packaging (mfc user + host dirs)

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-296); seed **W7-297 (#1000) OPEN**; predecessor **PLAN-37 COMPLETE**  
**PLAN issue / queue:** [W7-296 / PLAN-38 #999](https://github.com/sesquicadaver/MTDirector/issues/999) **OPEN** (**§3.C NEXT**)  
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

## Out of scope (do not seed)

- Re-opening PLAN-37 OPS-HOST-ENV / systemd/WinSW authoring  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Bundling WinSW **binary** (licensing / third-party; stay XML-only)

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-37 COMPLETE / `6b05298f`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `mfc-controller.service` | `User=mfc` / `Group=mfc`; install sketch `install -d -o mfc -g mfc` | No sibling `sysusers.d` / `tmpfiles.d` under `packaging/systemd/` |
| `mfc-controller.env.example` | `MFC__Security__TrustedCa__ProfilesDirectory=/var/lib/mfc/trusted-ca` | Directory must exist; no tmpfiles fragment |
| `installation.md` | Manual `install -d` for `/opt/mfc/controller` + `/etc/mfc` | No declarative host-bootstrap templates |
| `package-controller.sh` | Bundles unit + WinSW + env.example | Does **not** copy sysusers/tmpfiles into `$OUT_DIR/controller/` |

## Ranked Controller host sysusers/tmpfiles tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **OPS-HOST-SYSUSERS-01** | Author `packaging/systemd/` sysusers + tmpfiles (mfc user/group + `/etc/mfc` + `/var/lib/mfc` (+ trusted-ca) + optionally `/opt/mfc/controller`) + docs/Living Spec; inventory may also require bundle into `$OUT_DIR/controller/` | Unit User/Group + path sketches @ `6b05298f`; no sysusers/tmpfiles files | after inventory **W7-296**; seed **W7-297 (#1000)** |

Inventory (**W7-296**) may refine ranking and open implement issues; seed **W7-297** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-37 CLOSED)

PLAN-37 sole ranked row (**OPS-HOST-ENV-01**) is **DONE**. No further PLAN-37 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- WinSW binary redistribution — not §3 (operator-supplied)  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-37 COMPLETE** (W7-294 OPS-HOST-ENV-01; seed **W7-295 DONE**).  
2. **W7-296 OPEN** — PLAN-38 inventory → open first sysusers/tmpfiles implement + follow-up seeds.  
3. **W7-297 OPEN** — seed first PLAN-38 implement after inventory.  
4. Execute ranked OPS-HOST-SYSUSERS row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-296 (#999)** — PLAN-38 Inventory Controller host sysusers/tmpfiles packaging after PLAN-37.
