# 客户模块视觉债 + CustomerManage·Detail C+B 收敛 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 CRM 客户模块对齐"中性墨+橙点缀"设计系统(清视觉债+迁三件套),给 CustomerManage 加 StatFilterTabs 状态快筛(后端补只读 statistics 端点),并新增 `--avatar-palette-*` 设计令牌;不碰业务行为。

**Architecture:** Tier1 = CustomerManage 全套(后端 statistics → StatFilterTabs + DataTable + StatusTag + 头像令牌 + 全令牌化);Tier2 = CustomerDetail C tokenize(11 表去边 + a-tag 预设色→StatusTag + inline 色→令牌)。前端无测试运行器→验证 build+stylelint+rg+live;后端只读 GroupBy 按 contract/vehicle 先例不单测。

**真值锁定(theme.ts):** success `#3E9E6E`/warning `#D49A2E`/danger `#D6584E`/info `#5B7290`/text-1 `#1F2329`/text-2 `#5A6068`/text-3 `#8A9099`/text-disabled `#BFC3C9`/bg-card `#FFFFFF`/bg-muted `#F1F3F6`/border `#ECEEF1`/text-on-accent `#FFFFFF`。BD 头像协调板 6 色 `#5B7290/#6BA292/#C99A6B/#9B8AB8/#C77B6B/#8FB07E`。

**参照:** `web/src/views/vehicle/VehicleManage.vue`(三件套范式)、已完成的 ContractList(`src/STOTOP.Module.Contract` statistics + `web/src/views/contract/ContractList.vue`)。

---

## Phase A — 后端客户状态统计端点

### Task A1: 新增 GET /crm/customers/statistics

**Files:**
- Modify: `src/STOTOP.Module.CRM/Dtos/CustomerDto.cs`(文件尾追加 2 DTO)
- Modify: `src/STOTOP.Module.CRM/Services/Interfaces/ICustomerService.cs`
- Modify: `src/STOTOP.Module.CRM/Services/CustomerService.cs`
- Modify: `src/STOTOP.Module.CRM/Controllers/CustomerController.cs`

- [ ] **Step 1: DTO** — `CustomerDto.cs` 末尾追加:

```csharp
/// <summary>客户状态统计</summary>
public class CustomerStatisticsDto
{
    public int TotalCount { get; set; }
    public List<CustomerStatusGroupDto> ByStatus { get; set; } = new();
}

public class CustomerStatusGroupDto
{
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
}
```

- [ ] **Step 2: 接口** — `ICustomerService.cs` 加(放 `GetCustomersAsync` 附近):

```csharp
/// <summary>客户状态统计(各状态计数)</summary>
Task<CustomerStatisticsDto> GetStatisticsAsync();
```

- [ ] **Step 3: 实现** — `CustomerService.cs` 加(放 `GetCustomersAsync` 之后)。基查询与列表一致(`_customerRepository.Query()`,组织隔离仓储自动);GroupBy + 3 态兜底:

```csharp
public async Task<CustomerStatisticsDto> GetStatisticsAsync()
{
    var grouped = await _customerRepository.Query()
        .GroupBy(c => c.FStatus)
        .Select(g => new { Status = g.Key, Count = g.Count() })
        .ToListAsync();

    var statusNames = new[] { "潜在", "活跃", "流失" };
    var byStatus = new List<CustomerStatusGroupDto>();
    for (var s = 0; s < statusNames.Length; s++)
    {
        byStatus.Add(new CustomerStatusGroupDto
        {
            Status = s,
            StatusName = statusNames[s],
            Count = grouped.FirstOrDefault(x => x.Status == s)?.Count ?? 0
        });
    }
    return new CustomerStatisticsDto { TotalCount = byStatus.Sum(b => b.Count), ByStatus = byStatus };
}
```
（`Microsoft.EntityFrameworkCore` 的 `ToListAsync` 已在文件 using 中——`GetCustomersAsync` 已用；若缺则补 `using Microsoft.EntityFrameworkCore;`。）

- [ ] **Step 4: Controller** — `CustomerController.cs` 在 `[HttpGet("{code}")]`(GetByCode)**之前**插入:

