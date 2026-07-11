# CardFlow 设计器二期 M8-D · 定义级开关 — 设计

> 承接 kickoff `docs/superpowers/plans/2026-07-09-cardflow-M8-二期-kickoff.md`（拆批表 M8-D）。
> 上游已完成：M8-A/B/C 已 push `origin/master`，seeder 到 **V75**，HEAD `66cac0d`。
> 铁律沿用：引擎先→UI 后 / **不做假配置** / 后端全在 `STOTOP.Module.CardFlow` / 提交经 hook 门禁、不 push 等点头。

## 一、核实结论（三项引擎消费现状，子代理只读核实）

kickoff 表把 M8-D 记为三项「①审批人去重 ②允许发起人撤回 ③停用节点 skip」，并称「F4 发布设置三个二期灰行转真开关」。核实推翻了其中两处预设：

| 项 | kickoff 预设 | 核实实测 | 处置 |
|---|---|---|---|
| ① 审批人去重(定义级) | 无消费→占位 | 引擎确无定义级去重字段，**但**定义级设置有现成零 schema 载体 `CfFlowVersion.FFlowSettingsJson`（`rejectStrategy` 等同款），且引擎 `AssignStageHandlersAsync` 已加载该 JSON → **可真落地** | ✅ 真开关 |
| ② 允许发起人撤回(定义级) | 仅批次级 `RevokeBatchAsync`，无定义级→占位 | 需求文档看漏了**卡片级 `WithdrawAsync`**——"发起人撤回、回到 draft"的运行时**已完整落地、前后端接通**，唯一缺定义级 gate → **可真落地** | ✅ 真开关 |
| ③ 停用节点 skip | 三灰行第三行转真开关 | **三灰行第三行实为「允许加签/转交」，非「停用节点」**（kickoff line25 记忆错误）；前端**无任何停用节点占位**；规则/条件分支模式下"停用分支源节点跳向哪条下游"**语义有真实歧义**，首节点无条件进入、全停用会崩 → **不宜真落地** | ❌ 本批不做 |

**决策记录**（用户 2026-07-11 拍板，均取推荐项）：
1. **停用节点 skip = 本批不做**（守"不做假配置"；歧义大、前端无占位、需大量护栏）。留后续单独立项。
2. **撤回默认 = 缺失即允许**（保留现状——现状对发起人一律放开撤回；避免上线即回归）。
3. **落地路线 = 走 `FFlowSettingsJson`，零 schema**（与现有 flow-level 设置一致；**本批无需 V76 seeder**）。

**易混淆点（写清防再踩）**：
- 定义级去重 ≠ M8-C 件② 的**节点级** `CfStageDefinition.FSkipDuplicateApprover`（seeder V74）。本批是 `CfFlowVersion.FFlowSettingsJson` 里的**流程级 JSON 键**，与节点级 OR 叠加。
- 唯一叫"停用"的既有真开关是**条件出边(edge)停用**（`RouteRuleCardEditor.vue:153`，引擎 `StageRouteResolver.cs` 只取 `status=active` 的边），与"停用节点"无关。
- `RevokeBatchAsync`（批次级，作废导入批次）≠ `WithdrawAsync`（卡片级，发起人撤回回草稿）。本批只碰后者。

> 行号均为核实快照（HEAD `66cac0d`），实现前**以代码为准**。

## 二、范围

**做**：件① 审批人去重(定义级)、件② 允许发起人撤回(定义级)，两件各独立 commit。
**明确不碰**：
- `FlowDefinitionEditPage.vue` 发布设置 2802「允许加签/转交」灰行 —— 不在本批，保持占位。
- 停用节点 —— 不新增占位、不动引擎。
- 任何 EF 实体列 / seeder（V 编号）/ 请求 DTO 结构 —— 两件全走 `FFlowSettingsJson` 不透明 blob。

## 三、件① 审批人去重(定义级)

### 存储（零 schema）
- 前端 `FlowSettings` 接口（`FlowDefinitionEditPage.vue:112-122`）+ init（`:156-166`）加 `skipDuplicateApprover: boolean`。
- 随现有链路 round-trip：存 `state.settings`→`JSON.stringify`（`:1596`）→`FlowSettingsJson`（`Requests.cs:115`）→`CfFlowVersion.FFlowSettingsJson`（`FlowDefinitionService.cs:611/623`）；读 `:509`→`Responses.cs:46`→`Object.assign(state.settings, JSON.parse(...))`（`:1337`）。**不动 DTO 结构。**

### 引擎消费
- `AssignStageHandlersAsync`（`FlowEngineService.cs:3109`）在 `:3114-3117` 已按 `card.FFlowVersionId` 加载 `flowSettingsJson`。
- 仿 `GetResubmitStrategy(:1522)` 写解析取 `skipDuplicateApprover`（`bool?`，缺失/false=不启用）。
- 把 `:3141` 的 `if (stageDef.FSkipDuplicateApprover)` 放宽为 `if (stageDef.FSkipDuplicateApprover || flowLevelSkipDup)`。
- `:3148-3178` 的去重查询（剔除"本卡更早、不同、非作废节点已 approved/rejected 的人"）与"全剔空→auto-advance"逻辑 **完全复用**，不改。

