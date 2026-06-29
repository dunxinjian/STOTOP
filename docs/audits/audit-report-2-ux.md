# CardFlow「流程定义设计器」UX 评估报告（5 视角合并 · 经落地核查）

## 一、总体结论

设计器在**底层能力**上相当完整——状态机画布、条件路由、动态加签、组件低代码、运行态预演、自动保存、模板克隆一应俱全。但从「让一个不懂状态机的快递财务管理员独立配出第一条审批流」这个**首次成功**标准看，它目前更像一台**面向实施工程师的专家工具**，而非面向业务管理员的引导式产品。

核心矛盾有三：

1. **能力面 > 表达面，且表达面有断点。** 运行时支持 7 种处理人策略 / 角色·组织链·明细汇总等条件维度，UI 只暴露其中一部分；而唯一的可视化条件构建器在 enum/人员/组织三类最常用分流上**值下拉是空的、选不出值**——这是任务级阻断。
2. **冻结期能力仍在前台占道。** 计划在阶段 3e 冻结的「卡片视图」步骤、「动态加签」编辑器、70+ 业务套件组件库，目前全部无开关地暴露，既加认知负荷又制造未来死入口，其中 cardView 还**硬锁住预演**。
3. **反馈系统不诚实/不可定位。** 自动保存 pill 永远显示「✓ 已保存」（脏标记从未生效）；发布校验把几十条错误拼成一条不可点的超长 toast；字段保存失败静默 return。用户「不知道存没存上、不知道哪里错了、不知道为什么没反应」。

### 四维成熟度小结

| 维度 | 评分 | 一句话 |
|---|---|---|
| 智能度 | ★★☆☆☆ | 只有被动校验（你配错了再告诉你），无模板分流首选、无字段驱动建议、无智能默认；且健康检查/预演对 amount 硬编码，泛化即失真。 |
| 上手难度 | ★★☆☆☆ | 新建直落空白 6 步向导、满屏黑话、模板路径发现性弱且克隆即断流，新手撞最高难度路径。 |
| 逻辑合理性 | ★★☆☆☆ | 预演门槛与发布校验口径自相矛盾、删节点不联动清理悬空引用、结构化字段被降级成文本运算符、设计器表达力 < 运行时。 |
| UI-UX 与丝滑度 | ★★☆☆☆ | 双 tab/双入口割裂、破坏性操作确认不一致、原生 confirm 与 ant 风格断裂、340+ 处裸 hex 违背令牌真源、假移动端预演。 |

### 缺陷分级计数（合并去重后共 19 项）

- 🔴 阻断：**1**（条件值空下拉）
- 🔴 高摩擦：**9**
- 🟡 中：**6**
- 🟢 低（打磨/智能化机会）：**3**

---

## 二、🔴 阻断 / 高摩擦（优先改）

### 1. 条件构建器 enum/人员/组织值控件是空下拉，条件根本选不出值
- **严重度**：🔴 阻断（5 视角同指认同一根因，取最高）
- **位置**：`web/src/components/cardflow/ConditionBuilder.vue:354-410`；根因数据管道：`FieldOption` 接口（ConditionBuilder.vue:7-11）缺 `options`，两个上游 `StageDefinitionEditor.vue:653-655`、`RouteRuleCardEditor.vue:21-27（designer/）` map 时剥掉 options；源字段 options 实存于 `types/cardflow.ts:895`
- **问题**：enum+in 多选、enum eq/neq 单选、user/org 三个 `a-select` 全部未绑 `:options`，运行态是永远空的下拉。组件被节点进入条件、路由条件、动态加签三处共用，影响面大。
- **用户影响**：凡按「费用类型=差旅」「组织属于华东」「按角色」分流（财务审批最典型）都配不出值，且不报错只是空白，极易误判系统坏了。任务在选值这步彻底卡死。
- **改进建议**：① `FieldOption` 增 `options?: string[]`；② 两个上游 map 透传 `field.options`；③ 三个 select 绑 `:options`。enum 可立即修通（选项 cardSchema 现成）；user/org 需接 `getUserList/getRoleList` 或组织树异步数据源，可拆为第二步。
- **工作量**：enum 部分=速赢；user/org=中

