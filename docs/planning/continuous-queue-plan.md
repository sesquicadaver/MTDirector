# Continuous product queue (PLAN-02)

**Date:** 2026-08-31  
**Baseline:** `main` @ `877a529` (W2.2 Routing assurance next-hop/subject fields, [#338](https://github.com/sesquicadaver/MTDirector/pull/338))  
**PLAN issue:** [PLAN-02 #339](https://github.com/sesquicadaver/MTDirector/issues/339)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  
**PLAN-03 (quality gates):** [`plan-03-quality-gates.md`](plan-03-quality-gates.md)  
**PLAN-04 (contract tests):** [`plan-04-contract-tests.md`](plan-04-contract-tests.md)  
**PLAN-05 (Desktop operator-surface):** [`plan-05-desktop-operator-surface.md`](plan-05-desktop-operator-surface.md) **COMPLETE**  
**PLAN-06 (Incident Desktop operator-surface):** [`plan-06-incident-desktop-operator-surface.md`](plan-06-incident-desktop-operator-surface.md) **COMPLETE**  
**PLAN-07 (Core MVP Desktop operator-surface):** [`plan-07-core-mvp-desktop-operator-surface.md`](plan-07-core-mvp-desktop-operator-surface.md) **COMPLETE**
**PLAN-08 (Desktop secondary operator-surface):** [`plan-08-desktop-secondary-operator-surface.md`](plan-08-desktop-secondary-operator-surface.md) **COMPLETE**  
**PLAN-09 (Desktop connection-status operator-surface):** [`plan-09-desktop-connection-status-operator-surface.md`](plan-09-desktop-connection-status-operator-surface.md) **COMPLETE**  
**PLAN-10 (Desktop shell chrome & Policies authoring depth) COMPLETE:** [`plan-10-desktop-shell-policies-authoring-depth.md`](plan-10-desktop-shell-policies-authoring-depth.md)  
**PLAN-11 (Desktop Policies review-compose lifecycle) COMPLETE:** [`plan-11-desktop-policies-review-compose-lifecycle.md`](plan-11-desktop-policies-review-compose-lifecycle.md)  
**PLAN-12 (Desktop Policies residual lifecycle) COMPLETE:** [`plan-12-desktop-policies-residual-lifecycle.md`](plan-12-desktop-policies-residual-lifecycle.md)  
**PLAN-13 (Desktop layout density) COMPLETE:** [`plan-13-desktop-layout-density.md`](plan-13-desktop-layout-density.md)
**PLAN-14 (Desktop Avalonia PlaceholderText / Incident surface) COMPLETE:** [`plan-14-desktop-avalonia-placeholder-incident-surface.md`](plan-14-desktop-avalonia-placeholder-incident-surface.md)
**PLAN-15 (Desktop Incident mfc-field style hygiene) COMPLETE:** [`plan-15-desktop-incident-mfc-field-style-hygiene.md`](plan-15-desktop-incident-mfc-field-style-hygiene.md)
**PLAN-16 (Desktop Incident AutomationProperties accessible-name) COMPLETE:** [`plan-16-desktop-incident-automation-properties.md`](plan-16-desktop-incident-automation-properties.md)
**PLAN-17 (Desktop Incident bind-action AutomationProperties) COMPLETE:** [`plan-17-desktop-incident-bind-action-automation.md`](plan-17-desktop-incident-bind-action-automation.md)
**PLAN-18 (Desktop Incident ingest-action AutomationProperties) COMPLETE:** [`plan-18-desktop-incident-ingest-action-automation.md`](plan-18-desktop-incident-ingest-action-automation.md)
**PLAN-19 (Desktop shell Connect/Disconnect AutomationProperties) COMPLETE:** [`plan-19-desktop-shell-connect-disconnect-automation.md`](plan-19-desktop-shell-connect-disconnect-automation.md)
**PLAN-20 (Desktop Policies lifecycle-action AutomationProperties) COMPLETE:** [`plan-20-desktop-policies-lifecycle-action-automation.md`](plan-20-desktop-policies-lifecycle-action-automation.md)
**PLAN-21 (Desktop Policies authoring residual AutomationProperties) COMPLETE:** [`plan-21-desktop-policies-authoring-residual-automation.md`](plan-21-desktop-policies-authoring-residual-automation.md)
**PLAN-22 (Desktop Policies acknowledge/record-analysis AutomationProperties) COMPLETE:** [`plan-22-desktop-policies-ack-record-automation.md`](plan-22-desktop-policies-ack-record-automation.md)
**PLAN-23 (Desktop Policies catalog/object AutomationProperties) COMPLETE:** [`plan-23-desktop-policies-catalog-object-automation.md`](plan-23-desktop-policies-catalog-object-automation.md)
**PLAN-24 (Desktop Onboarding/Deployment AutomationProperties) COMPLETE:** [`plan-24-desktop-onboarding-deployment-automation.md`](plan-24-desktop-onboarding-deployment-automation.md)
**PLAN-25 (Desktop Inventory / Zones / Add-router AutomationProperties):** [`plan-25-desktop-inventory-zones-add-router-automation.md`](plan-25-desktop-inventory-zones-add-router-automation.md) **COMPLETE** (W7-209)  
**PLAN-26 (Code-audit remediation `11cb746`) COMPLETE:** [`plan-26-code-audit-remediation-11cb746.md`](plan-26-code-audit-remediation-11cb746.md) ranks 1…14 **DONE**; seed **W7-237 DONE**; audit [`docs/audits/MTDirector-audit-11cb746-20260911.md`](../audits/MTDirector-audit-11cb746-20260911.md)
**PLAN-27 (Desktop Snapshot/Node/Drift/Audit AutomationProperties residual) COMPLETE:** [`plan-27-desktop-snapshot-panel-automation.md`](plan-27-desktop-snapshot-panel-automation.md) ranks 1…2 **DONE**; seed **W7-237 DONE**; inventory **W7-238 DONE**; seed **W7-239 DONE**; implement **W7-240 DONE**; seed **W7-241 DONE**; implement **W7-242 DONE**; seed **W7-243 DONE**
**PLAN-28 (Desktop residual field/control AutomationProperties) COMPLETE:** [`plan-28-desktop-residual-field-control-automation.md`](plan-28-desktop-residual-field-control-automation.md) ranks 1…2 **DONE**; seed **W7-249 DONE**; inventory **W7-244 DONE**; seed **W7-245 DONE**; implement **W7-246 DONE**; seed **W7-247 DONE**; implement **W7-248 DONE**
**PLAN-29 (Desktop connection health / reconnect after Controller stop) COMPLETE:** [`plan-29-desktop-connection-health-reconnect.md`](plan-29-desktop-connection-health-reconnect.md) ranks 1…2 **DONE**; seed **W7-255 DONE**; inventory **W7-250 DONE**; seed **W7-251 DONE**; implement **W7-252 DONE**; seed **W7-253 DONE**; implement **W7-254 (#915) DONE**
**PLAN-30 (Watch operation-owner ACL / hub slow-subscriber backpressure) COMPLETE:** [`plan-30-watch-owner-acl-hub-backpressure.md`](plan-30-watch-owner-acl-hub-backpressure.md) ranks 1…2 **DONE**; seed **W7-261 DONE**; inventory **W7-256 DONE**; seed **W7-257 DONE**; implement **W7-258 DONE**; seed **W7-259 DONE**; implement **W7-260 DONE**
**PLAN-31 (Desktop residual ListBox / Drift–Audit read-only a11y) COMPLETE:** [`plan-31-desktop-residual-listbox-readonly-a11y.md`](plan-31-desktop-residual-listbox-readonly-a11y.md) ranks 1…2 **DONE**; inventory **W7-262 (#931) DONE**; seed **W7-263 (#932) DONE**; implement **W7-264 (#934) DONE**; seed **W7-265 (#935) DONE**; implement **W7-266 (#939) DONE**; seed **W7-267 (#940) DONE**
**PLAN-32 (Controller host-process packaging templates) COMPLETE:** [`plan-32-controller-host-process-packaging.md`](plan-32-controller-host-process-packaging.md) ranks 1…2 **DONE**; inventory **W7-268 (#943) DONE**; seed **W7-269 (#944) DONE**; implement **W7-270 (#946) DONE**; seed **W7-271 (#947) DONE**; implement **W7-272 (#951) DONE**; seed **W7-273 (#952) DONE**
**PLAN-33 (Desktop Inventory TreeView / residual TabControl a11y) COMPLETE:** [`plan-33-desktop-inventory-treeview-a11y.md`](plan-33-desktop-inventory-treeview-a11y.md) sole rank **DESK-A11Y-TREE-01 DONE**; TAB-01 dropped; inventory **W7-274 DONE**; seed **W7-275 DONE**; implement **W7-276 DONE**; COMPLETE seed **W7-277 DONE**; predecessor seed **W7-273 DONE**
**PLAN-34 (Desktop operator launch packaging templates) COMPLETE:** [`plan-34-desktop-operator-launch-packaging.md`](plan-34-desktop-operator-launch-packaging.md) ranks 1…2 **DONE**; inventory **W7-278 (#963) DONE**; seed **W7-279 (#964) DONE**; implement **W7-280 (#966) DONE**; WIN seed **W7-281 (#967) DONE**; WIN implement **W7-282 (#971) DONE**; COMPLETE seed **W7-283 (#972) DONE**
**PLAN-35 (Desktop launch-template publish bundling):** [`plan-35-desktop-launch-template-publish-bundling.md`](plan-35-desktop-launch-template-publish-bundling.md) **COMPLETE** (inventory **W7-284 (#975) DONE**; seed **W7-285 (#976) DONE**; implement **W7-286 (#978) DONE**; COMPLETE **W7-287 (#979) DONE**)

**PLAN-36 (Controller host-template publish bundling):** [`plan-36-controller-host-template-publish-bundling.md`](plan-36-controller-host-template-publish-bundling.md) **COMPLETE** (inventory **W7-288 (#983) DONE**; seed **W7-289 (#984) DONE**; implement **W7-290 (#986) DONE**; COMPLETE **W7-291 (#987) DONE**)

**PLAN-37 (Controller host env sample packaging):** [`plan-37-controller-host-env-sample-packaging.md`](plan-37-controller-host-env-sample-packaging.md) **COMPLETE** (inventory **W7-292 (#991) DONE**; seed **W7-293 (#992) DONE**; implement **W7-294 (#994) DONE**; COMPLETE **W7-295 (#996) DONE**)

**PLAN-38 (Controller host sysusers/tmpfiles packaging):** [`plan-38-controller-host-sysusers-tmpfiles-packaging.md`](plan-38-controller-host-sysusers-tmpfiles-packaging.md) **COMPLETE** (inventory **W7-296 (#999) DONE**; seed **W7-297 (#1000) DONE**; implement **W7-298 (#1002) DONE**; COMPLETE **W7-299 (#1004) DONE**; seeded by **W7-295 DONE** after **PLAN-37 COMPLETE**; seeded by **W7-291 DONE** after **PLAN-36 COMPLETE**)

**PLAN-62 (Repository-audit remediation `acd0759`):** [`plan-62-audit-remediation-acd0759.md`](plan-62-audit-remediation-acd0759.md) **OPEN** — inventory **W7-393 (#1195) DONE**; seed **W7-394 (#1196) DONE**; **AUDIT-STATUS-01 W7-395 (#1197) DONE**; seed **W7-396 (#1199) DONE**; **AUDIT-SBOM-01 W7-397 (#1200) DONE**; seed **W7-398 (#1202) DONE**; **AUDIT-OWN-01 W7-399 (#1203) DONE**; seed **W7-400 (#1205) DONE**; **§3.C NEXT = W7-401 (#1206)** AUDIT-COMMIT-01; audit [`docs/audits/MTDirector-audit-acd0759-20260923.md`](../audits/MTDirector-audit-acd0759-20260923.md). Seeded by explicit operator command after freeze **W7-392** (audit TOR, not correlation-id vanity).

**PLAN-61 (Desktop connection Disconnected RPC fault text):** [`plan-61-desktop-connection-disconnected-fault-text.md`](plan-61-desktop-connection-disconnected-fault-text.md) **COMPLETE** (inventory **W7-388 (#1183) DONE**; seed **W7-389 (#1184) DONE**; implement **DESK-CONN-DISC-01 W7-390 (#1186) DONE**; COMPLETE **W7-391 (#1187) DONE**). Operator fault-correlation wave **PLAN-52…61 CLOSED**. Freeze **W7-392 (#1191) DONE**. Successor **PLAN-62** seeded from audit TOR. Not seeded from freeze: another DESK-*-FAULT, SNAP-*-CORR, Onboarding/Deployment ErrorCode proto, a11y vanity, MSI, Type=notify. Seeded by **W7-387 (#1179) DONE** after **PLAN-60 COMPLETE**
**PLAN-60 (Desktop service RPC fault text):** [`plan-60-desktop-service-rpc-fault-text.md`](plan-60-desktop-service-rpc-fault-text.md) **COMPLETE** (inventory **W7-384 (#1175) DONE**; seed **W7-385 (#1176) DONE**; implement **DESK-SVC-FAULT-01 W7-386 (#1178) DONE**; COMPLETE **W7-387 (#1179) DONE**; successor **PLAN-61 COMPLETE**; seeded by **W7-383 (#1171) DONE** after **PLAN-59 COMPLETE**)
**PLAN-59 (Snapshot failed-stage ErrorText correlation):** [`plan-59-snapshot-failed-errortext-correlation.md`](plan-59-snapshot-failed-errortext-correlation.md) **COMPLETE** (inventory **W7-380 (#1167) DONE**; seed **W7-381 (#1168) DONE**; implement **SNAP-ERRTEXT-CORR-01 W7-382 (#1170) DONE**; COMPLETE **W7-383 (#1171) DONE**; successor **PLAN-60** inventory **W7-384 DONE**; seed **W7-385 DONE**; implement **W7-386 DONE**; seeded by **W7-379 (#1163) DONE** after **PLAN-58 COMPLETE**)
**PLAN-58 (Desktop panel status fault text):** [`plan-58-desktop-panel-status-fault-text.md`](plan-58-desktop-panel-status-fault-text.md) **COMPLETE** (inventory **W7-376 (#1159) DONE**; seed **W7-377 (#1160) DONE**; implement **DESK-PANEL-FAULT-01 W7-378 (#1162) DONE**; COMPLETE **W7-379 (#1163) DONE**; successor **PLAN-59** inventory **W7-380 DONE**; seed **W7-381 OPEN**; seeded by **W7-375 (#1155) DONE** after **PLAN-57 COMPLETE**)
**PLAN-57 (VRRP capture-progress fault text):** [`plan-57-vrrp-capture-progress-fault-text.md`](plan-57-vrrp-capture-progress-fault-text.md) **COMPLETE** (inventory **W7-372 (#1151) DONE**; seed **W7-373 (#1152) DONE**; implement **DESK-VRRP-PROG-01 W7-374 (#1154) DONE**; COMPLETE **W7-375 (#1155) DONE**; successor **PLAN-58** inventory **W7-376 DONE**; seed **W7-377 OPEN**; seeded by **W7-371 (#1147) DONE** after **PLAN-56 COMPLETE**)
**PLAN-56 (VRRP pair status fault text):** [`plan-56-vrrp-pair-status-fault-text.md`](plan-56-vrrp-pair-status-fault-text.md) **COMPLETE** (inventory **W7-368 (#1143) DONE**; seed **W7-369 (#1144) DONE**; implement **DESK-VRRP-FAULT-01 W7-370 (#1146) DONE**; COMPLETE **W7-371 (#1147) DONE**; successor **PLAN-57** inventory **W7-372 OPEN**; seeded by **W7-367 (#1139) DONE** after **PLAN-55 COMPLETE**)
**PLAN-55 (Capture progress fault correlation):** [`plan-55-capture-progress-fault-correlation.md`](plan-55-capture-progress-fault-correlation.md) **COMPLETE** (inventory **W7-364 (#1135) DONE**; seed **W7-365 (#1136) DONE**; implement **SNAP-FAULT-CORR-01 W7-366 (#1138) DONE**; COMPLETE **W7-367 (#1139) DONE**; successor **PLAN-56** inventory **W7-368 OPEN**; seeded by **W7-363 (#1131) DONE** after **PLAN-54 COMPLETE**)
**PLAN-54 (Desktop connection-status fault text):** [`plan-54-desktop-connection-status-fault-text.md`](plan-54-desktop-connection-status-fault-text.md) **COMPLETE** (inventory **W7-360 (#1127) DONE**; seed **W7-361 (#1128) DONE**; implement **DESK-CONN-FAULT-01 W7-362 (#1130) DONE**; COMPLETE **W7-363 (#1131) DONE**; successor **PLAN-55** inventory **W7-364 OPEN**; seeded by **W7-359 (#1123) DONE** after **PLAN-53 COMPLETE**)
**PLAN-53 (Controller fault-correlation logging):** [`plan-53-controller-fault-correlation-log.md`](plan-53-controller-fault-correlation-log.md) **COMPLETE** (inventory **W7-356 (#1119) DONE**; seed **W7-357 (#1120) DONE**; implement **CTRL-ERRDETAIL-LOG-01 W7-358 (#1122) DONE**; COMPLETE **W7-359 (#1123) DONE**; successor **PLAN-54** inventory **W7-360 OPEN**; seeded by **W7-355 DONE** after **PLAN-52 COMPLETE**)
**PLAN-52 (Desktop gRPC ErrorDetail operator mapping):** [`plan-52-desktop-grpc-error-detail.md`](plan-52-desktop-grpc-error-detail.md) **COMPLETE** (inventory **W7-352 (#1111) DONE**; seed **W7-353 (#1112) DONE**; implement **DESK-RPC-FAULT-01 W7-354 (#1114) DONE**; COMPLETE **W7-355 (#1115) DONE**; successor **PLAN-53** inventory **W7-356 DONE**; seed **W7-357 DONE**; implement **W7-358 DONE**; COMPLETE **W7-359 OPEN**; seeded by **W7-351 DONE** after **PLAN-51 COMPLETE**)
**PLAN-51 (Desktop gRPC unary call deadline / timeout):** [`plan-51-desktop-grpc-unary-deadline.md`](plan-51-desktop-grpc-unary-deadline.md) **COMPLETE** (inventory **W7-348 (#1103) DONE**; seed **W7-349 (#1104) DONE**; implement **DESK-GRPC-DEADLINE-01 W7-350 (#1106) DONE**; COMPLETE **W7-351 (#1107) DONE**; successor **PLAN-52** inventory **W7-352 DONE**; seed **W7-353 DONE**; implement **W7-354 DONE**; seeded by **W7-347 DONE** after **PLAN-50 COMPLETE**)
**PLAN-50 (Controller Kestrel min request/response data-rate):** [`plan-50-controller-kestrel-min-data-rate.md`](plan-50-controller-kestrel-min-data-rate.md) **COMPLETE** (inventory **W7-344 (#1095) DONE**; seed **W7-345 (#1096) DONE**; implement **CTRL-KESTREL-MINRATE-01 W7-346 (#1098) DONE**; COMPLETE **W7-347 (#1099) DONE**; successor **PLAN-51** inventory **W7-348 DONE**; seed **W7-349 DONE**; seeded by **W7-343 DONE** after **PLAN-49 COMPLETE**
**PLAN-49 (Controller/Desktop gRPC HTTP/2 keepalive):** [`plan-49-controller-grpc-http2-keepalive.md`](plan-49-controller-grpc-http2-keepalive.md) **COMPLETE** (inventory **W7-340 (#1087) DONE**; seed **W7-341 (#1088) DONE**; implement **CTRL-GRPC-KEEPALIVE-01 W7-342 (#1090) DONE**; COMPLETE **W7-343 (#1092) DONE**; successor **PLAN-50** inventory **W7-344 OPEN**; seeded by **W7-339 DONE** after **PLAN-48 COMPLETE**
**PLAN-48 (Controller Kestrel request-body / HTTP2 limits):** [`plan-48-controller-kestrel-request-body-limits.md`](plan-48-controller-kestrel-request-body-limits.md) **COMPLETE** (inventory **W7-336 (#1079) DONE**; seed **W7-337 (#1080) DONE**; implement **CTRL-KESTREL-BODY-01 W7-338 (#1082) DONE**; COMPLETE **W7-339 (#1084) DONE**; successor **PLAN-49** inventory **W7-340 DONE**; seed **W7-341 DONE**; implement **W7-342 DONE**; seeded by **W7-335 DONE** after **PLAN-47 COMPLETE**
**PLAN-47 (Controller gRPC message-size / transport limits):** [`plan-47-controller-grpc-message-size-limits.md`](plan-47-controller-grpc-message-size-limits.md) **COMPLETE** (inventory **W7-332 (#1071) DONE**; seed **W7-333 (#1072) DONE**; implement **CTRL-GRPC-MSGSIZE-01 W7-334 (#1074) DONE**; COMPLETE **W7-335 (#1076) DONE**; successor **PLAN-48** inventory **W7-336 DONE**; seed **W7-337 DONE**; seeded by **W7-331 DONE** after **PLAN-46 COMPLETE**
**PLAN-46 (Controller OpenTelemetry resource identity):** [`plan-46-controller-otel-resource-identity.md`](plan-46-controller-otel-resource-identity.md) inventory **W7-328 (#1063) DONE**; seed **W7-329 (#1064) DONE**; implement **W7-330 (#1066) DONE**; COMPLETE **W7-331 (#1068) DONE**; successor **PLAN-47 COMPLETE**; PLAN-48 inventory **W7-336 DONE**; seeded by **W7-327 DONE** after **PLAN-45 COMPLETE**
**PLAN-45 (Controller log↔trace correlation):** [`plan-45-controller-log-trace-correlation.md`](plan-45-controller-log-trace-correlation.md) inventory **W7-324 (#1055) DONE**; seed **W7-325 (#1056) DONE**; implement **W7-326 (#1058) DONE**; COMPLETE **W7-327 (#1060) DONE**; successor **PLAN-46** inventory **W7-328 DONE**; seed **W7-329 OPEN**; seeded by **W7-323 DONE** after **PLAN-44 COMPLETE**
**PLAN-44 (Controller OpenTelemetry tracing):** [`plan-44-controller-otel-tracing.md`](plan-44-controller-otel-tracing.md) inventory **W7-320 (#1047) DONE**; seed **W7-321 (#1048) DONE**; implement **W7-322 (#1050) DONE**; COMPLETE **W7-323 (#1052) DONE**; successor **PLAN-45 COMPLETE**; seeded by **W7-319 DONE** after **PLAN-43 COMPLETE**
**PLAN-43 (Controller metrics/OpenTelemetry):** [`plan-43-controller-http-metrics-otel.md`](plan-43-controller-http-metrics-otel.md) inventory **W7-316 (#1039) DONE**; seed **W7-317 (#1040) DONE**; implement **W7-318 (#1042) DONE**; COMPLETE **W7-319 (#1044) DONE**; successor **PLAN-44** inventory **W7-320 DONE**; seeded by **W7-315 DONE** after **PLAN-42 COMPLETE**
**PLAN-42 (Controller HTTP health probes):** [`plan-42-controller-http-health-probes.md`](plan-42-controller-http-health-probes.md) inventory **W7-312 (#1031) DONE**; seed **W7-313 (#1032) DONE**; implement **W7-314 (#1034) DONE**; COMPLETE **W7-315 (#1036) DONE**; successor **PLAN-43** inventory **W7-316 DONE**; seeded by **W7-311 DONE** after **PLAN-41 COMPLETE**
**PLAN-41 (Release signing crypto GPG/Sigstore):** [`plan-41-release-signing-crypto-gpg-sigstore.md`](plan-41-release-signing-crypto-gpg-sigstore.md) **COMPLETE** (inventory **W7-308 (#1023) DONE**; seed **W7-309 (#1024) DONE**; implement **W7-310 (#1026) DONE**; COMPLETE **W7-311 (#1028) DONE**; seeded by **W7-307 DONE** after **PLAN-40 COMPLETE**)
**PLAN-40 (Controller host journald/syslog identity):** [`plan-40-controller-host-journald-syslog-identity.md`](plan-40-controller-host-journald-syslog-identity.md) inventory **W7-304 (#1015) DONE**; seed **W7-305 (#1016) DONE**; implement **W7-306 (#1018) DONE**; COMPLETE seed **W7-307 (#1020) DONE**; successor **PLAN-41** inventory **W7-308 DONE**; seeded by **W7-303 DONE** after **PLAN-39 COMPLETE**
**PLAN-39 (Controller host operator doc packaging):** [`plan-39-controller-host-operator-doc-packaging.md`](plan-39-controller-host-operator-doc-packaging.md) inventory **W7-300 (#1007) DONE**; seed **W7-301 (#1008) DONE**; implement **W7-302 (#1010) DONE**; COMPLETE seed **W7-303 (#1012) DONE**; successor **PLAN-40** inventory **W7-304 DONE**; seeded by **W7-299 DONE** after **PLAN-38 COMPLETE**

This is the in-repo plan (`.omx/plans/` is gitignored). Historical PLAN-02 replaced an idle **NEXT = none** by requiring a seed. That self-seed is **revoked**: a non-empty NEXT only means the current planned row is open.

## Why the previous queue stopped work

| Stopper | Evidence | Effect |
|---------|----------|--------|
| Empty `ROADMAP.md` §3 | `NEXT = none` after P2-11 + post-queue UX | `/autopilot` reports and **idles** |
| W5 labelled “PLAN only” | [`desktop-ui-backend-alignment.md`](../development/desktop-ui-backend-alignment.md) | P3 work waits for a PLAN that was never opened |
| Lab phase gates (GNS3 / CHR / `WriteEnabled`) | `~/gns3-lab` (outside git); [`known-limitations.md`](../release/known-limitations.md) live CHR **OFF** | Operators treat phase N as a product stop — **not a MUST in this repo** |
| ROADMAP §6 “no skip predecessors” | Correct **inside** the product line | Misread as “wait for lab phase close” |

**Rule (queue exhaustion; supersedes PLAN-02 self-seed):** a non-empty NEXT only means the current planned §3 row is still open. After that row closes, if the next NEXT is empty, `/autopilot` **stops** and reports that the queue is exhausted. Inventing the next tranche, PLAN, or TOR to keep NEXT non-empty is forbidden. Lab/CHR/`WriteEnabled` run **in parallel** and are **never** predecessors of Desktop/Contracts PRs.

## Readiness (evidence vs inference)

### Code / normative milestones (evidence)

| Layer | Status | Notes |
|-------|--------|-------|
| MVP M0–M6 + N1 | **100% CLOSED** | 109/109 |
| Post-MVP M7.1–M7.4 | **100% CLOSED** | `v0.2.0` (2026-08-24) |
| P2 read P2-04…P2-06 | **CLOSED** | `Mfc:RouterOs:Enabled` fail-closed default |
| P2 write P2-07…P2-11 | **CLOSED** | `WriteEnabled` fail-closed default — **do not** flip to wake UI |
| Desktop alignment W1.1–W4.4, W2.1–W2.2 | **DONE** | P0–P2 UI ↔ existing RPC |
| Product issues in `ISSUES.md` §2.2 | **139 DONE** | Tracker aligned TRACKER-01 |

### Operational / lab (evidence)

| Item | Status |
|------|--------|
| Live CHR / physical CRS in CI | **OFF** (scripted Living Specs are DoD) |
| Default `WriteEnabled` | **false** (correct fail-closed) |
| GNS3 lab (`~/gns3-lab`) | Outside this git repo; day-2 / write unlock is **ops**, not §3 |

### Remaining product glue (evidence — not W5)

| Gap | Where | Queue ID |
|-----|-------|----------|
| Rollback Start without Watch | `DeploymentViewModel.RollbackAsync` vs `StartAndWatchAsync` | **CONT-01 DONE** |
| Neighbor apply ignores VRRP member b | `AddRouterWizardViewModel.ApplyNeighborCandidate` | **CONT-02 DONE** |
| Onboarding Rollback without Watch / hub stops at Committed | `OnboardingViewModel` + `OnboardingProgressHub` | **W6-04 DONE** |
| GetNode Reachability always Unknown | `ViewMapper` / DiscoverDevice LastSupportState | **W6-05 DONE** |
| Policies Diff flattened to SummaryLine only | `PolicyPanelService.DiffAsync` / DiffLines | **W6-06 DONE** |
| Diff baseline UUID paste ritual | `PoliciesViewModel.DiffBaselineRevisionIdText` | **W6-07 DONE** |
| Unreachable lost on Controller restart | `IDeviceReachabilityObservationStore` (process-local) | **W6-08 DONE** |
| ReorderRules only via UUID paste | `PoliciesViewModel.ReorderRuleIdsText` | **W6-09 DONE** |
| System actor spoofable via `x-mfc-actor` | `SystemActorAuthorizationBoundary` + gRPC ResolveActor | **SEC-01 DONE** |
| AnchorOnly empty deploy materializer in production | `AnchorOnlyDeploymentArtifactMaterializer` | **SEC-02 DONE** |
| Audit hash uses predecessor length only | `EfAuditEventWriter` | **SEC-03 DONE** |
| INTERNAL_CA empty CA store + RevocationMode.NoCheck | `IRouterOsTrustedCaStore` / `ApiSslCertificateValidator` | **SEC-04 DONE** |
| Non-atomic mutation vs idempotency/audit | write path | **SEC-05 DONE** |
| No Incident gRPC surface | Contracts / Controller | **SEC-06 DONE** |
| Partial UoW on mutations | Application write paths | **SEC-07 DONE** |
| Deploy/profile UoW residual | Application write paths | **SEC-08 DONE** |
| Onboarding UoW residual | Application write paths | **SEC-09 DONE** |
| Incident overlay expire UoW | Application write paths | **SEC-10 DONE** |
| Drift detect + response-feedback UoW | Application write paths | **SEC-11 DONE** |
| CaptureSnapshot persist+audit UoW | Application write paths | **SEC-12 DONE** |
| Device hash-state upsert UoW | Application write paths | **SEC-13 DONE** |
| Endpoint presence multi-store UoW | Application write paths | **SEC-14 DONE** |
| Routing assurance state upsert UoW | Application write paths | **SEC-15 DONE** |
| Desktop MikroTik/Winbox display labels | Desktop presentation | **W7-01 DONE** |
| gRPC actor ↔ authenticated principal | Controller authn | **W7-02 DONE** |
| Controller Kestrel mTLS client certificates | Controller TLS | **W7-03 DONE** |
| Validate mTLS client certs against TrustedCa | Controller TLS | **W7-04 DONE** |
| Bind Desktop actor from client cert CN | Desktop authn | **W7-05 DONE** |
| Map mTLS client cert to HttpContext.User | Controller authn | **W7-06 DONE** |
| Prefer HttpContext.User over gRPC peer identity | Controller authn | **W7-07 DONE** |
| Desktop status shows resolved mTLS actor | Desktop UX | **W7-08 DONE** |
| Production mTLS operator checklist | Ops docs | **W7-09 DONE** |
| Log redacted client-cert thumbprint on mTLS principal map | Controller observability | **W7-10 DONE** |
| Harden SessionFaultInjection timeout pending-clear | RouterOS session tests | **W7-11 DONE** |
| Desktop AuthenticationFailed status Living Spec | Desktop UX tests | **W7-12 DONE** |
| mTLS map log includes request TraceIdentifier | Controller observability | **W7-13 DONE** |
| SECURITY.md documents TraceIdentifier on mTLS map log | Security docs | **W7-14 DONE** |
| Pilot runbook TraceIdentifier for mTLS Connect correlation | Ops docs | **W7-15 DONE** |
| controller-configuration.md TraceIdentifier cross-link | Ops docs | **W7-16 DONE** |
| Lock resolve-only zone UoW residual Living Spec | Docs / SEC residual | **W7-17 DONE** |
| Lock Start* AddOperationAsync outside-UoW residual Living Spec | Docs / SEC residual | **W7-18 DONE** |
| Lock orchestrator-only audit residual Living Spec | Docs / SEC residual | **W7-19 DONE** |
| Lock Feedback delivery / RouterOS capture outside-DB residual Living Spec | Docs / SEC residual | **W7-20 DONE** |
| Lock IResponseFeedbackDeliveryPort not-configured residual Living Spec | Docs / SEC residual | **W7-21 DONE** |
| Lock Desktop zip/tar installer packaging residual Living Spec | Docs / packaging residual | **W7-22 DONE** |
| Lock SHA256SUMS attestation packaging residual Living Spec | Docs / packaging residual | **W7-23 DONE** |
| Lock CycloneDX-lite SBOM packaging residual Living Spec | Docs / packaging residual | **W7-24 DONE** |
| Lock MVP scope-lock no-NAT/RAW writes residual Living Spec | Docs / scope residual | **W7-25 DONE** |
| Lock MVP scope-lock no-campaigns/auto-deploy residual Living Spec | Docs / scope residual | **W7-26 DONE** |
| Lock Live CHR matrix OFF residual Living Spec | Docs / lab residual | **W7-27 DONE** |
| Lock Golden live CHR hashes env-gated residual Living Spec | Docs / lab residual | **W7-28 DONE** |
| Lock Live physical CRS hardware OFF ops residual Living Spec | Docs / lab residual | **W7-29 DONE** |
| Lock Controller migrate-only startup residual Living Spec | Docs / ops residual | **W7-30 DONE** |
| Lock Development master-key provider residual Living Spec | Docs / ops residual | **W7-31 DONE** |
| Lock GitHub-hosted CI billing-limited residual Living Spec | Docs / ops residual | **W7-32 DONE** |
| Seed next continuous residual after CI billing Living Spec | Docs / queue seed | **W7-33 DONE** |
| Lock Desktop window does not stop Controller residual Living Spec | Docs / Desktop residual | **W7-34 DONE** |
| Lock Inventory Add router wizard DONE residual Living Spec | Docs / Desktop residual | **W7-35 DONE** |
| Seed next continuous residual after Inventory Add router Living Spec | Docs / queue seed | **W7-36 DONE** |
| Lock gRPC remains available for automation residual Living Spec | Docs / Desktop residual | **W7-37 DONE** |
| Seed next continuous residual after gRPC automation Living Spec | Docs / queue seed | **W7-38 DONE** |
| Lock RouterOs Enabled default fail-closed residual Living Spec | Docs / P2 residual | **W7-39 DONE** |
| Seed next continuous residual after RouterOs fail-closed Living Spec | Docs / queue seed | **W7-40 DONE** |
| Lock RouterOs WriteEnabled operator residual Living Spec | Docs / P2 residual | **W7-41 DONE** |
| Seed next continuous residual after WriteEnabled Living Spec | Docs / queue seed | **W7-42 DONE** |
| Lock N1-07 path-class E2E DONE residual Living Spec | Docs / MVP residual | **W7-43 DONE** |
| Seed next continuous residual after N1-07 Living Spec | Docs / queue seed | **W7-44 DONE** |
| Lock M7.1…M7.4 CLOSED residual Living Spec | Docs / M7 residual | **W7-45 DONE** |
| Seed next continuous residual after M7 CLOSED Living Spec | Docs / queue seed | **W7-46 DONE** |
| Lock SEC-07…SEC-15 DONE residual Living Spec | Docs / SEC residual | **W7-47 DONE** |
| Seed next continuous residual after SEC-07…15 Living Spec | Docs / queue seed | **W7-48 DONE** |
| Lock known-limitations residual Living Spec corpus COMPLETE | Docs / residual corpus | **W7-49 DONE** |
| Seed next product tranche after residual corpus Living Spec | Docs / product seed | **W7-50 DONE** |
| PLAN-03 — Inventory next quality-gate product tranche | Docs / PLAN-03 | **W7-51 DONE** |
| QG-IMPORT-01 — Import graph + cycle anomaly Living Spec gate | Docs / quality gate | **W7-52 DONE** |
| QG-DOCS-01 — Weekly docs smoke Living Spec gate | Docs / quality gate | **W7-53 DONE** |
| QG-ANTISTUB-01 — Anti-stub CI Living Spec gate | Docs / quality gate | **W7-54 DONE** |
| QG-LIVESPEC-MATRIX-01 — Living Spec matrix PR gate | Docs / quality gate | **W7-55 DONE** |
| QG-SIGN-01 — Release signing checklist Living Spec gate | Docs / quality gate | **W7-56 DONE** |
| Seed next product tranche after PLAN-03 quality gates | Docs / product seed | **W7-57 DONE** |
| PLAN-04 — Inventory next contract-test / API Living Spec product tranche | Docs / PLAN-04 | **W7-58 DONE** |
| CT-DEPLOY-01 — DeploymentService GrpcHost contract Living Spec | Docs / contract test | **W7-59 DONE** |
| CT-ZONE-01 — ZoneService GrpcHost contract Living Spec | Docs / contract test | **W7-60 DONE** |
| CT-DRIFT-01 — DriftService GrpcHost contract Living Spec | Docs / contract test | **W7-61 DONE** |
| CT-AUDIT-01 — AuditService GrpcHost contract Living Spec | Docs / contract test | **W7-62 DONE** |
| CT-ROUTING-01 — RoutingAssuranceService ProtoContract + GrpcHost Living Spec | Docs / contract test | **W7-63 DONE** |
| CT-INCIDENT-01 — IncidentService ProtoContract + GrpcHost Living Spec | Docs / contract test | **W7-64 DONE** |
| Seed next product tranche after PLAN-04 → PLAN-05 Desktop operator-surface | Docs / product seed | **W7-65 DONE** |
| PLAN-05 — Inventory next Desktop operator-surface Living Spec product tranche | Docs / PLAN-05 | **W7-66 DONE** |
| DESK-AUDIT-01 — Desktop Audit panel Living Spec vs AuditGrpcHost | Docs / Desktop Living Spec | **W7-67 DONE** |
| DESK-DRIFT-01 — Desktop Drift panel Living Spec vs DriftGrpcHost | Docs / Desktop Living Spec | **W7-68 DONE** |
| DESK-ZONE-01 — Desktop Zones panel Living Spec vs ZoneGrpcHost | Docs / Desktop Living Spec | **W7-69 DONE** |
| DESK-ROUTING-01 — Desktop Routing assurance Living Spec vs RoutingAssuranceGrpcHost | Docs / Desktop Living Spec | **W7-70 DONE** |
| Seed next product tranche after PLAN-05 Desktop operator-surface | Docs / product seed | **W7-71 DONE** |
| PLAN-06 — Inventory next Incident Desktop operator-surface Living Spec product tranche | Docs / PLAN-06 | **W7-72 DONE** |
| DESK-INCIDENT-01 — Desktop Incident ViewModel + client Living Spec vs IncidentGrpcHost | Docs / Desktop Living Spec | **W7-73 DONE** |
| DESK-INCIDENT-02 — Desktop Incident MainWindow panel Living Spec | Docs / Desktop Living Spec | **W7-74 DONE** |
| DESK-INCIDENT-03 — Desktop Incident Bind assessment Living Spec | Docs / Desktop Living Spec | **W7-75 DONE** |
| DESK-INCIDENT-04 — Desktop Incident fail-closed no deploy/overlay Living Spec | Docs / Desktop Living Spec | **W7-76 DONE** |
| Seed next product tranche after PLAN-06 Incident Desktop operator-surface | Docs / product seed | **W7-77 DONE** |
| PLAN-07 — Inventory next Core MVP Desktop operator-surface Living Spec product tranche | Docs / PLAN-07 | **W7-78 DONE** |
| DESK-POLICY-01 — Desktop Policies panel Living Spec vs PolicyGrpcHost | Docs / Desktop Living Spec | **W7-79 DONE** |
| Seed next PLAN-07 row after DESK-POLICY-01 → DESK-DEPLOY-01 | Docs / PLAN-07 | **W7-80 DONE** |
| DESK-DEPLOY-01 — Desktop Deployment Living Spec vs DeploymentGrpcHost | Docs / Desktop Living Spec | **W7-81 DONE** |
| Seed next PLAN-07 row after DESK-DEPLOY-01 → DESK-ONBOARD-01 | Docs / PLAN-07 | **W7-82 DONE** |
| DESK-ONBOARD-01 — Desktop Onboarding Living Spec vs OnboardingGrpcHost | Docs / Desktop Living Spec | **W7-83 DONE** |
| Seed next PLAN-07 row after DESK-ONBOARD-01 → DESK-SNAPSHOT-01 | Docs / PLAN-07 | **W7-84 DONE** |
| DESK-SNAPSHOT-01 — Desktop Snapshot Living Spec vs SnapshotGrpcHost | Docs / Desktop Living Spec | **W7-85 DONE** |
| Seed next PLAN-07 row after DESK-SNAPSHOT-01 → DESK-INVENTORY-01 | Docs / PLAN-07 | **W7-86 DONE** |
| DESK-INVENTORY-01 — Desktop Inventory Living Spec vs InventoryGrpcHost | Docs / Desktop Living Spec | **W7-87 DONE** |
| Seed next product tranche after PLAN-07 Core MVP Desktop | Docs / product seed | **W7-88 DONE** |
| PLAN-08 — Inventory next Desktop secondary operator-surface Living Spec product tranche | Docs / PLAN-08 | **W7-89 DONE** |
| DESK-NODE-01 — Desktop Node VRRP pair Living Spec vs InventoryGrpcHost | Docs / Desktop Living Spec | **W7-90 DONE** |
| Seed next PLAN-08 row after DESK-NODE-01 → DESK-NBR-01 | Docs / PLAN-08 | **W7-91 DONE** |
| DESK-NBR-01 — Desktop Neighbor candidates Living Spec depth | Docs / Desktop Living Spec | **W7-92 DONE** |
| Seed next PLAN-08 row after DESK-NBR-01 → DESK-PROBE-01 | Docs / PLAN-08 | **W7-93 DONE** |
| DESK-PROBE-01 — Desktop ValidateDeviceConnection probe Living Spec depth | Docs / Desktop Living Spec | **W7-94 DONE** |
| Seed next PLAN-08 row after DESK-PROBE-01 → DESK-POLICY-02 | Docs / PLAN-08 | **W7-95 DONE** |
| DESK-POLICY-02 — Desktop Policy safety analysis Living Spec depth | Docs / Desktop Living Spec | **W7-96 DONE** |
| Seed next product tranche after PLAN-08 → PLAN-09 | Docs / product seed | **W7-97 DONE** |
| PLAN-09 — Inventory next Desktop connection-status operator-surface Living Spec product tranche | Docs / PLAN-09 | **W7-98 DONE** |
| DESK-CONN-01 — Desktop Connect/Disconnect Living Spec depth | Docs / Desktop Living Spec | **W7-99 DONE** |
| Seed next PLAN-09 row after DESK-CONN-01 → DESK-MTLS-01 | Docs / PLAN-09 | **W7-100 DONE** |
| DESK-MTLS-01 — Desktop mTLS actor status Living Spec depth | Docs / Desktop Living Spec | **W7-101 DONE** |
| Seed next PLAN-09 row after DESK-MTLS-01 → DESK-AUTH-01 | Docs / PLAN-09 | **W7-102 DONE** |
| DESK-AUTH-01 — Desktop AuthenticationFailed/TlsError Living Spec depth | Docs / Desktop Living Spec | **W7-103 DONE** |
| Seed next product tranche after PLAN-09 → PLAN-10 | Docs / product seed | **W7-104 DONE** |
| PLAN-10 — Inventory next Desktop shell chrome & Policies authoring depth Living Spec product tranche | Docs / PLAN-10 | **W7-105 DONE** |
| DESK-SHELL-01 — Desktop Shell navigation/hotkeys Living Spec depth | Docs / Desktop Living Spec | **W7-106 DONE** |
| Seed next PLAN-10 row after DESK-SHELL-01 → DESK-DIFF-01 | Docs / PLAN-10 | **W7-107 DONE** |
| DESK-DIFF-01 — Desktop Policies Diff Living Spec depth | Docs / Desktop Living Spec | **W7-108 DONE** |
| Seed next PLAN-10 row after DESK-DIFF-01 → DESK-REORDER-01 | Docs / PLAN-10 | **W7-109 DONE** |
| DESK-REORDER-01 — Desktop Policies Move up/down Living Spec depth | Docs / Desktop Living Spec | **W7-110 DONE** |
| Seed next product tranche after PLAN-10 → PLAN-11 | Docs / product seed | **W7-111 DONE** |
| PLAN-11 — Inventory next Desktop Policies review-compose lifecycle Living Spec product tranche | Docs / PLAN-11 | **W7-112 DONE** |
| DESK-SUBMIT-01 — Desktop Policies SubmitForReview Living Spec depth | Docs / Desktop Living Spec | **W7-114 DONE** |
| Seed next PLAN-11 row after DESK-SUBMIT-01 → DESK-COMPOSE-01 | Docs / PLAN-11 | **W7-113 DONE** |
| DESK-COMPOSE-01 — Desktop Policies Compose+RecordAnalysis Living Spec depth | Docs / Desktop Living Spec | **W7-115 DONE** |
| Seed next PLAN-11 row after DESK-COMPOSE-01 → DESK-GATE-01 | Docs / PLAN-11 | **W7-117 DONE** |
| DESK-GATE-01 — Desktop Policies Approve/Bind/Compile Living Spec depth | Docs / Desktop Living Spec | **W7-116 DONE** |
| Seed next product tranche after PLAN-11 → PLAN-12 | Docs / product seed | **W7-118 DONE** |
| PLAN-12 — Inventory next Desktop Policies residual lifecycle Living Spec product tranche | Docs / PLAN-12 | **W7-119 DONE** |
| DESK-ACK-01 — Desktop Policies AcknowledgeWarning Living Spec depth | Docs / Desktop Living Spec | **W7-120 DONE** |
| Seed next PLAN-12 row after DESK-ACK-01 → DESK-DRAFT-01 | Docs / PLAN-12 | **W7-121 DONE** |
| DESK-DRAFT-01 — Desktop Policies Create/Load draft Living Spec depth | Docs / Desktop Living Spec | **W7-122 DONE** |
| Seed next PLAN-12 row after DESK-DRAFT-01 → DESK-CATALOG-01 | Docs / PLAN-12 | **W7-123 DONE** |
| DESK-CATALOG-01 — Desktop Policies Catalog refresh Living Spec depth | Docs / Desktop Living Spec | **W7-124 DONE** |
| Seed next product tranche after PLAN-12 → PLAN-13 | Docs / product seed | **W7-125 DONE** |
| PLAN-13 — Inventory Desktop layout density Living Spec product tranche | Docs / PLAN-13 | **W7-126 DONE** |
| DESK-LAYOUT-00 — Shared layout tokens + desktop-layout.md Living Spec | Docs / Desktop Living Spec | **W7-127 DONE** |
| Seed next PLAN-13 row after DESK-LAYOUT-00 → DESK-LAYOUT-01 | Docs / PLAN-13 | **W7-128 DONE** |
| DESK-LAYOUT-01 — Snapshot tab primary pane + splitter Living Spec | Docs / Desktop Living Spec | **W7-129 DONE** |
| Seed next PLAN-13 row after DESK-LAYOUT-01 → DESK-LAYOUT-02 | Docs / PLAN-13 | **W7-130 DONE** |
| DESK-LAYOUT-02 — Semantic Diff entry list + splitter Living Spec | Docs / Desktop Living Spec | **W7-131 DONE** |
| Seed next PLAN-13 row after DESK-LAYOUT-02 → DESK-LAYOUT-03 | Docs / PLAN-13 | **W7-132 DONE** |
| DESK-LAYOUT-03 — Drift events/findings/detail splitter Living Spec | Docs / Desktop Living Spec | **W7-133 DONE** |
| Seed next PLAN-13 row after DESK-LAYOUT-03 → DESK-LAYOUT-04 | Docs / PLAN-13 | **W7-134 DONE** |
| DESK-LAYOUT-04 — Audit event list + payload splitter Living Spec | Docs / Desktop Living Spec | **W7-135 DONE** |
| Seed next PLAN-13 row after DESK-LAYOUT-04 → DESK-LAYOUT-05 | Docs / PLAN-13 | **W7-136 DONE** |
| DESK-LAYOUT-05 — Policies MaxHeight cascade Living Spec | Docs / Desktop Living Spec | **W7-137 DONE** |
| Seed next PLAN-13 row after DESK-LAYOUT-05 → DESK-LAYOUT-06 | Docs / PLAN-13 | **W7-138 DONE** |
| DESK-LAYOUT-06 — Node + RoutingAssurance MaxHeight Living Spec | Docs / Desktop Living Spec | **W7-139 DONE** |
| Seed next PLAN-13 row after DESK-LAYOUT-06 → DESK-LAYOUT-07 | Docs / PLAN-13 | **W7-140 DONE** |
| DESK-LAYOUT-07 — Operations Onboarding/Deploy MaxHeight Living Spec | Docs / Desktop Living Spec | **W7-141 DONE** |
| Seed next PLAN-13 row after DESK-LAYOUT-07 → DESK-LAYOUT-08 | Docs / PLAN-13 | **W7-142 DONE** |
| DESK-LAYOUT-08 — Shell chrome column splitter Living Spec | Docs / Desktop Living Spec | **W7-143 DONE** |
| Seed next PLAN-13 row after DESK-LAYOUT-08 → DESK-LAYOUT-09 | Docs / PLAN-13 | **W7-144 DONE** |
| DESK-LAYOUT-09 — Inventory/Zones MaxHeight frames Living Spec | Docs / Desktop Living Spec | **W7-145 DONE** |
| Seed next PLAN-13 row after DESK-LAYOUT-09 → DESK-LAYOUT-10 | Docs / PLAN-13 | **W7-146 DONE** |
| DESK-LAYOUT-10 — PLAN-13 regression lock + docs sync Living Spec | Docs / Desktop Living Spec | **W7-147 DONE** |
| Seed next product tranche after PLAN-13 → PLAN-14 | Docs / product seed | **W7-148 DONE** |
| PLAN-14 — Inventory Desktop Avalonia PlaceholderText / Incident surface Living Spec product tranche | Docs / PLAN-14 | **W7-149 DONE** |
| DESK-PLACEHOLDER-01 — Incident TextBox Watermark→PlaceholderText Living Spec | Desktop / Avalonia | **W7-150 DONE** |
| Seed next PLAN-14 row after DESK-PLACEHOLDER-01 → DESK-PLACEHOLDER-02 | Docs / product seed | **W7-151 DONE** |
| DESK-PLACEHOLDER-02 — Repo-wide Desktop XAML Watermark residue Living Spec | Desktop / Avalonia | **W7-152 DONE** |
| Seed next product tranche after PLAN-14 → PLAN-15 | Docs / product seed | **W7-153 DONE** |
| PLAN-15 — Inventory Desktop Incident mfc-field style hygiene Living Spec product tranche | Docs / PLAN-15 | **W7-154 DONE** |
| DESK-FIELD-01 — Incident TextBox mfc-field Classes Living Spec | Desktop / Avalonia | **W7-155 DONE** |
| Seed next PLAN-15 row after DESK-FIELD-01 → DESK-FIELD-02 | Docs / product seed | **W7-156 DONE** |
| DESK-FIELD-02 — Incident PlaceholderText + mfc-field regression Living Spec | Desktop / Avalonia | **W7-157 DONE** |
| Seed next product tranche after PLAN-15 → PLAN-16 | Docs / product seed | **W7-158 DONE** |
| PLAN-16 — Inventory Desktop Incident AutomationProperties accessible-name Living Spec product tranche | Docs / PLAN-16 | **W7-159 DONE** |
| DESK-A11Y-01 — Incident TextBox AutomationProperties.Name Living Spec | Desktop / Avalonia | **W7-160 DONE** |
| Seed next PLAN-16 row after DESK-A11Y-01 → DESK-A11Y-02 | Docs / product seed | **W7-161 DONE** |
| DESK-A11Y-02 — Incident Names + PlaceholderText + mfc-field regression Living Spec | Desktop / Avalonia | **W7-162 DONE** |
| Seed next product tranche after PLAN-16 → PLAN-17 | Docs / product seed | **W7-163 DONE** |
| PLAN-17 — Inventory Desktop Incident bind-action AutomationProperties Living Spec product tranche | Docs / PLAN-17 | **W7-164 DONE** |
| DESK-A11Y-ACTION-01 — Bind assessment AutomationProperties.Name Living Spec | Desktop / Avalonia | **W7-165 DONE** |
| Seed next PLAN-17 row after DESK-A11Y-ACTION-01 → DESK-A11Y-ACTION-02 | Docs / product seed | **W7-166 DONE** |
| DESK-A11Y-ACTION-02 — Bind Name + Incident field Names regression Living Spec | Desktop / Avalonia | **W7-167 DONE** |
| Seed next product tranche after PLAN-17 → PLAN-18 | Docs / product seed | **W7-168 DONE** |
| PLAN-18 — Inventory Desktop Incident ingest-action AutomationProperties Living Spec product tranche | Docs / PLAN-18 | **W7-169 DONE** |
| DESK-A11Y-INGEST-01 — Ingest signal AutomationProperties.Name Living Spec | Desktop / Avalonia | **W7-170 DONE** |
| Seed next PLAN-18 row after DESK-A11Y-INGEST-01 → DESK-A11Y-INGEST-02 | Docs / product seed | **W7-171 DONE** |
| DESK-A11Y-INGEST-02 — Ingest Name + Bind Name + Incident field Names regression Living Spec | Desktop / Avalonia | **W7-172 DONE** |
| Seed next product tranche after PLAN-18 → PLAN-19 | Docs / product seed | **W7-173 DONE** |
| PLAN-19 — Inventory Desktop shell Connect/Disconnect AutomationProperties Living Spec product tranche | Docs / PLAN-19 | **W7-174 DONE** |
| DESK-A11Y-CONN-01 — Connect/Disconnect AutomationProperties.Name Living Spec | Desktop / Avalonia | **W7-175 DONE** |
| Seed next PLAN-19 row after DESK-A11Y-CONN-01 → DESK-A11Y-CONN-02 | Docs / product seed | **W7-176 DONE** |
| DESK-A11Y-CONN-02 — Connect/Disconnect Names + Incident action Names regression Living Spec | Desktop / Avalonia | **W7-177 DONE** |
| Seed next product tranche after PLAN-19 → PLAN-20 | Docs / product seed | **W7-178 DONE** |
| PLAN-20 — Inventory Desktop Policies lifecycle-action AutomationProperties Living Spec product tranche | Docs / PLAN-20 | **W7-179 DONE** |
| DESK-A11Y-POLICY-01 — Policies lifecycle AutomationProperties.Name Living Spec | Desktop / Avalonia | **W7-180 DONE** |
| Seed next PLAN-20 row after DESK-A11Y-POLICY-01 → DESK-A11Y-POLICY-02 | Docs / product seed | **W7-181 DONE** |
| DESK-A11Y-POLICY-02 — Policies lifecycle Names + shell/Incident Names regression Living Spec | Desktop / Avalonia | **W7-182 DONE** |
| Seed next product tranche after PLAN-20 → PLAN-21 | Docs / product seed | **W7-183 DONE** |
| PLAN-21 — Inventory next Desktop Living Spec product tranche after PLAN-20 | Docs / PLAN-21 | **W7-184 DONE** |
| DESK-A11Y-POLICY-EDIT-01 — Policies authoring residual AutomationProperties.Name Living Spec | Desktop / Avalonia | **W7-185 DONE** |
| Seed next PLAN-21 row after DESK-A11Y-POLICY-EDIT-01 → DESK-A11Y-POLICY-EDIT-02 | Docs / product seed | **W7-186 DONE** |
| DESK-A11Y-POLICY-EDIT-02 — Policies authoring residual Names + lifecycle + shell/Incident Names regression Living Spec | Desktop / Avalonia | **W7-187 DONE** |
| Seed next product tranche after PLAN-21 → PLAN-22 | Docs / product seed | **W7-188 DONE** |
| PLAN-22 — Inventory next Desktop Living Spec product tranche after PLAN-21 | Docs / PLAN-22 | **W7-189 DONE** |
| DESK-A11Y-POLICY-ACK-01 — Policies Record analysis / Acknowledge warning AutomationProperties.Name Living Spec | Desktop / Avalonia | **W7-190 DONE** |
| Seed next PLAN-22 row after DESK-A11Y-POLICY-ACK-01 → DESK-A11Y-POLICY-ACK-02 | Docs / product seed | **W7-191 DONE** |
| DESK-A11Y-POLICY-ACK-02 — Policies ack/record Names + authoring residual + lifecycle + shell/Incident Names regression Living Spec | Desktop / Avalonia | **W7-192 DONE** |
| Seed next product tranche after PLAN-22 → PLAN-23 | Docs / product seed | **W7-193 DONE** |
| PLAN-23 — Inventory next Desktop Living Spec product tranche after PLAN-22 | Docs / PLAN-23 | **W7-194 DONE** |
| DESK-A11Y-POLICY-OBJ-01 — Policies catalog/object AutomationProperties.Name Living Spec | Desktop / Avalonia | **W7-195 DONE** |
| Seed next PLAN-23 row after DESK-A11Y-POLICY-OBJ-01 → DESK-A11Y-POLICY-OBJ-02 | Docs / product seed | **W7-196 DONE** |
| DESK-A11Y-POLICY-OBJ-02 — Policies catalog/object Names + ack/record + authoring residual + lifecycle + shell/Incident Names regression Living Spec | Desktop / Avalonia | **W7-197 DONE** |
| Seed next product tranche after PLAN-23 → PLAN-24 | Docs / product seed | **W7-198 DONE** |
| PLAN-24 — Inventory next Desktop Living Spec product tranche after PLAN-23 | Docs / PLAN-24 | **W7-199 DONE** |
| DESK-A11Y-OPS-01 — Onboarding/Deployment primary actions AutomationProperties.Name Living Spec | Desktop / Avalonia | **W7-200 DONE** |
| Seed next PLAN-24 row after DESK-A11Y-OPS-01 → DESK-A11Y-OPS-02 | Docs / product seed | **W7-201 DONE** |
| DESK-A11Y-OPS-02 — Onboarding/Deployment Names + Policies/shell/Incident Names regression Living Spec | Desktop / Avalonia | **W7-202 DONE** |
| Seed next product tranche after PLAN-24 → PLAN-25 | Docs / product seed | **W7-203 DONE** |
| PLAN-25 — Inventory next Desktop Living Spec product tranche after PLAN-24 | Docs / PLAN-25 | **W7-204 DONE** |
| DESK-A11Y-INV-01 — Inventory/Zones primary actions AutomationProperties.Name | Desktop / Avalonia | **W7-207 DONE** |
| Seed next PLAN-25 row after DESK-A11Y-INV-01 → DESK-A11Y-INV-02 | Docs / product seed | **W7-208 DONE** |
| DESK-A11Y-INV-02 — Inventory/Zones Names + ops + Policies/shell/Incident Names regression Living Spec | Desktop / Avalonia | **W7-209 DONE** |

### P3 / new Contracts (evidence)

| Gap | Queue ID |
|-----|----------|
| No `ListPolicies` in `policy.proto` (catalog browse) | **W5-01 DONE** |
| No Desktop RPC for ManagementPath / FastTrack | **W5-02 DONE** |
| Deployment `semantic_diff_entries` is `repeated string` | **W5-03 DONE** |
| CRS / physical lab runner | **Not §3** — residual in known-limitations; ops parallel |

### Deferred / not this tranche (evidence)

- `StartCapture` `node_id` — **W6-03 DONE** (#356); device_id path unchanged
- Policies “Save and Deploy” — MVP scope lock
- Local Desktop `SemanticDiffEngine` — anti-goal
- Auto-fix drift — anti-goal
- Fake VRRP Master/Backup labels without capture facts — anti-goal

## Dual track (product never waits on lab)

```text
Product §3.C (linear, one NEXT)     Ops / lab (parallel, never blocks §3)
─────────────────────────────────   ─────────────────────────────────────
PLAN-02 docs **DONE** (#345)        GNS3 day-2 fixture (out of git)
CONT-01 Rollback Watch **DONE**      Isolated WriteEnabled=true lab
CONT-02 Neighbor → member b **DONE** Live CHR when an isolated runner exists
W5-01 ListPolicies **DONE**
W5-02 ManagementPath / FastTrack **DONE**
W5-03 Typed deploy policy diff **DONE**
W6-01 Operator-readable Diff/Snapshot **DONE**
W6-02 VRRP pair consistency **DONE**
W6-03 StartCapture node_id **DONE**
W6-04 Onboarding Rollback Watch **DONE**
W6-05 GetNode Reachability **DONE**
W6-06 Policies typed Diff rows **DONE**
W6-07 Diff baseline catalog **DONE**
W6-08 Durable Unreachable **DONE**
W6-09 Policies Move up/down reorder **DONE**
SEC-01 Reject system actor gRPC spoof **DONE**
SEC-02 Deploy artifact materializer **DONE**
SEC-03 Audit hash chain **DONE**
SEC-04 INTERNAL_CA trusted CA store **DONE**
SEC-05 Atomic mutation/idempotency/audit **DONE**
SEC-06 Incident assessment gRPC **DONE**
SEC-07 Extend atomic mutation boundary **DONE**
SEC-08 Connection profile + deployment UoW **DONE**
SEC-09 Onboarding workflow UoW **DONE**
SEC-10 Incident overlay expire UoW **DONE**
SEC-11 Drift detect + response-feedback UoW **DONE**
SEC-12 CaptureSnapshot persist+audit UoW **DONE**
SEC-13 UpsertDeviceHashState UoW **DONE**
SEC-14 OpenEndpointPresence UoW **DONE**
SEC-15 UpsertRoutingAssuranceState UoW **DONE**
W7-01 Desktop MikroTik/Winbox display labels **DONE**
W7-02 Bind gRPC actor to authenticated principal **DONE**
W7-03 Controller Kestrel mTLS client certificates **DONE**
W7-04 Validate mTLS client certs against TrustedCa **DONE**
W7-05 Bind Desktop actor from client cert CN **DONE**
W7-06 Map mTLS client cert to HttpContext.User **DONE**
W7-07 Prefer HttpContext.User over gRPC peer identity for actor **DONE**
W7-08 Desktop status shows resolved mTLS actor **DONE**
W7-09 Production mTLS operator checklist **DONE**
W7-10 Log redacted client-cert thumbprint on mTLS principal map **DONE**
W7-11 Harden SessionFaultInjection timeout pending-clear **DONE**
W7-12 Desktop AuthenticationFailed status Living Spec **DONE**
W7-13 mTLS map log includes request TraceIdentifier **DONE**
W7-14 SECURITY.md documents TraceIdentifier on mTLS map log **DONE**
W7-15 Pilot runbook TraceIdentifier for mTLS Connect correlation **DONE**
W7-16 controller-configuration.md TraceIdentifier cross-link **DONE**
W7-17 Lock resolve-only zone UoW residual Living Spec **DONE**
W7-18 Lock Start* AddOperationAsync outside-UoW residual Living Spec **DONE**
W7-19 Lock orchestrator-only audit residual Living Spec **DONE**
W7-20 Lock Feedback delivery / RouterOS capture outside-DB residual Living Spec **DONE**
W7-21 Lock IResponseFeedbackDeliveryPort not-configured residual Living Spec **DONE**
W7-22 Lock Desktop zip/tar installer packaging residual Living Spec **DONE**
W7-23 Lock SHA256SUMS attestation packaging residual Living Spec **DONE**
W7-24 Lock CycloneDX-lite SBOM packaging residual Living Spec **DONE**
W7-25 Lock MVP scope-lock no-NAT/RAW writes residual Living Spec **DONE**
W7-26 Lock MVP scope-lock no-campaigns/auto-deploy residual Living Spec **DONE**
W7-27 Lock Live CHR matrix OFF residual Living Spec **DONE**
W7-28 Lock Golden live CHR hashes env-gated residual Living Spec **DONE**
W7-29 Lock Live physical CRS hardware OFF ops residual Living Spec **DONE**
W7-30 Lock Controller migrate-only startup residual Living Spec **DONE**
W7-31 Lock Development master-key provider residual Living Spec **DONE**
W7-32 Lock GitHub-hosted CI billing-limited residual Living Spec **DONE**
W7-33 Seed next continuous residual after CI billing Living Spec **DONE**
W7-34 Lock Desktop window does not stop Controller residual Living Spec **DONE**
W7-35 Lock Inventory Add router wizard DONE residual Living Spec **DONE**
W7-36 Seed next continuous residual after Inventory Add router Living Spec **DONE**
W7-37 Lock gRPC remains available for automation residual Living Spec **DONE**
W7-38 Seed next continuous residual after gRPC automation Living Spec **DONE**
W7-39 Lock RouterOs Enabled default fail-closed residual Living Spec **DONE**
W7-40 Seed next continuous residual after RouterOs fail-closed Living Spec **DONE**
W7-41 Lock RouterOs WriteEnabled operator residual Living Spec **DONE**
W7-42 Seed next continuous residual after WriteEnabled Living Spec **DONE**
W7-43 Lock N1-07 path-class E2E DONE residual Living Spec **DONE**
W7-44 Seed next continuous residual after N1-07 Living Spec **DONE**
W7-45 Lock M7.1…M7.4 CLOSED residual Living Spec **DONE**
W7-46 Seed next continuous residual after M7 CLOSED Living Spec **DONE**
W7-47 Lock SEC-07…SEC-15 DONE residual Living Spec **DONE**
W7-48 Seed next continuous residual after SEC-07…15 Living Spec **DONE**
W7-49 Lock known-limitations residual Living Spec corpus COMPLETE **DONE**
W7-50 Seed next product tranche after residual corpus Living Spec **DONE**
W7-51 PLAN-03 — Inventory next quality-gate product tranche **DONE**
W7-52 QG-IMPORT-01 — Import graph + cycle anomaly Living Spec gate **DONE**
W7-53 QG-DOCS-01 — Weekly docs smoke Living Spec gate **DONE**
W7-54 QG-ANTISTUB-01 — Anti-stub CI Living Spec gate **DONE**
W7-55 QG-LIVESPEC-MATRIX-01 — Living Spec matrix PR gate **DONE**
W7-56 QG-SIGN-01 — Release signing checklist Living Spec gate **DONE**
W7-57 Seed next product tranche after PLAN-03 quality gates **DONE**
W7-58 PLAN-04 — Inventory next contract-test / API Living Spec product tranche **DONE**
W7-59 CT-DEPLOY-01 — DeploymentService GrpcHost contract Living Spec **DONE**
W7-60 CT-ZONE-01 — ZoneService GrpcHost contract Living Spec **DONE**
W7-61 CT-DRIFT-01 — DriftService GrpcHost contract Living Spec **DONE**
W7-62 CT-AUDIT-01 — AuditService GrpcHost contract Living Spec **DONE**
W7-63 CT-ROUTING-01 — RoutingAssuranceService ProtoContract + GrpcHost Living Spec **DONE**
W7-64 CT-INCIDENT-01 — IncidentService ProtoContract + GrpcHost Living Spec **DONE**
W7-65 Seed next product tranche after PLAN-04 → PLAN-05 Desktop operator-surface **DONE**
W7-66 PLAN-05 — Inventory next Desktop operator-surface Living Spec product tranche **DONE**
W7-67 DESK-AUDIT-01 — Desktop Audit panel Living Spec vs AuditGrpcHost **DONE**
W7-68 DESK-DRIFT-01 — Desktop Drift panel Living Spec vs DriftGrpcHost **DONE**
W7-69 DESK-ZONE-01 — Desktop Zones panel Living Spec vs ZoneGrpcHost **DONE**
W7-70 DESK-ROUTING-01 — Desktop Routing assurance Living Spec vs RoutingAssuranceGrpcHost **DONE**
W7-71 Seed next product tranche after PLAN-05 Desktop operator-surface **DONE**
W7-72 PLAN-06 — Inventory next Incident Desktop operator-surface Living Spec product tranche **DONE**
W7-73 DESK-INCIDENT-01 — Desktop Incident ViewModel + client Living Spec vs IncidentGrpcHost **DONE**
W7-74 DESK-INCIDENT-02 — Desktop Incident MainWindow panel Living Spec **DONE**
W7-75 DESK-INCIDENT-03 — Desktop Incident Bind assessment Living Spec **DONE**
W7-76 DESK-INCIDENT-04 — Desktop Incident fail-closed no deploy/overlay Living Spec **DONE**
W7-77 Seed next product tranche after PLAN-06 Incident Desktop operator-surface **DONE**
W7-78 PLAN-07 — Inventory next Core MVP Desktop operator-surface Living Spec product tranche **DONE**
W7-79 DESK-POLICY-01 — Desktop Policies panel Living Spec vs PolicyGrpcHost **DONE**
W7-80 Seed next PLAN-07 row after DESK-POLICY-01 → DESK-DEPLOY-01 **DONE**
W7-81 DESK-DEPLOY-01 — Desktop Deployment Living Spec vs DeploymentGrpcHost **DONE**
W7-82 Seed next PLAN-07 row after DESK-DEPLOY-01 → DESK-ONBOARD-01 **DONE**
W7-83 DESK-ONBOARD-01 — Desktop Onboarding Living Spec vs OnboardingGrpcHost **DONE**
W7-84 Seed next PLAN-07 row after DESK-ONBOARD-01 → DESK-SNAPSHOT-01 **DONE**
W7-85 DESK-SNAPSHOT-01 — Desktop Snapshot Living Spec vs SnapshotGrpcHost **DONE**
W7-86 Seed next PLAN-07 row after DESK-SNAPSHOT-01 → DESK-INVENTORY-01 **DONE**
W7-87 DESK-INVENTORY-01 — Desktop Inventory Living Spec vs InventoryGrpcHost **DONE**
W7-88 Seed next product tranche after PLAN-07 Core MVP Desktop **DONE**
W7-89 PLAN-08 — Inventory next Desktop secondary operator-surface Living Spec product tranche **DONE**
W7-90 DESK-NODE-01 — Desktop Node VRRP pair Living Spec vs InventoryGrpcHost **DONE**
W7-91 Seed next PLAN-08 row after DESK-NODE-01 → DESK-NBR-01 **DONE**
W7-92 DESK-NBR-01 — Desktop Neighbor candidates Living Spec depth **DONE**
W7-93 Seed next PLAN-08 row after DESK-NBR-01 → DESK-PROBE-01 **DONE**
W7-94 DESK-PROBE-01 — Desktop ValidateDeviceConnection probe Living Spec depth **DONE**
W7-95 Seed next PLAN-08 row after DESK-PROBE-01 → DESK-POLICY-02 **DONE**
W7-96 DESK-POLICY-02 — Desktop Policy safety analysis Living Spec depth **DONE**
W7-97 Seed next product tranche after PLAN-08 → PLAN-09 **DONE**
W7-98 PLAN-09 — Inventory next Desktop connection-status operator-surface Living Spec product tranche **DONE**
W7-99 DESK-CONN-01 — Desktop Connect/Disconnect Living Spec depth **DONE**
W7-100 Seed next PLAN-09 row after DESK-CONN-01 → DESK-MTLS-01 **DONE**
W7-101 DESK-MTLS-01 — Desktop mTLS actor status Living Spec depth **DONE**
W7-102 Seed next PLAN-09 row after DESK-MTLS-01 → DESK-AUTH-01 **DONE**
W7-103 DESK-AUTH-01 — Desktop AuthenticationFailed/TlsError Living Spec depth **DONE**
W7-104 Seed next product tranche after PLAN-09 → PLAN-10 **DONE**
W7-105 PLAN-10 — Inventory next Desktop shell chrome & Policies authoring depth Living Spec product tranche **DONE**
W7-106 DESK-SHELL-01 — Desktop Shell navigation/hotkeys Living Spec depth **DONE**
W7-107 Seed next PLAN-10 row after DESK-SHELL-01 → DESK-DIFF-01 **DONE**
W7-108 DESK-DIFF-01 — Desktop Policies Diff Living Spec depth **DONE**
W7-109 Seed next PLAN-10 row after DESK-DIFF-01 → DESK-REORDER-01 **DONE**
W7-110 DESK-REORDER-01 — Desktop Policies Move up/down Living Spec depth **DONE**
PLAN-10 COMPLETE
W7-111 Seed next product tranche after PLAN-10 → PLAN-11 **DONE**
W7-112 PLAN-11 — Inventory next Desktop Policies review-compose lifecycle Living Spec product tranche **DONE**
W7-113 Seed next PLAN-11 row after DESK-SUBMIT-01 → DESK-COMPOSE-01 **DONE**
W7-114 DESK-SUBMIT-01 — Desktop Policies SubmitForReview Living Spec depth **DONE**
W7-115 DESK-COMPOSE-01 — Desktop Policies Compose+RecordAnalysis Living Spec depth **DONE**
W7-116 DESK-GATE-01 — Desktop Policies Approve/Bind/Compile Living Spec depth **DONE**
W7-118 Seed next product tranche after PLAN-11 → PLAN-12 **DONE**
W7-119 PLAN-12 — Inventory next Desktop Policies residual lifecycle Living Spec product tranche **DONE**
W7-120 DESK-ACK-01 — Desktop Policies AcknowledgeWarning Living Spec depth **DONE**
W7-121 Seed next PLAN-12 row after DESK-ACK-01 → DESK-DRAFT-01 **DONE**
W7-122 DESK-DRAFT-01 — Desktop Policies Create/Load draft Living Spec depth **DONE**
W7-123 Seed next PLAN-12 row after DESK-DRAFT-01 → DESK-CATALOG-01 **DONE**
W7-124 DESK-CATALOG-01 — Desktop Policies Catalog refresh Living Spec depth **DONE**
W7-125 Seed next product tranche after PLAN-12 → PLAN-13 **DONE**
W7-126 PLAN-13 — Inventory Desktop layout density Living Spec product tranche **DONE**
W7-127 DESK-LAYOUT-00 — Shared layout tokens + desktop-layout.md Living Spec **DONE**
W7-128 Seed next PLAN-13 row after DESK-LAYOUT-00 → DESK-LAYOUT-01 **DONE**
W7-129 DESK-LAYOUT-01 — Snapshot tab primary pane + splitter Living Spec **DONE**
W7-130 Seed next PLAN-13 row after DESK-LAYOUT-01 → DESK-LAYOUT-02 **DONE**
W7-131 DESK-LAYOUT-02 — Semantic Diff entry list + splitter Living Spec **DONE**
W7-132 Seed next PLAN-13 row after DESK-LAYOUT-02 → DESK-LAYOUT-03 **DONE**
W7-133 DESK-LAYOUT-03 — Drift events/findings/detail splitter Living Spec **DONE**
W7-134 Seed next PLAN-13 row after DESK-LAYOUT-03 → DESK-LAYOUT-04 **DONE**
W7-135 DESK-LAYOUT-04 — Audit event list + payload splitter Living Spec **DONE**
W7-136 Seed next PLAN-13 row after DESK-LAYOUT-04 → DESK-LAYOUT-05 **DONE**
W7-137 DESK-LAYOUT-05 — Policies MaxHeight cascade Living Spec **DONE**
W7-138 Seed next PLAN-13 row after DESK-LAYOUT-05 → DESK-LAYOUT-06 **DONE**
W7-139 DESK-LAYOUT-06 — Node + RoutingAssurance MaxHeight Living Spec **DONE**
W7-140 Seed next PLAN-13 row after DESK-LAYOUT-06 → DESK-LAYOUT-07 **DONE**
W7-141 DESK-LAYOUT-07 — Operations Onboarding/Deploy MaxHeight Living Spec **DONE**
W7-142 Seed next PLAN-13 row after DESK-LAYOUT-07 → DESK-LAYOUT-08 **DONE**
W7-143 DESK-LAYOUT-08 — Shell chrome column splitter Living Spec **DONE**
W7-144 Seed next PLAN-13 row after DESK-LAYOUT-08 → DESK-LAYOUT-09 **DONE**
W7-145 DESK-LAYOUT-09 — Inventory/Zones MaxHeight frames Living Spec **DONE**
W7-146 Seed next PLAN-13 row after DESK-LAYOUT-09 → DESK-LAYOUT-10 **DONE**
W7-147 DESK-LAYOUT-10 — PLAN-13 regression lock + docs sync Living Spec **DONE**
W7-148 Seed next product tranche after PLAN-13 → PLAN-14 **DONE**
W7-149 PLAN-14 — Inventory Desktop Avalonia PlaceholderText / Incident surface Living Spec product tranche **DONE**
W7-150 DESK-PLACEHOLDER-01 — Incident TextBox Watermark→PlaceholderText Living Spec **DONE**
W7-151 Seed next PLAN-14 row after DESK-PLACEHOLDER-01 → DESK-PLACEHOLDER-02 **DONE**
W7-152 DESK-PLACEHOLDER-02 — Repo-wide Desktop XAML Watermark residue Living Spec **DONE**
W7-153 Seed next product tranche after PLAN-14 → PLAN-15 **DONE**
W7-154 PLAN-15 — Inventory Desktop Incident mfc-field style hygiene Living Spec product tranche **DONE**
W7-155 DESK-FIELD-01 — Incident TextBox mfc-field Classes Living Spec **DONE**
W7-156 Seed next PLAN-15 row after DESK-FIELD-01 → DESK-FIELD-02 **DONE**
W7-157 DESK-FIELD-02 — Incident PlaceholderText + mfc-field regression Living Spec **DONE**
W7-158 Seed next product tranche after PLAN-15 → PLAN-16 **DONE**
W7-159 PLAN-16 — Inventory Desktop Incident AutomationProperties accessible-name Living Spec product tranche **DONE**
W7-160 DESK-A11Y-01 — Incident TextBox AutomationProperties.Name Living Spec **DONE**
W7-161 Seed next PLAN-16 row after DESK-A11Y-01 → DESK-A11Y-02 **DONE**
W7-162 DESK-A11Y-02 — Incident Names + PlaceholderText + mfc-field regression Living Spec **DONE**
W7-163 Seed next product tranche after PLAN-16 → PLAN-17 **DONE**
W7-164 PLAN-17 — Inventory Desktop Incident bind-action AutomationProperties Living Spec product tranche **DONE**
W7-165 DESK-A11Y-ACTION-01 — Bind assessment AutomationProperties.Name Living Spec **DONE**
W7-166 Seed next PLAN-17 row after DESK-A11Y-ACTION-01 → DESK-A11Y-ACTION-02 **DONE**
W7-167 DESK-A11Y-ACTION-02 — Bind Name + Incident field Names regression Living Spec **DONE**
W7-168 Seed next product tranche after PLAN-17 → PLAN-18 **DONE**
W7-169 PLAN-18 — Inventory Desktop Incident ingest-action AutomationProperties Living Spec product tranche **DONE**
W7-170 DESK-A11Y-INGEST-01 — Ingest signal AutomationProperties.Name Living Spec **DONE**
W7-171 Seed next PLAN-18 row after DESK-A11Y-INGEST-01 → DESK-A11Y-INGEST-02 **DONE**
W7-172 DESK-A11Y-INGEST-02 — Ingest Name + Bind Name + Incident field Names regression Living Spec **DONE**
W7-173 Seed next product tranche after PLAN-18 → PLAN-19 **DONE**
W7-174 PLAN-19 — Inventory Desktop shell Connect/Disconnect AutomationProperties Living Spec product tranche **DONE**
W7-175 DESK-A11Y-CONN-01 — Connect/Disconnect AutomationProperties.Name Living Spec **DONE**
W7-176 Seed next PLAN-19 row after DESK-A11Y-CONN-01 → DESK-A11Y-CONN-02 **DONE**
W7-177 DESK-A11Y-CONN-02 — Connect/Disconnect Names + Incident action Names regression Living Spec **DONE**
W7-178 Seed next product tranche after PLAN-19 → PLAN-20 **DONE**
W7-179 PLAN-20 — Inventory Desktop Policies lifecycle-action AutomationProperties Living Spec product tranche **DONE**
W7-180 DESK-A11Y-POLICY-01 — Policies lifecycle AutomationProperties.Name Living Spec **DONE**
W7-181 Seed next PLAN-20 row after DESK-A11Y-POLICY-01 → DESK-A11Y-POLICY-02 **DONE**
W7-182 DESK-A11Y-POLICY-02 — Policies lifecycle Names + shell/Incident Names regression Living Spec **DONE**
W7-183 Seed next product tranche after PLAN-20 → PLAN-21 **DONE**
W7-184 PLAN-21 — Inventory next Desktop Living Spec product tranche after PLAN-20 **DONE**
W7-185 DESK-A11Y-POLICY-EDIT-01 — Policies authoring residual AutomationProperties.Name Living Spec **DONE**
W7-186 Seed next PLAN-21 row after DESK-A11Y-POLICY-EDIT-01 → DESK-A11Y-POLICY-EDIT-02 **DONE**
W7-187 DESK-A11Y-POLICY-EDIT-02 — Policies authoring residual Names + lifecycle + shell/Incident Names regression Living Spec **DONE**
W7-188 Seed next product tranche after PLAN-21 → PLAN-22 **DONE**
W7-189 PLAN-22 — Inventory next Desktop Living Spec product tranche after PLAN-21 **DONE**
W7-190 DESK-A11Y-POLICY-ACK-01 — Policies Record analysis / Acknowledge warning AutomationProperties.Name Living Spec **DONE**
W7-191 Seed next PLAN-22 row after DESK-A11Y-POLICY-ACK-01 → DESK-A11Y-POLICY-ACK-02 **DONE**
W7-192 DESK-A11Y-POLICY-ACK-02 — Policies ack/record Names + authoring residual + lifecycle + shell/Incident Names regression Living Spec **DONE**
W7-193 Seed next product tranche after PLAN-22 → PLAN-23 **DONE**
W7-194 PLAN-23 — Inventory next Desktop Living Spec product tranche after PLAN-22 **DONE**
W7-195 DESK-A11Y-POLICY-OBJ-01 — Policies catalog/object AutomationProperties.Name Living Spec **DONE**
W7-196 Seed next PLAN-23 row after DESK-A11Y-POLICY-OBJ-01 → DESK-A11Y-POLICY-OBJ-02 **DONE**
W7-197 DESK-A11Y-POLICY-OBJ-02 — Policies catalog/object Names + ack/record + authoring residual + lifecycle + shell/Incident Names regression Living Spec **DONE**
W7-198 Seed next product tranche after PLAN-23 → PLAN-24 **DONE**
W7-199 PLAN-24 — Inventory next Desktop Living Spec product tranche after PLAN-23 **DONE**
W7-200 DESK-A11Y-OPS-01 — Onboarding/Deployment primary actions AutomationProperties.Name Living Spec **DONE**
W7-201 Seed next PLAN-24 row after DESK-A11Y-OPS-01 → DESK-A11Y-OPS-02 **DONE**
W7-202 DESK-A11Y-OPS-02 — Onboarding/Deployment Names + Policies/shell/Incident Names regression Living Spec **DONE**
W7-203 Seed next product tranche after PLAN-24 → PLAN-25 **DONE**
W7-204 PLAN-25 — Inventory next Desktop Living Spec product tranche after PLAN-24 **DONE**
W7-207 DESK-A11Y-INV-01 — Inventory/Zones primary actions AutomationProperties.Name **DONE**
W7-208 Seed next PLAN-25 row after DESK-A11Y-INV-01 → DESK-A11Y-INV-02 **DONE**
W7-209 DESK-A11Y-INV-02 — Inventory/Zones Names + ops + Policies/shell/Incident Names regression Living Spec **DONE**
W7-205 Seed next product tranche after PLAN-25 → PLAN-26 **DONE**
W7-206 PLAN-26 — Inventory code-audit remediation tranche (11cb746) **DONE**
W7-210 AUDIT-RULE-01 — Update rule predicate / hidden fields round-trip **DONE**
W7-211 Seed next PLAN-26 row after AUDIT-RULE-01 → AUDIT-CTX-01 **DONE**
W7-212 AUDIT-CTX-01 — Node switch must invalidate mutation context **DONE**
W7-213 Seed next PLAN-26 row after AUDIT-CTX-01 → AUDIT-CAP-01 **DONE**
W7-214 AUDIT-CAP-01 — Canonical filter must include firewall match fields **DONE**
W7-215 Seed next PLAN-26 row after AUDIT-CAP-01 → AUDIT-CAP-02 **DONE**
W7-216 AUDIT-CAP-02 — Required-section read failure must not complete snapshot **DONE**
W7-217 Seed next PLAN-26 row after AUDIT-CAP-02 → AUDIT-AN-01 **DONE**
W7-218 AUDIT-AN-01 — Validate/Record must require full analysis + mandatory tests **DONE**
W7-219 Seed next PLAN-26 row after AUDIT-AN-01 → AUDIT-AN-02 **DONE**
W7-220 AUDIT-AN-02 — Analysis fingerprint CAS must use controller-computed value **DONE**
W7-221 Seed next PLAN-26 row after AUDIT-AN-02 → AUDIT-DIFF-01 **DONE**
W7-222 AUDIT-DIFF-01 — Semantic policy diff must surface reachability change **DONE**
W7-223 Seed next PLAN-26 row after AUDIT-DIFF-01 → AUDIT-GUARD-01 **DONE**
W7-224 AUDIT-GUARD-01 — ManagementPath must enforce complete guard contract **DONE**
W7-225 Seed next PLAN-26 row after AUDIT-GUARD-01 → AUDIT-DEP-01 **DONE**
W7-226 AUDIT-DEP-01 — Background recovery must not race active deployment **DONE**
W7-227 Seed next PLAN-26 row after AUDIT-DEP-01 → AUDIT-DEP-02 **DONE**
W7-228 AUDIT-DEP-02 — Watchdog durable clock/TTL; cleanup result must not be ignored **DONE**
W7-229 Seed next PLAN-26 row after AUDIT-DEP-02 → AUDIT-DEP-03 **DONE**
W7-230 AUDIT-DEP-03 — Fake VRRP reachability/traffic facts **DONE**
W7-231 Seed next PLAN-26 row after AUDIT-DEP-03 → AUDIT-GUI-01 **DONE**
W7-232 AUDIT-GUI-01 — Onboarding/Deployment synthetic payloads; Deploy never enables **DONE**
W7-233 Seed next PLAN-26 row after AUDIT-GUI-01 → AUDIT-AUTH-01 **DONE**
W7-234 AUDIT-AUTH-01 — Production operator authorization DenyAll **DONE**
W7-235 Seed next PLAN-26 row after AUDIT-AUTH-01 → AUDIT-INT-01 **DONE**
W7-236 AUDIT-INT-01 — FastTrack topology / verification session disposal / progress Watch auth hubs **DONE**
W7-237 Seed next after AUDIT-INT-01 (PLAN-26 COMPLETE) **DONE**
W7-238 PLAN-27 — Inventory Desktop Snapshot/Node/Drift/Audit AutomationProperties residual tranche after PLAN-26 **DONE**
W7-239 Seed first PLAN-27 atomic row after inventory → DESK-A11Y-SNAP-01 **DONE**
W7-240 DESK-A11Y-SNAP-01 — Snapshot Capture/Reload/Compare/Copy AutomationProperties.Name **DONE**
W7-241 Seed next PLAN-27 row after DESK-A11Y-SNAP-01 → DESK-A11Y-PANEL-01 **DONE**
W7-242 DESK-A11Y-PANEL-01 — Node Refresh / Validate / Drift / Audit Refresh AutomationProperties.Name **DONE**
W7-243 Seed next after DESK-A11Y-PANEL-01 (PLAN-27 COMPLETE) **DONE**
W7-244 PLAN-28 — Inventory Desktop residual field/control AutomationProperties tranche after PLAN-27 **DONE**
W7-245 Seed first PLAN-28 atomic row after inventory → DESK-A11Y-FIELD-01 **DONE**
W7-246 DESK-A11Y-FIELD-01 — Zones / Policies draft TextBox AutomationProperties.Name **DONE**
W7-247 Seed next PLAN-28 row after DESK-A11Y-FIELD-01 → DESK-A11Y-CTRL-01 **DONE**
W7-248 DONE — DESK-A11Y-CTRL-01 — Snapshot/Diff ComboBox & CheckBox + TabItem AutomationProperties.Name
W7-249 Seed next after DESK-A11Y-CTRL-01 (PLAN-28 COMPLETE) **DONE**
W7-250 PLAN-29 — Inventory Desktop connection health / reconnect after Controller stop (AUDIT §18 residual) **DONE**
W7-251 Seed first PLAN-29 atomic row after inventory → DESK-CONN-HEALTH-01 **DONE**
W7-252 DESK-CONN-HEALTH-01 — Connected-state periodic gRPC health probe after Controller stop **DONE**
W7-253 Seed next PLAN-29 row after DESK-CONN-HEALTH-01 → DESK-CONN-RECONNECT-01 **DONE**
W7-254 DESK-CONN-RECONNECT-01 — Bounded reconnect after health-fail drop + shell StatusText/LastError sync **DONE**
W7-255 Seed next after DESK-CONN-RECONNECT-01 (PLAN-29 COMPLETE) **DONE**
W7-256 PLAN-30 — Inventory Watch operation-owner ACL / hub slow-subscriber backpressure (AUDIT §19 residual) **DONE**
W7-257 Seed first PLAN-30 atomic row after inventory → WATCH-OWN-01 **DONE**
W7-258 WATCH-OWN-01 — Bind Watch RPCs to operation owner beyond Read permission **DONE**
W7-259 Seed next PLAN-30 row after WATCH-OWN-01 → WATCH-BP-01 **DONE**
W7-260 WATCH-BP-01 — Bounded ProgressHub subscriber channels / slow-subscriber backpressure + live `_history` cap **DONE**
W7-261 Seed next after WATCH-BP-01 (PLAN-30 COMPLETE) **DONE**
W7-117 Seed next PLAN-11 row after DESK-COMPOSE-01 → DESK-GATE-01 **DONE**
residual ops: CRS / physical lab runner (not §3 stop-gate)
```

`/autopilot` always takes **§3 NEXT**. It does not wait for lab phase transitions.

## Linear queue (one PR each)

| Order | ID | GitHub | Scope | Status |
|------:|----|-------:|-------|--------|
| 1 | PLAN-02 | [#339](https://github.com/sesquicadaver/MTDirector/issues/339) | Seed §3.C + process | **DONE** ([#345](https://github.com/sesquicadaver/MTDirector/pull/345)) |
| 2 | CONT-01 | [#340](https://github.com/sesquicadaver/MTDirector/issues/340) | Rollback + Watch (existing RPC) | **DONE** |
| 3 | CONT-02 | [#341](https://github.com/sesquicadaver/MTDirector/issues/341) | Neighbor apply → VRRP member b | **DONE** |
| 4 | W5-01 | [#342](https://github.com/sesquicadaver/MTDirector/issues/342) | `ListPolicies` catalog browse | **DONE** |
| 5 | W5-02 | [#343](https://github.com/sesquicadaver/MTDirector/issues/343) | ManagementPath / FastTrack Desktop | **DONE** |
| 6 | W5-03 | [#344](https://github.com/sesquicadaver/MTDirector/issues/344) | Typed deployment semantic policy diff | **DONE** |
| 7 | W6-01 | [#352](https://github.com/sesquicadaver/MTDirector/issues/352) | Operator-readable snapshot/diff + VRRP surface | **DONE** |
| 8 | W6-02 | [#354](https://github.com/sesquicadaver/MTDirector/issues/354) | VRRP pair consistency (config + logical FW) | **DONE** |
| 9 | W6-03 | [#356](https://github.com/sesquicadaver/MTDirector/issues/356) | StartCapture node_id fan-out | **DONE** |
| 10 | W6-04 | [#358](https://github.com/sesquicadaver/MTDirector/issues/358) | Onboarding Rollback + Watch (hub + Desktop) | **DONE** |
| 11 | W6-05 | [#360](https://github.com/sesquicadaver/MTDirector/issues/360) | GetNode Reachability from probe | **DONE** |
| 12 | W6-06 | [#362](https://github.com/sesquicadaver/MTDirector/issues/362) | Policies typed Diff rows | **DONE** |
| 13 | W6-07 | [#364](https://github.com/sesquicadaver/MTDirector/issues/364) | Diff baseline from catalog picker | **DONE** |
| 14 | W6-08 | [#366](https://github.com/sesquicadaver/MTDirector/issues/366) | Durable GetNode Unreachable | **DONE** |
| 15 | W6-09 | [#369](https://github.com/sesquicadaver/MTDirector/issues/369) | Policies Reorder via Move up/down | **DONE** |
| 16 | SEC-01 | [#371](https://github.com/sesquicadaver/MTDirector/issues/371) | Reject system actor via gRPC metadata | **DONE** |
| 17 | SEC-02 | [#372](https://github.com/sesquicadaver/MTDirector/issues/372) | Deploy artifact materializer + observed hash | **DONE** |
| 18 | SEC-03 | [#373](https://github.com/sesquicadaver/MTDirector/issues/373) | Audit hash chain includes predecessor bytes | **DONE** |
| 19 | SEC-04 | [#377](https://github.com/sesquicadaver/MTDirector/issues/377) | INTERNAL_CA directory trusted CA store + revocation | **DONE** |
| 20 | SEC-05 | [#378](https://github.com/sesquicadaver/MTDirector/issues/378) | Atomic mutation + idempotency + audit boundary | **DONE** |
| 21 | SEC-06 | [#380](https://github.com/sesquicadaver/MTDirector/issues/380) | Incident assessment gRPC surface | **DONE** |
| 22 | SEC-07 | [#383](https://github.com/sesquicadaver/MTDirector/issues/383) | Extend atomic mutation boundary | **DONE** |
| 23 | SEC-08 | [#385](https://github.com/sesquicadaver/MTDirector/issues/385) | Connection profile + deployment UoW | **DONE** |
| 24 | SEC-09 | [#387](https://github.com/sesquicadaver/MTDirector/issues/387) | Onboarding workflow UoW | **DONE** |
| 25 | SEC-10 | [#389](https://github.com/sesquicadaver/MTDirector/issues/389) | Incident overlay expire UoW | **DONE** |
| 26 | SEC-11 | [#391](https://github.com/sesquicadaver/MTDirector/issues/391) | Drift detect + response-feedback UoW | **DONE** |
| 27 | SEC-12 | [#392](https://github.com/sesquicadaver/MTDirector/issues/392) | CaptureSnapshot persist+audit UoW | **DONE** |
| 28 | SEC-13 | [#394](https://github.com/sesquicadaver/MTDirector/issues/394) | UpsertDeviceHashState UoW | **DONE** |
| 29 | SEC-14 | [#396](https://github.com/sesquicadaver/MTDirector/issues/396) | OpenEndpointPresence UoW | **DONE** |
| 30 | SEC-15 | [#398](https://github.com/sesquicadaver/MTDirector/issues/398) | UpsertRoutingAssuranceState UoW | **DONE** |
| 31 | W7-01 | [#401](https://github.com/sesquicadaver/MTDirector/issues/401) | Desktop MikroTik/Winbox display labels | **DONE** |
| 32 | W7-02 | [#402](https://github.com/sesquicadaver/MTDirector/issues/402) | Bind gRPC actor to authenticated principal | **DONE** |
| 33 | W7-03 | [#404](https://github.com/sesquicadaver/MTDirector/issues/404) | Controller Kestrel mTLS client certificates | **DONE** |
| 34 | W7-04 | [#406](https://github.com/sesquicadaver/MTDirector/issues/406) | Validate mTLS client certs against TrustedCa | **DONE** |
| 35 | W7-05 | [#409](https://github.com/sesquicadaver/MTDirector/issues/409) | Bind Desktop actor from client cert CN | **DONE** |
| 36 | W7-06 | [#411](https://github.com/sesquicadaver/MTDirector/issues/411) | Map mTLS client cert to HttpContext.User | **DONE** |
| 37 | W7-07 | [#413](https://github.com/sesquicadaver/MTDirector/issues/413) | Prefer HttpContext.User over gRPC peer identity for actor | **DONE** |
| 38 | W7-08 | [#415](https://github.com/sesquicadaver/MTDirector/issues/415) | Desktop status shows resolved mTLS actor | **DONE** |
| 39 | W7-09 | [#417](https://github.com/sesquicadaver/MTDirector/issues/417) | Production mTLS operator checklist | **DONE** |
| 40 | W7-10 | [#419](https://github.com/sesquicadaver/MTDirector/issues/419) | Log redacted client-cert thumbprint on mTLS principal map | **DONE** |
| 41 | W7-11 | [#421](https://github.com/sesquicadaver/MTDirector/issues/421) | Harden SessionFaultInjection timeout pending-clear assertion | **DONE** |
| 42 | W7-12 | [#423](https://github.com/sesquicadaver/MTDirector/issues/423) | Desktop status Living Spec covers AuthenticationFailed | **DONE** |
| 43 | W7-13 | [#425](https://github.com/sesquicadaver/MTDirector/issues/425) | mTLS map log includes request TraceIdentifier | **DONE** |
| 44 | W7-14 | [#427](https://github.com/sesquicadaver/MTDirector/issues/427) | SECURITY.md documents TraceIdentifier on mTLS map log | **DONE** |
| 45 | W7-15 | [#429](https://github.com/sesquicadaver/MTDirector/issues/429) | Pilot runbook TraceIdentifier for mTLS Connect correlation | **DONE** |
| 46 | W7-16 | [#431](https://github.com/sesquicadaver/MTDirector/issues/431) | controller-configuration.md TraceIdentifier cross-link | **DONE** |
| 47 | W7-17 | [#433](https://github.com/sesquicadaver/MTDirector/issues/433) | Lock resolve-only zone UoW residual Living Spec | **DONE** |
| 48 | W7-18 | [#435](https://github.com/sesquicadaver/MTDirector/issues/435) | Lock Start* AddOperationAsync outside-UoW residual Living Spec | **DONE** |
| 49 | W7-19 | [#437](https://github.com/sesquicadaver/MTDirector/issues/437) | Lock orchestrator-only audit residual Living Spec | **DONE** |
| 50 | W7-20 | [#439](https://github.com/sesquicadaver/MTDirector/issues/439) | Lock Feedback delivery / RouterOS capture outside-DB residual Living Spec | **DONE** |
| 51 | W7-21 | [#441](https://github.com/sesquicadaver/MTDirector/issues/441) | Lock IResponseFeedbackDeliveryPort not-configured residual Living Spec | **DONE** |
| 52 | W7-22 | [#443](https://github.com/sesquicadaver/MTDirector/issues/443) | Lock Desktop zip/tar installer packaging residual Living Spec | **DONE** |
| 53 | W7-23 | [#445](https://github.com/sesquicadaver/MTDirector/issues/445) | Lock SHA256SUMS attestation packaging residual Living Spec | **DONE** |
| 54 | W7-24 | [#447](https://github.com/sesquicadaver/MTDirector/issues/447) | Lock CycloneDX-lite SBOM packaging residual Living Spec | **DONE** |
| 55 | W7-25 | [#449](https://github.com/sesquicadaver/MTDirector/issues/449) | Lock MVP scope-lock no-NAT/RAW writes residual Living Spec | **DONE** |
| 56 | W7-26 | [#451](https://github.com/sesquicadaver/MTDirector/issues/451) | Lock MVP scope-lock no-campaigns/auto-deploy residual Living Spec | **DONE** |
| 57 | W7-27 | [#453](https://github.com/sesquicadaver/MTDirector/issues/453) | Lock Live CHR matrix OFF residual Living Spec | **DONE** |
| 58 | W7-28 | [#455](https://github.com/sesquicadaver/MTDirector/issues/455) | Lock Golden live CHR hashes env-gated residual Living Spec | **DONE** |
| 59 | W7-29 | [#457](https://github.com/sesquicadaver/MTDirector/issues/457) | Lock Live physical CRS hardware OFF ops residual Living Spec | **DONE** |
| 60 | W7-30 | [#459](https://github.com/sesquicadaver/MTDirector/issues/459) | Lock Controller migrate-only startup residual Living Spec | **DONE** |
| 61 | W7-31 | [#460](https://github.com/sesquicadaver/MTDirector/issues/460) | Lock Development master-key provider residual Living Spec | **DONE** |
| 62 | W7-32 | [#462](https://github.com/sesquicadaver/MTDirector/issues/462) | Lock GitHub-hosted CI billing-limited residual Living Spec | **DONE** |
| 63 | W7-33 | [#464](https://github.com/sesquicadaver/MTDirector/issues/464) | Seed next continuous residual after CI billing Living Spec | **DONE** |
| 64 | W7-34 | [#466](https://github.com/sesquicadaver/MTDirector/issues/466) | Lock Desktop window does not stop Controller residual Living Spec | **DONE** |
| 65 | W7-35 | [#468](https://github.com/sesquicadaver/MTDirector/issues/468) | Lock Inventory Add router wizard DONE residual Living Spec | **DONE** |
| 66 | W7-36 | [#470](https://github.com/sesquicadaver/MTDirector/issues/470) | Seed next continuous residual after Inventory Add router Living Spec | **DONE** |
| 67 | W7-37 | [#472](https://github.com/sesquicadaver/MTDirector/issues/472) | Lock gRPC remains available for automation residual Living Spec | **DONE** |
| 68 | W7-38 | [#474](https://github.com/sesquicadaver/MTDirector/issues/474) | Seed next continuous residual after gRPC automation Living Spec | **DONE** |
| 69 | W7-39 | [#476](https://github.com/sesquicadaver/MTDirector/issues/476) | Lock RouterOs Enabled default fail-closed residual Living Spec | **DONE** |
| 70 | W7-40 | [#478](https://github.com/sesquicadaver/MTDirector/issues/478) | Seed next continuous residual after RouterOs fail-closed Living Spec | **DONE** |
| 71 | W7-41 | [#480](https://github.com/sesquicadaver/MTDirector/issues/480) | Lock RouterOs WriteEnabled operator residual Living Spec | **DONE** |
| 72 | W7-42 | [#482](https://github.com/sesquicadaver/MTDirector/issues/482) | Seed next continuous residual after WriteEnabled Living Spec | **DONE** |
| 73 | W7-43 | [#484](https://github.com/sesquicadaver/MTDirector/issues/484) | Lock N1-07 path-class E2E DONE residual Living Spec | **DONE** |
| 74 | W7-44 | [#486](https://github.com/sesquicadaver/MTDirector/issues/486) | Seed next continuous residual after N1-07 Living Spec | **DONE** |
| 75 | W7-45 | [#488](https://github.com/sesquicadaver/MTDirector/issues/488) | Lock M7.1…M7.4 CLOSED residual Living Spec | **DONE** |
| 76 | W7-46 | [#490](https://github.com/sesquicadaver/MTDirector/issues/490) | Seed next continuous residual after M7 CLOSED Living Spec | **DONE** |
| 77 | W7-47 | [#492](https://github.com/sesquicadaver/MTDirector/issues/492) | Lock SEC-07…SEC-15 DONE residual Living Spec | **DONE** |
| 78 | W7-48 | [#494](https://github.com/sesquicadaver/MTDirector/issues/494) | Seed next continuous residual after SEC-07…15 Living Spec | **DONE** |
| 79 | W7-49 | [#496](https://github.com/sesquicadaver/MTDirector/issues/496) | Lock known-limitations residual Living Spec corpus COMPLETE | **DONE** |
| 80 | W7-50 | [#498](https://github.com/sesquicadaver/MTDirector/issues/498) | Seed next product tranche after residual corpus Living Spec | **DONE** |
| 81 | W7-51 | [#500](https://github.com/sesquicadaver/MTDirector/issues/500) | PLAN-03 — Inventory next quality-gate product tranche | **DONE** |
| 82 | W7-52 | [#502](https://github.com/sesquicadaver/MTDirector/issues/502) | QG-IMPORT-01 — Import graph + cycle anomaly Living Spec gate | **DONE** |
| 83 | W7-53 | [#504](https://github.com/sesquicadaver/MTDirector/issues/504) | QG-DOCS-01 — Weekly docs smoke Living Spec gate | **DONE** |
| 84 | W7-54 | [#506](https://github.com/sesquicadaver/MTDirector/issues/506) | QG-ANTISTUB-01 — Anti-stub CI Living Spec gate | **DONE** |
| 85 | W7-55 | [#508](https://github.com/sesquicadaver/MTDirector/issues/508) | QG-LIVESPEC-MATRIX-01 — Living Spec matrix PR gate | **DONE** |
| 86 | W7-56 | [#510](https://github.com/sesquicadaver/MTDirector/issues/510) | QG-SIGN-01 — Release signing checklist Living Spec gate | **DONE** |
| 87 | W7-57 | [#512](https://github.com/sesquicadaver/MTDirector/issues/512) | Seed next product tranche after PLAN-03 quality gates | **DONE** |
| 88 | W7-58 | [#514](https://github.com/sesquicadaver/MTDirector/issues/514) | PLAN-04 — Inventory next contract-test / API Living Spec product tranche | **DONE** |
| 89 | W7-59 | [#516](https://github.com/sesquicadaver/MTDirector/issues/516) | CT-DEPLOY-01 — DeploymentService GrpcHost contract Living Spec | **DONE** |
| 90 | W7-60 | [#518](https://github.com/sesquicadaver/MTDirector/issues/518) | CT-ZONE-01 — ZoneService GrpcHost contract Living Spec | **DONE** |
| 91 | W7-61 | [#520](https://github.com/sesquicadaver/MTDirector/issues/520) | CT-DRIFT-01 — DriftService GrpcHost contract Living Spec | **DONE** |
| 92 | W7-62 | [#522](https://github.com/sesquicadaver/MTDirector/issues/522) | CT-AUDIT-01 — AuditService GrpcHost contract Living Spec | **DONE** |
| 93 | W7-63 | [#524](https://github.com/sesquicadaver/MTDirector/issues/524) | CT-ROUTING-01 — RoutingAssuranceService ProtoContract + GrpcHost Living Spec | **DONE** |
| 94 | W7-64 | [#526](https://github.com/sesquicadaver/MTDirector/issues/526) | CT-INCIDENT-01 — IncidentService ProtoContract + GrpcHost Living Spec | **DONE** |
| 95 | W7-65 | [#528](https://github.com/sesquicadaver/MTDirector/issues/528) | Seed next product tranche after PLAN-04 → PLAN-05 Desktop operator-surface | **DONE** |
| 96 | W7-66 | [#530](https://github.com/sesquicadaver/MTDirector/issues/530) | PLAN-05 — Inventory next Desktop operator-surface Living Spec product tranche | **DONE** |
| 97 | W7-67 | [#532](https://github.com/sesquicadaver/MTDirector/issues/532) | DESK-AUDIT-01 — Desktop Audit panel Living Spec vs AuditGrpcHost | **DONE** |
| 98 | W7-68 | [#534](https://github.com/sesquicadaver/MTDirector/issues/534) | DESK-DRIFT-01 — Desktop Drift panel Living Spec vs DriftGrpcHost | **DONE** |
| 99 | W7-69 | [#536](https://github.com/sesquicadaver/MTDirector/issues/536) | DESK-ZONE-01 — Desktop Zones panel Living Spec vs ZoneGrpcHost | **DONE** |
| 100 | W7-70 | [#538](https://github.com/sesquicadaver/MTDirector/issues/538) | DESK-ROUTING-01 — Desktop Routing assurance Living Spec vs RoutingAssuranceGrpcHost | **DONE** |
| 101 | W7-71 | [#540](https://github.com/sesquicadaver/MTDirector/issues/540) | Seed next product tranche after PLAN-05 Desktop operator-surface | **DONE** |
| 102 | W7-72 | [#542](https://github.com/sesquicadaver/MTDirector/issues/542) | PLAN-06 — Inventory next Incident Desktop operator-surface Living Spec product tranche | **DONE** |
| 103 | W7-73 | [#544](https://github.com/sesquicadaver/MTDirector/issues/544) | DESK-INCIDENT-01 — Desktop Incident ViewModel + client Living Spec vs IncidentGrpcHost | **DONE** |
| 104 | W7-74 | [#546](https://github.com/sesquicadaver/MTDirector/issues/546) | DESK-INCIDENT-02 — Desktop Incident MainWindow panel Living Spec | **DONE** |
| 105 | W7-75 | [#548](https://github.com/sesquicadaver/MTDirector/issues/548) | DESK-INCIDENT-03 — Desktop Incident Bind assessment Living Spec | **DONE** |
| 106 | W7-76 | [#550](https://github.com/sesquicadaver/MTDirector/issues/550) | DESK-INCIDENT-04 — Desktop Incident fail-closed no deploy/overlay Living Spec | **DONE** |
| 107 | W7-77 | [#552](https://github.com/sesquicadaver/MTDirector/issues/552) | Seed next product tranche after PLAN-06 Incident Desktop operator-surface | **DONE** |
| 108 | W7-78 | [#554](https://github.com/sesquicadaver/MTDirector/issues/554) | PLAN-07 — Inventory next Core MVP Desktop operator-surface Living Spec product tranche | **DONE** |
| 109 | W7-79 | [#556](https://github.com/sesquicadaver/MTDirector/issues/556) | DESK-POLICY-01 — Desktop Policies panel Living Spec vs PolicyGrpcHost | **DONE** |
| 110 | W7-80 | [#558](https://github.com/sesquicadaver/MTDirector/issues/558) | Seed next PLAN-07 row after DESK-POLICY-01 → DESK-DEPLOY-01 | **DONE** |
| 111 | W7-81 | [#560](https://github.com/sesquicadaver/MTDirector/issues/560) | DESK-DEPLOY-01 — Desktop Deployment Living Spec vs DeploymentGrpcHost | **DONE** |
| 112 | W7-82 | [#562](https://github.com/sesquicadaver/MTDirector/issues/562) | Seed next PLAN-07 row after DESK-DEPLOY-01 → DESK-ONBOARD-01 | **DONE** |
| 113 | W7-83 | [#564](https://github.com/sesquicadaver/MTDirector/issues/564) | DESK-ONBOARD-01 — Desktop Onboarding Living Spec vs OnboardingGrpcHost | **DONE** |
| 114 | W7-84 | [#566](https://github.com/sesquicadaver/MTDirector/issues/566) | Seed next PLAN-07 row after DESK-ONBOARD-01 → DESK-SNAPSHOT-01 | **DONE** |
| 115 | W7-85 | [#568](https://github.com/sesquicadaver/MTDirector/issues/568) | DESK-SNAPSHOT-01 — Desktop Snapshot Living Spec vs SnapshotGrpcHost | **DONE** |
| 116 | W7-86 | [#570](https://github.com/sesquicadaver/MTDirector/issues/570) | Seed next PLAN-07 row after DESK-SNAPSHOT-01 → DESK-INVENTORY-01 | **DONE** |
| 117 | W7-87 | [#572](https://github.com/sesquicadaver/MTDirector/issues/572) | DESK-INVENTORY-01 — Desktop Inventory Living Spec vs InventoryGrpcHost | **DONE** |
| 118 | W7-88 | [#574](https://github.com/sesquicadaver/MTDirector/issues/574) | Seed next product tranche after PLAN-07 Core MVP Desktop | **DONE** |
| 119 | W7-89 | [#577](https://github.com/sesquicadaver/MTDirector/issues/577) | PLAN-08 — Inventory next Desktop secondary operator-surface Living Spec product tranche | **DONE** |
| 120 | W7-90 | [#578](https://github.com/sesquicadaver/MTDirector/issues/578) | DESK-NODE-01 — Desktop Node VRRP pair Living Spec vs InventoryGrpcHost | **DONE** |
| 121 | W7-91 | [#580](https://github.com/sesquicadaver/MTDirector/issues/580) | Seed next PLAN-08 row after DESK-NODE-01 → DESK-NBR-01 | **DONE** |
| 122 | W7-92 | [#582](https://github.com/sesquicadaver/MTDirector/issues/582) | DESK-NBR-01 — Desktop Neighbor candidates Living Spec depth | **DONE** |
| 123 | W7-93 | [#585](https://github.com/sesquicadaver/MTDirector/issues/585) | Seed next PLAN-08 row after DESK-NBR-01 → DESK-PROBE-01 | **DONE** |
| 124 | W7-94 | [#586](https://github.com/sesquicadaver/MTDirector/issues/586) | DESK-PROBE-01 — Desktop ValidateDeviceConnection probe Living Spec depth | **DONE** |
| 125 | W7-95 | [#589](https://github.com/sesquicadaver/MTDirector/issues/589) | Seed next PLAN-08 row after DESK-PROBE-01 → DESK-POLICY-02 | **DONE** |
| 126 | W7-96 | [#590](https://github.com/sesquicadaver/MTDirector/issues/590) | DESK-POLICY-02 — Desktop Policy safety analysis Living Spec depth | **DONE** |
| 127 | W7-97 | [#593](https://github.com/sesquicadaver/MTDirector/issues/593) | Seed next product tranche after PLAN-08 → PLAN-09 | **DONE** |
| 128 | W7-98 | [#594](https://github.com/sesquicadaver/MTDirector/issues/594) | PLAN-09 — Inventory next Desktop connection-status operator-surface Living Spec product tranche | **DONE** |
| 129 | W7-99 | [#597](https://github.com/sesquicadaver/MTDirector/issues/597) | DESK-CONN-01 — Desktop Connect/Disconnect Living Spec depth | **DONE** |
| 130 | W7-100 | [#598](https://github.com/sesquicadaver/MTDirector/issues/598) | Seed next PLAN-09 row after DESK-CONN-01 → DESK-MTLS-01 | **DONE** |
| 131 | W7-101 | [#600](https://github.com/sesquicadaver/MTDirector/issues/600) | DESK-MTLS-01 — Desktop mTLS actor status Living Spec depth | **DONE** |
| 132 | W7-102 | [#603](https://github.com/sesquicadaver/MTDirector/issues/603) | Seed next PLAN-09 row after DESK-MTLS-01 → DESK-AUTH-01 | **DONE** |
| 133 | W7-103 | [#604](https://github.com/sesquicadaver/MTDirector/issues/604) | DESK-AUTH-01 — Desktop AuthenticationFailed/TlsError Living Spec depth | **DONE** |
| 134 | W7-104 | [#607](https://github.com/sesquicadaver/MTDirector/issues/607) | Seed next product tranche after PLAN-09 → PLAN-10 | **DONE** |
| 135 | W7-105 | [#608](https://github.com/sesquicadaver/MTDirector/issues/608) | PLAN-10 — Inventory next Desktop shell chrome & Policies authoring depth Living Spec product tranche | **DONE** |
| 136 | W7-106 | [#611](https://github.com/sesquicadaver/MTDirector/issues/611) | DESK-SHELL-01 — Desktop Shell navigation/hotkeys Living Spec depth | **DONE** |
| 137 | W7-107 | [#612](https://github.com/sesquicadaver/MTDirector/issues/612) | Seed next PLAN-10 row after DESK-SHELL-01 → DESK-DIFF-01 | **DONE** |
| 138 | W7-108 | [#614](https://github.com/sesquicadaver/MTDirector/issues/614) | DESK-DIFF-01 — Desktop Policies Diff Living Spec depth | **DONE** |
| 139 | W7-109 | [#616](https://github.com/sesquicadaver/MTDirector/issues/616) | Seed next PLAN-10 row after DESK-DIFF-01 → DESK-REORDER-01 | **DONE** |
| 140 | W7-110 | [#617](https://github.com/sesquicadaver/MTDirector/issues/617) | DESK-REORDER-01 — Desktop Policies Move up/down Living Spec depth | **DONE** |
| 141 | W7-111 | [#620](https://github.com/sesquicadaver/MTDirector/issues/620) | Seed next product tranche after PLAN-10 → PLAN-11 | **DONE** |
| 142 | W7-112 | [#625](https://github.com/sesquicadaver/MTDirector/issues/625) | PLAN-11 — Inventory next Desktop Policies review-compose lifecycle Living Spec product tranche | **DONE** |
| 143 | W7-113 | [#626](https://github.com/sesquicadaver/MTDirector/issues/626) | Seed next PLAN-11 row after DESK-SUBMIT-01 → DESK-COMPOSE-01 | **DONE** |
| 144 | W7-114 | [#628](https://github.com/sesquicadaver/MTDirector/issues/628) | DESK-SUBMIT-01 — Desktop Policies SubmitForReview Living Spec depth | **DONE** |
| 145 | W7-115 | [#629](https://github.com/sesquicadaver/MTDirector/issues/629) | DESK-COMPOSE-01 — Desktop Policies Compose+RecordAnalysis Living Spec depth | **DONE** |
| 146 | W7-116 | [#630](https://github.com/sesquicadaver/MTDirector/issues/630) | DESK-GATE-01 — Desktop Policies Approve/Bind/Compile Living Spec depth | **DONE** |
| 148 | W7-118 | [#637](https://github.com/sesquicadaver/MTDirector/issues/637) | Seed next product tranche after PLAN-11 → PLAN-12 | **DONE** |
| 149 | W7-119 | [#638](https://github.com/sesquicadaver/MTDirector/issues/638) | PLAN-12 — Inventory next Desktop Policies residual lifecycle Living Spec product tranche | **DONE** |
| 150 | W7-120 | [#641](https://github.com/sesquicadaver/MTDirector/issues/641) | DESK-ACK-01 — Desktop Policies AcknowledgeWarning Living Spec depth | **DONE** |
| 151 | W7-121 | [#642](https://github.com/sesquicadaver/MTDirector/issues/642) | Seed next PLAN-12 row after DESK-ACK-01 → DESK-DRAFT-01 | **DONE** |
| 152 | W7-122 | [#643](https://github.com/sesquicadaver/MTDirector/issues/643) | DESK-DRAFT-01 — Desktop Policies Create/Load draft Living Spec depth | **DONE** |
| 153 | W7-123 | [#644](https://github.com/sesquicadaver/MTDirector/issues/644) | Seed next PLAN-12 row after DESK-DRAFT-01 → DESK-CATALOG-01 | **DONE** |
| 154 | W7-124 | [#645](https://github.com/sesquicadaver/MTDirector/issues/645) | DESK-CATALOG-01 — Desktop Policies Catalog refresh Living Spec depth | **DONE** |
| 155 | W7-125 | [#653](https://github.com/sesquicadaver/MTDirector/issues/653) | Seed next product tranche after PLAN-12 → PLAN-13 | **DONE** |
| 156 | W7-126 | [#654](https://github.com/sesquicadaver/MTDirector/issues/654) | PLAN-13 — Inventory Desktop layout density Living Spec product tranche | **DONE** |
| 157 | W7-127 | [#657](https://github.com/sesquicadaver/MTDirector/issues/657) | DESK-LAYOUT-00 — Shared layout tokens + desktop-layout.md Living Spec | **DONE** |
| 158 | W7-128 | [#658](https://github.com/sesquicadaver/MTDirector/issues/658) | Seed next PLAN-13 row after DESK-LAYOUT-00 → DESK-LAYOUT-01 | **DONE** |
| 159 | W7-129 | [#659](https://github.com/sesquicadaver/MTDirector/issues/659) | DESK-LAYOUT-01 — Snapshot tab primary pane + splitter Living Spec | **DONE** |
| 160 | W7-130 | [#663](https://github.com/sesquicadaver/MTDirector/issues/663) | Seed next PLAN-13 row after DESK-LAYOUT-01 → DESK-LAYOUT-02 | **DONE** |
| 161 | W7-131 | [#664](https://github.com/sesquicadaver/MTDirector/issues/664) | DESK-LAYOUT-02 — Semantic Diff entry list + splitter Living Spec | **DONE** |
| 162 | W7-132 | [#667](https://github.com/sesquicadaver/MTDirector/issues/667) | Seed next PLAN-13 row after DESK-LAYOUT-02 → DESK-LAYOUT-03 | **DONE** |
| 163 | W7-133 | [#668](https://github.com/sesquicadaver/MTDirector/issues/668) | DESK-LAYOUT-03 — Drift events/findings/detail splitter Living Spec | **DONE** |
| 164 | W7-134 | [#671](https://github.com/sesquicadaver/MTDirector/issues/671) | Seed next PLAN-13 row after DESK-LAYOUT-03 → DESK-LAYOUT-04 | **DONE** |
| 165 | W7-135 | [#672](https://github.com/sesquicadaver/MTDirector/issues/672) | DESK-LAYOUT-04 — Audit event list + payload splitter Living Spec | **DONE** |
| 166 | W7-136 | [#675](https://github.com/sesquicadaver/MTDirector/issues/675) | Seed next PLAN-13 row after DESK-LAYOUT-04 → DESK-LAYOUT-05 | **DONE** |
| 167 | W7-137 | [#676](https://github.com/sesquicadaver/MTDirector/issues/676) | DESK-LAYOUT-05 — Policies MaxHeight cascade Living Spec | **DONE** |
| 168 | W7-138 | [#679](https://github.com/sesquicadaver/MTDirector/issues/679) | Seed next PLAN-13 row after DESK-LAYOUT-05 → DESK-LAYOUT-06 | **DONE** |
| 169 | W7-139 | [#680](https://github.com/sesquicadaver/MTDirector/issues/680) | DESK-LAYOUT-06 — Node + RoutingAssurance MaxHeight Living Spec | **DONE** |
| 170 | W7-140 | [#683](https://github.com/sesquicadaver/MTDirector/issues/683) | Seed next PLAN-13 row after DESK-LAYOUT-06 → DESK-LAYOUT-07 | **DONE** |
| 171 | W7-141 | [#684](https://github.com/sesquicadaver/MTDirector/issues/684) | DESK-LAYOUT-07 — Operations Onboarding/Deploy MaxHeight Living Spec | **DONE** |
| 172 | W7-142 | [#687](https://github.com/sesquicadaver/MTDirector/issues/687) | Seed next PLAN-13 row after DESK-LAYOUT-07 → DESK-LAYOUT-08 | **DONE** |
| 173 | W7-143 | [#688](https://github.com/sesquicadaver/MTDirector/issues/688) | DESK-LAYOUT-08 — Shell chrome column splitter Living Spec | **DONE** |
| 174 | W7-144 | [#691](https://github.com/sesquicadaver/MTDirector/issues/691) | Seed next PLAN-13 row after DESK-LAYOUT-08 → DESK-LAYOUT-09 | **DONE** |
| 175 | W7-145 | [#692](https://github.com/sesquicadaver/MTDirector/issues/692) | DESK-LAYOUT-09 — Inventory/Zones MaxHeight frames Living Spec | **DONE** |
| 176 | W7-146 | [#695](https://github.com/sesquicadaver/MTDirector/issues/695) | Seed next PLAN-13 row after DESK-LAYOUT-09 → DESK-LAYOUT-10 | **DONE** |
| 177 | W7-147 | [#696](https://github.com/sesquicadaver/MTDirector/issues/696) | DESK-LAYOUT-10 — PLAN-13 regression lock + docs sync Living Spec | **DONE** |
| 178 | W7-148 | [#699](https://github.com/sesquicadaver/MTDirector/issues/699) | Seed next product tranche after PLAN-13 → PLAN-14 | **DONE** |
| 179 | W7-149 | [#700](https://github.com/sesquicadaver/MTDirector/issues/700) | PLAN-14 — Inventory Desktop Avalonia PlaceholderText / Incident surface Living Spec product tranche | **DONE** |
| 180 | W7-150 | [#703](https://github.com/sesquicadaver/MTDirector/issues/703) | DESK-PLACEHOLDER-01 — Incident TextBox Watermark→PlaceholderText Living Spec | **DONE** |
| 181 | W7-151 | [#704](https://github.com/sesquicadaver/MTDirector/issues/704) | Seed next PLAN-14 row after DESK-PLACEHOLDER-01 → DESK-PLACEHOLDER-02 | **DONE** |
| 182 | W7-152 | [#707](https://github.com/sesquicadaver/MTDirector/issues/707) | DESK-PLACEHOLDER-02 — Repo-wide Desktop XAML Watermark residue Living Spec | **DONE** |
| 183 | W7-153 | [#709](https://github.com/sesquicadaver/MTDirector/issues/709) | Seed next product tranche after PLAN-14 → PLAN-15 | **DONE** |
| 184 | W7-154 | [#710](https://github.com/sesquicadaver/MTDirector/issues/710) | PLAN-15 — Inventory Desktop Incident mfc-field style hygiene Living Spec product tranche | **DONE** |
| 185 | W7-155 | [#713](https://github.com/sesquicadaver/MTDirector/issues/713) | DESK-FIELD-01 — Incident TextBox mfc-field Classes Living Spec | **DONE** |
| 186 | W7-156 | [#714](https://github.com/sesquicadaver/MTDirector/issues/714) | Seed next PLAN-15 row after DESK-FIELD-01 → DESK-FIELD-02 | **DONE** |
| 187 | W7-157 | [#717](https://github.com/sesquicadaver/MTDirector/issues/717) | DESK-FIELD-02 — Incident PlaceholderText + mfc-field regression Living Spec | **DONE** |
| 188 | W7-158 | [#719](https://github.com/sesquicadaver/MTDirector/issues/719) | Seed next product tranche after PLAN-15 → PLAN-16 | **DONE** |
| 189 | W7-159 | [#720](https://github.com/sesquicadaver/MTDirector/issues/720) | PLAN-16 — Inventory Desktop Incident AutomationProperties accessible-name Living Spec product tranche | **DONE** |
| 190 | W7-160 | [#723](https://github.com/sesquicadaver/MTDirector/issues/723) | DESK-A11Y-01 — Incident TextBox AutomationProperties.Name Living Spec | **DONE** |
| 191 | W7-161 | [#724](https://github.com/sesquicadaver/MTDirector/issues/724) | Seed next PLAN-16 row after DESK-A11Y-01 → DESK-A11Y-02 | **DONE** |
| 192 | W7-162 | [#727](https://github.com/sesquicadaver/MTDirector/issues/727) | DESK-A11Y-02 — Incident Names + PlaceholderText + mfc-field regression Living Spec | **DONE** |
| 193 | W7-163 | [#729](https://github.com/sesquicadaver/MTDirector/issues/729) | Seed next product tranche after PLAN-16 → PLAN-17 | **DONE** |
| 194 | W7-164 | [#730](https://github.com/sesquicadaver/MTDirector/issues/730) | PLAN-17 — Inventory Desktop Incident bind-action AutomationProperties Living Spec product tranche | **DONE** |
| 195 | W7-165 | [#733](https://github.com/sesquicadaver/MTDirector/issues/733) | DESK-A11Y-ACTION-01 — Bind assessment AutomationProperties.Name Living Spec | **DONE** |
| 196 | W7-166 | [#734](https://github.com/sesquicadaver/MTDirector/issues/734) | Seed next PLAN-17 row after DESK-A11Y-ACTION-01 → DESK-A11Y-ACTION-02 | **DONE** |
| 197 | W7-167 | [#737](https://github.com/sesquicadaver/MTDirector/issues/737) | DESK-A11Y-ACTION-02 — Bind Name + Incident field Names regression Living Spec | **DONE** |
| 198 | W7-168 | [#739](https://github.com/sesquicadaver/MTDirector/issues/739) | Seed next product tranche after PLAN-17 → PLAN-18 | **DONE** |
| 199 | W7-169 | [#740](https://github.com/sesquicadaver/MTDirector/issues/740) | PLAN-18 — Inventory Desktop Incident ingest-action AutomationProperties Living Spec product tranche | **DONE** |
| 200 | W7-170 | [#743](https://github.com/sesquicadaver/MTDirector/issues/743) | DESK-A11Y-INGEST-01 — Ingest signal AutomationProperties.Name Living Spec | **DONE** |
| 201 | W7-171 | [#744](https://github.com/sesquicadaver/MTDirector/issues/744) | Seed next PLAN-18 row after DESK-A11Y-INGEST-01 → DESK-A11Y-INGEST-02 | **DONE** |
| 202 | W7-172 | [#747](https://github.com/sesquicadaver/MTDirector/issues/747) | DESK-A11Y-INGEST-02 — Ingest Name + Bind Name + Incident field Names regression Living Spec | **DONE** |
| 203 | W7-173 | [#749](https://github.com/sesquicadaver/MTDirector/issues/749) | Seed next product tranche after PLAN-18 → PLAN-19 | **DONE** |
| 204 | W7-174 | [#750](https://github.com/sesquicadaver/MTDirector/issues/750) | PLAN-19 — Inventory Desktop shell Connect/Disconnect AutomationProperties Living Spec product tranche | **DONE** |
| 205 | W7-175 | [#753](https://github.com/sesquicadaver/MTDirector/issues/753) | DESK-A11Y-CONN-01 — Connect/Disconnect AutomationProperties.Name Living Spec | **DONE** |
| 206 | W7-176 | [#754](https://github.com/sesquicadaver/MTDirector/issues/754) | Seed next PLAN-19 row after DESK-A11Y-CONN-01 → DESK-A11Y-CONN-02 | **DONE** |
| 207 | W7-177 | [#757](https://github.com/sesquicadaver/MTDirector/issues/757) | DESK-A11Y-CONN-02 — Connect/Disconnect Names + Incident action Names regression Living Spec | **DONE** |
| 208 | W7-178 | [#759](https://github.com/sesquicadaver/MTDirector/issues/759) | Seed next product tranche after PLAN-19 → PLAN-20 | **DONE** |
| 209 | W7-179 | [#760](https://github.com/sesquicadaver/MTDirector/issues/760) | PLAN-20 — Inventory Desktop Policies lifecycle-action AutomationProperties Living Spec product tranche | **DONE** |
| 210 | W7-180 | [#763](https://github.com/sesquicadaver/MTDirector/issues/763) | DESK-A11Y-POLICY-01 — Policies lifecycle AutomationProperties.Name Living Spec | **DONE** |
| 211 | W7-181 | [#764](https://github.com/sesquicadaver/MTDirector/issues/764) | Seed next PLAN-20 row after DESK-A11Y-POLICY-01 → DESK-A11Y-POLICY-02 | **DONE** |
| 212 | W7-182 | [#767](https://github.com/sesquicadaver/MTDirector/issues/767) | DESK-A11Y-POLICY-02 — Policies lifecycle Names + shell/Incident Names regression Living Spec | **DONE** |
| 213 | W7-183 | [#769](https://github.com/sesquicadaver/MTDirector/issues/769) | Seed next product tranche after PLAN-20 → PLAN-21 | **DONE** |
| 214 | W7-184 | [#770](https://github.com/sesquicadaver/MTDirector/issues/770) | PLAN-21 — Inventory next Desktop Living Spec product tranche after PLAN-20 | **DONE** |
| 215 | W7-185 | [#773](https://github.com/sesquicadaver/MTDirector/issues/773) | DESK-A11Y-POLICY-EDIT-01 — Policies authoring residual AutomationProperties.Name Living Spec | **DONE** |
| 216 | W7-186 | [#774](https://github.com/sesquicadaver/MTDirector/issues/774) | Seed next PLAN-21 row after DESK-A11Y-POLICY-EDIT-01 → DESK-A11Y-POLICY-EDIT-02 | **DONE** |
| 217 | W7-187 | [#777](https://github.com/sesquicadaver/MTDirector/issues/777) | DESK-A11Y-POLICY-EDIT-02 — Policies authoring residual Names + lifecycle + shell/Incident Names regression Living Spec | **DONE** |
| 218 | W7-188 | [#779](https://github.com/sesquicadaver/MTDirector/issues/779) | Seed next product tranche after PLAN-21 → PLAN-22 | **DONE** |
| 219 | W7-189 | [#780](https://github.com/sesquicadaver/MTDirector/issues/780) | PLAN-22 — Inventory next Desktop Living Spec product tranche after PLAN-21 | **DONE** |
| 220 | W7-190 | [#783](https://github.com/sesquicadaver/MTDirector/issues/783) | DESK-A11Y-POLICY-ACK-01 — Policies Record analysis / Acknowledge warning AutomationProperties.Name | **DONE** |
| 221 | W7-191 | [#784](https://github.com/sesquicadaver/MTDirector/issues/784) | Seed next PLAN-22 row after DESK-A11Y-POLICY-ACK-01 → DESK-A11Y-POLICY-ACK-02 | **DONE** |
| 222 | W7-192 | [#787](https://github.com/sesquicadaver/MTDirector/issues/787) | DESK-A11Y-POLICY-ACK-02 — Policies ack/record Names + authoring residual + lifecycle + shell/Incident Names regression Living Spec | **DONE** |
| 223 | W7-193 | [#789](https://github.com/sesquicadaver/MTDirector/issues/789) | Seed next product tranche after PLAN-22 → PLAN-23 | **DONE** |
| 224 | W7-194 | [#790](https://github.com/sesquicadaver/MTDirector/issues/790) | PLAN-23 — Inventory next Desktop Living Spec product tranche after PLAN-22 | **DONE** |
| 225 | W7-195 | [#793](https://github.com/sesquicadaver/MTDirector/issues/793) | DESK-A11Y-POLICY-OBJ-01 — Policies catalog/object AutomationProperties.Name | **DONE** |
| 226 | W7-196 | [#794](https://github.com/sesquicadaver/MTDirector/issues/794) | Seed next PLAN-23 row after DESK-A11Y-POLICY-OBJ-01 → DESK-A11Y-POLICY-OBJ-02 | **DONE** |
| 227 | W7-197 | [#797](https://github.com/sesquicadaver/MTDirector/issues/797) | DESK-A11Y-POLICY-OBJ-02 — Policies catalog/object Names + ack/record + authoring residual + lifecycle + shell/Incident Names regression Living Spec | **DONE** |
| 228 | W7-198 | [#799](https://github.com/sesquicadaver/MTDirector/issues/799) | Seed next product tranche after PLAN-23 → PLAN-24 | **DONE** |
| 229 | W7-199 | [#800](https://github.com/sesquicadaver/MTDirector/issues/800) | PLAN-24 — Inventory next Desktop Living Spec product tranche after PLAN-23 | **DONE** |
| 230 | W7-200 | [#803](https://github.com/sesquicadaver/MTDirector/issues/803) | DESK-A11Y-OPS-01 — Onboarding/Deployment primary actions AutomationProperties.Name | **DONE** |
| 231 | W7-201 | [#804](https://github.com/sesquicadaver/MTDirector/issues/804) | Seed next PLAN-24 row after DESK-A11Y-OPS-01 → DESK-A11Y-OPS-02 | **DONE** |
| 232 | W7-202 | [#807](https://github.com/sesquicadaver/MTDirector/issues/807) | DESK-A11Y-OPS-02 — Onboarding/Deployment Names + Policies/shell/Incident Names regression Living Spec | **DONE** |
| 233 | W7-203 | [#809](https://github.com/sesquicadaver/MTDirector/issues/809) | Seed next product tranche after PLAN-24 → PLAN-25 | **DONE** |
| 234 | W7-204 | [#810](https://github.com/sesquicadaver/MTDirector/issues/810) | PLAN-25 — Inventory next Desktop Living Spec product tranche after PLAN-24 | **DONE** |
| 237 | W7-207 | [#817](https://github.com/sesquicadaver/MTDirector/issues/817) | DESK-A11Y-INV-01 — Inventory/Zones primary actions AutomationProperties.Name | **DONE** |
| 238 | W7-208 | [#818](https://github.com/sesquicadaver/MTDirector/issues/818) | Seed next PLAN-25 row after DESK-A11Y-INV-01 → DESK-A11Y-INV-02 | **DONE** |
| 239 | W7-209 | [#821](https://github.com/sesquicadaver/MTDirector/issues/821) | DESK-A11Y-INV-02 — Inventory/Zones Names + ops + Policies/shell/Incident Names regression Living Spec | **DONE** |
| 235 | W7-205 | [#814](https://github.com/sesquicadaver/MTDirector/issues/814) | Seed next product tranche after PLAN-25 → PLAN-26 | **DONE** |
| 236 | W7-206 | [#815](https://github.com/sesquicadaver/MTDirector/issues/815) | PLAN-26 — Inventory code-audit remediation tranche (11cb746) | **DONE** |
| 240 | W7-210 | [#825](https://github.com/sesquicadaver/MTDirector/issues/825) | AUDIT-RULE-01 — Update rule predicate / hidden fields round-trip | **DONE** |
| 241 | W7-211 | [#826](https://github.com/sesquicadaver/MTDirector/issues/826) | Seed next PLAN-26 row after AUDIT-RULE-01 → AUDIT-CTX-01 | **DONE** |
| 242 | W7-212 | [#829](https://github.com/sesquicadaver/MTDirector/issues/829) | AUDIT-CTX-01 — Node switch must invalidate mutation context | **DONE** |
| 243 | W7-213 | [#830](https://github.com/sesquicadaver/MTDirector/issues/830) | Seed next PLAN-26 row after AUDIT-CTX-01 → AUDIT-CAP-01 | **DONE** |
| 244 | W7-214 | [#833](https://github.com/sesquicadaver/MTDirector/issues/833) | AUDIT-CAP-01 — Canonical filter must include firewall match fields | **DONE** |
| 245 | W7-215 | [#834](https://github.com/sesquicadaver/MTDirector/issues/834) | Seed next PLAN-26 row after AUDIT-CAP-01 → AUDIT-CAP-02 | **DONE** |
| 246 | W7-216 | [#837](https://github.com/sesquicadaver/MTDirector/issues/837) | AUDIT-CAP-02 — Required-section read failure must not complete snapshot | **DONE** |
| 247 | W7-217 | [#838](https://github.com/sesquicadaver/MTDirector/issues/838) | Seed next PLAN-26 row after AUDIT-CAP-02 → AUDIT-AN-01 | **DONE** |
| 248 | W7-218 | [#841](https://github.com/sesquicadaver/MTDirector/issues/841) | AUDIT-AN-01 — Validate/Record must require full analysis + mandatory tests | **DONE** |
| 249 | W7-219 | [#842](https://github.com/sesquicadaver/MTDirector/issues/842) | Seed next PLAN-26 row after AUDIT-AN-01 → AUDIT-AN-02 | **DONE** |
| 250 | W7-220 | [#845](https://github.com/sesquicadaver/MTDirector/issues/845) | AUDIT-AN-02 — Analysis fingerprint CAS must use controller-computed value | **DONE** |
| 251 | W7-221 | [#846](https://github.com/sesquicadaver/MTDirector/issues/846) | Seed next PLAN-26 row after AUDIT-AN-02 → AUDIT-DIFF-01 | **DONE** |
| 252 | W7-222 | [#849](https://github.com/sesquicadaver/MTDirector/issues/849) | AUDIT-DIFF-01 — Semantic policy diff must surface reachability change | **DONE** |
| 253 | W7-223 | [#850](https://github.com/sesquicadaver/MTDirector/issues/850) | Seed next PLAN-26 row after AUDIT-DIFF-01 → AUDIT-GUARD-01 | **DONE** |
| 254 | W7-224 | [#855](https://github.com/sesquicadaver/MTDirector/issues/855) | AUDIT-GUARD-01 — ManagementPath must enforce complete guard contract | **DONE** |
| 255 | W7-225 | [#856](https://github.com/sesquicadaver/MTDirector/issues/856) | Seed next PLAN-26 row after AUDIT-GUARD-01 → AUDIT-DEP-01 | **DONE** |
| 256 | W7-226 | [#859](https://github.com/sesquicadaver/MTDirector/issues/859) | AUDIT-DEP-01 — Background recovery must not race active deployment | **DONE** |
| 257 | W7-227 | [#860](https://github.com/sesquicadaver/MTDirector/issues/860) | Seed next PLAN-26 row after AUDIT-DEP-01 → AUDIT-DEP-02 | **DONE** |
| 258 | W7-228 | [#863](https://github.com/sesquicadaver/MTDirector/issues/863) | AUDIT-DEP-02 — Watchdog durable clock/TTL; cleanup result must not be ignored | **DONE** |
| 259 | W7-229 | [#864](https://github.com/sesquicadaver/MTDirector/issues/864) | Seed next PLAN-26 row after AUDIT-DEP-02 → AUDIT-DEP-03 | **DONE** |
| 260 | W7-230 | [#867](https://github.com/sesquicadaver/MTDirector/issues/867) | AUDIT-DEP-03 — Fake VRRP reachability/traffic facts | **DONE** |
| 261 | W7-231 | [#868](https://github.com/sesquicadaver/MTDirector/issues/868) | Seed next PLAN-26 row after AUDIT-DEP-03 → AUDIT-GUI-01 | **DONE** |
| 262 | W7-232 | [#871](https://github.com/sesquicadaver/MTDirector/issues/871) | AUDIT-GUI-01 — Onboarding/Deployment synthetic payloads; Deploy never enables | **DONE** |
| 263 | W7-233 | [#872](https://github.com/sesquicadaver/MTDirector/issues/872) | Seed next PLAN-26 row after AUDIT-GUI-01 → AUDIT-AUTH-01 | **DONE** |
| 264 | W7-234 | [#875](https://github.com/sesquicadaver/MTDirector/issues/875) | AUDIT-AUTH-01 — Production operator authorization DenyAll | **DONE** |
| 265 | W7-235 | [#876](https://github.com/sesquicadaver/MTDirector/issues/876) | Seed next PLAN-26 row after AUDIT-AUTH-01 → AUDIT-INT-01 | **DONE** |
| 266 | W7-236 | [#879](https://github.com/sesquicadaver/MTDirector/issues/879) | AUDIT-INT-01 — FastTrack topology / verification session disposal / progress Watch auth hubs | **DONE** |
| 267 | W7-237 | [#880](https://github.com/sesquicadaver/MTDirector/issues/880) | Seed next after AUDIT-INT-01 (PLAN-26 COMPLETE) | **DONE** |
| 268 | W7-238 | [#883](https://github.com/sesquicadaver/MTDirector/issues/883) | PLAN-27 — Inventory Desktop Snapshot/Node/Drift/Audit AutomationProperties residual tranche after PLAN-26 | **DONE** |
| 269 | W7-239 | [#884](https://github.com/sesquicadaver/MTDirector/issues/884) | Seed first PLAN-27 atomic row after inventory → DESK-A11Y-SNAP-01 | **DONE** |
| 270 | W7-240 | [#886](https://github.com/sesquicadaver/MTDirector/issues/886) | DESK-A11Y-SNAP-01 — Snapshot Capture/Reload/Compare/Copy AutomationProperties.Name | **DONE** |
| 271 | W7-241 | [#887](https://github.com/sesquicadaver/MTDirector/issues/887) | Seed next PLAN-27 row after DESK-A11Y-SNAP-01 → DESK-A11Y-PANEL-01 | **DONE** |
| 272 | W7-242 | [#891](https://github.com/sesquicadaver/MTDirector/issues/891) | DESK-A11Y-PANEL-01 — Node Refresh / Validate / Drift / Audit Refresh AutomationProperties.Name | **DONE** |
| 273 | W7-243 | [#892](https://github.com/sesquicadaver/MTDirector/issues/892) | Seed next after DESK-A11Y-PANEL-01 (PLAN-27 COMPLETE) | **DONE** |
| 274 | W7-244 | [#895](https://github.com/sesquicadaver/MTDirector/issues/895) | PLAN-28 — Inventory Desktop residual field/control AutomationProperties tranche after PLAN-27 | **DONE** |
| 275 | W7-245 | [#896](https://github.com/sesquicadaver/MTDirector/issues/896) | Seed first PLAN-28 atomic row after inventory → DESK-A11Y-FIELD-01 | **DONE** |
| 276 | W7-246 | [#898](https://github.com/sesquicadaver/MTDirector/issues/898) | DESK-A11Y-FIELD-01 — Zones / Policies draft TextBox AutomationProperties.Name | **DONE** |
| 277 | W7-247 | [#899](https://github.com/sesquicadaver/MTDirector/issues/899) | Seed next PLAN-28 row after DESK-A11Y-FIELD-01 → DESK-A11Y-CTRL-01 | **DONE** |
| 278 | W7-248 | [#903](https://github.com/sesquicadaver/MTDirector/issues/903) | DESK-A11Y-CTRL-01 — Snapshot/Diff ComboBox & CheckBox + TabItem AutomationProperties.Name | **DONE** |
| 279 | W7-249 | [#904](https://github.com/sesquicadaver/MTDirector/issues/904) | Seed next after DESK-A11Y-CTRL-01 (PLAN-28 COMPLETE) | **DONE** |
| 280 | W7-250 | [#907](https://github.com/sesquicadaver/MTDirector/issues/907) | PLAN-29 — Inventory Desktop connection health / reconnect after Controller stop (AUDIT §18 residual) | **DONE** |
| 281 | W7-251 | [#908](https://github.com/sesquicadaver/MTDirector/issues/908) | Seed first PLAN-29 atomic row after inventory → DESK-CONN-HEALTH-01 | **DONE** |
| 282 | W7-252 | [#910](https://github.com/sesquicadaver/MTDirector/issues/910) | DESK-CONN-HEALTH-01 — Connected-state periodic gRPC health probe after Controller stop | **DONE** |
| 283 | W7-253 | [#911](https://github.com/sesquicadaver/MTDirector/issues/911) | Seed next PLAN-29 row after DESK-CONN-HEALTH-01 → DESK-CONN-RECONNECT-01 | **DONE** |
| 284 | W7-254 | [#915](https://github.com/sesquicadaver/MTDirector/issues/915) | DESK-CONN-RECONNECT-01 — Bounded reconnect after health-fail drop + shell StatusText/LastError sync | **DONE** |
| 285 | W7-255 | [#916](https://github.com/sesquicadaver/MTDirector/issues/916) | Seed next after DESK-CONN-RECONNECT-01 (PLAN-29 COMPLETE) | **DONE** |
| 286 | W7-256 | [#919](https://github.com/sesquicadaver/MTDirector/issues/919) | PLAN-30 — Inventory Watch operation-owner ACL / hub slow-subscriber backpressure (AUDIT §19 residual) | **DONE** |
| 287 | W7-257 | [#920](https://github.com/sesquicadaver/MTDirector/issues/920) | Seed first PLAN-30 atomic row after inventory → WATCH-OWN-01 | **DONE** |
| 288 | W7-258 | [#922](https://github.com/sesquicadaver/MTDirector/issues/922) | WATCH-OWN-01 — Bind Watch RPCs to operation owner beyond Read permission | **DONE** |
| 289 | W7-259 | [#923](https://github.com/sesquicadaver/MTDirector/issues/923) | Seed next PLAN-30 row after WATCH-OWN-01 → WATCH-BP-01 | **DONE** |
| 290 | W7-260 | [#927](https://github.com/sesquicadaver/MTDirector/issues/927) | WATCH-BP-01 — Bounded ProgressHub subscriber channels / slow-subscriber backpressure + live `_history` cap | **DONE** |
| 291 | W7-261 | [#928](https://github.com/sesquicadaver/MTDirector/issues/928) | Seed next after WATCH-BP-01 (PLAN-30 COMPLETE) | **DONE** |
| 292 | W7-262 | [#931](https://github.com/sesquicadaver/MTDirector/issues/931) | PLAN-31 — Inventory Desktop residual ListBox / Drift–Audit read-only a11y (PLAN-28 deferred) | **DONE** |
| 293 | W7-263 | [#932](https://github.com/sesquicadaver/MTDirector/issues/932) | Seed first PLAN-31 atomic row after inventory → DESK-A11Y-LIST-01 | **DONE** |
| 294 | W7-264 | [#934](https://github.com/sesquicadaver/MTDirector/issues/934) | DESK-A11Y-LIST-01 — ListBox host AutomationProperties.Name across operator browse/select surfaces | **DONE** |
| 295 | W7-265 | [#935](https://github.com/sesquicadaver/MTDirector/issues/935) | Seed next PLAN-31 row after DESK-A11Y-LIST-01 → DESK-A11Y-RO-01 | **DONE** |
| 296 | W7-266 | [#939](https://github.com/sesquicadaver/MTDirector/issues/939) | DESK-A11Y-RO-01 — Drift SemanticDiff + Audit PayloadJson read-only TextBox AutomationProperties.Name | **DONE** |
| 297 | W7-267 | [#940](https://github.com/sesquicadaver/MTDirector/issues/940) | Seed next after DESK-A11Y-RO-01 (PLAN-31 COMPLETE) | **DONE** |
| 298 | W7-268 | [#943](https://github.com/sesquicadaver/MTDirector/issues/943) | PLAN-32 — Inventory Controller host-process packaging templates (systemd / Windows Service) | **DONE** |
| 299 | W7-269 | [#944](https://github.com/sesquicadaver/MTDirector/issues/944) | Seed first PLAN-32 atomic row after inventory → OPS-HOST-SYSTEMD-01 | **DONE** |
| 300 | W7-270 | [#946](https://github.com/sesquicadaver/MTDirector/issues/946) | OPS-HOST-SYSTEMD-01 — systemd unit template for framework-dependent Controller | **DONE** |
| 301 | W7-271 | [#947](https://github.com/sesquicadaver/MTDirector/issues/947) | Seed next PLAN-32 row after OPS-HOST-SYSTEMD-01 → OPS-HOST-WINSVC-01 | **DONE** |
| 302 | W7-272 | [#951](https://github.com/sesquicadaver/MTDirector/issues/951) | OPS-HOST-WINSVC-01 — Windows Service host template for framework-dependent Controller | **DONE** |
| 303 | W7-273 | [#952](https://github.com/sesquicadaver/MTDirector/issues/952) | Seed next after OPS-HOST-WINSVC-01 (PLAN-32 COMPLETE) | **DONE** |
| 304 | W7-274 | [#955](https://github.com/sesquicadaver/MTDirector/issues/955) | PLAN-33 — Inventory Desktop Inventory TreeView / residual TabControl a11y | **DONE** |
| 305 | W7-275 | [#956](https://github.com/sesquicadaver/MTDirector/issues/956) | Seed first PLAN-33 atomic row after inventory → DESK-A11Y-TREE-01 | **DONE** |
| 306 | W7-276 | [#958](https://github.com/sesquicadaver/MTDirector/issues/958) | DESK-A11Y-TREE-01 — Inventory TreeView AutomationProperties.Name | **DONE** |
| 307 | W7-277 | [#959](https://github.com/sesquicadaver/MTDirector/issues/959) | Seed next after DESK-A11Y-TREE-01 (PLAN-33 COMPLETE) | **DONE** |
| 308 | W7-278 | [#963](https://github.com/sesquicadaver/MTDirector/issues/963) | PLAN-34 — Inventory Desktop operator launch packaging templates (.desktop / Windows shortcut) | **DONE** |
| 309 | W7-279 | [#964](https://github.com/sesquicadaver/MTDirector/issues/964) | Seed first PLAN-34 atomic row after inventory → DESK-HOST-LINUX-01 | **DONE** |
| 310 | W7-280 | [#966](https://github.com/sesquicadaver/MTDirector/issues/966) | DESK-HOST-LINUX-01 — freedesktop .desktop template for framework-dependent Desktop | **DONE** |
| 311 | W7-281 | [#967](https://github.com/sesquicadaver/MTDirector/issues/967) | Seed next PLAN-34 row after DESK-HOST-LINUX-01 → DESK-HOST-WIN-01 | **DONE** |
| 312 | W7-282 | [#971](https://github.com/sesquicadaver/MTDirector/issues/971) | DESK-HOST-WIN-01 — Windows Start Menu shortcut sketch for framework-dependent Desktop | **DONE** |
| 313 | W7-283 | [#972](https://github.com/sesquicadaver/MTDirector/issues/972) | Seed next after DESK-HOST-WIN-01 (PLAN-34 COMPLETE) | **DONE** |
| 314 | W7-284 | [#975](https://github.com/sesquicadaver/MTDirector/issues/975) | PLAN-35 — Inventory Desktop launch-template publish bundling (package-desktop copies templates into OUT_DIR/desktop) | **DONE** |
| 315 | W7-285 | [#976](https://github.com/sesquicadaver/MTDirector/issues/976) | Seed first PLAN-35 atomic row after inventory → DESK-HOST-BUNDLE-01 | **DONE** |
| 316 | W7-286 | [#978](https://github.com/sesquicadaver/MTDirector/issues/978) | DESK-HOST-BUNDLE-01 — package-desktop copies launch templates into OUT_DIR/desktop | **DONE** |
| 317 | W7-287 | [#979](https://github.com/sesquicadaver/MTDirector/issues/979) | Seed next after DESK-HOST-BUNDLE-01 (PLAN-35 COMPLETE) | **DONE** |
| 318 | W7-288 | [#983](https://github.com/sesquicadaver/MTDirector/issues/983) | PLAN-36 — Inventory Controller host-template publish bundling (package-controller copies systemd/WinSW into OUT_DIR/controller) | **DONE** |
| 319 | W7-289 | [#984](https://github.com/sesquicadaver/MTDirector/issues/984) | Seed first PLAN-36 atomic row after inventory → OPS-HOST-BUNDLE-01 | **DONE** |
| 320 | W7-290 | [#986](https://github.com/sesquicadaver/MTDirector/issues/986) | OPS-HOST-BUNDLE-01 — package-controller copies systemd/WinSW into OUT_DIR/controller | **DONE** |
| 321 | W7-291 | [#987](https://github.com/sesquicadaver/MTDirector/issues/987) | Seed next after OPS-HOST-BUNDLE-01 (PLAN-36 COMPLETE) | **DONE** |
| 322 | W7-292 | [#991](https://github.com/sesquicadaver/MTDirector/issues/991) | PLAN-37 — Inventory Controller host env sample packaging (mfc-controller.env.example for EnvironmentFile) | **DONE** |
| 323 | W7-293 | [#992](https://github.com/sesquicadaver/MTDirector/issues/992) | Seed first PLAN-37 atomic row after inventory → OPS-HOST-ENV-01 | **DONE** |
| 324 | W7-294 | [#994](https://github.com/sesquicadaver/MTDirector/issues/994) | OPS-HOST-ENV-01 — author mfc-controller.env.example + docs + package-controller bundle | **DONE** |
| 325 | W7-295 | [#996](https://github.com/sesquicadaver/MTDirector/issues/996) | Seed next after OPS-HOST-ENV-01 (PLAN-37 COMPLETE) | **DONE** |
| 326 | W7-296 | [#999](https://github.com/sesquicadaver/MTDirector/issues/999) | PLAN-38 — Inventory Controller host sysusers/tmpfiles packaging (mfc user + /etc/mfc + /var/lib/mfc) | **DONE** |
| 327 | W7-297 | [#1000](https://github.com/sesquicadaver/MTDirector/issues/1000) | Seed first PLAN-38 atomic row after inventory → OPS-HOST-SYSUSERS-01 | **DONE** |
| 328 | W7-298 | [#1002](https://github.com/sesquicadaver/MTDirector/issues/1002) | OPS-HOST-SYSUSERS-01 — author sysusers.d/tmpfiles.d + docs + package-controller bundle | **DONE** |
| 329 | W7-299 | [#1004](https://github.com/sesquicadaver/MTDirector/issues/1004) | Seed next after OPS-HOST-SYSUSERS-01 (PLAN-38 COMPLETE) | **DONE** |
| 330 | W7-300 | [#1007](https://github.com/sesquicadaver/MTDirector/issues/1007) | PLAN-39 — Inventory Controller host operator doc packaging (Documentation=/usr/share/doc/mfc) | **DONE** |
| 331 | W7-301 | [#1008](https://github.com/sesquicadaver/MTDirector/issues/1008) | Seed first PLAN-39 atomic row after inventory → OPS-HOST-DOC-01 | **DONE** |
| 332 | W7-302 | [#1010](https://github.com/sesquicadaver/MTDirector/issues/1010) | OPS-HOST-DOC-01 — author packaging/doc/mfc/README.md + package-controller bundle | **DONE** |
| 333 | W7-303 | [#1012](https://github.com/sesquicadaver/MTDirector/issues/1012) | Seed next after OPS-HOST-DOC-01 (PLAN-39 COMPLETE) | **DONE** |
| 334 | W7-304 | [#1015](https://github.com/sesquicadaver/MTDirector/issues/1015) | PLAN-40 — Inventory Controller host journald/syslog identity (SyslogIdentifier) | **DONE** |
| 335 | W7-305 | [#1016](https://github.com/sesquicadaver/MTDirector/issues/1016) | Seed first PLAN-40 atomic row after inventory → OPS-HOST-LOG-01 | **DONE** |
| 336 | W7-306 | [#1018](https://github.com/sesquicadaver/MTDirector/issues/1018) | OPS-HOST-LOG-01 — SyslogIdentifier + journal stdout/stderr on mfc-controller.service | **DONE** |
| 337 | W7-307 | [#1020](https://github.com/sesquicadaver/MTDirector/issues/1020) | Seed next after OPS-HOST-LOG-01 (PLAN-40 COMPLETE) | **DONE** |
| 338 | W7-308 | [#1023](https://github.com/sesquicadaver/MTDirector/issues/1023) | PLAN-41 — Inventory release signing crypto (GPG/Sigstore beyond QG-SIGN-01) | **DONE** |
| 339 | W7-309 | [#1024](https://github.com/sesquicadaver/MTDirector/issues/1024) | Seed first PLAN-41 atomic row after inventory → QG-SIGN-02 | **DONE** |
| 340 | W7-310 | [#1026](https://github.com/sesquicadaver/MTDirector/issues/1026) | QG-SIGN-02 — Opt-in cryptographic signing gate (GPG/Sigstore) beyond QG-SIGN-01 | **DONE** |
| 341 | W7-311 | [#1028](https://github.com/sesquicadaver/MTDirector/issues/1028) | Seed next after QG-SIGN-02 (PLAN-41 COMPLETE) | **DONE** |
| 342 | W7-312 | [#1031](https://github.com/sesquicadaver/MTDirector/issues/1031) | PLAN-42 — Inventory Controller HTTP liveness/readiness probes beyond gRPC health | **DONE** |
| 343 | W7-313 | [#1032](https://github.com/sesquicadaver/MTDirector/issues/1032) | Seed first PLAN-42 atomic row after inventory → CTRL-HTTP-HEALTH-01 | **DONE** |
| 344 | W7-314 | [#1034](https://github.com/sesquicadaver/MTDirector/issues/1034) | CTRL-HTTP-HEALTH-01 — HTTP liveness/readiness probes beyond gRPC health | **DONE** |
| 345 | W7-315 | [#1036](https://github.com/sesquicadaver/MTDirector/issues/1036) | Seed next after CTRL-HTTP-HEALTH-01 (PLAN-42 COMPLETE) | **DONE** |
| 346 | W7-316 | [#1039](https://github.com/sesquicadaver/MTDirector/issues/1039) | PLAN-43 — Inventory Controller metrics/OpenTelemetry beyond HTTP health probes | **DONE** |
| 347 | W7-317 | [#1040](https://github.com/sesquicadaver/MTDirector/issues/1040) | Seed first PLAN-43 atomic row after inventory → CTRL-HTTP-METRICS-01 | **DONE** |
| 348 | W7-318 | [#1042](https://github.com/sesquicadaver/MTDirector/issues/1042) | CTRL-HTTP-METRICS-01 — Controller scrapeable Prometheus/OTel metrics beyond HTTP health | **DONE** |
| 349 | W7-319 | [#1044](https://github.com/sesquicadaver/MTDirector/issues/1044) | Seed next after CTRL-HTTP-METRICS-01 (PLAN-43 COMPLETE) | **DONE** |
| 350 | W7-320 | [#1047](https://github.com/sesquicadaver/MTDirector/issues/1047) | PLAN-44 — Inventory Controller OpenTelemetry tracing beyond metrics scrape | **DONE** |
| 351 | W7-321 | [#1048](https://github.com/sesquicadaver/MTDirector/issues/1048) | Seed first PLAN-44 atomic row after inventory → CTRL-HTTP-OTEL-TRACE-01 | **DONE** |
| 352 | W7-322 | [#1050](https://github.com/sesquicadaver/MTDirector/issues/1050) | CTRL-HTTP-OTEL-TRACE-01 — Controller opt-in OpenTelemetry tracing beyond metrics scrape | **DONE** |
| 353 | W7-323 | [#1052](https://github.com/sesquicadaver/MTDirector/issues/1052) | Seed next after CTRL-HTTP-OTEL-TRACE-01 (PLAN-44 COMPLETE) | **DONE** |
| 354 | W7-324 | [#1055](https://github.com/sesquicadaver/MTDirector/issues/1055) | PLAN-45 — Inventory Controller log↔trace correlation after OTel tracing | **DONE** |
| 355 | W7-325 | [#1056](https://github.com/sesquicadaver/MTDirector/issues/1056) | Seed first PLAN-45 atomic row after inventory → CTRL-LOG-OTEL-CORRELATE-01 | **DONE** |
| 356 | W7-326 | [#1058](https://github.com/sesquicadaver/MTDirector/issues/1058) | CTRL-LOG-OTEL-CORRELATE-01 — Enrich JSON console logs with Activity TraceId/SpanId | **DONE** |
| 357 | W7-327 | [#1060](https://github.com/sesquicadaver/MTDirector/issues/1060) | Seed next after CTRL-LOG-OTEL-CORRELATE-01 (PLAN-45 COMPLETE) | **DONE** |
| 358 | W7-328 | [#1063](https://github.com/sesquicadaver/MTDirector/issues/1063) | PLAN-46 — Inventory Controller OpenTelemetry resource identity after log↔trace correlation | **OPEN** |
| 359 | W7-329 | [#1064](https://github.com/sesquicadaver/MTDirector/issues/1064) | Seed first PLAN-46 atomic row after inventory → CTRL-HTTP-OTEL-RESOURCE-01 | **OPEN** |
| 147 | W7-117 | [#632](https://github.com/sesquicadaver/MTDirector/issues/632) | Seed next PLAN-11 row after DESK-COMPOSE-01 → DESK-GATE-01 | **DONE** |

**§3.C NEXT = W7-401 (#1206)** (PLAN-37 COMPLETE; PLAN-38 inventory DONE; PLAN-31 inventory DONE; W7-262 DONE; seed W7-263 DONE; LIST-01 W7-264 DONE → RO seed; PLAN-30 COMPLETE; W7-261 DONE; PLAN-29 COMPLETE; W7-255 DONE; W7-254 DONE; PLAN-30 inventory DONE [#919](https://github.com/sesquicadaver/MTDirector/issues/919); seed [#920](https://github.com/sesquicadaver/MTDirector/issues/920) **DONE**; implement [#922](https://github.com/sesquicadaver/MTDirector/issues/922) WATCH-OWN-01 **DONE**; seed [#923](https://github.com/sesquicadaver/MTDirector/issues/923) **DONE**; implement [#927](https://github.com/sesquicadaver/MTDirector/issues/927) WATCH-BP-01 **DONE**; seed [#928](https://github.com/sesquicadaver/MTDirector/issues/928) PLAN-30 COMPLETE **DONE**; inventory [#931](https://github.com/sesquicadaver/MTDirector/issues/931) PLAN-31 **DONE**; seed [#932](https://github.com/sesquicadaver/MTDirector/issues/932) **DONE**; implement [#934](https://github.com/sesquicadaver/MTDirector/issues/934) **DONE**; seed [#935](https://github.com/sesquicadaver/MTDirector/issues/935) **DONE**; implement [#939](https://github.com/sesquicadaver/MTDirector/issues/939) **DONE**; seed [#940](https://github.com/sesquicadaver/MTDirector/issues/940) **DONE**; inventory [#943](https://github.com/sesquicadaver/MTDirector/issues/943) **DONE**; seed [#944](https://github.com/sesquicadaver/MTDirector/issues/944) **DONE**; implement [#946](https://github.com/sesquicadaver/MTDirector/issues/946) **DONE**; seed [#947](https://github.com/sesquicadaver/MTDirector/issues/947) **DONE**; implement [#951](https://github.com/sesquicadaver/MTDirector/issues/951) **DONE**; COMPLETE [#952](https://github.com/sesquicadaver/MTDirector/issues/952) **DONE**; inventory [#955](https://github.com/sesquicadaver/MTDirector/issues/955) **DONE**; seed [#956](https://github.com/sesquicadaver/MTDirector/issues/956) **DONE**; implement [#958](https://github.com/sesquicadaver/MTDirector/issues/958) **DONE**; COMPLETE [#959](https://github.com/sesquicadaver/MTDirector/issues/959) **DONE**; inventory [#963](https://github.com/sesquicadaver/MTDirector/issues/963) **DONE**; seed [#964](https://github.com/sesquicadaver/MTDirector/issues/964) **DONE**; LINUX [#966](https://github.com/sesquicadaver/MTDirector/issues/966) **DONE**; WIN seed [#967](https://github.com/sesquicadaver/MTDirector/issues/967) **DONE**; WIN [#971](https://github.com/sesquicadaver/MTDirector/issues/971) **DONE**; COMPLETE [#972](https://github.com/sesquicadaver/MTDirector/issues/972) **DONE**; inventory [#975](https://github.com/sesquicadaver/MTDirector/issues/975) **DONE**; seed [#976](https://github.com/sesquicadaver/MTDirector/issues/976) **DONE**; implement [#978](https://github.com/sesquicadaver/MTDirector/issues/978) **DONE**; COMPLETE [#979](https://github.com/sesquicadaver/MTDirector/issues/979) **DONE**; inventory [#983](https://github.com/sesquicadaver/MTDirector/issues/983) **DONE**; seed [#984](https://github.com/sesquicadaver/MTDirector/issues/984) **DONE**; implement [#986](https://github.com/sesquicadaver/MTDirector/issues/986) **DONE**; COMPLETE [#987](https://github.com/sesquicadaver/MTDirector/issues/987) **DONE**; inventory [#991](**NEXT**)(https://github.com/sesquicadaver/MTDirector/issues/991); seed [#992](https://github.com/sesquicadaver/MTDirector/issues/992) **OPEN**; PLAN-28 COMPLETE; seed W7-249 DONE; PLAN-27 COMPLETE; W7-243 seed DONE). W7-203 **DONE**; W7-202 **DONE**; DESK-A11Y-OPS-02 **DONE**; PLAN-24 **COMPLETE**; W7-201 **DONE**; W7-200 **DONE**; DESK-A11Y-OPS-01 **DONE**; W7-199 **DONE**; PLAN-24 inventory **DONE**; W7-198 **DONE**; W7-197 **DONE**; DESK-A11Y-POLICY-OBJ-02 **DONE**; PLAN-23 **COMPLETE**; W7-196 **DONE**; W7-195 **DONE**; DESK-A11Y-POLICY-OBJ-01 **DONE**; W7-194 **DONE**; PLAN-23 inventory **DONE**; W7-193 **DONE**; W7-192 **DONE**; DESK-A11Y-POLICY-ACK-02 **DONE**; PLAN-22 **COMPLETE**; W7-191 **DONE**; W7-190 **DONE**; DESK-A11Y-POLICY-ACK-01 **DONE**; W7-189 **DONE**; PLAN-22 inventory **DONE**; W7-188 **DONE**; W7-187 **DONE**; DESK-A11Y-POLICY-EDIT-02 **DONE**; PLAN-21 **COMPLETE**; W7-186 **DONE**; W7-185 **DONE**; DESK-A11Y-POLICY-EDIT-01 **DONE**; W7-184 **DONE**; PLAN-21 inventory **DONE**; W7-183 **DONE**; W7-182 **DONE**; DESK-A11Y-POLICY-02 **DONE**; PLAN-20 **COMPLETE**; W7-181 **DONE**; W7-180 **DONE**; DESK-A11Y-POLICY-01 **DONE**; W7-179 **DONE**; PLAN-20 inventory **DONE**; W7-178 **DONE**; W7-177 **DONE**; DESK-A11Y-CONN-02 **DONE**; PLAN-19 **COMPLETE**; W7-176 **DONE**; W7-175 **DONE**; DESK-A11Y-CONN-01 **DONE**; W7-174 **DONE**; PLAN-19 inventory **DONE**; W7-173 **DONE**; W7-172 **DONE**; DESK-A11Y-INGEST-02 **DONE**; PLAN-18 **COMPLETE**; W7-171 **DONE**; W7-170 **DONE**; DESK-A11Y-INGEST-01 **DONE**; W7-169 **DONE**; PLAN-18 inventory **DONE**; W7-168 **DONE**; W7-167 **DONE**; DESK-A11Y-ACTION-02 **DONE**; PLAN-17 **COMPLETE**; W7-166 **DONE**; W7-165 **DONE**; DESK-A11Y-ACTION-01 **DONE**; W7-164 **DONE**; PLAN-17 inventory **DONE**; W7-163 **DONE**; W7-162 **DONE**; DESK-A11Y-02 **DONE**; PLAN-16 **COMPLETE**; W7-161 **DONE**; W7-160 **DONE**; DESK-A11Y-01 **DONE**; W7-159 **DONE**; PLAN-16 inventory **DONE**; W7-158 **DONE**; W7-157 **DONE**; DESK-FIELD-02 **DONE**; PLAN-15 **COMPLETE**; W7-156 **DONE**; W7-155 **DONE**; DESK-FIELD-01 **DONE**; W7-154 **DONE**; PLAN-15 inventory **DONE**; W7-153 **DONE**; W7-152 **DONE**; DESK-PLACEHOLDER-02 **DONE**; PLAN-14 **COMPLETE**; W7-151 **DONE**; W7-150 **DONE**; DESK-PLACEHOLDER-01 **DONE**; W7-149 **DONE**; PLAN-14 inventory **DONE**; W7-148 **DONE**; W7-147 **DONE**; DESK-LAYOUT-10 **DONE**; PLAN-13 **COMPLETE**; W7-146 **DONE**; W7-145 **DONE**; DESK-LAYOUT-09 **DONE**; W7-144 **DONE**; W7-143 **DONE**; DESK-LAYOUT-08 **DONE**; W7-142 **DONE**; W7-141 **DONE**; DESK-LAYOUT-07 **DONE**; W7-140 **DONE**; W7-139 **DONE**; DESK-LAYOUT-06 **DONE**; W7-138 **DONE**; W7-137 **DONE**; DESK-LAYOUT-05 **DONE**; W7-136 **DONE**; W7-135 **DONE**; DESK-LAYOUT-04 **DONE**; W7-134 **DONE**; W7-133 **DONE**; DESK-LAYOUT-03 **DONE**; W7-132 **DONE**; W7-131 **DONE**; DESK-LAYOUT-02 **DONE**; W7-130 **DONE**; W7-129 **DONE**; DESK-LAYOUT-01 **DONE**; W7-128 **DONE**; W7-127 **DONE**; DESK-LAYOUT-00 **DONE**; W7-126 **DONE**; PLAN-13 inventory **DONE**; W7-125 **DONE**; W7-124 **DONE**; PLAN-12 **COMPLETE**; W7-123 **DONE**; W7-122 **DONE**; W7-121 **DONE**; W7-120 **DONE**; W7-119 **DONE**; PLAN-12 inventory **DONE**; W7-118 **DONE**; W7-116 **DONE**; PLAN-11 **COMPLETE**; W7-117 **DONE**; W7-115 **DONE**; W7-113 **DONE**; W7-114 **DONE**; W7-112 **DONE**; W7-111 **DONE**; W7-110 **DONE**; PLAN-10 **COMPLETE**; W7-109 **DONE**; W7-108 **DONE**; W7-107 **DONE**; W7-106 **DONE**; W7-105 **DONE**; PLAN-09 **COMPLETE**; PLAN-10 inventory **DONE**; PLAN-08 **COMPLETE**; PLAN-09 inventory **DONE**; PLAN-07 **COMPLETE**; PLAN-05 **COMPLETE**; PLAN-06 **COMPLETE**. CRS/physical lab runner remains ops-parallel ([`known-limitations.md`](../release/known-limitations.md)), not a product §3 stop-gate.

## Anti-goals (unchanged)

- Local SemanticDiffEngine on Desktop
- `WriteEnabled=true` “so the UI works”
- Auto-fix drift / Save and Deploy
- Fake VRRP role labels
- Treating GNS3 phase N as a Desktop/Contracts stop-gate

## DoD per product PR

Issue AC; Living Spec row; CHANGELOG; CI Linux validate + Windows Desktop; no `pass` / `NotImplemented`; Domain/App ↛ RouterOS; this plan + alignment + ROADMAP NEXT advanced in the same PR.
