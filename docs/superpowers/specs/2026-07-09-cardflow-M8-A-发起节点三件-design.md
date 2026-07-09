# CardFlow 设计器二期 · M8-A 发起节点三件 · 设计

> 承接 `docs/superpowers/plans/2026-07-09-cardflow-M8-二期-kickoff.md`（拆批表 M8-A）。
> 本文是 M8-A 的**设计真源**；实施细化 plan 另出（writing-plans）。
> 起点 HEAD `61ffc28`（master）。核实产出见本文「一、核实结论」。

## 〇、决策（已与用户确认）

1. **范围**：三件（发起范围校验 / 代提交 onBehalf / 重提强制重路由 E1）**全落在 M8-A 一批**。
2. **发起范围维度**：扩成**结构化 scope**（角色 / 组织 / 岗位 / 人员，全维度），不止角色一维。
3. **存储**：**一列装两件策略**——发起范围 + 可代提交范围 共用 `CfFlowDefinition` 新列 `F发起策略JSON`。

三条铁律（一期已验证，继续守）：**不新增 `cc` FType**（引擎节点分派是 `FType=="auto"?auto:human` 二元）；**不做假配置**（引擎不消费的不出真开关）；后端全在 `STOTOP.Module.CardFlow`，版本化 seeder 无 EF migrations，提交经 hook 门禁，**不 push 等点头**。

## 一、核实结论（子代理只读核实，2026-07-09）

三件今天**引擎全不消费**（均 `needs-engine-addition`），且发现一处**活的假配置**必须一并修：

| 件 | 引擎现状 | 已有管线 | 判定 |
|---|---|---|---|
| E1 重提重路由 | `ResubmitAsync`(FlowEngineService.cs:1051) **硬编码 `stages[0]`**，从不读策略 | UI 单选 `fromStart/fromRejected` 已在、`state.settings.resubmitStrategy` 已上送并存 `CfFlowVersion.FFlowSettingsJson` | 零 schema，只差引擎读 |
| 发起范围校验 | `GetAvailableFlowsAsync`(CardService.cs:41) 的 `userId` 参数**未使用**；`CreateAsync`(756) 不校验发起人 | 仅 `FAllowedRolesJson`（角色一维）stored-not-consumed | 需引擎 + 新列（结构化全维度） |
| 代提交 onBehalf | `CreateAsync` 硬写 `FInitiatorId=操作人`；`SubmitAsync`(429) 门禁 `FInitiatorId==operator` 连"A建B提"都拒 | 无 DTO 字段、无代理人列、无范围配置 | 需引擎 + 新列 + 越权护栏（最重、安全敏感） |

**活的假配置**：`FlowDefinitionEditPage.vue:2604` 的「重提策略」单选注释写"（引擎真消费）"，但引擎从不读 `resubmitStrategy`——用户能拨、引擎不认。E1 落地后此注释成真。

**下游好消息**：`initiator` 处理人策略(FlowEngineService.cs:2727)、`ApproverResolver`(:39)、"我发起的"待办(TodoService.cs:114) 均按 `FInitiatorId` 计算——onBehalf 只要把 `FInitiatorId` 落成被代理人，处理人/待办自动跟随，无需逐点改。

## 二、存储模型

### 2.1 新列 `F发起策略JSON`（发起范围 + 代提交范围，seeder V71）

- 实体：`CfFlowDefinition.FStartPolicyJson : string?`（表 `CF卡片流程`，列 `F发起策略JSON`，`NVARCHAR(MAX) NULL`）。
- EF 映射（`CfFlowDefinitionConfiguration`）：
  ```csharp
  builder.Property(e => e.FStartPolicyJson).HasColumnName("F发起策略JSON").HasColumnType("nvarchar(max)");
  ```
- seeder V71（add-column 幂等，照 V68 范式）：
  ```csharp
  ExecSql(ctx, @"IF COL_LENGTH(N'CF卡片流程', N'F发起策略JSON') IS NULL
      ALTER TABLE [CF卡片流程] ADD [F发起策略JSON] NVARCHAR(MAX) NULL;");
  ```
- JSON 形状（两件同居一列）：
  ```json
  {
    "initiatorScope": { "roles": [], "orgs": [], "positions": [], "users": [] },
    "onBehalf": { "enabled": false, "agentScope": { "roles": [], "orgs": [], "positions": [], "users": [] } }
  }
  ```
