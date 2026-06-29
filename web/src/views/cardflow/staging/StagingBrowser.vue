<template>
  <div class="staging-browser">
    <PageHeader title="暂存数据">
      <template #center>
        <a-segmented v-model:value="activeTab" :options="tabOptions" size="small" @change="handleTabChange" />
      </template>
      <template #right>
        <a-button size="small" @click="handleExport">导出 Excel</a-button>
      </template>
      <template #toolbar>
        <a-input-number v-model:value="filters.batchId" placeholder="批次ID" :min="1" :controls="false" size="small" style="width: 110px" />
        <a-select v-model:value="filters.status" placeholder="处理状态" allowClear size="small" style="width: 110px">
          <a-select-option :value="0">未处理</a-select-option>
          <a-select-option :value="1">已加工</a-select-option>
          <a-select-option :value="2">加工失败</a-select-option>
        </a-select>
        <a-range-picker v-model:value="filters.dateRange" valueFormat="YYYY-MM-DD" size="small" style="width: 240px" />
        <a-input v-model:value="filters.keyword" placeholder="关键字搜索" allowClear size="small" style="width: 160px" @pressEnter="handleSearch" />
        <a-button type="primary" size="small" @click="handleSearch"><SearchOutlined /> 搜索</a-button>
        <a-button size="small" @click="handleReset">重置</a-button>
      </template>
    </PageHeader>

    <!-- 统计摘要条 -->
    <div class="stats-bar">
      <a-spin :spinning="statsLoading" size="small">
        <div class="stats-bar__inner">
          <span class="stats-item">总记录 <strong>{{ stats.total ?? 0 }}</strong></span>
          <span class="stats-item stats-item--info">未处理 <strong>{{ stats.unprocessed ?? 0 }}</strong></span>
          <span class="stats-item stats-item--success">已加工 <strong>{{ stats.processed ?? 0 }}</strong></span>
          <span class="stats-item stats-item--danger">加工失败 <strong>{{ stats.failed ?? 0 }}</strong></span>
          <span class="stats-item">收入 <strong class="text-success">¥{{ formatAmount(stats.totalIncome ?? 0) }}</strong></span>
          <span class="stats-item">支出 <strong class="text-danger">¥{{ formatAmount(stats.totalExpense ?? 0) }}</strong></span>
        </div>
      </a-spin>
    </div>

    <!-- 表格区域 -->
    <a-card class="table-card" :bodyStyle="{ padding: 0 }">
      <!-- 批量操作 -->
      <div v-if="selectedRowKeys.length" class="batch-bar">
        <span>已选中 <strong>{{ selectedRowKeys.length }}</strong> 条记录</span>
        <a-button danger size="small" @click="handleBatchDelete">批量删除</a-button>
        <a-dropdown :trigger="['click']">
          <a-button size="small">批量修改状态 <DownOutlined /></a-button>
          <template #overlay>
            <a-menu @click="({ key }: any) => handleBatchStatus(Number(key))">
              <a-menu-item key="0">未处理</a-menu-item>
              <a-menu-item key="1">已加工</a-menu-item>
              <a-menu-item key="2">加工失败</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
        <a-button size="small" @click="handleBatchReprocess">批量重新加工</a-button>
      </div>

      <!-- 数据表格 -->
      <a-table
        :columns="tableColumns"
        :dataSource="tableData"
        :loading="tableLoading"
        :rowSelection="{ selectedRowKeys, onChange: onSelectChange }"
        :rowClassName="tableRowClassName"
        rowKey="FID"
        bordered
        :scroll="{ y: 'calc(100vh - 300px)' }"
        :pagination="false"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'F处理状态'">
            <a-tag v-if="record['F处理状态'] === 0" color="default">未处理</a-tag>
            <a-tag v-else-if="record['F处理状态'] === 1" color="success">已加工</a-tag>
            <a-tag v-else-if="record['F处理状态'] === 2" color="error">加工失败</a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="openEditDialog(record)">编辑</a-button>
          </template>
        </template>
        <template #expandedRowRender="{ record }">
          <div class="expand-detail">
            <a-descriptions :column="3" bordered size="small">
              <a-descriptions-item v-for="(val, key) in record" :key="key" :label="String(key)">
                <template v-if="key === 'F处理状态'">
                  <a-tag v-if="val === 0" color="default">未处理</a-tag>
                  <a-tag v-else-if="val === 1" color="success">已加工</a-tag>
                  <a-tag v-else-if="val === 2" color="error">加工失败</a-tag>
                </template>
                <span v-else-if="key === 'F错误信息' && val" class="text-danger">{{ val }}</span>
                <span v-else>{{ val ?? '-' }}</span>
              </a-descriptions-item>
            </a-descriptions>
            <div v-if="record['F关联凭证ID']" class="expand-detail__extra">
              关联凭证ID: <strong>{{ record['F关联凭证ID'] }}</strong>
            </div>
            <div v-if="record['F错误信息']" class="expand-detail__extra">
              错误信息: <span class="text-danger">{{ record['F错误信息'] }}</span>
            </div>
          </div>
        </template>
      </a-table>

      <!-- 分页 -->
      <div class="pagination-wrapper">
        <a-pagination
          v-model:current="pagination.page"
          v-model:pageSize="pagination.pageSize"
          :total="pagination.total"
          :pageSizeOptions="['50', '100']"
          showSizeChanger
          :showTotal="(total: number) => `共 ${total} 条`"
          @change="loadData"
          @showSizeChange="loadData"
        />
      </div>
    </a-card>

    <!-- 编辑弹窗 -->
    <a-modal v-model:open="editDialogVisible" title="编辑暂存记录" width="700px" centered :destroyOnClose="true">
      <a-form :model="editForm" :labelCol="{ style: { width: '130px' } }" layout="horizontal">
        <a-form-item v-for="field in editableFields" :key="field.key" :label="field.label">
          <a-input v-model:value="editForm[field.key]" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="editDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="editSaving" @click="handleSaveEdit">保存</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { SearchOutlined, DownOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getStagingData,
  getStagingStats,
  updateStagingRecord,
  batchDeleteStaging,
  batchUpdateStagingStatus,
  reprocessStaging,
} from '@/api/cardflow'

