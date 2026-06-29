# CardFlow 卡片流程 简化/瘦身方案（设计稿）

> 日期：2026-06-16　状态：边界与决策点 D1-D5 已确认（2026-06-16），进入 writing-plans 细化阶段1
> 来源：基于 2026-06-16 一次 18 智能体只读设计审视（14 子系统映射 + 4 批判视角），所有结论均有真实 file:line 证据。本文为决策性方案文档，**不修改任何代码**。

---

## 0. 背景与诊断结论（先看这 5 条）

1. **作者自评"卡片流程设计有些重了、到现在还没真正用起来"。** 审视证实"重"是真的，但**"重"与"用不起来"是两件根因相反的事**，不能用同一副药。
2. **"重"的根因 = 重构未收尾**：同一件本质很小的事并存 3–5 套互不复用的实现 + 大量死链 + 零数据的扩展点。这部分约 80% 可直接删/冻结，**删了不丢任何能力**。
3. **"用不起来"的主因 = 最后一公里没做完**，不是抽象太重：
   - 没有任何一条"可复制改改就用"的种子流程（Seeder **零条卡片实例**，人工节点全是 fixed 到管理员的占位）；
   - 唯一的"从模板创建"入口**恒返回空**：`FlowDefinitionService.GetTemplatesAsync` 过滤 `FOrgId==0 && published`，而所有种子流程 `FOrgId=192`；
   - 审批主线的可视化设计器其实是**表单驱动、能跑通的**（"缺设计器/缺移动端"与代码不符，可排除）。
4. **自动凭证那段确实卡在上游口径**（申通 `ruleGroups` 空 `[]`、韵达/极兔零映射），但它**只挡"导入→凭证"，不挡"人工审批"本身**。
5. **规模佐证**：52 实体 / 127 服务文件 / ~50 对接口 / 11 插件 / 后端 ~208 端点 / 前端 169 个 API；而生产里真正成链跑通的只有 **3 条直线导入流**（迁移脚本里全部 `F类型=auto`、`审批模式=none`）。审批/路由/编排/展示这些重抽象，生产数据里一行都没用上。

---

## 1. 目标 / 非目标

### 目标
在**完整保留作者 4 条设计意图**的前提下：
- 把"重"的体感来源（死代码 / 重复实现 / 零数据扩展点）清掉或冻结；
- 让 CardFlow 从"建好没通电"变成"明天能用"——至少一条真实审批流端到端跑通；
- 给主引擎 `FlowEngineService` 补上第一条端到端回归护栏。

### 作者 4 条设计意图（不可丢的约束）
1. 整个系统基于**事件/异常驱动**。
2. 工作中到处是流程，按顺序与条件处理；环节抽象为两类节点：**人工处理节点、自动处理节点**。
3. 信息传递往往是一张表单，移动端展示像一张**卡片**，信息流转即卡片在各环节间流转。
4. 使用者设计/调整流转规则**尽量简单易上手**；卡片信息按不同节点以恰当内容与形式展示。

### 非目标（本轮明确不做）
- ❌ 不重写编排引擎——跨流程 DAG 整体**冻结待 V2**。
- ❌ 不解决韵达/极兔凭证映射——上游口径未定。
- ❌ 不动 Express 计费逻辑本身。
- ❌ 不改已在生产跑的导入主链的**行为**（只去重复实现，不改结果）。

---

## 2. 路线图（执行顺序 A）

顺序经反方视角校正：**"收敛重复"要动正在跑的 `FlowEngineService`，而现有测试全是内存桩、无端到端测试**，故必须先有一条真实流 + E2E 测试做护栏，再做这台大手术。

```
阶段1  清死代码（零风险减法）
   └─ git rm 确证零调用代码 + 摘前端冗余入口   → 验收：现有单测全绿 + 后端编译通过
阶段2  点亮（补齐 + 修口径，让它通电）          ← 最高价值
   └─ seed 费用报销样板流 + 修“从模板创建恒空” + 补第一条端到端测试
   └─ 验收：一条报销卡片真实走完“发起→审批→完成” + E2E 绿
阶段3  收敛重复（有真实流做护栏后才动主引擎）
   └─ 5套条件评估→1套 / 3套字段模型→1套 / 处理器三代→1套 / 恢复·撤销各留一份
   └─ 验收：E2E + 单测全程绿
```

> 转入 writing-plans 时**先只把阶段1细化成可执行实现计划**；阶段2、3 待阶段1 落地、作者复核后各自再细化。

