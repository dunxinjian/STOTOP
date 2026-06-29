# StatFilterTabs + 车辆台账（阶段 0b）实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: 用 `superpowers:subagent-driven-development` 按任务逐个实现。步骤用 checkbox（`- [ ]`）跟踪。

**Goal:** 新建带计数的状态快筛条 `StatFilterTabs`，并把 `vehicle/VehicleManage` 作为验证页迁移到「列表模板 + 状态快筛」——顶部 4 张统计卡 + 车辆状态下拉合并为一条点击即筛的状态 Tab。

**Architecture:** 站在既有 `PATTERNS.md`/`TOKENS.md` 与阶段 0a 的 `DataTable`/`StatusTag`/`.page-toolbar` 之上。`StatFilterTabs` 把「KPI 计数 + 状态过滤」合一（`v-model:active` + `@change`，全令牌、无裸 hex），收编 CustomerManage/ServiceOrderManage 的手写快筛与 VehicleManage 的统计卡。承接 spec `docs/superpowers/specs/2026-06-17-功能页逐模块巡检与收敛-design.md`（方向① 克制收敛）。**用户已确认**统计卡→快筛 Tab 的 UX 变更。

**Tech Stack:** Vue 3.5 `<script setup>` + TS + Ant Design Vue 4.2 + SCSS 令牌。组件经 `unplugin-vue-components` 自动注册（也兼容显式 import）。

---

## ⚠️ 验证方式（无前端测试运行器，同 0a）

不写单元测试。每任务验证 = `npm run build`（vite build，不跑 type-check）绿 + `npx stylelint "<file>"` 0 problems + 必要时 preview。不跳过任何 hook。

## 文件结构

| 文件 | 责任 | 动作 |
|---|---|---|
| `web/src/components/StatFilterTabs.vue` | 带计数的状态快筛条（KPI+过滤合一） | 新建 |
| `web/src/views/vehicle/VehicleManage.vue` | 验证页：统计卡+状态下拉→StatFilterTabs，并迁到 DataTable+StatusTag+.page-toolbar | 改 |
| `web/docs/PATTERNS.md` | §二 增 StatFilterTabs 契约；§一.4 仪表盘/§一.1 列表注记 | 改 |

---

## Task 1：新建 `StatFilterTabs.vue`

**Files:** Create: `web/src/components/StatFilterTabs.vue`

- [ ] **Step 1：创建文件（逐字）**

```vue
<!--
  StatFilterTabs —— 带计数的状态快筛 Tab 条
  统一替代各页手写的「KPI 统计卡 + 状态下拉筛选」（VehicleManage 顶部统计卡 / CustomerManage .status-tab-bar / ServiceOrderManage .tab-item）。
  KPI 计数 + 点击过滤合一：v-model:active 绑当前选中 key（'' 通常表示「全部」），点击切换并 emit change(key)。
  全令牌、无裸 hex；可选 color（传 var(--token) 语义色）渲染状态圆点。
-->
<template>
  <div class="stat-filter-tabs">
    <button
      v-for="tab in tabs"
      :key="tab.key"
      type="button"
      class="stat-filter-tab"
      :class="{ 'stat-filter-tab--active': tab.key === active }"
      @click="select(tab.key)"
    >
      <span v-if="tab.color" class="stat-filter-tab__dot" :style="{ background: tab.color }" />
      <span class="stat-filter-tab__label">{{ tab.label }}</span>
      <span class="stat-filter-tab__count">{{ tab.count ?? 0 }}</span>
    </button>
  </div>
</template>

<script setup lang="ts">
interface TabItem {
  /** 选中值；'' 常表示「全部」 */
  key: string | number
  /** 文案 */
  label: string
  /** 计数 */
  count?: number
  /** 可选状态圆点色，传 var(--token) */
  color?: string
}

defineProps<{
  tabs: TabItem[]
  active: string | number
}>()

const emit = defineEmits<{
  (e: 'update:active', key: string | number): void
  (e: 'change', key: string | number): void
}>()

function select(key: string | number) {
  emit('update:active', key)
  emit('change', key)
}
</script>

<style scoped lang="scss">
.stat-filter-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-sm8);
  margin-bottom: var(--space-md12);
}

.stat-filter-tab {
  display: inline-flex;
  align-items: center;
  gap: var(--space-xs4);
  padding: var(--space-xs4) var(--space-md12);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  background: var(--bg-card);
  color: var(--text-2);
  font-size: var(--font-sm2);
  line-height: 22px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.stat-filter-tab:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.stat-filter-tab--active {
  border-color: var(--color-primary);
  color: var(--color-primary);
  background: var(--color-primary-light);
}

.stat-filter-tab__dot {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
}

.stat-filter-tab__count {
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}
</style>
```