### 2. 自动保存 pill 假显「✓ 已保存」——markDirty 从未被调用
- **严重度**：🔴 高摩擦
- **位置**：`web/src/views/cardflow/FlowDefinitionEditPage.vue:216-231`（pill 渲染 :1740-1746）
- **问题**：页面只有本地 `watch(() => state, () => { dirty.value = true })`（:217），从不调用 `auto.markDirty()`，故 `auto.saveState` 始终停在初始 `'saved'`，`saveStateText` 里 `'dirty'` 分支是死代码，pill 持续显示绿勾，直到 30s 定时器 flush 才短暂变化。
- **用户影响**：刚改完字段/节点，状态明明是脏的却显示「已保存」，给出**错误的安全感**；用户据此切走（SPA 内导航不触发 beforeunload），新建未填 name+code 时根本不落库，真实丢草稿。这是「配完不知生效没」的反向版——以为存了其实没存。
- **改进建议**：deep watch 内并列调用 `auto.markDirty()`；或直接用 `auto.saveState` 驱动 pill、删掉冗余本地 dirty ref，统一单一真源；flush 成功回 `'saved'`。
- **工作量**：速赢

### 3. 发布校验把全部错误拼成一条不可点的超长 toast，无法按节点/字段定位
- **严重度**：🔴 高摩擦（3 视角同指认）
- **位置**：`web/src/views/cardflow/FlowDefinitionEditPage.vue:1556-1562`；错误源 `validateCardFlow2Config():1462-1514`；errors 红边仅 4 键 :1520-1523；步骤6 现成清单 :2602-2604
- **问题**：所有 msgs 用「；」拼成一条 `message.error`（:1557），随后只 `activeStep=3` 粗跳，且所有 cardFlow2 错误被统一塞进 `errors.stages`（:1553），步骤4 的 settings 错误也会误跳到步骤3 节点链。errors 红边只覆盖 basic/schema/stages/condition，步骤2/步骤4 无红边映射。
- **用户影响**：首次发布几乎必踩多条校验，面对挤满分号的 toast 看不全、点不动、几秒消失，不知哪个节点哪个字段错，反复试错，发布这一高频关键操作反馈极差。
- **改进建议**：把 msgs 改为结构化数组（含 step/stageId/fieldKey），复用步骤6 `previewConfigWarnings` 清单渲染成可停留、每条带「定位」按钮 →`activeStep`+`selectDesignerNode/selectDesignerEdge` 跳转高亮；toast 仅「发现 N 项问题，详见校验面板」；拆 error 维度补 cardView/settings 键给步骤2/4 红边。
- **工作量**：中（复用现成清单组件）

### 4. cardView「卡片视图」步骤即将冻结却仍作完整 wizard 步骤暴露，且硬锁预演
- **严重度**：🔴 高摩擦（视角1/2 同指认）
- **位置**：`web/src/views/cardflow/FlowDefinitionEditPage.vue:173`（STEPS）、:196-197（步骤徽章）、:277-284（previewReadinessItems 的 cardView 项 `ready: cardComponents.length>0`）、previewReady :299
- **问题**：计划阶段 3e 冻结、CardComponent 低代码层零使用的 cardView，被硬编码插在「字段设计」与「节点链」两个真正必填步骤之间；且预演就绪把它列为硬门槛——不拖出至少一个组件，预演面板永远「未就绪」。但发布校验 `validateForPublish` 只查 cardSchema 非空（:1529-1531），根本不看 cardComponents——**预演要、发布不要**，自相矛盾。
- **用户影响**：新手配完字段和节点想「预演看看对不对」，被要求先去一个本应冻结、对快递财务无用的低代码拖拽步骤硬凑组件，否则永远预演不了；而真要发布又不需要它，极易困惑卡住。
- **改进建议**：随 3e 用 feature flag 移除 cardView 步骤，previewReadinessItems 去掉 cardView 阻断项改「字段≥1 即可预演」（复用 buildPreviewComponentDefinitions :774 的 cardSchema 字段兜底），6 步降 5 步；暂不删则降级为「可选/高级」、徽章默认 finish、不进 previewReady。
- **工作量**：中