---

## 3. 红线清单 · 必留（动了就丢作者意图，任何阶段都不碰）

| 能力 | 关键位置 | 对应意图 |
|---|---|---|
| `FlowEngineService` 单卡线性引擎（提交/审批/退回上一节点·退回发起人） | `src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs`（`SubmitAsync`/`ApproveAsync`/`RejectAsync`/`ProcessBatchStagesAsync`） | ①② |
| `ApprovalModeHandler` 的 **single + 会签(countersign)** 两种 | `Services/ApprovalModeHandler.cs`（全文 ~60 行干净 switch） | ② |
| 取人策略 `fixedUsers` / `role` / `initiator` + **amountMatrix（金额分级）** | `Services/ApproverResolver.cs`（单层 switch 分支，加性） | ② |
| 节点级 `fieldAccess` + 身份证/银行卡**脱敏** | `Services/StageViewProfileResolver.cs`（`RedactJson`/`RedactDetailJson`） | ④ |
| 一套条件路由 `ConditionRuleEvaluator`(JSON 规则树) + `StageRouteResolver` | `Services/ConditionRuleEvaluator.cs` / `Services/StageRouteResolver.cs` | ② |
| 前端 `ConditionBuilder.vue`（唯一真可视化）+ `FlowStateCanvas`(vue-flow) + `SchemaFieldEditor` | `web/src/components/cardflow/**` | ④ |
| 导入主链 `ExcelInputPlugin` + `Pricing/Cost` + `AutoVoucherPlugin` + `ExcelParserService` + 批次 Channel/`BatchJobProcessorService` | `AutoPlugin/Implementations/**`、`Services/Import/ExcelParserService.cs` | ①② |
| 移动端填单/审批（**已存在**，`/m/cardflow/fill`、`/m/cardflow/approval`） | `web/src/router/routes.ts`、`web/src/views/cardflow-mobile/**` | ③ |

---

## 4. 桶 A · 确证死代码 → `git rm`（阶段1，风险低：零调用）

> 实现原则：**每删一项先 `grep` 复核"零调用/零编排器遍历"**，删后跑现有单测应全绿（删的都是零调用）。

| 项 | 位置 / 证据 | 备注 |
|---|---|---|
| `IImportStage` 全套 5 个 Stage + `ImportContext` | `Services/Import/ImportStages.cs`、`StagingImportStage.cs`、`QualityAnalysisStage.cs`；无任何 `IEnumerable<IImportStage>` 遍历，与 Plugin 逐行重复 | 注册在 `CardFlowModuleExtensions.cs:121-125` |
| `SourceParserFactory` + `DynamicSourceParser`（~540 行） | `Services/Import/Parsers/*`；工厂 `GetParserAsync` 唯一实现是 `throw`，Parser 仅被该空壳工厂引用 | 自闭环死代码 |
| `DispatchRouter`（分类派发主干） | `Services/Handlers/DispatchRouter.cs`；`DispatchAsync` 全库零调用方 | 生产链 `ClassificationPlugin` 只调 `AnalyzeAsync` 即返回 |
| `BatchProgressCallbackService` / `IBatchProgressCallback` | `Services/BatchProgressCallbackService.cs`；零调用，且 `UpdateProgressAsync` 把 percent 误写进 `F状态` 列（潜在 bug） | 注册在 `CardFlowModuleExtensions.cs:92` |
| `BatchRecoveryHostedService` | 与 `BatchJobProcessorService` 内置 `RecoverPendingBatches` 重复恢复（同一 -10min cutoff），且用裸 `_ = Task.Run` | **保留** `BatchJobProcessorService` 内置那份 |
| 死成员若干 | `QualityAnalysisStage.CheckDuplicateKeysAsync`、`QualityAnalysisPlugin.ValidateRowBuiltin`（无规则时走不到）、`ITransformEngine.Preview`、`BatchTriggerService` 内 `[Obsolete]` 段（~150 行）、退化的 `BatchJobKind` 三分支 | 定义后无调用 |
| **委托** `CheckAndCreateDelegateTodoAsync`（半成品死链） | `Services/DelegationService.cs:116`；src 内零调用方 | ⚠️ **决策点 D1**：阶段1 先删死方法，委托作为 V2；或接通一行调用。**默认：删** |

**桶 A 风险**：低。唯一需人工确认的是"零调用"判定，实现时逐项 `grep` 复核。