### 叠加语义
定义级与节点级 **OR 合并**：定义级 ON ⇒ 全流程所有人工节点套用去重。二者同源（同一查询口径），语义一致不冲突——定义级是全局粗开关，节点级是单点细开关。

### 默认
新建流程默认 **OFF**（缺失=false=不去重，保留"现状无定义级去重"）。

### UI
`:2790-2797` 灰行：去 `is-deferred`/`disabled`/二期 tag，`a-switch` 绑 `v-model:checked="state.settings.skipDuplicateApprover"`，desc 改为诚实生效文案。

## 四、件② 允许发起人撤回(定义级)

### 存储（零 schema）
前端 `FlowSettings` 加 `allowInitiatorRevoke: boolean`，同件① 链路。

### 引擎 gate
- `WithdrawAsync`（`FlowEngineService.cs:1340-1414`）现对发起人/代提交人无条件放行（校验 `FInitiatorId/FAgentId`、`active`、当前节点无人已审 —— 全部**保留**）。
- 新增 gate：读**卡片锁定版本**的 `CfFlowVersion.FFlowSettingsJson`（与 `resubmitStrategy` 一致，避免中途改定义回溯影响在途卡片），解析 `allowInitiatorRevoke`（`bool?`）。
- **仅当显式 `== false` 时** `Fail("该流程不允许发起人撤回")`；`null`（存量缺失）与 `true` 放行 —— 落实"缺失即允许"。

### 默认
新建流程默认 **ON**（与撤回现状放开一致；想禁用才关）。注意与件① 默认方向相反，各自都对齐"保留现状"：去重现状=无→默认关；撤回现状=放开→默认开。

### 运行时按钮尊重开关
- 后端 gate 为权威兜底。为诚实体验，前端撤回按钮也尊重开关：**卡片详情响应 DTO 增加只读布尔标志**（如 `allowInitiatorRevoke`，从卡片锁定版本 json 解析，非 EF schema 改动）。
- `CardDetailPage.vue`（`canWithdraw:134`/`showToolbarWithdraw:166`）与移动 `CardFlowPanel.vue`（`:977/1552`）撤回按钮条件加 `&& allowInitiatorRevoke !== false`；缺失/true 照常显示，显式 false 隐藏。

### UI（定义级开关）
`:2807-2813` 灰行：去 `is-deferred`/`disabled`/二期 tag，`a-switch` 绑 `v-model:checked="state.settings.allowInitiatorRevoke"`，desc 改诚实文案。

## 五、测试（TDD 先红后绿，`test-dotnet cardflow`）

**件①**（xUnit，仿节点级去重既有测试）：
- 定义级 ON + 某人更早节点已审 → 新节点分配剔除此人。
- 定义级 ON + 全剔空 → auto-advance（复用 auto-decision 路径）。
- 定义级 OFF + 节点级 ON → 仍按节点级去重（**不回归 M8-C**）。
- 定义级 OFF + 节点级 OFF → 不去重。

**件②**：
- 开关缺失（存量）→ 发起人可撤回（保留现状）。
- 开关显式 false → 撤回被拒 `Fail`。
- 开关 true → 可撤回。
- 非发起人 → 仍拒（不回归原校验）。
- 读**卡片锁定版本** json（中途改定义不影响在途卡片）。

**前端**：`type-check` + `lint:style`(零裸 hex) 每件收尾必绿；若有对应 vitest 一并跑。

## 六、提交（各件独立 commit，经 hook 门禁，不 push 等点头）

1. `feat(cardflow): 审批人去重定义级开关(FFlowSettingsJson) + 引擎OR叠加节点级 (M8-D 件①)`
2. `feat(cardflow): 允许发起人撤回定义级开关 + WithdrawAsync gate (M8-D 件②)`

收口：子代理对抗性只读整体终审 + `test-dotnet cardflow` 全量回归。

## 七、风险与边界

- **件② 向后兼容**：gate 必须 `bool?` 三态（缺失/false/true），只拦显式 false。若误用 `bool` 把缺失当 false，存量流程立即失去撤回 —— 已由"缺失即允许"测试钉住。
- **件② 读版本而非定义**：必须读卡片 `FFlowVersionId` 锁定版本的 json，不能读 definition 当前草稿，否则在途卡片被回溯。
- **件① 顺序**：`:3111` 先跑 `TryApplyAutoDecisionAsync`，去重在其后，放宽 if 不影响该顺序。
- **零 schema 复核**：两件均不新增列，`build-filter cardflow` 编译 + `test-dotnet cardflow` 即可验证，无 baseline JSON / bin 副本坑。
- **停用节点 skip**：本批不做已记录在案，后续若立项须先解决"停用分支源节点"的路由歧义（建议前端禁止停用分支源/首/尾节点，仅限单下游/线性）。
