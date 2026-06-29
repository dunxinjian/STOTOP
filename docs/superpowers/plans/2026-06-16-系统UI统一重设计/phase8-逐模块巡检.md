## 阶段 8 总览

本阶段是**全站功能页风格巡检对齐**：不是新建设计系统（那是阶段0/1/7的事），而是把阶段7产出的**唯一权威令牌集 + PATTERNS.md 范式**逐页落到 `web/src/views/` 下的每一个业务模块。

### 依赖前置（阶段7 必须已就位）
- 阶段0：令牌单一真源 + CSS 变量桥（`var(--color-primary)` 等可用，主色 `#E85E00`）。
- 阶段1：全局去蓝拆紫收敛（不再有 `#1677ff/#1890ff` 蓝、`#722ED1` 紫主题）。
- 阶段7：设计系统层（PageHeader/AppBreadcrumb/EmptyState/SearchBar/StatCard 等范式组件已令牌化）+ `PATTERNS.md`（页头/容器/卡片/工具栏/表格/表单/空态范式定义）已落地。

### 巡检前的现状基线（已抽样实测，用于排期与验收）
| 指标 | 实测值 | 含义 |
|---|---|---|
| `var(--token)` 在 views 中使用 | 仅 5 文件 / 30 处 | 视图层几乎未消费令牌——**本阶段主要工作量** |
| `--biz-*` 业务色使用 | **0 处** | 阶段7 建令牌，本阶段首次应用到页面 |
| 硬编码 hex 色 | **304 文件 / 3487 处** | 首要偏离；`TaskList.vue` 甚至残留蓝 `#1890ff` |
| emoji 占用 | 18 文件 / 62 处 | 需替换为 SvgIcon/Ant 图标 |
| 已接入 PageHeader | 205 文件 | 页头范式覆盖良好——巡检重点是**未接入者**与配色令牌化 |
| 工具栏 Teleport 宿主 | `AppBreadcrumb.vue` | 页内自造工具栏条需收敛至此 |

### 模块体量（决定排序，大模块在前）
express 62 · cardflow 61 · finance 46 · task 39 · system 28 · oa 24 · conference 22 · quality 16 · points 16 · crm 15 · oa-mobile 12 · dormitory 10 · workhub 10 · salary 7 · insurance 7 · vehicle 6 · contract 6 · cardflow-mobile 6 · ppv 5 · ksf 5 · supplier 2 · hr 2 · dataimport 2 · reports 1 · workflow 1。

---

### Task 1：定义"设计系统一致性巡检清单"（Conformance Checklist，一次定义，全模块复用）

本 Task 不改代码，产出一份**断言型清单**供后续每个模块 Task 逐页套用。清单挂在 PATTERNS.md（阶段7 文件）尾部作为"巡检章节"，或作为各模块 Task 的复制模板。

**Files:**
- Modify `web/PATTERNS.md`（阶段7 创建，本 Task 追加"§巡检清单"章节）

- [ ] **Step 1** 读阶段7 的 `web/PATTERNS.md` 与 `web/src/styles/variables.scss`，核对令牌名与本阶段权威令牌集逐项一致（主色 `--color-primary #E85E00`、状态四色含 `-light/-text`、文字四档、表面六项、`--biz-waybill/contract/quality/approval/points/finance`、圆角 `--radius-sm/md/lg/modal/pill`、阴影 `--shadow-sm/md/lg`、字号 `--font-xs…2xl`、间距 `--space-2xs2…2xl`）。如有缺名先记录，待阶段7 补齐再继续。
- [ ] **Step 2** 写"七维巡检清单"（每维给 ripgrep 静态断言 + 目测判据）：
  1. **页头**：是否用 `<PageHeader>` + 经 AppBreadcrumb 工具栏，无页内自造 `.page-title/.header` 大标题条。
  2. **容器/卡片**：用统一卡片范式（`a-card` 或 PATTERNS 约定 class），无自造圆角/阴影/边框组合。
  3. **工具栏**：操作按钮 Teleport 至 `#page-toolbar-actions/left`，无页内固定工具栏条。
  4. **表格**：统一 `a-table` 范式（密度/斑马/对齐/金额右对齐 mono），无自造 `<table>`。
  5. **表单**：统一 `a-form` + label 宽度/必填星范式；金额用 AmountInput。
  6. **空态**：用 `<EmptyState>`，无自造"暂无数据"div。
  7. **图标/文案**：无 emoji（改 SvgIcon/Ant 图标）。
