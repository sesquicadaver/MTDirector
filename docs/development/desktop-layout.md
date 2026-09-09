# Desktop layout density tokens

**PLAN-13 / DESK-LAYOUT-00…03.** Shared Avalonia resources and Snapshot / Semantic Diff / Drift density contracts for operator-readable data panes.

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
3. Later DESK-LAYOUT-04…09 rows migrate remaining magic heights to these tokens and add `GridSplitter` where needed.

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

## Living Spec

- `DesktopLayoutTokensLivingSpecTests` (+ `CtDeskLayout00…`) — token presence.  
- `DesktopLayoutSnapshotLivingSpecTests` (+ `CtDeskLayout01…`) — Snapshot single-primary + splitter contract.  
- `DesktopLayoutSemanticDiffLivingSpecTests` (+ `CtDeskLayout02…`) — Semantic Diff entry/detail splitter contract.  
- `DesktopLayoutDriftLivingSpecTests` (+ `CtDeskLayout03…`) — Drift three-pane splitter contract.
