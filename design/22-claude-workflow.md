# 用 Claude Code 高效开发 STOTOP

> 本文是团队用 Claude Code / AI 助手开发本仓库的**操作流程**。规约本身见 `CLAUDE.md` 与 `design/21-dev-rules.md`；本文只讲"怎么把规约跑成高效闭环"。配套产物在 `.claude/`（命令、子代理）与 `scripts/dev/`（脚本）。

## 1. 上下文分层（让 AI 默认就懂规矩）

| 层 | 文件 | 作用 |
|---|---|---|
| 全局规约 | 根 `CLAUDE.md` | 每会话自动加载的硬约束（命名、分层、边界） |
| 模块细则 | `src/STOTOP.Module.<X>/CLAUDE.md` | 模块特有约定（现有 CardFlow，按需补） |
| 设计背景 | `design/NN-*.md` | 模块运行边界与数据流（索引 `00-overview.md`） |
| 持久记忆 | Claude 的 memory | 跨会话的偏好与在办项（不进仓库） |

新模块/新约定落地后，**同步更新对应层**——尤其新增模块要改 `Program.cs` 注册顺序与 `00-overview.md`。

## 2. 日常开发闭环

```
非平凡任务 → 先进 Plan 模式定方案（只读探查 + 列步骤，确认后再改）
   │
   ▼
/module <name>     载入该模块 .slnf + design 文档 + 模块 CLAUDE.md 上下文
   │
   ▼
改代码 → /build <name>   只编译该模块依赖闭包（非 60 项目全图）
   │
   ▼
/test <name>       按 filter 收敛测试（不跑全图）
   │
   ▼
/rule-review       rule-reviewer 子代理按 21-dev-rules 审 diff
   │
   ▼
/check <name>      提交前门禁：编译+测试 / type-check / lint:style / 裸 hex
   │
   ▼
提交（由人确认；commit 信息中文，遵循现有 commit 风格）
```

并行/有风险的改动 → 用 **worktree 隔离**（见 §5）。

## 3. 斜杠命令（`.claude/commands/`）

| 命令 | 作用 |
|---|---|
| `/module <name>` | 聚焦单模块：载入 .slnf + design + 模块 CLAUDE.md，约束后续编译/测试范围 |
| `/build <name>` | 用 `src/<name>.slnf` 只编译模块依赖闭包（`build-filter` 脚本） |
| `/test [filter]` | 跑 `tests/` 下测试，按 filter 收敛（`test-dotnet` 脚本，自动发现项目） |
| `/check [name]` | 提交前门禁复核：后端编译+测试、前端 type-check + lint:style、裸 hex 自查 |
| `/precommit [name]` | 一键完整提交前门禁：`/check` + rule-reviewer 审规约 + 自查 diff 范围/秘密/残留，给放行结论与中文 commit 草稿（不自动提交） |
| `/scaffold <名> [中文名]` | 按分层约定脚手架新后端模块（含边界判断与注册顺序） |
| `/rule-review` | 启动 rule-reviewer 子代理审当前 diff 的规约符合性 |

可用 `.slnf` 过滤器：`cardflow` / `core` / `crm` / `dormitory` / `express` / `finance` / `task`。

## 4. 子代理（`.claude/agents/`）

- **rule-reviewer**：按 `CLAUDE.md` + `21-dev-rules.md` 审 diff，输出 🔴硬约束违规 / 🟡建议 / ✅通过。只读，不改文件。与通用代码审查互补（找 bug 用 `/code-review`）。
- **module-explorer**：单模块只读探索，定位前后端落点与调用链，返回结论而非整文件转储。摸清陌生模块时先派它。

## 5. 大库提速要点

