# 合同模块视觉债清理 + ContractList B 收敛 — 设计

> Phase 2 痛点页 #3。前两页:①报价模块视觉债(纯 C)②毛利分析重做(A+B+C)。本页 = **C + B 收敛**,范围**整个合同模块 6 文件**。
> 日期:2026-06-19。复用 [[stotop-theme-token-system]] 的 PATTERNS.md/TOKENS.md 契约与三件套(`PageLayout`/`DataTable`/`StatFilterTabs`/`StatusTag`)。样板范式 = `web/src/views/vehicle/VehicleManage.vue`。

## 1. 背景与真痛点(已与用户确认,非假设)

`/contract/list` 及整个合同模块完全早于"中性墨 + 橙点缀"设计系统。审计(4 路多代理)结论:

- **C 视觉债(铁证)**:6 文件共 **12 张 `bordered` 表**;ContractList 续签链 timeline 硬编码 `color="blue/green"`、条款 tag `color="error"`、3 个旧 SCSS 变量(`$text-primary`/`$border-color-lighter`/`$bg-page`)、~13 处布局内联 style、工具栏未走"填满首行";ContractDashboard 更重(`#fff`/`#d9d9d9`/`#f0f0f0` 裸 hex、`rgba(0,0,0,.45/.85)` 文本色、**ECharts 状态柱旧蓝绿 6 色硬编码** `#409eff/#67c23a/...`);3 个列表子页(ESign/Type/Template)同样 `bordered` + `paginationConfig` 样板 + `#toolbar` 手搓 flex。
- **B 信息密度**:6 态生命周期(草稿/审批中/待签署/已生效/已到期/已终止)却只有一个状态下拉筛选,无 `StatFilterTabs` 状态快筛 + 计数(VehicleManage 已有此待遇)。
- **A / D(本轮明确不做)**:900px 新建弹窗重做、详情抽屉 5 Tab 瘦身、续签链追溯、跨模块链接增强、签署落地、`发起签署` 桩——均属**行为/产品改造**,通法明确"行为类改进单独排期"。

用户决策(三轮拍板):
1. 痛点组合 = **C + B 收敛**(不碰业务行为)。
2. C 清理范围 = **整个合同模块 6 文件**(避免列表新、内页旧的割裂)。
3. StatFilterTabs 计数 = **后端加 statistics 端点**(镜像 vehicle,最干净)。
4. 3 个列表子页深度 = **全迁 DataTable + 填满首行**(与主页完全一致)。

## 2. 范围分层

### Tier 1 — `ContractList.vue`(主痛点页,C + B 全套)
### Tier 2 — 子页 C 视觉债 + 结构收敛
- `ContractDashboard.vue` — C tokenize(保留自有 flex-height 仪表盘布局,**不迁** PageLayout)
- `ESignManage.vue` / `ContractTypeManage.vue` / `ContractTemplateManage.vue` — 全迁 DataTable + 填满首行 + tokenize
- `components/ContractStatusFlow.vue` — tokenize(若有硬编码色)

**严格不动**:任何后端业务逻辑(除新增只读 statistics 查询)、任何前端交互行为(弹窗/抽屉/续签/签署/审批流程 1:1 保留)、modal/drawer 的 `width="900px"`(响应式属 A)、移动端、暗黑皮肤。

## 3. Tier 1:ContractList 详细设计

### 3.1 B — 后端 statistics 端点(镜像 vehicle `GetStatisticsAsync`)

**后端**(`src/STOTOP.Module.Contract/`):

- `Dtos/ContractDto.cs` 新增:
  ```csharp
  public class ContractStatisticsDto
  {
      public int TotalCount { get; set; }
      public List<ContractStatusGroupDto> ByStatus { get; set; } = new();
  }
  public class ContractStatusGroupDto
  {
      public int Status { get; set; }
      public string StatusName { get; set; } = string.Empty;
      public int Count { get; set; }
  }
  ```
