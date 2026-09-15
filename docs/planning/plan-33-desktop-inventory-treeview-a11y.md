# PLAN-33 — Desktop Inventory TreeView / residual TabControl a11y

**Date:** 2026-09-15 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-274); seed **W7-275 (#956) OPEN**; predecessor **PLAN-32 COMPLETE**  
**PLAN issue / queue:** [W7-274 / PLAN-33 #955](https://github.com/sesquicadaver/MTDirector/issues/955) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-32 Controller host-process packaging templates **COMPLETE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative prior locks:** PLAN-16…31 Desktop AutomationProperties; PLAN-32 OPS-HOST-SYSTEMD/WINSVC — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **product** continuous-queue a11y gap after PLAN-32 closed Controller host-process packaging: the Inventory **TreeView** primary browse host still lacks control-level `AutomationProperties.Name` (explicit PLAN-32 adjacent residual). Nested ListBox item-template a11y stays deferred vanity. MSI/AppImage and self-contained publish default stay locked — **do not re-open**.

## Principles

1. Primary operator browse hosts (Inventory TreeView) must expose stable `AutomationProperties.Name` for AT / UI Automation.  
2. Inventory may rank residual unnamed `TabControl` panel hosts as a second atomic row when evidence supports it.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-32 OPS-HOST product rows — that tranche is **COMPLETE**.  
5. Do not invent nested ListBox item-template Names (vanity).

## Out of scope (do not seed)

- Re-opening PLAN-32 OPS-HOST-SYSTEMD / OPS-HOST-WINSVC product rows  
- Nested ListBox tags without `ItemsSource` (item templates)  
- Native MSI / AppImage / changing `--self-contained false` without separate packaging inventory  
- Ops / CRS / physical lab live runners as §3 stop-gates

## Inventory evidence (seed baseline 2026-09-15 `main` @ `7a24b96`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Inventory TreeView | `ItemsSource="{Binding Inventory.Roots}"` + `SelectedItem` TwoWay @ `MainWindow.axaml` ~86 | **No** `AutomationProperties.Name` on the TreeView control |
| PLAN-32 adjacent residual | Explicitly deferred Inventory TreeView Name | Now the highest-value product a11y gap |
| PLAN-16…31 a11y | Buttons / fields / ListBox hosts / RO TextBoxes named | Saturated — do not invent nested-ListBox vanity |
| TabControl panel hosts | Several `<TabControl>` opens without control-level Name | Optional rank-2 (inventory may refine / drop) |
| PLAN-32 packaging | systemd + WinSW templates shipped | COMPLETE — do not re-open MSI (W7-22) |

## Ranked Desktop Inventory TreeView a11y tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-TREE-01** | Inventory TreeView control-level `AutomationProperties.Name` | `MainWindow.axaml` ~86 TreeView without Name; PLAN-32 adjacent residual | after inventory **W7-274**; seed **W7-275 (#956)** |
| 2 | **DESK-A11Y-TAB-01** *(optional)* | Residual unnamed `TabControl` panel hosts | Seed baseline TabControl opens without Name | inventory may confirm / drop |

Inventory (**W7-274**) may refine ranking, drop TAB-01 if vanity, and open implement issues; seed **W7-275** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-32 CLOSED)

PLAN-32 ranks 1…2 (**OPS-HOST-SYSTEMD-01**, **OPS-HOST-WINSVC-01**) are **DONE**. No further PLAN-32 product rows.

## Adjacent residuals (not seeded here)

- Nested ListBox item-template hosts — deferred vanity  
- Self-contained / single-file publish default — separate packaging policy decision  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-32 COMPLETE** (W7-272 OPS-HOST-WINSVC-01; seed **W7-273 DONE**).  
2. **W7-274 OPEN** — PLAN-33 inventory → open first TreeView implement + follow-up seeds.  
3. **W7-275 OPEN** — seed first PLAN-33 implement after inventory.  
4. Execute ranked DESK-A11Y-TREE / optional DESK-A11Y-TAB rows atomically.

## §3.C NEXT

**§3.C NEXT = W7-274 (#955)** — PLAN-33 Inventory Desktop Inventory TreeView / residual TabControl a11y after PLAN-32.
