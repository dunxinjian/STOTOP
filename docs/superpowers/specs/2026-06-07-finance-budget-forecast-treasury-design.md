# 财务预算、滚动预测与资金管理设计

## 背景

STOTOP 已经具备承接这套管理机制的基础：

- Finance 模块已经包含账套、期间、科目、凭证、日记账、银行流水、银行对账、财务报表和阿米巴经营报表。
- 阿米巴经营报表已经支持模板化损益项，并能读取实际数、手工填报数和暂估数。
- OA 与 CardFlow 已经承接费用请款、费用报销、审批节点、流程实例和凭证生成路径。
- 当前缺口不是基础会计核算，而是把预算、损益滚动预测、资金滚动预测和预算控制连成一个经营管理闭环。

本设计在现有 Finance 模块内扩展预算、预测和资金计划子域，并把 OA/CardFlow 作为预算占用、审批和释放的业务入口。

## 目标

第一期建立一套可用的经营管理闭环：

- 年度预算、调整预算和滚动预测版本管理；
- 阿米巴经营报表支持实际、预算、预测和差异对比；
- 13 周滚动资金预测；
- OA/CardFlow 费用业务触发预算占用、锁定、消耗和释放；
- 财务首页展示资金风险、预算执行、预测偏差和超预算预警。

第一期目标是让财务负责人和经营负责人能真正用起来，而不是做一个完整但孤立的 FP&A 系统。

## 非目标

- 不在 Finance 外另建独立 FP&A 模块。
- 不替换现有凭证、报表、银行流水、银行对账和阿米巴 PL 服务。
- 不重构会计期间、账套、科目模板或凭证模板。
- 第一版不要求所有业务单据都强制预算控制。
- 第一版不新增复杂公式语言，继续复用现有报表公式和阿米巴模板思路。

## 总体方案

在 Finance 模块内新增四个子域：

- `预算 Budget`：按账套、组织、期间、科目或阿米巴损益项维护版本化预算。
- `预测 Forecast`：保存滚动损益预测版本，支持“已发生期间取实际数，未来期间取预测数”。
- `资金计划 TreasuryPlan`：按日期或周维护直接法现金流入、流出预测。
- `预算占用 BudgetOccupation`：记录 OA/CardFlow 单据对预算的占用、锁定、消耗和释放。

系统继续从现有 Finance 数据读取实际数，用户通过新增预算、预测和资金计划页面维护计划值。预算维度要同时服务会计视角和阿米巴经营视角。

## 一期范围

第一期分两个可交付闭环执行，避免范围和指标不一致。

### 一期 A：预算与资金闭环

交付：

- 预算版本管理；
- 预算明细维护；
- 费用类型到预算科目/阿米巴损益项的映射；
- 13 周滚动资金预测；
- CardFlow 费用流程预算占用；
- 财务首页展示可用资金、13 周最低结余、预算执行率、超预算单据数。

不交付：

- 全年预测利润；
- 预测差异；
- 当前预测与历史预测对比。

### 一期 B：阿米巴预算与预测对比

交付：

- 损益滚动预测版本；
- 预测明细维护；
- 阿米巴实际、预算、预测和差异对比；
- 财务首页增加全年预测利润、预测偏差和预测更新状态。

这样处理后，第一期 A 不依赖 Forecast 模型即可上线；预测相关指标只在一期 B 出现。

## 数据模型

### 预算版本 `FinBudgetVersion`

用于表示一个预算或计划场景。

字段：

- `FAccountSetId`：账套 ID。
- `FName`：版本名称，例如 `2026年度预算`、`2026Q3调整预算`。
- `FScenarioType`：场景类型，取值为 `annual_budget`、`adjusted_budget`、`rolling_forecast_baseline`、`cash_plan_baseline`。
- `FYear`：预算年度。
- `FBaseVersionId`：可选，复制来源版本。
- `FStatus`：`draft`、`submitted`、`approved`、`locked`、`archived`。
- `FOwnerOrgId`：预算归属组织。
- `FCreatedBy`、`FCreatedTime`、`FApprovedBy`、`FApprovedTime`。

规则：

- 只有 `approved` 或 `locked` 版本可作为正式预算基线。
- `draft` 可以编辑。
- `locked` 不允许编辑，只能复制成调整预算。
- 同一账套、同一年度只能有一个启用的正式年度预算。

### 预算明细 `FinBudgetLine`

