#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

printf 'STOTOP development doctor\n'

print_section "Tools"
print_command_version "dotnet" dotnet --version
print_command_version "node" node --version
print_command_version "npm" npm --version
print_command_version "git" git --version

print_section "Ports"
print_port_status "backend" "$BACKEND_PORT"
print_port_status "frontend" "$FRONTEND_PORT"

print_section "Configuration"
if [ -f "$(db_config_path)" ]; then
  status_ok "database config present"
else
  status_warn "database config missing: $(db_config_path)"
  status_warn "database example: $(db_config_example_path)"
fi

if [ -d "$WEB_DIR/node_modules" ]; then
  status_ok "frontend dependencies installed"
else
  status_warn "frontend dependencies missing; run $SCRIPT_DIR/setup.sh"
fi

print_section "Solution coverage"
if sln_output="$("$SCRIPT_DIR/check-sln.sh" 2>&1)"; then
  status_ok "solution coverage"
  printf '%s\n' "$sln_output"
else
  status_warn "solution coverage has known gaps"
  printf '%s\n' "$sln_output"
fi

print_section "Suggested commands"
printf '%s/setup.sh\n' "$SCRIPT_DIR"
printf '%s/backend.sh\n' "$SCRIPT_DIR"
printf '%s/frontend.sh\n' "$SCRIPT_DIR"
printf '%s/build-all.sh\n' "$SCRIPT_DIR"
printf '%s/preflight.sh\n' "$SCRIPT_DIR"
