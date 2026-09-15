# PLAN-37 — Controller host env sample packaging (EnvironmentFile / controller.env.example)

**Date:** 2026-09-15 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-292); seed **W7-293 (#992) OPEN**; predecessor **PLAN-36 COMPLETE**  
**PLAN issue / queue:** [W7-292 / PLAN-37 #991](https://github.com/sesquicadaver/MTDirector/issues/991) **OPEN** (**§3.C NEXT**)  
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

## Out of scope (do not seed)

- Re-opening PLAN-36 OPS-HOST-BUNDLE / systemd/WinSW authoring  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Bundling WinSW **binary** (licensing / third-party; stay XML-only)

## Inventory evidence (seed baseline 2026-09-15 `main` @ PLAN-36 COMPLETE / `b866aa91`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `mfc-controller.service` | `EnvironmentFile=-/etc/mfc/controller.env` | No sibling `mfc-controller.env.example` under `packaging/` |
| `installation.md` | Instructs “create /etc/mfc/controller.env” | No copyable template path |
| `package-controller.sh` | Bundles systemd + WinSW (OPS-HOST-BUNDLE-01) | Does **not** copy an env example into `$OUT_DIR/controller/` |
| WinSW xml | Comments show sample `<env>` placeholders | Linux EnvironmentFile path still lacks a file artifact |

## Ranked Controller host env-sample tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **OPS-HOST-ENV-01** | Author `packaging/systemd/mfc-controller.env.example` (documented `MFC__…` keys, no secrets) + docs/Living Spec; inventory may also require bundle into `$OUT_DIR/controller/` | EnvironmentFile + install sketch @ `b866aa91`; no example file | after inventory **W7-292**; seed **W7-293 (#992)** |

Inventory (**W7-292**) may refine ranking and open implement issues; seed **W7-293** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-36 CLOSED)

PLAN-36 sole ranked row (**OPS-HOST-BUNDLE-01**) is **DONE**. No further PLAN-36 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- WinSW binary redistribution — not §3 (operator-supplied)  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-36 COMPLETE** (W7-290 OPS-HOST-BUNDLE-01; seed **W7-291 DONE**).  
2. **W7-292 OPEN** — PLAN-37 inventory → open first env-sample implement + follow-up seeds.  
3. **W7-293 OPEN** — seed first PLAN-37 implement after inventory.  
4. Execute ranked OPS-HOST-ENV row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-292 (#991)** — PLAN-37 Inventory Controller host env sample packaging after PLAN-36.
