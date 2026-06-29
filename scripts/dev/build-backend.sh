#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is not installed. Install a .NET SDK that supports net10.0 first." >&2
  exit 127
fi

cd "$ROOT_DIR/src"
dotnet restore STOTOP.WebAPI/STOTOP.WebAPI.csproj
dotnet build STOTOP.WebAPI/STOTOP.WebAPI.csproj --no-restore -m:1 /p:UseSharedCompilation=false