```csharp
[HttpGet("statistics")]
[RequirePermission(CrmPermissions.CustomerView)]
public async Task<ApiResult<CustomerStatisticsDto>> GetStatistics()
    => ApiResult<CustomerStatisticsDto>.Success(await _customerService.GetStatisticsAsync());
```
> `statistics` 是字面段,ASP.NET 路由字面段优先于 `{code}` 参数段;放 `{code}` 之前更稳妥。

- [ ] **Step 5: 编译** — Run: `dotnet build src/STOTOP.Module.CRM/STOTOP.Module.CRM.csproj`  → Expected: 0 Error。

- [ ] **Step 6: 提交**

```bash
git add src/STOTOP.Module.CRM/
git commit -m "feat(crm): 新增客户状态统计端点 GET /crm/customers/statistics(供 StatFilterTabs 计数)"
```

---

## Phase B — 前端 API + 令牌 + CustomerManage

### Task B1: api/crm.ts 类型与函数

**Files:** Modify `web/src/api/crm.ts`

- [ ] **Step 1: 类型** — 在 `CustomerListItemDto` 定义之后追加:

```ts
export interface CustomerStatusGroupDto { status: number; statusName: string; count: number }
export interface CustomerStatisticsDto { totalCount: number; byStatus: CustomerStatusGroupDto[] }
```

- [ ] **Step 2: 函数** — 在 `getCustomerList` 之后追加:

```ts
export function getCustomerStatistics() {
  return get('/crm/customers/statistics')
}
```
（确认 `get` 已从 `./request` 导入——`getCustomerList` 已用。）

- [ ] **Step 3: 提交**

```bash
git add web/src/api/crm.ts
git commit -m "feat(crm): 前端 getCustomerStatistics + 类型"
```

### Task B2: 新增 --avatar-palette 令牌

**Files:** Modify `web/src/stores/theme.ts`

- [ ] **Step 1: 加令牌** — 在 `applyTheme` 函数里 `--biz-finance` 那行(`s.setProperty('--biz-finance', '#B8860B')`)**之后**追加:

```ts
// 头像色环(CustomerManage BD 头像等;DOM 元素走 var() 可解析)
s.setProperty('--avatar-palette-1', '#5B7290')
s.setProperty('--avatar-palette-2', '#6BA292')
s.setProperty('--avatar-palette-3', '#C99A6B')
s.setProperty('--avatar-palette-4', '#9B8AB8')
s.setProperty('--avatar-palette-5', '#C77B6B')
s.setProperty('--avatar-palette-6', '#8FB07E')
```

- [ ] **Step 2: 提交**

```bash
git add web/src/stores/theme.ts
git commit -m "feat(theme): 新增 --avatar-palette-1..6 头像色环令牌"
```

### Task B3: CustomerManage.vue 全收敛(C+B)

**Files:** Modify `web/src/views/crm/CustomerManage.vue`

> 一次性改完保持 build 绿。**严格 1:1 保留交互**(`@row-click` 跳详情、报价点击、抽屉 CRUD)。

- [ ] **Step 1: imports** — 加:
  ```ts
  import DataTable from '@/components/DataTable.vue'
  import StatFilterTabs from '@/components/StatFilterTabs.vue'
  import StatusTag from '@/components/StatusTag.vue'
  import { getCustomerStatistics, type CustomerStatisticsDto } from '@/api/crm'
  ```
  （`getCustomerStatistics`/类型 并入已有 `@/api/crm` import 块。）

- [ ] **Step 2: 容器 flush + PageHeader #left/#right** — 根 `<div class="page-container">` → `<div class="page-container page-container--flush">`;把 `<PageHeader title="客户管理">` 的 `#actions` 整块替换为:

```vue
      <template #left>
        <StatFilterTabs inline v-model:active="activeStatusTab" :tabs="statusTabs" @change="handleTabChange" />
      </template>
      <template #right>
        <a-input v-model:value="searchForm.keyword" size="middle" placeholder="简称 / 联系人 / 电话" allow-clear style="width: 200px" @keyup.enter="handleSearch" />
        <a-select v-model:value="searchForm.orgId" size="middle" placeholder="所属组织" allow-clear style="width: 130px" :options="orgOptions" show-search :filter-option="(input: string, option: any) => option.label?.toLowerCase().includes(input.toLowerCase())" @change="handleSearch" />
        <a-button size="middle" @click="handleReset"><template #icon><ReloadOutlined /></template>重置</a-button>
        <a-button v-if="has(CrmPermissions.CustomerCreate)" type="primary" size="middle" @click="handleAdd"><template #icon><PlusOutlined /></template>新增客户</a-button>
      </template>
```
> 删原 `#actions` 里的 `<a-divider>`。`@change="handleSearch"` 给 org 下拉即时筛选(对齐样板,行为增强可接受;若要 1:1 严格可不加,但 ContractList 类型下拉已加 @change 为定例)。

