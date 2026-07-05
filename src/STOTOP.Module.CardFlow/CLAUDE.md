# CardFlow 模块工作指南

> 在本模块（`src/STOTOP.Module.CardFlow/`）工作时加载。完整设计见 [design/07-cardflow.md](../../design/07-cardflow.md)；通用规则以根 `CLAUDE.md` 为准。本模块是全系统最大（393 文件）、最核心的运行时——改动前先建立心智模型，别孤立地读单个文件。

## 这个模块是什么

「审批 / 动态表单 / 节点流转 / 卡片待办」的统一运行时载体，外加从历史 DataCenter 迁入的批量导入管道。六大子系统：流程引擎 / 卡片表单 / 条件路由+编排 / 导入管道 / 自动插件 / 质量凭证。

**新审批、动态表单、节点流转、卡片待办一律进这里**（硬约束）。不要在业务模块里复制流转运行时，也不要新建 DataCenter。

## 核心心智模型

**设计态三层**：`CfFlowDefinition`（流程定义）→ `CfFlowVersion`（版本，**表单 schema 存这里**）→ `CfStageDefinition`（节点）+ `CfStageRouteRule`（条件出边）+ `CfDynamicStagePolicy`（动态审批策略）。

**执行态**：`CfCard`（卡片＝流程实例，提交时锁版本）→ `CfStageInstance`（节点实例，带 `FRound` 轮次）→ `CfStageAssignee`（指派处理人）；每个动作写 `CfActionLog`，每次路由选边写 `CfRouteDecisionSnapshot`。

引擎心脏是 `Services/FlowEngineService.cs`（2869 行，提交/审批/驳回/退回/撤回/作废/加签/转办/抄送）。卡片状态机：`draft → active ⇄ returned → completed`，旁支 `withdraw/void/exception`。

## 硬边界

- **CardFlow → Workflow**（单向）：把质量问题/导入异常**派发为** `WfWorkItem` 并消费其状态。**禁止反向**让 Workflow 依赖 CardFlow。
- **CardFlow → Finance**：凭证经 `IVoucherService` 落 `FIN凭证`，不要在本模块写会计账务。
- **Express → CardFlow**：Express 计费插件继承本模块 `BatchPluginBase`。因此 `Program.cs` 里 **`AddCardFlowModule` 必须早于 `AddExpressModule`**——改注册顺序前务必确认。
- **OA**：仅 `CardFlowSourceContextVerifier` 读历史实体，不要新增 OA 入口。

## 改之前必须知道的坑

- **后台/批次链无 HttpContext**：入口先 `orgAccessor.CurrentOrgId = batch.FOrgId`，否则组织过滤器整体放行 → 跨组织串数据。
- **DbContext 默认 NoTracking**：更新前显式 `.AsTracking()`，否则改动不落库。
- **`AutoPluginFactory.Create` 必须传 scoped provider**（插件是 Scoped，别用根容器）。
- **明细全量替换**：`CardService.UpdateAsync` 删旧插新，客户端须提交完整明细集。
- **条件求值已收敛（阶段3g）**：流程路由/节点进入条件/流程组触发/编排边条件统一走 `ConditionRuleEvaluator`（JSON 规则树）；仅 `AutoVoucherMatchingEngineV2.EvaluateCondition`（生产凭证链，D2 后置）与 `ClassificationEngine.BuildWhereClause`（JSON→SQL 编译范式）保留独立实现，新增算子先评估复用主力。
- **schema 解析静默降级**：非法 JSON 不报错只「字段消失」。
- **半成品/死代码**（别误以为生效）：委托 `Delegation` 未在引擎消费；`Events/`+`EventHandlers/` 未注册 DI 故永不触发；编排 `TriggerCardFlowNodeAsync` 是占位实现；下载后自动导入是 TODO。详见 design/07 §7。

## 扩展点

- **加自动插件**：继承 `InputPluginBase`/`ProcessingPluginBase`/`BatchPluginBase` → 重写 `PluginName`+`ExecuteAsync` → 在 `CardFlowModuleExtensions` 里 `AddScoped<T>()` + `AutoPluginFactory.Register<T>("Code")`。
- **加审批人策略**：集中在 `Services/ApproverResolver.cs`。
- **加通知渠道**：实现 `INotificationChannel`（参考 `DingTalkChannel`）。
- **加分类处理器**：实现 `IClassificationHandler` → 在 `CardFlowModuleExtensions` 注册具体类（Plugin 薄壳注入用）+ `AddTransient<IClassificationHandler, XxxHandler>()`（接口注册决定派发规则 handler-types 接口的可见列表）。

## 约定

- 实体/表前缀 `Cf*` / `CF中文`；列名 DB 用 `F+中文`、C# 属性用 `F+英文`（例外：`CfPluginRule` 属性直接用中文）。
- EF 配置在 `Configurations/`（不在扩展方法手动 `ApplyConfiguration`，由 `RegisterModuleAssembly` 程序集扫描）。
- 测试在 `tests/STOTOP.Module.CardFlow.Tests`（220 用例，InMemory + `TestDbContextFactory`）——改引擎/路由/审批务必补回归用例。
- 权限码 `CardFlowPermissions`（`cardflow:*`）已全量定义，但卡片/待办/定义/看板类控制器目前只挂 `[Authorize]`，仅导入/暂存/派发/凭证类启用了 `[RequirePermission]`。
