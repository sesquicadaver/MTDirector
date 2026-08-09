#!/usr/bin/env bash
# Provision standalone CHR for M1-30 acceptance (OUTSIDE Mfc.RouterOs product adapter).
# Applies the synthetic fixture via lab tooling only — never through Controller write RPCs.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
TOPOLOGY="${ROOT}/testlab/chr/topologies/standalone/topology.json"
FIXTURE="${ROOT}/testlab/chr/fixtures/standalone-minimal.rsc.example"
HOST="${MFC_CHR_STANDALONE_HOST:-}"
PORT="${MFC_CHR_STANDALONE_PORT:-8729}"

if [[ ! -f "${TOPOLOGY}" ]]; then
  echo "missing topology contract: ${TOPOLOGY}" >&2
  exit 1
fi
if [[ ! -f "${FIXTURE}" ]]; then
  echo "missing fixture: ${FIXTURE}" >&2
  exit 1
fi

if [[ -z "${HOST}" ]]; then
  cat <<EOF
provision-standalone: dry-check OK (no live host).
Set MFC_CHR_STANDALONE_HOST (and optional MFC_CHR_STANDALONE_PORT) to apply
${FIXTURE} to a lab CHR using your isolated runner tooling
(ssh/scp/API outside the MTDirector product write path).
EOF
  exit 0
fi

echo "provision-standalone: live apply is runner-specific."
echo "  host=${HOST} port=${PORT}"
echo "  fixture=${FIXTURE}"
echo "Implement the apply step in the self-hosted runner (ssh/scp/netinstall),"
echo "then export MFC_CHR_STANDALONE_HOST for StandaloneChrLiveAcceptanceTests."
exit 0
