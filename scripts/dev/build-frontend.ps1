# scripts/dev/build-frontend.ps1 —— 对齐 build-frontend.sh。
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

if (-not (Test-Cmd npm)) {
  Write-Host "npm is not installed. Install Node.js with npm first." -ForegroundColor Red
  exit 127
}

Set-Location $WebDir
if (-not (Test-Path (Join-Path $WebDir 'node_modules'))) {
  npm install
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
npm run build
exit $LASTEXITCODE
