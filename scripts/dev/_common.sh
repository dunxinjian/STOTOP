#!/usr/bin/env bash

STOTOP_DEV_COMMON_SOURCED=1

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
SRC_DIR="$ROOT_DIR/src"
WEBAPI_DIR="$ROOT_DIR/src/STOTOP.WebAPI"
WEB_DIR="$ROOT_DIR/web"
SLN_FILE="$ROOT_DIR/src/STOTOP.sln"
BACKEND_PORT="${BACKEND_PORT:-9000}"
FRONTEND_PORT="${FRONTEND_PORT:-9001}"
BACKEND_URL_DEFAULT="http://localhost:${BACKEND_PORT}"
FRONTEND_URL_DEFAULT="http://localhost:${FRONTEND_PORT}"

print_section() {
  printf '\n== %s ==\n' "$1"
}

status_ok() {
  printf 'OK   %s\n' "$1"
}

status_warn() {
  printf 'WARN %s\n' "$1"
}

status_fail() {
  printf 'FAIL %s\n' "$1"
}

has_command() {
  command -v "$1" >/dev/null 2>&1
}

require_command() {
  local command_name="$1"
  local install_hint="$2"

  if ! has_command "$command_name"; then
    status_fail "$command_name: missing"
    printf '%s\n' "$install_hint" >&2
    exit 127
  fi
}

print_command_version() {
  local label="$1"
  shift

  if has_command "$1"; then
    local version_output
    if version_output="$("$@" 2>&1)"; then
      status_ok "$label: $version_output"
    else
      status_warn "$label: command failed ($version_output)"
    fi
  else
    status_fail "$label: missing"
  fi
}

port_is_listening() {
  local port="$1"

  if [[ ! "$port" =~ ^[0-9]+$ ]] || [ "$port" -lt 1 ] || [ "$port" -gt 65535 ]; then
    return 64
  fi

  if ! has_command lsof; then
    return 127
  fi

  lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1
}

print_port_status() {
  local label="$1"
  local port="$2"

  if port_is_listening "$port"; then
    status_ok "$label port $port: listening"
  else
    local result=$?
    if [ "$result" -eq 127 ]; then
      status_warn "$label port $port: lsof missing, listener not checked"
    elif [ "$result" -eq 64 ]; then
      status_warn "$label port $port: invalid port"
    elif [ "$result" -eq 1 ]; then
      status_warn "$label port $port: not listening"
    else
      status_warn "$label port $port: listener check failed with lsof exit $result"
    fi
  fi
}

db_config_path() {
  printf '%s/db-connections.json\n' "$WEBAPI_DIR"
}

db_config_example_path() {
  printf '%s/db-connections.example.json\n' "$WEBAPI_DIR"
}
