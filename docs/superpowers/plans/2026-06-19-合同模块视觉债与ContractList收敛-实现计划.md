# 合同模块视觉债清理 + ContractList B 收敛 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把合同模块 6 文件对齐"中性墨+橙点缀"设计系统(清视觉债+迁三件套),并给 ContractList 加 StatFilterTabs 状态快筛(后端补只读 statistics 端点),不碰任何业务行为。

**Architecture:** Tier 1 = `ContractList.vue` 全套(后端 statistics → StatFilterTabs + DataTable + 填满首行 + StatusTag);Tier 2 = 子页 C 收敛(3 列表子页全迁 DataTable+填满首行;Dashboard 仅 tokenize+ECharts 换真 hex;StatusFlow 已洁净)。前端无测试运行器,验证走 build+stylelint+rg 复扫+live;后端只读 GroupBy 按 vehicle 先例不单测,靠 build+live 计数核对。

**Tech Stack:** Vue 3 `<script setup>` + Ant Design Vue + 三件套(`PageLayout`/`DataTable`/`StatFilterTabs`/`StatusTag`)+ ECharts;后端 .NET 10 / EF Core / 仓储模式。

**样板范式参照(执行子代理务必先读):** `web/src/views/vehicle/VehicleManage.vue`(列表三件套完整范式)、`src/STOTOP.Module.Vehicle/Services/VehicleService.cs::GetStatisticsAsync`(statistics 范式)。

**设计令牌真值(本计划已查 `web/src/stores/theme.ts` 锁定):**
- success `#3E9E6E` / warning `#D49A2E` / danger `#D6584E` / info `#5B7290` / 中性 text-3 `#8A9099` / primary `#E85E00`
- `--bg-card #FFFFFF` / `--border #ECEEF1` / `--border-faint #F2F4F6` / `--text-1 #1F2329` / `--text-3 #8A9099` / `--bg-page #F7F8FA`

---

## Phase A — 后端 statistics 端点

### Task A1: 新增合同状态统计端点

**Files:**
- Modify: `src/STOTOP.Module.Contract/Dtos/ContractDto.cs`(文件尾追加两个 DTO)
- Modify: `src/STOTOP.Module.Contract/Services/Interfaces/IContractService.cs`(加方法签名)
- Modify: `src/STOTOP.Module.Contract/Services/ContractService.cs`(实现)
- Modify: `src/STOTOP.Module.Contract/Controllers/ContractController.cs`(加端点)

- [ ] **Step 1: DTO** — 在 `src/STOTOP.Module.Contract/Dtos/ContractDto.cs` 文件**末尾**追加:

```csharp
/// <summary>合同状态统计</summary>
public class ContractStatisticsDto
{
    public int TotalCount { get; set; }
    public List<ContractStatusGroupDto> ByStatus { get; set; } = new();
}

/// <summary>按状态分组统计</summary>
public class ContractStatusGroupDto
{
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
}
```

- [ ] **Step 2: 接口** — 在 `IContractService.cs` 接口体内追加方法签名(放在 `GetContractsAsync` 附近):

```csharp
/// <summary>合同状态统计(各状态计数)</summary>
Task<ContractStatisticsDto> GetStatisticsAsync();
```

- [ ] **Step 3: 实现** — 在 `ContractService.cs` 内新增方法(放在 `GetContractsAsync` 之后)。基查询与列表一致(`_contractRepository.Query()`,组织隔离由仓储全局过滤自动生效,不额外加软删过滤);GroupBy 后对 6 个状态做兜底(GroupBy 不产 0 计数行):

```csharp
public async Task<ContractStatisticsDto> GetStatisticsAsync()
{
    var grouped = await _contractRepository.Query()
        .GroupBy(c => c.FStatus)
        .Select(g => new { Status = g.Key, Count = g.Count() })
        .ToListAsync();

    var statusNames = new[] { "草稿", "审批中", "待签署", "已生效", "已到期", "已终止" };
    var byStatus = new List<ContractStatusGroupDto>();
    for (var s = 0; s < statusNames.Length; s++)
    {
        byStatus.Add(new ContractStatusGroupDto
        {
            Status = s,
            StatusName = statusNames[s],
            Count = grouped.FirstOrDefault(x => x.Status == s)?.Count ?? 0
        });
    }

    return new ContractStatisticsDto
    {
        TotalCount = byStatus.Sum(b => b.Count),
        ByStatus = byStatus
    };
}
```

- [ ] **Step 4: Controller** — 在 `ContractController.cs` 的 `GetById`(`[HttpGet("{id}")]`)**之前**新增静态段端点(`statistics` 非数字,路由按字面段优先,但放前更稳妥):

