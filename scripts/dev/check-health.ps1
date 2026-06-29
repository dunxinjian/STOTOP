# scripts/dev/check-health.ps1 —— 后端健康检查，对齐 check-health.sh（用 Invoke-WebRequest 替代 curl）。
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

$BackendUrl = if ($env:BACKEND_URL) { $env:BACKEND_URL } else { $BackendUrlDefault }

try {
  $resp = Invoke-WebRequest -Uri "$BackendUrl/health" -UseBasicParsing -TimeoutSec 10
  Write-Host $resp.Content
} catch {
  Write-Host "health check failed: $($_.Exception.Message)" -ForegroundColor Red
  exit 1
}
