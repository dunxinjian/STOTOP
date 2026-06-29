#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEB_DIR="$ROOT_DIR/web"

if ! command -v npm >/dev/null 2>&1; then
  echo "npm is not installed. Install Node.js with npm first." >&2
  exit 127
fi

cd "$WEB_DIR"

if [ ! -d node_modules ]; then
  npm install
fi

npm run build
