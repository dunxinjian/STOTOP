#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

require_command node "Install Node.js first."

filter="${1:-}"
selected=()
passed=()
failures=()

while IFS= read -r test_file; do
  test_name="$(basename "$test_file")"
  if [ -z "$filter" ] || [[ "$test_name" == *"$filter"* ]]; then
    selected+=("$test_file")
  fi
done < <(find "$ROOT_DIR/scripts/tests" -maxdepth 1 -type f -name '*.mjs' | sort)

if [ "${#selected[@]}" -eq 0 ]; then
  if [ -n "$filter" ]; then
    status_fail "no contract tests selected for filter: $filter"
  else
    status_fail "no contract tests found in scripts/tests"
  fi
  exit 1
fi

for test_file in "${selected[@]}"; do
  test_label="${test_file#$ROOT_DIR/}"
  print_section "$test_label"

  if node "$test_file"; then
    status_ok "$test_label"
    passed+=("$test_label")
  else
    exit_code=$?
    status_fail "$test_label (exit $exit_code)"
    failures+=("$test_label (exit $exit_code)")
  fi
done

print_section "Contract test summary"
status_ok "${#passed[@]} passed"

if [ "${#failures[@]}" -eq 0 ]; then
  status_ok "all selected contract tests passed"
else
  status_fail "${#failures[@]} failed"
  for failure in "${failures[@]}"; do
    status_fail "$failure"
  done
  exit 1
fi
