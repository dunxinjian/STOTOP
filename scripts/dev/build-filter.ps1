# scripts/dev/build-filter.ps1 —— 按 .slnf 工作区过滤器只构建单个模块及其依赖闭包（而非整个 WebAPI 图）。
# 用法： .\build-filter.ps1 <name>      例： .\build-filter.ps1 cardflow
#        .\build-filter.ps1             不带参数列出可用过滤器
param([string]$Name = '')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

if (-not (Test-Cmd dotnet)) {
  Write-Host "dotnet is not installed. Install a .NET SDK that supports net10.0 first." -ForegroundColor Red
  exit 127
}

$filters = @(Get-ChildItem -Path $SrcDir -Filter '*.slnf' -File | Sort-Object Name)
if ($filters.Count -eq 0) { Write-FailLine "no .slnf filters found under src/"; exit 1 }

if (-not $Name) {
  Write-Section "可用工作区过滤器 (src/*.slnf)"
  foreach ($f in $filters) { Write-Host "  $($f.BaseName)" }
  Write-Host ""
  Write-Host "用法: .\build-filter.ps1 <name>"
  exit 0
}

$target = $filters | Where-Object { $_.BaseName -ieq $Name } | Select-Object -First 1
if (-not $target) {
  Write-FailLine "未找到过滤器: $Name（可用: $(($filters | ForEach-Object BaseName) -join ', ')）"
  exit 1
}

# 注：不切换工作目录——dotnet restore/build 用 .slnf 绝对路径，其内部相对项目路径相对 slnf 文件解析，
# 与 cwd 无关。若在此 Set-Location，会泄漏到调用方会话（同会话先 build 再 test 时相对路径会失效）。
Write-Section "restore $($target.Name)"
dotnet restore $target.FullName
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Section "build $($target.Name)"
dotnet build $target.FullName --no-restore -m:1 /p:UseSharedCompilation=false
exit $LASTEXITCODE
