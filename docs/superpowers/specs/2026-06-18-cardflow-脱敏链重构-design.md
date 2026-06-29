# CardFlow 脱敏链重构（设计稿）

> 日期：2026-06-18　状态：设计已确认（3 个核心分叉 + 粘附授权 + MVP 强度均已拍板），待 writing-plans 细化
> 上游：CardFlow 三轮审计（[统一报告](../../audits/audit-report-0-unified.md) 第 0 批 · 脱敏链）。本文为方案文档，**不修改任何代码**；现状结论带真实 file:line（2026-06-17 审计 + 2026-06-18 复核）。

---

## 0. 背景与缺陷（已核实）

脱敏链当前由 `CardService.GetByIdAsync` 内一段 `if (active 且 v2)` 触发，存在四处叠加缺陷，导致设计意图④「卡片信息按节点以恰当内容/形式展示 + 字段权限脱敏」在真实路径上未兑现：

| 编号 | 缺陷 | 证据 |
|---|---|---|
| #3（严重） | 脱敏只在 `currentStage` active 且 `normalizedConfig.Version==2` 时执行；否则 `detail.DataJson=card.FDataJson` 原值回传。已完成/归档/草稿/v1/无 active 节点卡片，masked/hidden 字段原值直达浏览器，最该控权的归档态最敞开。`operatorId/userId` 传入却全程未用于脱敏。 | [CardService.cs:137](../../../src/STOTOP.Module.CardFlow/Services/CardService.cs:137)、[:194-228](../../../src/STOTOP.Module.CardFlow/Services/CardService.cs:194) |
| #4（严重） | `RedactJson`/`RedactDetailJson` 只遍历 `fieldAccess` 键（= schema 声明字段）做删/掩，DataJson 中 schema 外的键（导入/历史/扩展）即便敏感也漏脱、原值下发。`fieldAccess` 由 `BuildFieldAccess(ReadFieldKeys(cardSchemaJson))` 产生。 | [StageViewProfileResolver.cs:163-179](../../../src/STOTOP.Module.CardFlow/Services/StageViewProfileResolver.cs:163)、[:85-105](../../../src/STOTOP.Module.CardFlow/Services/StageViewProfileResolver.cs:85) |
| #9（中） | `MaskValue` 恒走通用规则 `len<=4?"****":"前2****后2"`，从不读 `StageFieldAccessRule.MaskPattern`（模型有该字段）；前端 `maskedValue` 却按 `props.maskPattern` 支持 phone/idCard，且对后端已掩串再掩一次 → 两端口径漂移、二次打码。 | [StageViewProfileResolver.cs:226-243](../../../src/STOTOP.Module.CardFlow/Services/StageViewProfileResolver.cs:226)、[StageViewProfileModels.cs:42](../../../src/STOTOP.Module.CardFlow/Models/Schema/StageViewProfileModels.cs:42)、[CardComponentRenderer.vue:139-148](../../../web/src/components/cardflow/runtime/CardComponentRenderer.vue:139) |
| #17（低） | 明细聚合 `SumDetailField`/`ApplyAggregations`/`ComputeDefaultDetailSummary` 直接对原始 `FDataJson` 求和，不看 `DetailAccess`，被 hidden/masked 列照样求和并随 detailSummary/amountSummary 下发，可反推被脱敏列。 | [CardPresentationResolver.cs:350-397](../../../src/STOTOP.Module.CardFlow/Services/CardPresentationResolver.cs:350) |

**硬前置（审计 #7）**：字段定义模型 `SchemaFieldDefinition`（[Responses.cs:399](../../../src/STOTOP.Module.CardFlow/Dtos/Responses.cs:399)，仅 Key/Label/Type/Required/Readonly）与 `CardFieldDefinitionV2`（[CardSchemaV2Models.cs:23](../../../src/STOTOP.Module.CardFlow/Models/Schema/CardSchemaV2Models.cs:23)，有 `FieldDisplayBehavior` 但无敏感位）**均无 sensitive/maskPattern**。脱敏目前只能经节点 `ViewProfile.FieldAccess` 表达 → 一旦按阶段3e 冻结低代码、只留扁平渲染，意图④「分节点脱敏」将无处声明。本重构补齐字段级敏感位，是冻结低代码的硬前置。

