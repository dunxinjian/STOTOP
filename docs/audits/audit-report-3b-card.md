# CardFlow 卡片控件与展示设计 — 对抗式验证缺陷报告

> 一句话结论：低代码卡片体系在**运行态校验链与脱敏链**上存在三处“严重”级正确性/机密性缺陷（v2 信封提交被硬阻断、脱敏只在 active+v2 节点瞬时生效、脱敏按白名单做减法漏过 schema 外敏感键），叠加扁平/低代码两套字段·选项·控件模型长期漂移；当前代码**未能稳定兑现意图③（信息=表单流转）与意图④（按节点恰当脱敏展示）**。

分级计数（去重后 17 条）：
- 严重：3（其中 2 条为机密性/数据完整性，1 条为提交硬阻断）
- 高：5
- 中：8
- 低：1
- 按类别：正确性/完整性 bug 9 · 设计缺陷 3 · UX 摩擦 2 · 一致性漂移 3 · 智能化机会 0（本轮无坐实项）

---

## 一、严重

### 1. v2 卡片 schema（Object 信封）在提交期 ValidateCardData 反序列化即失败，硬阻断卡片提交
- 严重度：严重 ｜ 类别：正确性 bug
- 位置：`src/STOTOP.Module.CardFlow/Services/CardSchemaService.cs:17-27`（调用方 `Services/FlowEngineService.cs:440-444`）
- 问题：设计器保存的 `FCardSchemaJson` 恒为 v2 Object 信封 `{version:2,fields:[],components:[],header:{}}`（`FlowDefinitionEditPage.vue:1042-1049` → `:1342` JSON.stringify 写入），但 `ValidateCardData` 仍按顶层数组反序列化 `JsonSerializer.Deserialize<List<SchemaFieldDefinition>>(schemaJson,...)`（已亲验 :20）。Object 根反序列化为 `List<T>` 抛 `JsonException` → 落入裸 `catch`（:23-26）返回 `IsValid=false`。`FlowEngineService` 据此 `return CardOperationResult.Fail("数据校验失败: Schema定义解析失败")`。
- 影响：凡经 v2 设计器编排过卡片字段（schema 非空）的流程，卡片提交一律失败、且必填/类型校验整体失效（异常在解析阶段，到不了逐字段校验）。这是**硬阻断**而非旧 MEMORY 所记“静默跳过”。同文件 `ReadFieldKeys`/前端 `parseCardSchemaPayload`（cardflowSchema.ts:32-48）早已双形态兼容，唯独本校验入口未跟上。
- 建议：`ValidateCardData` 复用与 `ReadFieldKeys` 一致的形态判定——先 `JsonDocument.Parse` 看根 `ValueKind`，Array 走 `List<SchemaFieldDefinition>`，Object 取 `fields` 子数组；或直接反序列化 `CardSchemaV2` 读 `.Fields`，顺手消除 `SchemaFieldDefinition` 这套前后端旁路模型。补 v2 信封提交校验回归测试。
- 置信度：high

### 2. 脱敏只在 active+v2 节点生效，已完成/草稿/v1/无活动节点查看路径返回原始未脱敏 DataJson
- 严重度：严重 ｜ 类别：数据完整性/机密性
- 位置：`src/STOTOP.Module.CardFlow/Services/CardService.cs:194-228`（实际方法名 `GetByIdAsync`；详情页 `web/src/views/cardflow/CardDetailPage.vue:72-79`、`:493-499`）
- 问题：仅当 `currentStage?.FStageDefinitionId != null`（:195）且 `normalizedConfig.Version == 2`（:204）时才把 `detail.DataJson = resolved.RedactedDataJson`（:219）；否则（已完成无 active stage、草稿、节点仍 v1、无 active 节点）整段不执行，`detail.DataJson` 保持实体直读的全量 JSON。PC 详情页 `JSON.parse(card.value.dataJson)` 直出。另：`operatorId/userId` 虽传入 Resolve 却全程未用——脱敏按节点配置而非查看者角色。验证者补充：该接口连卡片级访问权限校验都未见，暴露面更宽。
- 影响：`payeeAccountNo` 等 masked/hidden 敏感字段在**完成/归档态**（最该控权）对所有有详情访问权者原样可见；脱敏退化为“审批进行中的临时遮罩”，不是稳定的字段级访问控制。
- 建议：把字段可见性/脱敏提升为详情接口的稳定后置过滤层，对任意状态按“查看者在该卡片的权限”计算红版；引入 `operatorId` 区分审批人/经办人/旁观者；完成态用末节点或流程级默认视图档案兜底。
- 置信度：high

