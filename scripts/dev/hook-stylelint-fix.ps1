# scripts/dev/hook-stylelint-fix.ps1
# Claude Code PostToolUse(Write|Edit) hook helper：编辑 web/src/*.{vue,scss} 后跑 stylelint --fix。
# 本项目 stylelint 仅启用 color-no-hex（不可自动修）——所以 --fix 自动修可修项（将来扩了可修规则时生效），
# 当前主要作用是「存盘即查」：把剩余问题（多为裸 hex，须改 var(--token)）通过 JSON 回报给模型/用户，便于当场修。
# best-effort：始终 exit 0，绝不阻断编辑流；文件不匹配 / web 未装 node_modules 静默跳过。从 stdin 读 PostToolUse JSON。
$ErrorActionPreference = 'SilentlyContinue'
# 强制 UTF-8 输出，否则重定向给 hook 运行器时中文按 OEM 码页乱码
try { [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false) } catch { }
try {
  $raw = [Console]::In.ReadToEnd()
  if (-not $raw) { exit 0 }
  $fp = ($raw | ConvertFrom-Json).tool_input.file_path
  if (-not $fp) { exit 0 }
  $norm = ($fp -replace '\\', '/')
  if ($norm -notmatch '(?i)/web/src/.*\.(vue|scss)$') { exit 0 }
  if ($norm -notmatch '(?i)^(.*?/web)/src/') { exit 0 }
  $webDir = $Matches[1]
  $stylelint = Join-Path $webDir 'node_modules/.bin/stylelint.cmd'
  if (-not (Test-Path $stylelint)) { exit 0 }
  Push-Location $webDir
  try { $out = (& $stylelint $fp --fix --formatter compact 2>&1 | Out-String) } finally { Pop-Location }
  if ($LASTEXITCODE -ne 0 -and $out.Trim()) {
    $name = [System.IO.Path]::GetFileName($fp)
    $msg = "stylelint 在刚编辑的 $name 发现未修复问题（本项目多为裸 hex，须改用设计令牌 var(--token)，真源 web/docs/TOKENS.md）：`n" + $out.Trim()
    (@{ systemMessage = $msg; hookSpecificOutput = @{ hookEventName = 'PostToolUse'; additionalContext = $msg } } | ConvertTo-Json -Compress)
  }
} catch { }
exit 0
