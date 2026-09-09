# Desktop layout density tokens

**PLAN-13 COMPLETE / DESK-LAYOUT-00…10.** Shared Avalonia resources and Snapshot / Semantic Diff / Drift density contracts for operator-readable data panes.

## Normative tokens (`App.axaml`)

| Key | Role |
|-----|------|
| `Mfc.ListMinHeight` | Floor height for primary list surfaces (ListBox / list frames) |
| `Mfc.DetailMinHeight` | Floor height for selected-record / detail panes |
| `Mfc.SectionSpacing` | Default vertical spacing between section chrome blocks |

Use `{StaticResource Mfc.ListMinHeight}` (and siblings) instead of magic `MinHeight` / spacing numbers on primary panes.

## Principles (PLAN-13)

1. One primary data pane per view grows with `*` (or fills a ScrollViewer).  
2. No hard `MaxHeight` on primary lists unless paired with a splitter or an explicit compact mode.  
3. DESK-LAYOUT-10 regression lock asserts DESK-LAYOUT-00…09 Living Specs and absence of primary-list MaxHeight cascade.

## Snapshot (DESK-LAYOUT-01)

In [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml) Snapshot tab:

- Inner panel `RowDefinitions="Auto,*,Auto,*"` — header, primary list, `GridSplitter`, detail.  
- Configuration **or** Observations via `TabControl` (one primary `*` at a time).  
- Primary lists bind `Mfc.ListMinHeight`; detail binds `Mfc.DetailMinHeight` with no `MaxHeight` cap.  
- Vertical `GridSplitter` between list and detail so the operator can reclaim space.

## Semantic Diff (DESK-LAYOUT-02)

In the Semantic Diff tab right pane:

- `RowDefinitions="*,Auto,*"` — entry list `*`, vertical `GridSplitter`, before/after detail `*`.  
- Entry list binds `Mfc.ListMinHeight`; detail binds `Mfc.DetailMinHeight`.  
- Before/after panes scroll inside; no `MaxHeight="220"` trap on the detail grid.

## Drift (DESK-LAYOUT-03)

In the Drift module:

- `RowDefinitions="Auto,*,Auto,*,Auto,*"` — header, events `*`, `GridSplitter`, findings `*`, `GridSplitter`, detail `*`.  
- Events and findings bind `Mfc.ListMinHeight`; detail binds `Mfc.DetailMinHeight`.  
- Findings pane has **no** `MaxHeight` (removed the old `MaxHeight="200"` trap).  
- `ClipToBounds` remains; pane floors keep content readable at shell `MinHeight=680`.



## Audit (DESK-LAYOUT-04)

In the Audit module:

- `RowDefinitions="Auto,*,Auto,*"` — header, events `*`, vertical `GridSplitter`, payload `*`.  
- Event list binds `Mfc.ListMinHeight`; payload binds `Mfc.DetailMinHeight`.  
- Operator can reclaim space between list and JSON payload (no starved competing `*` without splitter).



## Policies (DESK-LAYOUT-05)

In the Policies scroll page (`IsPoliciesSelected`):

- Catalog, rules, objects, safety findings, diff/compose/compile lists bind `MinHeight="{StaticResource Mfc.ListMinHeight}"`.  
- Removed the tiny `MaxHeight` cascade (`80`–`180`) that starved list content inside the vertical `ScrollViewer`.  
- Lists grow with content; the page scrolls as a whole (readable operator density).



## Node + Routing assurance (DESK-LAYOUT-06)

In the Node scroll page (`IsNodeSelected`), including the nested Routing assurance panel:

- Workflow / zones / VRRP / devices / hashes and RoutingAssurance expectation / finding / trace lists bind `MinHeight="{StaticResource Mfc.ListMinHeight}"`.  
- Removed `MaxHeight` cascade (`160` / `200` / `280`) that starved list content inside the vertical `ScrollViewer`.  
- Lists grow with content; the page scrolls as a whole.



## Operations Onboarding/Deploy (DESK-LAYOUT-07)

In Operations tabs Onboarding and Deploy (`IsOperationsSelected`):

- Prerequisite / placements / progress and Deploy diff / artifacts / order / probes / progress lists bind `MinHeight="{StaticResource Mfc.ListMinHeight}"`.  
- Removed `MaxHeight` cascade (`100` / `120` / `140`) that starved list content inside the vertical `ScrollViewer`.  
- Incident tab is out of scope for this row (no MaxHeight list cascade there).



## Shell chrome (DESK-LAYOUT-08)

Main shell grid (`MainWindow.axaml`):

- `ColumnDefinitions="260,Auto,150,Auto,*"` — Inventory, column `GridSplitter`, Modules, column `GridSplitter`, content `*`.  
- Replaces fixed spacer columns `12` so the operator can reclaim horizontal space.  
- Pane floors: Inventory `MinWidth=160`, Modules `MinWidth=100`, content `MinWidth=240`.



## Inventory / Zones (DESK-LAYOUT-09)

In Inventory detail Zones panel:

- Company zones / Node bindings lists and Resolve results frame bind `MinHeight="{StaticResource Mfc.ListMinHeight}"`.  
- Removed nested `MaxHeight` caps (`220` / `240`) that starved zone lists inside the Inventory scroll page.



## Regression lock (DESK-LAYOUT-10)

**PLAN-13 COMPLETE.**

- All DESK-LAYOUT-00…09 Desktop Living Specs remain present with their primary AC methods.  
- `MainWindow.axaml` has no primary-list `MaxHeight` cascade residue (`80`…`280` values used historically).  
- Matrix: `DesktopLayoutRegressionLockLivingSpecTests` (+ `CtDeskLayout10…`).

## Living Spec

- `DesktopLayoutTokensLivingSpecTests` (+ `CtDeskLayout00…`) — token presence.  
- `DesktopLayoutSnapshotLivingSpecTests` (+ `CtDeskLayout01…`) — Snapshot single-primary + splitter contract.  
- `DesktopLayoutSemanticDiffLivingSpecTests` (+ `CtDeskLayout02…`) — Semantic Diff entry/detail splitter contract.  
- `DesktopLayoutDriftLivingSpecTests` (+ `CtDeskLayout03…`) — Drift three-pane splitter contract.  
- `DesktopLayoutAuditLivingSpecTests` (+ `CtDeskLayout04…`) — Audit list/payload splitter contract.  
- `DesktopLayoutPoliciesLivingSpecTests` (+ `CtDeskLayout05…`) — Policies MaxHeight→token floors contract.  
- `DesktopLayoutNodeRoutingLivingSpecTests` (+ `CtDeskLayout06…`) — Node + RoutingAssurance token floors contract.  
- `DesktopLayoutOperationsLivingSpecTests` (+ `CtDeskLayout07…`) — Operations Onboarding/Deploy token floors contract.  
- `DesktopLayoutShellChromeLivingSpecTests` (+ `CtDeskLayout08…`) — Shell chrome column splitter contract.  
- `DesktopLayoutZonesLivingSpecTests` (+ `CtDeskLayout09…`) — Inventory/Zones MaxHeight→token floors contract.  
- `DesktopLayoutRegressionLockLivingSpecTests` (+ `CtDeskLayout10…`) — PLAN-13 COMPLETE regression lock.