### 5. 新建无「模板 vs 空白」分流 + 模板克隆发现性弱且克隆后停列表，最易上手路径全程掉链子
- **严重度**：🔴 高摩擦（视角1 高摩擦×2，视角2 中×1，合并取高）
- **位置**：`web/src/views/cardflow/FlowDefinitionListPage.vue:444-449`（主色新建/从模板按钮）、:657-660（空态）、:346-356（handleCloneFromTemplate 只 loadData 停列表）、:679-693（模板弹窗仅 flowName+desc 平铺无预览/搜索）；设计器内部对模板零感知
- **问题**：两个新建入口都直落空白 6 步向导，「从模板创建」只是并排的非主色普通按钮、视觉权重低；模板弹窗无字段数/节点数/场景预览与搜索；克隆成功后停在列表（克隆项与模板**同名**、仅 flowCode 加时间戳后缀），用户还要自己找回再点编辑；设计器内无「从模板新建/另存为模板」就近入口。
- **用户影响**：不懂系统的管理员被默认推上「从零搭状态机」最难路径；即使发现模板，也在「找不到入口→看不出哪个合适→克隆完不知去哪」每个环节掉链子，大概率放弃模板退回空白。
- **改进建议**：① 把「新建流程」改为下拉/弹层分流「从模板开始 / 空白开始」，模板卡片为默认第一选择；② 模板项补字段数/节点数/简介与搜索（后端模板列表为空时优雅退化为仅空白）；③ `handleCloneFromTemplate` 成功后用返回的 `FlowDefinitionDto.Id`（cardflow.ts:117-122）直接 `router.push` 到新草稿 edit 页，克隆即编辑一气呵成。
- **工作量**：中

### 6. 删节点不联动清理路由/动态策略，悬空引用只在发布/预演时才暴露，且删节点无二次确认
- **严重度**：🔴 高摩擦（视角4 高摩擦 + 视角3 删节点无确认 中，合并取高）
- **位置**：`web/src/components/cardflow/StageDefinitionEditor.vue:197-205`（removeStage 仅 splice+emit）、删除按钮 :823-825 无 popconfirm；父页 `v-model="state.stages"` :2326 无回收 handler；悬空仅 `validateCardFlow2Config:1504-1506` 在发布/预演风险面板（previewConfigWarnings :1517）拦
- **问题**：删一个被路由引用的节点后，`state.routes` 的 from/to、`state.dynamicPolicies` 的 sourceStageKey 仍指向已不存在的 id，编辑现场零反馈零联动；删节点按钮直接删除无确认，而删条件/条件组却用了 a-popconfirm（ConditionBuilder.vue:282-296/427-435），破坏性操作口径不一致。
- **用户影响**：删中间审批节点后，画布条件边与加签策略变悬空脏数据，继续编辑/保存/预演都基于脏数据，直到点发布才被难定位的报错挡住；误删一个配好处理人/权限的节点无挽回提示（虽有 Ctrl+Z 但用户未必知道）。属「必须按隐含顺序否则配置坏」的陷阱。
- **改进建议**：父页对 stages 变化加 watch，自动剪除引用该 id 的 routes/dynamicPolicies 并 `message.info('已同步移除关联的 N 条流转/策略')`；removeStage 包 a-popconfirm，被引用时提示「将同时移除 N 条关联流转」（`state.routes.filter(r=>r.fromStageKey===id||r.toStageKey===id).length`）。
- **工作量**：中

### 7. 组件库 70+ 业务套件大量与快递财务无关，且组件/套件 tab 无搜索，信息密度严重过载
- **严重度**：🔴 高摩擦（视角2/5 同指认）
- **位置**：`web/src/components/cardflow/designer/CardComponentCatalog.vue:130-213`（SUITE_GROUPS 46~47 套件：假勤7/人事10/财税24~25/法务4/客户1）；搜索框仅在关联 tab 渲染 :531-534；共用滚动容器 max-height 640px :650；套件磁贴模板 :507-513 不渲染 badge
- **问题**：财税组塞了机票超标/改签/退票、火车票改签/退票，人事组塞了转正/离职/离职交接/调岗/入职/晋升等套件，对快递报销几乎全是噪音；组件 tab（基础/增强/业务/高级 5 分组数十项）与套件 tab（46+ 项）均无任何搜索/过滤，只能滚动逐组翻找；这些套件全为 publishable:false 的 deferred 能力却无任何「模板/暂缓」标识。
- **用户影响**：第一次进卡片视图就面对几十个 tile 的目录墙，找不到「报销套件/明细表格/金额」等少数有用项，且误以为「机票退票」等是本系统支持的业务，认知与决策成本极高，还易误拖不可发布的套件（发布才报错）。
- **改进建议**：① 组件/套件 tab 也加搜索框（复用关联 tab 实现）；② 按组织/行业做白名单或「常用」置顶，无关域默认折叠收起；③ 不可发布项默认折叠/灰显；或随步骤3 冻结一并下线套件 tab。
- **工作量**：中

