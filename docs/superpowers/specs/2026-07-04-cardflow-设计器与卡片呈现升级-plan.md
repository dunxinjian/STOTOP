# CardFlow 设计器与卡片呈现升级方案（节点设计 / 卡片组件 / 内容显示 / 预览）

日期：2026-07-04 ・ 状态：待拍板 ・ 产出方式：10 代理工作流（5 路摸底 + 3 视角设计 + 合成 + 完整性批判），批判修正已并入本稿。

范围锁定四块：流程节点设计器、卡片组件体系、卡片内容显示、预览功能。基线是当日工作树（含两轮大修未提交改动，见 `2026-06-16-cardflow-简化瘦身-design.md` 与阶段3路线图）。

---

## 一、现状诊断（要害摘录）

### P0（数据丢失 / 误导发布决策）
| # | 问题 | 依据 |
|---|------|------|
| 1 | 多明细表设计器打开即保存即丢表：parse 只取 default/首表，保存硬编码单表，无提示 | `cardflowSchema.ts:58-75`、`FlowDefinitionEditPage.vue:1087-1098` |
| 2 | file 字段无真实上传链路：File 对象经 JSON.stringify 后内容静默丢失，报销附件实际不存在 | `SchemaRenderer.vue:214-226/474-482`，api 无卡片附件端点 |
| 3 | 路径预演上下文缺明细数据：detailSummary.* 条件恒不命中、恒落默认分支，误导发布决策 | `CardFlowPathPreviewService.cs:253-271` vs 运行时 `ConditionEvaluationContextBuilder.cs:24-35` |

### P1（每天撞到 / 口径漂移 / 安全）
- **预览口径漂移**：卡片预览的组件运行态在前端复刻后端 `CardPresentationResolver`——脱敏字段设计期显示原值、聚合不算、visibilityCondition 不求值、无组件流程预览走伪组件而运行时走 SchemaRenderer 扁平回退（`FlowDefinitionEditPage.vue:634-871`）。
- **后端三套 NormalizeAccess 语义不一**：`CardPresentationResolver.cs:518-528` 大小写敏感且未知→readonly（`'Hidden'` 会被放行）；`CardRedactionService.cs:140-149` fail-closed（基准）；`StageViewProfileResolver` 散点 Equals。
- **非 default 表敏感列明文下发（安全）**：呈现运行时其实已支持 `{tables:[...]}` 多表，但 `StageViewProfileResolver.BuildDetailAccess` 只给 default 表建敏感列 baseline（`:127-154`），非 default 表敏感列取不到规则时按 readonly 明文下发行值。
- **运行时保存丢 detailTableKey（归并损坏）**：`CardFlowPanel.buildSavePayload` details 不含 detailTableKey，非 default 表行经 PC 保存全部落回 default 表；后端契约 `UpdateCardDetailRequest.DetailTableKey` 已就位（`Requests.cs:218-224`），前端漏传。`DetailTableComponent` edit 模式也不按 binding.detailTableKey 过滤行。
- **发布校验 100% 在前端**：后端对 cardSchemaJson.components 零校验，直调 API 可发布带 deferred 占位组件的流程；无 capabilityKey 的存量 aiAssist/serialNumber 组件回退到 publishable 兜底绕过前端门禁（`cardComponentCapabilities.ts:577-589`）。
- **"真声明假实现"组件面大**：imageList/signature/relationLookup 标 publishable 却是占位渲染；paymentInfo/invoiceStatus/budgetStatus/loanOffset 忽略全部已配置 props；9 个通讯录类 placeholderControl 编辑态渲染空选项列表无法输入。
- **组件视图只覆盖审批态**：CurrentStageWorkView 仅对 active 处理人 + v2 节点生成（`CardService.cs:266-289`），填单/PC详情/移动填单全部回退 legacy 字段渲染——设计器所见 ≠ 多数运行态所得。
- **移动端显示坏死**：只读渲染 user/org/account/auxiliary/bankAccount/voucherRef 六类显示 `[object Object]`（`SchemaRenderer.vue:96-109/537-566`）；edit 态四类财务字段整个不渲染（required 时永远无法提交）；明细 enum PC 全表无 options、移动 picker 恒空列。
- **user/org/cardRef 选择器四处 TODO 占位**；ConditionBuilder/PathPreviewPanel 手输数字 ID，条件路由实际"仅开发可用"。
- **节点配置双入口能力不对等**：画布抽屉是子集（改策略为"按角色"后无处选角色→必发布失败），全集在节点链 tab；同 from/to 两条条件边完全重叠无法点选第二条；动态加签编辑器 v-if=false 冻结但健康面板照常报错（报错无处修的死循环）。
- **阶段视图应用逻辑双份复制**：CardFlowPanel 与 MobileCardApprovalPage 各一份且 required 兜底语义不一致；StageWorkView.sections/summary 后端下发、前端零消费（节点摘要配了白配）。
- **预演维度缺失**：发起人填单视角 / 移动端形态 / 审批人解析（派给谁）/ 编辑态交互，四者设计期全部看不到；功能较全的运行态预览 modal 冻结在 v-show=false 后。

