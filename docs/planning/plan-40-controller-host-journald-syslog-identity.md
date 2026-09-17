# PLAN-40 — Controller host journald/syslog identity (SyslogIdentifier)

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-304); seed **W7-305 (#1016) OPEN**; predecessor **PLAN-39 COMPLETE**  
**PLAN issue / queue:** [W7-304 / PLAN-40 #1015](https://github.com/sesquicadaver/MTDirector/issues/1015) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-39 Controller host operator doc packaging **COMPLETE** (OPS-HOST-DOC-01)  
**Normative files:** [`mfc-controller.service`](../../packaging/systemd/mfc-controller.service), [`installation.md`](../operations/installation.md), [`packaging.md`](../release/packaging.md)  
**Normative prior locks:** PLAN-32…39 host templates + BUNDLE + ENV + SYSUSERS + DOC; W7-22 MSI/AppImage — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **Controller host observability** continuous-queue gap after PLAN-39 shipped operator doc packaging: `mfc-controller.service` still has **no** `SyslogIdentifier=` / `StandardOutput=journal` (or equivalent), so operators cannot reliably filter `journalctl` for the Controller host process despite a complete systemd unit + Documentation= README.

## Principles

1. Host-process packaging should include **journald/syslog identity** so operators can diagnose the running Controller (`journalctl -t mfc-controller` / unit logs).  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-39 OPS-HOST-DOC product rows — that tranche is **COMPLETE**.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer observability over another publish-tree copy vanity row (packaging artifact-shipping wave PLAN-32…39 is saturating).

## Out of scope (do not seed)

- Re-opening PLAN-39 OPS-HOST-DOC / SYSUSERS / ENV authoring  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Bundling WinSW **binary** (licensing / third-party; stay XML-only)  
- Full centralized logging/SIEM (out of MVP scope)

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-39 COMPLETE / `c2a807e9`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `mfc-controller.service` | Unit + User/Group + Documentation= + Restart | **No** `SyslogIdentifier=` / `StandardOutput=journal` / `StandardError=journal` |
| Docs / HOWTO | Install + enable unit | No `journalctl` identity guidance for Controller |
| Glob / rg | `SyslogIdentifier` absent under `packaging/` @ `c2a807e9` | Confirmed |

## Ranked Controller host journald/syslog tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **OPS-HOST-LOG-01** | Author systemd journald/syslog identity (`SyslogIdentifier=mfc-controller` + journal stdout/stderr) + docs/Living Spec | Unit missing identity @ `c2a807e9` | after inventory **W7-304**; seed **W7-305 (#1016)** |

Inventory (**W7-304**) may refine ranking and open implement issues; seed **W7-305** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-39 CLOSED)

PLAN-39 sole ranked row (**OPS-HOST-DOC-01**) is **DONE**. No further PLAN-39 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- WinSW binary redistribution — not §3 (operator-supplied)  
- Optional GPG/Sigstore CI crypto beyond QG-SIGN-01 cleartext — quality residual (not this tranche)  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-39 COMPLETE** (W7-302 OPS-HOST-DOC-01; seed **W7-303 DONE**).  
2. **W7-304 OPEN** — PLAN-40 inventory → open first journald/syslog implement + follow-up seeds.  
3. **W7-305 OPEN** — seed first PLAN-40 implement after inventory.  
4. Execute ranked OPS-HOST-LOG row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-304 (#1015)** — PLAN-40 Inventory Controller host journald/syslog identity after PLAN-39.