### 8. 动态加签编辑器在每个人工/自动节点抽屉无条件展开，冻结零数据=认知噪音+未来死入口
- **严重度**：🔴 高摩擦
- **位置**：`web/src/views/cardflow/FlowDefinitionEditPage.vue:2692-2697`（DynamicApprovalPolicyEditor 无开关无折叠）；组件 `web/src/components/cardflow/designer/DynamicApprovalPolicyEditor.vue:19-26`（6 个 STRATEGY_OPTIONS）；渲染守卫是 `designerSelection.type==='node'`（auto 节点也展开）
- **问题**：节点抽屉里紧跟名称/类型/处理人无条件渲染整套企业级加签 IA（金额矩阵/组织链/角色/费用BP/字段人员/指定人员 + 触发时机/插入位置/兜底），无「启用加签」开关或折叠；代码里无任何冻结/隐藏痕迹。
- **用户影响**：配「主管审批→财务审批」2 节点直线流的用户，每点一个节点都被一整套加签矩阵糊脸，误以为必须配置；而这是计划冻结的能力，投入理解成本纯浪费。
- **改进建议**：用 a-collapse「高级：动态加签」可折叠区（默认收起、dynamicPolicies 为空时不展开）包住；冻结期直接 `v-if=featureFlag` 隐藏整块。
- **工作量**：速赢

### 9. 进入条件/路由只能引用卡片字段，运行时支持的角色/组织链/明细汇总维度在设计器里无法表达
- **严重度**：🔴 高摩擦
- **位置**：`web/src/components/cardflow/StageDefinitionEditor.vue:653-655`（conditionFields 只 map 卡片字段）、`RouteRuleCardEditor.vue:21-27`、运行时 `src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:161-189`（支持 roles.code/orgChain/detailSummary.*/initiator.*/initiatorOrg.*/relations.* 等，inorgchain :133/:299）；同缺口波及 `FlowGroupConnectionEditor.vue:87-88`、`DynamicApprovalPolicyEditor.vue:46-50`
- **问题**：设计器把运行时白白具备的上下文维度全藏起来，用户无法配出「发起人属于某角色」「组织属于某组织链」这类完全受支持的条件。
- **用户影响**：需按角色/组织层级分流（不同部门走不同审批链）的用户在 UI 里找不到这些字段，误以为系统不支持，转而用更笨的卡片字段硬凑或放弃；运行时能力被 UI 阉割。
- **改进建议**：在 ConditionBuilder 的 fields 追加一组「上下文/系统字段」分组（roles.code/orgChain/initiator.* 等），type 对应已支持运算符（org 已含 inOrgChain :96-102，值控件 :384-391 现成）；或提供「高级：手填字段路径」入口。建议把上下文字段集中到 ConditionBuilder 内部或共享 helper，统一修四个调用方。
- **工作量**：中

### 10. 处理人策略前端只暴露 4 种，运行时支持 7 种，组织链/金额矩阵/费用BP 主路径不可达
- **严重度**：🔴 高摩擦（原标中，但与第9条「表达力<运行时」同根因，且 orgChain 这类典型诉求主路径无入口，归入优先改）
- **位置**：`web/src/components/cardflow/StageDefinitionEditor.vue:112-117`（ASSIGNEE_STRATEGIES 仅 role/fixed/fieldUsers/initiator）、页面内联画布 assigneeStrategyOptions :343-348 同样 4 种；运行时 `ApproverResolver.cs:31-41,153-302`（含 orgChain/amountMatrix/feeTypeBp）
- **问题**：节点处理人下拉只有 4 项，orgChain（逐级上级）、amountMatrix（金额矩阵）、feeTypeBp（费用类型 BP）三种较强能力在常规节点编辑器配不出，只能通过将被冻结的动态加签层间接触达。
- **用户影响**：想配「逐级上级审批」或「按金额找不同审批人」的用户在处理人里找不到对应策略，要么以为不支持，要么被迫去用即将冻结的动态加签层，在两层间迷路。
- **改进建议**：把 orgChain（至少）提升为节点处理人一等选项，复用 ApproverResolver 的 startOrgId/stopOrgCode/maxLevels；amountMatrix/feeTypeBp 若归动态层，则在处理人说明里明确指引「按金额/费用类型分配请用动态策略」。
- **工作量**：中

