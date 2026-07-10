# CardFlow 设计器二期 · M8-C 引擎增强四件 · 设计+实施 Plan

> 承接 `docs/superpowers/plans/2026-07-09-cardflow-M8-二期-kickoff.md`（拆批表 M8-C）。
> 起点 HEAD `1e62b08`（M8-B 收尾后 push 到 origin/master）。

## 〇、决策

- **Scope**: 四件全做（④会签比例 → ②跨节点去重 → ①超时升级链 → ③自定义动作），从轻到重排序。
- 铁律不变：不新增 cc FType；不做假配置；无 EF migrations 走 V 编号 seeder；每件独立 commit 经 hook 门禁，不 push 等点头。

## 一、核实结论（2026-07-10）

| 件 | 引擎现状 | 存储 | UI | 判定 |
|---|---|---|---|---|
| ④ 会签比例 | `ApprovalModeHandler` 仅 single/countersign(all)/orsign/sequential；无 ratio/百分比 | `FApprovalMode` string，无 threshold 列 | 4 选 1 单选，无比例输入 | needs-engine-addition |
| ② 跨节点去重 | 节点内 `.Distinct()` 已有；跨节点零逻辑 | 无 dedup 字段 | 无 UI | needs-engine-addition |
| ① 超时升级链 | CardFlowTimeoutJob 仅 flag+SignalR+一次提醒；无升级/自动通过/自动驳回 | `FTimeoutHours`(consumed)；无 action 列 | 仅 timeoutHours 输入 | needs-engine-addition |
| ③ 自定义动作 | actionPolicy 仅 8 种内置 toggle；无 customActions | 无字段 | 硬编码 8 项 | needs-engine-addition |

**附带发现**：orsign 退回语义 bug（`IsStageReturned` 实现全 rejected 才退回，但 `RejectAsync` 从未调用它）。非本批范畴但值得修——纳入 ④ 会签比例 implementation（同一个 `ApprovalModeHandler` 改动区域）。

---

# Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans.

**Goal:** 4 个引擎增强——会签比例(ratio)、跨节点审批人去重、超时三级升级链(自动通过/自动驳回/升级到上级)、自定义动作挂自动处理。

**Tech Stack:** .NET 10 / EF Core / SQL Server / xUnit；Vue 3 / TS / AntD。

## Global Constraints

- 不新增 cc FType。不做假配置（引擎消费的才出 UI）。
- Schema 走版本化 seeder V 编号（V73+）。
- 全局 NoTracking；事务须 `IExecutionStrategy.ExecuteAsync` 包裹（新改动若在已有事务内则无需另建）。
- 向后兼容：新字段 null/缺省=现有行为不变。
- 前端：`type-check`+`lint:style`(零裸hex)+`vitest` 绿。
- 每件独立 commit，经 hook 门禁，不 push。

---

## 件④ 会签比例（Commit 1，seeder V73）

### Task 1: ApprovalModeHandler ratio + seeder V73 + 测试

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Entities/CfStageDefinition.cs`（加 `FApprovalThreshold`）
- Modify: `src/STOTOP.Module.CardFlow/Configurations/CfStageDefinitionConfiguration.cs`
- Modify: `src/STOTOP.Module.CardFlow/Services/ApprovalModeHandler.cs`（加 `ratio` 分支）
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs`（IsStageCompleted 传 threshold；顺修 orsign 退回 bug）
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs`（NormalizeApprovalMode 加 `ratio`）
- Modify: `src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs`（V73 加列）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Approval/ApprovalRatioTests.cs`

**设计**：
- 新列 `CfStageDefinition.FApprovalThreshold : int?`（DB `F通过比例`，1-99 百分比，null=不适用）。
- `ApprovalModeHandler.IsStageCompleted` 新增 `ratio` 分支：`approvedCount / totalCount >= threshold/100.0`。
- `ApprovalModeHandler.IsStageReturned` 新增 `ratio` 分支：`rejectedCount / totalCount > (100-threshold)/100.0`（补数驳回）。
- **顺修 orsign bug**：`FlowEngineService.RejectAsync` 当 `approvalMode=="orsign"` 时不直接标 returned，改调 `ApprovalModeHandler.IsStageReturned`（需全部 rejected 才退回）。
- `CfStageInstance.FApprovalMode` 运行时复制（现有逻辑），threshold 运行时从 `CfStageDefinition` 重查（或存入实例的 JSON/新列——轻量取法：直接查定义的 threshold，避免加实例列）。
- seeder V73：`IF COL_LENGTH(N'CF节点定义', N'F通过比例') IS NULL ALTER TABLE [CF节点定义] ADD [F通过比例] INT NULL;`
- 向后兼容：`ratio` mode 需设 threshold，否则 fallback 100%（=countersign 语义）。

### Task 2: 前端 StageConfigPanel ratio UI

**Files:**
- Modify: `web/src/components/cardflow/StageConfigPanel.vue`（APPROVAL_MODES 加 ratio + 条件 threshold 输入）
- Modify: `web/src/types/cardflow.ts`（ApprovalModeConfig 加 threshold?）
- Modify: `web/src/components/cardflow/stageDefinitionShared.ts`（若需）

---

## 件② 跨节点审批人去重（Commit 2，seeder V74）

