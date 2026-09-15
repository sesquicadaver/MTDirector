# PLAN-34 — Desktop operator launch packaging templates (.desktop / Windows shortcut)

**Date:** 2026-09-15 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-278); seed **W7-279 (#964) OPEN**; predecessor **PLAN-33 COMPLETE**  
**PLAN issue / queue:** [W7-278 / PLAN-34 #963](https://github.com/sesquicadaver/MTDirector/issues/963) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-33 Desktop Inventory TreeView a11y **COMPLETE** (TREE-01 sole; TAB-01 dropped)  
**Normative files:** [`package-desktop.sh`](../../scripts/release/package-desktop.sh), [`packaging.md`](../release/packaging.md)  
**Normative prior locks:** PLAN-32 OPS-HOST-SYSTEMD/WINSVC; PLAN-16…33 Desktop AutomationProperties; W7-22 MSI/AppImage — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **product** continuous-queue packaging gap after PLAN-33 closed Inventory TreeView a11y: framework-dependent Desktop zip/tar publish still lacks operator launch templates (Linux freedesktop `.desktop`, optional Windows shortcut sketch), while Controller already has systemd/WinSW from PLAN-32. Nested ListBox / TabControl container a11y vanity and native MSI/AppImage stay locked — **do not re-open**.

## Principles

1. Operator launch affordances for framework-dependent Desktop must match `$OUT_DIR/desktop` layout from `package-desktop.sh`.  
2. Inventory may rank an optional Windows shortcut / Start Menu sketch as a second atomic row when evidence supports it.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-33 DESK-A11Y product rows — that tranche is **COMPLETE** (TAB-01 stays dropped).  
5. Do not re-open MSI / AppImage / `--self-contained false` default (W7-22).

## Out of scope (do not seed)

- Re-opening PLAN-33 DESK-A11Y-TREE / dropped TAB rows  
- Nested ListBox item-template Names (vanity)  
- Native MSI / AppImage / changing `--self-contained false` without separate packaging inventory  
- Ops / CRS / physical lab live runners as §3 stop-gates

## Inventory evidence (seed baseline 2026-09-15 `main` @ `42d83fe`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `package-desktop.sh` | `DEST="$OUT_DIR/desktop"`; `dotnet publish … --self-contained false -o "$DEST"`; zip/tar archive | No accompanying `.desktop` / Windows shortcut template copy |
| `docs/release/packaging.md` | Desktop installer = zip/tar publish directory | Documents MSI residual; no Linux desktop-entry template path |
| PLAN-32 Controller packaging | `packaging/systemd/` + `packaging/windows/` shipped | Desktop launch templates still missing |
| PLAN-33 TREE-01 | Inventory TreeView named | COMPLETE — do not invent TAB-01 vanity |

## Ranked Desktop operator launch packaging tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-HOST-LINUX-01** | freedesktop `.desktop` template (+ docs) for framework-dependent Desktop matching `$OUT_DIR/desktop` | `package-desktop.sh`; intended `packaging/linux/mfc-desktop.desktop` | after inventory **W7-278**; seed **W7-279 (#964)** |
| 2 | **DESK-HOST-WIN-01** *(optional)* | Windows shortcut / Start Menu sketch for framework-dependent Desktop | Seed baseline Windows Desktop zip lacks shortcut template | inventory may confirm / drop |

Inventory (**W7-278**) may refine ranking, drop WIN-01 if vanity, and open implement issues; seed **W7-279** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-33 CLOSED)

PLAN-33 sole ranked row (**DESK-A11Y-TREE-01**) is **DONE**. **DESK-A11Y-TAB-01** stays dropped. No further PLAN-33 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity (TabItems already named)  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-33 COMPLETE** (W7-276 DESK-A11Y-TREE-01; seed **W7-277 DONE**).  
2. **W7-278 OPEN** — PLAN-34 inventory → open first Desktop launch-template implement + follow-up seeds.  
3. **W7-279 OPEN** — seed first PLAN-34 implement after inventory.  
4. Execute ranked DESK-HOST-LINUX / optional DESK-HOST-WIN rows atomically.

## §3.C NEXT

**§3.C NEXT = W7-278 (#963)** — PLAN-34 Inventory Desktop operator launch packaging templates after PLAN-33.
