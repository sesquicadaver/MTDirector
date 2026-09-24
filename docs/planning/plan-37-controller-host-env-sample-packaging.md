# PLAN-37 — Controller host env sample packaging (EnvironmentFile / controller.env.example)

**Date:** 2026-09-17 (inventory **DONE** @ `05212fce`)  
**Status:** **PLAN-37 COMPLETE** — Inventory **DONE** (W7-292); seed **W7-293 (#992) DONE**; implement **W7-294 (#994) DONE**; COMPLETE seed **W7-295 (#996) DONE**; successor **PLAN-38** inventory **W7-296 (#999) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-292 / PLAN-37 #991](https://github.com/sesquicadaver/MTDirector/issues/991) **DONE**  
**Predecessor:** PLAN-36 Controller host-template publish bundling **COMPLETE** (OPS-HOST-BUNDLE-01)  
**Normative files:** [`mfc-controller.service`](../../packaging/systemd/mfc-controller.service), [`installation.md`](../operations/installation.md), [`controller-configuration.md`](../operations/controller-configuration.md), [`package-controller.sh`](../../scripts/release/package-controller.sh), [`packaging.md`](../release/packaging.md)  
**Normative prior locks:** PLAN-32/36 host templates + BUNDLE; W7-22 MSI/AppImage — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **product** continuous-queue packaging gap after PLAN-36 shipped Controller host-template publish bundling: operators who install the systemd unit still must hand-author `/etc/mfc/controller.env` — the unit documents `EnvironmentFile=-/etc/mfc/controller.env` and installation says “create … controller.env”, but the repo ships **no** example env under `packaging/` and `package-controller.sh` does not place one in `$OUT_DIR/controller/`.

## Principles

1. Host-process packaging should include an operator-facing **example** env that matches documented `MFC__…` keys (no secrets).  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-36 OPS-HOST-BUNDLE product rows — that tranche is **COMPLETE**.  
5. Inventory **confirmed** sole rank **OPS-HOST-ENV-01** (author example + docs + bundle into `$OUT_DIR/controller/`; no second vanity rank; MSI/AppImage stay locked).

## Out of scope (do not seed)

- Re-opening PLAN-36 OPS-HOST-BUNDLE / systemd/WinSW authoring  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Bundling WinSW **binary** (licensing / third-party; stay XML-only)

## Inventory evidence (W7-292 @ `main` `05212fce`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `mfc-controller.service` | `EnvironmentFile=-/etc/mfc/controller.env` (line 29); install sketch says `sudoedit /etc/mfc/controller.env` | No sibling `mfc-controller.env.example` under `packaging/systemd/` |
| `installation.md` | Instructs “create /etc/mfc/controller.env” with `MFC__…` | No copyable template path |
| `controller-configuration.md` | Documents required `Mfc:` / `MFC__…` keys | No packaging artifact operators can copy |
| `package-controller.sh` | Bundles systemd + WinSW (OPS-HOST-BUNDLE-01) via `mfc_controller_bundle_host_templates` | Does **not** copy an env example into `$OUT_DIR/controller/` |
| WinSW xml | Comments show sample `<env>` placeholders | Linux EnvironmentFile path still lacks a file artifact |
| Glob check | `packaging/systemd/mfc-controller.env.example` **absent** @ `05212fce` | Confirmed |

## Ranked Controller host env-sample tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **OPS-HOST-ENV-01** | Author `packaging/systemd/mfc-controller.env.example` (documented `MFC__…` keys, no secrets) + docs/Living Spec **and** bundle into `$OUT_DIR/controller/` via `package-controller.sh` (OPS-HOST-BUNDLE-01 pattern) | EnvironmentFile + install sketch + missing file @ `05212fce`; package script omits env copy | implement **W7-294 (#994)** after seed **W7-293 (#992)** |

Inventory (**W7-292 DONE**) confirmed sole rank (ENV-01 kept with required publish bundling; no MSI/AppImage vanity rank). Seed **W7-293** advances NEXT to OPS-HOST-ENV-01 implement; COMPLETE seed opens after ENV-01.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-36 CLOSED)

PLAN-36 sole ranked row (**OPS-HOST-BUNDLE-01**) is **DONE**. No further PLAN-36 product rows.

## Adjacent residuals (seeded as PLAN-37 COMPLETE / PLAN-38)

- Controller host sysusers/tmpfiles packaging (`mfc` user + `/etc/mfc` + `/var/lib/mfc`) — **PLAN-38** [`plan-38-controller-host-sysusers-tmpfiles-packaging.md`](plan-38-controller-host-sysusers-tmpfiles-packaging.md)

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- WinSW binary redistribution — not §3 (operator-supplied)  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-36 COMPLETE** (W7-290 OPS-HOST-BUNDLE-01; seed **W7-291 DONE**).  
2. **W7-292 DONE** — PLAN-37 inventory; opened **W7-294 (#994)** ENV implement.  
3. **W7-293 DONE** — seed first PLAN-37 implement → OPS-HOST-ENV-01; opened COMPLETE **W7-295 (#996)**.  
4. **W7-294 DONE** — sole OPS-HOST-ENV-01 shipped.
5. **W7-295 DONE** — PLAN-37 COMPLETE; seeded PLAN-38 inventory **W7-296**.

## §3.C NEXT

**§3.C NEXT = W7-421 (#1239)** — PLAN-38 Inventory Controller host sysusers/tmpfiles packaging after PLAN-37.
