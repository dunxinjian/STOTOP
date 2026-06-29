---
description: 聚焦单个模块开发——载入对应 .slnf 工作区、design 文档与模块 CLAUDE.md 上下文，后续编译/测试都收敛到该模块依赖闭包。
argument-hint: <cardflow|express|finance|crm|task|core|dormitory>
---

你现在要聚焦开发 **$ARGUMENTS** 模块。建立上下文，**不要加载整个 60 项目图**。

## 模块 → 资源映射

| 模块名 | .slnf 工作区 | 设计文档 | 后端项目 | 测试项目 |
|---|---|---|---|---|
| cardflow | `src/cardflow.slnf` | `design/07-cardflow.md` | `src/STOTOP.Module.CardFlow` | `tests/STOTOP.Module.CardFlow.Tests` |
| express | `src/express.slnf` | `design/04-express.md` | `src/STOTOP.Module.Express` | `tests/STOTOP.Module.Express.Tests` |
| finance | `src/finance.slnf` | `design/05-finance.md` | `src/STOTOP.Module.Finance` | `tests/STOTOP.Module.Finance.Tests` |
| crm | `src/crm.slnf` | `design/06-crm.md` | `src/STOTOP.Module.CRM` | （暂无，待补） |
| task | `src/task.slnf` | `design/09-task.md` | `src/STOTOP.Module.Task` | `tests/STOTOP.Module.Task.Tests` |
| dormitory | `src/dormitory.slnf` | `design/17-dormitory.md` | `src/STOTOP.Module.Dormitory` | `tests/STOTOP.Module.Dormitory.Tests` |
| core | `src/core.slnf` | `design/01-core.md` + `design/02-infrastructure.md` | `STOTOP.Core` / `STOTOP.Infrastructure` | （随各模块测试覆盖） |

## 步骤

1. 若 `$ARGUMENTS` 不在上表，列出可用过滤器（`src/*.slnf`）让我确认，不要继续。
2. 读取并记住该模块的**设计文档**与模块级 `CLAUDE.md`（若存在，如 `src/STOTOP.Module.CardFlow/CLAUDE.md`）。
3. 复述与本模块相关的硬约束摘要（来自 @CLAUDE.md）：分层目录、`F中文`列名 vs `F+PascalCase`属性映射、ApiResult 包装、路由小写、IOrgScoped 组织隔离、以及 CardFlow/Workflow/OA 边界。
4. 之后本会话编译用 `/build $ARGUMENTS`、测试用 `/test $ARGUMENTS`，保持在该模块依赖闭包内。

完成后**一句话汇报**：对应 .slnf、design 文档、是否有模块 CLAUDE.md。然后等我给具体任务。
