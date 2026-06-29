# scripts/dev/hook-precommit-gate.ps1
# Claude Code PreToolUse(Bash|PowerShell) hook：拦截 git commit，在提交前编译「本次将提交的 .cs」所属 csproj（含依赖），编译失败则阻止提交。
# 差异化：只编你这次改的工程，不被别处历史问题连累；不跑测试（太慢，留给 /test）。前端 type-check 不在门禁内（全工程检查 + 当前有历史错，见 design/22）。
# best-effort：hook 自身异常 / 无法判断时一律放行，绝不因 hook bug 卡住提交。从 stdin 读 PreToolUse JSON。
$ErrorActionPreference = 'SilentlyContinue'
try { [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false) } catch { }
function Allow { exit 0 }
function Deny([string]$reason) {
  (@{ hookSpecificOutput = @{ hookEventName = 'PreToolUse'; permissionDecision = 'deny'; permissionDecisionReason = $reason } } | ConvertTo-Json -Compress -Depth 6)
  exit 0
}
try {
  $raw = [Console]::In.ReadToEnd(); if (-not $raw) { Allow }
  $cmd = [string]((($raw | ConvertFrom-Json).tool_input).command); if (-not $cmd) { Allow }
  if ($cmd -notmatch '(?i)\bgit\s+commit\b') { Allow }                       # 非 git commit：放行
  if ($cmd -match '(?i)\bgit\s+commit\b[^\n]*(--help|\s-h(\s|$))') { Allow }  # git commit --help/-h：放行
  $root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
  Set-Location $root   # 子进程内，不泄漏到调用方会话
  # 将提交的文件：暂存 + 若 -a/-am/--all 再加未暂存的已跟踪改动
  $files = @(git diff --cached --name-only --diff-filter=ACM 2>$null)
  if ($cmd -match '(?i)\bgit\s+commit\b[^\n]*\s-(a|am|ma|all)\b' -or $cmd -match '(?i)--all\b') {
    $files += @(git diff --name-only --diff-filter=ACM 2>$null)
  }
  $cs = @($files | Where-Object { $_ -like '*.cs' } | Select-Object -Unique)
  if (-not $cs) { Allow }   # 本次不含 C# 改动：放行
  # 把每个变更 .cs 映射到最近的 .csproj
  $projs = @()
  foreach ($f in $cs) {
    $dir = Split-Path (Join-Path $root $f) -Parent
    while ($dir -and $dir.Length -ge $root.Length) {
      $pj = @(Get-ChildItem -LiteralPath $dir -Filter *.csproj -File -EA SilentlyContinue)
      if ($pj.Count -gt 0) { $projs += $pj[0].FullName; break }
      $parent = Split-Path $dir -Parent
      if (-not $parent -or $parent -eq $dir) { break }
      $dir = $parent
    }
  }
  $projs = @($projs | Select-Object -Unique)
  if (-not $projs) { Allow }
  $problems = @()
  foreach ($pj in $projs) {
    $out = (& dotnet build $pj -m:1 --nologo -clp:ErrorsOnly /p:UseSharedCompilation=false 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) {
      $name = [System.IO.Path]::GetFileName($pj)
      $tail = (($out.Trim() -split "`n") | Where-Object { $_ -match ': error' } | Select-Object -First 15) -join "`n"
      if (-not $tail) { $tail = (($out.Trim() -split "`n") | Select-Object -Last 15) -join "`n" }
      $problems += "✗ $name 编译失败：`n$tail"
    }
  }
  if ($problems.Count -gt 0) {
    Deny ("提交前自动门禁：本次改动的 C# 工程编译未通过，已阻止提交。修复后重提：`n`n" + ($problems -join "`n`n"))
  }
  Allow
} catch { Allow }
exit 0