- [ ] **Step 2：构建** `cd E:/STOTOP_Fable/web && npm run build` → 成功。
- [ ] **Step 3：stylelint** `npx stylelint "src/components/StatFilterTabs.vue"` → 0 problems。
- [ ] **Step 4：提交**
```bash
git add web/src/components/StatFilterTabs.vue
git commit -m "feat(ui): 新增 StatFilterTabs 状态快筛条（KPI+过滤合一，全令牌）"
```
（提交体尾行 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`）

---

## Task 2：迁移 `VehicleManage`（统计卡+状态下拉→StatFilterTabs，并套列表模板）

**Files:** Modify: `web/src/views/vehicle/VehicleManage.vue`。先 Read 确认锚点，按下列定向编辑。

- [ ] **Step 1：#actions 只留新增；筛选移入 #toolbar；删状态下拉与 divider**

把模板 `<template #actions>`（第 4–13 行）整块替换为：

```vue
      <template #right>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增车辆
        </a-button>
      </template>
      <template #toolbar>
        <div class="page-toolbar">
          <div class="page-toolbar__group">
            <a-input-search v-model:value="searchForm.keyword" placeholder="编码/车牌号" style="width: 200px" allow-clear @search="handleSearch" />
            <a-select v-model:value="searchForm.ownershipType" placeholder="权属类型" allow-clear style="width: 120px" :options="ownershipOptions" @change="handleSearch" />
          </div>
          <div class="page-toolbar__filters">
            <a-button @click="handleReset">
              <template #icon><ReloadOutlined /></template>重置
            </a-button>
          </div>
        </div>
      </template>
```

- [ ] **Step 2：统计卡 a-row → StatFilterTabs**

把模板第 16–38 行（`<!-- 统计卡片 -->` 注释 + 整个 `<a-row>...</a-row>`）替换为：

```vue
    <!-- 状态快筛（KPI 计数 + 点击过滤合一，替代顶部统计卡 + 车辆状态下拉） -->
    <StatFilterTabs v-model:active="searchForm.vehicleStatus" :tabs="statusTabs" @change="handleSearch" />
```

- [ ] **Step 3：a-card+a-table → DataTable**

把模板第 40–91 行（`<a-card>...</a-card>`）替换为：

```vue
    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 1200 }"
      row-key="id"
      empty-text="暂无车辆数据"
      @change="fetchVehicleList"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'code'">
          <a-tooltip :title="record.code">{{ record.code }}</a-tooltip>
        </template>
        <template v-if="column.dataIndex === 'ownershipType'">
          <StatusTag :type="record.ownershipType === 1 ? 'info' : 'default'">
            {{ record.ownershipType === 1 ? '公司' : '个人' }}
          </StatusTag>
        </template>
        <template v-if="column.dataIndex === 'vehicleStatus'">
          <StatusTag :type="getVehicleStatusType(record.vehicleStatus)" dot>
            {{ getVehicleStatusText(record.vehicleStatus) }}
          </StatusTag>
        </template>
        <template v-if="column.dataIndex === 'createdTime'">
          {{ formatDate(record.createdTime) }}
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button type="link" size="small" @click="handleEdit(record)">
            <EditOutlined />编辑
          </a-button>
          <a-popconfirm
            title="确定删除该车辆吗？"
            ok-text="确定"
            cancel-text="取消"
            @confirm="handleDelete(record)"
          >
            <a-button type="link" size="small" danger>
              <DeleteOutlined />删除
            </a-button>
          </a-popconfirm>
        </template>
      </template>
    </DataTable>
```

- [ ] **Step 4：脚本 import 调整**

替换 `import { ref, reactive, computed, onMounted } from 'vue'` → `import { ref, reactive, computed, onMounted } from 'vue'`（**保留 computed**——statusTabs 用它）。

替换 `import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons-vue'` → `import { PlusOutlined, EditOutlined, DeleteOutlined, ReloadOutlined } from '@ant-design/icons-vue'`。

替换 `import EmptyState from '@/components/EmptyState.vue'` → 删除该行，新增：
```ts
import DataTable from '@/components/DataTable.vue'
import StatusTag from '@/components/StatusTag.vue'
import StatFilterTabs from '@/components/StatFilterTabs.vue'
```
（`PageHeader` import 保留。）

- [ ] **Step 5：删 tableColumns 的序号列**

删除 `tableColumns` 首行：
```ts
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
```

- [ ] **Step 6：pagination reactive→ref + 删 paginationConfig/handleTableChange**

替换：
```ts
const pagination = reactive({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})
```
为：
```ts
const pagination = ref({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})
```

