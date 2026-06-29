# STOTOP Development Environment Tooling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 STOTOP 建立仓库内可重复的本地开发工具链，覆盖环境检查、依赖准备、启动、构建、测试、preflight 和文档入口。

**Architecture:** 保留 `scripts/dev` 作为脚本边界，新增共享 Bash helper 和少量组合脚本，避免复制端口、路径和状态输出逻辑。配置文件只声明开发约定，文档入口负责解释命令语义；`preflight.sh` 分清阻塞项、已知债务项和跳过项。

**Tech Stack:** Bash、.NET 10、Node.js 24/npm、Vite、VS Code JSONC、Markdown、Git executable-bit metadata。

---

## File Structure

- Create `.editorconfig`: repository-wide text formatting defaults.
- Create `global.json`: .NET SDK selection for `net10.0` development.
- Create `.node-version`: Node major-version hint for frontend development.
- Modify `.vscode/settings.json`: preserve existing Windows/Qoder settings and add macOS/Linux/editor defaults.
- Create `.vscode/extensions.json`: recommended editor extensions.
- Create `.vscode/tasks.json`: VS Code task wrappers around repository scripts.
- Create `scripts/dev/_common.sh`: shared paths, status output, command checks, and port listener helpers.
- Modify `scripts/dev/check-env.sh`: use shared paths and emit clearer tool/config/dependency status.
- Create `scripts/dev/setup.sh`: one-time local dependency preparation without writing secrets.
- Create `scripts/dev/doctor.sh`: read-only diagnostic summary.
- Create `scripts/dev/build-all.sh`: backend and frontend production build wrapper.
- Create `scripts/dev/test-contracts.sh`: Node contract test runner with filename filtering.
- Create `scripts/dev/test-dotnet.sh`: .NET module test runner with project filtering.
- Create `scripts/dev/preflight.sh`: local pre-submit/pre-release check with summary categories.
- Modify `README.md`: Chinese root developer entry.
- Create `README.en.md`: short English entry.
- Create `web/README.md`: frontend-specific developer entry.
- Modify `docs/dev/mac-development.md`: align Mac guide with unified scripts and known debt handling.

Current worktree note: `README.md`, `README.en.md`, `web/README.md`, and many `docs/design` files already have user-side modifications or deletions. During implementation, inspect `git status --short --branch` before each task and stage only the files listed in that task.

### Task 1: Add Repository Toolchain Configuration

**Files:**
- Create: `.editorconfig`
- Create: `global.json`
- Create: `.node-version`
- Create: `.vscode/extensions.json`
- Create: `.vscode/tasks.json`
- Modify: `.vscode/settings.json`

- [ ] **Step 1: Confirm missing baseline config files**

Run:

```bash
test ! -f .editorconfig
test ! -f global.json
test ! -f .node-version
test ! -f .vscode/extensions.json
test ! -f .vscode/tasks.json
```

Expected: all commands exit `0` in the current checkout before this task starts.

- [ ] **Step 2: Create `.editorconfig`**

Write `.editorconfig` with this content:

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 2

[*.{cs,csproj,props,targets,sln}]
indent_size = 4

