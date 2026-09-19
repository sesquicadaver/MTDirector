# PLAN-34 — Desktop operator launch packaging templates (.desktop / Windows shortcut)

**Date:** 2026-09-15 (inventory **DONE** @ `3e112bf`; seed W7-279 **DONE**)  
**Status:** **PLAN-34 COMPLETE** — Inventory **DONE** (W7-278); seed **W7-279 (#964) DONE**; implement **W7-280 (#966) DONE**; WIN seed **W7-281 (#967) DONE**; WIN implement **W7-282 (#971) DONE**; COMPLETE seed **W7-283 (#972) DONE**; successor **PLAN-35** inventory **W7-284 (#975) DONE**; seed **W7-285 (#976) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-278 / PLAN-34 #963](https://github.com/sesquicadaver/MTDirector/issues/963) **DONE**  
**Predecessor:** PLAN-33 Desktop Inventory TreeView a11y **COMPLETE** (TREE-01 sole; TAB-01 dropped)  
**Normative files:** [`package-desktop.sh`](../../scripts/release/package-desktop.sh), [`packaging.md`](../release/packaging.md)  
**Normative prior locks:** PLAN-32 OPS-HOST-SYSTEMD/WINSVC; PLAN-16…33 Desktop AutomationProperties; W7-22 MSI/AppImage — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **product** continuous-queue packaging gap after PLAN-33 closed Inventory TreeView a11y: framework-dependent Desktop zip/tar publish still lacks operator launch templates (Linux freedesktop `.desktop`, Windows Start Menu shortcut sketch), while Controller already has systemd/WinSW from PLAN-32. Nested ListBox / TabControl container a11y vanity and native MSI/AppImage stay locked — **do not re-open**.

## Principles

1. Operator launch affordances for framework-dependent Desktop must match `$OUT_DIR/desktop` layout from `package-desktop.sh`.  
2. Inventory **confirmed** the Windows Start Menu shortcut sketch as rank 2 (parity with PLAN-32 dual-rank host packaging; not vanity).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-33 DESK-A11Y product rows — that tranche is **COMPLETE** (TAB-01 stays dropped).  
5. Do not re-open MSI / AppImage / `--self-contained false` default (W7-22).

## Out of scope (do not seed)

- Re-opening PLAN-33 DESK-A11Y-TREE / dropped TAB rows  
- Nested ListBox item-template Names (vanity)  
- Native MSI / AppImage / changing `--self-contained false` without separate packaging inventory  
- Ops / CRS / physical lab live runners as §3 stop-gates

## Inventory evidence (W7-278 @ `main` `3e112bf`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `package-desktop.sh` | `DEST="$OUT_DIR/desktop"`; `dotnet publish … --self-contained false -o "$DEST"`; zip/tar archive; dry-run writes `Mfc.Desktop` | No accompanying `.desktop` / Windows shortcut template copy into publish tree or `packaging/` |
| `docs/release/packaging.md` | Desktop installer = zip/tar; Controller host-process templates documented | No Desktop launch-template rows beside systemd/WinSW |
| HOWTO §6 | Desktop launch = unzip + run `/opt/mfc/desktop/Mfc.Desktop` (or `Mfc.Desktop.exe`) | No freedesktop / Start Menu template pointers (Controller templates already listed) |
| Repo `packaging/` | `systemd/mfc-controller.service` + `windows/mfc-controller.winsw.xml` only | **0** `*.desktop` files; **0** Desktop shortcut sketches |
| PLAN-33 TREE-01 | Inventory TreeView named | COMPLETE — do not invent TAB-01 vanity |

## Ranked Desktop operator launch packaging tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-HOST-LINUX-01** | freedesktop `.desktop` template (+ docs) for framework-dependent Desktop matching `$OUT_DIR/desktop` → `/opt/mfc/desktop/Mfc.Desktop` | `package-desktop.sh`; intended `packaging/linux/mfc-desktop.desktop` | implement **W7-280 (#966)** after seed **W7-279 (#964)** |
| 2 | **DESK-HOST-WIN-01** | Windows Start Menu shortcut sketch for framework-dependent Desktop (`win-x64` → `Mfc.Desktop.exe`) | HOWTO Windows unzip path; intended `packaging/windows/mfc-desktop-start-menu.ps1` | seed **W7-281 (#967)** after LINUX DONE |

Inventory (**W7-278 DONE**) confirmed both ranks (WIN-01 kept for PLAN-32 host-packaging parity). Seed **W7-279** advances NEXT to DESK-HOST-LINUX-01 implement.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-33 CLOSED)

PLAN-33 sole ranked row (**DESK-A11Y-TREE-01**) is **DONE**. **DESK-A11Y-TAB-01** stays dropped. No further PLAN-33 product rows.

## Adjacent residuals (seeded as PLAN-34 COMPLETE / PLAN-35)

- Desktop launch-template publish bundling (`package-desktop.sh` → `OUT_DIR/desktop/`) — **PLAN-35** [`plan-35-desktop-launch-template-publish-bundling.md`](plan-35-desktop-launch-template-publish-bundling.md)

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity (TabItems already named)  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-33 COMPLETE** (W7-276 DESK-A11Y-TREE-01; seed **W7-277 DONE**).  
2. **W7-278 DONE** — PLAN-34 inventory; opened **W7-280 (#966)** LINUX implement + **W7-281 (#967)** WIN seed.  
3. **W7-279 DONE** — seed first PLAN-34 implement → DESK-HOST-LINUX-01.  
4. **W7-280 DONE** / **W7-282 DONE** — both DESK-HOST ranks shipped; **W7-283 DONE** — PLAN-34 COMPLETE → PLAN-35.
5. Successor **PLAN-35** inventory **W7-284 DONE**; seed **W7-285 OPEN** (**§3.C NEXT**).

## §3.C NEXT

**§3.C NEXT = W7-387 (#1179)** — PLAN-35 Inventory Desktop launch-template publish bundling after PLAN-34.
