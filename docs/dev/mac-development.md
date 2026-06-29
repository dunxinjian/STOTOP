# STOTOP Mac 开发落地指南

这份文档把 `STOTOP-Mac-落地开发清单.md` 里的建议落成仓库内可重复执行的步骤。

## 必备工具

- 支持 `net10.0` 的 .NET SDK
- Node.js + npm
- Xcode Command Line Tools
- Git

检查当前机器：

```bash
./scripts/dev/check-env.sh
```

当前 Mac 已采用用户级安装方式：

- `.NET SDK`: `~/.dotnet`
- `Node.js/npm`: `~/.local/node/current`
- Shell PATH: `~/.zprofile`

## 数据库

STOTOP 本地开发强依赖 SQL Server。Mac 下建议优先使用 SQL 用户名/密码认证，不要把 Windows 集成认证作为默认方案。

本地连接文件放在：

```text
src/STOTOP.WebAPI/db-connections.json
```

该文件会包含敏感连接信息，因此已经被 `.gitignore` 忽略。WebAPI 启动时会通过 `DbConnectionsHelper` 读取它。

可以参考这个样例：

```text
src/STOTOP.WebAPI/db-connections.example.json
```

## 后端

构建：

```bash
./scripts/dev/build-backend.sh
```

启动：

```bash
./scripts/dev/backend.sh
```

后端默认监听：

```text
http://localhost:9000
```

健康检查：

```bash
./scripts/dev/check-health.sh
```

## 前端

构建：

```bash
./scripts/dev/build-frontend.sh
```

启动：

```bash
./scripts/dev/frontend.sh
```

前端默认监听：

```text
http://localhost:9001
```

Vite 会把 `/api`、`/hangfire`、`/hubs` 代理到 `http://127.0.0.1:9000`。

## Playwright

CardFlow 自动下载功能使用 Playwright。NuGet 包恢复后，如果需要运行自动下载功能，还要安装 Playwright 浏览器二进制。

具体命令会随 SDK 和包版本略有差异。后端成功构建后，如果 `src/STOTOP.WebAPI/bin` 下生成了 Playwright 安装脚本，就从 WebAPI 输出目录执行它。

当前机器没有安装 PowerShell，所以 `.NET` 生成的 `playwright.ps1` 不能直接执行。可以改用构建输出中的 Node CLI：

```bash
node src/STOTOP.WebAPI/bin/Debug/net10.0/.playwright/package/cli.js install chromium
```

本次尝试下载 Chromium 时，下载完成后进程在缓存解压/锁阶段卡住；常规后端和前端开发不受影响，但 CardFlow 自动下载功能仍需要后续重新验证 Playwright 浏览器安装。

## 仓库卫生

仓库已经补了 `.gitattributes`，用于减少 Windows/macOS 换行差异造成的无意义 diff。

以下生成文件不应进入 Git：

- `bin/`
- `obj/`
- `node_modules/`
- `dist/`
- `.venv/`
- `Temp/`
- `Report/`
- `*.log`

## Solution 检查

检查 `src/STOTOP.sln` 是否包含所有模块：

```bash
./scripts/dev/check-sln.sh
```

当前已知：CardFlow、Insurance、Quality、Workflow 等项目存在于源码目录，也被 WebAPI 引用，但还没有进入 `STOTOP.sln`。

## 已知后续项

- 安装 .NET SDK 和 npm 后，再执行后端/前端构建验收。
- 根目录里还有一些 Windows 时代的一次性工具和业务资料目录，建议确认后归档或迁出主仓库。
