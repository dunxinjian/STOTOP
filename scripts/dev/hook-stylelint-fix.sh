#!/usr/bin/env sh
# scripts/dev/hook-stylelint-fix.sh
# Claude Code PostToolUse(Write|Edit) hook helper：编辑 web/src/*.{vue,scss} 后跑 stylelint --fix。
# 本项目 stylelint 仅启用 color-no-hex（不可自动修）——--fix 修可修项，主要作用是「存盘即查」：
# 把剩余问题（多为裸 hex，须改 var(--token)）通过 JSON 回报给模型/用户，便于当场修。
# best-effort：始终 exit 0，绝不阻断编辑流；文件不匹配 / 未装 node_modules 静默跳过。从 stdin 读 PostToolUse JSON。
raw=$(cat); [ -z "$raw" ] && exit 0
if command -v jq >/dev/null 2>&1; then
  fp=$(printf '%s' "$raw" | jq -r '.tool_input.file_path // empty' 2>/dev/null)
else
  fp=$(printf '%s' "$raw" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -n1)
fi
[ -z "$fp" ] && exit 0
case "$fp" in */web/src/*.vue|*/web/src/*.scss) ;; *) exit 0 ;; esac
webdir=$(printf '%s' "$fp" | sed -n 's#\(.*/web\)/src/.*#\1#p')
[ -z "$webdir" ] && exit 0
sl="$webdir/node_modules/.bin/stylelint"
[ -x "$sl" ] || exit 0
out=$(cd "$webdir" && "$sl" "$fp" --fix --formatter compact 2>&1); status=$?
if [ "$status" -ne 0 ] && [ -n "$out" ]; then
  name=$(basename "$fp")
  msg="stylelint 在刚编辑的 $name 发现未修复问题（多为裸 hex，须改用 var(--token)，真源 web/docs/TOKENS.md）：
$out"
  if command -v jq >/dev/null 2>&1; then
    printf '%s' "$msg" | jq -Rs '{systemMessage: ., hookSpecificOutput:{hookEventName:"PostToolUse", additionalContext: .}}'
  fi
fi
exit 0
