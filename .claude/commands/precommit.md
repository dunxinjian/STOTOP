---
description: 一键提交前门禁——按改动栈跑编译+测试+type-check+lint+裸hex，再用 rule-reviewer 审规约，再自查 diff 范围/秘密/残留，给出放行/拦截结论。不自动提交。
argument-hint: [模块名，用于收敛后端编译/测试范围]
---

对当前**未提交改动**做完整提交前门禁。先 `git status --porcelain` + `git diff --name-only`（含暂存与未暂存）判断改了哪些栈，只跑相关步骤。**任一步失败不要继续后续步骤之外的"放行"判断**，最后给统一结论。**全程不执行 `git commit`** ——止步于结论 + 修复清单，等我确认。

## 第 1 关：编译 + 测试 + 前端校验（≈ `/check`）
- 改了 `.cs` → `/build $ARGUMENTS`（闭包编译，0 错）→ `/test $ARGUMENTS`（定向测试，全绿）。
- 改了 `web/` → `npm --prefix web run type-check`（vue-tsc strict）→ `npm --prefix web run lint:style`（stylelint）。
- **裸 hex 自查**：扫本次 diff 里 `web/src/**.{vue,scss}` 新增行的裸 hex（`.husky/pre-commit` 只在**暂存**文件上强制此项——提前过一遍免得被打回）。改用 `var(--token)` / SCSS `$变量`（真源 @web/docs/TOKENS.md）；确属 ECharts/SVG/打印导出再加 `/* hex-ok: 原因 */`，或确认落在豁免文件 `styles/{variables,ant-override,layout,button-styles}.scss`。

## 第 2 关：规约符合性
启动 **rule-reviewer** 子代理（Agent 工具，subagent_type: `rule-reviewer`）审当前 diff，按 🔴硬约束违规 / 🟡建议 / ✅通过 汇报。重点：`F中文`列名 vs `F+PascalCase`属性映射、表名前缀+中文、`ApiResult` 包装、路由小写、`IOrgScoped` 组织隔离、CardFlow/Workflow/OA 边界、前端禁裸 any/令牌。
（找正确性 bug 不在本命令范围，需要时另跑 `/code-review`。）

## 第 3 关：人工自查 diff（机器查不了的）
通读 `git diff`（暂存+未暂存），逐条核：
- **范围收敛**：没夹带无关改动；无调试残留（`Console.WriteLine` / `console.log` / 注释掉的代码 / `TODO` 临时桩）；无误删。
- **无秘密入库**：连接串/密钥/`db-connections.json` 不进库（已 gitignore，勿手动 `git add`）；`appsettings.json` 被跟踪，勿塞敏感值。
- **自动产物不手改**：`web/src/components.d.ts` 自动生成，勿提交手改版。
- **联动同步**：新增模块时 `Program.cs` 注册顺序（CardFlow 早于 Express）与 `design/00-overview.md` 索引是否一起更新；改了运行边界时对应 `design/NN-*.md` 是否同步。
- **缩进/编码**：`.cs`/csproj=4 空格、其余=2 空格、`.sln`=Tab；utf-8 / lf / 文末换行。

## 结论
输出：
1. 三关逐项 ✅/❌ 摘要。
2. 仍需修复的清单（`文件:行` + 一句修复建议），🔴 优先。
3. 放行判断：**全绿 → 建议提交，并给一条贴合现有风格的中文 commit message 草稿**（等我点头才执行提交，不用 `--no-verify`）；**有 🔴/失败 → 列出阻塞项，先修再说**。
