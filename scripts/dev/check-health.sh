#!/usr/bin/env bash
set -euo pipefail

BACKEND_URL="${BACKEND_URL:-http://localhost:9000}"

if ! command -v curl >/dev/null 2>&1; then
  echo "curl is not installed." >&2
  exit 127
fi

curl --fail --show-error --silent "$BACKEND_URL/health"
echo
