# CardFlow 卡片流程 — 统一审计报告（按设计要求全面审视）

> 三个多智能体工作流并行产出、对抗式验证、跨工作流去重整合。日期 2026-06-17。
> 子报告：[①正确性](audit-report-1-correctness.md)（47 坐实）· [②设计器UX](audit-report-2-ux.md)（39 坐实）· [③a路由](audit-report-3a-routing.md)（16 项）· [③b卡片](audit-report-3b-card.md)（17 项）。

## 一句话总体结论

**核心架构方向是对的（事件驱动、人工/自动节点、表单即卡片、条件路由、分节点脱敏展示），但"最后一公里 + 单一真源"两件事没做完，导致设计意图在真实使用路径上多处未兑现，并叠加一批可触发的财务数据完整性、组织隔离/越权、卡片提交硬阻断与机密性缺陷。** 引擎内核（条件求值算子、状态机主线）基本健康，问题集中在它周围的"配置面—预演—发布—退回—撤销—脱敏—展示"各外环各写一份、互不收敛。

## 设计意图四维兑现度

| 设计意图 | 兑现度 | 关键缺口（交叉印证） |
|---|---|---|
| ① 事件/异常驱动 | **低** | 重复数据未被识别为异常静默入库；撤销不触发红冲；凭证去重键碰撞静默丢张；超时退化为一次性广播 |
| ② 顺序+条件流转，状态机可达终态 | **中下** | orsign 单人拒绝整卡退回、会签转办永久卡死；组织链/角色路由运行期恒 false；空条件变 catch-all；退回按 sortOrder 而非真实路由图；发布不查环 |
| ③ 表单即卡片流转（含移动端） | **中下** | v2 信封 schema 提交被硬阻断；移动端明细枚举无法录入；外推/委托/源上下文多处未通电 |
| ④ 分节点恰当展示 + 字段权限脱敏 | **中下** | 脱敏只在 active+v2 节点瞬时生效（完成/归档态全量明文）、按 schema denylist 漏脱 schema 外键；模板复用链被组织隔离打穿 |

## 缺陷规模

| 报告 | 坐实 | 去重 | 严重/P0 | 高/P1 |
|---|---|---|---|---|
| ① 正确性（11 维度） | 47 | 35 | 2 | 13 |
| ② 设计器 UX（5 视角） | 39 | 19 | 1 阻断 | 9 高摩擦 |
| ③a 节点路由 | — | 16 | 1 | 6 |
| ③b 卡片控件/展示 | — | 17 | 3 | 5 |

---

## 🔴 第一优先级 · 立刻止血（财务 / 安全 / 硬阻断）

| # | 缺陷 | 位置 | 来源 |
|---|---|---|---|
| 1 | **跨批次去重对所有种子规则静默失效** — 去重字段配 Excel 列名、DataTable 用 DB 列名，`Columns.Contains` 恒 false；又叠加 STG_出港运费**无唯一约束**，DB 兜底永不触发 → 重复财务行 100% 入取数源表 | [ExcelInputPlugin.cs:469](src/STOTOP.Module.CardFlow/AutoPlugin/Implementations/ExcelInputPlugin.cs:469)·[CardFlowSeeder.cs:571](src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs:571) | ① P0 |
| 2 | **v2 信封 schema 提交期硬阻断** — 设计器恒写 `{version:2,fields:[...]}`，但 `ValidateCardData` 仍按顶层数组反序列化 → JsonException → 提交报"Schema解析失败"。凡经 v2 设计器编排过字段的流程，卡片**根本提交不了**（阶段2 E2E 用数组形/直接 seed 才没暴露） | [CardSchemaService.cs:17](src/STOTOP.Module.CardFlow/Services/CardSchemaService.cs:17) | ③b 严重（新发现） |
| 3 | **脱敏只在 active+v2 节点生效** — 已完成/归档/草稿/v1/无活动节点查看一律回传原始 `FDataJson`，银行卡号/手机号原值直达浏览器；最该控权的归档态最敞开 | [CardService.cs:194](src/STOTOP.Module.CardFlow/Services/CardService.cs:194) | ①P1 + ③b严重（双印证） |
| 4 | **脱敏按 schema denylist 做减法** — 只遍历 schema 声明字段，`FDataJson` 里 schema 外的敏感键（导入/历史/扩展）即便标 hidden/masked 也漏脱、原值下发 | [StageViewProfileResolver.cs:163](src/STOTOP.Module.CardFlow/Services/StageViewProfileResolver.cs:163) | ③b 严重 |
| 5 | **组织链/角色路由上下文永不赋值** — 评估器读 OrgChain/RoleCodes/RoleNames/Relations 当真源，但构造器从不赋值 → 这类路由恒 false、静默走默认分支、零告警；前端 UI 还主动诱导配置（把死功能当活功能卖） | [ConditionEvaluationContextBuilder.cs:28](src/STOTOP.Module.CardFlow/Services/ConditionEvaluationContextBuilder.cs:28)·[ConditionRuleEvaluator.cs:161](src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:161) | ①P1 + ③a严重（双印证） |
| 6 | **模板克隆信任客户端 OrgId** — 任意登录用户可把流程定义克隆进无权组织（跨组织写越权） | [FlowDefinitionService.cs:945](src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:945) | ① P1 安全 |
| 7 | **GetTemplates 不限 FOrgId=0** — 组织绑定模板跨租户泄露（含角色/触发配置） | [FlowDefinitionService.cs:1019](src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:1019) | ① P1 |
| 8 | **save-as-template 全程不可用** — FillOrgId 把 FOrgId=0 盖成当前组织 → 唯一索引冲突 500（控制器 catch 不到） | [FlowDefinitionService.cs:1105](src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:1105) | ① P1 |
| 9 | **CardFlowBatchController 整类无权限网关 + GetRows 跨组织 IDOR** — 任一登录用户即可上传/改/确认/撤销批次、读他组织未脱敏原始明细 | [CardFlowBatchController.cs:18](src/STOTOP.Module.CardFlow/Controllers/CardFlowBatchController.cs:18)·[:185](src/STOTOP.Module.CardFlow/Controllers/CardFlowBatchController.cs:185) | ① P1×2 安全 |