---

## 三、🟡 中等摩擦

### 11. SchemaFieldEditor 字段 key 空/重复时静默 return，点「确定」无任何反馈
- **严重度**：🟡 中（4 视角同指认）
- **位置**：`web/src/components/cardflow/SchemaFieldEditor.vue:133-143`（:137 `if(!key)return`、:139 `if(dup)return`），确定按钮 :501 绑 commitEditor
- **问题**：两处裸 return，无 message、无表单标红、Drawer 不关。新建时 key 由 genKey 预填唯一值（:78-82），故触发面是「用户主动清空 key」或「手动改成重名」——属新手真实操作（想自定义编码、复制粘贴撞名）。
- **用户影响**：点「确定」毫无反应像按钮坏了，既不知是 key 没填还是重复，也不知改哪，反复点无果，卡在第2步字段设计。
- **改进建议**：两处 return 前加 `message.warning('请填写字段标识 key')` / `message.error('字段标识已存在，请换一个')`，并把 key 输入框置 `validateStatus='error'`+help、聚焦，保持 Drawer 打开；可顺带补 snake_case 正则校验（placeholder 写了但代码未校验）。
- **工作量**：速赢

### 12. 向导满屏黑话术语无随处可见的解释，术语门槛高
- **严重度**：🟡 中
- **位置**：`web/src/views/cardflow/FlowDefinitionEditPage.vue:1779-1788`（基本信息三模板字段）、:1834（schema-guide）、处理人策略真实标签 :2681「处理人策略」
- **问题**：基本信息要求填 snake_case 编码、编号模板 `EXP-{YYYYMMDD}-{SEQ}`、标题模板 `{initiator}的报销-{amount}元`，花括号占位符无「是什么、从哪来」说明；「节点链/节点权限/脱敏/处理人策略/fieldAccess/路由分支」等术语对从未接触状态机的管理员是连串黑话。字段设计顶部三段式引导条是亮点但仅此一处，基本信息三模板字段与节点链步骤顶部零等价解释。
- **用户影响**：管理员看不懂「编码为什么要 snake_case」「处理人策略/脱敏」指什么，要么瞎填要么卡住求助，不看文档难以独立完成。
- **改进建议**：① 编码/编号模板/标题模板加 tooltip + 「用名称生成编码」按钮（中文名→拼音 snake_case）；② 把三段式引导条复制到基本信息与节点链步骤顶部；③ 关键术语首次出现加 a-tooltip/helper。
- **工作量**：中

### 13. 结构化字段类型（科目/辅助核算/卡片引用/银行账户/凭证引用）被降级成文本运算符
- **严重度**：🟡 中
- **位置**：`web/src/components/cardflow/ConditionBuilder.vue:45-107`（operatorMap 仅 8 类）、:150-153（未知类型回退 operatorMap.text）、:412-420（默认 a-input）；类型源 `SchemaFieldEditor.vue:46-57`（12 种含 account/auxiliary/cardRef/bankAccount/voucherRef）；RuleHealthPanel.checkTypeMismatch :174-176 抓不到
- **问题**：对会计科目/卡片引用等结构化字段，UI 给出「包含/开头是」文本运算符并让填字符串，运行时 ConditionRuleEvaluator 按 dictionary 路径取值比较，语义与用户预期严重不符且无告警。DynamicApprovalPolicyEditor 复用 ConditionBuilder 同样受影响。
- **用户影响**：在含财务结构化字段的流程（费用报销 seed 模板正是）里，用户以为按科目/辅助核算分流，实际在对内部字符串做文本匹配，配出的规则运行时大概率不命中，隐蔽的逻辑不一致。
- **改进建议**：为 5 个结构化类型在 operatorMap 补 eq/neq/empty/notEmpty + 对应选择器值控件，或显式禁用作为条件字段并灰显说明；checkTypeMismatch 扩展为「结构化字段使用 contains/startsWith 文本运算符」告警。
- **工作量**：中

