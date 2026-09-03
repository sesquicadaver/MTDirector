# Known limitations (MVP CLOSED + M7 CLOSED)

These limitations match the normative MVP scope lock (TOR-2 / ROADMAP §1). They are intentional residuals, not defects against M6-09 / N1-07 / M7 DoD.

## Closed milestones

- **N1-07 (#109)** — E2E/drift acceptance for container/VLAN/VETH/HW path classes is DONE (`PathClassE2EDriftLivingSpecTests`). Spine complete: `M6(+N1-07) → MVP CLOSED`.
- **M7.1…M7.4 (#110–#136)** — Post-MVP routing assurance, endpoint mobility, external correlation, and incident enforcement are DONE. **M7.4 CLOSED**; Post-MVP M7 = **0** open. Release tag **`v0.2.0`** (2026-08-24).

## Production wiring (P2 pilot)

- **Read path (P2-04…P2-06)** — **DONE**. Enable via `Mfc:RouterOs:Enabled=true`; default remains fail-closed (`ProbeOnlyRouterOsReadPort` / `NotConfiguredSnapshotCapturePort`). Pilot checklist: [`pilot-runbook.md`](../operations/pilot-runbook.md).
- Onboarding/deploy/watchdog-residue: **P2-07…P2-10 DONE** in code; enable via **`Mfc:RouterOs:WriteEnabled=true`**. Operator checklist: [`pilot-runbook.md`](../operations/pilot-runbook.md) (P2-11).

## Desktop inventory registration

- Inventory **Add router** wizard is **DONE** ([#309](https://github.com/sesquicadaver/MTDirector/pull/309)): Site→Node→Device + `UpdateDeviceConnection` from Desktop. gRPC remains available for automation.
- Closing the Desktop window **does not** stop Controller — stop the Controller process separately (separate OS processes).

## Live lab residuals (optional)

- Live CHR matrix is **OFF**. Scripted E2E Living Specs (M6-05…M6-07 + N1-07 + M7.1-11 + M7.2-04 + M7.4-06) are the DoD substitute.
- Live physical CRS hardware exercise is **OFF**. Scripted CRS fixture + `VrrpCrsE2ELivingSpecTests` AC11 are the DoD substitute. Physical CRS is **ops residual**, not a §3 stop-gate. **§3.C NEXT = residual (CRS lab ops)** after SEC-10.
- Golden live CHR hashes remain env-gated until an isolated runner exists.

## Packaging / signing residuals

- Desktop “installer” for MVP is a **zip/tar publish directory** (Avalonia), not MSI/setup.exe.
- Artifact “signing” for MVP is **cleartext `SHA256SUMS` + documented attestation**; cryptographic GPG/Sigstore is a CI signing gate (see [`RELEASE_SIGNING.md`](RELEASE_SIGNING.md)).
- CycloneDX CLI is optional; SBOM script falls back to CycloneDX-lite metadata + package inventory.

## Product scope lock (out of MVP / M7)

- No NAT / RAW / Mangle / routing / VRRP / bridge / VLAN **writes** beyond managed filter/onboarding/deploy allowlists.
- No campaigns, auto-deploy, auto-fix drift, web/mobile UI, multi-tenant, microservices/Redis/K8s, multi-vendor, SIEM/SOAR in Controller.
- `IResponseFeedbackDeliveryPort` defaults to **not configured** until an external analytics complex is wired.
- **SEC-07…SEC-10 DONE:** Zone/policy (SEC-07), `UpdateConnectionProfileUseCase` + `DeploymentWorkflowUseCases` (SEC-08), `OnboardingWorkflowUseCases` (SEC-09), and `ExpireIncidentDenyOverlayBindingUseCase` (SEC-10) share `IUnitOfWork` for entity+idempotency+audit co-writes. Intentional residual: resolve-only zone updates (no idempotency/audit triple); Start* pre-runtime `AddOperationAsync` stays outside UoW; ResponseFeedback emit stays outside the DB boundary.

## Operational notes

- Controller does not migrate on normal startup; use `--migrate-only` or the EF migrations bundle.
- Development master-key provider is forbidden outside Development.
- GitHub-hosted CI may be billing-limited; local gates in [`release-gates.md`](release-gates.md) remain authoritative for acceptance.
