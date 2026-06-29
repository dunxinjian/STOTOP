# scripts/dev/doctor.ps1 —— 开发体检，对齐 doctor.sh（Windows 版）。
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

Write-Host "STOTOP development doctor (Windows)"

Write-Section "Tools"
Show-CommandVersion "dotnet" "dotnet"
Show-CommandVersion "node"   "node"
Show-CommandVersion "npm"    "npm"
Show-CommandVersion "git"    "git"

Write-Section "Ports"
Show-PortStatus "backend"  $BackendPort
Show-PortStatus "frontend" $FrontendPort

Write-Section "Configuration"
if (Test-Path (Get-DbConfigPath)) { Write-Ok "database config present" }
else {
  Write-WarnLine "database config missing: $(Get-DbConfigPath)"
  Write-WarnLine "database example: $(Get-DbConfigExamplePath)"
}

if (Test-Path (Join-Path $WebDir 'node_modules')) { Write-Ok "frontend dependencies installed" }
else { Write-WarnLine "frontend dependencies missing; run $ScriptDir\setup.ps1" }

Write-Section "Solution"
if (Test-Path $SlnFile) { Write-Ok "solution file present: $SlnFile" }
else { Write-WarnLine "solution file missing: $SlnFile" }

Write-Section "Suggested commands"
Write-Host "$ScriptDir\setup.ps1"
Write-Host "$ScriptDir\backend.ps1"
Write-Host "$ScriptDir\frontend.ps1"
Write-Host "$ScriptDir\doctor.ps1"