### 3. 脱敏只遍历 schema 内声明字段（denylist），DataJson 中 schema 外的敏感键（导入/历史/扩展）原值下发
- 严重度：严重 ｜ 类别：数据完整性/机密性
- 位置：`src/STOTOP.Module.CardFlow/Services/StageViewProfileResolver.cs:163-179`（白名单来源 :44-45、:85-105；下发 `CardService.cs:219`）
- 问题：`RedactJson`/`RedactDetailJson` 仅遍历 `fieldAccess` 的键做删/掩（已亲验 :166-178、:188-204），而 `fieldAccess` 由 `BuildFieldAccess(ReadFieldKeys(cardSchemaJson))` 产生——键集合 = 卡片 schema 声明字段。`FDataJson` 含 schema 未声明键时（`UpdateAsync:751` 无校验直赋、`MergeTrustedDataJson:903-911` 全键合并、`ExcelInputPlugin.cs:235` 导入行直序列化），即便标 hidden/masked 也命不中，原值写入 `RedactedDataJson` 下发。
- 影响：脱敏是“按 schema 做减法”而非“按数据做减法”，安全边界依赖 `dataJson 键 ⊆ schema 字段` 这一未被保证的假设；denylist 形态对“未知敏感键”天然漏过。验证者把探针2 同根因下调为高（需 hidden/masked 配置 + 额外键同时成立），但取较高定级保留严重——机密性边界缺陷确证。
- 建议：改为默认白名单——遍历 `dataJson` 实际键，凡不在显式 editable/readonly 白名单内的键一律 Remove（或标 unknown→hidden）；至少对 schema 外键默认 Remove。
- 置信度：high

---

## 二、高

### 4. 移动端明细行 enum/select picker columns 硬编码空数组，无法录入
- 严重度：高 ｜ 类别：正确性 bug
- 位置：`web/src/components/cardflow/CardDetailTable.vue:821-824`（`:813-827`）
- 问题：移动端明细可编辑枚举/选择弹窗 `<VanPicker :title="field.label" :columns="[]" ...>` columns 写死空数组，遍历 `schema.filter(f=>['enum','select'].includes(f.type))` 却未把 `field.options` 传给 columns；PC 紧凑卡（:602）有正确 map 作对照。
- 影响：移动端明细填报（`MobileCardFillPage`/`MobileCardApprovalPage`）枚举列点开 picker 为空、无任何可选项→无法完成录入，明细核心录入链断裂，违反意图③。
- 建议：`:columns` 绑 `(field.options||[]).map(o=>({text:o,value:o}))`；`onPickerConfirm` 读 `selectedOptions[0]?.value`（已就位）。注：验证者证伪了“PC 把 options 当 string[] 会渲染 [object Object]”子项——`SchemaFieldDefinition.options` 契约即 `string[]`（types/cardflow.ts:895），PC 实现正确，不要按对象数组归一改 PC。
- 置信度：high

### 5. PC 详情页从不向 SchemaRenderer 传 :components，设计器编排的低代码卡片视图在 PC 完全不可见
- 严重度：高 ｜ 类别：一致性漂移
- 位置：`web/src/views/cardflow/CardDetailPage.vue:493-499`（分流 `SchemaRenderer.vue:140`；移动审批页 `web/src/views/cardflow-mobile/MobileCardApprovalPage.vue`）
- 问题：PC 详情只传 `:schema`+`:model-value` 未传 `:components`；`hasRuntimeComponents = (props.components?.length ?? 0)>0`，无 components 即走扁平字段分支，`CardComponentRenderer` 不挂载。移动审批页正确传 `currentStageWorkView.components`。
- 影响：同一套低代码 components 配置移动可见、PC 不可见，两端展示分裂；PC 永远只有扁平字段，违反意图③④。
- 建议：保留低代码链则 PC 详情也接 `currentStageWorkView.components`；若冻结低代码则明确扁平为唯一形态并移除移动端对 components 的消费，避免两端口径不一。
- 置信度：high

