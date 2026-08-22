# Known limitations (MVP CLOSED)

These limitations match the normative MVP scope lock (TOR-2 / ROADMAP §1). They are intentional residuals, not defects against M6-09 / N1-07 DoD.

## Closed with N1-07 / MVP CLOSED

- **N1-07 (#109)** — E2E/drift acceptance for container/VLAN/VETH/HW path classes is DONE (`PathClassE2EDriftLivingSpecTests`). Spine complete: `M6(+N1-07) → MVP CLOSED`.

## Live lab residuals (optional)

- Live CHR matrix is **OFF**. Scripted E2E Living Specs (M6-05…M6-07 + N1-07) are the DoD substitute.
- Live physical CRS hardware exercise is **OFF**. Scripted CRS fixture + `VrrpCrsE2ELivingSpecTests` AC11 are the DoD substitute.
- Golden live CHR hashes remain env-gated until an isolated runner exists.

## Packaging / signing residuals

- Desktop “installer” for MVP is a **zip/tar publish directory** (Avalonia), not MSI/setup.exe.
- Artifact “signing” for MVP is **cleartext `SHA256SUMS` + documented attestation**; cryptographic GPG/Sigstore is a CI signing gate (see [`RELEASE_SIGNING.md`](RELEASE_SIGNING.md)).
- CycloneDX CLI is optional; SBOM script falls back to CycloneDX-lite metadata + package inventory.

## Product scope lock (out of MVP)

- No NAT / RAW / Mangle / routing / VRRP / bridge / VLAN **writes** beyond managed filter/onboarding/deploy allowlists.
- No campaigns, auto-deploy, auto-fix drift, web/mobile UI, multi-tenant, microservices/Redis/K8s, multi-vendor, SIEM/SOAR in Controller.
- Post-MVP **M7.*** (#110–#136) continues after MVP CLOSED (M7.1-09 DONE; NEXT = M7.1-10).

## Operational notes

- Controller does not migrate on normal startup; use `--migrate-only` or the EF migrations bundle.
- Development master-key provider is forbidden outside Development.
- GitHub-hosted CI may be billing-limited; local gates in [`release-gates.md`](release-gates.md) remain authoritative for acceptance.