- `Services/Interfaces/IContractService.cs` 新增 `Task<ContractStatisticsDto> GetStatisticsAsync();`
- `Services/ContractService.cs` 实现:`_contractRepository.Query()` 按 `FStatus` GroupBy 计数(**镜像 `GetContractsAsync` 的基查询**,组织隔离由 repository 全局过滤自动生效,不额外加软删过滤,与列表口径一致);`StatusName` 用本地状态名映射(草稿/审批中/待签署/已生效/已到期/已终止)。**注意**:某状态计数为 0 时 GroupBy 不产行,前端需对缺失状态兜底 0(见 3.3)。
- `Controllers/ContractController.cs` 新增:
  ```csharp
  [HttpGet("statistics")]
  [RequirePermission(ContractPermissions.ContractView)]
  public async Task<ApiResult<ContractStatisticsDto>> GetStatistics()
      => ApiResult<ContractStatisticsDto>.Success(await _service.GetStatisticsAsync());
  ```
  路由 = `GET /api/contract/contracts/statistics`(放在 `{id}` 路由**之前**或用静态段,避免被 `{id}` 误吞——`statistics` 非数字,ASP.NET 路由按字面段优先,安全)。

**前端**(`web/src/api/contract.ts`):新增类型 `ContractStatisticsDto`/`ContractStatusGroupDto` + `getContractStatistics()` → `get('/contract/contracts/statistics')`。

**单测**:`GetStatisticsAsync` 是 GroupBy 纯查询。按现有合同/vehicle service 测试范式加一个 service 级单测(若该模块无测试工程,落到能间接引用的测试工程或 InMemory provider,沿用项目既有做法;计数与缺失状态兜底各 1 例)。具体落点 plan 阶段定。

### 3.2 B + C — StatFilterTabs + 填满首行工具栏

照搬 VehicleManage 结构:

- 根容器:`.page-container` → `.page-container page-container--flush`。
- `PageHeader` 用 `#left` / `#right` 槽(**替代**现有 `#actions` + `#toolbar` 手搓 flex):
  - `#left`:`<StatFilterTabs inline v-model:active="searchForm.status" :tabs="statusTabs" @change="handleSearch" />`
  - `#right`:`a-input`(keyword,200px)+ `a-select`(类型,140px)+ `a-range-picker`(日期,240px)+ `a-button`(重置)+ `a-button type=primary`(新建合同,带权限 `v-if="has(ContractCreate)"`);全部 `size="middle"`。
- `statusTabs` computed(镜像 vehicle `getStatusCount`,**颜色用 `var(--color-*)` 令牌**;**"全部"键沿用 StatFilterTabs 约定 = `''` 空串**,与 vehicle 一致):
  ```ts
  const statusTabs = computed(() => [
    { key: '', label: '全部', count: statistics.value.totalCount },
    { key: 0, label: '草稿',   count: getStatusCount(0), color: 'var(--text-3)' },
    { key: 1, label: '审批中', count: getStatusCount(1), color: 'var(--color-info)' },
    { key: 2, label: '待签署', count: getStatusCount(2), color: 'var(--color-warning)' },
    { key: 3, label: '已生效', count: getStatusCount(3), color: 'var(--color-success)' },
    { key: 4, label: '已到期', count: getStatusCount(4), color: 'var(--color-danger)' },
    { key: 5, label: '已终止', count: getStatusCount(5), color: 'var(--text-3)' },
  ])
  ```
  - `searchForm.status` 类型改为 `'' | number`(默认 `''` = 全部),与 vehicle `'' as number|string` 范式一致;`fetchList` 守卫改为 `if (searchForm.status !== '' && searchForm.status !== undefined) params.status = searchForm.status`(空串不发 status 过滤)。
  - `getStatusCount(s)` 从 `statistics.value.byStatus.find(g=>g.status===s)?.count ?? 0`(缺失状态兜底 0)。
  - **删除**原工具栏的 status `a-select` 下拉(被 Tab 取代,过滤行为等价)。
- `onMounted` 增 `fetchStatistics()`;`handleSubmit`/`handleDelete`/`handleSubmitApproval` 成功后追加 `fetchStatistics()` 刷新计数(镜像 vehicle)。
- `handleReset` 重置 `searchForm.status = ''`(回"全部")。

