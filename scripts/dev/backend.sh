#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEBAPI_DIR="$ROOT_DIR/src/STOTOP.WebAPI"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is not installed. Install a .NET SDK that supports net10.0 first." >&2
  exit 127
fi

if [ ! -f "$WEBAPI_DIR/db-connections.json" ]; then
  echo "Missing $WEBAPI_DIR/db-connections.json" >&2
  echo "Create it with a SQL Server development connection before starting WebAPI." >&2
  exit 1
fi

cd "$WEBAPI_DIR"

# 显式 build 后直接跑 dll，绕开 `dotnet run` 的 launcher：
# `dotnet run` 会另起一个 apphost 子进程来监听端口，停服务时容易留下孤儿占着端口。
# `dotnet <dll>` 是同进程加载运行时、不再 fork apphost——真正的服务就是这个 dotnet 进程。
# 注意：不要用 `exec`。后台运行时（run_in_background）靠这层 bash 包装进程维持存活，
# exec 把它替换掉会导致服务在后续前台命令时被连带收掉。保留 bash 包装 → dotnet 子进程即可，
# bash 那层不持端口、kill 即净，没有隐藏的 apphost 孤儿。
CONFIGURATION="${CONFIGURATION:-Debug}"
TARGET_FRAMEWORK="${TARGET_FRAMEWORK:-net10.0}"
DLL_PATH="$WEBAPI_DIR/bin/$CONFIGURATION/$TARGET_FRAMEWORK/STOTOP.WebAPI.dll"

dotnet build -c "$CONFIGURATION" --nologo

if [ ! -f "$DLL_PATH" ]; then
  echo "Build did not produce $DLL_PATH" >&2
  exit 1
fi

ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}" \
ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:9000}" \
DOTNET_hostBuilder__reloadConfigOnChange="${DOTNET_hostBuilder__reloadConfigOnChange:-false}" \
ASPNETCORE_hostBuilder__reloadConfigOnChange="${ASPNETCORE_hostBuilder__reloadConfigOnChange:-false}" \
dotnet "$DLL_PATH"
