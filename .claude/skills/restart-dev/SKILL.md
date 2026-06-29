---
name: restart-dev
description: 重启 STOTOP 本地开发服务（.NET 后端 + Vite 前端）。当用户说"重启前后端""重启后端""重启前端""重启开发服务""restart dev/servers"等时使用。负责停掉旧进程、用 scripts/dev 脚本后台拉起、并验证健康。
---

# 重启 STOTOP 本地开发服务

重启 .NET 后端和 Vite 前端。端口默认后端 **9000**、前端 **9001**，以 `scripts/dev/_common.sh`（Bash）/ `scripts/dev/_common.ps1`（PowerShell）的 `BACKEND_PORT` / `FRONTEND_PORT` 为准——不要硬编码端口，有疑问先读对应 `_common`。

> **先按操作系统选择章节：**
> - **Windows** → 用下面的 [Windows (PowerShell)](#windows-powershell) 章节，脚本是 `scripts/dev/*.ps1`。
> - **macOS / Linux** → 用 [macOS / Linux (Bash)](#macos--linux-bash) 章节，脚本是 `scripts/dev/*.sh`。
>
> 两套脚本端口与启停行为一致，区别只在进程/端口管理工具与日志路径。

## 范围判定（通用）

- "重启前后端 / 重启开发服务" → 前后端都重启
- "重启后端" → 只做后端相关步骤
- "重启前端" → 只做前端相关步骤

---

## Windows (PowerShell)

> 用 PowerShell 工具执行。常驻脚本（`backend.ps1` / `frontend.ps1`）必须用 `run_in_background: true` 启动，否则会一直阻塞。日志重定向到 `$env:TEMP`。

### 1. 探查现有进程与端口

```powershell
foreach ($p in 9000,9001) {
  $ids = @(Get-NetTCPConnection -LocalPort $p -State Listen -EA SilentlyContinue | Select-Object -Expand OwningProcess -Unique)
  if ($ids) { "  $p: $($ids -join ', ')" } else { "  $p: 空闲" }
}
```

### 2. 释放目标端口（停止旧进程）

> 重启语义下，无条件按目标端口强制释放最可靠（探查有时序差，漏掉监听进程会导致启动时撞端口，Vite 的 `strictPort` 直接失败）。

`_common.ps1` 已提供 `Stop-Port` 函数（先优雅 `CloseMainWindow`，2s 后仍占用则 `Stop-Process -Force`）：

```powershell
. scripts\dev\_common.ps1
# 按范围释放：重启后端→ Stop-Port 9000；重启前端→ Stop-Port 9001；前后端→两者都释放
Stop-Port 9000
Stop-Port 9001
```

### 3. 后台拉起服务

> 关键：用 PowerShell 工具的 `run_in_background: true`。各自把所有输出流（`*>`）重定向到日志文件便于排查。

```powershell
# 后端（编译 + 跑数据库迁移，首次约 10-20s）
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev\backend.ps1 *> $env:TEMP\stotop-backend.log

# 前端（vite，约几秒；node_modules 缺失时脚本会自动 npm install）
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\dev\frontend.ps1 *> $env:TEMP\stotop-frontend.log
```

前后端无启动顺序依赖，可同时拉起。

### 4. 等待就绪、验证、端口占用兜底

用 until 轮询（不要裸 `Start-Sleep`）：

```powershell
# 后端：等 /health 返回 Healthy
$i=0; do { Start-Sleep 3; $i++; try { $h=(Invoke-WebRequest http://localhost:9000/health -UseBasicParsing -TimeoutSec 3).Content } catch { $h='' } } until ($h -match 'Healthy' -or $i -ge 20)
"后端 health: $h"

# 前端：等端口监听后查首页状态码（期望 200）
$j=0; do { Start-Sleep 2; $j++; $up=@(Get-NetTCPConnection -LocalPort 9001 -State Listen -EA SilentlyContinue).Count -gt 0 } until ($up -or $j -ge 10)
try { $code=(Invoke-WebRequest http://localhost:9001/ -UseBasicParsing -TimeoutSec 5).StatusCode } catch { $code='无响应' }
"前端首页: $code"
```

**兜底（端口占用自动恢复）**：若日志出现 `already in use`（端口被抢或释放未生效），自动恢复一次—— `Stop-Port` 释放 → 重新后台拉起 → 重跑该服务验证。例如前端：

```powershell
if (Select-String -Path $env:TEMP\stotop-frontend.log -Pattern 'already in use' -Quiet) {
  Stop-Port 9001
  # 重新后台拉起 frontend.ps1（仍 run_in_background: true），再重跑前端验证块
}
```

后端同理（关键字 `address already in use`，端口 9000）。**只自动重试一次**；二次仍失败转「排查」并如实报告用户。

### 5. 报告结果（Windows）

向用户汇报每个服务的地址、状态（Healthy / HTTP 200）、PID。从后端日志确认迁移耗时是否正常（稳态应只有几秒，会出现「baseline 文件未变化，跳过对齐」「补建 0 项」）：

```powershell
Select-String -Path $env:TEMP\stotop-backend.log -Pattern '数据库迁移开始','数据库迁移完成','baseline 文件未变化','Now listening'
Select-String -Path $env:TEMP\stotop-frontend.log -Pattern 'Local:','ready in'
```

---

## macOS / Linux (Bash)

### 1. 探查现有进程与端口

```bash
ps aux | grep -iE "STOTOP.WebAPI|dotnet run|vite|npm run dev" | grep -v grep | awk '{print $2, $11, $12, $13}'
for p in 9000 9001; do echo "  $p: $(lsof -ti tcp:$p -sTCP:LISTEN 2>/dev/null | tr '\n' ' ' || echo 空闲)"; done
```

端口取值若与默认不同，先 `grep -E 'BACKEND_PORT|FRONTEND_PORT' scripts/dev/_common.sh` 确认。

### 2. 释放目标端口（停止旧进程）

> 不要只依赖步骤 1 `ps` 查到的 PID——`lsof`/`ps` 探查偶有时序差，可能漏掉正在监听的进程，导致跳过停止、启动时撞端口（`strictPort` 直接失败）。**重启语义下，无条件按目标端口强制释放**才可靠。

定义一个按端口释放的辅助函数（先 SIGTERM，2s 后还在就 `kill -9`），后续步骤复用：

```bash
free_port() {
  local port=$1 pids
  pids=$(lsof -ti tcp:$port -sTCP:LISTEN 2>/dev/null)
  [ -z "$pids" ] && { echo "  $port 已空闲"; return; }
  echo "  释放 $port: $pids"; kill $pids 2>/dev/null; sleep 2
  pids=$(lsof -ti tcp:$port -sTCP:LISTEN 2>/dev/null)
  [ -n "$pids" ] && { echo "  $port 仍占，强杀 $pids"; kill -9 $pids 2>/dev/null; sleep 1; }
}
# 按范围释放：重启后端→ free_port 9000；重启前端→ free_port 9001；前后端→两者都释放
```

### 3. 后台拉起服务

> 关键：这两个脚本是**前台常驻**进程，必须用后台方式启动（Bash 工具的 `run_in_background: true`），否则会一直阻塞。各自把日志重定向到文件便于排查。

```bash
# 后端（会编译 + 跑数据库迁移，首次约 10-20s）
bash scripts/dev/backend.sh > /tmp/stotop-backend.log 2>&1

# 前端（vite，约几秒；node_modules 缺失时脚本会自动 npm install）
bash scripts/dev/frontend.sh > /tmp/stotop-frontend.log 2>&1
```

前后端无启动顺序依赖，可同时拉起。

### 4. 等待就绪、验证、并对端口占用兜底重试

用 until 轮询（不要用裸 `sleep`）：

```bash
# 后端：等 /health 返回 Healthy
i=0; until curl -s -m 3 http://localhost:9000/health 2>/dev/null | grep -qi healthy || [ $i -ge 40 ]; do sleep 3; i=$((i+1)); done
echo "后端 health: $(curl -s -m 3 http://localhost:9000/health || echo 无响应)"

# 前端：等端口监听后查首页状态码（期望 200）
j=0; until lsof -ti tcp:9001 -sTCP:LISTEN >/dev/null 2>&1 || [ $j -ge 20 ]; do sleep 2; j=$((j+1)); done
echo "前端首页: $(curl -s -o /dev/null -w '%{http_code}' -m 5 http://localhost:9001/ || echo 无响应)"
```

**兜底（端口占用自动恢复）**：若服务启动后日志出现 `already in use`（步骤 2 之后端口又被抢占，或释放未生效），自动恢复一次——`free_port` 释放 → 重新后台拉起 → 重跑该服务的验证。例如前端：

```bash
if grep -qi "already in use" /tmp/stotop-frontend.log; then
  echo "检测到 9001 被占，释放并重试一次"
  free_port 9001
  bash scripts/dev/frontend.sh > /tmp/stotop-frontend.log 2>&1   # 仍用 run_in_background: true
  # 然后重跑上面的前端验证块
fi
```

后端同理（关键字 `address already in use`，端口 9000）。**只自动重试一次**；二次仍失败转「排查」并如实报告用户。

### 5. 报告结果（Bash）

向用户汇报：每个服务的地址、状态（Healthy / HTTP 200）、PID。
顺带从后端日志确认迁移耗时是否正常（稳态应只有几秒，会出现「baseline 文件未变化，跳过对齐」「补建 0 项」）：

```bash
grep -E "数据库迁移开始|数据库迁移完成|baseline 文件未变化|Now listening" /tmp/stotop-backend.log
grep -iE "Local:|ready in" /tmp/stotop-frontend.log
```

---

## 排查

- **后端起不来 + `address already in use`**：上一步的旧进程没杀干净。Windows：`Stop-Port 9000`；Bash：回到步骤 2 用 `kill -9`。
- **后端崩在迁移**：看后端日志里「迁移执行失败」附近的异常；可用 `SKIP_DB_MIGRATION=true` 临时跳过迁移定位问题。
- **前端 `strictPort` 报错**：目标端口被占；`vite.config.ts` 开了 `strictPort`，不会自动换端口，需先释放该端口。
- **缺 `db-connections.json`**：后端脚本会直接报错退出，提示用户按 `db-connections.example.json` 创建。
- **(Windows) .ps1 报 "Unexpected token"**：脚本编码不是 UTF-8 BOM，PowerShell 5.1 按 GBK 误读中文注释导致解析失败；把脚本重新存为 UTF-8 BOM。
- **(Windows) .ps1 无法运行（ExecutionPolicy 限制）**：用 `powershell -NoProfile -ExecutionPolicy Bypass -File <脚本路径>` 调用。