### 6. 业务状态组件（budgetStatus/invoiceStatus/paymentInfo/loanOffset/riskAlert）配置项全是摆设，运行态恒显静态文案
- 严重度：高 ｜ 类别：设计缺陷 ｜ 已知冻结项
- 位置：`src/STOTOP.Module.CardFlow/Services/CardPresentationResolver.cs:151-174`（桩组件 `runtime/components/BudgetStatusComponent.vue:9-10` 等；抽屉 `designer/CardComponentConfigDrawer.vue:349-380`）
- 问题：抽屉让用户配 `statusField/severity/amountField/balanceField/summaryKey`，但 `BuildRuntimeComponent` 只按 `Binding.Source` 取值分发，从不读这些 props；桩组件只渲染 `component.value ?? props.statusText ?? '待确认'`，不读 severity/amountField/balanceField。
- 影响：跨 5 个 P0 业务组件暴露的状态/严重度/金额配置运行态零生效，制造“可配置业务状态卡片”的虚假能力感，违反意图④“规则简单但要真生效”。
- 建议：二选一——(1) 保留则 resolver 增按 Type 取值分支（读 props.statusField/severity/amountField 计算展示与配色），桩组件消费这些字段；(2) 按冻结决策从目录/抽屉移除 businessStatus 段，停止暴露无效配置。本条是“冻结隐藏业务状态桩”决策的实证依据，推荐走 (2)。
- 置信度：high

### 7. 字段编辑器无脱敏/分节点显隐配置——扁平字段路径无法兑现意图④，直接挑战冻结决策
- 严重度：高 ｜ 类别：设计缺陷 ｜ 已知冻结相关
- 位置：`web/src/types/cardflow.ts:888-908`（编辑器 `SchemaFieldEditor.vue:356-496`；只读渲染 `SchemaRenderer.vue:316-405`）
- 问题：`SchemaFieldDefinition` 全字段无任何脱敏位（无 maskPattern/sensitive），也无 list/summary/approval 这类按节点显隐位；`SchemaFieldEditor` 因此配不出脱敏，`SchemaRenderer` 只读分支也无 masked 处理。脱敏能力只存在于低代码侧（`designer/CardComponentConfigDrawer.vue:307-314` + `runtime/CardComponentRenderer.vue:139-148`）。
- 影响：若执行“冻结低代码、只留扁平渲染”，意图④的“分节点脱敏”在扁平路径上**无处可配、无处可渲染**——含手机号/身份证/账号的卡片会在审批人之外路径全量明文下发。冻结决策与意图④唯一落地点直接冲突。
- 建议：在 `SchemaFieldDefinition` 增 `sensitive/maskPattern` 与 list/summary/approval 可见位，`SchemaRenderer` 只读分支接入与 `maskedValue` 一致逻辑；或在冻结评审中把“字段直绑+脱敏”判为必须保留点亮的能力，不随低代码一起冻结。
- 置信度：high

### 8. enum 字段拖成卡片组件后丢选项 + 不在 choice 白名单，低代码路径退化为纯文本框
- 严重度：高 ｜ 类别：正确性 bug
- 位置：`web/src/components/cardflow/designer/CardComponentCatalog.vue:380-394`（运行端 `runtime/CardComponentRenderer.vue:78-92`、`:103-106`、`:374-382`）
- 问题：`buildComponent` 处理 schema 字段项时 binding 只写 `{source:'cardField',fieldKey}`，props 从不拷贝 `item.field.options` 到 `props.options`；运行端 `normalizedOptions` 读 `component.props?.options`，且 `isChoiceField` 白名单 `['radio','checkbox','select','category']` 不含 `'enum'`。enum→`isChoiceField=false`→走普通 `<input type=text>`。
- 影响：在被判为“有运行时价值、应保留点亮”的“字段直绑组件”路径上，enum 既无下拉也无选项，运行态退化为自由文本框，意图④“恰当形式”对枚举失效。验证者补充：radio/checkbox 因 capability 默认携示例 options 尚能渲染选择控件，enum 严格更差。
- 建议：`buildComponent` 对 field 项映射 `props.options=field.options.map(o=>({label:o,value:o}))`，并把 `'enum'` 加入 `isChoiceField`/控件映射渲染为单选/下拉。
- 置信度：high

---

## 三、中

