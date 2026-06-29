# 导入计算验证工作台设计

日期：2026-06-10
状态：已确认方向，进入实施计划阶段

## 背景

STOTOP 已经有三条与导入后计算相关的业务链路：

- CardFlow 导入链路：`CfImportController` 支持上传、预览、批次查询；`CfAutoVoucherController` 已有字段分析和 DryRun；`AutoVoucherHandler`、`VoucherGenerationService`、`CfVoucherRecord` 承接自动凭证生成。
- Express 价格计算链路：`PricingEngine` 按 KH、DL、WD、YW、CB、YZ 六步查找报价，使用 `PriceFormula`、报价矩阵、附加费和佣金生成 `ExpBillingResult`。
- Express 成本计算链路：`CostEngine` 读取成功计费结果，通过 `CostPlanCache` 命中成本方案、成本项和矩阵，写入 `ExpBillingCostBreakdown` 并回写 `F成本合计`。

当前缺口不是缺少计算能力，而是业务用户无法解释“为什么算成这样”，也无法快速判断异常来自导入数据、配置、计算逻辑还是结果写入链路。这个工具要把导入行、配置命中、计算步骤、生成结果和异常归因放在同一个验证台里。

## 目标

第一版建设一个面向前端业务用户的导入计算验证工作台：

- 针对已导入批次验证自动凭证生成、价格计算、成本计算三条链路；
- 展示原始导入行到最终业务结果的逐项证据链；
- 自动给异常打上 `导入数据问题`、`配置问题`、`计算逻辑问题`、`生成/写入链路问题` 四类归因；
- 给出可执行的修复建议，例如维护报价矩阵、启用成本方案、补充凭证规则、修正导入字段；
- 支持抽样验证、异常优先验证和导出验证结果；
- 不改变现有导入、计费、成本、凭证生成结果。

## 非目标

- 不替换现有 `PricingEngine`、`CostEngine`、`AutoVoucherHandler`。
- 不新建一套独立的报价、成本、凭证配置体系。
- 不在第一版做导入前沙箱审批；第一版只验证已导入批次。
- 不把全量批次重算做成强制同步流程；大批量全量验证留到异步任务二期。
- 不承诺工具能证明所有计算逻辑都绝对正确；第一版输出的是基于证据链和规则的高置信归因。

## 核心判断

工具必须把“错了”拆成四类原因。

### 导入数据问题

当原始行缺少计算所需字段，或字段格式无法被当前链路识别时，归因到导入数据问题。

典型证据：

- 业务日期、重量、金额、店铺、品牌、网点、目的省市为空或格式异常；
- 店铺别名无法匹配；
- 目的省市无法识别；
- 凭证金额字段不是数字；
- 同一批次重复导入或关键业务键重复。

### 配置问题

当原始数据完整，但系统无法命中必要配置，或命中的配置明显不完整时，归因到配置问题。

典型证据：

- 自动凭证规则覆盖率不足、规则组无输出、科目或辅助核算缺失；
- 报价方案未启用、报价矩阵缺省份/城市单元格、附加费作用域未配置；
- 成本方案未启用、成本项未维护矩阵、成本项适用网点不包含当前网点；
- `accountSetId`、`orgId`、期间、账套上下文缺失；
- 阿米巴或财务配置读取到了错误账套或错误组织。

### 计算逻辑问题

当导入数据和配置都足够完整，但系统持久化结果与工具按同一配置解释出来的应得结果不一致，或计算过程违反已定义公式/不变量时，归因到疑似计算逻辑问题。

典型证据：

- `ExpBillingResult.FChargeAmount` 与解释链路计算出的 `运费 + 附加费 + 保价费` 不一致；
- `ExpBillingResult.FTotalCost` 与成本明细求和不一致；
- 自动凭证 DryRun 借贷平衡，但实际凭证借贷不平；
- 首续重、进位、重量段边界、城市/省份/全国回退违反公式说明；
- 佣金、返利、负成本项符号与配置语义不一致。

工具要显示“疑似计算逻辑问题”，并附上公式、输入值、系统值、解释值和差异。真正修复仍需开发人员基于对应服务和测试确认。

### 生成/写入链路问题

当 DryRun 或 Explain 结果正确，但实际业务结果缺失或落库不一致时，归因到生成/写入链路问题。

典型证据：