// ==================== 列配置（数据驱动） ====================
interface ColumnDef {
  key: string
  label: string
  width: number
  align?: string
}

const COLUMN_CONFIGS: Record<string, ColumnDef[]> = {
  jt: [
    { key: 'F流水号', label: '流水号', width: 150 },
    { key: 'F记账日期', label: '记账日期', width: 110 },
    { key: 'F业务日期', label: '业务日期', width: 110 },
    { key: 'F网点编号', label: '网点编号', width: 100 },
    { key: 'F网点名称', label: '网点名称', width: 120 },
    { key: 'F业务类型', label: '业务类型', width: 100 },
    { key: 'F费用名称', label: '费用名称', width: 120 },
    { key: 'F发生额收入', label: '收入', width: 100, align: 'right' },
    { key: 'F发生额支出', label: '支出', width: 100, align: 'right' },
    { key: 'F余额', label: '余额', width: 100, align: 'right' },
    { key: 'F处理状态', label: '状态', width: 90 },
    { key: 'F创建时间', label: '创建时间', width: 160 },
  ],
  st: [
    { key: 'F单号', label: '单号', width: 150 },
    { key: 'F日期', label: '日期', width: 110 },
    { key: 'F网点编号', label: '网点编号', width: 100 },
    { key: 'F网点名称', label: '网点名称', width: 120 },
    { key: 'F费用类型', label: '费用类型', width: 100 },
    { key: 'F费用名称', label: '费用名称', width: 120 },
    { key: 'F金额', label: '金额', width: 100, align: 'right' },
    { key: 'F处理状态', label: '状态', width: 90 },
    { key: 'F创建时间', label: '创建时间', width: 160 },
  ],
  yd: [
    { key: 'F运单编号', label: '运单编号', width: 150 },
    { key: 'F日期', label: '日期', width: 110 },
    { key: 'F始发网点', label: '始发网点', width: 120 },
    { key: 'F目的网点', label: '目的网点', width: 120 },
    { key: 'F费用类型', label: '费用类型', width: 100 },
    { key: 'F应收金额', label: '应收', width: 100, align: 'right' },
    { key: 'F应付金额', label: '应付', width: 100, align: 'right' },
    { key: 'F处理状态', label: '状态', width: 90 },
    { key: 'F创建时间', label: '创建时间', width: 160 },
  ],
  'STG申通派件日明细': [
    { key: 'F结算日期', label: '结算日期', width: 110 },
    { key: 'F网点编号', label: '网点编号', width: 100 },
    { key: 'F网点名称', label: '网点名称', width: 130 },
    { key: 'F承包区编号', label: '承包区编号', width: 110 },
    { key: 'F业务员编码', label: '业务员编码', width: 110 },
    { key: 'F业务员名称', label: '业务员名称', width: 110 },
    { key: 'F基础派费收费件量', label: '派件量', width: 100, align: 'right' },
    { key: 'F基础派费收费金额', label: '基础派费金额', width: 120, align: 'right' },
    { key: 'F处理状态', label: '状态', width: 90 },
    { key: 'F创建时间', label: '创建时间', width: 160 },
  ],
}