### 9. 后端 MaskValue 忽略 MaskPattern，与前端 phone/idCard 口径不一致并二次掩码
- 严重度：中 ｜ 类别：一致性漂移
- 位置：`src/STOTOP.Module.CardFlow/Services/StageViewProfileResolver.cs:226-243`（规则字段 `Models/Schema/StageViewProfileModels.cs:42`；前端 `runtime/CardComponentRenderer.vue:139-148`、`:155-157`）
- 问题：后端 `MaskValue` 恒走通用规则 `text.Length<=4 ? "****" : "{0..2}****{^2..}"`（已亲验 :242），从不读任何 pattern；`CardPresentationResolver.MaskValue` 同样不读。前端 `maskedValue` 按 `component.props?.maskPattern` 支持 phone/idCard，且会对后端已掩串再掩一次。注意验证者校正：前端读的是组件级 `props.maskPattern`（设计器属性），与 access 规则上的 `MaskPattern` 是两套设置。
- 影响：配置者所设 maskPattern（phone/idCard）对后端脱敏完全无效（手机号被掩成 `138****00` 而非 `138****8000`）；前端对已掩串二次处理，两端展示口径漂移、结果不可预期，违反意图④。
- 建议：后端 `MaskValue` 接收并解析 pattern，phone/idCard 与前端对齐；脱敏既在后端做实，前端不应对已 masked 的 value 再掩；两端共用一份掩码规则定义。
- 置信度：high

### 10. SchemaRenderer 扁平只读 enum 直出 value 丢 label
- 严重度：中 ｜ 类别：正确性 bug
- 位置：`web/src/components/cardflow/SchemaRenderer.vue:91-94`、`:104-105`（明细同病 `CardDetailTable.vue:202-204`）
- 问题：`getEnumLabel` 直接 `String(val)`，不查 `field.options` 做 value→label 映射；对照 `runtime/CardComponentRenderer.vue` 的 `optionLabel/normalizedOptions`（:78-97）能映射 label——同一份数据扁平路径显 value、低代码路径显 label，行为分裂。
- 影响：PC 只读详情（走扁平路径）与移动端无组件流程，只读 enum 显存储 value 而非业务 label。验证者说明：当前扁平 `options` 是 `string[]`（label==value），实质回退仅在“value≠label 对象选项流入扁平渲染”时激活，属真实但当前多为结构性不对称，故定中而非高。
- 建议：`getEnumLabel` 复用 `normalizedOptions` 查 label 再回退 `String(val)`，`CardDetailTable` 同步；需先打通 options 模型（见第 11 条）否则无 label 可查。
- 置信度：high

### 11. 前后端两套 options 形态不互通：扁平 string[] vs 低代码/后端 {label,value}[]
- 严重度：中 ｜ 类别：一致性漂移 ｜ 已知冻结相关
- 位置：`web/src/components/cardflow/SchemaFieldEditor.vue:93`、`SchemaRenderer.vue:198`/`:116`（后端 `CardSchemaV2Models.cs:37,54-58`；抽屉 `designer/CardComponentConfigDrawer.vue:152-170`）
- 问题：扁平字段 `SchemaFieldDefinition.options` 为 `string[]`（label=value，无值码分离）；低代码侧/后端为 `FieldOption{Label,Value}`。两套在 enum 选项渲染上不互通；若对象数组流入扁平 `.map(o=>({label:o,value:o}))` 会渲染成 `[object Object]`。运行端 `normalizedOptions` 接受 string fallback，故低代码侧不崩，属潜伏漂移。
- 影响：扁平 enum 只能中文做值无法值码分离；与低代码路径数据无法平滑互转，是双套字段模型在“枚举展示”的具体落点。
- 建议：统一为 `{label,value}`（升级 `SchemaFieldDefinition.options` 类型），`SchemaFieldEditor` 改双列编辑；抽取 `normalizeOptions`（参考 `CardComponentRenderer.normalizedOptions:78-92`）全局复用，使 `parseCardSchemaFields` 出口归一。
- 置信度：high

