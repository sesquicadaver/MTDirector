# PLAN-33 — Desktop Inventory TreeView / residual TabControl a11y

**Date:** 2026-09-15 (**COMPLETE**)  
**Status:** **PLAN-33 COMPLETE** — Inventory **DONE** (W7-274); seed **W7-275 (#956) DONE**; implement **W7-276 DONE**; COMPLETE seed **W7-277 (#959) DONE**; successor **PLAN-34** inventory **W7-278 (#963) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-274 / PLAN-33 #955](https://github.com/sesquicadaver/MTDirector/issues/955) **DONE**  
**Predecessor:** PLAN-32 Controller host-process packaging templates **COMPLETE**  
**Successor:** [`plan-34-desktop-operator-launch-packaging.md`](plan-34-desktop-operator-launch-packaging.md) (Desktop operator launch packaging templates)  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative prior locks:** PLAN-16…31 Desktop AutomationProperties; PLAN-32 OPS-HOST-SYSTEMD/WINSVC — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **product** continuous-queue a11y gap after PLAN-32 closed Controller host-process packaging: the Inventory **TreeView** primary browse host now exposes control-level `AutomationProperties.Name=\"Inventory\"` (**DESK-A11Y-TREE-01 DONE**). Nested ListBox item-template a11y stays deferred vanity. Residual unnamed `TabControl` **containers** remain **dropped** from this tranche (TabItems already named). MSI/AppImage and self-contained publish default stay locked — **do not re-open**.

## Principles

1. Primary operator browse hosts (Inventory TreeView) must expose stable `AutomationProperties.Name` for AT / UI Automation.  
2. Inventory confirmed residual unnamed `TabControl` panel hosts exist, but TabItems already carry Names — container-level TabControl Name is deferred vanity (**DESK-A11Y-TAB-01 dropped**).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-32 OPS-HOST product rows — that tranche is **COMPLETE**.  
5. Do not invent nested ListBox item-template Names (vanity).

## Out of scope (do not seed)

- Re-opening PLAN-32 OPS-HOST-SYSTEMD / OPS-HOST-WINSVC product rows  
- Nested ListBox tags without `ItemsSource` (item templates)  
- DESK-A11Y-TAB-01 (dropped — TabItems already named)  
- Native MSI / AppImage / changing `--self-contained false` without separate packaging inventory  
- Ops / CRS / physical lab live runners as §3 stop-gates

## Inventory evidence (2026-09-15 `main` @ `50f1ae1`; implement closed gap)

| Surface | Behavior after TREE-01 | Gap / decision |
|---------|------------------------|----------------|
| Inventory TreeView | `ItemsSource="{Binding Inventory.Roots}"` + `AutomationProperties.Name="Inventory"` @ `MainWindow.axaml` | **DESK-A11Y-TREE-01 DONE** |
| PLAN-32 adjacent residual | Explicitly deferred Inventory TreeView Name | Closed by W7-276 |
| PLAN-16…31 a11y | Buttons / fields / ListBox hosts / RO TextBoxes named | Saturated — do not invent nested-ListBox vanity |
| TabControl panel hosts | **3** unnamed containers: Snapshots ~794, nested Snapshot Configuration/Observations ~898, Operations ~1666 | TabItems already have `AutomationProperties.Name` → **DESK-A11Y-TAB-01 dropped** (deferred vanity) |
| PLAN-32 packaging | systemd + WinSW templates shipped | COMPLETE — do not re-open MSI (W7-22) |

## Ranked Desktop Inventory TreeView a11y tranche (COMPLETE)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-TREE-01** | Inventory TreeView control-level `AutomationProperties.Name` (`Inventory`) | `MainWindow.axaml` TreeView named | seed **W7-275 (#956) DONE** → implement **W7-276 DONE**; COMPLETE seed **W7-277 DONE** |
| — | **DESK-A11Y-TAB-01** | *(dropped)* Residual unnamed TabControl containers | 3 TabControls lack container Name; TabItems already named | **not seeded** — deferred vanity outside PLAN-33 |

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-32 CLOSED)

PLAN-32 ranks 1…2 (**OPS-HOST-SYSTEMD-01**, **OPS-HOST-WINSVC-01**) are **DONE**. No further PLAN-32 product rows.

## Adjacent residuals (seeded as PLAN-33 COMPLETE / PLAN-34)

- Desktop operator launch packaging templates (`.desktop` / Windows shortcut) — **PLAN-34**  
- Unnamed TabControl containers — deferred vanity while TabItems remain named  
- Nested ListBox item-template hosts — deferred vanity  
- Self-contained / single-file publish default — separate packaging policy decision  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-32 COMPLETE** (W7-272 OPS-HOST-WINSVC-01; seed **W7-273 DONE**).  
2. **W7-274 DONE** — PLAN-33 inventory → opened TREE-01 implement **W7-276** + COMPLETE seed **W7-277**; dropped TAB-01.  
3. **W7-275 DONE** — seeded first PLAN-33 implement after inventory.  
4. **W7-276 DONE** — **DESK-A11Y-TREE-01** Inventory TreeView Name.  
5. **W7-277 DONE** — PLAN-33 COMPLETE; seeded PLAN-34 (**W7-278** / **W7-279**).

## §3.C NEXT

**§3.C NEXT = W7-318 (#1042)** — PLAN-34 Inventory Desktop operator launch packaging templates after PLAN-33.