[*.{sh,yml,yaml,json,jsonc,md,ts,tsx,vue,scss,css,html}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false

[*.sln]
indent_style = tab
```

- [ ] **Step 3: Create `global.json`**

Write `global.json` with this content:

```json
{
  "sdk": {
    "version": "10.0.300",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

- [ ] **Step 4: Create `.node-version`**

Write `.node-version` with this content:

```text
24
```

- [ ] **Step 5: Create `.vscode/extensions.json`**

Write `.vscode/extensions.json` with this content:

```json
{
  "recommendations": [
    "EditorConfig.EditorConfig",
    "Vue.volar",
    "ms-dotnettools.csharp",
    "ms-dotnettools.csdevkit",
    "dbaeumer.vscode-eslint"
  ]
}
```

- [ ] **Step 6: Replace `.vscode/settings.json` while preserving current Qoder exclusions**

Replace `.vscode/settings.json` with this JSONC:

```jsonc
{
  "terminal.integrated.defaultProfile.osx": "zsh",
  "terminal.integrated.defaultProfile.linux": "bash",
  "terminal.integrated.defaultProfile.windows": "Git Bash",
  "terminal.integrated.env.windows": {
    "PYTHONIOENCODING": "utf-8",
    "LANG": "en_US.UTF-8",
    "DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTED": "1"
  },
  "terminal.integrated.profiles.windows": {
    "Git Bash": {
      "path": "C:\\Program Files\\Git\\bin\\bash.exe",
      "icon": "terminal-bash"
    },
    "PowerShell": {
      "source": "PowerShell",
      "args": ["-NoExit", "-Command", "chcp 65001 > $null; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8"]
    },
    "Windows PowerShell": {
      "path": "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe",
      "args": ["-NoExit", "-Command", "chcp 65001 > $null; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8"]
    }
  },
  "files.eol": "\n",
  "files.insertFinalNewline": true,
  "editor.formatOnSave": false,
  "typescript.tsdk": "web/node_modules/typescript/lib",
  "dotnet.completion.showCompletionItemsFromUnimportedNamespaces": true,
  "csharp.semanticHighlighting.enabled": true,

  "files.exclude": {
    "**/__pycache__": true,
    "**/*.pyc": true,
    "BizObjTransform/Taicang/transform_script/output": true,
    "BizObjTransform/Taicang/*.sql": true,
    "BizObjTransform/Taicang/*.json": true
  },
  "search.exclude": {
    "**/__pycache__": true,
    "**/*.pyc": true,
    "BizObjTransform/**/*.sql": true,
    "BizObjTransform/**/*.json": true,
    "BizObjTransform/Taicang/transform_script/output/**": true,
    "**/bin/**": true,
    "**/obj/**": true,
    "**/node_modules/**": true,
    "**/dist/**": true
  },
  "workbench.editor.enablePreview": true,
  "workbench.editor.enablePreviewFromQuickOpen": true,
  "workbench.editor.revealIfOpen": false,
  "workbench.editor.focusRecentEditorAfterClose": false,
  "files.watcherExclude": {
    "**/__pycache__/**": true,
    "**/*.pyc": true,
    "BizObjTransform/**/*.sql": true,
    "BizObjTransform/**/*.json": true,
    "BizObjTransform/Taicang/transform_script/output/**": true,
    "**/bin/**": true,
    "**/obj/**": true,
    "**/node_modules/**": true,
    "**/dist/**": true
  }
}
```

- [ ] **Step 7: Create `.vscode/tasks.json`**

Write `.vscode/tasks.json` with this content:

```jsonc
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "STOTOP: doctor",
      "type": "shell",
      "command": "${workspaceFolder}/scripts/dev/doctor.sh",
      "problemMatcher": [],
      "group": "test"
    },
    {
      "label": "STOTOP: setup",
      "type": "shell",
      "command": "${workspaceFolder}/scripts/dev/setup.sh",
      "problemMatcher": [],
      "group": "build"
    },
    {
      "label": "STOTOP: backend",
      "type": "shell",
      "command": "${workspaceFolder}/scripts/dev/backend.sh",
      "problemMatcher": {
        "owner": "stotop-backend",
        "pattern": {
          "regexp": "^$"
        },
        "background": {
          "activeOnStart": true,
          "beginsPattern": ".",
          "endsPattern": "Now listening on:"
        }
      },
      "isBackground": true
    },
    {
      "label": "STOTOP: frontend",
      "type": "shell",
      "command": "${workspaceFolder}/scripts/dev/frontend.sh",
      "problemMatcher": {
        "owner": "stotop-frontend",
        "pattern": {
          "regexp": "^$"
        },
        "background": {
          "activeOnStart": true,
          "beginsPattern": ".",
          "endsPattern": "Local:"
        }
      },
      "isBackground": true
    },
    {
      "label": "STOTOP: build all",
      "type": "shell",
      "command": "${workspaceFolder}/scripts/dev/build-all.sh",
      "problemMatcher": "$msCompile",
      "group": "build"
    },
    {
      "label": "STOTOP: contract tests",
      "type": "shell",
      "command": "${workspaceFolder}/scripts/dev/test-contracts.sh",
      "problemMatcher": [],
      "group": "test"
    },
    {
      "label": "STOTOP: dotnet tests",
      "type": "shell",
      "command": "${workspaceFolder}/scripts/dev/test-dotnet.sh",
      "problemMatcher": "$msCompile",
      "group": "test"
    },
    {
      "label": "STOTOP: preflight",
      "type": "shell",
      "command": "${workspaceFolder}/scripts/dev/preflight.sh",
      "problemMatcher": "$msCompile",
      "group": "test"
    }
  ]
}
```

- [ ] **Step 8: Validate JSON-bearing files**

Run:

```bash
node -e "JSON.parse(require('fs').readFileSync('global.json','utf8')); JSON.parse(require('fs').readFileSync('.vscode/extensions.json','utf8')); console.log('json ok')"
```

Expected: prints `json ok`.

- [ ] **Step 9: Commit Task 1 files only**

Run:

```bash
git add .editorconfig global.json .node-version .vscode/settings.json .vscode/extensions.json .vscode/tasks.json
git commit -m "chore: add development toolchain config"
```

Expected: commit includes only the six listed paths.

### Task 2: Add Shared Script Helpers and Improve Environment Check

**Files:**
- Create: `scripts/dev/_common.sh`
- Modify: `scripts/dev/check-env.sh`

- [ ] **Step 1: Confirm current scripts parse before changes**

Run:

```bash
bash -n scripts/dev/check-env.sh
```

Expected: exit code `0`.

- [ ] **Step 2: Create `scripts/dev/_common.sh`**

Write `scripts/dev/_common.sh` with this content:

```bash
#!/usr/bin/env bash

if [ -n "${STOTOP_DEV_COMMON_SOURCED:-}" ]; then
  return 0
fi

STOTOP_DEV_COMMON_SOURCED=1

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
SRC_DIR="$ROOT_DIR/src"
WEBAPI_DIR="$ROOT_DIR/src/STOTOP.WebAPI"
WEB_DIR="$ROOT_DIR/web"
SLN_FILE="$ROOT_DIR/src/STOTOP.sln"
BACKEND_PORT="${BACKEND_PORT:-5001}"
FRONTEND_PORT="${FRONTEND_PORT:-5173}"
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
    printf 'OK   %s: ' "$label"
    "$@"
  else
    status_fail "$label: missing"
  fi
}

port_is_listening() {
  local port="$1"

  if ! has_command lsof; then
    return 2
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
    if [ "$result" -eq 2 ]; then
      status_warn "$label port $port: lsof missing, listener not checked"
    else
      status_warn "$label port $port: not listening"
    fi
  fi
}

db_config_path() {
  printf '%s/db-connections.json\n' "$WEBAPI_DIR"
}

db_config_example_path() {
  printf '%s/db-connections.example.json\n' "$WEBAPI_DIR"
}
```

- [ ] **Step 3: Replace `scripts/dev/check-env.sh`**

Replace `scripts/dev/check-env.sh` with this content:

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

printf 'STOTOP development environment check\n'

print_section "Tools"
print_command_version "dotnet" dotnet --version
print_command_version "node" node --version
print_command_version "npm" npm --version
print_command_version "git" git --version

if has_command xcode-select && xcode-select -p >/dev/null 2>&1; then
  status_ok "xcode cli: $(xcode-select -p)"
elif [[ "$(uname -s)" == "Darwin" ]]; then
  status_warn "xcode cli: missing"
else
  status_ok "xcode cli: not required on $(uname -s)"
fi

print_section "Configuration"
if [ -f "$(db_config_path)" ]; then
  status_ok "database config: present"
else
  status_warn "database config: missing ($(db_config_path))"
  status_warn "database example: $(db_config_example_path)"
fi

if [ -f "$WEB_DIR/package-lock.json" ]; then
  status_ok "frontend lockfile: present"
else
  status_fail "frontend lockfile: missing ($WEB_DIR/package-lock.json)"
fi

if [ -d "$WEB_DIR/node_modules" ]; then
  status_ok "frontend dependencies: installed"
else
  status_warn "frontend dependencies: missing"
fi

print_section "Ports"
print_port_status "backend" "$BACKEND_PORT"
print_port_status "frontend" "$FRONTEND_PORT"
```

- [ ] **Step 4: Validate shell syntax**

Run:

```bash
bash -n scripts/dev/_common.sh
bash -n scripts/dev/check-env.sh
```

Expected: both commands exit `0`.

- [ ] **Step 5: Run the improved environment check**

Run:

```bash
./scripts/dev/check-env.sh
```

Expected: prints tool, configuration, and port sections. It may warn if backend or frontend is not listening.

- [ ] **Step 6: Commit Task 2 files only**

Run:

```bash
git add scripts/dev/_common.sh scripts/dev/check-env.sh
git commit -m "chore: improve development environment check"
```

Expected: commit includes only `_common.sh` and `check-env.sh`.

### Task 3: Add Setup and Doctor Scripts

**Files:**
- Create: `scripts/dev/setup.sh`
- Create: `scripts/dev/doctor.sh`

- [ ] **Step 1: Verify scripts are absent before creation**

Run:

```bash
test ! -f scripts/dev/setup.sh
test ! -f scripts/dev/doctor.sh
```

Expected: both commands exit `0`.

- [ ] **Step 2: Create `scripts/dev/setup.sh`**

Write `scripts/dev/setup.sh` with this content:

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

print_section "Required tools"
require_command dotnet "Install a .NET SDK that supports net10.0."
require_command node "Install Node.js 24 with npm."
require_command npm "Install npm with Node.js 24."
require_command git "Install Git."

"$SCRIPT_DIR/check-env.sh"

print_section "Frontend dependencies"
if [ ! -f "$WEB_DIR/package-lock.json" ]; then
  status_fail "missing $WEB_DIR/package-lock.json"
  exit 1
fi

if [ -d "$WEB_DIR/node_modules" ]; then
  status_ok "node_modules already installed"
else
  status_warn "node_modules missing; running npm install"
  (cd "$WEB_DIR" && npm install)
  status_ok "frontend dependencies installed"
fi

print_section "Database configuration"
if [ -f "$(db_config_path)" ]; then
  status_ok "database config present: $(db_config_path)"
else
  status_warn "database config missing: $(db_config_path)"
  status_warn "copy $(db_config_example_path) to $(db_config_path) and fill a real SQL Server development connection"
fi

print_section "Next steps"
printf 'Run backend:  %s/backend.sh\n' "$SCRIPT_DIR"
printf 'Run frontend: %s/frontend.sh\n' "$SCRIPT_DIR"
printf 'Run doctor:   %s/doctor.sh\n' "$SCRIPT_DIR"
```

- [ ] **Step 3: Create `scripts/dev/doctor.sh`**

Write `scripts/dev/doctor.sh` with this content:

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

printf 'STOTOP development doctor\n'

print_section "Tools"
print_command_version "dotnet" dotnet --version
print_command_version "node" node --version
print_command_version "npm" npm --version
print_command_version "git" git --version

print_section "Ports"
print_port_status "backend" "$BACKEND_PORT"
print_port_status "frontend" "$FRONTEND_PORT"

print_section "Configuration"
if [ -f "$(db_config_path)" ]; then
  status_ok "database config present"
else
  status_warn "database config missing: $(db_config_path)"
  status_warn "database example: $(db_config_example_path)"
fi

if [ -d "$WEB_DIR/node_modules" ]; then
  status_ok "frontend dependencies installed"
else
  status_warn "frontend dependencies missing; run $SCRIPT_DIR/setup.sh"
fi

print_section "Solution coverage"
if sln_output="$("$SCRIPT_DIR/check-sln.sh" 2>&1)"; then
  status_ok "solution coverage"
  printf '%s\n' "$sln_output"
else
  status_warn "solution coverage has known gaps"
  printf '%s\n' "$sln_output"
fi

print_section "Suggested commands"
if [ ! -d "$WEB_DIR/node_modules" ]; then
  printf '%s/setup.sh\n' "$SCRIPT_DIR"
fi

if ! port_is_listening "$BACKEND_PORT"; then
  printf '%s/backend.sh\n' "$SCRIPT_DIR"
fi

if ! port_is_listening "$FRONTEND_PORT"; then
  printf '%s/frontend.sh\n' "$SCRIPT_DIR"
fi

printf '%s/build-all.sh\n' "$SCRIPT_DIR"
printf '%s/preflight.sh\n' "$SCRIPT_DIR"
```

- [ ] **Step 4: Validate shell syntax**

Run:

```bash
bash -n scripts/dev/setup.sh
bash -n scripts/dev/doctor.sh
```

Expected: both commands exit `0`.

- [ ] **Step 5: Run `doctor.sh`**

Run:

```bash
./scripts/dev/doctor.sh
```

Expected: command exits `0` and may print a solution coverage warning for `STOTOP.Module.Insurance` and `STOTOP.Module.Quality` until those projects are added to `src/STOTOP.sln`.

- [ ] **Step 6: Commit Task 3 files only**

Run:

```bash
git add scripts/dev/setup.sh scripts/dev/doctor.sh
git commit -m "chore: add setup and doctor scripts"
```

Expected: commit includes only `setup.sh` and `doctor.sh`.

### Task 4: Add Build, Test, and Preflight Entrypoints

**Files:**
- Create: `scripts/dev/build-all.sh`
- Create: `scripts/dev/test-contracts.sh`
- Create: `scripts/dev/test-dotnet.sh`
- Create: `scripts/dev/preflight.sh`

- [ ] **Step 1: Verify entrypoint scripts are absent before creation**

Run:

```bash
test ! -f scripts/dev/build-all.sh
test ! -f scripts/dev/test-contracts.sh
test ! -f scripts/dev/test-dotnet.sh
test ! -f scripts/dev/preflight.sh
```

Expected: all commands exit `0`.

- [ ] **Step 2: Create `scripts/dev/build-all.sh`**

Write `scripts/dev/build-all.sh` with this content:

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

print_section "Backend build"
"$SCRIPT_DIR/build-backend.sh"

print_section "Frontend build"
"$SCRIPT_DIR/build-frontend.sh"

print_section "Build summary"
status_ok "backend build passed"
status_ok "frontend Vite build passed"
status_warn "frontend type-check is separate: cd web && npm run type-check"
```

- [ ] **Step 3: Create `scripts/dev/test-contracts.sh`**

Write `scripts/dev/test-contracts.sh` with this content:

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

require_command node "Install Node.js 24 with npm."

filter="${1:-}"
shopt -s nullglob
all_tests=("$ROOT_DIR"/scripts/tests/*.mjs)
selected_tests=()

for test_file in "${all_tests[@]}"; do
  test_name="$(basename "$test_file")"
  if [ -z "$filter" ] || [[ "$test_name" == *"$filter"* ]]; then
    selected_tests+=("$test_file")
  fi
done

if [ "${#selected_tests[@]}" -eq 0 ]; then
  status_fail "no contract tests matched filter: ${filter:-<none>}"
  exit 1
fi

failures=0
for test_file in "${selected_tests[@]}"; do
  print_section "Contract test: ${test_file#$ROOT_DIR/}"
  if node "$test_file"; then
    status_ok "${test_file#$ROOT_DIR/}"
  else
    status_fail "${test_file#$ROOT_DIR/}"
    failures=$((failures + 1))
  fi
done

print_section "Contract test summary"
if [ "$failures" -eq 0 ]; then
  status_ok "${#selected_tests[@]} contract test file(s) passed"
else
  status_fail "$failures of ${#selected_tests[@]} contract test file(s) failed"
  exit 1
fi
```

- [ ] **Step 4: Create `scripts/dev/test-dotnet.sh`**

Write `scripts/dev/test-dotnet.sh` with this content:

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

require_command dotnet "Install a .NET SDK that supports net10.0."

filter="${1:-}"
projects=(
  "Finance:$ROOT_DIR/tests/STOTOP.Module.Finance.Tests/STOTOP.Module.Finance.Tests.csproj"
  "CardFlow:$ROOT_DIR/tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj"
  "Express:$ROOT_DIR/tests/STOTOP.Module.Express.Tests/STOTOP.Module.Express.Tests.csproj"
)

selected_projects=()
for project_entry in "${projects[@]}"; do
  project_name="${project_entry%%:*}"
  project_path="${project_entry#*:}"
  if [ -z "$filter" ] || [[ "$project_name" == *"$filter"* ]] || [[ "$project_path" == *"$filter"* ]]; then
    selected_projects+=("$project_entry")
  fi
done

if [ "${#selected_projects[@]}" -eq 0 ]; then
  status_fail "no .NET test projects matched filter: ${filter:-<none>}"
  exit 1
fi

failures=0
for project_entry in "${selected_projects[@]}"; do
  project_name="${project_entry%%:*}"
  project_path="${project_entry#*:}"
  print_section ".NET tests: $project_name"
  if dotnet test "$project_path" -m:1 /p:UseSharedCompilation=false; then
    status_ok "$project_name tests"
  else
    status_fail "$project_name tests"
    failures=$((failures + 1))
  fi
done

print_section ".NET test summary"
if [ "$failures" -eq 0 ]; then
  status_ok "${#selected_projects[@]} .NET test project(s) passed"
else
  status_fail "$failures of ${#selected_projects[@]} .NET test project(s) failed"
  exit 1
fi
```

- [ ] **Step 5: Create `scripts/dev/preflight.sh`**

Write `scripts/dev/preflight.sh` with this content:

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_common.sh"

passed=()
blockers=()
debts=()
skipped=()

record_pass() {
  passed+=("$1")
  status_ok "$1"
}

record_blocker() {
  blockers+=("$1")
  status_fail "$1"
}

record_debt() {
  debts+=("$1")
  status_warn "$1"
}

run_required() {
  local name="$1"
  shift
  print_section "$name"
  if "$@"; then
    record_pass "$name"
  else
    record_blocker "$name"
  fi
}

run_debt() {
  local name="$1"
  shift
  print_section "$name"
  if "$@"; then
    record_pass "$name"
  else
    record_debt "$name"
  fi
}

run_required "doctor" "$SCRIPT_DIR/doctor.sh"

if [ "${STOTOP_PREFLIGHT_STRICT_SLN:-0}" = "1" ]; then
  run_required "solution coverage" "$SCRIPT_DIR/check-sln.sh"
else
  run_debt "solution coverage" "$SCRIPT_DIR/check-sln.sh"
fi

run_required "backend build" "$SCRIPT_DIR/build-backend.sh"
run_required "frontend Vite build" "$SCRIPT_DIR/build-frontend.sh"
run_required "Node contract tests" "$SCRIPT_DIR/test-contracts.sh"
run_required ".NET module tests" "$SCRIPT_DIR/test-dotnet.sh"

if [ "${STOTOP_PREFLIGHT_TYPECHECK:-0}" = "1" ]; then
  run_debt "frontend type-check" bash -lc "cd '$WEB_DIR' && npm run type-check"
else
  skipped+=("frontend type-check: set STOTOP_PREFLIGHT_TYPECHECK=1 to run known debt check")
  status_warn "frontend type-check skipped"
fi

print_section "Preflight summary"
printf 'Passed:\n'
for item in "${passed[@]}"; do
  printf '  - %s\n' "$item"
done

printf 'Blockers:\n'
if [ "${#blockers[@]}" -eq 0 ]; then
  printf '  - none\n'
else
  for item in "${blockers[@]}"; do
    printf '  - %s\n' "$item"
  done
fi

printf 'Known debt:\n'
if [ "${#debts[@]}" -eq 0 ]; then
  printf '  - none\n'
else
  for item in "${debts[@]}"; do
    printf '  - %s\n' "$item"
  done
fi

printf 'Skipped:\n'
if [ "${#skipped[@]}" -eq 0 ]; then
  printf '  - none\n'
else
  for item in "${skipped[@]}"; do
    printf '  - %s\n' "$item"
  done
fi

if [ "${#blockers[@]}" -gt 0 ]; then
  exit 1
fi
```

- [ ] **Step 6: Validate shell syntax**

Run:

```bash
bash -n scripts/dev/build-all.sh
bash -n scripts/dev/test-contracts.sh
bash -n scripts/dev/test-dotnet.sh
bash -n scripts/dev/preflight.sh
```

Expected: all commands exit `0`.

- [ ] **Step 7: Run focused test entrypoints**

Run:

```bash
./scripts/dev/test-contracts.sh cardflow
./scripts/dev/test-dotnet.sh Finance
```

Expected: each command either passes or reports the exact failing test command. If a failure is unrelated to this task, keep the output for the final implementation report.

- [ ] **Step 8: Commit Task 4 files only**

Run:

```bash
git add scripts/dev/build-all.sh scripts/dev/test-contracts.sh scripts/dev/test-dotnet.sh scripts/dev/preflight.sh
git commit -m "chore: add development build and test entrypoints"
```

Expected: commit includes only the four new scripts.

### Task 5: Update Developer Documentation Entrypoints

**Files:**
- Modify: `README.md`
- Create: `README.en.md`
- Create: `web/README.md`
- Modify: `docs/dev/mac-development.md`

- [ ] **Step 1: Inspect current documentation state**

Run:

```bash
git status --short -- README.md README.en.md web/README.md docs/dev/mac-development.md
```

Expected in the current checkout: `README.md` is modified by existing user-side work, `README.en.md` and `web/README.md` may appear deleted, and `docs/dev/mac-development.md` is unchanged before this task.

- [ ] **Step 2: Replace `README.md` with the consolidated Chinese entry**

Write `README.md` with this content:

```markdown
# STOTOP

STOTOP 是面向快递、财务、CRM、任务、质量、合同、行政后勤等场景的一体化企业管理系统。当前审批与卡片流转统一以 CardFlow 为运行时，历史 OA 控制器不再作为新业务入口暴露。

## 技术栈

- 后端：.NET 10、ASP.NET Core、EF Core、SQL Server、Hangfire、SignalR
- 前端：Vue 3、Vite、TypeScript、Pinia、Ant Design Vue、Vant
- 数据库连接：`src/STOTOP.WebAPI/db-connections.json` 是系统连接的运行时来源
- 本地后端端口：`http://localhost:5001`
- 本地前端端口：`http://localhost:5173`

## 新机器快速开始

```bash
./scripts/dev/setup.sh
./scripts/dev/doctor.sh
```

`setup.sh` 会检查必备工具、安装前端依赖，并提示数据库连接文件状态。它不会写入真实数据库连接，也不会初始化数据库。

本地数据库连接文件放在：

```text
src/STOTOP.WebAPI/db-connections.json
```

可从样例复制后填写真实开发库连接：

```text
src/STOTOP.WebAPI/db-connections.example.json
```

## 本地启动

后端默认监听 `5001`：

```bash
./scripts/dev/backend.sh
```

前端默认监听 `5173`，并代理 `/api`、`/hangfire`、`/hubs` 到后端 `5001`：

```bash
./scripts/dev/frontend.sh
```

健康检查：

```bash
./scripts/dev/check-health.sh
curl -I http://localhost:5173/
```

## 构建与测试

后端构建：

```bash
./scripts/dev/build-backend.sh
```

前端 Vite 构建：

```bash
./scripts/dev/build-frontend.sh
```

后端和前端构建：

```bash
./scripts/dev/build-all.sh
```

Node contract tests：

```bash
./scripts/dev/test-contracts.sh
./scripts/dev/test-contracts.sh cardflow
```

.NET 模块测试：

```bash
./scripts/dev/test-dotnet.sh
./scripts/dev/test-dotnet.sh Finance
```

提交或发布前本地检查：

```bash
./scripts/dev/preflight.sh
```

前端类型检查是单独信号：

```bash
cd web
npm run type-check
```

当前仓库存在历史 TypeScript 类型债，因此 `npm run build` 和 `npm run type-check` 的结果需要分开判断。

## 当前边界

- CardFlow 承接动态表单、审批节点、待办、预算/财务流程钩子和导入校验工作台。
- Workflow 仍作为事件、派发、质量处理等底层协作能力保留。
- OA 项目与 Seeder 只保留历史数据迁移、引用兼容和退役清理用途，不再注册 OA 控制器。
- 旧 DataCenter 设计已并入 CardFlow 的导入、暂存、校验、自动插件能力，不应新增独立 `STOTOP.Module.DataCenter`。

## 文档入口

- [Mac 本地开发](docs/dev/mac-development.md)
- [系统总览](design/00-overview.md)
- [WebAPI 启动层](design/19-webapi.md)
- [钉钉 H5 微应用部署](docs/dingtalk-h5-setup.md)
- [开发环境工具工程化设计](docs/superpowers/specs/2026-06-12-dev-environment-tooling-design.md)
```

- [ ] **Step 3: Recreate `README.en.md`**

Write `README.en.md` with this content:

```markdown
# STOTOP

STOTOP is an integrated enterprise management system for logistics, finance, CRM, task management, quality, contracts, and administrative operations.

## Local Development

Run the local setup and diagnostics from the repository root:

```bash
./scripts/dev/setup.sh
./scripts/dev/doctor.sh
```

Default local endpoints:

- Backend WebAPI: `http://localhost:5001`
- Frontend Vite dev server: `http://localhost:5173`

Start services:

```bash
./scripts/dev/backend.sh
./scripts/dev/frontend.sh
```

Build and test:

```bash
./scripts/dev/build-all.sh
./scripts/dev/test-contracts.sh
./scripts/dev/test-dotnet.sh
./scripts/dev/preflight.sh
```

The frontend Vite build and TypeScript type-check are intentionally separate. Use `npm run build` for the production frontend build and `npm run type-check` to inspect the existing type-check debt.

Primary Chinese documentation:

- `README.md`
- `docs/dev/mac-development.md`
```

- [ ] **Step 4: Recreate `web/README.md`**

Write `web/README.md` with this content:

```markdown
# STOTOP Web

The frontend is a Vue 3 + Vite + TypeScript application.

## Commands

Install dependencies from the repository root:

```bash
./scripts/dev/setup.sh
```

Start the frontend dev server:

```bash
./scripts/dev/frontend.sh
```

Build the production frontend bundle:

```bash
./scripts/dev/build-frontend.sh
```

Run TypeScript type-check explicitly:

```bash
npm run type-check
```

## Local Proxy

Vite listens on:

```text
http://localhost:5173
```

The development server proxies these paths to the backend:

- `/api`
- `/hangfire`
- `/hubs`

The backend target defaults to:

```text
http://127.0.0.1:5001
```

Override the backend target with:

```bash
VITE_BACKEND_URL=http://127.0.0.1:5001 npm run dev
```

## Build Versus Type Check

`npm run build` runs the Vite production build. `npm run type-check` runs `vue-tsc -b`. The repository currently carries historical TypeScript debt, so treat these as separate signals during local validation.
```

- [ ] **Step 5: Update `docs/dev/mac-development.md`**

Add this section after the current "必备工具" section:

```markdown
## 统一开发入口

新机器或新工作区先运行：

```bash
./scripts/dev/setup.sh
./scripts/dev/doctor.sh
```

日常构建和检查优先使用：

```bash
./scripts/dev/build-all.sh
./scripts/dev/test-contracts.sh
./scripts/dev/test-dotnet.sh
./scripts/dev/preflight.sh
```

`preflight.sh` 会区分阻塞项、已知债务项和跳过项。当前 `src/STOTOP.sln` 已知缺少 `STOTOP.Module.Insurance` 与 `STOTOP.Module.Quality`，默认作为 solution coverage 债务提示；如果需要把 solution 覆盖问题当作阻塞项，可以运行：

```bash
STOTOP_PREFLIGHT_STRICT_SLN=1 ./scripts/dev/preflight.sh
```

前端类型检查需要显式开启：

```bash
STOTOP_PREFLIGHT_TYPECHECK=1 ./scripts/dev/preflight.sh
```
```

- [ ] **Step 6: Check documentation links**

Run:

```bash
rg -n "scripts/dev/(setup|doctor|build-all|test-contracts|test-dotnet|preflight)\\.sh|localhost:5001|localhost:5173|type-check" README.md README.en.md web/README.md docs/dev/mac-development.md
```

Expected: output shows the unified scripts, `5001`, `5173`, and type-check separation in the docs.

- [ ] **Step 7: Commit Task 5 files only**

Run:

```bash
git add README.md README.en.md web/README.md docs/dev/mac-development.md
git commit -m "docs: document development tooling"
```

Expected: commit includes only the four documentation files. Existing unrelated `design` or `docs` deletions remain unstaged.

### Task 6: Record Script Executable Bits and Run Verification

**Files:**
- Modify metadata: `scripts/dev/*.sh`

- [ ] **Step 1: Validate shell syntax for every dev script**

Run:

```bash
bash -n scripts/dev/_common.sh
bash -n scripts/dev/backend.sh
bash -n scripts/dev/build-all.sh
bash -n scripts/dev/build-backend.sh
bash -n scripts/dev/build-frontend.sh
bash -n scripts/dev/check-env.sh
bash -n scripts/dev/check-health.sh
bash -n scripts/dev/check-sln.sh
bash -n scripts/dev/doctor.sh
bash -n scripts/dev/export-baseline.sh
bash -n scripts/dev/frontend.sh
bash -n scripts/dev/init-database.sh
bash -n scripts/dev/preflight.sh
bash -n scripts/dev/setup.sh
bash -n scripts/dev/test-contracts.sh
bash -n scripts/dev/test-dotnet.sh
bash -n scripts/dev/validate-database.sh
```

Expected: every command exits `0`.

- [ ] **Step 2: Record executable bits in Git**

Run:

```bash
chmod +x scripts/dev/*.sh
git update-index --chmod=+x scripts/dev/*.sh
```

Expected: `git diff --cached --summary -- scripts/dev` shows mode changes for shell scripts that were previously tracked as `100644`.

- [ ] **Step 3: Run environment and doctor checks**

Run:

```bash
./scripts/dev/check-env.sh
./scripts/dev/doctor.sh
```

Expected: both commands exit `0`. `doctor.sh` may warn that solution coverage has known gaps for `STOTOP.Module.Insurance` and `STOTOP.Module.Quality`.

- [ ] **Step 4: Run build checks**

Run:

```bash
./scripts/dev/build-backend.sh
./scripts/dev/build-frontend.sh
```

Expected: both commands exit `0`. If either fails, preserve the failing command and first actionable error in the implementation report.

- [ ] **Step 5: Run focused test checks**

Run:

```bash
./scripts/dev/test-contracts.sh cardflow
./scripts/dev/test-dotnet.sh Finance
```

Expected: both commands exit `0` or report exact failing test output. Any failure must be classified as toolchain regression, existing test failure, or environment dependency before claiming completion.

- [ ] **Step 6: Run full preflight**

Run:

```bash
./scripts/dev/preflight.sh
```

Expected: exits `0` if backend build, frontend build, contract tests, and .NET module tests pass. Solution coverage may appear under known debt unless `STOTOP_PREFLIGHT_STRICT_SLN=1` is set.

- [ ] **Step 7: Check whitespace and staged files**

Run:

```bash
git diff --check
git status --short -- scripts/dev .editorconfig global.json .node-version .vscode README.md README.en.md web/README.md docs/dev/mac-development.md
```

Expected: `git diff --check` exits `0`. Status output includes only files touched by this implementation task family.

- [ ] **Step 8: Commit executable metadata and final verification adjustments**

Run:

```bash
git add scripts/dev
git commit -m "chore: mark development scripts executable"
```

Expected: commit contains script mode changes and any small script fixes required by verification. Do not stage unrelated docs/design deletions.

## Self-Review Checklist

- Spec coverage: Tasks 1-6 cover root config, editor config, unified scripts, setup, doctor, build/test/preflight, docs, executable bits, and verification.
- Known debt handling: `check-sln.sh` currently fails for `STOTOP.Module.Insurance` and `STOTOP.Module.Quality`; the plan records this as preflight debt by default with `STOTOP_PREFLIGHT_STRICT_SLN=1` for strict mode.
- Type-check handling: `npm run build` remains separate from `npm run type-check`; type-check can be enabled with `STOTOP_PREFLIGHT_TYPECHECK=1`.
- Dirty worktree handling: every commit step stages exact files only.
- No business behavior changes: all touched paths are tooling, editor settings, scripts, or documentation.