### 重要更正（相对既有记忆/清单）
- **3g 条件求值收敛已完成**（工作树）：模块 CLAUDE.md 明文 + grep 证实 FlowEngineService/StageRouteResolver/CardFlowPathPreviewService/FlowGroupService/OrchestrationEngineService 均已走 ConditionRuleEvaluator。visibilityCondition 求值今天即可复用它，不构成"第 6 套"——deferred 理由改为"需 B3 预览端点先行 + 独立拍板"，不再等一个已关闭的专项。
- **多明细表**语义修正：呈现运行时已支持 `{tables:[...]}`，是 resolver baseline 与设计器两处没跟上，非"整体不支持"。
- **aiAssist/serialNumber 漏拦截**已大部分修复（目录 seed 带 capabilityKey + 三重门禁），残余仅存量无 capabilityKey 数据的兜底漏洞。
- **退回 toSpecified**：目标节点由审批人运行时经 `request.TargetStageId` 现场选择（`FlowEngineService.cs:839-845`、`ReturnToStageRuntime.ResolveSpecifiedTarget`），**不是设计期配置**——设计器补"指定节点选择器"是伪需求，已否决。

---

## 二、方案总纲（三原则）

1. **后端干跑真值预览为地基**：新增 preview-presentation 端点，设计期预览 = 运行时真值，前端复刻逻辑整体删除。两个 resolver 均为纯内存函数无 DB 依赖，喂"草稿定义 + 样例数据"零障碍。
2. **单一事实源**：access 归一 / 字段格式化 / 节点配置面板 / 诊断规则 / 阶段视图应用，各收敛为一份实现。
3. **复用不新造（铁律）**：所有预览路径复用 CardComponentRenderer / SchemaRenderer + StageViewProfileResolver / CardPresentationResolver，不新写渲染分支。

节奏：安全与防丢打底（B1/B2）→ 预览可信度（B3/B4）→ 日常可用性（B5/B6/B7/B8）→ 结构性重构收尾（B9）。碰保存/发布/渲染语义的批次一律"先写护栏测试再动手"。引擎级改动不进本方案。

**前置纪律**：开工前先把当前工作树未提交改动 commit 落定，避免混批；后端改动保持加性，最小化与待合 stage4 分支的冲突面。

---

## 三、批次路线图