- 自动凭证 DryRun 有输出，但 `CfVoucherRecord` 无生成记录或 `FinVoucher` 缺失；
- 价格解释结果正常，但计费结果表没有对应运单；
- 成本解释结果正常，但成本明细表未写入；
- 重跑时旧结果未删除，导致重复或过期结果仍显示；
- 批次状态、暂存表计算状态和结果表状态不一致。

## 总体方案

新增一个“导入计算验证工作台”，入口建议放在 CardFlow 导入中心的批次详情或 WorkHub 相关入口旁边。

后端新增 `ImportCalculationValidationService`，以批次为入口，读取暂存表、导入批次、自动凭证规则、凭证生成记录、计费结果和成本明细。它不直接修改业务数据，只返回验证报告。

前端新增 `ImportCalculationValidationWorkbench.vue`，包含四个区域：

- 批次概览：批次号、流程、暂存表、导入行数、计算状态、验证范围；
- 归因摘要：四类问题数量、严重程度、置信度、主要建议；
- 明细列表：按原始行、运单、凭证来源行展示系统值、解释值、差异和归因；
- 证据抽屉：展示字段来源、配置命中、公式步骤、落库结果、修复建议。

## 后端架构

### 控制器

新增控制器：

```text
src/STOTOP.Module.CardFlow/Controllers/CfImportValidationController.cs
```

API：

```text
GET  /api/cardflow/import-validation/batches/{batchId}/summary
POST /api/cardflow/import-validation/batches/{batchId}/run
GET  /api/cardflow/import-validation/batches/{batchId}/rows/{rowId}
```

`summary` 返回批次和已有结果概览，不做重算。

`run` 执行验证，支持参数：

- `domains`：`voucher`、`pricing`、`cost`；
- `mode`：`sample`、`errorsOnly`、`allLimited`；
- `sampleSize`：抽样数量，默认 100；
- `includeEvidence`：是否返回完整证据，默认 true；
- `tolerance`：金额容差，默认 0.01。

`rows/{rowId}` 返回单行详细证据，便于前端按需展开。

### 服务边界

新增服务：

```text
src/STOTOP.Module.CardFlow/Services/Validation/IImportCalculationValidationService.cs
src/STOTOP.Module.CardFlow/Services/Validation/ImportCalculationValidationService.cs
src/STOTOP.Module.CardFlow/Services/Validation/VoucherValidationAnalyzer.cs
src/STOTOP.Module.CardFlow/Services/Validation/PricingValidationAnalyzer.cs
src/STOTOP.Module.CardFlow/Services/Validation/CostValidationAnalyzer.cs
src/STOTOP.Module.CardFlow/Services/Validation/ValidationAttributionClassifier.cs
```

责任划分：

- `ImportCalculationValidationService` 负责批次上下文、抽样策略、聚合报告；
- `VoucherValidationAnalyzer` 负责自动凭证规则覆盖、DryRun、凭证生成记录、凭证分录对账；
- `PricingValidationAnalyzer` 负责价格解释、计费结果对账；
- `CostValidationAnalyzer` 负责成本解释、成本明细和成本合计对账；
- `ValidationAttributionClassifier` 根据证据统一输出归因类别、严重程度、置信度和建议。

### DTO

新增 DTO：

```text
src/STOTOP.Module.CardFlow/Dtos/ImportValidationDtos.cs
```

核心结构：

```text
ImportValidationSummaryDto
ImportValidationRunRequest
ImportValidationReportDto
ImportValidationFindingDto
ImportValidationEvidenceDto
CalculationTraceStepDto
ValidationAttribution
ValidationSeverity
ValidationDomain
```

每个 finding 至少包含：

- `domain`：凭证、价格或成本；
- `sourceRowId`、`businessKey`、`waybillNo`、`voucherId`；
- `attribution`：导入数据、配置、计算逻辑、写入链路；
- `severity`：阻断、高、中、低；
- `confidence`：0 到 1；
- `systemValue`、`expectedValue`、`difference`；
- `evidence`：字段、配置、公式、落库结果；
- `suggestedAction`。

## Explain 机制

### 自动凭证 Explain

复用现有 `CfAutoVoucherController` 中的 field-analysis 和 dry-run 思路，但输出要从统计级扩展到行级：

- 读取批次实际暂存表；
- 解析自动凭证规则；
- 对每行记录命中的规则层级、规则组、分录行、金额字段、方向、科目、辅助核算；
- 汇总出应生成的凭证草案；
- 与 `CfVoucherRecord` 和实际 `FinVoucher` / `FinVoucherEntry` 对账。