- [ ] **Step 3: 删手搓 status-tab-bar 模板** — 删除整个 `<!-- 状态快筛 Tab 兼 KPI 指标条 --> <div class="status-tab-bar">...</div>`(原 line 29-44)。

- [ ] **Step 4: 表格 → DataTable** — 把 `<a-table ... class="customer-table" ...>` 整块替换为:

```vue
    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 1100 }"
      row-key="id"
      empty-text="暂无客户数据"
      :row-class-name="() => 'clickable-row'"
      @change="fetchList"
      @row-click="(record: any) => handleViewDetail(record)"
    >
      <template #bodyCell="{ column, record }">
        <!-- 客户名称 -->
        <template v-if="column.key === 'customerName'">
          <div class="customer-name-cell">
            <span class="name-primary">{{ record.shortName }}</span>
            <span v-if="record.fullName" class="name-secondary">{{ record.fullName }}</span>
          </div>
        </template>
        <!-- 报价数量 -->
        <template v-if="column.key === 'quotationCount'">
          <span
            class="quotation-count"
            :class="qc(record) > 0 ? 'count-positive' : 'count-zero'"
            @click.stop="qc(record) > 0 && handleQuotation(record)"
            :style="qc(record) > 0 ? 'cursor:pointer' : ''"
          >{{ quotationCountLoading ? '…' : qc(record) }}</span>
        </template>
        <!-- 联系信息 -->
        <template v-if="column.key === 'contactInfo'">
          <div class="contact-cell">
            <span v-if="record.contact" class="contact-name"><UserOutlined class="contact-icon" />{{ record.contact }}</span>
            <span v-if="record.phone" class="contact-phone"><PhoneOutlined class="contact-icon" />{{ record.phone }}</span>
            <span v-if="!record.contact && !record.phone" class="text-muted">-</span>
          </div>
        </template>
        <!-- 状态 -->
        <template v-if="column.dataIndex === 'status'">
          <StatusTag :type="custStatusType(record.status)" dot>{{ statusTextMap[record.status] || '未知' }}</StatusTag>
        </template>
        <!-- BD负责人 -->
        <template v-if="column.key === 'bd'">
          <template v-if="record.bdEmployeeId">
            <div class="bd-cell">
              <span class="bd-avatar" :style="{ background: getBdColor(record.bdEmployeeId) }">{{ getEmployeeName(record.bdEmployeeId).charAt(0) }}</span>
              <span>{{ getEmployeeName(record.bdEmployeeId) }}</span>
            </div>
          </template>
          <span v-else class="text-muted">-</span>
        </template>
        <!-- 操作 -->
        <template v-if="column.key === 'action'">
          <div class="action-cell" @click.stop>
            <a-button type="link" size="small" @click="handleViewDetail(record as any)"><EyeOutlined />详情</a-button>
            <a-button v-if="has(CrmPermissions.CustomerEdit)" type="link" size="small" @click="handleEdit(record as any)"><EditOutlined />编辑</a-button>
            <a-dropdown trigger="click">
              <a-button type="link" size="small"><EllipsisOutlined /></a-button>
              <template #overlay>
                <a-menu>
                  <a-menu-item @click="handleQuotation(record)"><DollarOutlined />报价</a-menu-item>
                  <a-menu-item v-if="has(CrmPermissions.CustomerDelete)" danger @click="confirmDelete(record as any)"><DeleteOutlined />删除</a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>
          </div>
        </template>
      </template>
    </DataTable>
```
> 变化:去手算 `index` 列分支(DataTable 内建序号);`getQuotationCount(record.code||String(record.id))` 三处调用 → 局部 helper `qc(record)`(见 Step 6);联系图标内联 `style="font-size:11px;margin-right:3px;opacity:.5"` → class `contact-icon`(Step 8 样式)。`#emptyText` 用 `empty-text` prop。`@row-click`/`@click.stop` 行为保留。

- [ ] **Step 5: tableColumns 删序号列** — 删首项 `{ title: '序号', dataIndex: 'index', ... }`。