```csharp
[HttpGet("statistics")]
[RequirePermission(ContractPermissions.ContractView)]
public async Task<ApiResult<ContractStatisticsDto>> GetStatistics()
    => ApiResult<ContractStatisticsDto>.Success(await _service.GetStatisticsAsync());
```

- [ ] **Step 5: 编译** — Run: `dotnet build src/STOTOP.Module.Contract/STOTOP.Module.Contract.csproj`
  Expected: Build succeeded, 0 Error。

- [ ] **Step 6: 提交**

```bash
git add src/STOTOP.Module.Contract/
git commit -m "feat(contract): 新增合同状态统计端点 GET /contracts/statistics(供 StatFilterTabs 计数)"
```

> 说明:Contract 无测试工程,vehicle 同款 `GetStatisticsAsync` 亦无单测;本 GroupBy 平凡,按既有约定不新建测试工程,正确性由 build + 用户 live 的 StatFilterTabs 计数核对。

---

## Phase B — 前端 API + ContractList 主页

### Task B1: api/contract.ts 增类型与函数

**Files:**
- Modify: `web/src/api/contract.ts`

- [ ] **Step 1: 加类型** — 在 `web/src/api/contract.ts` 的 `ContractListItemDto` 定义(约 line 98-111)**之后**追加:

```ts
export interface ContractStatusGroupDto {
  status: number
  statusName: string
  count: number
}

export interface ContractStatisticsDto {
  totalCount: number
  byStatus: ContractStatusGroupDto[]
}
```

- [ ] **Step 2: 加函数** — 在 `getContractList`(约 line 309)**之后**追加:

```ts
export function getContractStatistics() {
  return get('/contract/contracts/statistics')
}
```

- [ ] **Step 3: 提交**

```bash
git add web/src/api/contract.ts
git commit -m "feat(contract): 前端 getContractStatistics + 类型"
```

### Task B2: ContractList.vue 全收敛(C+B)

**Files:**
- Modify: `web/src/views/contract/ContractList.vue`

> 同一文件的模板+脚本一次性改完保持 build 绿。**严格 1:1 保留所有交互**(弹窗/抽屉/审批/续签/删除流程不动),仅做"结构收敛 + 令牌化"。

- [ ] **Step 1: 改 imports**(`<script setup>` 顶部)
  - 加图标 `ReloadOutlined`:`import { PlusOutlined, DeleteOutlined, ReloadOutlined } from '@ant-design/icons-vue'`
  - 加三件套与 API:
    ```ts
    import DataTable from '@/components/DataTable.vue'
    import StatFilterTabs from '@/components/StatFilterTabs.vue'
    import StatusTag from '@/components/StatusTag.vue'
    ```
  - 在 `@/api/contract` 的 import 里追加 `getContractStatistics` 与 `type ContractStatisticsDto`。
  - 保留 `PageHeader`、`EmptyState`(EmptyState 仍用于抽屉内嵌表)、`ContractStatusFlow`。

- [ ] **Step 2: 根容器加 flush** — `<div class="page-container">` → `<div class="page-container page-container--flush">`

- [ ] **Step 3: PageHeader 改 #left/#right(替换原 #actions + #toolbar)** — 把 `<PageHeader ...>` 内整块 `#actions` 与 `#toolbar` 替换为:

```vue
      <template #left>
        <StatFilterTabs inline v-model:active="searchForm.status" :tabs="statusTabs" @change="handleSearch" />
      </template>
      <template #right>
        <a-input v-model:value="searchForm.keyword" size="middle" placeholder="合同号/标题" style="width: 200px" allow-clear @keyup.enter="handleSearch" />
        <a-select v-model:value="searchForm.typeId" size="middle" placeholder="类型" style="width: 140px" allow-clear :options="typeOptions" />
        <a-range-picker v-model:value="searchForm.dateRange" size="middle" style="width: 240px" />
        <a-button size="middle" @click="handleReset">
          <template #icon><ReloadOutlined /></template>重置
        </a-button>
        <a-button v-if="has(ContractPermissions.ContractCreate)" type="primary" size="middle" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新建合同
        </a-button>
      </template>
```

> 注:原 status `a-select` 下拉删除(被 StatFilterTabs 取代);dateRange 控件原样保留(现状未 wire,1:1 不动)。

- [ ] **Step 4: 主表 a-card+a-table → DataTable** — 把 `<a-card :bordered="false"> ... </a-card>` 整块(含内部 `<a-table>`)替换为:

