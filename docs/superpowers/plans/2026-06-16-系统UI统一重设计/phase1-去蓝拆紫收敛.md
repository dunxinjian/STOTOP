## 阶段1：全局去蓝 / 拆紫 / 同义多值收敛（令牌迁移扫荡）

> **前置依赖（阶段0 必须已完成，否则本阶段全部失败）**：经实测，当前仓库 `web/src` 中**尚无任何 `:root` CSS 变量定义**（`rg ":root|--color-" web/src/styles` 0 命中），`web/src/views/finance/AmoebaPLTemplate/tokens.scss` 是局部私有 token 文件而非全局令牌。本阶段所有 `var(--color-*)/var(--biz-*)/var(--bg-page)` 均假定由阶段0 在全局 `:root`（如 `web/src/styles/tokens.scss` 并被 `index.scss` `@use`）定义就绪。**阶段0 同时已配置 stylelint 防回潮**（实测当前无 `.stylelintrc*`），本阶段不重复造防护，只做迁移。
>
> **运行时主题事实**：`web/src/App.vue:47` 使用 `themeStore.antdTheme`（来自 `stores/theme.ts`）。`web/src/config/theme.ts` 的 `antThemeConfig` **未被任何地方引用**（`rg "antThemeConfig" web/src` 仅命中自身），是死配置——其 `#409eff` 等是 rg 残留来源，需一并清理但不影响运行时。

### 语义二分总则（每批通用，先判语义再选令牌）

| 原始色 | 出现语义 | 判定 → 目标令牌 |
|---|---|---|
| 蓝 #1677ff/#1890ff/#409eff、rgba(24,144,255,*)、#096dd9 | `&:hover{color}`、激活页签 `box-shadow: inset 0 N 0 0 蓝`、选中 `border-color`、链接 `<a style="color:蓝">`、次操作按钮、勾选/激活背景 | **交互态 → `var(--color-primary)`** 系（hover 用 `--color-primary-hover`，active/选中底用 `--color-primary-light`，选中边框用 `--color-primary-border`） |
| 蓝 同上 | KPI 统计数字色、信息标签、`$tone-info-fg` 之类"信息前景"局部变量、`border-left: 3px solid 蓝` 的信息条 | **纯信息态 → `var(--color-info)`**（浅底 `--color-info-light`，文字 `--color-info-text`） |
| 金黄 #faad14/#fa8c16/#d4b106 | "警告/即将到期/超时"语义 | **`var(--color-warning)`** 系 |
| 金黄 #d4b106、#faad14 | "积分/points" 域身份色（如 workhub `points:{color}`） | **`var(--biz-points)`**（域分类色，非警告语义） |
| 红 #f5222d、#FF4D4F | 错误/负数/删除/失败 | **`var(--color-danger)`** 系 |
| 青 #13C2C2 | info/信息分类 | **`var(--color-info)`**（青统一并入蓝信息色） |
| 紫 #722ED1 | **业务**运单/物流/CardFlow 分类（workhub `datacenter`、express `运单` KPI、运单池） | **`var(--biz-waybill)`** |
| 紫 #722ED1 | 后台主题（`AdminLayout.vue`、`variables.scss` 的 `$admin-*`） | **本阶段跳过，归阶段5** |
| 内容灰 #f4f6f8/#F0F2F5/#F4F5F7 | 页面/卡片内容区底 | **`var(--bg-page)`**（`#F4F5F7` 在 admin 上下文者跳过归阶段5，仅迁内容区用途） |

**图表分类调色板（重要排除项）**：实测 23 行形如 `const colors = ['#1890ff','#52c41a','#fa8c16',...]`（ECharts 分类色板）。这些是**数据可视化分类色**而非 UI 语义色，且 ECharts JS 数组内 `var(--x)` 不会被解析（需 `getComputedStyle` 运行时解析=结构改造，超出"只迁颜色"范围）。**本阶段不迁色板数组**，并在每批 rg 验证中显式排除这些行；遗留作阶段5/后续专项。每批末尾 rg 命令用 `--glob '!**/charts/**'` 与正则排除 `\[.*#.*,.*#` 多色板行后断言归零。

---

### Task 1: 基线 SCSS 变量与死配置收口（variables.scss / ant-override.scss / config/theme.ts）

**Files:**
- Modify `web/src/styles/variables.scss`（L5-6 颜色定义、L122-139 admin/content 区）
- Modify `web/src/styles/ant-override.scss`（L283-291 `.toolbar-btn`）
- Modify `web/src/config/theme.ts`（L5-9 死配置 token）