用于保存最低管理颗粒度的预算金额。

字段：

- `FBudgetVersionId`。
- `FPeriod`：`YYYYMM`。
- `FOrgId`：组织或部门。
- `FAmoebaUnitId`：可选，阿米巴单元。
- `FAccountId` 或 `FAccountCode`：可选，会计科目。
- `FPLItemId`：可选，阿米巴损益项。
- `FDimensionJson`：可选，客户、供应商、项目、线路、品牌、员工等维度。
- `FAmount`：预算金额。
- `FQuantity`：可选，业务量驱动。
- `FUnitPrice`：可选，单价或费率驱动。
- `FRemark`。

规则：

- `FAccountId`、`FAccountCode`、`FPLItemId` 至少填一个。
- 如果同时填科目和阿米巴损益项，同一条预算可同时服务会计报表和阿米巴报表。
- 明细按期间、组织、科目或损益项累加。

### 费用类型预算映射 `FinBudgetExpenseMapping`

用于解决 OA 费用请款只有费用类型，没有明细科目和阿米巴损益项的问题。没有这张映射表，费用请款在提交时无法可靠占用预算。

字段：

- `FAccountSetId`：账套 ID。
- `FOrgId`：可选，组织级覆盖。
- `FExpenseType`：OA 费用类型。
- `FAccountCode`：预算占用对应科目编码。
- `FPLItemId`：预算占用对应阿米巴损益项。
- `FCashCategory`：资金计划分类，例如 `expense_reimbursement`、`supplier_payment`、`tax`。
- `FStatus`：启用状态。
- `FRemark`。

规则：

- 费用请款优先用 `OrgId + ExpenseType` 匹配，找不到时退回账套级 `ExpenseType` 匹配。
- 费用报销明细已有 `ExpenseAccountCode` 时，优先使用明细科目，再补映射到 `PLItemId`。
- 映射缺失时，预算占用预览必须提示“未配置预算映射”，不能静默跳过。

### 预测版本 `FinForecastVersion`

用于保存滚动损益预测快照。

字段：

- `FAccountSetId`。
- `FName`。
- `FYear`。
- `FForecastStartPeriod`：第一个预测期间。
- `FHorizonMonths`：默认 12。
- `FStatus`：`draft`、`approved`、`archived`。
- `FSourceBudgetVersionId`：可选，对比基线。

规则：

- `FForecastStartPeriod` 之前的期间取实际数。
- `FForecastStartPeriod` 及之后的期间取预测数。
- 已批准预测版本不可修改，便于复盘预测准确性。

### 预测明细 `FinForecastLine`

字段：

- `FForecastVersionId`。
- `FPeriod`。
- `FOrgId`。
- `FAmoebaUnitId`。
- `FAccountId` 或 `FAccountCode`。
- `FPLItemId`。
- `FAmount`。
- `FDriverJson`：可选，业务量、单价、人头、手工假设等说明。
- `FConfidenceLevel`：`high`、`medium`、`low`。
- `FRemark`。

### 资金账户映射 `FinTreasuryAccountBinding`

用于解决现有 `FinPaymentChannel`、`FinBankTransaction` 没有账套和组织字段的问题。13 周资金预测必须先建立银行渠道、现金银行科目与账套/组织的对应关系，否则会混用不同账套或组织的资金余额。

字段：

- `FAccountSetId`：账套 ID。
- `FOrgId`：可选，组织 ID。
- `FChannelId`：可选，交易渠道 ID。
- `FCashAccountId`：可选，现金/银行科目 ID。
- `FAccountNo`：可选，银行账号冗余，用于展示和校验。
- `FOpeningSource`：`bank_transaction_balance`、`account_balance`、`manual`。
- `FManualOpeningAmount`：当来源为 `manual` 时使用。
- `FStatus`：启用状态。
- `FRemark`。

规则：

- 每个参与资金预测的银行渠道必须绑定到账套；否则不能作为期初资金来源。
- 如果绑定了现金银行科目，优先用科目余额计算期初资金；如果只绑定银行渠道，则用该渠道最新银行流水余额。
- 银行流水余额只能作为渠道级资金余额，不能直接代表账套余额。
- 多账套共用同一银行账号时，必须配置组织或账套拆分规则；第一版不支持自动拆分。

### 资金计划明细 `FinTreasuryPlanLine`

用于保存直接法现金流预测行。

字段：