```vue
    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 1400 }"
      row-key="id"
      empty-text="暂无合同数据"
      @change="fetchList"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'title'">
          <a-tooltip :title="record.title">
            <a class="contract-title-link" @click="handleViewDetail(record)">{{ record.title }}</a>
          </a-tooltip>
        </template>
        <template v-if="column.dataIndex === 'typeName'">
          <a-tag>{{ record.typeName || '-' }}</a-tag>
        </template>
        <template v-if="column.dataIndex === 'amount'">
          {{ record.amount != null ? formatAmount(record.amount) : '-' }}
        </template>
        <template v-if="column.dataIndex === 'contractNature'">
          {{ natureText(record.contractNature) }}
        </template>
        <template v-if="column.dataIndex === 'status'">
          <StatusTag :type="statusTagType(record.status)">{{ statusText(record.status) }}</StatusTag>
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button type="link" size="small" @click="handleViewDetail(record)">查看</a-button>
          <a-button
            v-if="has(ContractPermissions.ContractEdit) && record.status === 0"
            type="link" size="small" @click="handleEdit(record)"
          >编辑</a-button>
          <a-button
            v-if="has(ContractPermissions.ContractApprove) && record.status === 0"
            type="link" size="small" @click="handleSubmitApproval(record)"
          >发起审批</a-button>
          <a-popconfirm
            v-if="has(ContractPermissions.ContractDelete) && record.status === 0"
            title="确定删除该合同吗？" ok-text="确定" cancel-text="取消" @confirm="handleDelete(record)"
          >
            <a-button type="link" size="small" danger>删除</a-button>
          </a-popconfirm>
        </template>
      </template>
    </DataTable>
```

> 变化点:去掉 `#bodyCell` 的 `index` 分支(DataTable 内建序号列);去 `bordered`;去 `:pagination` / `@change="handleTableChange"`(改 v-model:pagination + @change="fetchList");去 `#emptyText`(用 `empty-text` prop);标题 `<a style="cursor:pointer">` → `<a class="contract-title-link">`。

- [ ] **Step 5: 列定义删序号列** — `tableColumns` 数组删除首项 `{ title: '序号', dataIndex: 'index', ... }`(DataTable 自动加序号列)。其余列不动。

- [ ] **Step 6: 状态枚举区加 statusTagType** — 在 `statusColor` 函数附近新增(并保留 `statusText`;`statusColor` 可删,因不再被引用):

```ts
function statusTagType(s: number): 'success' | 'warning' | 'danger' | 'info' | 'default' {
  return (['default', 'info', 'warning', 'success', 'danger', 'default'] as const)[s] || 'default'
}
```

- [ ] **Step 7: searchForm.status 改 '' 范式** — `searchForm` 里 `status: undefined as number | undefined,` → `status: '' as '' | number,`

- [ ] **Step 8: pagination 改 ref + statistics 状态 + statusTabs/getStatusCount** — 把 `const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })` 与其下的 `paginationConfig` computed、`handleTableChange` 函数,整体替换为:

```ts
const pagination = ref({ pageIndex: 1, pageSize: 20, total: 0 })

const statistics = ref<ContractStatisticsDto>({ totalCount: 0, byStatus: [] })

function getStatusCount(s: number): number {
  return statistics.value.byStatus.find(g => g.status === s)?.count ?? 0
}

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

- [ ] **Step 9: fetchList 用 pagination.value + status 守卫** — `fetchList` 内:
  - `pageIndex: pagination.pageIndex` → `pageIndex: pagination.value.pageIndex`;`pageSize: pagination.pageSize` → `pagination.value.pageSize`
  - status 守卫:`if (searchForm.status !== undefined) params.status = searchForm.status` → `if (searchForm.status !== '' && searchForm.status !== undefined) params.status = searchForm.status`
  - 回填:`pagination.total = res?.total || res?.length || 0` → `pagination.value.total = res?.total || res?.length || 0`

- [ ] **Step 10: 新增 fetchStatistics** — 在 `fetchList` 之后新增:

```ts
async function fetchStatistics() {
  try {
    const res = await getContractStatistics() as any
    if (res) statistics.value = res
  } catch (e) { console.error('获取合同统计失败:', e) }
}
```

- [ ] **Step 11: handleSearch / handleReset 改 pagination.value + status''** —
  - `handleSearch`:`pagination.pageIndex = 1` → `pagination.value.pageIndex = 1`
  - `handleReset`:`searchForm.status = undefined` → `searchForm.status = ''`;`pagination.pageIndex = 1` → `pagination.value.pageIndex = 1`

- [ ] **Step 12: 增删改后刷新计数** — 在 `handleSubmit`、`handleDelete`、`handleSubmitApproval` 三处的 `fetchList()` 调用**后面**各加一行 `fetchStatistics()`。

- [ ] **Step 13: onMounted 加 fetchStatistics** — `onMounted` 内 `fetchList()` 之后加 `fetchStatistics()`。

- [ ] **Step 14: 内嵌表去边框** — 把以下 5 处 `<a-table ... bordered ...>` 的 `bordered` 改为 `:bordered="false"`(保留各自其余属性与 `#emptyText`/`size="small"`):
  - 合同方 modal 表(`v-if="formData.parties.length > 0"` 那张)
  - 详情抽屉:合同方表、条款表、签署记录表、提醒表(4 张)
  - **`a-descriptions bordered` 保留不动**(KV 网格合理例外)。