### B1 — 安全与数据防丢打底（无前置）
| 项 | 做法 | 后端 |
|---|------|------|
| 多明细表防丢透传 | parseDetailSchemaFields 保留 tables 元信息；buildDetailSchemaPayload 只替换 default 表、其余原样透传；编辑页顶部 alert 提示。**先写红测试**（多表 JSON load→save 往返 diff）再动保存链路 | 无 |
| 运行时保存补 detailTableKey（批判修正并入） | CardFlowPanel.buildSavePayload details 补传 detailTableKey（后端契约已就位）；DetailTableComponent edit 按 binding.detailTableKey 过滤行 | 无 |
| 非 default 表敏感列 baseline（安全） | 三形态明细 schema 读取抽到 CardSchemaReader.ReadDetailTables 公共入口；BuildDetailAccess 为每表生成 `{tableKey}.{col}` baseline、Sensitive→masked | StageViewProfileResolver + CardSchemaReader；xUnit 断言多表敏感列 masked |
| NormalizeAccess 收敛 | 新建 StageAccessLevels 静态类（fail-closed：未知→masked，忽略大小写），三处 resolver 改调；前端镜像 cardflowAccess.ts | 三处改调；回归脱敏全量用例 + dev 库存量大小写抽查 |
| capability 回退补特判 | resolveComponentCapability 补 aiAssist/serialNumber 两行特判，封死存量绕过 | 无 |

风险：碰保存链路（后端全删全建语义）与 resolve 链，护栏测试先红后绿；CardFlow.Tests 有 flaky 前科，判回归多跑 2-3 遍。

### B2 — 附件真实上传链路（与 B1 并行安全）
CardFileValue 值契约（{name,url,size,mimeType}，只存元数据）→ 新增 `POST /api/cardflow/cards/attachments`（multipart，带权限、组织隔离、token 授权下载，不留静态直链）→ SchemaRenderer PC customRequest / 移动 after-read 接线 + 只读端预览下载。费用报销闭环的硬前提。

### B3 — 预览真值地基（依赖 B1）
| 项 | 做法 |
|---|------|
| preview-path 补明细上下文 | 请求 DTO 增 Details（{DetailTableKey,DataJson,SortOrder}）；BuildPreviewContextAsync 灌入 ConditionContextInputs.DetailData——**限定复用 ConditionEvaluationContextBuilder 口径而非复刻** |
| 新增 preview-presentation 干跑端点 | `POST /definitions/{id}/draft-version/preview-presentation`，请求 {FlowVersionId?, StageKey, DataJson, Details, ViewerMode=assignee\|observer\|initiator}；版本回退照 preview-path 模式；new CfCard+内存明细喂两 resolver；observer/initiator 再过 CardRedactionService；响应复用 StageWorkViewDto（抽 StageWorkViewMapper）。visibilityCondition 与运行时一致地"同样不求值" |
| 前端切端点真值（两 commit） | 第一 commit 端点结果与本地复刻并行比对；确认一致后第二 commit 删除 FlowDefinitionEditPage.vue:634-871 整块复刻。渲染仍走 CardComponentRenderer 零改动 |
| 编辑页级样例数据面板 | PathPreviewPanel 内嵌样例表单提升为编辑页级侧栏（卡片字段 + 明细行，明细复用 CardDetailTable compact）；路径预演与呈现预览共用同一份数据 |

### B4 — 预览工作台：三视角×双设备（硬依赖 B3；与 B5 串行——SchemaRenderer/CardFlowPanel 文件面重叠）
- **CardWorkViewRenderer 装配层抽取**：从 CardFlowPanel 抽纯装配组件（components 非空走 CardComponentRenderer、否则 SchemaRenderer 扁平回退，判断上移），CardFlowPanel 与设计器预览共用——顺带修掉"预览伪组件 vs 运行时扁平回退"漂移。
- **分屏 + 三组切换**：左"样例数据+路径预演"右"卡片预览"；节点下拉 ×视角（处理人=assignee / 发起人填单=SchemaRenderer edit / 旁观者=observer 过脱敏）× 设备（pc / mobile：限宽 390px + platform='mobile'，Vant 已就绪）；500ms 防抖自动重刷。
- **预演审批人解析上屏（批判修正并入）**：preview-path 步骤 DTO 增加 ApproverResolver 干跑结果，显示"该节点将派给谁"。
- **预演步骤点击联动**卡片视图切到该节点（纯接线）。
- **冻结运行态 modal 删除**（edit 体验由"发起人填单"视角吸收）。