- `FAccountSetId`。
- `FOrgId`。
- `FPlanDate`：计划日期。
- `FWeekStartDate`：所属周。
- `FDirection`：`inflow` 或 `outflow`。
- `FCashCategory`：`customer_collection`、`supplier_payment`、`salary`、`tax`、`expense_reimbursement`、`loan_in`、`loan_repayment`、`interest`、`capex`、`internal_transfer`、`other`。
- `FAmount`。
- `FProbability`：默认 100。
- `FSourceType`：`manual`、`bank_transaction`、`oa_expense_request`、`oa_reimbursement`、`cardflow_card`、`voucher`、`budget`、`forecast`。
- `FSourceId`：可选，来源记录 ID。
- `FCounterpartyName`。
- `FRemark`。

规则：

- 金额始终为正数，由 `FDirection` 决定流入或流出。
- 来源单据生成的资金计划行必须按 `FSourceType + FSourceId + FCashCategory` 幂等。
- 手工计划行用于工资、税费、供应商付款、贷款、本息、预计回款等尚未形成业务单据的事项。

### 预算占用 `FinBudgetOccupation`

用于记录业务单据对预算的占用、锁定、消耗和释放。

字段：

- `FBudgetVersionId`。
- `FBudgetLineId`：可选，命中的预算明细。
- `FSourceType`：`cardflow_card`、`oa_expense_request`、`oa_expense_reimbursement`。
- `FSourceId`。
- `FOrgId`。
- `FPeriod`。
- `FAccountCode`。
- `FPLItemId`。
- `FAmount`。
- `FStatus`：`occupied`、`locked`、`consumed`、`released`。
- `FTransitionKey`：最后处理过的 CardFlow 状态或动作，用于幂等。
- `FCreatedTime`、`FUpdatedTime`。

规则：

- 同一来源单据和同一预算维度不能重复占用。
- 超预算策略为 `block` 时，不能创建超过可用预算的占用。
- `warn` 策略只记录超预算预警，不阻止流程。
- `extra_approval` 策略给 CardFlow 返回“需要追加审批”的判定，交由流程节点路由处理。
- 释放和消耗都必须保留变更记录，不能物理删除占用记录。

## CardFlow 预算占用生命周期

现有 OA 费用请款和费用报销的直接 `SubmitAsync` 已废弃，服务会提示通过 CardFlow 发起审批。因此预算控制不能挂在旧 OA Submit 方法上，必须挂在 CardFlow 卡片生命周期上。

### 费用请款流程

适用来源：

- `FYQK` 或后续费用请款 CardFlow；
- 关联 OA 费用请款单时，`SourceType = oa_expense_request`；
- 直接由 CardFlow 卡片承载时，`SourceType = cardflow_card`。

生命周期：

1. 创建草稿：只做预算预览，不占用。
2. 提交卡片：调用 `BudgetOccupationService.OccupyAsync`，状态为 `occupied`。
3. 普通审批通过到财务复核或终审节点：调用 `LockAsync`，状态为 `locked`。
4. 流程驳回、撤回、作废：调用 `ReleaseAsync`，状态为 `released`。
5. 后续付款或报销引用请款：按实际支付或引用金额调用 `ConsumeAsync`，状态为 `consumed` 或部分消耗。

### 费用报销流程

适用来源：

- `FYBS`、`FYFK`、`FYBS_VOUCHER` 等费用报销相关 CardFlow。

生命周期：

1. 草稿保存：预算预览，校验费用类型映射和明细科目。
2. 提交卡片：按明细行合计调用 `OccupyAsync`。
3. 审批通过：调用 `LockAsync`。
4. 凭证生成或付款确认：调用 `ConsumeAsync`。
5. 驳回、撤回、冲销：调用 `ReleaseAsync` 或反向冲减。

### CardFlow 钩子要求

预算服务需要暴露给 CardFlow 的窄接口：

- `PreviewAsync(cardType, dataJson, details)`：返回预算余额、占用金额、缺口和映射缺失项。
- `OccupyAsync(cardId, cardType, dataJson, details, transitionKey)`。
- `LockAsync(cardId, transitionKey)`。
- `ConsumeAsync(cardId, amount, transitionKey)`。
- `ReleaseAsync(cardId, transitionKey)`。

CardFlow 调用点应放在卡片状态转换成功后的事务内或同一幂等补偿流程中。第一版优先在 CardFlow 后端服务中实现，不在前端直接写预算占用。