const META_FIELDS = new Set(['FID', 'F创建时间', 'F更新时间', 'F处理状态', 'F批次ID', 'F关联凭证ID', 'F错误信息', 'FOrgId', 'F账套ID', 'F归属网点编号', 'FDataScopeId', 'FSourceWorkItemId', 'FIsRevoked', 'F流水号', 'F原始行号', 'F其他列数据', 'F业务主键'])

const tabOptions = [
  { label: '极兔', value: 'jt' },
  { label: '申通', value: 'st' },
  { label: '韵达', value: 'yd' },
  { label: '派件', value: 'STG申通派件日明细' },
]

// ==================== 状态 ====================
const activeTab = ref('jt')
const statsLoading = ref(false)
const tableLoading = ref(false)
const editSaving = ref(false)
const editDialogVisible = ref(false)

const stats = reactive<Record<string, any>>({})
const tableData = ref<Record<string, any>[]>([])
const selectedRowKeys = ref<(string | number)[]>([])

const filters = reactive({
  batchId: undefined as number | undefined,
  status: undefined as number | undefined,
  dateRange: undefined as [string, string] | undefined,
  keyword: '',
})

const pagination = reactive({ page: 1, pageSize: 50, total: 0 })
const sortState = reactive({ prop: '', order: '' })

const editForm = reactive<Record<string, any>>({})
const editingRowId = ref<number | null>(null)

// ==================== 计算属性 ====================
const visibleColumns = computed(() => COLUMN_CONFIGS[activeTab.value] || COLUMN_CONFIGS.jt)

const columnLabelMap = computed(() => {
  const map = new Map<string, string>()
  visibleColumns.value.forEach(col => map.set(col.key, col.label))
  return map
})

const tableColumns = computed(() => {
  const cols = visibleColumns.value.map(col => ({
    title: col.label,
    dataIndex: col.key,
    key: col.key,
    width: col.width,
    align: (col.align || 'left') as 'left' | 'right' | 'center',
    sorter: true,
    ellipsis: true,
  }))
  cols.push({ title: '操作', dataIndex: 'action', key: 'action', width: 80, align: 'center' as const, sorter: false, ellipsis: false })
  return cols
})

const editableFields = computed(() => {
  const keys = Object.keys(editForm)
  if (!keys.length) return []
  return keys
    .filter(k => !META_FIELDS.has(k))
    .map(k => ({ key: k, label: columnLabelMap.value.get(k) || k }))
})

