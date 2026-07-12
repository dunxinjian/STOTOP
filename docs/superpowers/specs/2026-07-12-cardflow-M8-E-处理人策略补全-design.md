# CardFlow M8-E 处理人策略补全 · 设计

> 承接 `docs/superpowers/plans/2026-07-09-cardflow-M8-二期-kickoff.md` 拆批表 **M8-E**。
> 目标：`IApproverResolver` 补三种处理人策略，前端 `ASSIGNEE_STRATEGIES` 5→8。
> 三决策已拍板：连续多级主管=**直属上级链**(非 orgChain 复用)、发起人自选=**全链路真做**、上一节点处理人=**可配 sourceStageKey + 默认最近完成**。全部真实现、无占位（守"不做假配置"）。
> 模块锁：`STOTOP.Module.CardFlow`（`build-filter cardflow` / `test-dotnet CardFlow`）。不新增 cc FType。不 push 等点头。

## 0. 现状核实结论（三子代理只读核实，作为设计依据）

- **resolver 体系**：单接口 `IApproverResolver` + 唯一实现 `ApproverResolver`，`ResolveAsync` 内按 `stageDefinition.FAssigneeStrategy` 做 `strategy switch`（`ApproverResolver.cs:31-41`，字符串魔法值，非 DI 工厂）。现 7 标识：`fixedUsers/role/fieldUsers/orgChain/amountMatrix/feeTypeBp/initiator`。**resolver 已注入 `STOTOPDbContext`，可自查库**；入参含整张 `card`（`FID/FInitiatorId/FCurrentRound/FOrgId` 等）。
- **策略存储**：`CfStageDefinition.FAssigneeStrategy`（列 `F处理人策略`，`HasMaxLength(30)`）+ `FAssigneeConfigJson`（列 `F处理人配置JSON`）。**非** `FStageConfigJson`。
- **节点稳定标识**：`CfStageDefinition.FStageKey`（string，保存强制非空+唯一 `EnsureStageKey`）；跨节点引用（路由/动态策略）一律用 string key；前端草稿 `stage.id='stg_xxx'` 保存后**即成为持久 `FStageKey`（无回填漂移）**。→ nodeAssignments 的 key 与 prevStage 的 sourceStageKey 都用 string `stageKey`。
- **发起提交路径**：`CardService.CreateAsync` 只建 draft（不分派）；提交 `FlowEngineService.SubmitAsync:416` → `AssignStageHandlersAsync:514/3117` → `_approverResolver.ResolveAsync:3126`。运行时发起入口 `web/src/views/workhub/InitiatePage.vue` → `createCard`（空草稿）→ `CardFlowPanel`（`mode='fill'`）填表；提交两步：`updateCard(id,{dataJson,details})` → `submitCard(id)`（**无 body**）。
- **kickoff 旧假设已推翻**：所谓"发起抽屉 B1 说明弹层占位"过时——M8-A 已把发起路径做成真的，运行时发起 UI 与 `CardFlowPanel` fill 宿主真实存在，`UserSelect.vue` 选人器现成可复用。→ initiatorSelect 有真实宿主，不需新造发起 UI。

## 1. 贯穿全批的硬约束

### 1.1 策略名大小写归一化（硬坑，每 commit 必守）
保存侧 `FlowDefinitionService.NormalizeAssigneeStrategy`(`:715-724`) 对未列举策略 `ToLowerInvariant()` **强制小写**；resolver 侧 `ApproverResolver.NormalizeStrategy`(`:418-434`) 为**大小写敏感 switch**。三新策略均 camelCase，**必须三处各补显式 case 保住规范大小写**：
1. `FlowDefinitionService.NormalizeAssigneeStrategy`（保存归一）
2. `ApproverResolver.ResolveAsync` switch(`:31-41`) + `NormalizeStrategy`(`:418`)（解析分派）
3. 前端 `stageDefinitionShared.ts normalizeAssigneeStrategy`(`:38`)
否则存 `initiatorSelect` → 落库 `initiatorselect` → resolver 匹配失败 → `"不支持的处理人策略"`。**存→取 round-trip 用例是每策略的必备回归**。

### 1.2 摘要/健康/标签
`ASSIGNEE_STRATEGY_LABELS`(`stageDefinitionShared.ts:65-70`) 现**连 orgChain 都缺**——补齐 `orgChain` + 三新项，否则竖图摘要显裸 value。`formatAssigneeSummary` / `getStageHealth` 补三新策略分支。

### 1.3 动态策略白名单
三者**暂不**加入 `DynamicStagePolicyResolver.SupportedStrategies`(`:13-21`)——它们是静态节点配置策略，静态解析路径（`ResolveAsync` switch）不受白名单约束；prevStage/initiatorSelect 在动态插节点(加签)语境语义歧义（无前驱/无预选）。守 YAGNI，后续需要再放开。