## 数据流

### 实际损益数据

实际数继续来自现有 Finance 和 Amoeba 逻辑：

1. 凭证和科目余额生成标准财务报表；
2. 阿米巴模板把科目、计费、折旧、暂估和手工数据映射到经营损益项；
3. `manual` 和 `estimate` 继续作为无法直接取系统数的补充。

预算和预测对比不能复制实际数算法，应调用现有报表或阿米巴服务，再按期间、组织、科目或损益项拼接预算/预测数据。

### 预算编制

1. 财务负责人创建预算版本。
2. 用户按组织、期间、科目或阿米巴损益项录入预算明细。
3. 可从上一年度实际数、已有预算版本或预测版本复制。
4. 预算版本提交审批。
5. 审批通过后成为正式对比基线。

### 损益滚动预测

1. 用户创建预测版本，并指定预测起始期间。
2. 起始期间之前读取实际数。
3. 起始期间及之后读取预测明细。
4. 阿米巴报表展示全年“实际 + 预测”。
5. 差异列对比正式预算版本。

### 13 周资金预测

1. 用户为参与资金预测的银行渠道或现金银行科目配置 `FinTreasuryAccountBinding`。
2. 系统按账套和组织计算期初可用资金。
3. 未来流入、流出来自资金计划明细。
4. CardFlow 费用请款和费用报销生成已承诺流出。
5. 手工计划行补充工资、税费、供应商付款、贷款、本息、投资支出和预计回款。
6. 仪表盘计算每周净流量、期末资金和低于安全资金的周次。

## 后端设计

### 新增实体

新增到 `src/STOTOP.Module.Finance/Entities`：

- `FinBudgetVersion`
- `FinBudgetLine`
- `FinBudgetExpenseMapping`
- `FinForecastVersion`
- `FinForecastLine`
- `FinTreasuryAccountBinding`
- `FinTreasuryPlanLine`
- `FinBudgetOccupation`

新增 EF 配置到 `src/STOTOP.Module.Finance/Configurations`。

关键索引：

- `FinBudgetLine`: `(FBudgetVersionId, FPeriod, FOrgId, FPLItemId)`。
- `FinBudgetLine`: `(FBudgetVersionId, FPeriod, FOrgId, FAccountCode)`。
- `FinBudgetExpenseMapping`: `(FAccountSetId, FOrgId, FExpenseType)`。
- `FinForecastLine`: `(FForecastVersionId, FPeriod, FOrgId, FPLItemId)`。
- `FinTreasuryAccountBinding`: `(FAccountSetId, FOrgId, FChannelId)`。
- `FinTreasuryPlanLine`: `(FAccountSetId, FWeekStartDate, FDirection, FCashCategory)`。
- `FinBudgetOccupation`: `(FBudgetVersionId, FSourceType, FSourceId, FPeriod, FAccountCode, FPLItemId)`。

### 新增服务

新增到 `src/STOTOP.Module.Finance/Services`：

- `BudgetService`：预算版本 CRUD、预算明细批量保存、提交、审批、从实际数复制。
- `BudgetExpenseMappingService`：费用类型到科目和阿米巴损益项的映射维护。
- `ForecastService`：预测版本 CRUD、实际加预测序列、差异计算。
- `TreasuryPlanService`：13 周资金预测、资金计划 CRUD、期初资金计算。
- `BudgetOccupationService`：预算预览、占用、锁定、消耗、释放和可用预算校验。

接口放在 `src/STOTOP.Module.Finance/Services/Interfaces`。

### 新增控制器

新增：

- `BudgetController`：`/api/finance/budgets`。
- `ForecastController`：`/api/finance/forecasts`。
- `TreasuryPlanController`：`/api/finance/treasury-plans`。
- `BudgetControlController`：`/api/finance/budget-control`。

代表接口：

- `GET /api/finance/budgets/versions`
- `POST /api/finance/budgets/versions`
- `POST /api/finance/budgets/versions/{id}/copy-from-actual`
- `POST /api/finance/budgets/versions/{id}/submit`
- `POST /api/finance/budgets/versions/{id}/approve`
- `GET /api/finance/budgets/versions/{id}/lines`
- `POST /api/finance/budgets/versions/{id}/lines:batch-upsert`
- `GET /api/finance/budgets/expense-mappings`
- `POST /api/finance/budgets/expense-mappings`
- `GET /api/finance/forecasts/versions`
- `POST /api/finance/forecasts/versions`
- `GET /api/finance/forecasts/{id}/amoeba-series`
- `GET /api/finance/treasury-plans/account-bindings`
- `POST /api/finance/treasury-plans/account-bindings`
- `GET /api/finance/treasury-plans/rolling-13-weeks`
- `POST /api/finance/treasury-plans/lines`
- `PUT /api/finance/treasury-plans/lines/{id}`
- `DELETE /api/finance/treasury-plans/lines/{id}`
- `POST /api/finance/budget-control/preview`

