# scripts/dev/test-contracts.ps1 —— 对齐 test-contracts.sh（支持 filter 参数）。
# 用法： .\test-contracts.ps1 [filter]
param([string]$Filter = '')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

if (-not (Test-Cmd node)) {
  Write-Host "node is not installed. Install Node.js first." -ForegroundColor Red
  exit 127
}

$testsDir = Join-Path $RootDir 'scripts\tests'
$files = @(Get-ChildItem -LiteralPath $testsDir -Filter '*.mjs' -File |
    Where-Object { $_.Name -notlike '._*' } | Sort-Object Name)
$selected = @()
foreach ($f in $files) {
  if (-not $Filter -or $f.Name -like "*$Filter*") { $selected += $f }
}
if ($selected.Count -eq 0) {
  if ($Filter) { Write-FailLine "no contract tests selected for filter: $Filter" }
  else { Write-FailLine "no contract tests found in scripts/tests" }
  exit 1
}

$passed = @(); $failures = @()
foreach ($f in $selected) {
  Write-Section $f.Name
  node $f.FullName
  if ($LASTEXITCODE -eq 0) { Write-Ok $f.Name; $passed += $f.Name }
  else { Write-FailLine "$($f.Name) (exit $LASTEXITCODE)"; $failures += "$($f.Name) (exit $LASTEXITCODE)" }
}

Write-Section "Contract test summary"
Write-Ok "$($passed.Count) passed"
if ($failures.Count -eq 0) { Write-Ok "all selected contract tests passed" }
else {
  Write-FailLine "$($failures.Count) failed"
  foreach ($f in $failures) { Write-FailLine $f }
  exit 1
}