> ⚠️ **dateRange 现状**:模板有 `a-range-picker` 但 `fetchList` **从未发送** `dateRange`(死控件,pre-existing)。本轮按 1:1 **原样保留**(不 wire 不删),归入行为待办另排。

### 3.3 C — 主列表迁 DataTable

- `a-card :bordered=false` 包裹 + `a-table bordered` → `<DataTable v-model:pagination="pagination" ...>`:
  - 去 `paginationConfig` computed、去手算序号列(`index` dataIndex 那段 `#bodyCell`)、去 `handleTableChange`(DataTable v-model 接管;`@change="fetchList"`)。
  - `pagination` 改为 `ref({ pageIndex:1, pageSize:20, total:0 })`(vehicle 范式),`fetchList` 读 `pagination.value.*`,`total` 写 `pagination.value.total`。
  - `:scroll="{ x: 1400 }"`、`row-key="id"`、`empty-text="暂无合同数据"`(DataTable 内建 EmptyState)。
  - **去掉** `index` 列定义(DataTable 内建序号列)。
- 单元格令牌化:
  - 状态 `a-tag :color="statusColor(status)"` → `<StatusTag :type="statusTagType(status)">`(新增 `statusTagType` 映射:0→default,1→info,2→warning,3→success,4→danger,5→default)。
  - 合同性质保持纯文本 `natureText`(原本就是文本,无色)。
  - 标题列 `<a style="cursor:pointer">` → 去内联 `cursor`,改 scoped class 或复用现有链接类。

### 3.4 C — 弹窗 / 抽屉内嵌表 + 旧变量

- **内嵌非分页表**(合同方 modal 表 line 190、详情抽屉 4 张表 line 298/323/341/360)→ 仅 `:bordered="false"`(**不迁 DataTable**,它们 `:pagination=false` 内嵌,DataTable 无增益);保留各自 `#emptyText` 的 `EmptyState`。
- **`a-descriptions bordered`**(line 272)→ **保留 bordered**(KV 网格是合理例外,无边框令牌只针对数据表)。
- **续签链 timeline**(line 381/384)`color="blue"`/`color="green"` → 传**主题真 hex**(ant timeline 圆点不解析 `var()`):info/success 的 theme.ts 真值(plan 阶段取准确 hex)。或更简:改用 `StatusTag` 表达"原合同/当前合同"语义。plan 二选一,优先真 hex 保持 timeline 形态。
- 条款 tag `color="error"`(line 333)→ `<StatusTag type="danger">关键条款</StatusTag>`。
- `<style scoped>`:`.section-title` 的 `$text-primary`→`var(--text-1)`、`$border-color-lighter`→`var(--border-faint)`(区块标题分隔线走耳语级)、`.clause-item` 的 `$bg-page`→`var(--bg-page)`、`$border-radius-sm`→对应圆角令牌或保留 SCSS 变量(若是尺寸非颜色,非视觉债,可留)。`.cross-module-link` 已用 `var()`,不动。
- **布局内联 style 保留**(`width:200px`、`padding:10px 20px`、`width:100%` 等)——视觉债指**颜色**,布局宽度内联与 vehicle 范式一致,不算债。

## 4. Tier 2:子页设计

### 4.1 ContractDashboard.vue(C tokenize,保留仪表盘布局)
- `<style>` 裸 hex / rgba → 令牌:`#fff`→`var(--bg-card)`(`.kpi-bar`/`.content-panel`);`#d9d9d9`→`var(--border)`;`#f0f0f0`→`var(--border)`(`.content-panel`/`.quick-action-item`);`rgba(0,0,0,.45)`→`var(--text-3)`;`rgba(0,0,0,.85)`→`var(--text-1)`;阴影 `rgba(0,0,0,.06/.08)` 保留(中性阴影非配色债)。
- **ECharts 状态柱**(line 221-228)旧 6 色 `['#909399','#409eff','#e6a23c','#67c23a','#f56c6c','#909399']` → **状态语义真 hex**(0草稿中性/1审批 info/2待签 warning/3生效 success/4到期 danger/5终止中性),取 theme.ts 真值(ECharts 不解析 `var()`)。饼图(类型分布)无显式色板,默认走全局 ECharts 主题板即可(若全局已设新板则继承;否则 plan 阶段补设 `color` 数组为新协调板)。
- bordered 表(line 47/77)→ `:bordered="false"`。
- a-tag:`remainDays` 的 `color="error"/"warning"`(line 53)、`contractStatusColor`(line 83)→ `StatusTag`(remainDays:≤7→danger 否则 warning;status→映射)。
- KPI `:style="{ background/color: kpi.color }"`:**保留**(kpi.color 已是 `var(--color-*)` 令牌,运行时解析正常,非债)。
- 保留 `.contract-dashboard` 自有 flex-height 布局,不迁 PageLayout。