- **语义**：
  - `initiatorScope` 各维度取并集（union）——命中**任一**维度即放行；**四维全空 / 列为 null = 不限制**（向后兼容硬约束，否则既有流程一上线发起全卡死）。
  - `onBehalf.enabled=false`（默认）→ 不接受代提交入参；`agentScope` 空 = 不允许任何人代提交（安全默认关闭）。
- **向后兼容（无数据回填）**：读策略时若 `FStartPolicyJson` 为 null/空，`initiatorScope.roles` 从既有 `FAllowedRolesJson` 派生（`ParseObject` 静默降级安全，非法 JSON → 视为不限制）；`onBehalf` 默认关闭。

### 2.2 新列 `F代理人ID` / `F代理人姓名`（onBehalf 运行时，seeder V72）

- 实体：`CfCard.FAgentId : long?` / `FAgentName : string?`（表 `CF流程实例`）。
- EF 映射（`CfCardConfiguration`）：
  ```csharp
  builder.Property(e => e.FAgentId).HasColumnName("F代理人ID");
  builder.Property(e => e.FAgentName).HasColumnName("F代理人姓名").HasMaxLength(100);
  ```
- seeder V72：
  ```csharp
  ExecSql(ctx, @"IF COL_LENGTH(N'CF流程实例', N'F代理人ID') IS NULL
      ALTER TABLE [CF流程实例] ADD [F代理人ID] BIGINT NULL;");
  ExecSql(ctx, @"IF COL_LENGTH(N'CF流程实例', N'F代理人姓名') IS NULL
      ALTER TABLE [CF流程实例] ADD [F代理人姓名] NVARCHAR(100) NULL;");
  ```
- 语义：null = 本人发起（非代提交）；非 null = 代提交，`FInitiatorId/FInitiatorName`=被代理人，`FAgentId/FAgentName`=真实操作人。`CfCard` 已 `ITenantScoped/IOrgScoped`，新列随现有 `F租户ID/F组织ID` 隔离，无需额外墙。

### 2.3 重提策略：复用现有 `FFlowSettingsJson.resubmitStrategy`（无 schema）

- 前端已 round-trip：`state.settings.resubmitStrategy: 'fromStart'|'fromRejected'`（FlowDefinitionEditPage.vue:105/148），存入 `CfFlowVersion.FFlowSettingsJson`（load `Object.assign(state.settings, fs)`，save `flowSettingsJson: JSON.stringify(state.settings)`）。引擎只需读。

## 三、三件详细设计（各自独立 commit，按风险从轻到重）

### 件 ①：E1 重提强制重路由（无 seeder，先做）

**引擎**（`FlowEngineService.ResubmitAsync`, :1021）：
- 现状：`stages = 按 FFlowVersionId 取节点; firstStage = stages[0]`（:1052-1059），无条件从头。
- 改：先加载卡片版本的 `CfFlowVersion.FFlowSettingsJson`（`card.FFlowVersionId`），复用 `ApproverResolver` 的 `ParseObject/TryGetProperty` 范式解析 `resubmitStrategy`（缺省 `fromStart`）。
- `fromRejected`：定位本卡**最近一次被驳回的节点**——`CfStageInstance` 中 `FFinalAction=="rejected"`（覆盖 reject 路径 FlowEngineService.cs:770 与 autoReject 路径）按 `FRound` desc / `FCompletedTime` desc 取第一条；用其 `FStageDefinitionId` 在 `stages` 中定位重启节点。找不到（防御）→ 回退 `stages[0]`。
- `fromStart`（默认）：保持现状 `stages[0]`。
- 其余不变：`FCurrentRound+1`、建实例、`OccupyBudgetOnSubmitAsync`（幂等键已含 `resubmit:{round}`，无需改）、auto 节点 `ExecuteAutoStageAsync` / 人工 `AssignStageHandlersAsync`、`LogActionAsync(..., "resubmit", ...)`。

**UI**：`FlowDefinitionEditPage.vue:2604` 注释保留"（引擎真消费）"（现已属实）；无新增控件（单选已在）。

**测试**（`tests/STOTOP.Module.CardFlow.Tests`）：
- A→B→C 三节点；`FFlowSettingsJson` 置 `resubmitStrategy=fromRejected`；在 B reject 使卡片 `returned`；`ResubmitAsync` 后断言活跃节点=B、`FCurrentRound` 递增。
- `resubmitStrategy=fromStart`（或缺省）→ 断言回到 A（锁默认行为不回归）。

