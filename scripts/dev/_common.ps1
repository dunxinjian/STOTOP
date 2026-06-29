# STOTOP Windows 开发脚本共享库（对齐 scripts/dev/_common.sh 的职责）。
# 用法：在其他脚本顶部 dot-source 本文件：  . "$PSScriptRoot\_common.ps1"
# 兼容 Windows PowerShell 5.1 与 PowerShell 7+。

$script:ScriptDir = $PSScriptRoot
$script:RootDir   = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script:SrcDir    = Join-Path $RootDir 'src'
$script:WebApiDir = Join-Path $RootDir 'src\STOTOP.WebAPI'
$script:WebDir    = Join-Path $RootDir 'web'
$script:SlnFile   = Join-Path $RootDir 'src\STOTOP.sln'

# 端口：环境变量优先，默认对齐 _common.sh（后端 9000 / 前端 9001）
$script:BackendPort  = if ($env:BACKEND_PORT)  { [int]$env:BACKEND_PORT }  else { 9000 }
$script:FrontendPort = if ($env:FRONTEND_PORT) { [int]$env:FRONTEND_PORT } else { 9001 }
$script:BackendUrlDefault  = "http://localhost:$BackendPort"
$script:FrontendUrlDefault = "http://localhost:$FrontendPort"

function Write-Section($title) { Write-Host ""; Write-Host "== $title ==" }
function Write-Ok($msg)        { Write-Host "OK   $msg" -ForegroundColor Green }
function Write-WarnLine($msg)  { Write-Host "WARN $msg" -ForegroundColor Yellow }
function Write-FailLine($msg)  { Write-Host "FAIL $msg" -ForegroundColor Red }

function Test-Cmd($name) { [bool](Get-Command $name -ErrorAction SilentlyContinue) }

# 打印某命令的 --version（所有相关工具 dotnet/node/npm/git 都支持 --version）
function Show-CommandVersion($label, $name) {
  if (Test-Cmd $name) {
    try {
      $v = (& $name --version | Out-String).Trim()
      if (-not $v) { $v = '(no output)' }
      Write-Ok "${label}: $v"
    } catch { Write-WarnLine "${label}: command failed" }
  } else {
    Write-FailLine "${label}: missing"
  }
}

# 返回监听指定端口（Listen 状态）的进程 ID 列表
function Get-ListeningProcessId([int]$Port) {
  try {
    @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique)
  } catch { @() }
}

function Test-PortListening([int]$Port) { (Get-ListeningProcessId $Port).Count -gt 0 }

function Show-PortStatus($label, [int]$Port) {
  if (Test-PortListening $Port) { Write-Ok "$label port ${Port}: listening" }
  else { Write-WarnLine "$label port ${Port}: not listening" }
}

# 释放端口：先尝试优雅关闭，2 秒后仍占用则强制结束（对齐 restart-dev 的 free_port 语义）
function Stop-Port([int]$Port) {
  $ids = Get-ListeningProcessId $Port
  if (-not $ids -or $ids.Count -eq 0) { Write-Host "  port ${Port}: already free"; return }
  Write-Host "  freeing port ${Port}: $($ids -join ', ')"
  foreach ($procId in $ids) {
    try { $p = Get-Process -Id $procId -ErrorAction SilentlyContinue; if ($p) { $p.CloseMainWindow() | Out-Null } } catch {}
  }
  Start-Sleep -Seconds 2
  $ids = Get-ListeningProcessId $Port
  if ($ids -and $ids.Count -gt 0) {
    Write-Host "  port ${Port} still busy, killing: $($ids -join ', ')"
    foreach ($procId in $ids) { try { Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue } catch {} }
    Start-Sleep -Seconds 1
  }
}

function Get-DbConfigPath        { Join-Path $WebApiDir 'db-connections.json' }
function Get-DbConfigExamplePath { Join-Path $WebApiDir 'db-connections.example.json' }
