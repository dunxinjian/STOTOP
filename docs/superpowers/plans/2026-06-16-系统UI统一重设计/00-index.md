# 系统 UX/UI 统一重设计 实现计划（总索引）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. 各阶段文件内的步骤用 checkbox（`- [ ]`）跟踪。

**Goal:** 建立唯一配色/主题令牌真源，把申通橙降为克制强调色，全系统去蓝、拆紫、重做登录，并把统一视觉语言落到全站所有功能页（设计系统层 + 逐模块巡检），达成严谨、干净、雅致、WCAG-AA 的企业内部办公风格。

**Architecture:** `stores/theme.ts` 运行时把完整令牌注入 `:root`（权威），`variables.scss` 编译期同值桥接 `var()`；组件全部改用 `var(--token)`；stylelint 禁裸 hex 防回潮。视觉统一靠三通道自动继承（全局类/CSS 变量 + ant-override + ConfigProvider token）+ 共享组件（PageHeader/EmptyState/BaseCard/StatusTag），再逐模块巡检收敛偏离页。

**Tech Stack:** Vue 3 + ant-design-vue 4 + Vite 8 + SCSS + Pinia；移动端 vant 4。**无前端测试运行器**——验证用 ripgrep 静态断言 + `npm run build`(vite) + preview 工具截图。

**Spec:** [docs/superpowers/specs/2026-06-16-系统UX-UI统一重设计-design.md](../../specs/2026-06-16-系统UX-UI统一重设计-design.md)

---

## 阶段总览与依赖

| # | 阶段 | 计划文件 | 规模 | 依赖 |
|---|---|---|---|---|
| 0 | 配色/主题令牌单一真源 + CSS 变量桥 | [phase0-令牌地基.md](phase0-令牌地基.md) | 11 文件 / 8 任务 | 无（前置） |
| 1 | 全局去蓝 / 拆紫 / 同义多值收敛（令牌迁移扫荡） | [phase1-去蓝拆紫收敛.md](phase1-去蓝拆紫收敛.md) | 11 文件 / 9 任务 | 0 |
| 2 | 登录页重做（精致深墨 + 去全部动画装饰） | [phase2-登录深墨重做.md](phase2-登录深墨重做.md) | 4 文件 / 9 任务 | 0,1 |
| 3 | 主页工作台重设计 | [phase3-主页工作台.md](phase3-主页工作台.md) | 7 文件 / 11 任务 | 0,1 |
| 4 | 全局搜索/命令面板重设计 | [phase4-全局搜索.md](phase4-全局搜索.md) | 2 文件 / 9 任务 | 0,1 |
| 5 | 主框架外壳——前后台统一、后台紫退役 | [phase5-外壳前后台统一.md](phase5-外壳前后台统一.md) | 6 文件 / 6 任务 | 0,1 |
| 6 | 移动端（oa-mobile / vant）令牌对齐 | [phase6-移动端对齐.md](phase6-移动端对齐.md) | 14 文件 / 8 任务 | 0,1 |
| 7 | 设计系统层——共享组件 + 全局覆盖，全站自动继承 | [phase7-设计系统层.md](phase7-设计系统层.md) | 12 文件 / 10 任务 | 0（含防御兜底） |
| 8 | 逐模块功能页风格巡检对齐（覆盖全站所有功能页） | [phase8-逐模块巡检.md](phase8-逐模块巡检.md) | 11 文件 / 15 任务 | 7 |
| 9 | 验收与回归 | [phase9-验收.md](phase9-验收.md) | 全站一致性 / 构建 / 可视 | 0–8 |

## 推荐执行顺序

```
阶段0（令牌地基）──┬─→ 阶段1（去蓝拆紫）──┬─→ 阶段2 登录
                  │                      ├─→ 阶段3 主页
                  │                      ├─→ 阶段4 搜索
                  │                      ├─→ 阶段5 外壳
                  │                      └─→ 阶段6 移动端
                  └─→ 阶段7（设计系统层）──→ 阶段8（逐模块巡检）
                                                    └─→ 阶段9 验收
```

- 阶段0 是一切前置。阶段1 在 0 之后；阶段 2–6 都依赖 0&1，彼此独立可并行。
- 阶段7 设计系统层含**防御性 `tokens.scss` 兜底**：若阶段0 已落地则退化为"校验+补缺"；阶段8 逐模块巡检依赖 7。
- 阶段9 在全部完成后做全站一致性 + 构建 + 可视化回归验收。

## 验证约定（全计划通用）

- **静态断言**：ripgrep（PowerShell 语法，`;` 串联、`$null`）。例：`cd web; rg -n "#1677ff" src` 期望 0 命中。
- **构建**：`cd web; npm run build`（vite）须通过。
- **类型**：`npm run type-check`（vue-tsc）本就基线红，**不作门禁**，仅确认无新增报错。
- **可视化**：preview 工具（`preview_start` / `preview_screenshot` / `preview_resize`）逐屏验证；运行时令牌验证 = 改 `--color-primary` 看全局变色。
- **提交**：每个 Task 末尾 `git add` + `git commit`（中文 message，结尾带 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`）。