### 现有模块接入点

- `FinanceModuleExtensions` 注册新增服务和 EF 配置。
- `AmoebaPLService` 新增预算/预测对比查询，不把预算逻辑塞进现有基础报表算法。
- CardFlow 后端服务在提交、审批通过、驳回、撤回、凭证生成或付款确认等状态转换点调用 `BudgetOccupationService`。
- OA 服务只作为读取业务数据和映射来源，不再把旧 `SubmitAsync` 当作预算占用入口。

## 前端设计

### API

扩展 `web/src/api/finance.ts`：

- 预算版本 API；
- 预算明细 API；
- 费用类型预算映射 API；
- 预测版本 API；
- 阿米巴实际/预算/预测对比 API；
- 13 周资金预测 API；
- 资金账户映射 API；
- 预算占用预览 API。

### 新页面

新增到 `web/src/views/finance`：

- `BudgetVersionManage.vue`：预算版本列表、创建、复制、提交、审批。
- `BudgetLineEditor.vue`：按期间和组织编辑预算明细。
- `BudgetExpenseMapping.vue`：维护费用类型到科目、阿米巴损益项、资金分类的映射。
- `ForecastWorkbench.vue`：滚动预测维护和对比。
- `TreasuryAccountBinding.vue`：银行渠道、现金银行科目与账套/组织绑定。
- `TreasuryRollingForecast.vue`：13 周资金预测，展示周维度流入、流出、期末资金和现金缺口。

新增路由：

- `/finance/budget/versions`
- `/finance/budget/editor/:versionId`
- `/finance/budget/expense-mapping`
- `/finance/forecast/workbench`
- `/finance/treasury/account-bindings`
- `/finance/treasury/rolling-13-weeks`

### 现有页面扩展

扩展 `AmoebaPL.vue`：

- 增加预算版本和预测版本选择器；
- 增加展示模式：`实际`、`预算`、`预测`、`实际+预测`、`预算差异`、`预测差异`；
- 继续沿用当前“图标 + 右侧 Drawer”的说明列压缩方式；
- Excel 导出保留完整数据来源和计算逻辑文本。

扩展 `FinanceHome.vue`：

一期 A 展示：

- 可用资金；
- 未来 13 周最低期末资金；
- 首个低于安全资金的周次；
- 收入预算完成率；
- 费用预算使用率；
- 超预算单据数。

一期 B 增加：

- 全年预测利润；
- 预测偏差；
- 当前预测与上一版预测差异。

扩展 OA/CardFlow 费用表单：

- 提交前展示预算预览；
- 显示可用预算、占用金额和缺口；
- 映射缺失时提示配置费用类型预算映射；
- 超预算时根据策略显示预警、阻止提交或提示追加审批。

## 权限

新增 Finance 权限：

- `finance:budget:view`
- `finance:budget:edit`
- `finance:budget:approve`
- `finance:budget:mapping`
- `finance:forecast:view`
- `finance:forecast:edit`
- `finance:forecast:approve`
- `finance:treasury:view`
- `finance:treasury:edit`
- `finance:budget-control:view`

第一版可以先复用财务管理员角色，后续再细化到组织和账套维度。

## 校验规则

### 预算版本

- 年度必填。
- 场景类型必填。
- 已批准或已锁定版本不能编辑。
- 同账套、同年度只能有一个启用的正式年度预算。

### 预算明细

- 期间必须是 `YYYYMM`。
- 金额不能为空，可以为正或负。
- 科目或阿米巴损益项必填其一。
- 可控预算必须有组织。

### 费用类型映射

- 费用类型必填。
- 科目编码或阿米巴损益项至少填一个。
- 启用状态下，同账套、同组织、同费用类型不能重复。

### 资金账户映射