- [ ] **Step 15: 续签链 timeline 令牌化** — 抽屉"续签链" tab 内:
  - `<a-timeline-item color="blue">` → `<a-timeline-item color="var(--color-info)">`
  - `<a-timeline-item color="green">` → `<a-timeline-item color="var(--color-success)">`

- [ ] **Step 16: 条款关键 tag → StatusTag** — 抽屉条款表 `#bodyCell` 内:
  `<a-tag v-if="record.isKeyClause" color="error">关键条款</a-tag>` → `<StatusTag v-if="record.isKeyClause" type="danger">关键条款</StatusTag>`

- [ ] **Step 17: `<style scoped>` 令牌化** — 替换:
  - `.section-title` 内 `color: $text-primary;` → `color: var(--text-1);`
  - `.section-title` 内 `border-bottom: 1px solid $border-color-lighter;` → `border-bottom: 1px solid var(--border-faint);`
  - `.clause-item` 内 `background: $bg-page;` → `background: var(--bg-page);`;`border-radius: $border-radius-sm;` **保留**(尺寸令牌非颜色债)
  - 新增标题链接类(替代内联 cursor):
    ```scss
    .contract-title-link { cursor: pointer; }
    ```
  - `.cross-module-link` 已用 `var()`,不动。
  - 若 `$text-primary/$border-color-lighter/$bg-page` 替换后 `@use '@/styles/variables.scss' as *;` 不再被引用,可保留该 import(无害)。

- [ ] **Step 18: 验证** —
  - Run: `cd web && npx stylelint "src/views/contract/ContractList.vue"` → Expected: 0 problems。
  - Run: `cd web && npx vue-tsc --noEmit -p tsconfig.json 2>&1 | grep ContractList` → Expected: 无 **新增** ContractList 报错(vue-tsc 基线本就红,只看本文件未引入新错)。
  - Run(裸 hex 复扫): `rg -n "#[0-9a-fA-F]{6}|#[0-9a-fA-F]{3}\b" web/src/views/contract/ContractList.vue` → Expected: 无输出(本文件应 0 裸 hex)。

- [ ] **Step 19: 提交**

```bash
git add web/src/views/contract/ContractList.vue
git commit -m "refactor(contract): ContractList 迁三件套+StatFilterTabs状态快筛+令牌化(C+B收敛)"
```

---

## Phase C — 子页收敛

> C2/C3/C4 三个列表子页互相独立,可并行。统一改造范式(均确认为标准分页列表):①容器加 `page-container--flush` ②`a-card`+`a-table bordered`+`paginationConfig`+手算序号+`handleTableChange` → `DataTable v-model:pagination` + `@change="fetchList"` ③`pagination` 由 `reactive` 改 `ref`,`fetchList`/`handleSearch`/`handleReset`/`handleTableChange` 内所有 `pagination.X` 改 `pagination.value.X` ④删 `paginationConfig` computed、删 `handleTableChange`、删序号列定义与 `#bodyCell` 的 `index` 分支 ⑤工具栏 `#toolbar`/`#actions` → `#left`(筛选)/`#right`(主操作),控件 `size="small"` → `size="middle"` ⑥a-tag 状态 → `StatusTag` ⑦`a-alert` 提示条保留。导入加 `DataTable`/`StatusTag`(按需 `ReloadOutlined`)。

### Task C1: ContractDashboard.vue(C tokenize,保留仪表盘布局)

**Files:**
- Modify: `web/src/views/contract/ContractDashboard.vue`

> 仪表盘非列表,**不迁** PageLayout/DataTable;保留 `.contract-dashboard` flex-height 布局。仅 tokenize + ECharts 换真 hex + 内嵌表去边 + a-tag→StatusTag。

- [ ] **Step 1: import StatusTag** — `import StatusTag from '@/components/StatusTag.vue'`

