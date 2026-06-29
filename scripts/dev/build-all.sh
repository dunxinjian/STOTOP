#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

print_section "Backend build"
"$SCRIPT_DIR/build-backend.sh"

print_section "Frontend build"
"$SCRIPT_DIR/build-frontend.sh"

print_section "Build summary"
status_ok "backend build passed"
status_ok "frontend Vite build passed"
status_warn "frontend type-check is separate: cd web && npm run type-check"