- [ ] **Step 6: 状态映射 + qc helper + pagination ref + statistics** —
  - 加 `function custStatusType(s: number): 'success'|'warning'|'danger'|'info'|'default' { return (['info','success','danger'] as const)[s] || 'default' }`
  - 加 `function qc(record: any): number { return getQuotationCount(record.code || String(record.id)) }`
  - `getBdColor` 改:
    ```ts
    function getBdColor(id?: number): string {
      if (!id) return 'var(--text-disabled)'
      return `var(--avatar-palette-${(id % 6) + 1})`
    }
    ```
    （删 `const bdColors = [...]` 数组。)
  - `const pagination = reactive({...})` → `const pagination = ref({ pageIndex: 1, pageSize: 20, total: 0 })`;删 `paginationConfig` computed、删 `handleTableChange`;`fetchList`/`handleSearch`/`handleReset`/`handleTabChange` 内所有 `pagination.X` → `pagination.value.X`。
  - 删 `initStats()` 函数,新增:
    ```ts
    const statistics = ref<CustomerStatisticsDto>({ totalCount: 0, byStatus: [] })
    function getStatCount(s: number): number { return statistics.value.byStatus.find(g => g.status === s)?.count ?? 0 }
    async function fetchStatistics() {
      try { const res = await getCustomerStatistics() as any; if (res) statistics.value = res } catch { /* ignore */ }
    }
    ```
  - `stats` reactive 删除;`statusTabs` 改用 statistics:
    ```ts
    const statusTabs = computed(() => [
      { key: '', label: '全部', count: statistics.value.totalCount },
      { key: 0, label: '潜在', count: getStatCount(0), color: 'var(--color-info)' },
      { key: 1, label: '活跃', count: getStatCount(1), color: 'var(--color-success)' },
      { key: 2, label: '流失', count: getStatCount(2), color: 'var(--color-danger)' },
    ])
    ```
  - `handleTabChange(key)` 内 `pagination.pageIndex = 1` → `pagination.value.pageIndex = 1`(其余逻辑不变:setActive + searchForm.status + fetchList)。
  - `onMounted`、`handleSubmit`、`confirmDelete` 里的 `initStats()` 调用 → `fetchStatistics()`。

- [ ] **Step 7: 删 status-tag/status-tab-bar/customer-table 旧样式** — `<style>` 内删除:`.status-tab-bar`/`.status-tab-item`/`.tab-dot`/`.tab-label`/`.tab-count` 整段(原 717-784)、`.status-tag` 及 `.status-0/1/2` 整段(原 857-891)、`.customer-table` 整段(原 787-800,含 `#fafafa` 表头覆盖与 `!important` hover)。新增轻量行指针样式(替代 customer-table 的 clickable-row):
  ```scss
  :deep(.clickable-row) { cursor: pointer; }
  ```

- [ ] **Step 8: 剩余样式令牌化** — 替换 `<style>` 内所有字面色:
  - `.customer-name-cell .name-primary` `color: rgba(0,0,0,0.85)` → `var(--text-1)`
  - `.name-secondary` `rgba(0,0,0,0.4)` → `var(--text-3)`
  - `.contact-cell .contact-name` `rgba(0,0,0,0.75)` → `var(--text-2)`
  - `.contact-phone` `rgba(0,0,0,0.45)` → `var(--text-3)`
  - `.bd-avatar` `color: #fff` → `var(--text-on-accent)`
  - `.text-muted` `rgba(0,0,0,0.25)` → `var(--text-disabled)`
  - `.form-section-title` `rgba(0,0,0,0.65)` → `var(--text-2)`
  - 新增 `.contact-icon { font-size: 11px; margin-right: 3px; opacity: .5; }`(替代联系图标内联 style)

- [ ] **Step 9: 验证** —
  - `cd web && npx stylelint "src/views/crm/CustomerManage.vue"` → 0
  - `rg -n "#[0-9a-fA-F]{6}\b|rgba\(0, ?0, ?0" web/src/views/crm/CustomerManage.vue` → 无输出
  - `cd web && npx vite build` → 成功

- [ ] **Step 10: 提交**

```bash
git add web/src/views/crm/CustomerManage.vue
git commit -m "refactor(crm): CustomerManage 迁三件套+StatFilterTabs状态快筛+头像令牌+全令牌化(C+B收敛)"
```