如果规则无法命中，优先归因配置问题；如果字段缺失导致无法判断，归因导入数据问题；如果 DryRun 与实际凭证不一致，归因生成/写入链路或疑似计算逻辑问题。

### 价格 Explain

在 `PricingEngine` 附近增加只读解释能力，不写结果表：

- 店铺和别名如何命中报价；
- 六步业务对象链路中哪一步命中；
- 结算重量来源；
- 重量段、目的省份/城市单元格；
- 进位参数；
- 公式：`base + ceil(max(0, roundedWeight - firstWeight) / continueStep) * continuePrice`；
- 附加费作用域：报价级、业务对象级、全局；
- 保价费和佣金；
- 解释值与 `ExpBillingResult` 的系统值对比。

如果报价或矩阵缺失，归因配置问题；如果字段缺失，归因导入数据问题；如果解释值与系统值不一致，归因疑似计算逻辑问题或写入链路问题。

### 成本 Explain

在 `CostPlanCache` 附近增加只读解释能力：

- 成本方案命中：组织方案优先，全局方案回退；
- 成本项是否适用于当前网点；
- 成本项矩阵的生效日期、重量段、定价范围；
- 城市、省份、全国回退路径；
- 返利项符号处理；
- 成本明细合计与 `ExpBillingResult.FTotalCost` 对账。

如果没有成本方案或成本项矩阵，归因配置问题；如果目的地或重量缺失，归因导入数据问题；如果明细求和和主表成本不一致，归因疑似计算逻辑或写入链路问题。

## 前端设计

新增页面：

```text
web/src/views/cardflow/import-validation/ImportCalculationValidationWorkbench.vue
```

新增 API：

```text
web/src/api/importValidation.ts
```

新增路由：

```text
/cardflow/import-validation/:batchId
```

页面交互：

- 顶部显示批次基础信息和验证按钮；
- 使用 tabs 切换 `总览`、`自动凭证`、`价格计算`、`成本计算`；
- 每个 tab 的表格列保持一致：来源、系统值、解释值、差异、归因、严重程度、建议；
- 点击一行打开证据抽屉；
- 抽屉中按“导入字段”“命中配置”“计算步骤”“实际结果”“修复建议”分组；
- 支持只看阻断问题、只看配置问题、只看疑似逻辑问题；
- 支持导出当前验证结果。

前端不把这个工具做成面向开发者的日志页。业务用户应该能从每条异常直接知道找谁处理：导入人员、业务配置人员、财务配置人员，还是开发人员。

## 数据与安全

- 所有接口必须通过 CardFlow 导入权限或更细的验证权限控制。
- 验证只能读取当前组织可访问的批次。
- 暂存表名必须沿用现有表名校验规则，禁止任意 SQL 表名拼接。
- 验证接口默认限制返回行数；大批次全量验证需要二期异步任务。
- 金额比较默认容差 0.01，可由请求覆盖但不能小于 0。

## 验收标准

第一版完成后，用户可以：

- 从导入批次进入验证工作台；
- 看到自动凭证、价格、成本三类验证摘要；
- 对异常行看到明确归因；
- 打开证据抽屉看到字段、配置、公式、结果的完整链路；
- 区分“需要补配置”还是“疑似计算逻辑或写入链路有问题”；
- 导出验证结果给业务、财务或开发处理。

技术验收：

- 新增 CardFlow 后端合同测试覆盖 controller、service、DTO、路由；
- 新增价格公式 Explain 单元测试覆盖固定单价、首续重、进位和金额差异；
- 新增成本 Explain 或成本验证测试覆盖成本配置缺失、成本明细求和、成本合计差异；
- 新增自动凭证验证测试覆盖未匹配、规则组无输出、借贷不平和生成记录缺失；
- 后端 `dotnet build src/STOTOP.WebAPI/STOTOP.WebAPI.csproj` 通过；
- 前端生产构建通过。

## 分期

### 一期

- 已导入批次验证；
- 抽样、异常优先和有限全量模式；
- 自动凭证、价格、成本三条链路；
- 证据抽屉和导出；
- 不新增持久化验证运行表。

### 二期

- 异步全量验证和验证历史；
- 导入前沙箱验证；
- 配置变更后的影响评估；
- 自动生成配置修复草案；
- 与 WorkHub 问题任务联动。