**附带事实**：`GetByIdAsync` 取 `GetUserId()`（[CardController.cs:65](../../../src/STOTOP.Module.CardFlow/Controllers/CardController.cs:65)）但**无任何卡片级访问校验**——同组织（CfCard 全局过滤器）任意登录用户可读任意卡片整卡 + 明细。

---

## 1. 目标 / 非目标

### 目标
1. 脱敏成为**适用任意卡片状态**、与 active 节点解耦的稳定后置过滤层（解 #3）。
2. 改 **allowlist** 语义：按有效访问表白名单输出，schema 外键默认移除（解 #4）。
3. **maskPattern 后端权威**：后端按 pattern 打码、返回已掩值；前端不再二次打码（解 #9）。
4. 明细**聚合尊重列权限**（解 #17）。
5. 字段定义补**字段级敏感基线**（sensitive + maskPattern），作为冻结低代码的硬前置（解 #7 前置）。
6. 补**最小卡片访问门**：查看者须与卡片有关系，否则拒绝。
7. 收敛为**单一脱敏权威** `CardRedactionService`，消除散落两份实现。

### 非目标（本轮明确不做）
- ❌ 不做完整 RBAC（不按"部门/数据范围"细分可见性，只按"查看者与本卡片的关系"）。
- ❌ 不改流程设计器的整体 UX（仅在 `SchemaFieldEditor` 加一个敏感开关 + maskPattern 下拉，供声明敏感位）。
- ❌ 不在本轮执行低代码冻结本身（只补齐其硬前置）。
- ❌ 不改写计费/凭证逻辑。

---

## 2. 已锁定决策

| 编号 | 决策 | 结论 |
|---|---|---|
| D1 | 无 active 节点卡片的脱敏口径 | **字段级敏感基线**：任何状态/查看者默认按字段 `Sensitive` 脱敏；节点只能在其 active 时对处理人提权 |
| D2 | DataJson 中 schema 外键 | **默认移除**（allowlist） |
| D3 | 卡片级访问门 | **本轮纳入最小访问门** |
| D4 | 过去节点处理人再查看 | **保留其节点当时被授予的明文**（授权随处理历史粘附，非仅 active 期间） |
| D5 | 敏感基线强度（MVP） | `Sensitive=true` 默认 `masked`；完全 `hidden` 仍可经节点 `ViewProfile.FieldAccess` 表达，不在字段级单设 hidden 档 |

---

## 3. 架构：单一脱敏权威 `CardRedactionService`

新建 `Services/Redaction/ICardRedactionService` + `CardRedactionService`：

```
RedactionResult Redact(CardRedactionRequest req)
  req: { Card, FlowVersion(cardSchemaJson/detailSchemaJson), AllStageInstances, AllAssignees,
         StageDefinitions(按需:viewer 处理过的节点 + 当前 active 节点), Details, ViewerRelation }
  返回: { RedactedDataJson, RedactedDetails[], EffectiveFieldAccess, EffectiveDetailAccess }
```

- **纯函数式**（入参为已加载实体，无 DbContext）→ 可独立单测。
- `CardService.GetByIdAsync` 加载实体后**无条件**调用（去掉 `active+v2` 闸门，v1 也走基线）。
- `StageViewProfileResolver` 与 `CardPresentationResolver` 的脱敏（`RedactJson`/`RedactDetailJson`/`MaskValue`/明细聚合）改为委托同一服务，消除两份实现（单一真源）。
- `MaskValue` 提升为共享 `FieldMasker.Mask(value, pattern)`，前后端共用同一组 pattern 语义。

---

## 4. 设计细节

### 4.1 字段级敏感基线（数据模型）
- `SchemaFieldDefinition` 加 `bool Sensitive=false` + `string? MaskPattern`。
- `CardFieldDefinitionV2` 加 `bool Sensitive=false` + `string? MaskPattern`（明细列 `CardFieldDefinitionV2` 复用，明细列共用同模型）。
- 前端：`web/src/types/cardflow.ts` 同步类型；`SchemaFieldEditor.vue` 加"敏感字段"开关 + `maskPattern` 下拉（phone/idCard/bankCard/email/name/通用）。
- 兼容：旧 schema 无该字段 → 反序列化默认 `Sensitive=false`，行为不变。

