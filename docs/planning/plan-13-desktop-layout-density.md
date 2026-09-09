# PLAN-13 — Desktop layout density & readable data panes

**Date:** 2026-09-09  
**Status:** Inventory (ready to seed after PLAN-12 closes)  
**Predecessor:** GUI density analysis (lab session 2026-09-09); PLAN-12 Policies residual lifecycle  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml), [`App.axaml`](../../src/Mfc.Desktop/App.axaml)  
**Normative execution order (when seeded):** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Operator report: data panes often so small that content is unreadable. Analysis root causes (evidence in XAML):

1. Dozens of `MaxHeight="80"…"280"` on ListBox / detail frames  
2. Competing `*` rows (esp. Snapshots `Auto,Auto,*,Auto,*,Auto`) + `ClipToBounds`  
3. Drift / Audit multi-star grids with no `GridSplitter`  
4. Fixed shell columns `260 + 150` + chrome; `MinHeight="680"`  
5. Two inconsistent module patterns (scroll+tiny lists vs clipped star grids)

## Principles

1. **One primary data pane per view** grows with `*` (or fills a ScrollViewer); secondary panes are optional / collapsible / tabbed — not N equal `*` competitors.  
2. **No hard `MaxHeight` on primary lists** unless paired with a user-resizable splitter or an explicit “compact” mode. Cap only overflow chrome (toasts, banners).  
3. **Operator can reclaim space** via `GridSplitter` (horizontal and/or vertical) on multi-pane modules.  
4. **Shared layout tokens** in `App.axaml` (`Mfc.ListMinHeight`, section spacing) — avoid magic numbers scattered in `MainWindow.axaml`.  
5. **Living Spec locks layout contracts** (presence of splitter / absence of primary MaxHeight / MinHeight floors) — no pixel-perfect CI required in v1.  
6. Lab / CHR / `WriteEnabled` are **not** stop-gates for this tranche.

## Out of scope (do not seed)

- Visual redesign / new Fluent theme / dark mode  
- Extracting every module into separate UserControls (optional later; not required to fix density)  
- Changing gRPC / ViewModel business logic except bindings needed for collapse/expand  
- Replacing completed DESK-* Living Specs for RPC surface  
- Windows-only DPI quirks beyond Avalonia defaults  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Snapshots (star starvation) before Policies MaxHeight cascade |
| Risk | Layout-only PRs; keep bindings/names; Desktop build + focused Living Spec |
| Queue fit | Seed **after** PLAN-12 COMPLETE (do not preempt W7-123…W7-124) unless operator elevates |

## Ranked linear tranche

| Rank | ID | Gap | Evidence | Acceptance (testable) |
|------|----|-----|----------|------------------------|
| 1 | **DESK-LAYOUT-00** | No shared layout tokens / density contract doc | `App.axaml` styles; no `Mfc.*Height` resources for lists | Resources + short `docs/development/desktop-layout.md` + Living Spec that tokens exist and MainWindow references them for ≥1 primary list |
| 2 | **DESK-LAYOUT-01** | Snapshot tab: competing `*` + clipped detail | `MainWindow.axaml` ~685–853 `RowDefinitions="Auto,Auto,*,Auto,*,Auto"` | Single primary `*` for records (config **or** observation via sub-tab/toggle); detail pane below with splitter **or** dedicated bottom `*` ≥ `Mfc.DetailMinHeight`; no `MaxHeight` on primary ListBox; Windows Desktop build green |
| 3 | **DESK-LAYOUT-02** | Semantic Diff: entry list vs before/after starved | ~857–1051 `RowDefinitions="*,8,Auto"` + before/after `MaxHeight="220"` | Vertical `GridSplitter` between entries and detail; remove `MaxHeight` on before/after (scroll inside); entry list keeps `*` |
| 4 | **DESK-LAYOUT-03** | Drift: `Auto,*,Auto,*` + findings `MaxHeight="200"` | ~1737–1831 | Vertical splitters (events ↔ findings ↔ detail) **or** events `*` + findings/detail in tab/splitter pair; remove findings `MaxHeight`; `ClipToBounds` does not zero a pane at `MinHeight=680` |
| 5 | **DESK-LAYOUT-04** | Audit: `Auto,*,*` without splitter | ~1835–1875 | `GridSplitter` between event list and payload; both panes `MinHeight` from tokens |
| 6 | **DESK-LAYOUT-05** | Policies: scroll page of tiny MaxHeight lists (80–160) | ~1056–1466 | Primary surfaces (rules / findings / catalog) use token `MinHeight` + grow inside section panels **or** inner TabControl (Catalog \| Author \| Safety \| Diff \| Compile); delete primary-list `MaxHeight≤120` |
| 7 | **DESK-LAYOUT-06** | Node + RoutingAssurance MaxHeight cascade | ~429–680 | Same policy as LAYOUT-05 for VRRP findings / zone / hash / routing lists |
| 8 | **DESK-LAYOUT-07** | Operations (Onboarding/Deploy) MaxHeight lists | ~1471–1626 | Same; progress/findings readable ≥ token floor |
| 9 | **DESK-LAYOUT-08** | Shell chrome: fixed `260,12,150,12,*`; no column splitter | ~65–150 | Optional column `GridSplitter` after inventory; Modules column collapses to icons **or** width ≤120 when `Width<1280`; document behavior |
| 10 | **DESK-LAYOUT-09** | Inventory / Zones nested MaxHeight frames | ~341–426 | Zones lists use tokens; no `MaxHeight="240"` trap on primary zone list |
| 11 | **DESK-LAYOUT-10** | Regression lock + docs sync | Living Specs + `desktop-ui-backend-alignment.md` / `testing.md` | `DesktopLayoutDensityLivingSpecTests` Ac1–AcN; matrix row in `testing.md`; CHANGELOG; PLAN-13 COMPLETE |

## Suggested PR slicing (still linear for `/autopilot`)

| Wave | Ships | Notes |
|------|-------|-------|
| A | LAYOUT-00 + LAYOUT-01 | Tokens + Snapshot (highest pain) |
| B | LAYOUT-02 | Diff tab |
| C | LAYOUT-03 + LAYOUT-04 | Drift + Audit |
| D | LAYOUT-05 | Policies (largest XAML churn) |
| E | LAYOUT-06 + LAYOUT-07 + LAYOUT-09 | Node / Ops / Inventory leftovers |
| F | LAYOUT-08 + LAYOUT-10 | Shell + Living Spec complete + docs |

Each wave = one §3 atomic ID (or seed+implement pair per project habit). Prefer **one ID per wave** for reviewability.

## Dual track

Product §3 never waits on GNS3. Validate layout on Desktop against lab captures **ops-parallel** (`~/gns3-lab` day-2 filter density is a good manual probe, not a CI gate).

## Seed rule

Do **not** change **§3.C NEXT** until PLAN-12 (DESK-CATALOG-01) is COMPLETE, unless explicitly elevated. Then:

1. Seed PLAN-13 inventory issue (PLAN-13)  
2. Seed **DESK-LAYOUT-00** as NEXT  
3. Continue rank 1→10 without skipping  

## Immediate operator workaround (until seeded)

- Maximize window above 900×680  
- Snapshots: expect config/obs lists to stay short until LAYOUT-01  
- Prefer Diff tab full-screen for before/after fields  

## References

- Analysis chat 2026-09-09 (GUI density)  
- [`desktop-ui-backend-alignment.md`](../development/desktop-ui-backend-alignment.md)  
- [`plan-10-desktop-shell-policies-authoring-depth.md`](plan-10-desktop-shell-policies-authoring-depth.md) (shell chrome precedent)