- [ ] **Step 3** 写"令牌化巡检"子项与对应 ripgrep 断言（PowerShell，`;` 串联，`$null`）：
  - 硬编码色：`rg -n "#[0-9a-fA-F]{3,6}" web/src/views/<模块>` 期望→仅剩注释/SVG 必要值，业务色/状态色/文字/边框全部 `var(--...)`。
  - 残留蓝紫：`rg -n "#1677ff|#1890ff|#722ED1|#52C41A" web/src/views/<模块>` 期望→0 命中。
  - 状态色：`rg -n "color-(success|warning|danger|info)" web/src/views/<模块>` 应走 `var(--color-success)` 等。
  - 业务色：列表/单证/质量/审批/积分/财务相关强调处应用 `var(--biz-*)`。
  - 圆角/字号/间距：`rg -n "border-radius:\s*\d|font-size:\s*\d+px|padding:\s*\d+px" web/src/views/<模块>` 期望→收敛为 `var(--radius-*/--font-*/--space-*)`。
  - emoji：`rg -nP "[\x{1F300}-\x{1FAFF}\x{2600}-\x{27BF}]" web/src/views/<模块>` 期望→0 命中。
- [ ] **Step 4** 写"WCAG 抽检"子项：每模块至少抽 1 页，目测主文字对底（`--text-1`/`--bg-card`）≥4.5:1、次要文字（`--text-3`）用于非关键信息、橙色按钮文字白底对比达标（沿用阶段1 已校 `#E85E00`）；记录抽检页路径与结论。
- [ ] **Step 5** 写"每模块验证套路"（后续 Task 复用）：①列页面清单→②逐页套七维清单+令牌化收敛→③`cd web; npm run build` 须过（type-check 基线红不作门禁）→④关键页 `preview_start`/`preview_screenshot`（必要 `preview_resize` 移动页）截图比对→⑤`git add -A; git commit`（中文 message + 结尾 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`）。
- [ ] **Step 6** `git add web/PATTERNS.md; git commit`，message：`docs(ui): 新增设计系统一致性巡检清单（七维+令牌化断言+WCAG抽检）`。

---

### Task 2：express 模块巡检收敛（62 页，最大模块优先）

**Files:**（Modify，按子目录分组逐页；关键页单列）
- `web/src/views/express/quotation/`：`QuotationWorkbench.vue`、`QuotationList.vue`、`QuotationEdit.vue`、`ImportQuotationModal.vue` 及 `components/*`（BasicInfoCard/BatchFillToolbar/CellOverrideDialog/ChangeLogPanel/ClientPanel/CommissionConfigPanel/FixedPriceCostMatrix/PriceCellInput/PriceMatrixTable/SettingsDrawer/SharedAliasPanel/ShopPanel/ShopQueryPanel/SurchargeDrawer/WeightSegmentEditor/WeightSegmentPanel）
- `web/src/views/express/cost-plan/`：`CostPlanList.vue`、`CostPlanEdit.vue`、`CostItemList.vue`、`CostItemDetail.vue` 及 `components/*`（CityPriceMatrix/CostItemSidebar/CostItemToolbar/CostMatrixTable/NationalPriceRow/SegmentToolbar）
- `web/src/views/express/quality-center/`：`Dashboard.vue`、`Overview.vue`、`EmptyShopRows.vue`、`NetworkPointMismatch.vue`、`PendingShops.vue`、`UnrecognizedNetworkPoints.vue`
- `web/src/views/express/`其余：`agent/AgentManage.vue`、`billing/{BillingDispute,BillingErrors,BillingRecalc}.vue`、`components/{BatchShopModal,QuotationDrawer,QuotationEditModal}.vue`、`dashboard/Dashboard.vue`、`franchise-area/FranchiseAreaManage.vue`、`invoice/{InvoiceDetail,InvoiceList,ReviewRules}.vue`、`last-mile-station/LastMileStationManage.vue`、`network-point/{AliasManagement,NetworkPointManage}.vue`、`policy-rebate/{PolicyRebateList,RebateSettlement,RebateSimulation}.vue`、`prepayment/PrepaymentManage.vue`、`province/ProvinceManage.vue`、`report/{FlowAnalysis,ProfitAnalysis,WeightSegment}.vue`、`surcharge/{SurchargeEdit,SurchargeList}.vue`、`waybill-number/WaybillNumberManage.vue`

- [ ] **Step 1** `rg -n "#[0-9a-fA-F]{3,6}" web/src/views/express`（基线 ~21 文件命中：dashboard/Dashboard 21、FixedPriceCostMatrix 41、CostMatrixTable 30、WeightSegmentEditor 38、CityPriceMatrix 15 等）；`rg -nP "[\x{1F300}-\x{1FAFF}\x{2600}-\x{27BF}]" web/src/views/express`（QuotationEditModal/FixedPriceCostMatrix/WeightSegmentEditor 含 emoji）。记录命中清单作为本模块工作面。
- [ ] **Step 2** 套用 Task 1 七维清单逐页：先收敛大命中矩阵表格组件（FixedPriceCostMatrix/CostMatrixTable/CityPriceMatrix），把 `<style>` 内硬编码色改 `var(--...)`，价格强调用 `var(--biz-waybill)`（快递单证业务色 `#6B4FB0`）。**改前必读真实文件给"当前→目标"代码**，例如读出 `border-radius: 4px` → `var(--radius-sm)`、自造蓝 → `var(--color-info)`。
- [ ] **Step 3** quotation/cost-plan 工具栏（BatchFillToolbar/SegmentToolbar/CostItemToolbar）核对是否页内自造——若属页级操作迁至 `#page-toolbar-actions`，否则保留但令牌化样式。
- [ ] **Step 4** emoji 三页改 SvgIcon/Ant 图标；空态自造处替换 `<EmptyState>`。
- [ ] **Step 5** `cd web; npm run build` 须过。复测 `rg -n "#1677ff|#1890ff|#722ED1" web/src/views/express` 期望 0；`rg -nP "[\x{1F300}-\x{1FAFF}\x{2600}-\x{27BF}]" web/src/views/express` 期望 0。
- [ ] **Step 6** 关键页 preview 截图比对：`QuotationWorkbench`、`cost-plan/CostPlanEdit`、`quality-center/Overview`、`dashboard/Dashboard`（`preview_start`→`preview_screenshot`），目测页头/卡片/表格/配色符合范式，WCAG 抽检 `dashboard/Dashboard`。
- [ ] **Step 7** `git commit`，message：`style(express): 全模块功能页风格巡检对齐（62页令牌化+去蓝拆紫+清emoji）`。

---

### Task 3：cardflow 模块巡检收敛（61 页）

**Files:**（Modify，分组）
- 顶层（20 页）：`AuditLogPage/BatchManage/CardApprovePage/CardDetailPage/CardFlowHome/CardFlowMonitorPage/DelegationPage/ExpenseClassification/FlowDefinitionEditPage/FlowDefinitionListPage/FlowGroupEditPage/FlowGroupListPage/NotificationSettingsPage/OrchestrationDetailPage/OrchestrationInstanceDetailPage/OrchestrationInstanceListPage/OrchestrationListPage/TodoStatsPage/VersionHistoryPage/index`.vue
- 子目录：`upload/*`（UploadCenter 36、ClassificationResults、AutoPluginTrailPanel、`components/*` BatchCard/BatchFilterBar/BatchMiniStepper/BatchStatsBar/ChainComments/ChainTimeline/UploadDropZone）、`auto-plugin/*`（AutoPluginList/AutoPluginRuleList/AutoVoucherRuleForm/ExcelInputRuleForm/CopyRuleFromOrgDialog/AutoPluginRuleFormDialog 及 `auto-voucher/*` 向导与 components）、`import-validation/ImportCalculationValidationWorkbench.vue`(49)、`issues/IssueWorktable.vue`、`automation/{FlowDesigner,AutomationList}.vue`、`file-manager/FileManager.vue`、`staging/StagingBrowser.vue`、`quality/QualityRuleEditor.vue`、`hangfire/HangfirePanel.vue`

- [ ] **Step 1** `rg -n "#[0-9a-fA-F]{3,6}" web/src/views/cardflow`（重命中：FlowDefinitionEditPage 199、ImportCalculationValidationWorkbench 49、CardDetailPage 49、CardFlowMonitorPage 39、UploadCenter 36、OrchestrationDetailPage 33）。`rg -nP "[emoji]" web/src/views/cardflow`（CardDetailPage/FlowDefinitionEditPage/DryRunPanel/FieldValuesPanel/BatchMiniStepper 含 emoji）。
- [ ] **Step 2** 套七维清单逐页；审批/流程强调处应用 `var(--biz-approval)`（`#3A6FB0`）。优先攻 FlowDefinitionEditPage（199 处，流程设计器画布配色最多）：读真实 `<style>` 给"当前→目标"，状态节点色映射状态四色令牌（成功/进行/异常→`--color-success/info/danger`）。
- [ ] **Step 3** TodoStatsPage/CardFlowMonitorPage 已部分用 `var(--biz-*)`（实测命中），核对是否齐全；统计卡收敛 StatCard。
- [ ] **Step 4** emoji 五处改图标；upload 链路时间线（ChainTimeline/BatchMiniStepper）配色令牌化。
- [ ] **Step 5** `cd web; npm run build` 须过；`rg -n "#1677ff|#1890ff|#722ED1" web/src/views/cardflow` 期望 0；emoji 复测 0。
- [ ] **Step 6** 关键页 preview：`CardFlowHome`、`FlowDefinitionEditPage`(设计器画布)、`upload/UploadCenter`、`import-validation/ImportCalculationValidationWorkbench`，截图比对+WCAG 抽检 `CardFlowMonitorPage`。
- [ ] **Step 7** `git commit`：`style(cardflow): 61页功能页风格巡检对齐（含流程设计器/上传链路令牌化）`。

---

### Task 4：finance 模块巡检收敛（46 页）

**Files:**（Modify，全 44 文件 + AmoebaPLTemplate 子目录）顶层报表/凭证/账套/预算/资金/阿米巴：`AccountBalanceReport/AccountDetailReport/AccountManage/AccountSetAuth/AccountSetManage/AccountTemplateManage/AmoebaAllocation/AmoebaClassify/AmoebaPL/AmoebaPLTemplate/AssetBalanceReport/AssetSettings/AuxiliaryAliasConfig/AuxiliaryBalanceReport/AuxiliarySetting/BalanceSheet/BankChannelManage/BankReconciliation/BankTransactionManage/BudgetExpenseMapping/BudgetLineEditor/BudgetVersionManage/CashFlowReport/ExchangeRateManage/FinanceHome/FormulaConfig/InvoiceManage/Journal/MigrationConfig/MigrationWizard/OperationLog/PeriodClosing/ProfitStatement/TaxPayableReport/TreasuryAccountBinding/TreasuryRollingForecast/VoucherEntry/VoucherList/VoucherPrint/VoucherRuleManage/VoucherTemplateManage`.vue + `components/{EstimateDataDialog,VoucherStatusFlow}.vue` + `AmoebaPLTemplate/{AccountCodePicker,HelperPanel}.vue` + `index.vue`

- [ ] **Step 1** `rg -n "#[0-9a-fA-F]{3,6}" web/src/views/finance`（重命中：AmoebaPL 106、AmoebaPLTemplate 97、VoucherEntry 93、HelperPanel 64、AuxiliarySetting 53、FinanceHome 39、PeriodClosing 36）。注意 `finance/AmoebaPLTemplate/tokens.scss` 是该页**自造局部令牌**——巡检判断是否应迁并入全局令牌或保留为业务专用。
- [ ] **Step 2** 套七维清单；财务强调/金额高亮用 `var(--biz-finance)`（`#B8860B`），金额数字统一 mono 右对齐。优先攻 AmoebaPL/AmoebaPLTemplate/VoucherEntry（命中最多）：读真实 `<style>`/`tokens.scss` 给"当前→目标"，把局部 hex 映射到全局令牌；凭证借贷/平衡校验色用状态四色。
- [ ] **Step 3** HelperPanel 含 25 处 emoji（最高）——批量改 SvgIcon/Ant 图标；其余 emoji 页同改。
- [ ] **Step 4** 报表类页（BalanceSheet/ProfitStatement/CashFlowReport/各 BalanceReport）表格统一 `a-table` 范式 + 空态 `<EmptyState>`；FinanceHome 统计卡收敛 StatCard。
- [ ] **Step 5** `cd web; npm run build` 须过；`rg -n "#1677ff|#1890ff|#722ED1" web/src/views/finance` 期望 0；emoji 复测 0。
- [ ] **Step 6** 关键页 preview：`FinanceHome`、`AmoebaPL`、`VoucherEntry`、`BalanceSheet`，截图比对+WCAG 抽检 `BalanceSheet`（报表密集文字）。
- [ ] **Step 7** `git commit`：`style(finance): 46页功能页风格巡检对齐（阿米巴/凭证/报表令牌化+清25处emoji）`。

---

### Task 5：task 模块巡检收敛（39 页）

**Files:**（Modify，全 39 文件）顶层：`GoalDetail/GoalList/GoalTree/KnowledgeCreate/KnowledgeDetail/KnowledgeHot/KnowledgeList/KnowledgeMyCollections/MyPerformance/MyTasks/NotificationCenter/PerformanceDimensions/PerformanceEvaluation/PerformancePeriods/ProjectDetail/ProjectList/ReviewDetail/ReviewList/ScheduleManage/TagManage/TaskCalendar/TaskDashboard/TaskDetail/TaskKanban/TaskList/TaskQuery`.vue + `components/*`（AttachmentUpload/CommentReactions/DingTalkPushBtn/NotificationBell/PriorityTag/ProgressReport/ProgressTimeline/SubTaskList/TaskComment/TaskForm/TaskStatusFlow/VisibilityConfig）+ `index.vue`

- [ ] **Step 1** 读 `web/src/views/task/TaskList.vue` 第 502–527 行（已确认偏离样本）：`background:#fafafa`→`var(--bg-page)`、`#e6f7ff`→`var(--color-info-light)`、`color:#333`→`var(--text-1)`、`color:#1890ff`（残留蓝）→`var(--color-primary)`或语义色、`#ff4d4f`→`var(--color-danger)`。作为本模块"当前→目标"教学样本逐行改。
- [ ] **Step 2** `rg -n "#[0-9a-fA-F]{3,6}" web/src/views/task`（重命中：TaskKanban 29、NotificationBell 28、ReviewDetail 15、TagManage 14）。看板/优先级标签（TaskKanban/PriorityTag）状态色映射状态四色令牌。
- [ ] **Step 3** 套七维清单逐页；TaskList 筛选 a-row（第24–60行自造）按 PATTERNS 决定是否收敛 SearchBar；空态替换 `<EmptyState>`；CommentReactions emoji（表情反应若为业务功能则保留，纯装饰 emoji 才清）。
- [ ] **Step 4** `cd web; npm run build` 须过；`rg -n "#1890ff|#1677ff|#722ED1" web/src/views/task` 期望 0。
- [ ] **Step 5** 关键页 preview：`TaskList`、`TaskKanban`、`TaskDashboard`、`GoalTree`，截图比对+WCAG 抽检 `TaskKanban`（多状态色）。
- [ ] **Step 6** `git commit`：`style(task): 39页功能页风格巡检对齐（看板/列表令牌化+TaskList残留蓝修复）`。

---

### Task 6：system 模块巡检收敛（28 页，含 admin 紫灰外壳页）

**Files:**（Modify，全 29 文件）顶层：`AdminConfigCenter/ChangeLogList/CodeRuleManage/DatabaseConfig/DatabaseSetup/DbConnectionManage/DbMigrationManage/DingTalkConfig/EnterpriseInfo/FeedbackCenter/MenuManage/OrgChart/Organization/PositionManage/RoleManage/ThemeConfig/UserManage/UserProfile`.vue + `security/{AuditLog,OnlineSessions,SecurityConfig}.vue` + `theme/{ColorConfig,ModeConfig,SidebarConfig,SizeConfig,SpacingConfig,TableConfig}.vue` + `index.vue`

- [ ] **Step 1** `rg -n "#[0-9a-fA-F]{3,6}" web/src/views/system`（重命中：DatabaseSetup 40、FeedbackCenter 31、DingTalkConfig 21、AssetSettings 类、OrgChart 12）。**注意 theme/* 与 ThemeConfig 是主题配置页**——其展示色板的 hex 是功能数据非样式，巡检需区分"展示用 hex（保留）"vs"样式硬编码（令牌化）"。
- [ ] **Step 2** 套七维清单；admin 页配色走阶段7 admin 令牌（`--topbar-ink-admin #171A22`），不得再现旧 `#722ED1` 紫主题硬编码。emoji（ThemeConfig 6、EnterpriseInfo/DatabaseSetup/OrgChart）改图标。
- [ ] **Step 3** OrgChart/MenuManage 树形/组织图自造连线色令牌化；FeedbackCenter 卡片收敛范式。
- [ ] **Step 4** `cd web; npm run build` 须过；`rg -n "#722ED1|#1890ff|#1677ff" web/src/views/system` 期望→仅 theme 色板展示残留（人工确认非样式）。
- [ ] **Step 5** 关键页 preview：`UserManage`、`RoleManage`、`AdminConfigCenter`、`theme/ColorConfig`，截图比对+WCAG 抽检 `UserManage`。
- [ ] **Step 6** `git commit`：`style(system): 28页功能页风格巡检对齐（admin外壳令牌化+区分主题色板数据）`。

---

### Task 7：oa 模块巡检收敛（24 页，含审批提交表单）

**Files:**（Modify）`OaHome/index.vue` + `calendar/{CalendarIndex,components/*}`（AttendeeSelector/BoardView/CalendarView/EventDetail/EventForm/RecurrenceEditor）+ `config/{DelegationConfig,ExpenseAccountMapping,ExpenseTypeConfig}.vue` + `ledger/{LoanLedger,PettyCashLedger}.vue` + `statistics/ProcessStatistics.vue` + `submit/*`（ExpenseReimburseSubmit/ExpenseRequestSubmit/ExternalPaymentSubmit/LoanApplySubmit/PettyCashApplySubmit/PettyCashReimburseSubmit/PettyCashReturnSubmit/PettyCashWriteOffSubmit/SalaryAdvanceSubmit）

- [ ] **Step 1** `rg -n "#[0-9a-fA-F]{3,6}" web/src/views/oa`（重命中：ProcessStatistics 24、EventDetail/EventForm/AttendeeSelector 日历组件）。
- [ ] **Step 2** 套七维清单；审批流程统计/状态用 `var(--biz-approval)` + 状态四色；日历视图（CalendarView/BoardView）事件色令牌化；submit/* 表单统一 `a-form` 范式 + 金额 AmountInput。
- [ ] **Step 3** ProcessStatistics 图表色走 charts 令牌（与阶段7 图表色板一致）。
- [ ] **Step 4** `cd web; npm run build` 须过；`rg -n "#1890ff|#1677ff" web/src/views/oa` 期望 0。
- [ ] **Step 5** 关键页 preview：`OaHome`、`calendar/CalendarIndex`、`submit/ExpenseReimburseSubmit`、`statistics/ProcessStatistics`，截图比对+WCAG 抽检 `ExpenseReimburseSubmit`（表单）。
- [ ] **Step 6** `git commit`：`style(oa): 24页功能页风格巡检对齐（日历/审批提交表单令牌化）`。

---

### Task 8：conference 模块巡检收敛（22 页，工作台多面板）

**Files:**（Modify）`ConferenceHome/EventList/EventWorkbench.vue` + `components/{GanttChart,RoomCard,RoundTable,SmartActionBar,StatCard,TimelineView}.vue` + `panels/*`（AccommodationPanel/AttendeePanel/BasicInfoPanel/DashboardPanel/FinancePanel/GiftPanel/MaterialPanel/MealPanel/RundownPanel/SchedulePanel/TablePanel/TransportPanel/VehicleSchedulePanel）

- [ ] **Step 1** `rg -n "#[0-9a-fA-F]{3,6}" web/src/views/conference`（重命中：TablePanel 43、GanttChart 19、DashboardPanel 15、AttendeePanel 13、RoundTable 13）。EventList 含 1 处 emoji。
- [ ] **Step 2** 套七维清单；甘特图/圆桌图/时间线（GanttChart/RoundTable/TimelineView）自造画布色令牌化；EventWorkbench 多面板容器统一卡片范式；conference 自有 StatCard 与全局 StatCard 比对收敛。
- [ ] **Step 3** EventList emoji 改图标；DashboardPanel 统计收敛 StatCard。
- [ ] **Step 4** `cd web; npm run build` 须过；emoji+蓝紫复测 0。
- [ ] **Step 5** 关键页 preview：`ConferenceHome`、`EventWorkbench`(含 GanttChart)、`EventList`，截图比对+WCAG 抽检 `EventWorkbench`。
- [ ] **Step 6** `git commit`：`style(conference): 22页功能页风格巡检对齐（甘特/圆桌画布令牌化）`。

---

### Task 9：quality 模块巡检收敛（16 页）

**Files:**（Modify，全 16 文件）`dashboard/QualityDashboard.vue` + `exceptions/{ExceptionAnalysis,ExceptionList}.vue` + `knowledge/{CaseLibrary,SopDocument}.vue` + `performance/{PerformanceStats,ProcessingLog}.vue` + `review/{ImprovementTrack,ReviewPlan,ReviewRecord}.vue` + `rules/{AlertRules,DetectionRules,DispatchRules,IssueTypeConfig,QualityRuleEditor,QualityRuleList}.vue`

- [ ] **Step 1** `rg -n "#[0-9a-fA-F]{3,6}" web/src/views/quality`（命中：QualityDashboard 31、ExceptionAnalysis 12、PerformanceStats 11）。
- [ ] **Step 2** 套七维清单；质量异常/告警强调用 `var(--biz-quality)`（`#D9603A`）+ 状态四色（异常红/合格绿）；图表（QualityDashboard/PerformanceStats/ExceptionAnalysis）走图表令牌。
- [ ] **Step 3** rules/* 规则编辑器表单统一范式；知识库（CaseLibrary/SopDocument）卡片+空态收敛。
- [ ] **Step 4** `cd web; npm run build` 须过；蓝紫复测 0。
- [ ] **Step 5** 关键页 preview：`QualityDashboard`、`exceptions/ExceptionList`、`rules/QualityRuleEditor`，截图比对+WCAG 抽检 `QualityDashboard`。
- [ ] **Step 6** `git commit`：`style(quality): 16页功能页风格巡检对齐（biz-quality业务色+异常状态色）`。

---

### Task 10：points 模块巡检收敛（16 页）

**Files:**（Modify，全 16 文件）`ManagerQuota/PointApplication/PointDashboard/PointRanking/PointRecords/PointRules/PointSources/RedeemManage/RedeemShop`.vue + `components/{ApplicationForm,AwardDeductForm,PointBadge,PointCard,PointTrend,RankingList,RedeemCard}.vue`

- [ ] **Step 1** `rg -n "#[0-9a-fA-F]{3,6}" web/src/views/points`（命中：PointDashboard 28、RankingList 11、ManagerQuota 7、RedeemShop 8）。
- [ ] **Step 2** 套七维清单；积分徽章/排行/积分卡（PointBadge/RankingList/PointCard/RedeemCard）强调用 `var(--biz-points)`（`#C99A2E`）+ `--color-warning`族；趋势图（PointTrend）走图表令牌。
- [ ] **Step 3** PointDashboard 统计卡收敛 StatCard；RedeemShop 商品卡统一卡片范式+空态。
- [ ] **Step 4** `cd web; npm run build` 须过；蓝紫复测 0。
- [ ] **Step 5** 关键页 preview：`PointDashboard`、`PointRanking`、`RedeemShop`，截图比对+WCAG 抽检 `PointDashboard`。
- [ ] **Step 6** `git commit`：`style(points): 16页功能页风格巡检对齐（biz-points业务色+排行/徽章令牌化）`。

---

### Task 11：crm 模块巡检收敛（15 页）

**Files:**（Modify，全 15 文件）`BonusManage/CrmDashboard/CustomerDetail/CustomerManage/FeedbackManage/FeedbackStats/GroupManage/PrepaymentManage/ProfitAnalysis/ReferralManage/ReferralStats/ServiceOrderManage/ServiceOrderStats/VisitManage/WaybillPoolManage`.vue

- [ ] **Step 1** `rg -n "#[0-9a-fA-F]{3,6}" web/src/views/crm`（命中：CustomerManage 23、CrmDashboard 16、CustomerDetail 6、ProfitAnalysis 5）。
- [ ] **Step 2** 套七维清单；CrmDashboard/各 *Stats/ProfitAnalysis 图表走图表令牌、统计卡收敛 StatCard；WaybillPoolManage 单号相关强调用 `var(--biz-waybill)`。CustomerDetail 详情页卡片范式。
- [ ] **Step 3** CustomerManage（命中最多）逐页令牌化 + 空态 `<EmptyState>`。
- [ ] **Step 4** `cd web; npm run build` 须过；蓝紫复测 0。
- [ ] **Step 5** 关键页 preview：`CrmDashboard`、`CustomerManage`、`CustomerDetail`，截图比对+WCAG 抽检 `CrmDashboard`。
- [ ] **Step 6** `git commit`：`style(crm): 15页功能页风格巡检对齐（仪表盘/客户管理令牌化）`。

---

### Task 12：oa-mobile + cardflow-mobile 移动端巡检收敛（18 页，需移动视口验证）

**Files:**（Modify）
- `web/src/views/oa-mobile/approve/components/{MobileApproveBar,MobileTimeline}.vue` + `approve/forms/{MExpenseReimburseForm,MExpenseRequestForm,MExternalPaymentForm,MLoanApplyForm,MPettyCashApplyForm,MPettyCashReimburseForm,MPettyCashReturnForm,MPettyCashWriteOffForm,MSalaryAdvanceForm}.vue`
- `web/src/views/cardflow-mobile/{CardApprovalView,CardDetailView,CardFillForm,FlowSelectPage,MobileCardApprovalPage,MobileCardFillPage}.vue`

- [ ] **Step 1** `rg -n "#[0-9a-fA-F]{3,6}"` 两目录（命中：MExpenseReimburseForm 61、MExpenseRequestForm 55、MPettyCashReimburseForm 26、MExternalPaymentForm 23、MobileCardApprovalPage 26、MPettyCashWriteOffForm 19、MobileCardFillPage 14）。
- [ ] **Step 2** 套七维清单（移动端版）；移动表单容器走 MobileFormContainer/DynamicFormMobile 范式；审批条/时间线（MobileApproveBar/MobileTimeline）状态色令牌化；金额 AmountInput。
- [ ] **Step 3** `cd web; npm run build` 须过；蓝紫复测 0。
- [ ] **Step 4** 关键页 preview **移动视口**：`preview_resize` 至 ~390×844，截图 `MExpenseReimburseForm`、`MobileCardApprovalPage`、`MobileCardFillPage`，比对触控热区/字号/配色；WCAG 抽检 `MobileCardApprovalPage`。
- [ ] **Step 5** `git commit`：`style(mobile): oa-mobile+cardflow-mobile 18页移动功能页风格巡检对齐`。

---

### Task 13：中型模块批次巡检收敛（dormitory 10 + workhub 10 + salary 7 + insurance 7）

**Files:**（Modify，按模块分组提交）
- dormitory：`BuildingManage/DormitoryDashboard/DormitoryHome/ExpenseManage/FacilityManage/HygieneCheckManage/RepairOrderManage/ResidenceManage/RoomManage/VisitorManage`.vue
- workhub：`ImportIssueDetail/QualityAlertBar/QualitySummaryCard/TriggerActionPanel/WorkHubCenter/WorkHubDetail/WorkHubRecentVisits/WorkItemCard/WorkItemSkeleton/index`.vue（注：workhub 已部分用 `var(--biz-*)`，命中 6 处）
- salary：`PromotionReviews/PromotionRules/SalaryArchives/SalaryDashboard/SalaryGrades/SalaryMyPayslip/SalaryPayrolls`.vue
- insurance：`ApprovalConfig/ClaimManage/CompanyManage/FundManage/PolicyManage/ReportDashboard/SettlementManage`.vue

- [ ] **Step 1** 四模块各跑 `rg -n "#[0-9a-fA-F]{3,6}"`（命中：DormitoryDashboard 19、WorkHubCenter 51、WorkItemCard 27、WorkHubDetail 27、QualitySummaryCard 13）+ emoji（WorkHubCenter 5、WorkHubDetail 3）。
- [ ] **Step 2** 逐模块套七维清单；workhub 已有 `var(--biz-*)` 基础，补齐 + 清 emoji；各 Dashboard/Home 统计卡收敛 StatCard；salary 工资条表格 mono 金额右对齐。
- [ ] **Step 3** `cd web; npm run build` 须过；四模块蓝紫+emoji 复测 0。
- [ ] **Step 4** 每模块至少 1 关键页 preview：`DormitoryDashboard`、`WorkHubCenter`、`SalaryDashboard`、`insurance/ReportDashboard`，截图比对。
- [ ] **Step 5** 分模块 4 次 `git commit`（或合并 1 次），message 例：`style(dormitory+workhub+salary+insurance): 34页功能页风格巡检对齐`。

---

### Task 14：小模块收尾巡检收敛（vehicle 6 / contract 6 / ppv 5 / ksf 5 / supplier 2 / hr 2 / dataimport 2 / reports 1 / workflow 1 / error / 404）

**Files:**（Modify）
- vehicle：`AssignmentManage/GpsTracking/MaintenanceManage/RentalChargeManage/VehicleDashboard/VehicleManage`.vue
- contract：`ContractDashboard/ContractList/ContractTemplateManage/ContractTypeManage/ESignManage`.vue + `components/ContractStatusFlow.vue`（合同强调用 `var(--biz-contract)` `#8A6D3B`）
- ppv：`PpvDashboard/PpvMyProgress/PpvRecords/PpvResults/PpvTemplates`.vue
- ksf：`KsfDashboard/KsfIndicators/KsfMyProgress/KsfPlans/KsfResults`.vue
- supplier：`SupplierHome/SupplierManage`.vue · hr：`EmployeeRoster/HRHome`.vue · dataimport：`upload/ClassificationResults.vue` 等 · reports：`ReportsHome.vue` · workflow：`Dashboard.vue` · error：`Forbidden403.vue` · `404/index.vue`

- [ ] **Step 1** 各模块 `rg -n "#[0-9a-fA-F]{3,6}"`（命中：VehicleDashboard 19、ContractDashboard 19、ClassificationResults 5、Forbidden403 7）。
- [ ] **Step 2** 套七维清单；contract 强调 `var(--biz-contract)` + ContractStatusFlow 状态四色；vehicle/ksf/ppv 各 Dashboard 统计卡收敛 StatCard；error/404 页配色令牌化。
- [ ] **Step 3** `cd web; npm run build` 须过；蓝紫复测 0。
- [ ] **Step 4** 关键页 preview：`VehicleDashboard`、`ContractList`、`KsfDashboard`、`404/index`，截图比对。
- [ ] **Step 5** `git commit`：`style(small-modules): vehicle/contract/ppv/ksf 等小模块功能页风格巡检对齐`。

---

### Task 15：全站一致性最终抽检与收尾

**Files:**（Modify，仅在发现遗漏时回写）任意巡检未达标页 + `web/PATTERNS.md`（追加"巡检完成度记录表"）

- [ ] **Step 1** 全站静态总检：`rg -c "#[0-9a-fA-F]{3,6}" web/src/views` 与基线 3487 处对比，给出收敛率；`rg -n "#1677ff|#1890ff|#722ED1" web/src/views` 期望→0（蓝紫彻底清除，theme 色板展示数据除外人工标注）；`rg -nP "[\x{1F300}-\x{1FAFF}\x{2600}-\x{27BF}]" web/src/views` 期望→0（业务功能性表情除外，逐一标注）。
- [ ] **Step 2** `--biz-*` 落地核查：`rg -c "var\(--biz-waybill\)" web/src/views/express`、`--biz-finance`/finance、`--biz-approval`/cardflow+oa、`--biz-quality`/quality、`--biz-points`/points、`--biz-contract`/contract 各 >0，证明业务色已应用（基线为 0）。
- [ ] **Step 3** **每模块至少一页符合范式**抽检清单（逐项 preview 截图归档）：express→QuotationWorkbench、cardflow→CardFlowHome、finance→FinanceHome、task→TaskList、system→UserManage、oa→OaHome、conference→ConferenceHome、quality→QualityDashboard、points→PointDashboard、crm→CrmDashboard、dormitory→DormitoryDashboard、workhub→WorkHubCenter、salary→SalaryDashboard、insurance→ReportDashboard、vehicle→VehicleDashboard、contract→ContractList、ppv→PpvDashboard、ksf→KsfDashboard、移动端→MobileCardApprovalPage。逐页对照七维清单全绿。
- [ ] **Step 4** WCAG 汇总：汇总各模块 Step 抽检结论，列不达标项（若有）回写修复；确认主文字/次要文字/橙色按钮三类对比度达标。
- [ ] **Step 5** `cd web; npm run build` 最终须过；在 PATTERNS.md 追加"阶段8 巡检完成度记录表"（模块×七维×达标）。
- [ ] **Step 6** `git commit`：`docs(ui): 阶段8 全站风格巡检完成度记录与最终抽检（每模块至少一页符合范式）`。

---

## 执行注意
- **只读样本已确认的真实偏离**：`TaskList.vue` 第502-527行硬编码（含蓝 `#1890ff`）、`PageHeader.vue` 第129行蓝 `#1677ff`、`EmptyState.vue` 第55/93/99行 `rgba(0,0,0,*)`、`variables.scss` 旧调色板（阶段7 桥接）、`finance/AmoebaPLTemplate/tokens.scss` 自造局部令牌。执行子智能体改任何页前**必先 Read 真实行**再给"当前→目标"，禁止占位符。
- **区分"样式色"与"数据色"**：system/theme/*、ThemeConfig 的色板 hex、各图表 series 色、SVG 资源色属于功能数据，巡检不强制令牌化，但需在记录表标注豁免。
- **emoji 豁免**：业务功能性表情（如 CommentReactions 表情反应）保留，仅清装饰性 emoji。
- 全程 `var(--token)` 名称严格用权威令牌集精确名（如 `--color-primary`/`--biz-finance`/`--radius-sm`/`--font-base`/`--space-md12`），不得臆造别名。