**风险**：预算重占与幂等键轮次对齐（已含轮次，验证即可）；多轮多次驳回须取**最新轮次**驳回实例；不新增 FType（仍二元 auto/human）。

### 件 ②：发起范围结构化校验（seeder V71）

**引擎**：
- 新增 `IInitiatorScopeResolver`（或在 `CardService` 内私有方法 + 注入身份查询）：给定 `userId` 与 `initiatorScope`，判定是否在范围内（角色/组织/岗位/人员 union；空=放行）。身份来源：
  - 角色：现有角色查询（沿用设计器 `roleOptions` 背后的角色数据源 / `Sys用户角色`）。
  - 组织：用户所属组织（`IOrgContextAccessor` / 任职）。
  - 岗位：用户任职岗位（`SYS任职` / stage2 成员模型）。
  - 人员：`users` 白名单直接比 `userId`。
  - > 具体身份服务在实施 plan 前用子代理核实各维度现有查询入口，避免臆造。
- `GetAvailableFlowsAsync`(CardService.cs:41)：加载各 published 定义的策略，按当前 `userId` 身份 ∩ `initiatorScope` 过滤（现 `userId` 未用）。空 scope 的定义一律入清单。
- `CreateAsync`(CardService.cs:756)：加载定义后校验发起人在 `initiatorScope` 内；不中 → `throw new InvalidOperationException("无发起权限")`（GlobalExceptionMiddleware → 400）。
- **系统触发链豁免**：`BatchTriggerService` / 编排 / fileUpload（本就不在 available-flows）为系统身份，**不做发起范围校验**（无 HttpContext 身份，校验会读空 → 崩）。校验只加在**人工发起 Service 路径**（`CreateAsync`），覆盖 REST + 内部人工调用。
- 兼容读：策略解析器在 `FStartPolicyJson` 为空时从 `FAllowedRolesJson` 派生角色维（见 2.1）。

**UI（B1 发起抽屉解占位）**：
- `FlowVerticalGraph.vue:264-280` 起点弹层升级为真的**发起抽屉/配置面板**：`initiatorScope` 四维选择器——角色（复用 `roleOptions`）、组织（复用 `OrgSelect`）、岗位（岗位选择器，若无则本批新增薄壳）、人员（复用 `UserSelect`）。四维全空显式提示"不限制，任何有菜单权限者可发起"。
- 旧独立「可发起角色」多选(`FlowDefinitionEditPage.vue:1976-1982`)**折叠进抽屉的角色维**（单一真源）；保存以 `FStartPolicyJson.initiatorScope`（新列）为**权威**，同时把角色维**同步回写** `allowedRolesJson`（角色子集）——保留兼容读回退有效、不破坏任何潜在旧读者（成本近零）。
- 复用 `cardflow-designer.scss` `.cfd-*`；令牌 `var(--token)`，零裸 hex。

**测试**：角色/组织/岗位/人员各命中放行、未命中拒绝；空 scope 放行；available-flows 按身份过滤未授权流程不出现；`FStartPolicyJson` 空 + `FAllowedRolesJson` 有值 → 角色维派生生效（兼容）。

### 件 ③：代提交 onBehalf（seeder V72，最重 + 安全敏感）

**引擎**：
- `CreateCardRequest` 加 `long? ActualInitiatorId`（"代替谁发起"）。
- `CardService.CreateAsync`(756)：
  - `ActualInitiatorId == null`（默认）→ 现状：`FInitiatorId=userId`，`FAgentId=null`。
  - `ActualInitiatorId` 有值 → ①`onBehalf.enabled` 必须为 true，否则拒；②**越权护栏**：校验操作人 `userId` 在 `onBehalf.agentScope` 内（复用件②的 scope 解析器），不中 → `throw InvalidOperationException("无代提交权限")`；③置 `FInitiatorId=ActualInitiatorId`、`FInitiatorName`=被代理人姓名（现 CreateAsync 写空由 GetById 投影补名，代提交需确保被代理人名可解析）、`FAgentId=userId`、`FAgentName`=操作人姓名。
