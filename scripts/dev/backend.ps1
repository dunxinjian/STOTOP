# scripts/dev/backend.ps1 —— Windows 版后端启动，对齐 backend.sh。
# 显式 build 后直接跑 dll，绕开 `dotnet run` 的 launcher：
# `dotnet run` 会另起 apphost 子进程监听端口，停服务时容易留下孤儿占端口；
# `dotnet <dll>` 是同进程加载运行时，kill 即净。前台常驻（后台启动请用调用方的后台机制）。
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

if (-not (Test-Cmd dotnet)) {
  Write-Host "dotnet is not installed. Install a .NET SDK that supports net10.0 first." -ForegroundColor Red
  exit 127
}

$dbConfig = Get-DbConfigPath
if (-not (Test-Path $dbConfig)) {
  Write-Host "Missing $dbConfig" -ForegroundColor Red
  Write-Host "Create it with a SQL Server development connection before starting WebAPI." -ForegroundColor Red
  exit 1
}

Set-Location $WebApiDir

$Configuration   = if ($env:CONFIGURATION)    { $env:CONFIGURATION }    else { 'Debug' }
$TargetFramework = if ($env:TARGET_FRAMEWORK) { $env:TARGET_FRAMEWORK } else { 'net10.0' }
$DllPath = Join-Path $WebApiDir "bin\$Configuration\$TargetFramework\STOTOP.WebAPI.dll"

dotnet build -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "dotnet build failed (exit $LASTEXITCODE)" -ForegroundColor Red; exit $LASTEXITCODE }
if (-not (Test-Path $DllPath)) { Write-Host "Build did not produce $DllPath" -ForegroundColor Red; exit 1 }

if (-not $env:ASPNETCORE_ENVIRONMENT) { $env:ASPNETCORE_ENVIRONMENT = 'Development' }
if (-not $env:ASPNETCORE_URLS)        { $env:ASPNETCORE_URLS = "http://localhost:$BackendPort" }
if (-not $env:DOTNET_hostBuilder__reloadConfigOnChange)    { $env:DOTNET_hostBuilder__reloadConfigOnChange = 'false' }
if (-not $env:ASPNETCORE_hostBuilder__reloadConfigOnChange) { $env:ASPNETCORE_hostBuilder__reloadConfigOnChange = 'false' }

Write-Host "Starting WebAPI on $($env:ASPNETCORE_URLS) ..." -ForegroundColor Cyan
dotnet $DllPath
