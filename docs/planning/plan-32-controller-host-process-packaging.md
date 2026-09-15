# PLAN-32 — Controller host-process packaging templates (systemd / Windows Service)

**Date:** 2026-09-15 (inventory **DONE** 2026-09-15)  
**Status:** Inventory **DONE** (W7-268); seed **W7-269 (#944) DONE**; implement **W7-270 (#946) OPEN** (**§3.C NEXT**); seed **W7-271 (#947) OPEN** → **OPS-HOST-WINSVC-01**  
**PLAN issue / queue:** [W7-268 / PLAN-32 #943](https://github.com/sesquicadaver/MTDirector/issues/943) **DONE**  
**Predecessor:** PLAN-31 Desktop residual ListBox / Drift–Audit read-only a11y **COMPLETE**  
**Normative files:** [`docs/howto/build-and-run.md`](../howto/build-and-run.md), [`docs/operations/installation.md`](../operations/installation.md), [`docs/release/packaging.md`](../release/packaging.md), [`scripts/release/`](../../scripts/release/)  
**Normative prior locks:** W7-22 zip/tar installer substitute; W7-23 SHA256SUMS; W7-24 SBOM; QG-SIGN-01 — **do not regress / do not invent MSI**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **ops/packaging** continuous-queue gap after Desktop operator a11y saturation (PLAN-16…31): release packages are framework-dependent zip/tar, but the repo has **no** managed host-process templates for Controller (systemd unit / Windows Service). Operators must invent their own units. MSI/AppImage remain intentional MVP residuals — **do not re-open**. Nested ListBox item-template a11y stays deferred vanity.

## Principles

1. Controller packages must be installable as a managed host process with documented templates (Linux systemd + Windows Service equivalent).  
2. Templates must match framework-dependent publish layout from `scripts/release/package-controller.sh`.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-31 DESK-A11Y-LIST/RO product rows — that tranche is **COMPLETE**.  
5. Do not invent native MSI/AppImage/setup.exe (W7-22 lock).

## Out of scope (do not seed)

- Re-opening PLAN-31 DESK-A11Y-LIST / DESK-A11Y-RO product rows  
- Nested ListBox tags without `ItemsSource` (item templates)  
- Native MSI / AppImage / `.dmg`  
- Changing default `--self-contained false` publish policy without inventory evidence  
- Ops / CRS / physical lab live runners as §3 stop-gates

## Inventory evidence (2026-09-15 `main` @ `a8834eb`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| HOWTO §6.4 gap #4 | Explicit: «Немає systemd unit / Windows Service шаблонів у репо (ручний host або власний unit)» | No repo templates |
| HOWTO §4 «Запуск з пакету» | Linux: `cd "$OUT_DIR/controller"` → `./Mfc.Controller` (foreground) | No `systemctl enable --now` path |
| `docs/operations/installation.md` Controller § | Steps 1–4: obtain package → configure `MFC__…` → migrations → **Start `Mfc.Controller` and verify gRPC health** | Manual process start only; no unit/service install |
| `docs/release/packaging.md` | `package-controller.sh` → `OUT_DIR/controller/` + `controller.artifact-path.txt`; Desktop zip = installer substitute | No service artifact column; MSI residual locked |
| `scripts/release/package-controller.sh` | `DEST="$OUT_DIR/controller"`; `dotnet publish … -r "$RID" --self-contained false -o "$DEST"`; dry-run writes `Mfc.Controller` + `Mfc.Controller.runtimeconfig.json` | No accompanying `.service` / WinSW / sc.exe template copy |
| `scripts/release/` siblings | `package-desktop.sh`, `create-migration-bundle.sh`, `generate-sbom-and-checksums.sh`, `run-dependency-scan.sh`, `_common.sh` | None emit host-process unit/service files |
| Repo `*.service` | **0** files under tree (excluding `.git`) | Template path not yet present |
| W7-22…24 packaging locks | zip/tar + SHA256SUMS + SBOM Living Spec locked | Do not regress |
| Desktop a11y (PLAN-16…31) | Buttons / fields / ListBox hosts / RO TextBoxes named | Saturated — do not invent nested-ListBox vanity |

### Publish layout contract (locked for templates)

| Item | Value |
|------|-------|
| Output dir | `$OUT_DIR/controller/` |
| Entrypoint (linux-x64) | `Mfc.Controller` (executable, framework-dependent) |
| Runtimeconfig | `Mfc.Controller.runtimeconfig.json` (tfm `net10.0`, `Microsoft.AspNetCore.App`) |
| Default RID | `linux-x64` (`MFC_RELEASE_RID`) |
| Self-contained | **false** (requires host ASP.NET Core / .NET runtime) |
| Intended systemd unit path | `packaging/systemd/mfc-controller.service` |
| Intended Windows Service template path | `packaging/windows/mfc-controller.winsw.xml` (WinSW-style; inventory locks path — implement may refine contents) |

## Ranked Controller host-process packaging tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **OPS-HOST-SYSTEMD-01** | systemd unit template (+ docs) for framework-dependent Controller matching `$OUT_DIR/controller` layout | HOWTO §6.4 #4; **0** `*.service`; `package-controller.sh` `--self-contained false` | implement **W7-270 (#946) OPEN**; seed **W7-269 (#944) DONE** |
| 2 | **OPS-HOST-WINSVC-01** | Windows Service host template (+ docs) for Controller (`win-x64` publish → `Mfc.Controller.exe`) | HOWTO §6.4 #4; installation.md Windows path is manual exe; no WinSW/sc template | seed **W7-271 (#947) OPEN** (opens WINSVC implement after SYSTEMD) |

Inventory (**W7-268 DONE**) locked ranking and opened SYSTEMD implement (**W7-270**) + WINSVC seed (**W7-271**). Seed **W7-269 DONE** advanced §3.C NEXT to **OPS-HOST-SYSTEMD-01** (**W7-270**). No third vanity rank — MSI/AppImage stay W7-22 residuals; self-contained default stays locked unless a later PLAN re-opens packaging policy. Canonical atomic row names: **OPS-HOST-SYSTEMD-01** / **OPS-HOST-WINSVC-01**.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-31 CLOSED)

PLAN-31 ranks 1…2 (**DESK-A11Y-LIST-01**, **DESK-A11Y-RO-01**) are **DONE**. No further PLAN-31 product rows.

## Adjacent residuals (not seeded here)

- Inventory `TreeView` `AutomationProperties.Name` (primary browse host) — deferred unless a later PLAN picks a11y again  
- Nested ListBox item-template hosts — deferred vanity  
- Self-contained / single-file publish default — separate packaging policy decision  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-31 COMPLETE** (W7-266 DESK-A11Y-RO-01; seed **W7-267 DONE**).  
2. **W7-268 DONE** — PLAN-32 inventory; opened **W7-270** / **W7-271**.  
3. **W7-269 DONE** — seed advanced NEXT to **OPS-HOST-SYSTEMD-01** (**W7-270**).  
4. Execute ranked OPS-HOST-SYSTEMD / OPS-HOST-WINSVC rows atomically.

## §3.C NEXT

**§3.C NEXT = W7-270 (#946)** — OPS-HOST-SYSTEMD-01 — systemd unit template for framework-dependent Controller.
