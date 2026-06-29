#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

passed=()
blockers=()
debts=()
skipped=()

run_required() {
  local label="$1"
  shift

  print_section "$label"
  if "$@"; then
    status_ok "$label"
    passed+=("$label")
  else
    local exit_code=$?
    status_fail "$label (exit $exit_code)"
    blockers+=("$label (exit $exit_code)")
  fi
}

run_debt() {
  local label="$1"
  shift

  print_section "$label"
  if "$@"; then
    status_ok "$label"
    passed+=("$label")
  else
    local exit_code=$?
    status_warn "$label (exit $exit_code)"
    debts+=("$label (exit $exit_code)")
  fi
}

skip_step() {
  local message="$1"

  status_warn "$message"
  skipped+=("$message")
}

print_summary_list() {
  local empty_message="$1"
  shift

  if [ "$#" -eq 0 ]; then
    printf '%s\n' "$empty_message"
  else
    local item
    for item in "$@"; do
      printf '- %s\n' "$item"
    done
  fi
}

run_required "doctor" "$SCRIPT_DIR/doctor.sh"

if [ "${STOTOP_PREFLIGHT_STRICT_SLN:-0}" = "1" ]; then
  run_required "solution coverage" "$SCRIPT_DIR/check-sln.sh"
else
  run_debt "solution coverage" "$SCRIPT_DIR/check-sln.sh"
fi

run_required "backend build" "$SCRIPT_DIR/build-backend.sh"
run_required "frontend build" "$SCRIPT_DIR/build-frontend.sh"
run_required "contract tests" "$SCRIPT_DIR/test-contracts.sh"
run_required "dotnet tests" "$SCRIPT_DIR/test-dotnet.sh"

if [ "${STOTOP_PREFLIGHT_TYPECHECK:-0}" = "1" ]; then
  run_debt "frontend type-check" bash -lc "cd '$WEB_DIR' && npm run type-check"
else
  skip_step "frontend type-check skipped; set STOTOP_PREFLIGHT_TYPECHECK=1 to run it as known debt"
fi

print_section "Preflight summary"

print_section "Passed"
print_summary_list "No passed checks." "${passed[@]}"

print_section "Blockers"
print_summary_list "No blockers." "${blockers[@]}"

print_section "Known debt"
print_summary_list "No known debt failures." "${debts[@]}"

print_section "Skipped"
print_summary_list "No skipped checks." "${skipped[@]}"

if [ "${#blockers[@]}" -gt 0 ]; then
  exit 1
fi
