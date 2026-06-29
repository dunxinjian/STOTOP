#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SLN_FILE="$ROOT_DIR/src/STOTOP.sln"

if [ ! -f "$SLN_FILE" ]; then
  echo "Missing solution file: $SLN_FILE" >&2
  exit 1
fi

missing=0

while IFS= read -r project_file; do
  project_name="$(basename "$project_file" .csproj)"
  if ! grep -q "\"$project_name\"" "$SLN_FILE"; then
    echo "Missing from STOTOP.sln: ${project_file#$ROOT_DIR/src/}"
    missing=1
  fi
done < <(find "$ROOT_DIR/src" -mindepth 2 -maxdepth 2 -name '*.csproj' | sort)

if [ "$missing" -eq 0 ]; then
  echo "STOTOP.sln includes all src/*/*.csproj projects."
fi

exit "$missing"
