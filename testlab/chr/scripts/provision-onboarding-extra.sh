#!/usr/bin/env bash
# Provision extra onboarding CHR topologies for M5-10 (OUTSIDE Mfc.RouterOs product adapter).
# Usage: provision-onboarding-extra.sh standalone-dual-stack|crs-switch
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
TOPOLOGY_ID="${1:-}"

if [[ "${TOPOLOGY_ID}" != "standalone-dual-stack" && "${TOPOLOGY_ID}" != "crs-switch" ]]; then
  echo "usage: $0 standalone-dual-stack|crs-switch" >&2
  exit 1
fi

TOPOLOGY="${ROOT}/testlab/chr/topologies/${TOPOLOGY_ID}/topology.json"
if [[ "${TOPOLOGY_ID}" == "standalone-dual-stack" ]]; then
  FIXTURE="${ROOT}/testlab/chr/fixtures/standalone-dual-stack-minimal.rsc.example"
else
  FIXTURE="${ROOT}/testlab/chr/fixtures/crs-switch-minimal.rsc.example"
fi

if [[ ! -f "${TOPOLOGY}" ]]; then
  echo "missing topology contract: ${TOPOLOGY}" >&2
  exit 1
fi
if [[ ! -f "${FIXTURE}" ]]; then
  echo "missing fixture: ${FIXTURE}" >&2
  exit 1
fi

cat <<EOF
provision-onboarding-extra: dry-check OK for ${TOPOLOGY_ID} (no live host).
Apply ${FIXTURE} on an isolated runner using ssh/scp/API outside the MTDirector product write path.
EOF