### 14. ConditionBuilder 值输入用 @change（blur/enter 才触发）而非 @update:value，输入中不同步易丢值
- **严重度**：🟡 中
- **位置**：`web/src/components/cardflow/ConditionBuilder.vue:340/390/419`（text/org-inOrgChain/默认 a-input 用 @change）；对照 select 与 RouteRuleCardEditor routeName 用 @update:value（RouteRuleCardEditor.vue:97）
- **问题**：a-input 的 @change 仅 blur/enter 触发，输入过程中 model 不更新、conditionSummary（:241）不实时刷新。注：a-input-number（:345-351）实际随输入即时触发，真正受影响的是 text/org 的 a-input。
- **用户影响**：敲完值不按回车直接点下一步/保存，最后输入可能未进 conditionJson，「看起来填了实际是空」。（注：不会触发「未配置流转条件」校验，因后者只判 conditionJson 整体非空，丢的是单值。）
- **改进建议**：text 类 a-input 改 @update:value 即时双向同步，与组件内 select、RouteRuleCardEditor 口径统一。
- **工作量**：速赢

### 15. PathPreview/规则健康对样例字段硬编码，与实际 cardSchema 脱节，泛化场景预演/检查失真
- **严重度**：🟡 中（视角3 中 + 视角5 低×2，合并取中）
- **位置**：`web/src/components/cardflow/designer/PathPreviewPanel.vue:13-48,92-110`（硬编码 amount/feeType/hasExpenseRequest/hasLoan/cardStatus + 写死费用类型枚举）；`RuleHealthPanel.vue:80-88`（规则重叠只识别 field==='amount' 且 gt/gte）、:21（stageKeys 计算却不用）；现成可复用 `FlowDefinitionEditPage.vue:634-655` previewSampleData/previewValueOf
- **问题**：预演样例字段与实际 cardSchema 完全独立，字段改名/换业务域即命不中真实条件边；规则重叠检测对非 amount/非 gt-gte 的边（按 feeType/enum 分流、lt/lte/eq/in 族）一律检测不到，给虚假安心；stageKeys 闲置印证它不做悬空节点检查。
- **用户影响**：依赖「规则健康/路径预演」自检的用户得到误导性绿色反馈——面板说「未发现重叠」但实际两条枚举边可能同时命中，预演通过≠配置正确。
- **改进建议**：预演样例表单按 cardSchema 动态生成（给 PathPreviewPanel 加 schema/sample prop，复用 previewValueOf，enum 取 field.options）；重叠检测改按 field+运算符族通用判定；用闲置 stageKeys 补「流转引用了不存在节点」健康项，把发布期校验提前到设计期。
- **工作量**：中（PathPreview 加 prop 复用现成逻辑）；重叠检测通用化偏结构性

### 16. 节点视图权限 StageComponentViewEditor 两处入口暴露同一配置
- **严重度**：🟡 中（视角2 低 / 视角5 中，合并取中并标注感知层风险）
- **位置**：`web/src/components/cardflow/StageDefinitionEditor.vue:1067-1072`（节点视图 tab）与 `web/src/views/cardflow/FlowDefinitionEditPage.vue:2699-2703`（流程图抽屉）
- **问题**：同一节点的 componentAccess 在「节点链 tab 属性面板」与「流程图 tab 点节点弹出的抽屉」两处可编辑。两者经 Vue 响应式读写同一 state 对象，无真实数据分叉风险，属感知层的「我刚改的怎么没了」疑虑。
- **用户影响**：用户在抽屉改了权限再到 tab 看到同一编辑器，不清楚以哪处为准，担心冲突/丢失，降低信任。
- **改进建议**：确定单一真源——组件视图权限只保留节点链 tab，流程图抽屉只留路径/健康预览并引导「详细权限去节点链 tab」；两处共存时至少加「与节点视图 tab 同步」提示。
- **工作量**：中

---

## 四、🟢 打磨与智能化机会