### 4.2 ESignManage / ContractTypeManage / ContractTemplateManage(全迁 DataTable + 填满首行)

三页统一改造(均确认为标准分页列表):
- 容器 → `.page-container page-container--flush`。
- `a-card`+`a-table bordered`+`paginationConfig`+手算序号+`handleTableChange` → `DataTable v-model:pagination`(`pagination` 改 `ref`,`@change="fetchList"`,去序号列与样板)。
- `a-alert` 提示条**保留**(ESign / Type 各有一条);置于 PageHeader 与 DataTable 之间(现状即此)。
- 工具栏走**填满首行(无 StatFilterTabs 变体)**:
  - **ESignManage**:`#left` = keyword(200px)+ signStatus 下拉(140px)+ 重置;无新增故 `#right` 空(或仅留必要)。`size="middle"`。
  - **ContractTypeManage**:无筛选;`#left` 空,`#right` = 新增类型(带权限)。
  - **ContractTemplateManage**:`#left` = keyword(200px)+ 类型(160px)+ 状态(120px)+ 重置;`#right` = 新增模板(带权限)。
  - (`#left`/`#right` 具体分配以 plan 为准,原则:筛选靠左、主操作靠右、`size="middle"` 对齐 32px。)
- a-tag 状态 → `StatusTag`:ESign signStatus(0待签 info/1已签 success/2拒签 danger)、Type status(1启用 success/0停用 default)、Template status(0草稿 default/1已发布 success/2已停用 default)。
- ContractTemplateManage 的 `version` 列 `customRender:({text})=>`V${text}`` 保留。

### 4.3 ContractStatusFlow.vue
- 读取后 tokenize 任何硬编码色(若为 a-steps 字面状态串 'wait/process/finish/error',ant 主题已统一,通常无需改;仅当存在裸 hex / 旧蓝才令牌化)。plan 阶段以实际内容定。

## 5. 验证

- **前端** `pnpm/npm build`(vite)绿;改动文件 `stylelint` 0 报错。
- **后端** `dotnet build` 绿;新增 statistics service 单测过(`--arch x64`,见 [[dotnet-test-x64-socket]])。
- **裸 hex 复扫**:`rg` 对 6 文件复扫颜色(含 inline `style` / `h()` / ECharts 数组等 stylelint 盲区)清零——这是真元凶,stylelint 抓不到(见报价页教训)。
- **用户 live 逐页看**:用户已登录 9001 会话 HMR 实时验(我不自起第二 vite 实例,避免 .vite 缓存双 Vue 报错)。逐页过:ContractList(状态 Tab 计数/快筛/表格观感/弹窗抽屉去边)、Dashboard(KPI/ECharts 新色/表)、ESign/Type/Template。

## 6. 执行方式

承接通法:spec → plan(writing-plans)→ 执行(后端 statistics 一段 + 前端 6 文件,可后台工作流并行分文件)→ 中央复验(build + stylelint + rg 复扫)→ 用户 live。后端改动小且独立,建议先落后端 statistics + 前端 ContractList(主页全套),再批量子页。

## 7. 明确不做(行为待办,另排期)

900px 新建弹窗重做 / 详情抽屉 5 Tab 瘦身 / 续签链链式追溯(现仅 status=3 单向 2 项)/ 跨模块链接增强(仅客户、仅 keyword)/ `发起签署` 桩落地 / dateRange 死控件 wire / modal·drawer 响应式宽度 / Dashboard `pageSize:9999` 全量加载改用 statistics 端点(可选优化,本轮仅视觉)。