- [ ] **Step 2: ECharts 状态柱换真 hex**(line ~221-228)— `itemStyle.color` 回调内:
  ```ts
  const colors = ['#909399', '#409eff', '#e6a23c', '#67c23a', '#f56c6c', '#909399']
  return colors[params.dataIndex] || '#409eff'
  ```
  →
  ```ts
  // 状态语义色(0草稿中性/1审批info/2待签warning/3生效success/4到期danger/5终止中性);ECharts 不解析 var()，用 theme.ts 真 hex
  const colors = ['#8A9099', '#5B7290', '#D49A2E', '#3E9E6E', '#D6584E', '#8A9099']
  return colors[params.dataIndex] || '#8A9099'
  ```

- [ ] **Step 3: 饼图(类型分布)设协调色板** — `updateCharts` 内 `pieChart.setOption({...})` 的顶层加 `color` 数组(与本仓库各图表内联协调板一致):
  ```ts
  pieChart.setOption({
    color: ['#5B7290', '#6BA292', '#C99A6B', '#9B8AB8', '#C77B6B', '#8FB07E', '#7C9CB5', '#B0976A'],
    tooltip: { ... },   // 其余原样
  ```

- [ ] **Step 4: 两张表去边框** — 到期预警表、续签待办表的 `bordered` → `:bordered="false"`(保留 `size="small"`/`:scroll`/`#emptyText`)。

- [ ] **Step 5: a-tag → StatusTag** —
  - remainDays:`<a-tag :color="record.remainDays <= 7 ? 'error' : 'warning'">{{ record.remainDays }}天</a-tag>` → `<StatusTag :type="record.remainDays <= 7 ? 'danger' : 'warning'">{{ record.remainDays }}天</StatusTag>`
  - 续签待办状态:`<a-tag :color="contractStatusColor(record.status)">{{ contractStatusText(record.status) }}</a-tag>` → `<StatusTag :type="statusTagType(record.status)">{{ contractStatusText(record.status) }}</StatusTag>`
  - 新增 `statusTagType`(同 ContractList 映射),删除不再引用的 `contractStatusColor`:
    ```ts
    function statusTagType(s: number): 'success' | 'warning' | 'danger' | 'info' | 'default' {
      return (['default', 'info', 'warning', 'success', 'danger', 'default'] as const)[s] || 'default'
    }
    ```

- [ ] **Step 6: `<style scoped>` 裸 hex/rgba → 令牌** — 替换:
  - `.kpi-bar { background: #fff; }` → `background: var(--bg-card);`
  - `.kpi-item + .kpi-item { ... border-left: 1px solid #d9d9d9; }` → `border-left: 1px solid var(--border);`
  - `.kpi-label { color: rgba(0, 0, 0, 0.45); }` → `color: var(--text-3);`
  - `.panel-title { color: rgba(0, 0, 0, 0.85); ... }` → `color: var(--text-1);`
  - `.content-panel { background: #fff; border: 1px solid #f0f0f0; }` → `background: var(--bg-card); border: 1px solid var(--border);`
  - `.quick-action-item { ... border: 1px solid #f0f0f0; }` → `border: 1px solid var(--border);`
  - 阴影 `box-shadow: 0 2px 8px rgba(0,0,0,.06/.08)` **保留**(中性阴影非配色债)。
  - KPI `:style="{ background/color: kpi.color }"` **保留**(kpi.color 已是 `var(--color-*)`)。

- [ ] **Step 7: 验证** —
  - `cd web && npx stylelint "src/views/contract/ContractDashboard.vue"` → 0 problems。
  - `rg -n "#[0-9a-fA-F]{6}" web/src/views/contract/ContractDashboard.vue` → Expected: 仅余 ECharts 两处合法色板(状态柱 6 色 + 饼图 8 色),**无其他裸 hex**。

- [ ] **Step 8: 提交**

```bash
git add web/src/views/contract/ContractDashboard.vue
git commit -m "refactor(contract): ContractDashboard 令牌化+ECharts换状态语义真hex+表去边"
```

### Task C2: ESignManage.vue

**Files:**
- Modify: `web/src/views/contract/ESignManage.vue`

- [ ] **Step 1: imports** — 加 `import DataTable from '@/components/DataTable.vue'`、`import StatusTag from '@/components/StatusTag.vue'`、图标 `import { UploadOutlined, ReloadOutlined } from '@ant-design/icons-vue'`。删 `EmptyState` import(DataTable 的 empty-text 接管空态)。

- [ ] **Step 2: 容器 flush** — `<div class="page-container">` → `<div class="page-container page-container--flush">`

- [ ] **Step 3: 工具栏 #toolbar → #left** — 把 `<template #toolbar>` 整块替换为:

