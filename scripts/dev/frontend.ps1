# scripts/dev/frontend.ps1 —— Windows 版前端启动，对齐 frontend.sh。
# Vite dev server 前台常驻（后台启动请用调用方的后台机制）。
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

if (-not (Test-Cmd npm)) {
  Write-Host "npm is not installed. Install Node.js with npm first." -ForegroundColor Red
  exit 127
}

Set-Location $WebDir

if (-not (Test-Path (Join-Path $WebDir 'node_modules'))) {
  Write-Host "Installing frontend dependencies..." -ForegroundColor Cyan
  npm install
  if ($LASTEXITCODE -ne 0) { Write-Host "npm install failed (exit $LASTEXITCODE)" -ForegroundColor Red; exit $LASTEXITCODE }
}

npm run dev