---

## 🟠 第二优先级 · 高（功能正确性 / 财务 / 引擎 / 阻断式体验）

**财务凭证**
- 凭证创建无事务，AddAsync 逐条立即提交 → 中途失败留半截/不平衡脏凭证 [Repository.cs:28](src/STOTOP.Infrastructure/Repositories/Repository.cs:28)
- 自动凭证去重键不含 ruleGroupId → 多规则组同日第二组凭证静默丢失、金额不入账 [AutoVoucherHandler.cs:284](src/STOTOP.Module.CardFlow/Services/Handlers/AutoVoucherHandler.cs:284)
- 经导入工作台撤销批次不红冲凭证 → 财务残留、重复入账 [CfImportController.cs:1735](src/STOTOP.Module.CardFlow/Controllers/CfImportController.cs:1735)

**导入去重**
- 跨批次去重只比对前两个字段 → 配 3+ 去重字段时过度去重误删合法行；**派件量接入 spec 正要用三字段，下一个特性即触发** [ExcelInputPlugin.cs:586](src/STOTOP.Module.CardFlow/AutoPlugin/Implementations/ExcelInputPlugin.cs:586)

**审批引擎状态机**
- RejectAsync 不区分审批模式 → 或签单人拒绝即整卡退回；已写好的 `IsStageReturned` 全库零调用 [FlowEngineService.cs:701](src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:701)
- 会签节点转办/加签后永久卡死（残留 transferred 破坏 All(approved)，无自救路径） [ApprovalModeHandler.cs:20](src/STOTOP.Module.CardFlow/Services/ApprovalModeHandler.cs:20)

**节点路由（你重点强调）**
- 非默认规则的空条件 / 空条件组被当默认匹配 → 隐形最高优先级 catch-all，截胡所有真实分支 [ConditionRuleEvaluator.cs:13](src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:13)·[:73](src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:73)
- 退回目标/作废下游纯按 FSortOrder，与运行时按路由图选路是两套拓扑 → 非线性流程退回不可靠 [ReturnToStageRuntime.cs:26](src/STOTOP.Module.CardFlow/Services/ReturnToStageRuntime.cs:26)
- 发布校验不查环/不可达/终端无出边 → 非法环流程可发布上线（同仓 Orchestration 却查环，口径不对称） [FlowDefinitionService.cs:733](src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:733)
- 预演与运行时上下文三处分叉 + 预演表单写死五字段 → "预演通过 ≠ 上线走对" [CardFlowPathPreviewService.cs:255](src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:255)（①P2+③a高 双印证）
- 带 TypeErrors 的命中被跳过 → 类型笔误被吞、走错分支、优先级序被打乱 [StageRouteResolver.cs:81](src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs:81)