### 12. 设计器三处预览全部硬编码 platform=pc，无移动端所见即所得
- 严重度：中 ｜ 类别：设计缺陷
- 位置：`web/src/views/cardflow/FlowDefinitionEditPage.vue:1950-1958`、`:2251-2256`、`:2553-2558`
- 问题：步骤3卡片画布、运行态预览抽屉、预演工作台三处 `CardComponentRenderer` 全写死 `platform="pc"`，全文件无 `platform="mobile"`。卡片真实消费端是移动审批页（经 `CardDetailTable platform="mobile"` 渲染移动分支）。
- 影响：标题写“所见即所得”（:1871）但配置者在设计期只看到 PC 形态，无法发现移动端可读性/换行/折叠问题，违反意图③④的预览保真承诺。
- 建议：画布/预览加 PC↔移动 视图切换，移动态用 `platform="mobile"` 套手机外框宽度，组件已支持 `platform` 入参可直接落地。
- 置信度：high

### 13. 移动端 SchemaRenderer 缺 account/auxiliary/bankAccount/voucherRef 分支：编辑无控件、只读显 [object Object]
- 严重度：中 ｜ 类别：正确性 bug
- 位置：`web/src/components/cardflow/SchemaRenderer.vue:484-496`（类型面板 `SchemaFieldEditor.vue:54-57`；只读 `:543-564`；PC 分支 `:267-396`）
- 问题：这 4 个财务类型在 `SchemaFieldEditor` 是一等字段类型，但 `SchemaRenderer` 移动端编辑分支只覆盖 text/money/enum/date/file/user/org/cardRef，4 类无 `v-else-if`→移动编辑态不渲染；移动只读分支只处理 money/file/cardRef，其余走 `getViewValue`→对象值落 default→`String(val)`→`[object Object]`。PC 端有完整分支。
- 影响：移动端用了财务结构化字段类型的卡片：编辑态字段消失（无法录入）、只读态显 `[object Object]`，端到端不一致，违反意图③。
- 建议：移动端补 4 类分支（编辑接对应 Selector，只读复用 PC 格式化展示）。
- 置信度：high

### 14. 目录拖入非字段类 cardField 组件默认静默绑到第一个 schema 字段
- 严重度：中 ｜ 类别：UX 摩擦 ｜ 已知冻结项
- 位置：`web/src/components/cardflow/designer/CardComponentCatalog.vue:403`（`:395-408`；目录种子 `:99-122`；画布 `FlowDefinitionEditPage.vue:472`/`:759`；发布校验 `:1443-1459`）
- 问题：`buildComponent` 对无 `item.field` 的 cardField 源目录项一律 `fieldKey: firstField?.key||null`；拖入“联系人/部门/省市区/客户/工程项目/外部联系人”等 placeholderControl 即静默绑到 schema 第一个字段，抽屉/发布校验都不查 fieldKey 意图，画布还把自动绑的首字段当已选展示。
- 影响：误配置无提示（如拖“部门”静默绑到“申请金额”），运行态展示错字段，违反意图④“简单且正确”。验证者说明：抽屉字段下拉 allow-clear 可纠正，非死局，故定中。
- 建议：无显式字段时 `fieldKey` 置 null 并在画布/抽屉显著标记“未绑定字段，请选择”，或保存/发布校验拦截未绑定/疑似误绑组件。
- 置信度：high

### 15. CardDetailTable 移动端 enum picker columns=[] / PC 表格 a-select 未传 :options
- 严重度：中 ｜ 类别：正确性 bug
- 位置：`web/src/components/cardflow/CardDetailTable.vue:821-824`、`:462-469`、`:596-604`
- 问题：本条与第 4 条同根因（移动端 columns 写死空数组），合并保留以记录 PC 可编辑表格 `<a-select>` 也未传 `:options` 这一同源遗漏。PC 紧凑卡 `.map((o:string)=>({label:o,value:o}))` 经验证符合 `string[]` 契约、正确，不需改。
- 影响：移动端明细 enum 列无法录入（核心）；PC 全表 enum 列亦无选项。
- 建议：移动端按第 4 条修；PC 表格 `a-select` 补 `:options`；紧凑卡保持现状。
- 置信度：high

### 16. （并入第 11 条）扁平 enum 选项与低代码选项形态不互通在“枚举丢 label”链上的落点
- 严重度：中 ｜ 类别：一致性漂移
- 说明：探针3 “SchemaFieldEditor 产 string[]，ConfigDrawer/runtime 用 {label,value}[]” 与第 11 条同根因，已合并；保留编号占位以示来源覆盖。修复随第 10/11 条统一推进。

---

## 四、低

