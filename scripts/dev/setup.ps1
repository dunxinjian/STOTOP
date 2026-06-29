# scripts/dev/setup.ps1 —— 一键环境准备，对齐 setup.sh（Windows 版）。
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

Write-Section "Required tools"
$missing = $false
foreach ($t in @(
  @{ Name = 'dotnet'; Hint = 'Install a .NET SDK that supports net10.0 (winget install Microsoft.DotNet.SDK.10).' },
  @{ Name = 'node';   Hint = 'Install Node.js 24 with npm.' },
  @{ Name = 'npm';    Hint = 'Install npm with Node.js 24.' },
  @{ Name = 'git';    Hint = 'Install Git.' }
)) {
  if (Test-Cmd $t.Name) { Write-Ok "$($t.Name): present" }
  else { Write-FailLine "$($t.Name): missing"; Write-Host "  $($t.Hint)"; $missing = $true }
}
if ($missing) { exit 127 }

& "$ScriptDir\check-env.ps1"

Write-Section "Frontend dependencies"
if (-not (Test-Path (Join-Path $WebDir 'package-lock.json'))) {
  Write-FailLine "missing $(Join-Path $WebDir 'package-lock.json')"
  exit 1
}
if (Test-Path (Join-Path $WebDir 'node_modules')) {
  Write-Ok "node_modules already installed"
} else {
  Write-WarnLine "node_modules missing; running npm ci"
  Push-Location $WebDir
  try { npm ci } finally { Pop-Location }
  Write-Ok "frontend dependencies installed"
}

Write-Section "Database configuration"
if (Test-Path (Get-DbConfigPath)) {
  Write-Ok "database config present: $(Get-DbConfigPath)"
} else {
  Write-WarnLine "database config missing: $(Get-DbConfigPath)"
  Write-WarnLine "copy $(Get-DbConfigExamplePath) to $(Get-DbConfigPath) and fill a real SQL Server development connection"
}

Write-Section "Next steps"
Write-Host "Run backend:  $ScriptDir\backend.ps1"
Write-Host "Run frontend: $ScriptDir\frontend.ps1"
Write-Host "Run doctor:   $ScriptDir\doctor.ps1"
