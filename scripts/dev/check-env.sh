#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

printf 'STOTOP development environment check\n'

print_section "Tools"
print_command_version "dotnet" dotnet --version
print_command_version "node" node --version
print_command_version "npm" npm --version
print_command_version "git" git --version

if has_command xcode-select && xcode-select -p >/dev/null 2>&1; then
  status_ok "xcode cli: $(xcode-select -p)"
elif [[ "$(uname -s)" == "Darwin" ]]; then
  status_warn "xcode cli: missing"
else
  status_ok "xcode cli: not required on $(uname -s)"
fi

print_section "Configuration"
if [ -f "$(db_config_path)" ]; then
  status_ok "database config: present"
else
  status_warn "database config: missing ($(db_config_path))"
  status_warn "database example: $(db_config_example_path)"
fi

if [ -f "$WEB_DIR/package-lock.json" ]; then
  status_ok "frontend lockfile: present"
else
  status_fail "frontend lockfile: missing ($WEB_DIR/package-lock.json)"
fi

if [ -d "$WEB_DIR/node_modules" ]; then
  status_ok "frontend dependencies: installed"
else
  status_warn "frontend dependencies: missing"
fi

print_section "Ports"
print_port_status "backend" "$BACKEND_PORT"
print_port_status "frontend" "$FRONTEND_PORT"