- [ ] **Step 1: 读取并改 variables.scss 状态色基线值。** 当前 `web/src/styles/variables.scss:5-6`：
  ```scss
  $color-danger: #FF4D4F;        // 红
  $color-info: #13C2C2;          // 青色（信息）
  ```
  目标（对齐权威令牌值，青→蓝信息色）：
  ```scss
  $color-danger: #E5484D;        // 红 → --color-danger
  $color-info: #3A6FB0;          // 信息蓝 → --color-info（原青#13C2C2 收敛）
  ```
  同步 L10 `$color-cyan: #13C2C2;` 改为 `$color-cyan: #3A6FB0;`（其消费方若为信息语义）；`$color-success: #52C41A;`(L3) 改 `#2BA471`、`$color-warning`(L4) 已是 `#E6A700` 不动。
- [ ] **Step 2: 改 variables.scss 内容区灰。** 当前 `web/src/styles/variables.scss:124`：`$content-bg: #F0F2F5;` → 目标 `$content-bg: #F5F6F8;`（= `--bg-page` 值）。L139 `$admin-content-bg: #F4F5F7;` **保留不动**（admin 归阶段5）。
- [ ] **Step 3: 改 ant-override.scss 次操作按钮蓝。** 当前 `web/src/styles/ant-override.scss:283-291`：
  ```scss
  .toolbar-btn {
    background: rgba(24, 144, 255, 0.08) !important;
    border: none !important;
    color: #1890ff !important;
    &:hover, &:focus {
      background: rgba(24, 144, 255, 0.15) !important;
      color: #096dd9 !important;
  ```
  目标（次操作按钮=交互态→primary）：
  ```scss
  .toolbar-btn {
    background: var(--color-primary-light) !important;
    border: none !important;
    color: var(--color-primary) !important;
    &:hover, &:focus {
      background: var(--color-primary-border) !important;
      color: var(--color-primary-hover) !important;
  ```
- [ ] **Step 4: 清死配置 config/theme.ts。** 当前 `web/src/config/theme.ts:5-9`：
  ```ts
  colorPrimary: '#409eff',
  colorSuccess: '#52c41a',
  colorWarning: '#fa8c16',
  colorError: '#f5222d',
  colorInfo: '#909399',
  ```
  目标（虽未被引用，仍统一为权威值以归零 rg；不引 var() 因 ant token 需具体色值）：
  ```ts
  colorPrimary: '#E85E00',
  colorSuccess: '#2BA471',
  colorWarning: '#E6A700',
  colorError: '#E5484D',
  colorInfo: '#3A6FB0',
  ```
- [ ] **Step 5: 构建验证。** 运行 `cd web; npm run build`，期望 `vite build` 退出码 0、无 SCSS 编译错误（`var(--color-primary-light)` 等不应报错，因阶段0 已在 `:root` 定义）。
- [ ] **Step 6: 提交。** `git add web/src/styles/variables.scss web/src/styles/ant-override.scss web/src/config/theme.ts; git commit`，message：
  ```
  refactor(ui): 阶段1基线-状态色青→蓝/红收敛、次操作按钮去蓝、清死配置

  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  ```

---

### Task 2: 第1批 — components/ 公共组件去蓝拆紫

**Files:**
- Modify `web/src/components/AccountSetSelector.vue`（L80-98 选中态蓝）
- Modify `web/src/components/AccountPeriodSelector.vue`、`web/src/components/PageHeader.vue`、`web/src/components/OrgSelectModal.vue`、`web/src/components/AuxiliaryPicker.vue`、`web/src/components/FeedbackQuickSubmit.vue`、`web/src/components/cardflow/ConditionBuilder.vue`、`web/src/components/cardflow/CardTimeline.vue`、`web/src/components/cardflow/designer/CardComponentCatalog.vue`、`web/src/components/charts/DrillDownModal.vue`（各文件含 `#1677ff`/`#1890ff` 命中）