### 1.4 前端下拉占位透传（本批不用但记录）
现 ASSIGNEE 下拉渲染(`StageConfigPanel.vue:945`)只 `map(value,label)`；本批三项均真实现（非灰置），故**不需**补 disabled 透传。占位范式（`CUSTOM_ACTION_HANDLER_OPTIONS` disabled）留档备用。

## 2. 策略① `superiorChain`（连续多级主管 = 直属上级链）

- **语义**：从**发起人**(`initiatorId`)起，沿 `SysUserOrganization.FDirectSuperiorId` 逐级向上取 N 级直属上级，产出**有序列表** `[L1,L2,…,LN]`（串行/并行由节点 `FApprovalMode` 决定，与策略正交，镜像 orgChain 的产出形态）。
- **纯直属上级、不带组织负责人兜底**——刻意区别于 orgChain（组织负责人链），避免语义污染；某级无 `FDirectSuperiorId` 即止，空集交节点既有 `fallback` 配置（`ApplyFallbackAsync:304`）。**真源=`SysUserOrganization.FDirectSuperiorId`（决策 B：个人直属上级唯一写侧真源；`SysAppointment.FDirectSuperiorId` 已废弃，勿读）**。
- **停用过滤（对齐 org-review 缺陷 [15]/[5] 方向）**：逐级取上级时过滤停用用户（`SysUser.FStatus==1`）——遇停用上级**跳过但穿透**（取其上级继续上溯），不是截断也不是把停用者当审批人。
- **落点（决策：就地实现于 ApproverResolver）**：新增私有 `ResolveSuperiorChainAsync`，用已注入的 `STOTOPDbContext` 查 `SysUserOrganization`（当前生效主任职行的 `FDirectSuperiorId`），`visited` 防环、`maxLevels` 上限、`FStatus==1` 过滤。**不动 FlowEngineService**（避开中心大文件并发协调风险；超时升级的 `ResolveSuperiorUserAsync:1175` 语义带组织兜底、不同）；加注释交叉引用 `ResolveSuperiorUserAsync` 留待未来 DRY。
- **config**：`{ "maxLevels": N }`（1–20，默认 5）。
- **前端**：`maxLevels` 数字框，复用 orgChain 的 `a-input-number` 范式（`StageConfigPanel.vue:994-998`），新 `editSuperiorMaxLevels` ref + `buildAssigneeConfig`/`rehydrateSelection` 分支。
- **数据依赖注记**：`FDirectSuperiorId` baseline 零种子（同 orgChain 现状）——引擎真消费该列，未维护则解析空走 fallback，属"真功能+待填数据"，**非假配置**。

## 3. 策略② `prevStage`（上一节点处理人指定）

- **语义**：取**来源节点已通过(approved)** 的处理人作为本节点处理人。
- **config**：`{ "sourceStageKey"?: string }`——显式给则取该 stageKey 节点；**缺省=按 `FCompletedTime` 最近完成的人工节点**（排除当前节点、排除 auto 节点）。
- **落点**：ApproverResolver 内查 `CfStageInstance`（本卡 `FCardId==card.FID`）：
  - sourceStageKey 显式：经 `FStageKey`+同 `FFlowVersionId` 解析出来源节点 FID → 该节点在本卡的实例（多轮取最新完成 `FRound`）。
  - 缺省：`FStatus=completed` 且非当前节点、join `CfStageDefinition` 过滤人工(`FType!='auto'`)，`FCompletedTime desc` 取首。
  - 再读 `CfStageAssignee.FUserId where FStageInstanceId in(…) and FStatus='approved'`，排除 rejected/cancelled。镜像 `FlowEngineService:3157-3166` 现成 join。
- **前端**：`sourceStageKey` 可选下拉（选项=本流程其它人工节点，`value=stage.id`=稳定 `FStageKey`；留空=最近完成）。新 `editPrevSourceStageKey` ref + 分支。设计器已持有全部 stages，选项就地取。

## 4. 策略③ `initiatorSelect`（发起人自选 · 全链路真做）

- **持久化（决策：新列，非 FDataJson）**：`CfCard` 新增 `FInitiatorAssignmentsJson`（列 `F发起人指定处理人JSON`），存 `{ "<stageKey>": [{userId,userName}] }`。
  - *不塞 FDataJson*：fill 提交全量替换 dataJson+明细、schema 字段清理会污染；发起人指派不应混入表单数据。
  - **走版本化 seeder 建列**（本批唯一 schema 变更）——**执行时先查 `CardFlowSeeder.cs` 实际末版本 + SYS 迁移历史再定 V 号，勿硬编**（现约 V78，下一个约 V79，以代码为准）。原生 DDL 用 `SeederHelper.ExecuteRawSql`。