### 4.2 有效访问计算（粘附授权模型）
对每个 schema 字段，按查看者算最终 access ∈ `{editable, readonly, masked, hidden}`，许可强弱序 `hidden(0) < masked(1) < readonly(2) < editable(3)`：

1. **基线** baseline = `field.Sensitive ? masked : readonly`。
2. **粘附授权**（D4）：取该查看者在本卡片**任意轮次**作为处理人的全部节点（含发起的提交节点），对每个字段取这些节点 `ViewProfile.FieldAccess` 给的许可；非 active 节点的 `editable` **降级为 readonly**（非活动卡不可编辑，但保留明文可见）。grant = 这些许可中的最强者。
3. **当前 active 节点**：若查看者是当前 active 节点处理人，叠加该节点**实时**规则（可 editable，`InputFields` 内 → editable；也可低于基线做限制，如把非敏感字段 hidden）。
4. **发起人**：视为提交节点的处理人，纳入步骤 2（保留对其填写字段的明文）。
5. 最终 access = `max(baseline, grant, activeRule)`，其中 activeRule 的"限制"（低于基线）仅对当前 active 处理人生效。
6. 非关联查看者（仅抄送/管理员，从未处理）→ baseline。

> 直观结果：默认人人看到敏感字段打码；曾经手过"放行该字段"节点的处理人（财务等）此后一直看明文；归档卡对没经手过的人仍打码。

明细列 access 同理（baseline 用明细列 `Sensitive`，节点 `DetailAccess` 提权/限制）。

### 4.3 allowlist 脱敏改写（D2）
`RedactDataJson(dataJson, effectiveFieldAccess)`：
- 解析 dataJson 为对象；**输出对象只遍历 `effectiveFieldAccess` 的键**（白名单）：
  - `hidden` → 不写入；
  - `masked` → `FieldMasker.Mask(原值, 该字段 maskPattern)`；
  - `readonly/editable` → 原值拷入。
- **dataJson 中不在访问表的键（schema 外）一律不写入**（默认移除）。
明细行 `RedactDetailJson` 同构，按 `detailTableKey.column` 白名单。

### 4.4 maskPattern 后端权威（D 解 #9）
- `FieldMasker.Mask(value, pattern)` 实现：`phone`(前3后4)、`idCard`(前4后4)、`bankCard`(后4)、`email`(首字符+***@域)、`name`(姓+*)、`null/通用`(前2后2，≤4 全 *)。pattern 取字段级 `MaskPattern`，节点 `FieldAccess.MaskPattern` 可覆盖。
- 后端返回**已掩值**；前端 `CardComponentRenderer.maskedValue`/`SchemaRenderer` **不再二次打码**，服务端值原样显示；`props.maskPattern` 退化为展示元数据（保留以免破坏模板，但不参与运算）。

### 4.5 明细聚合尊重列权限（解 #17）
`SumDetailField`/`ApplyAggregations`/`ComputeDefaultDetailSummary` 求和前查有效 `DetailAccess`：hidden/masked 列**不计入默认聚合、不下发**；配置型 sum 指向受限列 → 该 target 置 null/省略并记 warning。

### 4.6 最小卡片访问门（D3）
`GetByIdAsync`（及新服务入口）先算 `ViewerRelation`：
- `Initiator`：`card.FInitiatorId == userId`；
- `Assignee`（含过去）：本卡任意 `CfStageAssignee.FUserId == userId`；
- `CcRecipient`：本卡抄送待办 `CfTodoItem`（type=cc）含 userId；
- `Admin`：持卡片管理权限（权限码实现时定，复用 `CardFlowPermissions`）。

四者皆否 → 返回"无权查看该卡片"（控制器 → `ApiResult.Fail`/404 语义保持与现有"卡片不存在"一致，避免暴露存在性）。该 relation 同时喂给脱敏（4.2）。

---

## 5. 落地点与受影响读路径

