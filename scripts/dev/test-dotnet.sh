#!/usr/bin/env bash
set -euo pipefail

# scripts/dev/test-dotnet.sh —— 自动发现 tests/ 下全部 *.Tests.csproj 并运行（支持 filter 参数）。
# 用法： ./test-dotnet.sh [filter]
#   注：无参运行全部测试项目；CardFlow.Tests / WebAPI.Tests 依赖 WebAPI 会拉入近全图，
#       日常迭代建议带 filter 收敛（如 ./test-dotnet.sh Finance）。

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

require_command dotnet "Install a .NET SDK that supports net10.0 first."

tests_dir="$ROOT_DIR/tests"
if [ ! -d "$tests_dir" ]; then
  status_fail "tests directory not found: $tests_dir"
  exit 1
fi

# 自动发现：tests/ 下所有 *.Tests.csproj，避免写死清单导致漏跑（新增测试项目零改动自动纳入）。
projects=()
while IFS= read -r line; do
  projects+=("$line")
done < <(cd "$ROOT_DIR" && find tests -name '*.Tests.csproj' -type f | sort)

if [ "${#projects[@]}" -eq 0 ]; then
  status_fail "no *.Tests.csproj found under tests/"
  exit 1
fi

filter="${1:-}"
selected=()
passed=()
failures=()

for project_path in "${projects[@]}"; do
  project_name="$(basename "$project_path" .csproj)"
  if [ -z "$filter" ] || [[ "$project_path" == *"$filter"* ]] || [[ "$project_name" == *"$filter"* ]]; then
    selected+=("$project_path")
  fi
done

if [ "${#selected[@]}" -eq 0 ]; then
  status_fail "no dotnet test projects selected for filter: $filter"
  exit 1
fi

for project_path in "${selected[@]}"; do
  print_section "$project_path"

  if dotnet test "$ROOT_DIR/$project_path" -m:1 /p:UseSharedCompilation=false; then
    status_ok "$project_path"
    passed+=("$project_path")
  else
    exit_code=$?
    status_fail "$project_path (exit $exit_code)"
    failures+=("$project_path (exit $exit_code)")
  fi
done

print_section "Dotnet test summary"
status_ok "${#passed[@]} passed"

if [ "${#failures[@]}" -eq 0 ]; then
  status_ok "all selected dotnet test projects passed"
else
  status_fail "${#failures[@]} failed"
  for failure in "${failures[@]}"; do
    status_fail "$failure"
  done
  exit 1
fi
