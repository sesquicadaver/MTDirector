# PLAN-32 — Controller host-process packaging templates (systemd / Windows Service)

**Date:** 2026-09-15 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-268); seed **W7-269 (#944) OPEN**; predecessor **PLAN-31 COMPLETE**  
**PLAN issue / queue:** [W7-268 / PLAN-32 #943](https://github.com/sesquicadaver/MTDirector/issues/943) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-31 Desktop residual ListBox / Drift–Audit read-only a11y **COMPLETE**  
**Normative files:** [`docs/howto/build-and-run.md`](../howto/build-and-run.md), [`docs/operations/installation.md`](../operations/installation.md), [`docs/release/packaging.md`](packaging.md via `docs/release/`), [`scripts/release/`](../../scripts/release/)  
**Normative prior locks:** W7-22 zip/tar installer substitute; W7-23 SHA256SUMS; W7-24 SBOM; QG-SIGN-01 — **do not regress / do not invent MSI**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **ops/packaging** continuous-queue gap after Desktop operator a11y saturation (PLAN-16…31): release packages are framework-dependent zip/tar, but the repo has **no** managed host-process templates for Controller (systemd unit / Windows Service). Operators must invent their own units. MSI/AppImage remain intentional MVP residuals — **do not re-open**. Nested ListBox item-template a11y stays deferred vanity.

## Principles

1. Controller packages must be installable as a managed host process with documented templates (Linux systemd + Windows Service equivalent).  
2. Templates must match framework-dependent publish layout from `scripts/release/package-controller.sh`.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-31 DESK-A11Y-LIST/RO product rows — that tranche is **COMPLETE**.  
5. Do not invent native MSI/AppImage/setup.exe (W7-22 lock).

## Out of scope (do not seed)

- Re-opening PLAN-31 DESK-A11Y-LIST / DESK-A11Y-RO product rows  
- Nested ListBox tags without `ItemsSource` (item templates)  
- Native MSI / AppImage / `.dmg`  
- Changing default `--self-contained false` publish policy without inventory evidence  
- Ops / CRS / physical lab live runners as §3 stop-gates

## Inventory evidence (seed baseline 2026-09-15 `main` @ `9c945a5`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| HOWTO packaging gaps | §6.4 documents missing systemd / Windows Service templates | No repo templates |
| `docs/operations/installation.md` | Manual start of `Mfc.Controller` after extract | No unit/service install steps |
| `scripts/release/package-controller.sh` | Framework-dependent publish directory | No accompanying `.service` / WinSW / sc.exe template artifact |
| Desktop a11y (PLAN-16…31) | Buttons / fields / ListBox hosts / RO TextBoxes named | Saturated — do not invent nested-ListBox vanity |
| Inventory TreeView | Primary browse `TreeView` without Name | Adjacent residual (not this tranche) |
| W7-22…24 packaging locks | zip/tar + SHA256SUMS + SBOM Living Spec locked | Do not regress |

## Ranked Controller host-process packaging tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **OPS-HOST-SYSTEMD-01** | systemd unit template (+ docs) for framework-dependent Controller | HOWTO §6.4; no `.service` in repo | after inventory **W7-268**; seed **W7-269 (#944)** |
| 2 | **OPS-HOST-WINSVC-01** | Windows Service host template (+ docs) for Controller | HOWTO §6.4; installation.md Windows path is manual exe | after SYSTEMD (inventory may refine / split)

Inventory (**W7-268**) may refine ranking, add Living Spec locks for template paths, and open implement issues; seed **W7-269** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-31 CLOSED)

PLAN-31 ranks 1…2 (**DESK-A11Y-LIST-01**, **DESK-A11Y-RO-01**) are **DONE**. No further PLAN-31 product rows.

## Adjacent residuals (not seeded here)

- Inventory `TreeView` `AutomationProperties.Name` (primary browse host) — deferred unless a later PLAN picks a11y again  
- Nested ListBox item-template hosts — deferred vanity  
- Self-contained / single-file publish default — separate packaging policy decision  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-31 COMPLETE** (W7-266 DESK-A11Y-RO-01; seed **W7-267 DONE**).  
2. **W7-268 OPEN** — PLAN-32 inventory → open first host-process implement + follow-up seeds.  
3. **W7-269 OPEN** — seed first PLAN-32 implement after inventory.  
4. Execute ranked OPS-HOST-SYSTEMD / OPS-HOST-WINSVC rows atomically.

## §3.C NEXT

**§3.C NEXT = W7-268 (#943)** — PLAN-32 Inventory Controller host-process packaging templates (systemd / Windows Service) after PLAN-31.
