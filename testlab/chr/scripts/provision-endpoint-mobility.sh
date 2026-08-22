#!/usr/bin/env bash
# Provision endpoint-mobility CHR topology for M7.2-04 (OUTSIDE Mfc.RouterOs product adapter).
# Usage: provision-endpoint-mobility.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
TOPOLOGY="${ROOT}/testlab/chr/topologies/endpoint-mobility-migration/topology.json"
FIXTURE="${ROOT}/testlab/chr/fixtures/endpoint-mobility-migration-minimal.rsc.example"
HOST_ENV=MFC_CHR_ENDPOINT_MOBILITY_HOST
PORT_ENV="${HOST_ENV/HOST/PORT}"
HOST="${!HOST_ENV:-}"
PORT="${!PORT_ENV:-8729}"

if [[ ! -f "${TOPOLOGY}" ]]; then
  echo "missing topology: ${TOPOLOGY}" >&2
  exit 1
fi
if [[ ! -f "${FIXTURE}" ]]; then
  echo "missing fixture: ${FIXTURE}" >&2
  exit 1
fi

if [[ -z "${HOST}" ]]; then
  cat <<EOF
provision-endpoint-mobility: dry-check OK (no live host).
Set ${HOST_ENV} (optional ${PORT_ENV}) to apply ${FIXTURE}
via isolated runner tooling — never via Controller routing write RPCs.
Living Spec substitute: EndpointMobilityChrAcceptanceLivingSpecTests (live CHR OFF).
EOF
  exit 0
fi

echo "provision-endpoint-mobility: live apply is runner-specific."
echo "  host=${HOST} port=${PORT}"
echo "  fixture=${FIXTURE}"
exit 0