### 17. 节点链双 tab 把「建节点」与「连边」拆到两个面板，心智模型割裂
- **严重度**：🟢 低（视角2 低 + 视角3 中，核查发现流程图抽屉已承接处理人/视图权限/进入条件编辑，残存断裂仅「加节点入口只在节点链 tab」+ 两处处理人配置深浅不一，故取低）
- **位置**：`web/src/views/cardflow/FlowDefinitionEditPage.vue:2299-2338`（a-tabs 双视图）；流程图抽屉 :2632-2727；FlowStateCanvas 无 addStage，新增按钮在 `StageDefinitionEditor.vue:835/839`；画布空态文案「先在节点链中添加…」
- **问题**：新增节点入口只在「节点链」tab，画布上无「添加节点」入口，用户要切到节点链建好再切回流程图连边；且节点链 tab 处理人含角色/人员/字段/兜底细粒度（:937-988），画布抽屉处理人只到 strategy 选择层，能力深浅不一。
- **用户影响**：在空画布找不到怎么加节点（需自行领悟去另一个 tab），建一条带分支的多节点流程需多次切 tab，不连贯。
- **改进建议**：在 FlowStateCanvas 头部 actions 区（已有「添加条件边」:232）并排补「添加节点」下拉（人工/自动），emit 复用 addStage；或合并为左画布右属性单一布局。
- **工作量**：结构性

### 18. 设计器大面积裸 hex（340+ 处），违背令牌单一真源约定
- **严重度**：🟢 低（视觉一致性/可维护性，不阻断功能）
- **位置**：`web/src/views/cardflow/FlowDefinitionEditPage.vue:3144-3196`（196 处）+ designer/ 8 组件 147 处；典型必填星号 `#ef4444`(:1775)、画布 `#fff/#e5e7eb/#111827`、选中态 `rgba(31,111,95,.x)`、CardComponentCatalog 整文件 hex 且 badge 混用 `var(--color-warning-light)` 与裸 `#d46b08`、ConditionBuilder `#e8e8e8/#999`
- **问题**：列表页已用 var(--color-primary)/var(--text-1)，证明是漏改而非无令牌可用，且违反 stylelint 禁裸 hex 规则。
- **用户影响**：全局换主色/暗色/品牌色时设计器整片不跟随（曾有「改色被后端 DefaultConfigJson 覆盖致全站变色」同类风险），与系统其余页面视觉割裂。
- **改进建议**：危险色→var(--color-error/danger)、文本→var(--text-1/2/3)、面板底→var(--bg-card)、选择态绿系→var(--color-primary) 派生、网格/边框→中性边框令牌；vue-flow/ECharts 不解析 var() 处用映射真 hex。沿用项目已存在的语义令牌名（--color-info/--color-success/--color-warning-light），勿引入不存在的别名。可分文件批量替换。
- **工作量**：结构性（批量）

### 19. 运行态/预演卡片渲染硬编码 platform="pc"，缺移动端所见即所得
- **严重度**：🟢 低
- **位置**：`web/src/views/cardflow/FlowDefinitionEditPage.vue:1955`（画布）、:2256（运行态预览 modal）、:2558（步骤6 节点工作视图）三处 CardComponentRenderer 写死 pc；画布 surface 固定 375px :3145
- **问题**：运行态预览已有「展示态/处理态」切换（:2217-2236）和「节点视角」下拉，唯独无 PC/移动端切换；画布 375px 视觉像手机但渲染管线仍按 pc 走，是「假移动端」。
- **用户影响**：审批人多在手机端，但设计者无法在设计器里预览移动端真实呈现，配出的卡片到手机才发现排版/控件不合适，移动端 WYSIWYG 设计意图未落地。
- **改进建议**：把 platform 抽为单一响应式来源，在预览 modal 模式切换旁加 PC/移动端 a-segmented，统一驱动三处 + surface 宽度联动。
- **工作量**：中

### 离开未保存页面用原生 window.confirm，与 ant 风格断裂（附 · 低）
- **位置**：`web/src/views/cardflow/FlowDefinitionEditPage.vue:1667-1672`
- 原生 confirm 与全站 ant Modal.confirm 风格不一致，且只在点「返回」触发；useAutoSave 已注册 beforeunload（关页/刷新有原生拦截），真正缺守卫、会静默丢数据的是 SPA 内导航（点侧边栏、前进后退）。建议：换 ant Modal.confirm（注意改成 onOk 回调里 router.push，不能保持同步 if）；加 onBeforeRouteLeave 覆盖 SPA 内导航；新建态提示「先填名称+编码即可自动保存」。工作量：速赢。

---

## 五、速赢清单（建议本周做）

