#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEBAPI_DIR="$ROOT_DIR/src/STOTOP.WebAPI"
OUTPUT_PATH="${1:-$ROOT_DIR/Temp/baseline-snapshot.json}"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is not installed. Install a .NET SDK that supports net10.0 first." >&2
  exit 127
fi

if [ ! -f "$WEBAPI_DIR/db-connections.json" ]; then
  echo "Missing $WEBAPI_DIR/db-connections.json" >&2
  echo "Create it with a SQL Server development connection before exporting baseline." >&2
  exit 1
fi

cd "$WEBAPI_DIR"
ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}" \
dotnet run --no-build --no-launch-profile -- --export-baseline "$OUTPUT_PATH"