### 17. 明细聚合对 hidden/masked 列仍照常求和并下发，泄露被脱敏列
- 严重度：低 ｜ 类别：数据完整性
- 位置：`src/STOTOP.Module.CardFlow/Services/CardPresentationResolver.cs:350-397`
- 问题：`BindDetailTable` 对 hidden 列剔除、masked 列掩码（:317-339），但 `ApplyAggregations/SumDetailField` 直接对 `detail.FDataJson` 原始数据按 fieldKey 求和（:370、:384-397），完全不看 DetailAccess；`ComputeDefaultDetailSummary` 还默认对 `amount` 求和。
- 影响：把某金额明细列设 masked/hidden 脱敏时，其合计仍按真实值算出并随 detailSummary/amountSummary 下发，可反推被脱敏列。边缘配置场景，仅泄露聚合值非逐行原值。
- 建议：`SumDetailField` 求和前按 `(tableKey,fieldKey)` 查 DetailAccess，hidden/masked 列跳过默认聚合或将结果标记为受限。
- 置信度：high

---

## 五、该领域设计评判

**意图③（信息=表单流转，含移动端可填）：未兑现，存在硬阻断。** 经 v2 设计器编排过字段的流程，卡片提交被 `ValidateCardData` 硬阻断（条目1）；移动端明细 enum 列因 picker columns=[] 无法录入（条目4/15）、财务结构化字段在移动端编辑态消失（条目13）。这三处都落在“信息=表单”的录入/提交主链上，属功能性阻断，不是体验瑕疵。

**意图④（按不同节点以恰当内容与形式展示 + 字段权限脱敏）：基本未做到。** 脱敏链有三重结构性缺陷叠加：(a) 只在 active+v2 节点瞬时生效，完成/归档/草稿/v1 全量敞开（条目2）；(b) 按 schema denylist 做减法，schema 外敏感键漏脱敏（条目3）；(c) maskPattern 配置后端不读、两端口径漂移（条目9）；再叠加聚合反推（条目17）。结论是“脱敏只是审批进行中的临时遮罩，不是稳定的字段级访问控制”，最该控权的归档态反而最敞开。展示形式上：enum 在扁平/低代码两路 label 处理分裂（条目8/10/11），低代码 components 在 PC 不可见（条目5），设计期无移动预览（条目12）——“按节点恰当形式展示”与“所见即所得”均未稳定兑现。

**对低代码组件层冻结/删除/重设计的明确推荐：**

- **冻结并隐藏：5 类业务状态桩（budgetStatus/invoiceStatus/paymentInfo/loanOffset/riskAlert）及抽屉 businessStatus 段（条目6）。** 配置项运行态全零生效，纯虚假能力，应从目录与配置抽屉移除，避免继续误导配置者。这是本轮多探针交叉坐实、且与在途冻结决策一致的结论。
- **保留并修复（不可随低代码一起冻结）：字段直绑 cardField 组件 + 脱敏能力。** 字段直绑 enum 需补 options 与 choice 白名单（条目8），这是有真实运行价值的路径。更关键的是：**若执行“冻结低代码、只留扁平渲染”，必须先把脱敏/分节点显隐能力下沉到 `SchemaFieldDefinition`+`SchemaRenderer`（条目7）**，否则等于砍掉意图④唯一可落地点，含 PII 卡片将明文下发——这是冻结决策的硬前置条件。
- **重设计（而非冻结）：脱敏链整体。** 三条严重项（条目2/3）的根因不是低代码 UI，而是后端脱敏被绑定在“active+v2 节点 + schema denylist”这套错误的触发与遍历模型上。应把脱敏重构为“查看者维度 + 按 DataJson 实际键的 allowlist + 适用任意卡片状态”的稳定后置过滤层，与低代码是否冻结无关，须独立优先推进。
- **统一（消除双套模型）：选项/控件/字段三套漂移（条目10/11/14/15）。** 统一 options 为 `{label,value}`、抽取全局 `normalizeOptions`、归一 enum 控件判定，消除扁平与低代码两套实现长期漂移。这是降低后续所有展示类 bug 复发率的结构性投资。

总体设计兑现度：核心架构意图（信息=表单、按节点脱敏展示）方向正确，但落地链在**校验入口、脱敏触发模型、双套字段模型**三处系统性失守，当前不可对外承诺意图③④已兑现。