```vue
      <template #left>
        <a-input v-model:value="searchForm.keyword" size="middle" placeholder="合同号" style="width: 200px" allow-clear @keyup.enter="handleSearch" />
        <a-select v-model:value="searchForm.signStatus" size="middle" placeholder="签署状态" style="width: 140px" allow-clear :options="signStatusOptions" />
        <a-button type="primary" size="middle" @click="handleSearch">查询</a-button>
        <a-button size="middle" @click="handleReset">
          <template #icon><ReloadOutlined /></template>重置
        </a-button>
      </template>
```

- [ ] **Step 4: a-alert 保留** — `<a-alert ... />` 不动(留在 PageHeader 与表之间)。

- [ ] **Step 5: a-card+a-table → DataTable** — 把 `<a-card :bordered="false"> ... </a-card>` 替换为:

```vue
    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 1100 }"
      row-key="id"
      empty-text="暂无电子签记录"
      @change="fetchList"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'signStatus'">
          <StatusTag :type="signStatusTagType(record.signStatus)">{{ signStatusText(record.signStatus) }}</StatusTag>
        </template>
        <template v-if="column.dataIndex === 'signedTime'">
          {{ record.signedTime || '-' }}
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button
            v-if="has(ContractPermissions.ESignManage) && record.signStatus === 0"
            type="link" size="small" @click="handleMarkSigned(record)"
          >标记已签</a-button>
          <a-button
            v-if="has(ContractPermissions.ESignManage) && record.signStatus === 0"
            type="link" size="small" danger @click="handleReject(record)"
          >拒签</a-button>
        </template>
      </template>
    </DataTable>
```

- [ ] **Step 6: 列删序号** — `tableColumns` 删首项 `{ title: '序号', dataIndex: 'index', ... }`。

- [ ] **Step 7: 状态映射 + pagination ref** —
  - 新增并删旧:把 `signStatusColor` 替换为
    ```ts
    function signStatusTagType(s: number): 'success' | 'danger' | 'info' | 'default' {
      return (['info', 'success', 'danger'] as const)[s] || 'default'
    }
    ```
  - `const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })` → `const pagination = ref({ pageIndex: 1, pageSize: 20, total: 0 })`
  - 删 `paginationConfig` computed、删 `handleTableChange`。
  - `fetchList` 内 `pagination.pageIndex/pageSize/total` → `pagination.value.*`。
  - `handleSearch`/`handleReset` 内 `pagination.pageIndex = 1` → `pagination.value.pageIndex = 1`。

- [ ] **Step 8: 验证** — `cd web && npx stylelint "src/views/contract/ESignManage.vue"` → 0;`rg -n "#[0-9a-fA-F]{6}" web/src/views/contract/ESignManage.vue` → 无输出。

- [ ] **Step 9: 提交**

```bash
git add web/src/views/contract/ESignManage.vue
git commit -m "refactor(contract): ESignManage 迁DataTable+填满首行+StatusTag"
```

### Task C3: ContractTypeManage.vue

**Files:**
- Modify: `web/src/views/contract/ContractTypeManage.vue`

- [ ] **Step 1: imports** — 加 `DataTable`、`StatusTag`;保留 `PlusOutlined`/`EditOutlined`(本页无搜索,不需要 `ReloadOutlined`)。删 `EmptyState` import(DataTable 的 empty-text 接管空态)。

- [ ] **Step 2: 容器 flush** — `<div class="page-container">` → `<div class="page-container page-container--flush">`

- [ ] **Step 3: #actions → #right** — 把 `<template #actions>` 整块替换为:

```vue
      <template #right>
        <a-button v-if="has(ContractPermissions.TypeManage)" type="primary" size="middle" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增类型
        </a-button>
      </template>
```

- [ ] **Step 4: a-alert 保留** — 不动。

- [ ] **Step 5: a-card+a-table → DataTable** — 替换为:

```vue
    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 900 }"
      row-key="id"
      empty-text="暂无合同类型数据"
      @change="fetchList"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'status'">
          <StatusTag :type="record.status === 1 ? 'success' : 'default'">{{ record.status === 1 ? '启用' : '停用' }}</StatusTag>
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button v-if="has(ContractPermissions.TypeManage)" type="link" size="small" @click="handleEdit(record)">
            <EditOutlined />编辑
          </a-button>
          <a-button v-if="has(ContractPermissions.TypeManage)" type="link" size="small" @click="handleToggleStatus(record)">
            {{ record.status === 1 ? '停用' : '启用' }}
          </a-button>
        </template>
      </template>
    </DataTable>
```

- [ ] **Step 6: 列删序号** — `tableColumns` 删首项序号列。