// ==================== 方法 ====================
function formatAmount(val: number) {
  return (val ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function tableRowClassName(record: Record<string, any>) {
  const status = record['F处理状态']
  if (status === 1) return 'row--success'
  if (status === 2) return 'row--danger'
  return ''
}

function onSelectChange(keys: (string | number)[]) {
  selectedRowKeys.value = keys
}

function buildParams() {
  const params: Record<string, any> = {
    page: pagination.page,
    pageSize: pagination.pageSize,
  }
  if (filters.batchId != null) params.batchId = filters.batchId
  if (filters.status != null) params.status = filters.status
  if (filters.dateRange?.length === 2) {
    params.startDate = filters.dateRange[0]
    params.endDate = filters.dateRange[1]
  }
  if (filters.keyword) params.keyword = filters.keyword
  if (sortState.prop) {
    params.sortField = sortState.prop
    params.sortOrder = sortState.order === 'ascend' ? 'asc' : 'desc'
  }
  return params
}

async function loadStats() {
  statsLoading.value = true
  try {
    const res: any = await getStagingStats(activeTab.value)
    Object.assign(stats, res.data ?? res)
  } catch (e: any) {
    message.error('加载统计数据失败')
  } finally {
    statsLoading.value = false
  }
}

async function loadData() {
  tableLoading.value = true
  try {
    const res: any = await getStagingData(activeTab.value, buildParams())
    const data = res.data ?? res
    tableData.value = data.items ?? data.rows ?? []
    pagination.total = data.total ?? 0
  } catch (e: any) {
    tableData.value = []
    pagination.total = 0
    message.error('加载数据失败')
  } finally {
    tableLoading.value = false
  }
}

function loadAll() {
  loadStats()
  loadData()
}

function handleTabChange() {
  pagination.page = 1
  selectedRowKeys.value = []
  loadAll()
}

function handleSearch() {
  pagination.page = 1
  loadData()
}

function handleReset() {
  filters.batchId = undefined
  filters.status = undefined
  filters.dateRange = undefined
  filters.keyword = ''
  pagination.page = 1
  loadData()
}

function handleTableChange(_pagination: any, _filters: any, sorter: any) {
  sortState.prop = sorter.field || ''
  sortState.order = sorter.order || ''
  loadData()
}

function handleExport() {
  message.info('功能开发中')
}

// ---- 批量操作 ----
async function handleBatchDelete() {
  const ids = selectedRowKeys.value
  Modal.confirm({
    title: '批量删除',
    content: `确认删除选中的 ${ids.length} 条记录？此操作不可恢复。`,
    okText: '确认删除',
    cancelText: '取消',
    okType: 'danger',
    async onOk() {
      try {
        await batchDeleteStaging(activeTab.value, ids as number[])
        message.success('删除成功')
        selectedRowKeys.value = []
        loadAll()
      } catch {
        message.error('批量删除失败')
      }
    },
  })
}

async function handleBatchStatus(newStatus: number) {
  const ids = selectedRowKeys.value
  const labels: Record<number, string> = { 0: '未处理', 1: '已加工', 2: '加工失败' }
  Modal.confirm({
    title: '批量修改状态',
    content: `确认将选中的 ${ids.length} 条记录状态修改为「${labels[newStatus]}」？`,
    okText: '确认',
    cancelText: '取消',
    async onOk() {
      try {
        await batchUpdateStagingStatus(activeTab.value, { ids: ids as number[], newStatus })
        message.success('状态修改成功')
        selectedRowKeys.value = []
        loadAll()
      } catch {
        message.error('批量修改状态失败')
      }
    },
  })
}

async function handleBatchReprocess() {
  const ids = selectedRowKeys.value
  Modal.confirm({
    title: '批量重新加工',
    content: `确认对选中的 ${ids.length} 条记录重新加工？`,
    okText: '确认',
    cancelText: '取消',
    async onOk() {
      try {
        await reprocessStaging(activeTab.value, { ids: ids as number[] })
        message.success('已提交重新加工')
        selectedRowKeys.value = []
        loadAll()
      } catch {
        message.error('批量重新加工失败')
      }
    },
  })
}

// ---- 编辑 ----
function openEditDialog(row: Record<string, any>) {
  editingRowId.value = row.FID
  Object.keys(editForm).forEach(k => delete editForm[k])
  Object.entries(row).forEach(([k, v]) => {
    if (!META_FIELDS.has(k)) editForm[k] = v
  })
  editDialogVisible.value = true
}

async function handleSaveEdit() {
  editSaving.value = true
  try {
    const id = editingRowId.value
    if (!id) {
      message.error('无法确定记录ID')
      return
    }
    await updateStagingRecord(activeTab.value, id, { ...editForm })
    message.success('保存成功')
    editDialogVisible.value = false
    loadData()
  } catch {
    message.error('保存失败')
  } finally {
    editSaving.value = false
  }
}

// ==================== 初始化 ====================
onMounted(() => {
  loadAll()
})
</script>

<style lang="scss" scoped>
@use '@/styles/variables' as *;

.staging-browser {
  padding: $page-padding;
}

// 统计摘要条
.stats-bar {
  margin-bottom: $spacing-sm;

  &__inner {
    display: flex;
    align-items: center;
    gap: $spacing-lg;
    height: 40px;
    padding: 0 $spacing-md;
    background: $bg-card;
    border: 1px solid $border-color-lighter;
    border-radius: $border-radius-md;
  }
}

.stats-item {
  font-size: $font-size-sm;
  color: $text-secondary;
  white-space: nowrap;

  strong {
    font-size: $font-size-base;
    color: $text-primary;
    margin-left: $spacing-xs;
  }

  &--info strong { color: $color-info; }
  &--success strong { color: $color-success; }
  &--danger strong { color: $color-danger; }
}

.text-success { color: $color-success; }
.text-danger { color: $color-danger; }

// 表格卡片
.table-card {
  border-radius: $border-radius-md;
}

// 批量操作条
.batch-bar {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  padding: $toolbar-padding;
  background: $color-primary-light;
  border-bottom: 1px solid $border-color-lighter;
  font-size: $font-size-sm;
}

// 分页
.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
  padding: $spacing-md;
}

// 展开行
.expand-detail {
  padding: $spacing-md $spacing-lg;

  &__extra {
    margin-top: $spacing-sm;
    font-size: $font-size-sm;
    color: $text-regular;
  }
}

// 行状态着色
:deep(.row--success) {
  background-color: rgba($color-success, 0.06);
}

:deep(.row--danger) {
  background-color: rgba($color-danger, 0.06);
}
</style>