### B5 — 移动端与内容显示修复包（可与 B3 并行；与 B4 串行）
- formatFieldDisplayValue 单一真源（迁入 CardDetailTable 三个现成格式化函数），四消费点改调，根治 `[object Object]` 与 relation JSON.stringify 乱码。
- 移动 edit 财务四类字段只读兜底 +"请在 PC 端填写"引导（替代整个不渲染）。
- 明细 enum 双修 + options 支持 {label,value} 存码显名（兼容旧 string[]）。
- 性能小修：明细展开 max-height 2000px 截断改 grid-rows 方案；移动弹层 300→1 单实例；getFlowVersionDetail 内存缓存。
- MobileCardFillPage returned 状态收口为只读+引导原样重提（对齐后端契约，现状保存必 400）。

### B6 — 选择器接线包（排 B3/B4 后避免同文件 rebase 冲突）
useUserSearch/useOrgSearch composables（封装转交弹窗已验证的防抖搜索模式）→ SchemaRenderer 四处 TODO 清零（user/org/cardRef，cardRef 复用 CardRelationPicker）→ ConditionBuilder user/org 条件值 + PathPreviewPanel 发起人换选择器（值存 ID、缓存回显、历史裸 ID 降级显示）。业务可用性收益最高的单点。

### B7 — 发布门禁与组件真实度（无硬前置，与 B1 前端特判成对）
- **发布门禁后端镜像**：PublishAsync 增 ValidateCardComponents（componentStatus∈{deferred,template} 拒 / controlKind 命中静态拒绝集拒 / binding.source 越界拒），中文聚合报错走 InvalidOperationException→400。只镜像"拒绝面"，能力表真源留前端。**硬护栏：合入前对 dev 库全部存量定义 dry-run 零误报**。
- 六个业务组件吃满已配置 props（severity 配色走设计令牌，禁裸 hex）。
- 9 个通讯录占位项 + imageList/signature + **relationLookup 与 13 个关联 seed（批判修正：显式降级）** 标 componentStatus='deferred'（接 CardRelationPicker 的可编辑升级列为后续拍板项）；存量草稿给"移除或替换"引导。
- configSections inactive 元数据化：死旋钮（visibilityCondition/linkageGroup/statisticKey）标"暂未生效"Tag+说明；6 个无条件渲染分区补门控。visibilityCondition 文案改为"求值待独立立项（可复用已收敛的 ConditionRuleEvaluator），当前配置保存但运行时不生效"。
- 组件默认绑定按 supportedBindings+字段类型匹配（金额组件不再绑到文本字段）。

### B8 — 画布可用性与 schema 信封（与 B9 同文件面，勿并行）
- 平行边偏移（pathOptions offset）+ 节点抽屉"出边列表"兜底（含停用边）；实测不佳则 label 错位+边列表双保险。
- RouteRuleCardEditor 补路由停用开关 + failurePolicy 下拉（字段已走加载/保存链，纯补 UI；策略键只读核对引擎消费）。
- 退回 toSpecified 语义纠偏：只补说明文案（"审批人退回时现场选择目标节点"）+ 核对退回弹窗门控，**不建设计器选择器**。
- **CardSchemaV2 信封契约正式化**：后端补 Header 属性（消掉前端私约被反序列化吃掉的隐患）+ 各反序列化模型加 [JsonExtensionData] 透传规则；前端 types 定义 CardSchemaV2 接口。
- 画布布局持久化（flowSettingsJson.canvasLayout，依赖信封项先行）+ 拖动语义拆分（规则模式拖动只改布局不动 sortOrder；排序改显式操作）+"自动整理"按钮。
- 列表投影补 isTemplate/triggerConfigJson/accountSetId/matchPattern（DTO 属性已存在）。

