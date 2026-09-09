# PLAN-13 — Desktop layout density Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **DONE** (W7-126); **DESK-LAYOUT-00 DONE** (W7-127); **DESK-LAYOUT-01 DONE** (W7-129); **DESK-LAYOUT-02 DONE** (W7-131); **DESK-LAYOUT-03 DONE** (W7-133); **DESK-LAYOUT-04 DONE** (W7-135); **DESK-LAYOUT-05 DONE** (W7-137); **DESK-LAYOUT-06 DONE** (W7-139); **DESK-LAYOUT-07 DONE** (W7-141); seed **W7-142 DONE** → implement **DESK-LAYOUT-08 OPEN** (W7-143)  
**PLAN issue / queue:** [W7-126 / PLAN-13 #654](https://github.com/sesquicadaver/MTDirector/issues/654)  
**Predecessor:** GUI density analysis (lab session 2026-09-09); PLAN-12 Policies residual lifecycle **COMPLETE**; product seed **W7-125 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml), [`App.axaml`](../../src/Mfc.Desktop/App.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

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
| Queue fit | Seed **after** PLAN-12 COMPLETE; inventory locks **DESK-LAYOUT-00** as first implement |

## Evidence baseline (layout density)

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Shared tokens / density doc | `App.axaml` styles; magic MaxHeight in MainWindow | PLAN-13 LAYOUT-00 |
| Snapshots | Competing `*` rows + ClipToBounds | PLAN-13 LAYOUT-01 |
| Semantic Diff / Drift / Audit / Policies / Node / Ops / Shell / Zones | MaxHeight cascade / missing splitters | LAYOUT-02…09 |
| Regression lock | No density Living Spec matrix | LAYOUT-10 |

## Ranked Desktop layout density tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-LAYOUT-00** | No shared layout tokens / density contract doc | `App.axaml` + `docs/development/desktop-layout.md`; Snapshot primary list binds tokens | **W7-127 DONE** (#657) |
| 2 | **DESK-LAYOUT-01** | Snapshot tab: competing `*` + clipped detail | `MainWindow.axaml` Snapshot panel `Auto,*,Auto,*` + TabControl + GridSplitter | **W7-129 DONE** (#659); seeded by **W7-128 DONE** (#658) |
| 3 | **DESK-LAYOUT-02** | Semantic Diff: entry list vs before/after starved | Semantic Diff panel `*,Auto,*` + GridSplitter; no before/after MaxHeight | **W7-131 DONE** (#664); seeded by **W7-130 DONE** (#663) |
| 4 | **DESK-LAYOUT-03** | Drift: competing stars + findings `MaxHeight="200"` | Drift `Auto,*,Auto,*,Auto,*` + dual GridSplitter; no findings MaxHeight | **W7-133 DONE** (#668); seeded by **W7-132 DONE** (#667) |
| 5 | **DESK-LAYOUT-04** | Audit: `Auto,*,*` without splitter | Audit panel | **W7-135 DONE** (#672); seeded by **W7-134 DONE** (#671) |
| 6 | **DESK-LAYOUT-05** | Policies: scroll page of tiny MaxHeight lists (80–160) | Policies panel | **W7-137 DONE** (#676); seeded by **W7-136 DONE** (#675) |
| 7 | **DESK-LAYOUT-06** | Node + RoutingAssurance MaxHeight cascade | Node panel | **W7-139 DONE** (#680); seeded by **W7-138 DONE** (#679) |
| 8 | **DESK-LAYOUT-07** | Operations (Onboarding/Deploy) MaxHeight lists | Operations tabs | **W7-141 DONE** (#684); seeded by **W7-140 DONE** (#683) |
| 9 | **DESK-LAYOUT-08** | Shell chrome: fixed columns; no column splitter | ~65–150 | seeded by **W7-142 DONE** (#687) → implement **W7-143 OPEN** (#688) |
| 10 | **DESK-LAYOUT-09** | Inventory / Zones nested MaxHeight frames | ~341–426 | seed after DESK-LAYOUT-08 |
| 11 | **DESK-LAYOUT-10** | Regression lock + docs sync | Living Specs + alignment / testing docs | seed after DESK-LAYOUT-09; closes PLAN-13 |

## Suggested PR slicing (still linear for `/autopilot`)

| Wave | Ships | Notes |
|------|-------|-------|
| A | LAYOUT-00 + LAYOUT-01 | Tokens + Snapshot (highest pain) — **seeded** |
| B | LAYOUT-02 | Diff tab |
| C | LAYOUT-03 + LAYOUT-04 | Drift + Audit |
| D | LAYOUT-05 | Policies (largest XAML churn) |
| E | LAYOUT-06 + LAYOUT-07 + LAYOUT-09 | Node / Ops / Inventory leftovers |
| F | LAYOUT-08 + LAYOUT-10 | Shell + Living Spec complete + docs |

Each wave = one §3 atomic ID (or seed+implement pair per project habit). Prefer **one ID per wave** for reviewability.

## Dual track

Product §3 never waits on GNS3. Validate layout on Desktop against lab captures **ops-parallel** (`~/gns3-lab` day-2 filter density is a good manual probe, not a CI gate).

## References

- Analysis chat 2026-09-09 (GUI density)  
- [`desktop-ui-backend-alignment.md`](../development/desktop-ui-backend-alignment.md)  
- [`plan-10-desktop-shell-policies-authoring-depth.md`](plan-10-desktop-shell-policies-authoring-depth.md) (shell chrome precedent)

## §3.C NEXT

**§3.C NEXT = W7-143 (#688)** — DESK-LAYOUT-08 Shell chrome column splitter Living Spec depth.