- [ ] **Step 7: pagination ref** — 同 C2 Step 7 的 pagination 部分(`reactive`→`ref`,删 `paginationConfig`/`handleTableChange`,`fetchList`/`handleSearch` 等内 `pagination.value.*`)。本页 `fetchList` 无 search 条件分页外只读 pageIndex/pageSize;`fetchList` 内两处 `pagination.pageIndex/pageSize` → `.value.`,`pagination.total` → `.value.total`。无 handleSearch(本页无搜索);保留 onMounted。

- [ ] **Step 8: 验证** — `cd web && npx stylelint "src/views/contract/ContractTypeManage.vue"` → 0;`rg -n "#[0-9a-fA-F]{6}" web/src/views/contract/ContractTypeManage.vue` → 无输出。

- [ ] **Step 9: 提交**

```bash
git add web/src/views/contract/ContractTypeManage.vue
git commit -m "refactor(contract): ContractTypeManage 迁DataTable+填满首行+StatusTag"
```

### Task C4: ContractTemplateManage.vue

**Files:**
- Modify: `web/src/views/contract/ContractTemplateManage.vue`

- [ ] **Step 1: imports** — 加 `DataTable`、`StatusTag`、图标 `ReloadOutlined`。删 `EmptyState` import 与 `#emptyText`。

- [ ] **Step 2: 容器 flush** — `<div class="page-container">` → `<div class="page-container page-container--flush">`

- [ ] **Step 3: #actions + #toolbar → #left/#right** — 把原 `#actions` 与 `#toolbar` 两块替换为:

```vue
      <template #left>
        <a-input v-model:value="searchForm.keyword" size="middle" placeholder="模板名称" style="width: 200px" allow-clear @keyup.enter="handleSearch" />
        <a-select v-model:value="searchForm.typeId" size="middle" placeholder="合同类型" style="width: 160px" allow-clear :options="typeOptions" />
        <a-select v-model:value="searchForm.status" size="middle" placeholder="状态" style="width: 120px" allow-clear :options="statusOptions" />
        <a-button type="primary" size="middle" @click="handleSearch">查询</a-button>
        <a-button size="middle" @click="handleReset">
          <template #icon><ReloadOutlined /></template>重置
        </a-button>
      </template>
      <template #right>
        <a-button v-if="has(ContractPermissions.TemplateManage)" type="primary" size="middle" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增模板
        </a-button>
      </template>
```

- [ ] **Step 4: a-card+a-table → DataTable** — 替换为:

```vue
    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 1000 }"
      row-key="id"
      empty-text="暂无模板数据"
      @change="fetchList"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'status'">
          <StatusTag :type="templateStatusTagType(record.status)">{{ templateStatusText(record.status) }}</StatusTag>
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button v-if="has(ContractPermissions.TemplateManage)" type="link" size="small" @click="handleEdit(record)">
            <EditOutlined />编辑
          </a-button>
          <a-popconfirm
            v-if="has(ContractPermissions.TemplateManage) && record.status !== 1"
            title="发布后旧版本将自动停用，确定发布吗？" ok-text="确定" cancel-text="取消" @confirm="handlePublish(record)"
          >
            <a-button type="link" size="small"><CheckOutlined />发布</a-button>
          </a-popconfirm>
          <a-button
            v-if="has(ContractPermissions.TemplateManage) && record.status === 1"
            type="link" size="small" danger @click="handleDisable(record)"
          >停用</a-button>
        </template>
      </template>
    </DataTable>
```

> 注:`version` 列用 `customRender`(`columns` 定义里),保留原样,无需 `#bodyCell` 分支。

- [ ] **Step 5: 列删序号** — `tableColumns` 删首项序号列(其余含 `version` 的 `customRender` 不动)。

- [ ] **Step 6: 状态映射 + pagination ref** —
  - 把 `templateStatusColor` 替换为:
    ```ts
    function templateStatusTagType(s: number): 'success' | 'default' {
      return (['default', 'success', 'default'] as const)[s] || 'default'
    }
    ```
  - `pagination` `reactive`→`ref`;删 `paginationConfig`/`handleTableChange`;`fetchList`/`handleSearch`/`handleReset` 内 `pagination.X` → `pagination.value.X`。

- [ ] **Step 7: 验证** — `cd web && npx stylelint "src/views/contract/ContractTemplateManage.vue"` → 0;`rg -n "#[0-9a-fA-F]{6}" web/src/views/contract/ContractTemplateManage.vue` → 无输出。

- [ ] **Step 8: 提交**

```bash
git add web/src/views/contract/ContractTemplateManage.vue
git commit -m "refactor(contract): ContractTemplateManage 迁DataTable+填满首行+StatusTag"
```

