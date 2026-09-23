# PLAN-62 — Repository-audit remediation (`acd0759`)

**Date:** 2026-09-23  
**Status:** **OPEN** — Inventory **DONE** (W7-393 #1195); seed **W7-394 (#1196) DONE**; **§3.C NEXT = W7-395 (#1197)** AUDIT-STATUS-01  
**Audit SHA:** `acd0759e85414a83460c4cab971db2b0b58b30cd`  
**Normative audit:** [`docs/audits/MTDirector-audit-acd0759-20260923.md`](../audits/MTDirector-audit-acd0759-20260923.md)  
**Predecessor:** PLAN-61 COMPLETE; freeze W7-392 (#1191) DONE (correlation-id wave closed)  
**Successor:** none until PLAN-62 COMPLETE (do not invent PLAN-63 in this cycle)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Аудит 2026-09-23: `MVP CLOSED` / `M7 CLOSED` / write path CLOSED не відповідають наскрізній реалізації. Виправлення — атомарні PR у залежному порядку нижче. Архітектуру не переписувати. Засів після freeze W7-392 дозволений лише з **аудиту/TOR** (цей документ), не з ErrorText grepping.

## Principles

1. Один writer на Device/Node (durable lease) перед будь-яким recovery mutation.  
2. Safety evidence і analysis — server-owned; клієнт задає намір, не PASS.  
3. Commit snapshot + journal — частина кожного ефекту; без цього drift/next plan брешуть.  
4. Один rollback coordinator для automatic / explicit / recovery.  
5. Один атомарний PR на рядок; Living Spec + тести в тому ж циклі; без force-push / skip hooks.  
6. Lab/CHR/`WriteEnabled` не стоп-гейт §3; live acceptance — окремий статус, не підміна unit PASS.

## Out of scope

- Campaigns, auto drift repair, auto-create management guard  
- NAT/RAW/Mangle/routing **writes** (read/projection — у scope F08 / AUDIT-CAP-03)  
- Broker / microservices / rewrite стека  
- Новий PLAN-63 у цьому циклі  
- Повторне відкриття DESK-*-FAULT / SNAP-*-CORR correlation wave  

## Branch protocol (кожне завдання)

Після merge попереднього:

```bash
git checkout main && git pull --ff-only
git checkout -b w7-NNN-<short-id>
# implement + Living Spec + service docs
git push -u origin HEAD
gh pr create …   # base=main
# CI green → triage threads → gh pr merge --squash
git checkout main && git pull --ff-only
```

Не тримати кілька незамержених feature-гілок одного PLAN одночасно. Seed-рядки також окремі гілки/PR (як у PLAN-26…61), крім цього inventory+seed циклу (W7-393+W7-394 разом за наказом оператора).

## Ranked remediation tranche (inventory lock)

| Rank | ID | Audit | Gap | Queue |
|------|----|-------|-----|-------|
| 0 | **PLAN62-INV-01** | — | Seed §3.C + land plan/audit | **W7-393 (#1195) DONE** |
| seed | — | — | Advance NEXT to first implement | **W7-394 (#1196) DONE** |
| 1 | **AUDIT-STATUS-01** | F14 | Honest CLOSED / acceptance statuses | **W7-395 (#1197) OPEN (NEXT)** |
| 2 | **AUDIT-SBOM-01** | F14 | SBOM/signing fail-closed | after STATUS seed |
| 3 | **AUDIT-OWN-01** | F01 | Onboarding durable lease vs recovery | after SBOM |
| 4 | **AUDIT-COMMIT-01** | F03 | Commit snapshot + journal persist | after OWN |
| 5 | **AUDIT-EVID-01** | F02 | Real safety evidence (no AllSafeEvidence) | after COMMIT |
| 6 | **AUDIT-RB-01** | F04 | Unified strict rollback | after EVID |
| 7 | **AUDIT-CLK-01** | F05 | RouterOS clock / TTL budget | after RB |
| 8 | **AUDIT-RPC-01** | F10 | Fast Start + live Watch | after CLK |
| 9 | **AUDIT-CAP-03** | F08 | Full capture projection | after RPC |
| 10 | **AUDIT-CAP-04** | F09 | Capture attempt identity | after CAP-03 |
| 11 | **AUDIT-AN-03** | F06 | Server-owned analysis | after CAP-04 |
| 12 | **AUDIT-BIND-01** | F07 | Composition from active bindings | after AN-03 |
| 13 | **AUDIT-GUI-02** | F11 | Controller onboarding + stale policy GUI | after BIND |
| 14 | **AUDIT-DRIFT-01** | F12 | Drift from live RouterOS read | after GUI (needs COMMIT) |
| 15 | **AUDIT-M7-01** | F13 | Wire M7 production lifecycle | after DRIFT |
| 16 | **AUDIT-ACC-01** | F14 | Acceptance by behavior not file presence | after M7 |
| 17 | **PLAN62-DONE-01** | — | COMPLETE + freeze / NEXT=none | last |

## Inventory evidence (W7-393 @ `acd0759e`)

Normative audit findings F01–F14 mapped to ranks above. First wave after seed: **AUDIT-STATUS-01** (honest statuses) before write-path code.

## Dual track

Product §3 never waits on GNS3. Controlled CHR verification is DoD for deploy/onboarding rows after unit/integration Living Specs.

## §3.C NEXT

**§3.C NEXT = W7-395 (#1197)** — AUDIT-STATUS-01.