删除整段 `paginationConfig`（第 305–312 行）与 `handleTableChange`（第 352–356 行）。

- [ ] **Step 7：新增 statusTabs computed + 状态 type 映射；删旧 color 函数**

在 `getStatusCount` 之后新增：
```ts
const statusTabs = computed(() => [
  { key: '', label: '全部', count: statistics.value.totalCount },
  { key: 1, label: '闲置', count: getStatusCount(1), color: 'var(--text-3)' },
  { key: 2, label: '使用中', count: getStatusCount(2), color: 'var(--color-success)' },
  { key: 3, label: '维修中', count: getStatusCount(3), color: 'var(--color-warning)' },
  { key: 4, label: '报废', count: getStatusCount(4), color: 'var(--color-danger)' },
])

function getVehicleStatusType(status: number): 'success' | 'warning' | 'danger' | 'default' {
  const map: Record<number, 'success' | 'warning' | 'danger' | 'default'> = {
    1: 'default',
    2: 'success',
    3: 'warning',
    4: 'danger',
  }
  return map[status] || 'default'
}
```

删除旧的 `getVehicleStatusColor` 函数（第 326–334 行，整段；其唯一用途已被 StatusTag :type 取代）。`getVehicleStatusText` 保留（StatusTag 文案用）。

- [ ] **Step 8：fetch/search/reset 的 pagination 改 `.value`**

`fetchVehicleList` 内：`pageIndex: pagination.value.pageIndex, pageSize: pagination.value.pageSize`；`pagination.value.total = res.totalCount || 0`。
`handleSearch`：`pagination.value.pageIndex = 1`。
`handleReset`：保留清 keyword/ownershipType/vehicleStatus（`searchForm.vehicleStatus = ''` 使快筛回到「全部」），`pagination.value.pageIndex = 1`。

- [ ] **Step 9：构建 + stylelint**

`cd E:/STOTOP_Fable/web && npm run build` → 成功；`npx stylelint "src/views/vehicle/VehicleManage.vue"` → 0 problems。grep 确认无残留：`paginationConfig`/`handleTableChange`/`dataIndex === 'index'`/`getVehicleStatusColor`/裸 `#666`/`a-tag`/`pagination.pageIndex`（不带 `.value`）应全无。

- [ ] **Step 10：提交**
```bash
git add web/src/views/vehicle/VehicleManage.vue
git commit -m "refactor(ui): 车辆台账迁移——统计卡+状态下拉→StatFilterTabs + DataTable+StatusTag+.page-toolbar"
```
（尾行 Co-Authored-By 同上）

---

## Task 3：PATTERNS.md 增 StatFilterTabs 契约

**Files:** Modify: `web/docs/PATTERNS.md`

- [ ] **Step 1：§二 增 StatFilterTabs 段**（在 §二 DataTable 段之后插入）

```
### StatFilterTabs

带计数的状态快筛条（样板 `vehicle/VehicleManage`），把「KPI 统计卡 + 状态下拉」合一为一行可点击过滤的 Tab。

| prop | 说明 |
| --- | --- |
| `tabs` | `[{key,label,count?,color?}]`；`key=''` 常表示「全部」；`color` 传 `var(--token)` 渲染状态圆点 |
| `active`（v-model） | 当前选中 key；点击 emit `update:active` + `change(key)` |

- 用法：`<StatFilterTabs v-model:active="searchForm.status" :tabs="statusTabs" @change="handleSearch" />`，把状态过滤与计数合一，替代顶部 a-statistic 卡片与独立状态下拉。
- 全令牌：选中态走 `--color-primary`/`--color-primary-light`，计数 `tabular-nums`；禁裸 hex。
```

- [ ] **Step 2：§一.1 列表页注记追加一句**（在迁移配方后）

> 列表页若需「状态计数 + 快筛」，用 `<StatFilterTabs>`（§二）替代顶部统计卡 + 状态下拉，放在工具栏与 DataTable 之间。

- [ ] **Step 3：提交**
```bash
git add web/docs/PATTERNS.md
git commit -m "docs(ux): PATTERNS 增 StatFilterTabs 契约与列表用法"
```
（尾行 Co-Authored-By 同上）

---

## 完成标准（本批次）

- `npm run build` 绿；VehicleManage 不再含统计卡/状态下拉/paginationConfig/handleTableChange/序号列/裸#666/a-tag 字面色。
- `StatFilterTabs` 成为「计数+状态过滤」唯一封装；PATTERNS 契约就绪。
- preview（用户已登录、有数据的组织）可见：状态 Tab 带计数、点击过滤、StatusTag 状态色、分页「共 N 条」。
