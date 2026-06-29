# STOTOP Windows 开发落地指南

面向在 Windows 上开发 STOTOP 的环境配置与日常命令。macOS 见 [mac-development.md](mac-development.md)。

## 必备工具

| 工具 | 版本/要求 | 安装 |
| --- | --- | --- |
| .NET SDK | 满足 `global.json`（当前 `10.0.300`+，net10.0） | `winget install Microsoft.DotNet.SDK.10` |
| Node.js | 24（见 `.node-version`） | `winget install OpenJS.NodeJS.LTS` 或官网 |
| Git | 近期版本（自带 Git Bash） | `winget install Git.Git` |
| sqlcmd | 可选，连远程库调试用 | 随 SQL Server ODBC 提供 |

> 后端要求的 SDK 版本由仓库根 `global.json` 锁定。若在仓库根 `dotnet --version` 报"找不到兼容 SDK"，按上表安装最新 .NET 10 SDK（machine 级安装需管理员/UAC）。

检查当前机器：

```powershell
scripts\dev\check-env.ps1   # 工具 + 配置 + 端口
scripts\dev\doctor.ps1      # 一键体检
```

> **PowerShell 执行策略**：若 `.ps1` 无法运行，用 `powershell -NoProfile -ExecutionPolicy Bypass -File <脚本>` 调用；或为当前用户放开一次：`Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`。

## 数据库

STOTOP 本地开发连接 SQL Server。连接配置放在（已被 `.gitignore` 忽略，含敏感信息）：

```text
src\STOTOP.WebAPI\db-connections.json
```

样例见 `src\STOTOP.WebAPI\db-connections.example.json`。当前默认开发连接指向**远程库**（本机无需安装 SQL Server，只要网络可达）。WebAPI 启动时通过 `DbConnectionsHelper` 读取并解密密码。换库时参考样例填写后重启后端。

## 一键准备

```powershell
scripts\dev\setup.ps1   # 检查工具 + 安装前端依赖(npm ci) + 检查数据库配置
```

## 后端

```powershell
scripts\dev\build-backend.ps1   # 构建
scripts\dev\backend.ps1         # 启动（编译后直接跑 dll，前台常驻，默认 http://localhost:9000）
scripts\dev\check-health.ps1    # 健康检查（= Invoke-WebRequest http://localhost:9000/health）
```

## 前端

```powershell
scripts\dev\build-frontend.ps1  # 构建
scripts\dev\frontend.ps1        # 启动（Vite，默认 http://localhost:9001）
```

Vite 把 `/api`、`/hangfire`、`/hubs` 代理到 `http://127.0.0.1:9000`。端口被占用时（`strictPort`）不会自动换口，需先释放或换端口。

### 端口可配置（应对端口冲突）

前后端端口可用环境变量覆盖（默认 后端 9000 / 前端 9001，团队默认不变）：

```powershell
$env:BACKEND_PORT='9000'    # 后端端口
$env:FRONTEND_PORT='9001'   # 前端端口（vite server.port 与 hmr 端口都跟随；代理目标按 BACKEND_PORT 自动指向后端）
scripts\dev\backend.ps1     # 后端（建议各开一个窗口）
scripts\dev\frontend.ps1    # 前端
```

`vite.config.ts` 读 `FRONTEND_PORT` / `BACKEND_PORT`；`scripts\dev\*.ps1`（`_common.ps1`）的端口探测/释放也读同名变量。

> **注意端口被异常占用**：某些软件会 `Bound` 大量端口。例如本机 Foxmail 曾被发现绑定 1034~15000 间数千个端口，导致 vite 无法绑定 9001/9000/9001 等。排查：`Get-NetTCPConnection -LocalPort <端口>` 看 OwningProcess，再 `Get-Process -Id <PID>`。重启该软件通常可释放。.NET Kestrel 能与这种 `Bound` socket 共存（后端 9000 仍可用），但 Node/vite 默认独占绑定会冲突。

## 重启开发服务

直接对 Claude 说"重启前后端 / 重启后端 / 重启前端"，会触发 `restart-dev` 技能（Windows 章节）：按端口强制释放 → 后台拉起 `.ps1` → 轮询 health / 首页验证。手动版：

```powershell
. scripts\dev\_common.ps1
Stop-Port 9000; Stop-Port 9001
# 然后分别后台运行 backend.ps1 / frontend.ps1
```

## 测试

```powershell
scripts\dev\test-dotnet.ps1            # 三个模块单测（Finance/CardFlow/Express）
scripts\dev\test-dotnet.ps1 Express    # 按 filter 选模块
scripts\dev\test-contracts.ps1         # scripts\tests\*.mjs 契约测试
scripts\dev\test-contracts.ps1 quotation
```

## Playwright（CardFlow 自动下载用）

后端成功构建后，用构建输出里的 Node CLI 安装浏览器二进制：

```powershell
node src\STOTOP.WebAPI\bin\Debug\net10.0\.playwright\package\cli.js install chromium
```

也可用生成的 `playwright.ps1`（在 bin 输出目录）。常规前后端开发不依赖它。

## 脚本编码约定（重要）

`scripts\dev\*.ps1` 含中文注释，**必须存为 UTF-8 with BOM**。Windows PowerShell 5.1 读取无 BOM 的 UTF-8 `.ps1` 会按 GBK 误读、解析失败（典型报错 `Unexpected token '}'`）。用编辑器"另存为 UTF-8 BOM"，或批量修复：

```powershell
$bom = New-Object System.Text.UTF8Encoding($true)
Get-ChildItem scripts\dev\*.ps1 | ForEach-Object {
  $t = [IO.File]::ReadAllText($_.FullName, [System.Text.UTF8Encoding]::new($false))
  [IO.File]::WriteAllText($_.FullName, $t, $bom)
}
```

## 仓库卫生

从 macOS 拷来的目录会带 `._*`（AppleDouble）与 `.DS_Store` 垃圾，已在 `.gitignore` 忽略。如需再次清理：

```powershell
Get-ChildItem -Recurse -Force -File |
  Where-Object { $_.Name -like '._*' -or $_.Name -eq '.DS_Store' } |
  ForEach-Object { [IO.File]::Delete($_.FullName) }
```

## 与 .sh 脚本的关系

`scripts\dev\*.ps1` 与 `scripts\dev\*.sh` 一一对应，端口与启停行为一致：Windows 用 `.ps1`，macOS/Linux 用 `.sh`，两套并存、互不影响。