### Task 3: 跨节点去重引擎 + seeder V74 + 测试

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Entities/CfStageDefinition.cs`（加 `FSkipDuplicateApprover`）
- Modify: `src/STOTOP.Module.CardFlow/Configurations/CfStageDefinitionConfiguration.cs`
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs`（`AssignStageHandlersAsync` 去重逻辑）
- Modify: `src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs`（V74）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Approval/CrossStageDeduplicateTests.cs`

**设计**：
- 新列 `CfStageDefinition.FSkipDuplicateApprover : bool`（DB `F跳过重复审批人`，default false=不去重=向后兼容）。
- 引擎：`AssignStageHandlersAsync` 分配处理人后（在写入 `CfStageAssignee` 前），若该节点 `FSkipDuplicateApprover=true`，查本卡已有 `CfStageAssignee`（同 FCardId、status=approved/rejected/completed）的 userId 集合，从本次 assignees 中剔除。若剔除后为空→视为"自动通过"（与发起人自审同理）直接推进。
- seeder V74：`IF COL_LENGTH(N'CF节点定义', N'F跳过重复审批人') IS NULL ALTER TABLE [CF节点定义] ADD [F跳过重复审批人] BIT NOT NULL DEFAULT 0;`

### Task 4: 前端去重 UI

**Files:**
- Modify: `web/src/components/cardflow/StageConfigPanel.vue`（处理人 Tab 加去重开关）

---

## 件① 超时升级链（Commit 3，seeder V75）

### Task 5: 超时动作配置 + seeder V75 + Job 改造 + 测试

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Entities/CfStageDefinition.cs`（加 `FTimeoutActionJson`）
- Modify: `src/STOTOP.Module.CardFlow/Configurations/CfStageDefinitionConfiguration.cs`
- Modify: `src/STOTOP.Module.CardFlow/Jobs/CardFlowTimeoutJob.cs`（从 flag→调引擎执行配置的动作）
- Create: `src/STOTOP.Module.CardFlow/Models/TimeoutActionConfig.cs`（配置模型+解析）
- Modify: `src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs`（V75）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Jobs/TimeoutEscalationTests.cs`

**设计**：
- 新列 `CfStageDefinition.FTimeoutActionJson : string?`（DB `F超时动作JSON`，NVARCHAR(MAX) NULL）。
- JSON schema：
  ```json
  { "levels": [
    { "multiplier": 1, "action": "remind" },
    { "multiplier": 2, "action": "autoApprove" | "autoReject" | "escalate" | "remind" },
    { "multiplier": 3, "action": "autoApprove" | "autoReject" | "escalate" | "remind" }
  ]}
  ```
  `multiplier` = 超时时长倍数；`action` = remind(现有)/autoApprove/autoReject/escalate(升级到上级)。
- `CardFlowTimeoutJob` 改造：从现有的"标 flag + push SignalR"改为：按 `overHours/timeoutHours` 确定当前 level，从 `FTimeoutActionJson` 找对应 action（级别累进——3x 超时执行 3x 级的 action，不重复执行已过级）。每级执行一次（用新的 `FLastTimeoutLevel` 或在 `CfStageInstance` 加字段，或用 ActionLog 幂等判断）。
- action 执行：`autoApprove` → `FlowEngineService.ApproveAsync(cardId, systemOperatorId, new ApproveRequest{...})`（系统身份）；`autoReject` → `RejectAsync`；`escalate` → 取当前处理人的上级（走 orgChain / SYS组织闭包的上级节点），追加为新 assignee。
- 向后兼容：`FTimeoutActionJson` null → 现有行为（仅 remind/flag）。
- seeder V75：add column。

### Task 6: 前端超时升级 UI

**Files:**
- Modify: `web/src/components/cardflow/StageConfigPanel.vue`（高级 Tab 超时 → 三级动作配置）
- Create: `web/src/components/cardflow/timeoutActionShared.ts`（+ `.spec.ts`）

---

## 件③ 自定义动作（Commit 4，无 seeder——扩展现有 JSON）

### Task 7: StageActionPolicy 自定义动作模型 + 引擎分派 + 测试

**Files:**
- Modify: `src/STOTOP.Module.CardFlow/Models/Schema/StageViewProfileModels.cs`（`StageActionPolicy` 加 `CustomActions`）
- Modify: `src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs`（新 `ExecuteCustomActionAsync`）
- Modify: `src/STOTOP.Module.CardFlow/Services/StageActionPolicyService.cs`（校验扩展）
- Test: `tests/STOTOP.Module.CardFlow.Tests/Approval/CustomActionTests.cs`

**设计**：
- `StageActionPolicy` 扩展：
  ```csharp
  public List<CustomActionDefinition> CustomActions { get; set; } = new();
  ```
  `CustomActionDefinition { string Code; string Label; string Handler; string? HandlerConfigJson; bool RequireOpinion; }`
- 引擎：`ExecuteCustomActionAsync(cardId, operatorId, actionCode, opinion)` → 查 actionPolicy 找匹配的 `CustomActionDefinition`，执行其 `Handler`（初期支持：`autoApprove`=自动通过当前节点、`autoReject`=自动驳回、`notify`=触发通知、`webhook`=调外部URL）。
- 前端运行时：审批面板动态渲染自定义按钮（`useStageWorkView` 的 `allowedActions` 扩展含 custom codes）。
- 存储：复用现有 `FInputFieldsJson`(version=2 信封) 的 `actionPolicy` 段——无新列。

### Task 8: 前端动作 Tab 自定义动作 + 审批面板动态按钮

**Files:**
- Modify: `web/src/components/cardflow/StageConfigPanel.vue`（动作 Tab 加自定义动作编辑器）
- Modify: `web/src/components/cardflow/runtime/CardComponentRenderer.vue`（或审批面板 `CardFlowPanel.vue`）（动态渲染自定义按钮）

---

## 收口（Commit 5）

### Task 9: 整体终审 + 回归

- 子代理对抗性终审
- 全量回归 `test-dotnet cardflow` + 前端 `type-check`+`vitest`+`lint:style`
- 更新记忆