- **DTO/服务**：`UpdateCardRequest`(`Requests.cs:263-268`) 加 `InitiatorAssignmentsJson`（照抄 M8-A `ActualInitiatorId`→`FAgentId` DTO 加字段范式）；`CardService.UpdateAsync`(`:873-955`) 赋值该列。两步提交天然承接。
- **resolver**：`initiatorSelect` 分支读 `card.FInitiatorAssignmentsJson`，按 `stageDefinition.FStageKey` 取选人 → 复用 `NormalizeUserIds`(`:677`)；空→`ApplyFallbackAsync`/fail-closed。
- **发起端选人器（宿主 `CardFlowPanel` fill）**：`loadCardDetail:621` 已取 `getFlowVersionDetail`（`ver.stages` 含 stageKey+assigneeStrategy，现被丢弃）——改为消费 stages，筛 `assigneeStrategy==='initiatorSelect'` 的**全部**节点（条件路由不可预知激活，出超集），每节点渲染 `UserSelect`(多选)，绑进 `initiatorAssignments` map，纳入 `buildSavePayload`(`:1109-1125`) → `updateCard`。
  - 运行时 resolver 只取被激活节点那份；激活但未选→fail-closed 兜底；动态加签节点无预选=预期豁免。
- **设计器**：下拉加 `initiatorSelect`，**无附属配置**（选择发生在发起时，同 initiator 无 config）。

## 5. 测试（TDD 先红后绿）

### 后端 xUnit（`test-dotnet CardFlow`）
- **superiorChain**：逐级解析产出有序链 / `maxLevels` 上限截断 / `visited` 防环 / 空 `FDirectSuperiorId` → fallback。
- **prevStage**：显式 sourceStageKey 命中 / 缺省取最近完成人工节点 / 排除 rejected·cancelled·auto / 多轮取最新完成轮 / 来源节点无 approved → fallback。
- **initiatorSelect**：resolver 按 stageKey 取人 / 未选→fallback / `CardService.UpdateAsync` 持久化 `FInitiatorAssignmentsJson`。
- **归一化 round-trip（钉 §1.1 硬坑）**：三策略存 camelCase → 经保存归一 → resolver 仍正确分派（不落 `"不支持的处理人策略"`）。
- InMemory 说明：resolver 走 LINQ 可测；seeder DDL 不在单测覆盖（dev 运行时验证）。CardFlow.Tests 偏重/flaky，判绿多跑。

### 前端 vitest（`stageDefinitionShared.spec.ts`）
- `ASSIGNEE_STRATEGY_LABELS` 覆盖 orgChain + 三新项；`normalizeAssigneeStrategy` 认三新项；`formatAssigneeSummary` 三新策略摘要；`getStageHealth` 三新策略健康分支。

## 6. Commit 拆分（各自独立可发布，均经 hook 编译门禁，不 push）

1. **superiorChain**（零 schema）：resolver `ResolveSuperiorChainAsync` + 归一化三点 + 前端下拉/label/health/maxLevels UI + xUnit/vitest。
2. **prevStage**（零 schema）：resolver 上一节点查询 + 归一化 + 前端下拉/sourceStageKey UI + 测试。
3. **initiatorSelect**（含 seeder 新列）：seeder 建列(查末版本定 V 号) + `CfCard` 实体/`CfCardConfiguration` 映射 + `UpdateCardRequest` + `CardService.UpdateAsync` + resolver + 归一化 + `CardFlowPanel` fill 选人器 + 测试。

建议顺序：先两个零 schema（1、2），末做 initiatorSelect（3，面最大）。批收口做整体终审（子代理对抗性只读）+ 回归。

## 7. 明确不做（YAGNI 边界）

- 不抽取 `ResolveSuperiorUserAsync` 到共享服务（就地实现）。
- superiorChain 不带组织负责人兜底（区别 orgChain）。
- 三新策略不进 `DynamicStagePolicyResolver` 白名单。
- initiatorSelect 设计器侧不做候选范围约束（发起人可选组织内任意活跃用户）；不塞 FDataJson。
- 不新增 cc FType；不动 FlowEngineService 核心引擎逻辑（仅 resolver + 提交/更新链既有扩展点）。

## 8. 并发协调（同日并行工作流）

工作树现存三份同日(2026-07-12)并行计划文档（未跟踪）：`org-manager-fixes`、`cardflow-empty-draft-and-brand-flow-fixes`、`single-tenant-extraction`。其中 **`org-manager-fixes` 第 1 批大修 `ApproverResolver.cs`(orgChain 解析 153-192) + `FlowEngineService.cs`(超时升级)**，与本批**同文件**。

- **碰撞面最小化**：本批对 `ApproverResolver.cs` 的改动全部**增量**——只在 switch(31-41) 末尾追加 3 个 case、`NormalizeStrategy`(418) 追加 3 个别名、新增 3 个私有方法；**绝不改 orgChain 现有解析行**（那是第 1 批的领地）。合并时只在 switch/NormalizeStrategy 处做小块并入。
- **设计对齐**：本批 superiorChain 与第 1 批的 orgChain 修复**互补不重叠**（决策 B 两线并存）；superiorChain 的停用过滤方向与缺陷 [15] 一致。
- **执行前重查**：每个 commit 前 `git status` 确认无并发 M 冲突；若第 1 批已先落地改了 switch，rebase/手动并入我的 case。