- [ ] **Step 1: 读 AccountSetSelector.vue 选中态。** 当前 `web/src/components/AccountSetSelector.vue:80-98` 多处 `color: #1677ff !important;` / `border-color: #1677ff !important;`。语义=下拉项选中/激活=交互态。逐行改为 `color: var(--color-primary) !important;` / `border-color: var(--color-primary-border) !important;`（边框用 border 令牌，文字用 primary）。
- [ ] **Step 2: 逐文件二分迁移 components 其余 8 文件。** 对每个文件：先 `rg -n "#1677ff|#1890ff" <file>` 取真实行；`&:hover`/选中/链接/激活 → `var(--color-primary)` 系；KPI/信息标签/图表单序列 `itemStyle:{color}` → `var(--color-info)`。`charts/DrillDownModal.vue` 若命中在分类色板数组内则**跳过该行**（数据可视化排除项），仅迁非色板的 UI 蓝。
- [ ] **Step 3: 处理紫（若有运单语义）。** components 内 `#722ed1` 若为运单/CardFlow 域标识 → `var(--biz-waybill)`；否则按上表。
- [ ] **Step 4: 构建验证。** `cd web; npm run build`，期望退出码 0。
- [ ] **Step 5: 批内 rg 归零验证。** 运行：
  ```powershell
  rg -ni "#1677ff|#1890ff|#409eff" web/src/components --glob '!**/charts/**' | rg -v "= \[|colors = \[|COLORS = \["
  ```
  期望 0 命中（charts 色板数组与已排除行除外）。再 `rg -ni "#722ed1" web/src/components` 期望仅剩"非业务/非运单"语义（若全为运单已迁则 0）。
- [ ] **Step 6: 提交。** `git add web/src/components; git commit -m`（message 同上格式，标题 `refactor(ui): 阶段1批1-components 公共组件去蓝拆紫迁令牌`）。

---

### Task 3: 第2批 — views/finance/ 财务域

**Files:**
- Modify `web/src/views/finance/AmoebaPLTemplate.vue`（L3209/3236/3273/3278/3317/3343-3355/3457/3515/3596/3606 等，9处蓝+紫）
- Modify `web/src/views/finance/`（VoucherList.vue L267 链接蓝、BankReconciliation.vue L224/775、InvoiceManage.vue L538、AmoebaPL.vue、AccountManage.vue、AccountTemplateManage.vue、BalanceSheet.vue、CashFlowReport.vue、ProfitStatement.vue、OperationLog.vue、FormulaConfig.vue、VoucherTemplateManage.vue、MigrationConfig.vue、MigrationWizard.vue、AccountBalanceReport.vue、FinanceHome.vue L42-44、AmoebaPLTemplate/HelperPanel.vue、AmoebaPLTemplate/AccountCodePicker.vue、AmoebaClassify.vue L31、Journal.vue L573/582、AuxiliarySetting.vue、AssetSettings.vue、VoucherEntry.vue 等）

- [ ] **Step 1: 读 AmoebaPLTemplate.vue 蓝紫真实行。** `rg -n "#1677ff|#722ed1" web/src/views/finance/AmoebaPLTemplate.vue`。判定：L3209 `&:hover .resizer-line,&:active{background:#1677ff}`=拖拽交互→`var(--color-primary)`；L3278 `box-shadow: inset 0 3px 0 0 #1677ff`=激活页签指示→`var(--color-primary)`；L3317/3457 `&:hover{color}`→`var(--color-primary)`；L3343-3355 `border:1px solid #722ed1`+`color:#722ed1`+`background:#722ed1`=（确认其卡片语义，若为运单分组→`var(--biz-waybill)`，否则若为强调标记保留色值待阶段5）。逐行贴改。
- [ ] **Step 2: 迁 VoucherList.vue 链接蓝。** 当前 `web/src/views/finance/VoucherList.vue:267`：`<a @click="downloadImportTemplate" style="color: #1890ff;">下载导入模板</a>` → 目标 `style="color: var(--color-primary);"`（链接=交互态）。
- [ ] **Step 3: 迁 FinanceHome.vue KPI 点色。** 当前 `web/src/views/finance/FinanceHome.vue:42-44` `#722ed1`（费用 KPI 点/值）：若语义为"支出"业务分类→`var(--biz-waybill)` 不合适，应判为信息色→`var(--color-info)` 或保留为财务域色 `var(--biz-finance)`（权威集含 `--biz-finance #B8860B`）。按"费用/财务身份"判 → `var(--biz-finance)`。
- [ ] **Step 4: 迁 AmoebaClassify.vue 负数红 / Journal.vue 链接蓝。** `AmoebaClassify.vue:31` `record.amount>=0?'#333':'#f5222d'` → 负数 `var(--color-danger)`；`Journal.vue:573/582` `#409eff`（搜索 hover/凭证号链接）→ `var(--color-primary)`。
- [ ] **Step 5: 扫尾 finance 其余文件。** 逐文件 `rg -n "#1677ff|#1890ff|#409eff|#f5222d|#722ed1|#13c2c2|#faad14|#fa8c16" <file>`，按总则二分；金黄警告→warning、红→danger、青→info；色板数组行跳过。
- [ ] **Step 6: 构建 + 批内 rg 归零。** `cd web; npm run build`（退出码 0）；然后：
  ```powershell
  rg -ni "#1677ff|#1890ff|#409eff|#f5222d|#13c2c2" web/src/views/finance --glob '!**/tokens.scss' | rg -v "colors = \[|= \['#"
  ```
  期望 0 命中（finance 局部 `tokens.scss` 与色板数组除外）。