1. **自动保存 pill 修真**（:217 加 `auto.markDirty()` 或统一用 saveState 驱动）——消除「假已保存」丢草稿风险。
2. **条件构建器 enum 值下拉补 options**（FieldOption 加 options + 两上游透传 + enum select 绑定）——先打通最常用的枚举分流（user/org 接口联动可排到中等档）。
3. **SchemaFieldEditor 两处静默 return 加 message + 输入框红框**（:137/:139）。
4. **ConditionBuilder text/org 值输入改 @update:value**（:340/:390/:419）。
5. **动态加签编辑器包 a-collapse 默认收起 / 冻结期 v-if=featureFlag 隐藏**（:2692）。
6. **删节点加 a-popconfirm 二次确认**（StageDefinitionEditor.vue:823），文案与删条件对齐。
7. **离开页面 window.confirm 换 ant Modal.confirm + 加 onBeforeRouteLeave**（:1667）。
8. **基本信息编码/编号/标题模板加 tooltip + 「用名称生成编码」按钮**（:1779-1788）。

## 六、结构性改造（需排期）

1. **新建流程「模板 vs 空白」分流 + 克隆即进设计器**——把模板提升为新建一等首选，弹窗补预览/搜索，克隆后 router.push 到 edit。
2. **发布校验改结构化错误清单**——可停留、每条带「定位」按钮跳转高亮，拆 error 维度补步骤2/4 红边（复用步骤6 previewConfigWarnings）。
3. **冻结期能力统一收口**——cardView 步骤 feature flag 移除并解除预演硬门槛（6步降5步）；组件库 70+ 套件做白名单/常用置顶/搜索或随步骤3下线。
4. **删节点联动清理悬空引用**——父页 stages watch 自动剪除 routes/dynamicPolicies + message.info。
5. **设计器表达力对齐运行时**——ConditionBuilder 注入上下文字段分组（角色/组织链/明细汇总）；处理人策略至少补 orgChain 一等选项；结构化字段补正确运算符集。
6. **健康检查/预演泛化**——重叠检测按运算符族通用化、预演样例按 cardSchema 动态生成、补悬空节点健康项。
7. **节点链双 tab 合并/补画布加节点入口**；**移动端 platform 变量化预览**；**340+ 处裸 hex 收敛到令牌**。

---

## 七、回答用户四问

**Q1 是否更智能？** 结论：**尚未达到「更智能」标准，目前只有被动校验。** 理由：全文件 grep 推荐/建议/suggest/smart/preset 零命中；现有「智能」要素（RuleHealthPanel、previewReadinessItems、PathPreviewPanel）都是「你配错了再告诉你」的事后校验，且对 amount 字段硬编码、泛化即失真。缺少模板分流首选、字段驱动建议（如检测到 money 字段就建议金额分级路由）、智能默认、标题模板按字段联想 token 等主动智能。**改进后可达★★★★**（速赢2+结构性1/5/6 落地）。

**Q2 是否更易上手？** 结论：**目前对业务管理员上手门槛偏高。** 理由：新建直落空白 6 步向导（最难路径默认化）、最易上手的模板路径发现性弱且克隆即断流、满屏黑话术语无解释、字段保存失败静默无反馈。这些不是缺能力而是缺引导。**改进后可显著提升**——分流首选模板+克隆即编辑+术语 tooltip+静默反馈补齐这几项速赢/结构性，是上手体验的最高杠杆。

**Q3 逻辑是否更合理？** 结论：**存在多处逻辑自相矛盾与表达力缺口，合理性不足。** 理由：①预演门槛（要 cardComponents）与发布校验（只要 cardSchema）口径矛盾；②删节点不联动清理，悬空引用延迟到发布才暴露；③结构化字段被降级成文本运算符，配出「看似对、跑起来错」的规则；④设计器可配项 < 运行时能力（条件维度、处理人策略均被人为压窄）。这些会让「调流程的人」对系统行为产生错误心智模型。**修复优先级最高的是预演门槛对齐、删节点联动、表达力对齐三项。**

**Q4 UI-UX 是否合理且丝滑？** 结论：**控件规范与一致性达标线之下，丝滑度被多处割裂打断。** 理由：节点链双 tab/组件权限双入口造成视图往返；破坏性操作确认口径不一致（删条件要确认、删节点不要）；离开页面用原生 confirm 与 ant 风格断裂；340+ 处裸 hex 使设计器无法跟随全局换肤；画布是「假移动端」，审批人实际手机端形态无法预览。底层组件（ant + vue-flow）是规范的，问题集中在**编排层的一致性与令牌纪律**，多为速赢/批量可修。**改进后可达★★★★。**