---

## Phase C — CustomerDetail tokenize

### Task C1: CustomerDetail.vue C 清理(不重构 Tab)

**Files:** Modify `web/src/views/crm/CustomerDetail.vue`

- [ ] **Step 1: import StatusTag** — 加 `import StatusTag from '@/components/StatusTag.vue'`。

- [ ] **Step 2: 11 张表去边框** — 把所有 `<a-table ... bordered ...>` 的 `bordered` → `:bordered="false"`(联系人/拜访/工单/合同/账单/预付/运单发放/积分/财务 共 9 处具名 a-table;`a-descriptions bordered` **保留**)。

- [ ] **Step 3: 映射 helper(替换原 color map)** — 脚本里把状态色 map 换成 StatusTag type map(语义有序走语义、纯分类走中性);删除原 `*ColorMap`/`*Color`(仅保留 `*TextMap`/`*Text`):

```ts
type STagType = 'success' | 'warning' | 'danger' | 'info' | 'default'
const custStatusType = (s: number): STagType => (['info','success','danger'] as const)[s] || 'default'
const methodTagType = (_m: number): STagType => 'default'                                   // 拜访方式纯分类→中性
const orderStatusTagType = (s: number): STagType => (['default','info','warning','success','default'] as const)[s] || 'default'
const priorityTagType = (p: number): STagType => (({1:'danger',2:'warning',3:'info',4:'default'} as Record<number,STagType>)[p]) || 'default'
const invoiceStatusTagType = (s: number): STagType => (['default','info','warning','success'] as const)[s] || 'default'
const timelineTagType = (t: string): STagType => (({visit:'info',order:'warning',contract:'success',prepayment:'warning',feedback:'danger'} as Record<string,STagType>)[t]) || 'default'
const timelineDotColor = (t: string): string => ({visit:'var(--color-info)',order:'var(--color-warning)',contract:'var(--color-success)',prepayment:'var(--color-warning)',feedback:'var(--color-danger)'} as Record<string,string>)[t] || 'var(--text-3)'
```
（保留 `statusTextMap`/`methodTextMap`/`orderStatusText`/`priorityText`/`invoiceStatusText`/`timelineTypeText`。删 `statusColorMap`/`methodColorMap`/`orderStatusColor`/`priorityColor`/`invoiceStatusColor`，`timelineColor` 拆为上面两个。)

- [ ] **Step 4: 模板 a-tag → StatusTag** — 逐处替换(保留文字逻辑):
  - 基本信息状态(line 21-23):`<a-tag :color="statusColorMap[customer.status]...">` → `<StatusTag :type="custStatusType(customer.status)" dot>{{ statusTextMap[customer.status] || '未知' }}</StatusTag>`
  - 联系人主联系人(line 51):`<a-tag color="gold">主联系人</a-tag>` → `<StatusTag type="warning">主联系人</StatusTag>`
  - 拜访方式(line 81-83):`<StatusTag :type="methodTagType(record.visitMethod)">{{ methodTextMap[record.visitMethod] || '其他' }}</StatusTag>`
  - 工单状态(line 102-104):`<StatusTag :type="orderStatusTagType(record.status)" dot>{{ orderStatusText[record.status] || '未知' }}</StatusTag>`
  - 工单优先级(line 107-109):`<StatusTag :type="priorityTagType(record.priority)">{{ priorityText[record.priority] || '-' }}</StatusTag>`
  - 合同状态(line 128-130):`<StatusTag :type="record.status === 1 ? 'success' : 'default'">{{ record.status === 1 ? '生效' : '草稿' }}</StatusTag>`
  - 账单状态(line 160-162):`<StatusTag :type="invoiceStatusTagType(record.status)" dot>{{ invoiceStatusText[record.status] || '未知' }}</StatusTag>`
  - 预付状态(line 189-191):`<StatusTag :type="record.status === 1 ? 'success' : record.status === 0 ? 'info' : 'default'">{{ record.status === 0 ? '待确认' : record.status === 1 ? '已确认' : '已取消' }}</StatusTag>`
  - 时间线 tag(line 290):`<a-tag :color="timelineColor(item.type)" size="small">` → `<StatusTag :type="timelineTagType(item.type)">{{ timelineTypeText(item.type) }}</StatusTag>`(原 tag 内已是 `timelineTypeText`)
  - 时间线圆点(line 284):`:color="timelineColor(item.type)"` → `:color="timelineDotColor(item.type)"`(a-timeline-item)

