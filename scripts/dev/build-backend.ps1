# scripts/dev/build-backend.ps1 —— 对齐 build-backend.sh。
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

if (-not (Test-Cmd dotnet)) {
  Write-Host "dotnet is not installed. Install a .NET SDK that supports net10.0 first." -ForegroundColor Red
  exit 127
}

Set-Location $SrcDir
dotnet restore STOTOP.WebAPI/STOTOP.WebAPI.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build STOTOP.WebAPI/STOTOP.WebAPI.csproj --no-restore -m:1 /p:UseSharedCompilation=false
exit $LASTEXITCODE