- [ ] **Step 7: 提交。** `git add web/src/views/finance; git commit`（标题 `refactor(ui): 阶段1批2-finance 财务域去蓝拆紫收敛令牌`）。

---

### Task 4: 第3批 — views/cardflow/ 卡片流域

**Files:**
- Modify `web/src/views/cardflow/CardDetailPage.vue`（L597 `$tone-info-fg:#1677ff`、L627/801/822/840/883/980-981/999，9处）
- Modify `web/src/views/cardflow/`（CardFlowMonitorPage.vue、VersionHistoryPage.vue、index.vue、DelegationPage.vue、AuditLogPage.vue、FlowDefinitionListPage.vue、FlowDefinitionEditPage.vue、OrchestrationListPage.vue、OrchestrationDetailPage.vue、OrchestrationInstanceListPage.vue、OrchestrationInstanceDetailPage.vue、TodoStatsPage.vue、issues/IssueWorktable.vue、import-validation/ImportCalculationValidationWorkbench.vue、ExpenseClassification.vue、CardFlowHome.vue、auto-plugin/*、upload/*（UploadCenter.vue、VoucherGenerations.vue、AutoPluginTrailPanel.vue、components/BatchCard.vue、BatchFilterBar.vue、BatchMiniStepper.vue、BatchStatsBar.vue、ChainTimeline.vue、ChainComments.vue、UploadDropZone.vue、utils/batchStatus.ts、composables/useUploadCenter.ts）等）

- [ ] **Step 1: 读 CardDetailPage.vue 信息/交互行。** `rg -n "#1677ff" web/src/views/cardflow/CardDetailPage.vue`。判定：L597 `$tone-info-fg: #1677ff;`=信息前景局部变量→改 `$tone-info-fg: var(--color-info);`；L801 `border-left: 3px solid #1677ff`=信息条→`var(--color-info)`；L980-981 选中 `border-color`+`background`=交互态→`var(--color-primary)`+`var(--color-primary-light)`；其余 `&:hover{color}` → `var(--color-primary)`。逐行贴改。
- [ ] **Step 2: 迁 batchStatus.ts / useUploadCenter.ts 状态色。** `utils/batchStatus.ts` 4处 `#1677ff` 多为批次状态显示色：若为"进行中/信息"状态→`var(--color-info)`（注意 .ts 中若是返回 CSS 字符串供 `:style` 绑定，`var(--color-info)` 字符串在 style 绑定中有效；若用于 ECharts 则按色板排除）。`useUploadCenter.ts:348` 的 `const colors=['#1677ff',...]`=分类色板→**跳过**。
- [ ] **Step 3: 迁运单紫。** cardflow 域 `#722ed1` 多为 CardFlow/运单标识 → `var(--biz-waybill)`。
- [ ] **Step 4: 扫尾 cardflow 其余文件。** 逐文件二分迁移；`auto-plugin/*` 与 `upload/components/*` 的徽标/激活蓝按交互 vs 信息判定。
- [ ] **Step 5: 构建 + rg 归零。** `cd web; npm run build`（退出码 0）；
  ```powershell
  rg -ni "#1677ff|#1890ff|#409eff" web/src/views/cardflow | rg -v "colors = \[|= \['#|COLORS = \["
  ```
  期望 0 命中。`rg -ni "#722ed1" web/src/views/cardflow` 期望 0（运单紫全迁 biz-waybill）。
- [ ] **Step 6: 提交。** `git add web/src/views/cardflow; git commit`（标题 `refactor(ui): 阶段1批3-cardflow 卡片流去蓝拆紫(运单→biz-waybill)`）。

---

### Task 5: 第4批 — views/crm/ 客户关系域

**Files:**
- Modify `web/src/views/crm/`（CrmDashboard.vue L126/207/233/583、CustomerManage.vue L347 色板、CustomerDetail.vue、FeedbackManage.vue、FeedbackStats.vue L166、ServiceOrderManage.vue、ServiceOrderStats.vue、ProfitAnalysis.vue L69/72、ReferralStats.vue、WaybillPoolManage.vue L294）

- [ ] **Step 1: 读 CrmDashboard.vue 四类蓝。** `rg -n "#1890ff" web/src/views/crm/CrmDashboard.vue`。判定：L126 `<a-avatar :style="{backgroundColor:'#1890ff'}">`=头像装饰→信息→`var(--color-info)`；L207 KPI `{key:'total',color:'#1890ff'}`=统计信息→`var(--color-info)`；L233 ECharts `itemStyle:{color:'#1890ff'}` 单序列"毛利"=主指标→`var(--color-info)`（单序列非色板，可迁；如需精确按数据语义保留则记为信息）；L583 `border-left:3px solid #1890ff`=信息条→`var(--color-info)`。
- [ ] **Step 2: 迁 ProfitAnalysis.vue 负数红。** 当前 `web/src/views/crm/ProfitAnalysis.vue:69` `color: record.profit>=0?'#52c41a':'#f5222d'` → `record.profit>=0?'var(--color-success)':'var(--color-danger)'`；L72 同理。
- [ ] **Step 3: 迁 WaybillPoolManage.vue 红。** `web/src/views/crm/WaybillPoolManage.vue:294` `color:#f5222d` → `var(--color-danger)`（确认为错误/警示语义）。
- [ ] **Step 4: 处理 CustomerManage.vue 色板。** L347 `const bdColors=['#1890ff','#52c41a',...]`=BD 分类色板→**跳过**（数据可视化排除项），记入遗留。
- [ ] **Step 5: 扫尾 crm 其余文件二分迁移。** 含金黄/青→warning/info。
- [ ] **Step 6: 构建 + rg 归零。** `cd web; npm run build`（退出码 0）；
  ```powershell
  rg -ni "#1890ff|#1677ff|#409eff|#f5222d|#13c2c2" web/src/views/crm | rg -v "bdColors = \[|colors = \[|= \['#"
  ```
  期望 0 命中。
- [ ] **Step 7: 提交。** `git add web/src/views/crm; git commit`（标题 `refactor(ui): 阶段1批4-crm 客户域去蓝拆紫迁令牌`）。

---

### Task 6: 第5批 — views/task/ 任务·OKR·绩效域

**Files:**
- Modify `web/src/views/task/`（TaskKanban.vue L432 色板、TaskList.vue、TaskDashboard.vue L60/190、TaskCalendar.vue、TagManage.vue L89 色板、ProjectList.vue、ProjectDetail.vue、PerformanceEvaluation.vue L198/323、PerformanceDimensions.vue、MyPerformance.vue L165/265、NotificationCenter.vue L161、KnowledgeDetail.vue、GoalTree.vue、GoalList.vue、GoalDetail.vue、components/SubTaskList.vue、ProgressTimeline.vue、ProgressReport.vue、NotificationBell.vue、CommentReactions.vue、AttachmentUpload.vue）

- [ ] **Step 1: 读绩效评级色映射。** `web/src/views/task/PerformanceEvaluation.vue:198` `gradeColorMap={S:'#722ed1',A:'#1890ff',B:'#52c41a',C:'#faad14',D:'#ff4d4f'}`。判定：这是**评级分类色映射**（类色板）。语义二分=数据分类→可保留为域色或迁 biz：S 紫非运单语义，应按"绩效等级"分类映射处理——本阶段将映射统一为令牌：`{S:'var(--biz-points)',A:'var(--color-info)',B:'var(--color-success)',C:'var(--color-warning)',D:'var(--color-danger)'}`（绑定到 `:style` 有效）。`MyPerformance.vue:165` 同步同改。
- [ ] **Step 2: 处理 PerformanceEvaluation.vue 阈值函数。** L323 `if(score>=90)return '#722ed1'` → `return 'var(--biz-points)'`（高分=积分/卓越身份）；`MyPerformance.vue:265` 同改。
- [ ] **Step 3: 迁 TaskDashboard.vue KPI 紫。** L60/190 `#722ed1`（绩效评估入口/KPI）→ 判为信息/积分身份→`var(--biz-points)`（绩效域非运单）。
- [ ] **Step 4: 色板数组跳过。** TaskKanban.vue L432、TagManage.vue L89 的 `const colors=[...]` / 预设色板→**跳过**，记入遗留。
- [ ] **Step 5: 扫尾 task 其余文件二分迁移。**
- [ ] **Step 6: 构建 + rg 归零。** `cd web; npm run build`（退出码 0）；
  ```powershell
  rg -ni "#1890ff|#1677ff|#f5222d|#13c2c2" web/src/views/task | rg -v "colors = \[|gradeColorMap|colorPresets|= \['#"
  ```
  期望 0 命中（评级映射已改令牌、色板数组排除）。
- [ ] **Step 7: 提交。** `git add web/src/views/task; git commit`（标题 `refactor(ui): 阶段1批5-task 任务绩效域评级色迁令牌`）。

---

### Task 7: 第6批 — views/express/ 快递报价·成本·报表域

**Files:**
- Modify `web/src/views/express/`（quotation/QuotationWorkbench.vue、quotation/QuotationList.vue、quotation/components/*（WeightSegmentEditor.vue L命中7处、WeightSegmentPanel.vue、ShopQueryPanel.vue、ClientPanel.vue、PriceCellInput.vue、FixedPriceCostMatrix.vue 9处）、cost-plan/CostPlanEdit.vue、CostPlanList.vue、cost-plan/components/*（CostMatrixTable.vue、CostItemSidebar.vue、CityPriceMatrix.vue）、report/WeightSegment.vue L84 色板、ProfitAnalysis.vue、FlowAnalysis.vue、dashboard/Dashboard.vue L161、prepayment/PrepaymentManage.vue、invoice/InvoiceList.vue、InvoiceDetail.vue、policy-rebate/RebateSimulation.vue、RebateSettlement.vue、quality-center/Overview.vue、Dashboard.vue）

- [ ] **Step 1: 读 FixedPriceCostMatrix.vue 矩阵交互蓝。** `rg -n "#1677ff" web/src/views/express/quotation/components/FixedPriceCostMatrix.vue`（9处）。报价矩阵选中单元格/激活列高亮=交互态→`var(--color-primary)`/`var(--color-primary-light)`；表头信息蓝→`var(--color-info)`。逐行贴改。
- [ ] **Step 2: 读 WeightSegmentEditor.vue 7处蓝。** 二分：段落编辑选中/hover→primary；段标签信息→info。
- [ ] **Step 3: 迁 express/dashboard/Dashboard.vue 运单 KPI 紫。** L161 `{title:'本月运单',color:'#722ed1'}` → `var(--biz-waybill)`（运单业务语义）。
- [ ] **Step 4: 色板跳过。** report/WeightSegment.vue L84 `const colors=[...]`→**跳过**。
- [ ] **Step 5: 扫尾 express 其余文件二分迁移。**
- [ ] **Step 6: 构建 + rg 归零。** `cd web; npm run build`（退出码 0）；
  ```powershell
  rg -ni "#1677ff|#1890ff|#409eff|#13c2c2" web/src/views/express | rg -v "colors = \[|= \['#"
  ```
  期望 0 命中。`rg -ni "#722ed1" web/src/views/express` 期望 0（运单紫全迁）。
- [ ] **Step 7: 提交。** `git add web/src/views/express; git commit`（标题 `refactor(ui): 阶段1批6-express 快递域矩阵去蓝/运单紫→biz-waybill`）。

---

### Task 8: 第7批 — views/system/ 系统与主题配置域

**Files:**
- Modify `web/src/views/system/`（ThemeConfig.vue L命中3处、theme/SidebarConfig.vue、theme/SpacingConfig.vue、theme/TableConfig.vue L命中4处、AdminConfigCenter.vue、FeedbackCenter.vue L命中4处、DingTalkConfig.vue、EnterpriseInfo.vue、OrgChart.vue、DatabaseSetup.vue）

- [ ] **Step 1: 区分主题预览色 vs UI 自身色（关键）。** `web/src/views/system/ThemeConfig.vue` 与 `theme/*Config.vue` 是**主题配置面板**——其中部分蓝可能是"展示主题色样本/预览值"（不应迁，否则破坏配置语义），部分是面板自身 UI（应迁）。逐行 `rg -n "#1677ff" <file>` 后人工判定：若是 `:style` 预览样本/绑定 `themeConfig.colorXxx`→保留；若是面板按钮/选中态/链接→`var(--color-primary)`/`var(--color-info)`。
- [ ] **Step 2: 迁 FeedbackCenter.vue 4处。** 多为状态标签/链接，按交互 vs 信息二分。
- [ ] **Step 3: 迁 TableConfig.vue 4处 / EnterpriseInfo.vue 2处。** 配置面板 UI 蓝→按语义二分。
- [ ] **Step 4: 扫尾 system 其余文件。** 注意 `theme/SidebarConfig.vue` 涉及侧栏色——侧栏激活色已由阶段0 令牌 `--sidebar-item-active-*` 接管，若命中为侧栏激活蓝→改 `var(--sidebar-item-active-text)`/`var(--color-primary)`。
- [ ] **Step 5: 构建 + rg 归零（带豁免说明）。** `cd web; npm run build`（退出码 0）；
  ```powershell
  rg -ni "#1677ff|#1890ff|#409eff" web/src/views/system | rg -v "colors = \[|= \['#"
  ```
  期望 0 命中**或**仅剩"主题预览样本色"白名单行（如有，逐行注释 `// 主题预览样本，非UI色`说明豁免；该豁免行在 commit message 中列明）。
- [ ] **Step 6: 提交。** `git add web/src/views/system; git commit`（标题 `refactor(ui): 阶段1批7-system 配置面板去蓝(预览样本色豁免)`）。

---

### Task 9: 第8批 — 其余视图扫尾（workhub/conference/quality/oa/points/contract/vehicle/dormitory/hr/supplier/reports/insurance/workflow/mobile）+ 内容灰收口

**Files:**
- Modify `web/src/views/workhub/`（WorkItemCard.vue L405/408/409/411/421、WorkHubDetail.vue L98/100/101/103、WorkHubCenter.vue L59/61/185/187、index.vue L144、TriggerActionPanel.vue、ImportIssueDetail.vue）
- Modify `web/src/views/conference/`（panels/*、components/*）、`web/src/views/quality/`、`web/src/views/oa/`、`web/src/views/points/`、`web/src/views/contract/`、`web/src/views/vehicle/`、`web/src/views/dormitory/`、`web/src/views/hr/`、`web/src/views/supplier/`、`web/src/views/reports/`、`web/src/views/insurance/`、`web/src/views/workflow/`、`web/src/mobile/views/`（Dashboard.vue、ReportCost.vue L61 色板、ReportExpress.vue）、`web/src/views/cardflow-mobile/MobileCardFillPage.vue` L685
- Modify 内容灰：`web/src/views/workhub/WorkHubDetail.vue` L832/843、`WorkHubCenter.vue` L827、`workhub/index.vue` L144、`supplier/SupplierHome.vue` L45、`contract/ContractDashboard.vue` L372、`conference/ConferenceHome.vue` L45、`crm/CrmDashboard.vue` L500、`reports/ReportsHome.vue` L45、`points/PointDashboard.vue` L384、`express/dashboard/Dashboard.vue` L307、`finance/FinanceHome.vue` L652、`oa/OaHome.vue` L45、`cardflow/FlowGroupListPage.vue` L1029、`cardflow/CardFlowHome.vue` L382、`quality/dashboard/QualityDashboard.vue` L444、`hr/HRHome.vue` L45、`cardflow-mobile/MobileCardFillPage.vue` L685

- [ ] **Step 1: 迁 workhub 域分类色映射（运单紫/积分金黄）。** `web/src/views/workhub/WorkItemCard.vue`：L405 `datacenter:{label:'CardFlow',color:'#722ed1'}` → `var(--biz-waybill)`；L408 `points:{label:'积分',color:'#d4b106'}` → `var(--biz-points)`；L409 `finance:{label:'财务',color:'#faad14'}` → `var(--biz-finance)`（财务身份）；L411 `workflow:{color:'#13c2c2'}` → `var(--color-info)`；L421 `high:{color:'#fa8c16'}` → `var(--color-warning)`（优先级高=警告）。`WorkHubDetail.vue` L98/100/101/103、`WorkHubCenter.vue` L59/61/185/187 同步同改（这是同义映射的多处复制，须全改一致）。
- [ ] **Step 2: 迁内容区灰 #f0f2f5/#f4f6f8 → var(--bg-page)。** 对上列内容灰命中逐行：当前如 `background: #f0f2f5;` → `background: var(--bg-page);`。`MobileCardFillPage.vue:685` `#f4f5f7`（移动端内容区，非 admin）→ `var(--bg-page)`。注意 `AdminLayout.vue` 的 `#F4F5F7` **不在本批**（归阶段5）。
- [ ] **Step 3: 迁 insurance/ReportDashboard.vue 待审批紫与到期金黄。** `insurance/ReportDashboard.vue:24` `待审批理赔 value-style:{color:'#722ed1'}`→ 审批语义→`var(--biz-approval)`（权威集 `--biz-approval #3A6FB0`）；L14 `即将到期 color:'#faad14'`→`var(--color-warning)`。
- [ ] **Step 4: 色板数组跳过。** mobile/ReportCost.vue L61、conference/RoomCard.vue L112-113、quality/QualityDashboard.vue L308/327、oa/EventForm.vue L32 的 `const colors=[...]`/`colorPresets=[...]`→**跳过**，统一记入遗留清单。
- [ ] **Step 5: 扫尾其余所有视图。** 逐目录 `rg -n "#1677ff|#1890ff|#409eff|#f5222d|#13c2c2|#722ed1|#faad14|#fa8c16|#d4b106" <dir>` 按总则二分迁移；conference/components 的激活蓝→primary、信息蓝→info。
- [ ] **Step 6: 构建 + 全量 rg 归零（核心验收）。** `cd web; npm run build`（退出码 0）；然后全局断言（排除色板与令牌定义文件）：
  ```powershell
  rg -ni "#1677ff|#1890ff|#409eff" web/src --glob '!**/tokens.scss' | rg -v "colors = \[|COLORS = \[|colorPresets|bdColors|gradeColorMap|= \['#"
  ```
  期望 0 命中（仅图表分类色板与已迁评级映射的历史行除外）。
  ```powershell
  rg -ni "#f5222d" web/src | rg -v "colors = \[|= \['#"
  ```
  期望 0；
  ```powershell
  rg -ni "#13c2c2" web/src | rg -v "colors = \[|= \['#|variables.scss"
  ```
  期望 0；
  ```powershell
  rg -ni "#722ed1" web/src | rg -v "AdminLayout|variables.scss|colors = \[|= \['#"
  ```
  期望 0（admin 主题紫与色板除外，业务运单紫全迁 biz-waybill）；
  ```powershell
  rg -ni "#f4f6f8|#f0f2f5" web/src | rg -v "variables.scss"
  ```
  期望 0（内容区灰全迁 --bg-page）。
- [ ] **Step 7: 派生蓝形态补扫（防漏）。** 实测存在 `rgba(24, 144, 255, *)` 与 `#096dd9` 等蓝的 rgba/derived 形态（raw-hex 断言抓不到）。运行 `rg -ni "rgba\(24,\s*144,\s*255|#096dd9|#0958d9|#2f54eb" web/src | rg -v "colors = \[|= \['#"`，对命中按交互/信息二分迁 `var(--color-primary)`/`var(--color-info)`；期望补扫后再次运行 0 命中。
- [ ] **Step 8: 运行时令牌验证。** `npm run type-check`（vue-tsc）确认本批未**新增**报错（基线本就红，仅对比不作门禁）；用 preview 工具 `preview_start` 起前端，改 `:root` 的 `--color-primary` 看全局是否联动变色（验证迁移确实接入令牌而非死值）。
- [ ] **Step 9: 提交。** `git add web/src/views web/src/mobile; git commit`（标题 `refactor(ui): 阶段1批8-扫尾去蓝拆紫(运单/积分/审批→biz)+内容灰→bg-page`）。

---

### 遗留与移交（明确不在本阶段，避免范围蔓延）
- **图表分类调色板（23 行 `const colors=[...]`/`colorPresets`/`bdColors`）**：ECharts 数据可视化色板，需 `getComputedStyle` 运行时解析令牌=结构改造，移交后续数据可视化专项；本阶段所有 rg 验证已显式排除。
- **后台主题紫/冷灰**（`AdminLayout.vue`、`variables.scss` 的 `$admin-*`、`#F4F5F7` admin 内容区）：移交**阶段5（后台主题）**。
- **版式/结构**：本阶段只迁颜色到令牌，不动布局/间距/圆角结构（间距/圆角/字号令牌化属其他阶段）。