**卡片控件/展示（你重点强调）**
- 移动端明细 enum/select picker `columns` 写死空数组 → 移动端枚举列无法录入，明细录入链断裂 [CardDetailTable.vue:821](web/src/components/cardflow/CardDetailTable.vue:821)
- 字段直绑 enum 拖成组件后丢 options 且不在 choice 白名单 → 退化为纯文本框 [CardComponentCatalog.vue:380](web/src/components/cardflow/designer/CardComponentCatalog.vue:380)
- 低代码 components PC 详情页从不传 → 同配置移动可见、PC 不可见 [CardDetailPage.vue:493](web/src/views/cardflow/CardDetailPage.vue:493)
- 业务状态组件（预算/发票/付款/借款冲销/风险）配置项全是摆设、运行态恒显静态文案 [CardPresentationResolver.cs:151](src/STOTOP.Module.CardFlow/Services/CardPresentationResolver.cs:151)
- **扁平字段模型无脱敏位** → 若按计划冻结低代码、只留扁平渲染，意图④的脱敏将"无处可配、无处可渲染" [cardflow.ts:888](web/src/types/cardflow.ts:888)

**设计器 UX（阻断/高摩擦）**
- 条件构建器 enum/人员/组织的值下拉是空的、选不出值（最典型的财务分流配不出，还不报错） [ConditionBuilder.vue:354](web/src/components/cardflow/ConditionBuilder.vue:354)（②阻断+③a 双印证）
- 自动保存 pill 永远假显"✓已保存"（markDirty 从未调用）→ 误导切走、新建未填名+码真实丢草稿 [FlowDefinitionEditPage.vue:216](web/src/views/cardflow/FlowDefinitionEditPage.vue:216)
- 发布校验把几十条错误拼成一条不可点的超长 toast，无法按节点/字段定位 [FlowDefinitionEditPage.vue:1556](web/src/views/cardflow/FlowDefinitionEditPage.vue:1556)
- 新建直落空白 6 步向导，模板路径发现弱且"克隆即断流"（停列表、同名、找不回）

---

## 二、重点领域深判（你强调的两块）

### A. 节点路由 — 设计评判

**引擎内核健康，五个外环系统性脱节。** 求值算子（比较/类型/嵌套下钻/逻辑组）实现正确且有候选/快照/审计意识；但围绕它的「配置面—预演—发布—退回—审计」全部与运行时口径漂移：

- **「按条件送到对的下一节点」部分破坏**：四类静默错路由叠加——组织链/角色恒 false、空条件变 catch-all、类型错被吞走低优先级、退回按 sortOrder。条件能"送"，但在非线性/组织维度/误配场景下**送错且无声**。
- **「配置易上手」明显落空**：UI 暴露运行时不支持的 inOrgChain；最有价值的"按明细合计金额分流"在可视化里根本配不出；引用型字段降级字符串比。
- **「删改不悬空」有缺口**：发布门禁不查字段存在/类型/环/终端；克隆链静默搬运非法与 disabled 悬空配置。
- **「运行时与预览一致」系统性破坏**：这是该领域最集中的问题域，"配完即验"承诺对绝大多数真实流程不成立。

> **冻结决策结论**：#5 组织链/inOrgChain 标了"已知冻结"，但**这不是合理冻结，而是"UI 已上线、运行时未接通"的危险半成品**，必须**优先接通或下架并加禁用提示**，不应维持现状。

### B. 卡片控件与展示 — 设计评判 + 冻结决策结论

**方向正确，落地链在三处系统性失守：校验入口、脱敏触发模型、双套字段模型。**

- **意图③ 未兑现且有硬阻断**：v2 信封提交被硬阻断、移动端明细枚举无法录入、财务结构化字段在移动端编辑态消失——都落在"信息=表单"的录入/提交主链上。
- **意图④ 基本未做到**：脱敏三重结构性缺陷叠加（只在 active+v2 瞬时生效 / schema denylist 漏脱 / maskPattern 后端不读、两端漂移），最该控权的归档态反而最敞开 —— **脱敏退化为"审批进行中的临时遮罩"，不是稳定的字段级访问控制**。

> **低代码组件层冻结/删除/重设计的明确推荐**：
> 1. **冻结并隐藏**：5 类业务状态桩（预算/发票/付款/借款冲销/风险）+ 抽屉 businessStatus 段 —— 配置项运行态全零生效的虚假能力，**该冻**（多探针交叉坐实，与在途决策一致）。
> 2. **保留并修复**：字段直绑组件 + enum 选项/控件 —— 有真实运行价值，不该随低代码一起冻。
> 3. **冻结的硬前置条件**：执行"冻结低代码、只留扁平渲染"**之前，必须先把脱敏/分节点显隐能力下沉到 `SchemaFieldDefinition`+`SchemaRenderer`**，否则等于砍掉意图④唯一落地点，含 PII 卡片将明文下发。
> 4. **独立重设计（而非冻结）**：脱敏链整体 —— 根因不在低代码 UI，而在后端脱敏被绑死在"active+v2 节点 + schema denylist"这套错误模型上，须重构为"查看者维度 + 按 DataJson 实际键 allowlist + 适用任意状态"的稳定后置过滤层。

