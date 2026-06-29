#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

print_section "Required tools"
require_command dotnet "Install a .NET SDK that supports net10.0."
require_command node "Install Node.js 24 with npm."
require_command npm "Install npm with Node.js 24."
require_command git "Install Git."

"$SCRIPT_DIR/check-env.sh"

print_section "Frontend dependencies"
if [ ! -f "$WEB_DIR/package-lock.json" ]; then
  status_fail "missing $WEB_DIR/package-lock.json"
  exit 1
fi

if [ -d "$WEB_DIR/node_modules" ]; then
  status_ok "node_modules already installed"
else
  status_warn "node_modules missing; running npm install"
  (cd "$WEB_DIR" && npm install)
  status_ok "frontend dependencies installed"
fi

print_section "Database configuration"
if [ -f "$(db_config_path)" ]; then
  status_ok "database config present: $(db_config_path)"
else
  status_warn "database config missing: $(db_config_path)"
  status_warn "copy $(db_config_example_path) to $(db_config_path) and fill a real SQL Server development connection"
fi

print_section "Next steps"
printf 'Run backend:  %s/backend.sh\n' "$SCRIPT_DIR"
printf 'Run frontend: %s/frontend.sh\n' "$SCRIPT_DIR"
printf 'Run doctor:   %s/doctor.sh\n' "$SCRIPT_DIR"