### B9 — 结构性重构收尾（排 B8 后；useStageWorkView 待 B5 落稳）
- **StageConfigPanel 抽取**：节点链右栏 5-tab 抽成共用面板，画布抽屉内嵌 collapse 形态——消灭双入口能力差与"抽屉改出必发布失败配置"的断头路；同批：manual↔auto 切换走 ensureStageConfigDefaults、画布加"+节点"按钮、动态加签降级解冻为只读卡片+删除（解决报错无处修；完整编辑仍归 3e）。抽取零逻辑改动纯搬移，保住 undo 重定位与 editSeq。
- **useStageWorkView composable**：两端阶段视图应用逻辑合一，required 兜底统一（以后端语义为准），**先写两套旧实现对照快照测试、语义变化逐条拍板**再切换；同批首次消费 sections/summary 上屏（摘要条 + 字段分组）。
- **诊断合流**：RuleHealthPanel 与 validateCardFlow2Config 抽成 cardflowDiagnostics.ts 统一产出，每条可点击直达现场（切步骤+选中+开抽屉）；"规则重叠"从 amount+gt/gte 硬编码升级为通用区间相交 + enum 互斥。
- **组件注册表化**：capability 增 runtime/catalog 元数据，componentFor 硬编码 map 改查表分发，目录 seed 从能力表派生；配套 vitest 一致性门禁（publishable ⇒ 有真实现），机制化揪"真声明假实现"。

风险最高的一批：FlowDefinitionEditPage 2800+ 行巨石组件，与 undo/自动保存/串行化保存三机制交互面大——独立分支小步提交，每步 type-check。

---

## 四、依赖关系

```
B1 ──→ B3 ──→ B4 ──→ B6
 │                    ↑
 └─（并行）B2         │
B5（与B3并行，与B4串行，先于B9的useStageWorkView）
B7（独立；与B1特判成对；先于B9注册表化）
B8（信封项先行于布局持久化）──→ B9（同文件面串行）
```

## 五、显式否决清单
1. **设计器退回"指定节点"选择器**——引擎语义为运行时现场选择，建选择器是造死配置（已 grep 复核）。
2. **多明细表端到端完整支持**——牵动三宿主保存链路与后端全删全建语义，立独立专项；B1 只做防丢+baseline+detailTableKey 三个必要前置。
3. **visibilityCondition 求值**——可复用已收敛的 ConditionRuleEvaluator（3g 已完成，非等待项），但需 B3 预览端点先行否则设计期无法验证效果；B3 落地后独立立项拍板。
4. **预演遍历收编 StageRouteResolver**——当前双实现语义一致，引擎周边改动单独评估。
5. **运行时 GetByIdAsync 扩 observer/initiator 三档 WorkView**（详情/填单三态统一）——发起人档语义需产品拍板；B3 预览端点保留 viewerMode 三档纯加性，运行时档位仅标方向。**注意：这意味着本方案落地后"设计器所见 vs 发起人/旁观者运行态"双轨仍存在，是有意取舍。**
6. **卡片视图编排步整体解冻（桶B）**——待产品拍板；其四项修缮前提已拆入 B7 先行。
7. 契约生成门禁（swagger→openapi-typescript）、画布快捷键微调、移动端财务选择器可编辑版、版本 diff 补路由维度、抄送配置结构化——降级为后续候选。

## 六、验证纪律
- 每批：`npm run type-check` + `scripts/dev/test-dotnet.ps1 CardFlow`（flaky 需多跑判定）+ 涉及样式过 `lint:style`。
- B1/B9 行为变更批：护栏测试先红后绿 / 快照对照先固化现状。
- B7 发布门禁：dev 库全量 dry-run 零误报为合入硬条件。
- 预览手验用登录绕行配方（fetch /auth/login 写 stotop_* 键）；移动形态验证注意 preview_resize 不触发 window resize，抓 setupState recompute。
