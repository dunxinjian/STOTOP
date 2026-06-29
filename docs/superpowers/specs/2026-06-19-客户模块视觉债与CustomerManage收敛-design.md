# 客户模块视觉债清理 + CustomerManage·Detail C+B 收敛 — 设计

> Phase 2 痛点页 #4。与 ContractList(#3)**孪生治疗**：C 视觉债迁三件套 + B StatFilterTabs + 后端 statistics 端点；**新增项 = `--avatar-palette-*` 设计令牌**(预留的 0e 任务，BD 头像用)。日期 2026-06-19。范围 = CustomerManage(列表,C+B 全套) + CustomerDetail(11 Tab 详情,C tokenize)。样板范式 = `vehicle/VehicleManage.vue` + 已完成的 ContractList。

## 1. 背景与真痛点(已确认,非假设)

4 路审计实测:`CustomerManage.vue` 是「重灾区」——**手搓 `.status-tab-bar`**(应 StatFilterTabs)、**手搓 `.status-tag`**(应 StatusTag)、**BD 头像硬编码 6 色板**(应 `--avatar-palette`)、**9 裸 hex + 8 `rgba(0,0,0,.X)` 字面色**、`#fafafa` 旧表头覆盖 + `!important` 橙 hover、未迁 DataTable;**initStats 用 4 次 `getCustomerList(pageSize:1)`** 只为拿计数。`CustomerDetail.vue`(11 Tab)——**11 张 `bordered` 表** + **大量 `a-tag :color` 用 ant 预设色名**(blue/green/purple/gold/orange/red 分类色,与中性墨冲突)+ 时间线 inline `#999/#666`。

用户决策:① 痛点 = **C + B 收敛**;② 范围 = **Manage + Detail 都清**;③ 计数 = **后端 statistics 端点**(镜像 contract);④ 头像 = **新增 `--avatar-palette-*` 令牌**。

## 2. 范围分层

- **Tier 1 — CustomerManage.vue**:C(三件套+令牌+头像令牌) + B(StatFilterTabs+后端 statistics)。
- **Tier 2 — CustomerDetail.vue**:C tokenize(去 bordered + a-tag→StatusTag + inline 色→令牌),**不重构 11 Tab 结构**。
- **新令牌**:`web/src/stores/theme.ts` 加 `--avatar-palette-1..6`。

**严格不动**:任何业务行为(除新增只读 statistics 查询)、弹窗/抽屉/Tab 交互、`@row-click` 跳详情逻辑、各 fetch/CRUD 流程 1:1;移动端/暗黑。

## 3. Tier 1:CustomerManage 详细设计

### 3.1 B — 后端 statistics 端点(镜像 contract)

**后端 `src/STOTOP.Module.CRM/`**:
- `Dtos/` 新增(放 CustomerDto.cs 文件尾或同目录):
  ```csharp
  public class CustomerStatisticsDto { public int TotalCount { get; set; } public List<CustomerStatusGroupDto> ByStatus { get; set; } = new(); }
  public class CustomerStatusGroupDto { public int Status { get; set; } public string StatusName { get; set; } = string.Empty; public int Count { get; set; } }
  ```
- `ICustomerService` + `CustomerService` 加 `GetStatisticsAsync()`:`_customerRepository.Query().GroupBy(c => c.FStatus)` 计数,3 态(0 潜在/1 活跃/2 流失)兜底 0;基查询与 `GetCustomersAsync` 一致(组织隔离由仓储全局过滤自动生效,不额外加过滤)。
- `CustomerController` 新增端点,**置于 `[HttpGet("{code}")]` 之前**:
  ```csharp
  [HttpGet("statistics")]
  [RequirePermission(CrmPermissions.CustomerView)]
  public async Task<ApiResult<CustomerStatisticsDto>> GetStatistics()
      => ApiResult<CustomerStatisticsDto>.Success(await _customerService.GetStatisticsAsync());
  ```
  > ⚠️ 详情路由是 `[HttpGet("{code}")]`(**string** code,非数字)。`statistics` 段靠 ASP.NET **字面段优先于参数段**的路由优先级匹配到本端点(literal > param,标准行为);放在 `{code}` action 之前更稳妥。