- **永远先收敛范围**：单模块改动用 `/module` → `/build` → `/test`，不要全图 `dotnet build` 或跑全部测试。`cardflow.slnf` 只含生产闭包（不含 `CardFlow.Tests`，后者引用 WebAPI 会拉全图）。
- **启停服务**用 `scripts/dev/`（`backend.ps1` / `frontend.ps1` / `check-health.ps1`）或对助手说"重启前后端"（触发 `restart-dev` skill），不要手敲 dotnet/vite。
- **worktree 隔离**：并行任务或风险改动让助手在独立 git worktree 里做，避免污染主工作区。auto-worktree 落在 `.claude/worktrees/`，**该目录已 gitignore**；偶有残骸目录（无 `.git`、不在 `git worktree list`）可直接 `rm -rf` 清理，`git worktree prune` 清元数据。

## 6. 权限（减少确认弹窗）

`scripts/dev/*`、`dotnet build/test`、`npm run type-check/lint:style`、`git status/diff/log` 等**只读或安全**命令已加入 `.claude/settings.local.json` 的 allow 列表（仅本机，不提交）。新出现的高频安全命令可继续往里加，或用 `/fewer-permission-prompts` 从真实记录自动生成精确白名单。**写操作/迁移/删除不入白名单**，保留人工确认。

### 可选：存盘即查 hex（PostToolUse hook）

`scripts/dev/hook-stylelint-fix.{ps1,sh}` 是一个 hook helper：助手每次 Edit/Write `web/src/**.{vue,scss}` 后自动跑 `stylelint --fix`，把剩余问题（本项目 stylelint 仅启用 `color-no-hex`，不可自动修——所以主要是即时查出裸 hex）回报给助手，便于当场改成 `var(--token)`，而不是拖到提交时被 `.husky/pre-commit` 拦。best-effort、非阻塞、文件不匹配或未装 `web/node_modules` 时静默跳过。

想启用就在 `.claude/settings.local.json`（仅本机）加 PostToolUse hook（matcher `Write|Edit`），命令用 `pwsh -NoProfile -ExecutionPolicy Bypass -File <helper 绝对路径>`（Windows）。注意 helper 顶部已强制 UTF-8 输出，否则回报的中文会按 OEM 码页乱码。

### 可选：提交前编译门禁（PreToolUse hook）

`scripts/dev/hook-precommit-gate.{ps1,sh}` 是一个 PreToolUse(`Bash`/`PowerShell`) hook helper：拦截 `git commit`，在提交前**编译本次将提交的 `.cs` 所属 csproj（含依赖）**，编译不过就 `permissionDecision: deny` 阻止提交（补 `.husky/pre-commit` 只查 hex 的盲区）。差异化：只编你这次改的工程，不被别处历史问题连累；**不跑测试**（太慢，留给 `/test`）。

- **前端 type-check 不纳入此门禁**：vue-tsc 是全工程检查、~30s/次，且当前仓库有历史类型错（见后台任务），全工程硬拦会拦住所有 web 提交。前端类型检查留给 `/precommit`/`/check` 按需跑。
- 接法：在 `.claude/settings.local.json` 加两条 PreToolUse 条目，matcher 分别 `Bash` / `PowerShell`，各带 `if: "<Tool>(git commit:*)"` 只在提交时触发（避免拖慢每条命令），命令用 `pwsh -ExecutionPolicy Bypass -File <gate helper 绝对路径>`，timeout 给足（180s）。
- best-effort：hook 自身异常一律放行，绝不因 hook bug 卡住提交。注意 `if` 前缀匹配，复合命令 `git add && git commit` 不触发——单独跑 `git commit` 即可。

## 7. 门禁与提交

- 提交由 `.husky/pre-commit` 兜底：拦 `web/src/**.{vue,scss}` 新增行的裸 hex。提交前用 `/check` 提前过一遍同款门禁 + type-check + 后端测试，避免被 hook 打回。
- 设计令牌真源 `web/docs/TOKENS.md`；裸 hex 仅 ECharts/SVG/打印导出场景可加 `/* hex-ok: 原因 */`。
- 提交信息用中文、贴合现有 commit 风格；commit/push 由人决定，助手不擅自提交。