---

## 三、设计器 UX — 回答你的四问

| 问题 | 结论 | 一句话理由 |
|---|---|---|
| 更智能？ | **尚未达到（★★）** | 只有"配错了再告诉你"的被动校验；无模板分流首选/字段驱动建议/智能默认；健康检查与预演还对 amount 硬编码 |
| 更易上手？ | **门槛偏高（★★）** | 新建直落最难的空白 6 步向导、满屏黑话无解释、模板路径克隆即断流——缺的是引导不是能力 |
| 逻辑更合理？ | **多处自相矛盾（★★）** | 预演门槛要 cardComponents、发布只要 cardSchema；删节点不清理悬空引用；设计器可配项 < 运行时能力 |
| UI-UX 合理且丝滑？ | **达标线之下（★★）** | 双 tab/双入口往返、破坏性操作确认口径不一、原生 confirm 断裂、340+ 处裸 hex 无法跟随换肤、假移动端预览 |

---

## 四、贯穿性根因：单一真源缺失

绝大多数 P1/高问题指向同一病灶——**同一件事各写多份、互不收敛**：

- **求值上下文** 三份：`ConditionEvaluationContextBuilder`（运行时）vs `BuildPreviewContext`（预演）vs 线性兜底只塞 CardData → 预演≠运行时、兜底≠规则模式。
- **取值口径** 多份：求值器 `ResolveField` vs `AuditSnapshotPolicyService.ResolveField`（别名/嵌套下钻不一致）。
- **拓扑** 两套：正向 `StageRouteResolver`（路由图）vs 退回 `ReturnToStageRuntime`（sortOrder）。
- **决策快照** 两份：resolver 自带快照被丢弃、入库另起二次查库重算。
- **字段/选项/控件模型** 双套：`SchemaFieldDefinition`(string[]) vs `CardFieldDefinitionV2`/`FieldOption{label,value}` → enum label 分裂、PC/移动展示分裂。
- **脱敏 / 撤销 / 条件 evaluator** 各多份（与阶段 3c/3f/3g 收敛目标吻合）。

> 这印证了：**阶段 3f/3g 的"收敛重复"不只是清洁工作，它正在掩盖一批真 bug**。逐条打补丁会反复复发，应按"单一真源收敛"方向系统性整改。

---

## 五、修复路线图

**第 0 批 · 立刻止血（财务/安全/硬阻断，1–2 周）**
1. v2 信封 `ValidateCardData` 双形态解析（解卡片提交硬阻断）+ 回归测试。
2. 脱敏重构为"查看者维度 + DataJson 实际键 allowlist + 任意状态"后置过滤层（解 #3/#4 机密性 + maskPattern 两端统一）。
3. 跨批次去重：列名归一（Excel→DB）+ 关键 STG 表补唯一索引（解重复财务行）。
4. 组织隔离三连：克隆忽略客户端 OrgId、GetTemplates 加 `FOrgId==0`、save-as-template 修 FillOrgId 覆盖。
5. CardFlowBatchController 补权限网关 + GetRows 归属预检。
6. 组织链/角色路由：接通上下文或下架 UI + 禁用提示。

**第 1 批 · 财务与引擎正确性（2–4 周）**
- 凭证写入纳入事务；去重键纳入 ruleGroupId；撤销统一含红冲。
- 去重改全字段复合 key；orsign 退回判定调 `IsStageReturned`；会签完成/退回只看活跃集合。
- 路由：空条件→Matched=false + 发布校验对偶；退回基于 CfRouteDecisionSnapshot 真实路径；发布加图级校验（环/可达/终端）。

**第 2 批 · 单一真源收敛（结构性，配合阶段 3f/3g）**
- 抽共享 `BuildContext` 纯函数供预演/运行时/兜底共用；取值口径合一；退回与正向共用拓扑；决策快照单源。
- 字段/选项/控件模型合一为 `{label,value}` + 全局 `normalizeOptions`。

**速赢清单（设计器，本周可做）**：自动保存 pill 修真 · 条件构建器 enum 值下拉补 options · 字段保存失败静默 return 补提示 · 删节点加二次确认 · 动态加签编辑器折叠/冻结 · 离开页面换 ant Modal.confirm。

**冻结收口（配合阶段 3e，注意硬前置）**：cardView 步骤 feature flag 移除并解除预演硬门槛（6 步降 5 步）；5 类业务状态桩从目录移除；**但脱敏下沉必须先于冻结落地**。