- 新增：`Services/Redaction/ICardRedactionService.cs` + `CardRedactionService.cs` + `FieldMasker.cs`；DI 注册（[CardFlowModuleExtensions.cs](../../../src/STOTOP.Module.CardFlow/CardFlowModuleExtensions.cs)，Scoped 或纯静态）。
- 改：`CardService.GetByIdAsync`（去闸门、加访问门、无条件脱敏、明细脱敏走新服务）。
- 改：`StageViewProfileResolver`（脱敏委托新服务，保留它产出 active 节点 work-view 的职责）。
- 改：`CardPresentationResolver`（组件值脱敏 + 明细聚合走新服务/共享 masker）。
- 改：模型 `SchemaFieldDefinition` / `CardFieldDefinitionV2` + 前端类型 + `SchemaFieldEditor.vue` + 前端 `maskedValue`/`SchemaRenderer` 停止二次打码。
- **本轮覆盖读路径**：`GetByIdAsync`（整卡 DataJson + 明细 + 组件 work-view）。
- **验证项（不一定改）**：`GetCardsAsync` 列表 DTO（[CardController.cs:35](../../../src/STOTOP.Module.CardFlow/Controllers/CardController.cs:35)）若含字段值则同接，否则记录确认；移动端审批/详情页同样消费 GetById 输出，后端修好即覆盖。

---

## 6. 兼容性与风险

- **V1（Array）/旧卡片**：字段无 `Sensitive` → 基线 readonly，不脱敏，行为不变。
- **allowlist 移除 schema 外键的风险（最需验证）**：若现网卡片 DataJson 含前端依赖、但未在 schema 声明的键，会随 allowlist 消失。**计划阶段须验证**：采样在用流程的 `FDataJson` 键集 vs schema 字段集（费用报销模板键应全在 schema 内）；如发现在用的 schema 外键，评估"显式加入 schema"或临时 passthrough 白名单。
- **现有节点 `ViewProfile.FieldAccess`**：继续生效，现纳入粘附授权/active 实时规则，语义向后兼容。
- **访问门**：可能挡掉某些"旁观查看"既有用法 → 计划阶段确认现网是否有非关联用户查看卡片的合法场景（如审计岗），不足则把审计岗纳入 Admin 关系。

---

## 7. 测试

InMemory 单测（新建 `CardRedactionServiceTests`）：
- 归档/完成态卡片：敏感字段对非关联查看者 → masked；schema 外敏感键 → 被移除。
- 粘附授权：曾处理"放行银行卡"节点的财务，卡片完成后仍看明文；从未经手者 → masked。
- active 处理人：节点 `InputFields` → editable；节点限制非敏感字段 hidden 仅对其生效。
- 明细：masked 列值打码 + 默认聚合跳过该列、不下发。
- `FieldMasker`：phone/idCard/bankCard/email/name/通用 各格式。
- 访问门：无关系查看者被拒；发起人/处理人/抄送/管理员放行。
回归：既有 98 单测 + 阶段2 E2E（发起→部门→财务→完成）全绿；E2E 补一条"完成后非关联用户查看 → 敏感字段 masked + 访问门"。

---

## 8. 任务分解预览（供 writing-plans）

1. 数据模型加敏感位（`SchemaFieldDefinition`/`CardFieldDefinitionV2` + 前端类型）。
2. `FieldMasker` 共享打码器（含单测）。
3. `ICardRedactionService`/`CardRedactionService`：有效访问计算（粘附授权）+ allowlist 脱敏 + 明细 + 聚合（含单测）。
4. `CardService.GetByIdAsync` 接入：访问门 + 无条件脱敏 + 明细走新服务。
5. `StageViewProfileResolver`/`CardPresentationResolver` 脱敏委托新服务，删两份重复实现。
6. 前端：`SchemaFieldEditor` 敏感开关 + maskPattern；`maskedValue`/`SchemaRenderer` 停止二次打码。
7. 验证：schema 外键采样核查 + 列表路径确认 + 全量单测/E2E + WebAPI 编译。

> 转入 writing-plans 后逐项细化为可执行步骤。

---

## 9. 后续收口（2026-06-18，详情端点已落地后补）

主重构只覆盖了 `GetByIdAsync` 的整卡 DataJson / 明细 / 组件 work-view（第 5 节「本轮覆盖读路径」）。复核发现两处同类 PII 泄露面遗留在范围外，本节收口。**两处均已与用户确认取「最简移除」口径**（前端全量 grep 证据支撑）。

### 9.1 列表端点 `InitialDataJson`（泄露面 #1）