- `FlowEngineService.SubmitAsync` 门禁(:429-430)：放宽为 `operator==FInitiatorId || operator==FAgentId`（代理人可提交）。
- 修 `:518` 动作日志姓名口径：`LogActionAsync(..., operatorId, operatorId==card.FAgentId ? card.FAgentName : card.FInitiatorName, ...)`（现硬传 `FInitiatorName`，代提交下错标）。
- 下游 `initiator` 处理人/审批/待办自动按 `FInitiatorId`=被代理人（无需改）。

**UI**：
- 发起抽屉加「代提交」开关（`onBehalf.enabled`）+ `agentScope` 四维编辑器（谁可代提交）。
- 发起页 `views/workhub/InitiatePage.vue`：当所选流程 `onBehalf.enabled` 且当前用户在 agentScope 内 → 显示"代谁发起"人员选择器，提交带 `ActualInitiatorId`。

**测试**：A 代 B 建卡 → `FInitiatorId==B`、`FAgentId==A`、动作日志 `FOperatorId==A`；未授权代理（不在 agentScope）→ 拒绝；`onBehalf.enabled=false` 传 `ActualInitiatorId` → 拒绝；代理人 A 提交 B 的卡 → `SubmitAsync` 放行（现红）；`initiator` 策略/待办归 B。

**回归重点（`FInitiatorId` 语义反转）**：全系统把 `FInitiatorId` 当"操作即发起人"，代提交后=被代理人。须回归确认无处误用：
- `CardService.cs:164` isInitiator 访问门——被代理人 B 是发起人应可看；代理人 A 也应可看（**加 `FAgentId==userId` 到访问门**）。
- 撤回/作废/重提的"只有发起人可操作"门（`ResubmitAsync:1032`、`VoidAsync:1120`）——明确代提交后谁可重提/作废（建议：被代理人 + 代理人皆可，与提交门一致）。
- 迁移/预览/看板等读 `FInitiatorId` 处只作展示，不受影响（核实确认）。

## 四、实施顺序与 commit 边界

1. **commit 1**：件① E1 重提重路由（引擎 + 测试 + UI 注释落实）。无 seeder。
2. **commit 2**：件② 发起范围结构化（V71 + 实体/映射 + 引擎消费 + 发起抽屉 UI + 测试）。
3. **commit 3**：件③ 代提交 onBehalf（V72 + DTO + 引擎 + 门禁 + 日志口径 + UI + 测试 + 回归）。
4. **commit 4（可选）**：批收口——终审修正 + 回归补测。

每 commit：`build-filter cardflow` 编过、`test-dotnet cardflow` 绿（flaky 多跑几次）、前端 `type-check`+`vitest`+`lint:style`（零裸 hex）绿、经 hook 编译门禁。**不 push 等点头。**

> baseline 坑：本批不改 baseline JSON（纯 DDL add-column + 代码），故无"改 baseline 后重建 bin"步骤；若件②兼容读涉及 baseline 参考数据再评估。

## 五、复用资产（勿重造）

- FFlowSettingsJson 解析：`ApproverResolver` 的 `ParseObject/TryGetProperty/ReadLongArray`。
- seeder add-column 幂等：`MigrateV68`（`IF COL_LENGTH ... ALTER TABLE ... ADD`）。
- 续跑机制（件①可参考）：`ReturnToStageAsync/ReturnToStageRuntime`（在指定节点续跑全套）。
- UI：`cardflow-designer.scss` `.cfd-*`、`UserSelect`/`OrgSelect`/`roleOptions`、`stageDefinitionShared.ts`。
- 干跑核对：`CardFlowPathPreviewService` + 干跑工作台。

## 六、明确不做 / 保持诚实占位

- 发起范围维度到「角色/组织/岗位/人员」为止；更复杂的表达式/白名单组合不做。
- onBehalf 仅"发起+提交"阶段身份，不引入审批阶段代理（那是 `Delegation`，另属未消费死代码，本批不碰）。
- 不新增 `cc` FType；抄送归 M8-B。

## 七、测试策略

- 后端 xUnit `tests/STOTOP.Module.CardFlow.Tests`：`TestDbContextFactory.Create` + InMemory + `TestOrgContextAccessor` + `RegisterModuleAssembly`；中文 `[Fact]` 方法名；每件红先绿后。CardFlow.Tests 集成套件 flaky，判回归多跑。
- 前端 `vitest`：scope 解析/发起抽屉纯逻辑单测（若抽出 shared ts）。
- UI 保真：`ui-baseline.md` + `preview_inspect ≥5 项`。