- **不写单测**(CRM 无测试工程、vehicle/contract 同款 statistics 亦无单测;平凡 GroupBy 靠 build + live 计数核对,与 contract 一致)。

**前端 `web/src/api/crm.ts`**:加 `CustomerStatisticsDto`/`CustomerStatusGroupDto` 类型 + `getCustomerStatistics()` → `get('/crm/customers/statistics')`。

### 3.2 B + C — StatFilterTabs 替手搓 status-tab-bar

- 删 `initStats()` 的 4 个 `getCustomerList` 调用 → `fetchStatistics()`(调 `getCustomerStatistics`,写 `statistics.value`)。
- 删整个 `.status-tab-bar` 模板块(line 29-44)+ 其 `<style>`(line 717-784)+ `statusTabs` computed 里的 rgba 颜色;改用 `StatFilterTabs`:
  ```ts
  const statusTabs = computed(() => [
    { key: '', label: '全部', count: stats.total },
    { key: 0, label: '潜在', count: stats.potential, color: 'var(--color-info)' },
    { key: 1, label: '活跃', count: stats.active, color: 'var(--color-success)' },
    { key: 2, label: '流失', count: stats.lost, color: 'var(--color-danger)' },
  ])
  ```
- **布局**:4 签 + 轻筛选(keyword 200 + org 130 + 重置 + 新增)一行放得下 → 走 **VehicleManage 范式**:`PageHeader #left` 放 `<StatFilterTabs inline v-model:active="活动态" @change>`,`#right` 放筛选 + 新增;容器加 `page-container--flush`。StatFilterTabs `v-model:active="activeStatusTab"`(已是 `'' | number`)+ `@change="handleTabChange"`(**沿用现有 handler**:设 `searchForm.status = key===''?undefined:key` → pageIndex=1 → fetchList);**保留 `searchForm.status`**(fetchList 仍用它发请求)。
  > 若 live 实测一行放不下(换行),回退「StatFilterTabs 独占整行」(同 ContractList)。
- 增删改后 `fetchStatistics()` 刷新(替原 `initStats()`)。

### 3.3 C — 表格迁 DataTable

- `a-table.customer-table` → `DataTable v-model:pagination`(删 `paginationConfig`/手算序号列/`handleTableChange`,`pagination` 改 `ref`,`@change="fetchList"`,`empty-text="暂无客户数据"`)。
- **删 `.customer-table` scoped 覆盖**:`:deep(.ant-table-thead) { background:#fafafa }`(让全局中性表头接管)、`:deep(.clickable-row):hover td { background: var(--color-primary-light)!important }`(全局已有中性 hover;若要保留「整行可点」的指针,保留 `cursor:pointer` 但去掉 `!important` 橙底)。`@row-click` 跳详情**保留**(行为 1:1)。
- 删手算序号列定义 + `#bodyCell` 的 `index` 分支(DataTable 内建)。

### 3.4 C — status-tag → StatusTag + 头像令牌 + 令牌化

- 列表状态单元格手搓 `.status-tag.status-{0,1,2}` → `<StatusTag :type="custStatusType(record.status)" dot>{{ statusTextMap[record.status] }}</StatusTag>`,映射 `0潜在→info / 1活跃→success / 2流失→danger`;删 `.status-tag` 整段 scoped(line 857-891)。
- **`--avatar-palette` 令牌**(`theme.ts` applyTheme,挨着 `--biz-*` 之后):
  ```ts
  s.setProperty('--avatar-palette-1', '#5B7290'); ... 6 个,值=现 bdColors 协调板
  ```
  `bdColors` 数组删除;`getBdColor(id)` 改返回 `var(--avatar-palette-${(id % 6) + 1})`,fallback `#d9d9d9` → `var(--text-disabled)`;`.bd-avatar` 文字 `#fff` → `var(--text-on-accent)`。
