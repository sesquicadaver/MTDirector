# Installation (v0.2.0)

Release **`v0.2.0`** ships MVP + Post-MVP M7 feature-complete **code**. P2 pilot wiring is **CLOSED** in code:

- Live read/capture: **`Mfc:RouterOs:Enabled=true`**
- Live onboarding/deploy: **`Mfc:RouterOs:WriteEnabled=true`** (lab only)

See [`pilot-runbook.md`](pilot-runbook.md), [`controller-configuration.md`](controller-configuration.md), and [`../release/known-limitations.md`](../release/known-limitations.md).

## Prerequisites

- .NET runtime/SDK matching [`global.json`](../../global.json) on the Controller host (publish is framework-dependent by default).
- PostgreSQL (only supported database).
- TLS certificates for non-Development Controller binds.
- Operator workstation for Desktop (Avalonia publish archive).

See also [`prerequisite-checklist.md`](prerequisite-checklist.md) for RouterOS device gates.

## Controller

1. Obtain the Controller package from release packaging (`scripts/release/package-controller.sh` → `OUT_DIR/controller`).
2. Configure `Mfc` settings / env (`MFC__…`) per [`controller-configuration.md`](controller-configuration.md).
3. Apply schema with the migrations bundle (`OUT_DIR/migrations/mfc-ef-migrations`) **or** Development `--migrate-only`.
4. Start `Mfc.Controller` and verify gRPC health.

## Desktop

1. Obtain `Mfc.Desktop-<rid>.zip` (or `.tar.gz`) from `scripts/release/package-desktop.sh`.
2. Extract and run `Mfc.Desktop`.
3. Point `Desktop:ControllerEndpoint` at the Controller URL.
4. Connect → Inventory → **Add router** to register Site/Node/Device and connection profile (see [`../development/connection-profiles.md`](../development/connection-profiles.md)).

Native MSI/setup installers are out of MVP scope (zip publish is the installer substitute).

Desktop and Controller are **separate processes** — closing Desktop does not stop Controller.

## Verify integrity

```bash
cd "$OUT_DIR"
sha256sum -c SHA256SUMS
```

Signing policy: [`../release/RELEASE_SIGNING.md`](../release/RELEASE_SIGNING.md).