- **现状**：`CardListDto` 含 `InitialDataJson`，被 `GetCardsAsync`（[CardService.cs:100](../../../src/STOTOP.Module.CardFlow/Services/CardService.cs:100)）、`GetInitiatedCardsAsync`、`GetAvailablePrerequisitesAsync`（[:915](../../../src/STOTOP.Module.CardFlow/Services/CardService.cs:915)）三个列表投影**明文下发**；列表无卡片级访问门 → 任意通过的查看者可读他人卡片预填业务载荷明文。
- **证据**：前端全量搜索 `initialDataJson` 仅命中「构造请求」（`PathPreviewPanel.vue`）与「读 query string」（`CardFillForm.vue`），**无任何响应消费方**（列表/详情均不读）。
- **决策（D6）**：从 `CardListDto` **移除** `InitialDataJson`，**下沉**到 `CardDetailDto`。最简最安全、零 UX 回归。否决「列表逐卡脱敏」（N 卡 × schema 解析 + N+ 查询，开销大且无消费方）。
- **改动**：
  - `Responses.cs`：`InitialDataJson` 从 `CardListDto` 移到 `CardDetailDto`。
  - `CardService.GetCardsAsync` / `GetAvailablePrerequisitesAsync`：两处列表投影删除 `InitialDataJson` 赋值。
  - `GetByIdAsync`（[:149](../../../src/STOTOP.Module.CardFlow/Services/CardService.cs:149)/[:258](../../../src/STOTOP.Module.CardFlow/Services/CardService.cs:258)）不动——赋给 `CardDetailDto` 自身属性，仍是脱敏后 `RedactedInitialDataJson`。
  - 前端 `cardflow.ts`：`CardListDto` 删 `initialDataJson?`；`CardDetailDto` 显式补 `initialDataJson?: string | null`（不再继承）。

### 9.2 详情 `AuditTrail` 决策快照（泄露面 #2）

- **现状**：`LoadRuntimeAuditTrailAsync` 的 routeDecision 项 metadata 含 `decisionSnapshot`（`FDecisionSnapshotJson`，含 `fields` 值映射，写入期仅按字段名启发式打码 bank/phone…，**漏掉 schema 声明的 `Sensitive` 字段如金额**）与 `candidateResults`（`FCandidateResultsJson`，含 `Explanation`/`TypeErrors` 自由文本，可能内嵌「金额 50000 > 阈值」这类值）。
- **证据**：前端唯一消费方 `CardTimeline.vue` 只读 `edgeKey`/`selectedRouteEdgeKey`/`policyKey`/`handlerNames`，**不渲染** `decisionSnapshot`/`candidateResults`。
- **决策（D7）**：从 routeDecision 审计项 metadata **移除** `decisionSnapshot` + `candidateResults`，保留路由结构元数据键。否决「字段访问表逐键打码」——`Explanation` 自由文本内嵌值无法可靠打码，移除是唯一彻底口径，且前端不渲染、零 UX 回归。
- **改动**：`CardService.LoadRuntimeAuditTrailAsync` routeDecision 分支删 `["decisionSnapshot"]`/`["candidateResults"]` 两行。动态插入分支已走 `SanitizeDynamicInsertContext` 白名单，不动。
- **范围边界（指出，本轮不做）**：`LoadPresentationSnapshotsAsync`（[:362-363](../../../src/STOTOP.Module.CardFlow/Services/CardService.cs:362)）亦把这两载荷塞进 `CurrentStageWorkView` presentation snapshot，但该路径仅当前 active 处理人可见（对该节点本就有权），暴露面与 AuditTrail 不同，留待后续评估。

### 9.3 测试（`dotnet test tests/STOTOP.Module.CardFlow.Tests --arch x64`，必须 x64 宿主）

- **#2 主测**（新增 `Approval/CardDetailAuditTrailRedactionTests.cs`）：仿 `CardDetailWorkViewContractTests` 造卡片 + 带敏感值的 `CfRouteDecisionSnapshot`，调 `GetByIdAsync`，断言 routeDecision 审计项 metadata 含 `edgeKey`/`fromStageKey`、不含 `decisionSnapshot`/`candidateResults`，且整条 `AuditTrail` 序列化后不出现原始敏感值。
- **#1 守护测**：详情 `result.InitialDataJson` 仍被脱敏返回（防下沉误丢详情行为）；列表无 `InitialDataJson` 由类型删除编译期保证。
- 回归：既有单测全绿 + 前端 `vite build` 兜底。