---

## 5. 桶 B · 有测试但零数据的能力 → 冻结（隐藏入口 + 保留代码 + 标 V2）

> 冻结 = 前端路由/菜单摘掉 + 保留后端代码（不删），加注释标 `// V2：暂未启用`。等单卡流真正用起来、出现真实需求再逐个通电。

| 冻结项 | 位置 | 为什么冻结而非删 | 风险 |
|---|---|---|---|
| 编排引擎 `OrchestrationEngineService` + `OrchestrationController` + 4 个 `Orchestration*.vue` + `CfOrchestration*` 实体 | `Services/OrchestrationEngineService.cs`(969 行) 等 | 跨流程 DAG 是真实未来能力，有测试有前端；但核心触发 `TriggerCardFlowNodeAsync:490` 是自认占位死链 | 中：通电要补完触发逻辑 |
| 动态加签 `DynamicStagePolicyResolver` + `CfDynamicStagePolicy` + `DynamicApprovalPolicyEditor.vue` | `Services/DynamicStagePolicyResolver.cs` | 金额分级可先用"条件路由 + 占位节点"表达 | 低 |
| Jint 转换引擎 + 前端转换步骤 | `Services/Import/TransformEngine/JintTransformEngine.cs`、`web/.../ExcelInputRuleForm.vue` | 生产规则 `transformRules` 全空、`lookup` 是 TODO；保留代码 | 低 |
| 下载/采集子系统 | `Services/Download/**`、`web/.../automation/FlowDesigner.vue`、automation 路由 | 核心闭环"下载→入卡片流"是 TODO 空壳、前后端模型不通、路由名错配 | 低 |
| `CardComponent` 低代码展示层（~40 组件目录 + 9 runtime 组件 + relation/snapshot 绑定） | `web/.../designer/CardComponentCatalog.vue`、`cardComponentCapabilities.ts` 等 | 种子零使用（`components`/`componentAccess` grep 命中 0）；保留**扁平字段渲染**即满足第4条 | 中：摘前端要确认无在用页面引用 |
| 质量规则引擎 `QualityRuleEngine`（配置驱动） | `Services/Quality/QualityRuleEngine.cs` | `CfQualityRule` 零种子、0 规则即跳过；改留 3–5 条**内置硬规则**，隐藏编辑器 | 低 |
| 导入计算验证工作台（~3300 行） | `Services/Validation/ImportCalculationValidationService.cs`(1603) 等 | 为复杂凭证兜底而生；先后置，与 AutoVoucher 收敛联动 | 中 |

**桶 B 风险**：低–中。冻结不删，可随时 git 复活；主要风险在前端摘除时确认无在用引用。

---

## 6. 桶 C · 收敛重复（阶段3，有真实流 + E2E 护栏后才动）

> 动的是**正在跑的 `FlowEngineService`**，故排在阶段2点亮 + E2E 测试之后。

| 收敛项 | 现状 | 目标 |
|---|---|---|
| 条件评估 5 套 → 1 套 | `ConditionRuleEvaluator`(JSON树) + `ConditionEvaluator`(字符串DSL，内部又委托前者) + 编排私有 `EvaluateCondition` + `AutoVoucherMatchingEngineV2.EvaluateCondition` + `ClassificationEngine` 的 JSON→SQL 编译器 | 统一到 `ConditionRuleEvaluator`，删其余四套（顺带消除 SQL 注入面） |
| 字段模型 3 套 → 1 套 | `SchemaFieldDefinition` / `CardFieldDefinitionV2` / `CardComponentDefinition` | 1 套扁平 Schema + 1 个读取器，**顺带修 Array/Object 解析口径不一致的真 bug** |
| 处理器三代 → 1 套 | `IAgent→IAutoNodeHandler→IAutoPlugin`；Handler 与 Plugin 双工厂注册同名编码 | Handler 直接实现 `IAutoPlugin`，删 4 个 Plugin 薄壳 + `ClassificationHandlerFactory` |
| 崩溃恢复 / 撤销 各两份 → 各一份 | 恢复双 HostedService；撤销 `BatchLifecycleService.RevokeBatchAsync` vs `BatchRevokeHandler` 口径不一 | 各收敛为单一路径 |
| Excel 落库 3 份 → 1 个纯函数 | `ExcelInputPlugin` / `StagingImportStage` / `DynamicSourceParser` 逐行重复 | 抽 `RowMapper` 无状态纯函数，**统一主键大小写**（现 HexString 大写 vs `ToLowerInvariant` 会去重错位） |
| SignalR 三套命名 | `Clients.All` / `org_{id}` / `batch-{id}` / 前端订 `import-{id}`，推送到不了 | 统一一套，或暂时下线只用轮询（前端 `BatchProgressPanel` 已在用） |
| AutoVoucher 三层级联 → 单层精确查表 | `AutoVoucherMatchingEngineV2`(523行) + 3300 行验证工作台 | ⚠️ **决策点 D2**：标为**阶段3可选 / 可再拆**，不阻塞前两阶段。退化为"费用编码→借贷科目映射表 + 单次 GroupBy" |