- 其余 `<style>` 与 `:style`/JS 字面色全令牌化:`rgba(0,0,0,.85)→var(--text-1)`、`.65→var(--text-2)`、`.45/.4→var(--text-3)`、`.25→var(--text-disabled)`、`.02→var(--bg-muted)`(hover);`#fff→var(--bg-card)`、`#f0f0f0→var(--border)`;`form-section-title` 同。tab-count 非活跃色 `rgba(0,0,0,.45)`、全部 tab 圆点 `rgba(0,0,0,.25)` 随 status-tab-bar 删除一并消失。
- **报价列三元顺手抽 computed**(可读性,行为 1:1):`getQuotationCount(record.code||String(record.id))` 在 `#bodyCell` 内算一次存局部变量,驱动 class/点击/cursor。

## 4. Tier 2:CustomerDetail(C tokenize,不重构)

- **11 张 `bordered` 表 → `:bordered="false"`**(不迁 DataTable——多为 `:pagination=false` 内嵌子表/或自带分页配置,仅去边即可;`a-descriptions bordered` 保留)。
- **`a-tag :color` 预设色名 → StatusTag**,映射表(语义有序的走语义,纯分类的走中性/克制):
  | 维度 | 原 ant 色 | StatusTag type |
  |---|---|---|
  | 客户状态 0/1/2 | blue/success/error | info/success/danger |
  | 主联系人 | gold | warning |
  | 拜访方式 1上门/2电话/3线上/4其他 | blue/green/purple/default | **全 default**(纯分类,标签文字区分;去装饰色) |
  | 工单状态 0..4 | default/processing/warning/success/error | default/info/warning/success/default |
  | 工单优先级 1紧急/2高/3中/4低 | red/orange/blue/default | danger/warning/info/default |
  | 合同状态(1 生效/其他) | success/default | success/default |
  | 账单状态 0..3 | default/blue/orange/green | default/info/warning/success |
  | 预付状态 0待确认/1已确认/2取消 | processing/success/default | info/success/default |
  | 时间线类型 visit/order/contract/prepayment/feedback | blue/orange/green/gold/red | info/warning/success/warning/danger |
  - 时间线 `a-timeline-item :color`(圆点)同样不吃语义类——传**真 hex 或 var()**:用 `timelineDotColor(type)` 返 `var(--color-info/warning/success/...)`(ant timeline-item 自定义色走 DOM inline style,`var()` 可解析,见 ContractList 续签链先例)。`timelineColor()` 拆为 `timelineTagType()`(给 StatusTag) + `timelineDotColor()`(给圆点)。
- **inline 色 → 令牌**:时间线 `#999→var(--text-3)`、`#666→var(--text-2)`(line 287/293/294)。积分 `:style` 的 `var(--color-success/danger)`、财务 `a-statistic` 的 `var(--color-*)` **已是令牌,保留**。
- 毛利曲线 `v-chart` 无显式色板(echarts 默认)——可选补协调板,**本轮不强求**(line chart 默认色影响小,归数据可视化专项)。

## 5. 验证

- 后端 `dotnet build`(CRM 模块 + WebAPI 集成 0 err)。
- 前端 `vite build` 0;`stylelint` CustomerManage/CustomerDetail/StatFilterTabs(若动)= 0。
- `rg` 裸 hex/rgba 字面复扫 `web/src/views/crm/Customer*.vue` 清零(ECharts 默认色板/已令牌的 `var()` 除外)。
- 用户 9001 live 逐页:CustomerManage(StatFilterTabs 计数/快筛/表格观感/BD 头像色/状态 tag/新增编辑抽屉)、CustomerDetail(11 Tab 表去边、各状态 tag 中性化、时间线令牌色)。

## 6. 执行方式

承通法:spec → plan(writing-plans)→ 子代理驱动逐任务双段审 → 中央复验 + 终审 → 用户 live。后端 statistics 独立先行;`--avatar-palette` 令牌(theme.ts)作为 CustomerManage C 的前置;Detail 的 a-tag 映射量大,单独成任务。

## 7. 明确不做(A/D 行为,另排期)

行点击 vs 单元格点击边界重整、详情 11 Tab 顶部 KPI 速览(财务数字上移)、客户流转/去重、毛利曲线换板(数据可视化专项)。