- 账套必填。
- 银行渠道或现金银行科目至少填一个。
- 作为期初资金来源的渠道必须启用。
- 多账套共用银行账号时不能自动拆分，必须人工配置唯一归属或暂不纳入。

### 资金计划

- 计划日期必填。
- 金额必须大于 0。
- 流入流出由方向字段决定。
- 概率必须在 0 到 100 之间。
- 来源单据生成的行必须幂等。

### 预算占用

- `block` 策略下不能超过可用预算。
- 同一 CardFlow 转换事件不能重复记账。
- 释放、锁定、消耗必须记录状态变更。

## 报表与指标

### 一期 A 指标

- 可用资金；
- 未来 13 周最低期末资金；
- 首个低于安全资金的周次；
- 收入预算完成率；
- 费用预算使用率；
- 超预算单据数；
- 预算占用金额；
- 已锁定预算金额；
- 已消耗预算金额。

### 一期 B 指标

- 实际 vs 预算；
- 实际 + 预测 vs 预算；
- 当前预测 vs 上一版预测；
- 月度、累计和全年视图；
- 全年预测利润；
- 预测偏差金额和比例。

## 测试策略

### 后端单元测试

- 预算版本状态流转；
- 预算明细校验和聚合；
- 费用类型映射匹配优先级；
- 资金账户绑定的期初资金来源选择；
- 13 周每周期末资金计算；
- 预算占用幂等；
- 预算占用超预算策略。

### 后端集成测试

- 创建预算版本、批量保存预算明细、审批、查询汇总。
- 配置费用类型映射后，对费用请款 CardFlow 做预算预览。
- 模拟 CardFlow 提交、通过、驳回、撤回，检查预算占用状态。
- 配置资金账户绑定后，查询 13 周资金预测。
- 创建预测版本后，查询阿米巴实际/预算/预测对比。

### 前端测试

- API 参数形状；
- 预算版本页面操作；
- 预算明细编辑；
- 费用类型映射页面校验；
- 资金账户绑定页面校验；
- 13 周资金预测表格计算展示；
- 阿米巴报表预算/预测模式切换。

### 手工验证

- 后端构建；
- 前端构建；
- 新路由可访问；
- 预算明细汇总正确；
- 费用请款/报销 CardFlow 预算占用状态正确；
- 资金期初余额不跨账套混用；
- 13 周资金结余计算正确；
- 阿米巴预算/预测导出保留完整说明文本。

## 实施顺序

### 阶段 1：预算基础与映射

交付：

- 预算版本；
- 预算明细；
- 费用类型预算映射；
- 权限常量；
- 基础 API 和页面。

### 阶段 2：资金账户绑定与 13 周资金预测

交付：

- 资金账户绑定；
- 资金计划明细；
- 13 周资金预测；
- 财务首页资金风险卡片。

### 阶段 3：CardFlow 预算占用

交付：

- CardFlow 预算预览；
- 提交占用；
- 审批通过锁定；
- 驳回/撤回释放；
- 凭证或付款确认消耗；
- 超预算策略。

### 阶段 4：阿米巴预算与预测对比

交付：

- 预测版本；
- 预测明细；
- 阿米巴实际/预算/预测对比；
- 财务首页预测指标。

### 阶段 5：增强能力

交付：

- 预算调整流程；
- 预测准确性复盘；
- Excel 导入导出；
- 超预算追加审批分支；
- 多组织资金安全线配置。

## 仍需确认的产品决策

实施计划开始前需要明确：

- 正式预算审批是否直接使用 CardFlow，还是先在 Finance 内做轻量状态流转。
- 安全资金线按账套配置、按组织配置，还是两者都支持。
- 费用请款是否只按费用类型占用预算，还是要求用户在请款时选择预算科目/阿米巴损益项。
- 预算明细第一版是否需要 Excel 导入；如不做导入，则先做表格编辑。
- 多账套共用银行账号是否在第一版纳入；建议第一版不支持自动拆分。

## 推荐第一版实现范围

第一版建议只做阶段 1 到阶段 3：

- 预算版本管理；
- 预算明细维护；
- 费用类型预算映射；
- 资金账户绑定；
- 13 周资金预测；
- CardFlow 费用预算占用；
- 财务首页预算和资金卡片。

这能先形成“预算编制 -> 单据占用 -> 资金预测 -> 首页预警”的管理闭环。阿米巴预测对比放到下一阶段，可以避免第一版同时改预算、资金、CardFlow 和复杂报表，降低交付风险。
