#!/usr/bin/env bash
# Provision multi-WAN CHR topologies for M1-31 (OUTSIDE Mfc.RouterOs product adapter).
# Usage: provision-multi-wan.sh failover|balanced
set -euo pipefail

MODE="${1:-}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

case "${MODE}" in
  failover)
    TOPOLOGY="${ROOT}/testlab/chr/topologies/multi-wan-failover/topology.json"
    FIXTURE="${ROOT}/testlab/chr/fixtures/multi-wan-failover-minimal.rsc.example"
    HOST_ENV=MFC_CHR_MULTIWAN_FAILOVER_HOST
    ;;
  balanced)
    TOPOLOGY="${ROOT}/testlab/chr/topologies/multi-wan-balanced/topology.json"
    FIXTURE="${ROOT}/testlab/chr/fixtures/multi-wan-balanced-minimal.rsc.example"
    HOST_ENV=MFC_CHR_MULTIWAN_BALANCED_HOST
    ;;
  *)
    echo "usage: $0 failover|balanced" >&2
    exit 2
    ;;
esac

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
provision-multi-wan (${MODE}): dry-check OK (no live host).
Set ${HOST_ENV} (optional ${PORT_ENV}) to apply ${FIXTURE}
via isolated runner tooling — never via Controller routing write RPCs.
EOF
  exit 0
fi

echo "provision-multi-wan (${MODE}): live apply is runner-specific."
echo "  host=${HOST} port=${PORT}"
echo "  fixture=${FIXTURE}"
exit 0