- [ ] **Step 5: 时间线 inline 色 → 令牌** —
  - line 287 `<span style="color: #999; font-size: 12px">` → `<span style="color: var(--text-3); font-size: 12px">`
  - line 293 `<div ... style="color: #666; margin-top: 4px; font-size: 13px">` → `color: var(--text-2)`
  - line 294 `<div ... style="color: #999; margin-top: 2px; font-size: 12px">` → `color: var(--text-3)`
  - （积分 `:style` 与财务 `a-statistic` 的 `var(--color-*)` 已是令牌,不动。)

- [ ] **Step 6: 验证** —
  - `cd web && npx stylelint "src/views/crm/CustomerDetail.vue"` → 0(注:本文件 `<style>` 为空,主要看模板/脚本)
  - `rg -n "#[0-9a-fA-F]{6}\b|color=\"(blue|green|purple|gold|orange|red)\"|:color=\"(statusColorMap|methodColorMap|orderStatusColor|priorityColor|invoiceStatusColor|timelineColor)" web/src/views/crm/CustomerDetail.vue` → 无输出(预设色名/旧 color map 清零;timelineDotColor 的 var() 不计)
  - `cd web && npx vite build` → 成功

- [ ] **Step 7: 提交**

```bash
git add web/src/views/crm/CustomerDetail.vue
git commit -m "refactor(crm): CustomerDetail 11表去边+a-tag预设色→StatusTag+时间线令牌化(C)"
```

---

## Phase D — 中央复验

### Task D1: 全量构建 + stylelint + 裸色终扫

- [ ] **Step 1: 后端** — `dotnet build src/STOTOP.WebAPI/STOTOP.WebAPI.csproj` → 0 Error。
- [ ] **Step 2: 前端** — `cd web && npx vite build` → 成功(vue-tsc 基线红不阻断)。
- [ ] **Step 3: stylelint** — `cd web && npx stylelint "src/views/crm/CustomerManage.vue" "src/views/crm/CustomerDetail.vue"` → 0。
- [ ] **Step 4: 裸 hex/rgba 终扫** — `rg -n "#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3}\b|rgba\(0, ?0, ?0" web/src/views/crm/CustomerManage.vue web/src/views/crm/CustomerDetail.vue` → 无输出。
- [ ] **Step 5: ant 预设色名终扫** — `rg -n "color=\"(blue|green|purple|gold|orange|red|cyan|magenta|lime|geekblue|volcano)\"" web/src/views/crm/CustomerManage.vue web/src/views/crm/CustomerDetail.vue` → 无输出。
- [ ] **Step 6: 用户 live 逐页验** —
  - `/crm/customers`:StatFilterTabs(全部/潜在/活跃/流失 计数+快筛+`#left` 一行是否放得下、放不下则回退独占行)、表格无边框观感、BD 头像色环、状态 StatusTag、`@row-click` 跳详情、新增/编辑抽屉、报价点击。
  - `/crm/customers/:id`:11 Tab 表去边、各状态 tag 中性/语义化、时间线圆点+tag 令牌色、积分/财务数字色。
- [ ] **Step 7: 补修提交(如有)** —
  ```bash
  git add web/src/views/crm/ src/STOTOP.Module.CRM/ web/src/stores/theme.ts
  git commit -m "fix(crm): 中央复验补修"
  ```

---

## 执行顺序

A(后端)→ B1(API)/B2(令牌)→ B3(CustomerManage,依赖 B1+B2)→ C1(CustomerDetail,独立)→ D1。B3 与 C1 不同文件可并行。

## 自查覆盖(spec → plan)

- §3.1 后端 statistics → A1 ✅(DTO/接口/GroupBy 兜底/controller 路由优先级)
- §3.2 StatFilterTabs 替 status-tab-bar + 删 4 调用 → B3 Step2/3/6 ✅
- §3.3 DataTable + 删 #fafafa/!important → B3 Step4/7 ✅
- §3.4 StatusTag + --avatar-palette + 令牌化 + 报价三元 → B2 + B3 Step6/8 ✅
- §4 CustomerDetail 去边/a-tag 映射/inline 令牌 → C1 ✅
- §5 验证 → D1 ✅
- §7 不做项(A/D 行为)→ 未触碰 ✅
