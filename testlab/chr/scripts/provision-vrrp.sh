#!/usr/bin/env bash
# Provision VRRP CHR topologies for M1-32 (OUTSIDE Mfc.RouterOs product adapter).
# Usage: provision-vrrp.sh active-passive|split-master
set -euo pipefail

MODE="${1:-}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

case "${MODE}" in
  active-passive)
    TOPOLOGY="${ROOT}/testlab/chr/topologies/vrrp-active-passive/topology.json"
    FIXTURE="${ROOT}/testlab/chr/fixtures/vrrp-active-passive-minimal.rsc.example"
    HOST_ENV=MFC_CHR_VRRP_ACTIVE_PASSIVE_HOST
    ;;
  split-master)
    TOPOLOGY="${ROOT}/testlab/chr/topologies/vrrp-split-master/topology.json"
    FIXTURE="${ROOT}/testlab/chr/fixtures/vrrp-split-master-minimal.rsc.example"
    HOST_ENV=MFC_CHR_VRRP_SPLIT_MASTER_HOST
    ;;
  *)
    echo "usage: $0 active-passive|split-master" >&2
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
provision-vrrp (${MODE}): dry-check OK (no live host).
Set ${HOST_ENV} (optional ${PORT_ENV}) to apply ${FIXTURE}
via isolated runner tooling — never via Controller VRRP write RPCs.
EOF
  exit 0
fi

echo "provision-vrrp (${MODE}): live apply is runner-specific."
echo "  host=${HOST} port=${PORT}"
echo "  fixture=${FIXTURE}"
exit 0
