# scripts/dev/test-dotnet.ps1 —— 自动发现 tests/ 下全部测试项目并运行（支持 filter 参数）。
# 用法： .\test-dotnet.ps1 [filter]
#   filter 为子串，匹配相对路径或项目名（如 cardflow / Finance）。
#   注：无参运行全部测试项目；CardFlow.Tests / WebAPI.Tests 依赖 WebAPI 会拉入近全图，
#       日常迭代建议带 filter 收敛（如 .\test-dotnet.ps1 Finance）。
param([string]$Filter = '')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

if (-not (Test-Cmd dotnet)) {
  Write-Host "dotnet is not installed. Install a .NET SDK that supports net10.0 first." -ForegroundColor Red
  exit 127
}

$testsDir = Join-Path $RootDir 'tests'
if (-not (Test-Path $testsDir)) { Write-FailLine "tests directory not found: $testsDir"; exit 1 }

# 自动发现：tests/ 下所有 *.Tests.csproj，避免写死清单导致漏跑（新增测试项目零改动自动纳入）。
$found = @(Get-ChildItem -Path $testsDir -Recurse -Filter '*.Tests.csproj' -File | Sort-Object FullName)
if ($found.Count -eq 0) { Write-FailLine "no *.Tests.csproj found under tests/"; exit 1 }

$selected = @()
foreach ($proj in $found) {
  $rel  = $proj.FullName.Substring($RootDir.Length).TrimStart('\', '/').Replace('\', '/')
  $name = $proj.BaseName
  if (-not $Filter -or $rel -like "*$Filter*" -or $name -like "*$Filter*") {
    $selected += [pscustomobject]@{ Path = $proj.FullName; Rel = $rel }
  }
}
if ($selected.Count -eq 0) { Write-FailLine "no dotnet test projects selected for filter: $Filter"; exit 1 }

$passed = @(); $failures = @()
foreach ($p in $selected) {
  Write-Section $p.Rel
  dotnet test $p.Path -m:1 /p:UseSharedCompilation=false
  if ($LASTEXITCODE -eq 0) { Write-Ok $p.Rel; $passed += $p.Rel }
  else { Write-FailLine "$($p.Rel) (exit $LASTEXITCODE)"; $failures += "$($p.Rel) (exit $LASTEXITCODE)" }
}

Write-Section "Dotnet test summary"
Write-Ok "$($passed.Count) passed"
if ($failures.Count -eq 0) { Write-Ok "all selected dotnet test projects passed" }
else {
  Write-FailLine "$($failures.Count) failed"
  foreach ($f in $failures) { Write-FailLine $f }
  exit 1
}
