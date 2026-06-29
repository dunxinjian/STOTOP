#!/usr/bin/env sh
# scripts/dev/hook-precommit-gate.sh
# Claude Code PreToolUse(Bash|PowerShell) hook：拦截 git commit，提交前编译「本次将提交的 .cs」所属 csproj（含依赖），失败则阻止提交。
# 差异化：只编你这次改的工程，不被别处历史问题连累；不跑测试。前端 type-check 不在门禁内。
# best-effort：hook 自身异常 / 无法判断时一律放行（恒 exit 0），绝不因 hook bug 卡住提交。从 stdin 读 PreToolUse JSON。
raw=$(cat); [ -z "$raw" ] && exit 0
if command -v jq >/dev/null 2>&1; then
  cmd=$(printf '%s' "$raw" | jq -r '.tool_input.command // empty' 2>/dev/null)
else
  cmd=$(printf '%s' "$raw" | sed -n 's/.*"command"[[:space:]]*:[[:space:]]*"\(.*\)".*/\1/p' | head -n1)
fi
[ -z "$cmd" ] && exit 0
printf '%s' "$cmd" | grep -Eiq '\bgit[[:space:]]+commit\b' || exit 0
printf '%s' "$cmd" | grep -Eiq '\bgit[[:space:]]+commit\b.*(--help|[[:space:]]-h([[:space:]]|$))' && exit 0
root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd) || exit 0
cd "$root" || exit 0
files=$(git diff --cached --name-only --diff-filter=ACM 2>/dev/null)
if printf '%s' "$cmd" | grep -Eiq '\bgit[[:space:]]+commit\b.*([[:space:]]-(a|am|ma|all)\b|--all\b)'; then
  files="$files
$(git diff --name-only --diff-filter=ACM 2>/dev/null)"
fi
cs=$(printf '%s\n' "$files" | grep -E '\.cs$' | sort -u)
[ -z "$cs" ] && exit 0
projs=""
oldIFS=$IFS; IFS='
'
for f in $cs; do
  d=$(dirname "$root/$f")
  while [ "$d" != "/" ] && [ ${#d} -ge ${#root} ]; do
    pj=$(ls "$d"/*.csproj 2>/dev/null | head -n1)
    [ -n "$pj" ] && { projs="$projs
$pj"; break; }
    d=$(dirname "$d")
  done
done
projs=$(printf '%s\n' "$projs" | grep -v '^$' | sort -u)
[ -z "$projs" ] && { IFS=$oldIFS; exit 0; }
problems=""
for pj in $projs; do
  out=$(dotnet build "$pj" -m:1 --nologo -clp:ErrorsOnly /p:UseSharedCompilation=false 2>&1)
  if [ $? -ne 0 ]; then
    name=$(basename "$pj")
    t=$(printf '%s\n' "$out" | grep ': error' | head -n 15); [ -z "$t" ] && t=$(printf '%s\n' "$out" | tail -n 15)
    problems="$problems
✗ $name 编译失败：
$t"
  fi
done
IFS=$oldIFS
if [ -n "$problems" ]; then
  reason="提交前自动门禁：本次改动的 C# 工程编译未通过，已阻止提交。修复后重提：
$problems"
  command -v jq >/dev/null 2>&1 && printf '%s' "$reason" | jq -Rs '{hookSpecificOutput:{hookEventName:"PreToolUse", permissionDecision:"deny", permissionDecisionReason: .}}'
fi
exit 0
