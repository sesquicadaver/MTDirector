# PLAN-63 — Re-audit residuals wave A (post PLAN-62)

**Date:** 2026-10-08  
**Status:** **COMPLETE** — Inventory **DONE** (W7-428 #1253); seed **W7-429 (#1254) DONE**; **OWN-HB-01 W7-430 (#1255) DONE**; seed **W7-431 (#1256) DONE**; **EVID-LIVE-01 W7-432 (#1257) DONE**; seed **W7-433 (#1258) DONE**; **M7-PRES-01 W7-434 (#1259) DONE**; seed **W7-435 (#1260) DONE**; **CAP-IDEM-01 W7-436 (#1261) DONE**; seed **W7-437 (#1263) DONE**; **PLAN63-DONE-01 W7-438 (#1264) DONE**  

**Handoff freeze (W7-438):** §3.C NEXT = none until operator TOR seed of wave B / PLAN-64 (Layer C live CHR/CRS). Autopilot must not invent PLAN-64.

**Normative TOR / audit:** [`docs/audits/MTDirector-reaudit-post-plan62-20261008.md`](../audits/MTDirector-reaudit-post-plan62-20261008.md) (operator order: **A now**, **B** Layer C as next wave)  
**Predecessor audit:** [`docs/audits/MTDirector-audit-acd0759-20260923.md`](../audits/MTDirector-audit-acd0759-20260923.md) via PLAN-62 COMPLETE  
**Predecessor:** PLAN-62 COMPLETE (W7-427 #1248); freeze NEXT=none lifted by this operator TOR  
**Successor (wave B, not seeded here):** PLAN-64 — Layer C live CHR/CRS acceptance (operator seed after PLAN-63 DONE)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Оператор 2026-10-08 затвердив варіант **A** (код-residuals) зараз і **B** (Layer C proof) як наступну хвилю. Засів дозволений ROADMAP §6: audit-backed TOR + явний наказ.

## Principles

1. Закрити лише PARTIAL / residuals з re-audit — не переписувати PLAN-62 DONE-ядро.  
2. Один атомарний PR на рядок; Living Spec + service docs у тому ж циклі.  
3. Lab/CHR/`WriteEnabled` **не** стоп-гейт §3 для wave A.  
4. Wave B (live acceptance) **не** підміняється unit PASS і **не** засівається в цьому PLAN.  
5. Без force-push / skip hooks.

## Out of scope (wave A)

- Live CHR / physical CRS Layer C (→ PLAN-64 / wave B)  
- Campaigns, auto drift repair, auto-create management guard  
- NAT/RAW/Mangle/routing **writes**  
- Broker / microservices / rewrite стека  
- Повторне відкриття DESK-*-FAULT / SNAP-*-CORR correlation wave  

## Ranked remediation tranche (inventory lock)

| Rank | ID | Audit residual | Gap | Queue |
|------|----|----------------|-----|-------|
| 0 | **PLAN63-INV-01** | — | Seed §3.C + land plan | **W7-428 (#1253) DONE** |
| seed | — | — | Advance NEXT to OWN-HB-01 | **W7-429 (#1254) DONE** |
| 1 | **OWN-HB-01** | F01 | Onboarding lock heartbeat job | **W7-430 (#1255) DONE** |
| seed | — | — | Advance NEXT to EVID-LIVE-01 | **W7-431 (#1256) DONE** |
| 2 | **EVID-LIVE-01** | F02 | Standalone live Recheck (ROS reads) | **W7-432 (#1257) DONE** |
| seed | — | — | Advance NEXT to M7-PRES-01 | **W7-433 (#1258) DONE** |
| 3 | **M7-PRES-01** | F13 | OpenEndpointPresence production caller | **W7-434 (#1259) DONE** |
| seed | — | — | Advance NEXT to CAP-IDEM-01 | **W7-435 (#1260) DONE** |
| 4 | **CAP-IDEM-01** | F09 | Idempotency unique + TargetId | **W7-436 (#1261) DONE** |
| seed | — | — | Advance NEXT to PLAN63-DONE-01 | **W7-437 (#1263) DONE** |
| 5 | **PLAN63-DONE-01** | — | COMPLETE; handoff note wave B | **W7-438 (#1264) DONE** |

## DoD per implement row

- Production wiring + behavioral / Living Spec tests у venv  
- Update `known-limitations.md`, `CHANGELOG`, `ISSUES.md`, `docs/development/testing.md`  
- Seed-рядок у тому ж або наступному атомарному PR за чергою  

## Dual track

Product §3 never waits on GNS3. Wave B (Layer C) is a separate acceptance tranche after A COMPLETE.

## §3.C NEXT

**§3.C NEXT = none** — PLAN-63 COMPLETE (W7-438 DONE). Wave B / PLAN-64 requires explicit operator TOR seed; `/autopilot` stops (черга вичерпана).
