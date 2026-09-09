# Desktop layout density tokens

**PLAN-13 / DESK-LAYOUT-00.** Shared Avalonia resources for operator-readable data panes.

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
3. Later DESK-LAYOUT-01…09 rows migrate remaining magic heights to these tokens and add `GridSplitter` where needed.

## First consumer

Snapshot → Configuration records ListBox and selected-record detail grid in [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml) bind `Mfc.ListMinHeight` / `Mfc.DetailMinHeight`.

## Living Spec

`DesktopLayoutTokensLivingSpecTests` (+ `CtDeskLayout00…`) lock token presence and ≥1 MainWindow primary-list reference.
