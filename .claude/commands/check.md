---
description: 提交前门禁复核——后端模块编译+测试、前端 type-check + lint:style，并比对 husky pre-commit 的裸 hex 门禁。
argument-hint: [模块名，用于收敛后端编译/测试范围]
---

按顺序复核，**任一步失败就停下修复再继续**，最后汇总 通过/失败。只复核与本次改动相关的栈，不相关的跳过并说明。

## 后端（本次改了 `.cs` 才做）
1. `/build $ARGUMENTS`（按模块闭包编译）
2. `/test $ARGUMENTS`（按模块收敛测试）

## 前端（本次改了 `web/` 才做）
3. `npm --prefix web run type-check`（vue-tsc `-b`，strict，禁裸 any）
4. `npm --prefix web run lint:style`（stylelint）
5. 检查本次 diff 里 `web/src/**.{vue,scss}` **新增行**有无裸 hex 颜色——`.husky/pre-commit` 会拦截。改用设计令牌 `var(--token)` 或 SCSS `$变量`（真源 @web/docs/TOKENS.md）；确属 ECharts/SVG/打印导出场景再在该行加 `/* hex-ok: 原因 */`，或确认落在豁免文件（`styles/{variables,ant-override,layout,button-styles}.scss`）。

## 汇总
逐项标 ✅/❌，列出仍需修复的 `文件:行`。全绿才建议提交。
