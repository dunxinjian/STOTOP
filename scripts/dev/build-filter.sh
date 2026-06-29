#!/usr/bin/env bash
set -euo pipefail

# scripts/dev/build-filter.sh —— 按 .slnf 工作区过滤器只构建单个模块及其依赖闭包（而非整个 WebAPI 图）。
# 用法： ./build-filter.sh <name>      例： ./build-filter.sh cardflow
#        ./build-filter.sh            不带参数列出可用过滤器

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

require_command dotnet "Install a .NET SDK that supports net10.0 first."

name="${1:-}"

filters=()
while IFS= read -r line; do
  filters+=("$line")
done < <(cd "$SRC_DIR" && find . -maxdepth 1 -name '*.slnf' -type f | sed 's#^\./##' | sort)

if [ "${#filters[@]}" -eq 0 ]; then
  status_fail "no .slnf filters found under src/"
  exit 1
fi

if [ -z "$name" ]; then
  print_section "可用工作区过滤器 (src/*.slnf)"
  for f in "${filters[@]}"; do
    printf '  %s\n' "${f%.slnf}"
  done
  printf '\n用法: %s <name>\n' "$(basename "$0")"
  exit 0
fi

name_lc="$(printf '%s' "$name" | tr '[:upper:]' '[:lower:]')"
target=""
for f in "${filters[@]}"; do
  base_lc="$(printf '%s' "${f%.slnf}" | tr '[:upper:]' '[:lower:]')"
  if [ "$base_lc" = "$name_lc" ]; then
    target="$f"
    break
  fi
done

if [ -z "$target" ]; then
  available="$(printf '%s ' "${filters[@]/%.slnf/}")"
  status_fail "未找到过滤器: $name（可用: ${available}）"
  exit 1
fi

cd "$SRC_DIR"
print_section "restore $target"
dotnet restore "$target"
print_section "build $target"
dotnet build "$target" --no-restore -m:1 /p:UseSharedCompilation=false