### Task C5: ContractStatusFlow.vue — 已洁净,免改

- [ ] **Step 1: 确认** — Run: `rg -n "#[0-9a-fA-F]{3,6}|\\$[a-z-]+" web/src/views/contract/components/ContractStatusFlow.vue`
  Expected: 无输出(该组件纯 a-steps 语义状态串 + 仅 padding 样式,**零视觉债**)。**本任务无代码改动**,仅记录确认。

---

## Phase D — 中央复验

### Task D1: 全量构建 + stylelint + 裸 hex 终扫

- [ ] **Step 1: 后端构建** — Run: `dotnet build src/STOTOP.Module.Contract/STOTOP.Module.Contract.csproj`
  Expected: 0 Error。

- [ ] **Step 2: 前端构建** — Run: `cd web && npm run build`(或仓库实际命令,见 package.json `scripts.build`)
  Expected: vite build 成功(vue-tsc 基线红不阻断,以 vite 产物为准;若 build 脚本含 `vue-tsc -b` 失败,改跑 `npx vite build` 兜底,见 [[frontend-typecheck-baseline-red]])。

- [ ] **Step 3: stylelint 全模块** — Run: `cd web && npx stylelint "src/views/contract/**/*.vue"`
  Expected: 0 problems。

- [ ] **Step 4: 裸 hex 终扫(抓 stylelint 盲区:inline/render/ECharts)** — Run: `rg -n "#[0-9a-fA-F]{6}\\b|#[0-9a-fA-F]{3}\\b" web/src/views/contract/`
  Expected: **仅** `ContractDashboard.vue` 内 ECharts 两处合法色板(状态柱 6 色 + 饼图 8 色);其余 5 文件 0 裸 hex。任何其他命中都要回到对应 Task 修掉。

- [ ] **Step 5: 旧蓝/旧 SCSS 变量终扫** — Run: `rg -n "#1677ff|#409eff|#67c23a|#e6a23c|#f56c6c|\\$text-primary|\\$border-color-lighter|\\$bg-page\\b" web/src/views/contract/`
  Expected: 无输出(旧蓝绿与旧颜色 SCSS 变量清零)。

- [ ] **Step 6: 用户 live 逐页验**(用户已登录 9001 会话 HMR;不自起第二 vite 实例)。逐页过:
  - `/contract/list`:状态 Tab 计数正确/点击快筛/全部回退;表格无边框观感;弹窗、详情抽屉(描述保留边框、内嵌表去边、续签链点着色、条款 danger tag)、审批/删除/续签流程 1:1。
  - `/contract/dashboard`(合同看板):KPI 条;ECharts 状态柱新语义色 + 饼图协调板;两表去边;预警/续签 StatusTag。
  - ESign / Type / Template:填满首行工具栏对齐 32px;表去边;StatusTag。

- [ ] **Step 7: 收尾提交(如 D 步有补修)** — 若 D 扫描发现遗漏,在对应文件修复后:
  ```bash
  git add web/src/views/contract/ src/STOTOP.Module.Contract/
  git commit -m "fix(contract): 中央复验补修(裸hex/旧变量清零)"
  ```

---

## 执行顺序与并行建议

1. **Phase A**(后端,独立)→ **B1**(前端 API)→ **B2**(ContractList 主页,依赖 B1)。
2. **C1 / C2 / C3 / C4** 互相独立,可并行(各自单文件,无共享状态);**C5** 仅确认。
3. **D1** 全绿后用户 live。

> 后端 statistics 与前端各文件无交叉,B2 依赖 B1 的类型/函数。子代理并行执行 C2/C3/C4 时各改各文件,无冲突。

## 自查覆盖(spec → plan)

- spec §3.1 后端 statistics → Task A1 ✅(DTO/接口/实现/controller/缺失状态兜底)
- spec §3.2 StatFilterTabs+填满首行 → B2 Step 3/7/8/9/11/12/13 ✅(`''` 全部键/计数刷新/守卫)
- spec §3.3 主列表迁 DataTable → B2 Step 4/5 ✅
- spec §3.4 内嵌表去边/descriptions 保留/timeline/条款 tag/旧变量 → B2 Step 14/15/16/17 ✅
- spec §4.1 Dashboard tokenize+ECharts → C1 ✅
- spec §4.2 三子页迁 DataTable+填满首行+StatusTag → C2/C3/C4 ✅
- spec §4.3 StatusFlow → C5(实测洁净,免改)✅
- spec §5 验证(build+stylelint+rg+live)→ D1 ✅
- spec §7 不做项(行为类)→ 计划未触碰 ✅