**桶 C 风险**：中–高（动主引擎），故强约束"必须在阶段2 的 E2E 护栏就位后才开工"。

---

## 7. 阶段2 点亮 · 具体设计（费用报销）

1. **样板流**：发起（金额 / 事由 / 费用归属单元 / 附件）→ 部门负责人审批（single / role）→ 财务审批（single / role）→ 完成。
   - **先只做人工审批，不接自动凭证**（凭证段因上游口径未定，留作后续）。
2. **修"从模板创建恒空"**（决策点 D3）：
   - 现状：`GetTemplatesAsync` 靠 `FOrgId==0` 隐式判定模板，种子是 `FOrgId=192` → 恒空。
   - **默认方案**：新增显式 `FIsTemplate` 标志，不再靠 `FOrgId==0` 隐式判定（更干净）。
   - 备选：直接 seed `FOrgId=0` 的系统模板（改动更小，但延续隐式约定）。
3. **端到端测试**：补第一条"发起→审批→完成"集成测试（现状全是 EFCore.InMemory 内存桩，**无 E2E**）——它也是阶段3收敛的护栏。
4. **入口可发现性**：WF 触发动作新增 `cardflow.apply` → 让"发起审批"成为一级入口（现状唯一入口 `cardflow.start` 指向 `/cardflow/upload`，把 CardFlow 对外定位成"上传"）。

---

## 8. 验证 / 回滚策略

- **分阶段独立 commit**（必要时独立分支）。
- 桶 B 冻结项 git 保留、随时可复活；桶 A 删除项 git 历史可恢复。
- 阶段1 验收 = 现有单测全绿 + 后端编译通过。
- 阶段2 验收 = 一条报销卡片真实走完"发起→审批→完成" + E2E 绿。
- 阶段3 验收 = E2E + 单测全程绿（动一处验一次）。
- 前端验证基线参照 memory：`vue-tsc` 基线本就红，验证以"本文件不新增报错 + `vite build` 兜底"为准，不引入新测试框架。

---

## 9. 决策点登记（已确认 2026-06-16）

| 编号 | 决策 | 结论（已确认） |
|---|---|---|
| D1 | 委托 `CheckAndCreateDelegateTodoAsync`：删死方法 还是 接通 | ✅ **删**（委托作为 V2） |
| D2 | AutoVoucher 三层级联简化：本轮做 还是 后置 | ✅ **后置**（阶段3可选，不阻塞前两阶段） |
| D3 | 修"模板恒空"：新增 `FIsTemplate` 标志 还是 seed `FOrgId=0` 模板 | ✅ **新增 `FIsTemplate` 标志** |
| D4 | 编排引擎：冻结 还是 删 | ✅ **冻结**（代表真实未来能力） |
| D5 | 报销流是否本轮接自动凭证 | ✅ **否**（先只做人工审批） |

---

## 10. 附：规模速览（审视采样口径）

- 后端：`src/STOTOP.Module.CardFlow` 52 实体 / 127 服务文件 / ~50 对接口 / 11 AutoPlugin / ~208 端点。
- 前端：`web/src/api/cardflow.ts` 169 个导出函数；`views/cardflow` + `components/cardflow` 共 109 文件；最大单文件 `FlowDefinitionEditPage.vue` 4412 行。
- 测试：27 文件 / 4075 行，全部 EFCore.InMemory 单测，**无端到端集成测试**。
- 生产真实在跑：迁移脚本 `database/migrate_pipeline_to_cardflow.sql` 仅建 3 流程 / 3 版本 / 10 节点，全部 `F类型=auto`、`审批模式=none`。

---

> **下一步**：本 spec 经作者复核通过后，调用 writing-plans 把**阶段1（清死代码）**细化为可执行实现计划。
