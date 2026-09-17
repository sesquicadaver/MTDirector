# PLAN-31 — Desktop residual ListBox / Drift–Audit read-only a11y

**Date:** 2026-09-15 (inventory **DONE** 2026-09-15)  
**Status:** **PLAN-31 COMPLETE** — Inventory **DONE** (W7-262); seed **W7-263 (#932) DONE**; implement **W7-264 (#934) DONE**; seed **W7-265 (#935) DONE**; implement **W7-266 (#939) DONE**; seed **W7-267 (#940) DONE**; successor **PLAN-32** inventory **W7-268 (#943) DONE**; seed **W7-269 (#944) DONE**; implement **W7-270 (#946) DONE**; seed **W7-271 (#947) DONE**; implement **W7-272 (#951) DONE**; COMPLETE **W7-273 (#952) DONE**; **PLAN-32 COMPLETE**; successor **PLAN-33** inventory **W7-274 (#955) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-262 / PLAN-31 #931](https://github.com/sesquicadaver/MTDirector/issues/931) **DONE**  
**Predecessor:** PLAN-30 Watch operation-owner ACL / hub backpressure **COMPLETE**; PLAN-28 deferred ListBox hosts / Drift–Audit read-only JSON TextBoxes  
**Successor:** [`plan-32-controller-host-process-packaging.md`](plan-32-controller-host-process-packaging.md) (Controller host-process packaging templates)  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative prior tranche:** [`plan-28-desktop-residual-field-control-automation.md`](plan-28-desktop-residual-field-control-automation.md) (deferred ListBox / Drift–Audit JSON TextBoxes)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest remaining **product** continuous-queue a11y gap after PLAN-30 closed Watch OWN+BP: PLAN-28 intentionally deferred **ListBox host** `AutomationProperties.Name` and **Drift/Audit read-only** JSON TextBox Names. FIELD/CTRL (PLAN-28), HEALTH/RECONNECT (PLAN-29), and OWN/BP (PLAN-30) Living Spec locks remain — **do not regress**.

## Principles

1. ListBox hosts used for operator selection/browse must expose stable `AutomationProperties.Name` (control-level; item templates may stay out of scope until inventory refines).  
2. Drift SemanticDiff and Audit PayloadJson read-only TextBoxes must be nameable to AT / UI Automation without inventing editable fields.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-30 WATCH-OWN/BP rows — that tranche is **COMPLETE**.

## Out of scope (do not seed)

- Re-opening PLAN-30 WATCH-OWN / WATCH-BP product rows  
- Re-opening PLAN-28 DESK-A11Y-FIELD / DESK-A11Y-CTRL product rows  
- Replacing PLAN-29 HEALTH/RECONNECT Living Spec locks  
- Naming every ListBox *item* template host (nested ListBoxes without `ItemsSource`)  
- Ops / CRS / physical lab residuals

## Inventory evidence (2026-09-15 `main` @ `184bb85`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Operator ListBox hosts (`ItemsSource`-bound) in `MainWindow.axaml` | **43** hosts: Modules (1), Zones (3), Node (6), RoutingAssurance (3), Snapshot (3), Diff (2), Policies (13), Onboarding (3), Deployment (6), Drift (2), Audit (1) | **DONE (W7-264)** — all 43 hosts have `AutomationProperties.Name` |
| Nested ListBoxes (no `ItemsSource`) | Present inside item templates / decorative hosts (~71 additional `<ListBox>` tags) | Out of scope for LIST-01 (control-level Names on operator `ItemsSource` hosts only) |
| Drift read-only TextBox | `Text="{Binding Drift.SemanticDiffText}"` + `IsReadOnly="True"` | **DONE (W7-266)** — `AutomationProperties.Name="Semantic diff"` |
| Audit read-only TextBox | `Text="{Binding Audit.SelectedEvent.PayloadJson}"` + `IsReadOnly="True"` | **DONE (W7-266)** — `AutomationProperties.Name="Payload (JSON)"` |
| PLAN-28 FIELD/CTRL | TextBox / ComboBox / CheckBox / TabItem Names locked | Do not regress |
| PLAN-29 HEALTH/RECONNECT | Connected probe + reconnect Living Spec locked | Do not regress |
| PLAN-30 OWN/BP | Watch owner ACL + hub backpressure Living Spec locked | Do not regress |

### ItemsSource host inventory (43)

| Panel | Count | Bindings |
|-------|------:|----------|
| Policies | 13 | Catalog, ManagementPathFindingLines, FastTrackFindingLines, SafetyWitnessLines, SafetySystemTestLines, Rules, AddressObjects, ServiceObjects, ChainContracts, DiffRows, DiffLines, Findings, CompileArtifactLines |
| Node | 6 | WorkflowDeviceLines, ZoneSummaryLines, VrrpMembers, VrrpPairFindings, DeviceMembers, DeviceHashLines |
| Deployment | 6 | SemanticDiffRows, SemanticDiffLines, ArtifactLines, OrderLines, ProbeAndWatchdogLines, ProgressLines |
| Zones | 3 | Zones, Bindings, ResolveResults |
| RoutingAssurance | 3 | ExpectationLines, FindingLines, TraceSummaryLines |
| Snapshot | 3 | VisibleSections, ConfigurationRecords, ObservationRecords |
| Onboarding | 3 | Findings, Placements, ProgressLines |
| Diff | 2 | SectionGroups, VisibleEntries |
| Drift | 2 | Events, SelectedEventFindings |
| Modules | 1 | Modules |
| Audit | 1 | Events |

## Ranked Desktop residual ListBox / read-only a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-LIST-01** | ListBox host `AutomationProperties.Name` on all 43 `ItemsSource`-bound operator browse/select surfaces | 43 ItemsSource ListBox hosts named (W7-264 DONE); inventory baseline @ `184bb85` | implement **W7-264 (#934) DONE**; seed **W7-263 (#932) DONE**; seed **W7-265 (#935) DONE** |
| 2 | **DESK-A11Y-RO-01** | Drift SemanticDiff + Audit PayloadJson read-only TextBox Names | Names locked (`DesktopDriftAuditReadOnlyAutomationLivingSpecTests`) | implement **W7-266 (#939) DONE**; seed **W7-265 (#935) DONE**; COMPLETE seed **W7-267 (#940) DONE** |

Inventory (**W7-262 DONE**) locked ranking and opened LIST implement (**W7-264**) + RO seed (**W7-265**). Seed **W7-263** advanced NEXT to LIST; seed **W7-265** advances NEXT to RO implement (**W7-266**) and opens PLAN-31 COMPLETE follow-up (**W7-267**). No third vanity rank — nested item-template ListBoxes stay out of scope; FIELD/CTRL / HEALTH/RECONNECT / OWN/BP locks remain the regression corpus. Seed IDs **DESK-A11Y-LIST-01** / **DESK-A11Y-RO-01** are the canonical atomic row names.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-30 CLOSED)

PLAN-30 ranks 1…2 (**WATCH-OWN-01**, **WATCH-BP-01**) are **DONE**. No further PLAN-30 product rows.

## Adjacent residuals (not seeded here)

- Nested ListBox tags without `ItemsSource` (item templates) — deferred unless a later PLAN splits them  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-30 COMPLETE** (W7-260 WATCH-BP-01; seed **W7-261 DONE**).  
2. **W7-262 DONE** — PLAN-31 inventory; opened **W7-264** / **W7-265**.  
3. **W7-263 DONE** — seed advanced NEXT to **DESK-A11Y-LIST-01** (**W7-264**).  
4. **W7-264 DONE** — DESK-A11Y-LIST-01 Names locked (`DesktopListBoxHostAutomationLivingSpecTests`).  
5. **W7-265 DONE** — seed advanced NEXT to **DESK-A11Y-RO-01** (**W7-266**); opened PLAN-31 COMPLETE follow-up (**W7-267**).  
6. **W7-266 DONE** — DESK-A11Y-RO-01 Names locked (`DesktopDriftAuditReadOnlyAutomationLivingSpecTests`).  
7. **W7-267 DONE** — PLAN-31 COMPLETE; seeded PLAN-32 (**W7-268** / **W7-269**).

## §3.C NEXT

**§3.C NEXT = W7-296 (#999)** — Seed next PLAN-32 row after OPS-HOST-SYSTEMD-01 → OPS-HOST-WINSVC-01.
