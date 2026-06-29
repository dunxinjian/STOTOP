# scripts/dev/check-env.ps1 —— 环境检查，对齐 check-env.sh（Windows 版，无 macOS xcode 检查）。
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

Write-Host "STOTOP development environment check (Windows)"

Write-Section "Tools"
Show-CommandVersion "dotnet" "dotnet"
Show-CommandVersion "node"   "node"
Show-CommandVersion "npm"    "npm"
Show-CommandVersion "git"    "git"

Write-Section "Configuration"
if (Test-Path (Get-DbConfigPath)) {
  Write-Ok "database config: present"
} else {
  Write-WarnLine "database config: missing ($(Get-DbConfigPath))"
  Write-WarnLine "database example: $(Get-DbConfigExamplePath)"
}

if (Test-Path (Join-Path $WebDir 'package-lock.json')) { Write-Ok "frontend lockfile: present" }
else { Write-FailLine "frontend lockfile: missing ($(Join-Path $WebDir 'package-lock.json'))" }

if (Test-Path (Join-Path $WebDir 'node_modules')) { Write-Ok "frontend dependencies: installed" }
else { Write-WarnLine "frontend dependencies: missing" }

Write-Section "Ports"
Show-PortStatus "backend"  $BackendPort
Show-PortStatus "frontend" $FrontendPort
