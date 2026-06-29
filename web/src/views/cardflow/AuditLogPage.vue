<script setup lang="ts">
/**
 * AuditLogPage.vue — 卡片流操作日志审计页（管理员）
 *
 * 双行工具栏 + 日志列表 + CSV 导出
 */
import { ref, reactive, computed, h, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  ExportOutlined,
  SearchOutlined,
  ClearOutlined,
} from '@ant-design/icons-vue'
import dayjs, { type Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import { searchAuditLogs } from '@/api/cardflow'
import type { AuditLogItemDto } from '@/types/cardflow'

const router = useRouter()

// ========== 操作类型元数据 ==========
type StatusTagType = 'success' | 'warning' | 'danger' | 'info' | 'default'

interface ActionMeta {
  label: string
  statusType: StatusTagType // StatusTag type
}

const ACTION_META: Record<string, ActionMeta> = {
  submit:      { label: '提交', statusType: 'info' },
  approve:     { label: '通过', statusType: 'success' },
  reject:      { label: '退回', statusType: 'danger' },
  transfer:    { label: '转交', statusType: 'default' },
  countersign: { label: '加签', statusType: 'info' },
  void:        { label: '废除', statusType: 'default' },
  withdraw:    { label: '撤回', statusType: 'default' },
  urge:        { label: '催办', statusType: 'warning' },
  delegate:    { label: '委托', statusType: 'default' },
  resubmit:    { label: '重提', statusType: 'info' },
  resume:      { label: '恢复', statusType: 'success' },
  cc:          { label: '抄送', statusType: 'default' },
}

const ACTION_FILTER_OPTIONS = [
  { value: 'submit', label: '提交' },
  { value: 'approve', label: '通过' },
  { value: 'reject', label: '退回' },
  { value: 'transfer', label: '转交' },
  { value: 'countersign', label: '加签' },
  { value: 'void', label: '废除' },
  { value: 'withdraw', label: '撤回' },
  { value: 'urge', label: '催办' },
  { value: 'delegate', label: '委托' },
]

function actionMeta(type: string): ActionMeta {
  return ACTION_META[type] || { label: type || '-', statusType: 'default' }
}

// ========== 筛选表单 ==========
interface FilterForm {
  actionTypes: string[]
  operatorName: string
  cardNumber: string
}
const filters = reactive<FilterForm>({
  actionTypes: [],
  operatorName: '',
  cardNumber: '',
})

const dateRange = ref<[Dayjs, Dayjs]>([
  dayjs().subtract(7, 'day').startOf('day'),
  dayjs().endOf('day'),
])

// ========== 表格状态 ==========
const loading = ref(false)
const exporting = ref(false)
const dataSource = ref<AuditLogItemDto[]>([])
const pagination = reactive({ current: 1, pageSize: 50, total: 0 })

const tableColumns = computed<TableColumnsType<AuditLogItemDto>>(() => [
  {
    title: '时间', dataIndex: 'operationTime', key: 'operationTime', width: 168, fixed: 'left',
    customRender: ({ text }) => h('span', { class: 'mono' }, text ? dayjs(text).format('YYYY-MM-DD HH:mm:ss') : '-'),
  },
  {
    title: '操作类型', dataIndex: 'actionType', key: 'actionType', width: 100,
    customRender: ({ text }) => {
      const meta = actionMeta(text)
      return h(StatusTag, { type: meta.statusType, class: 'action-tag' }, () => meta.label)
    },
  },
  { title: '操作人', dataIndex: 'operatorName', key: 'operatorName', width: 120, ellipsis: true },
  {
    title: '卡片编号', dataIndex: 'cardNumber', key: 'cardNumber', width: 180,
    customRender: ({ text, record }) => {
      if (!text) return h('span', { class: 'text-muted' }, '-')
      return h('a', {
        class: 'mono link',
        onClick: () => router.push(`/cardflow/cards/${record.cardId}`),
      }, text)
    },
  },
  { title: '卡片标题', dataIndex: 'cardTitle', key: 'cardTitle', ellipsis: true, minWidth: 200 },
  { title: '节点', dataIndex: 'stageName', key: 'stageName', width: 140, ellipsis: true,
    customRender: ({ text }) => text || h('span', { class: 'text-muted' }, '-') },
  {
    title: '详情', dataIndex: 'opinion', key: 'opinion', minWidth: 220,
    customRender: ({ record }) => {
      const text = buildDetail(record)
      if (!text) return h('span', { class: 'text-muted' }, '-')
      return h('a-tooltip', { placement: 'topLeft', title: text }, () =>
        h('div', { class: 'opinion-ellipsis' }, text)
      )
    },
  },
])

function buildDetail(row: AuditLogItemDto): string {
  if (row.opinion && row.opinion.trim()) return row.opinion
  if (row.detailJson && row.detailJson.trim()) return row.detailJson
  return ''
}

// ========== 查询/重置 ==========
function buildQuery() {
  const [start, end] = dateRange.value || []
  return {
    page: pagination.current,
    pageSize: pagination.pageSize,
    actionTypes: filters.actionTypes.length > 0 ? filters.actionTypes.join(',') : null,
    operatorName: filters.operatorName?.trim() || null,
    cardNumber: filters.cardNumber?.trim() || null,
    startDate: start ? start.format('YYYY-MM-DD') : null,
    endDate: end ? end.format('YYYY-MM-DD') : null,
  }
}

async function fetchList() {
  loading.value = true
  try {
    const res = await searchAuditLogs(buildQuery())
    dataSource.value = res?.items || []
    pagination.total = res?.total || 0
  } catch {
    message.error('加载操作日志失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pagination.current = 1
  fetchList()
}

function handleReset() {
  filters.actionTypes = []
  filters.operatorName = ''
  filters.cardNumber = ''
  dateRange.value = [dayjs().subtract(7, 'day').startOf('day'), dayjs().endOf('day')]
  pagination.current = 1
  fetchList()
}

function handleTableChange(pg: any) {
  pagination.current = pg.current || 1
  pagination.pageSize = pg.pageSize || 50
  fetchList()
}

// ========== CSV 导出 ==========
async function handleExport() {
  if (exporting.value) return
  exporting.value = true
  try {
    // 一次性拉取当前筛选条件下的全部数据（最多 10000 条防爆）
    const all: AuditLogItemDto[] = []
    const pageSize = 500
    let page = 1
    let total = 0
    do {
      const q = { ...buildQuery(), page, pageSize }
      const res = await searchAuditLogs(q)
      const items = res?.items || []
      all.push(...items)
      total = res?.total || 0
      page++
      if (all.length >= total || all.length >= 10000) break
    } while (true)

    const headers = ['时间', '操作类型', '操作人', '卡片编号', '卡片标题', '流程', '节点', '详情']
    const rows = all.map(r => [
      r.operationTime ? dayjs(r.operationTime).format('YYYY-MM-DD HH:mm:ss') : '',
      actionMeta(r.actionType).label,
      r.operatorName || '',
      r.cardNumber || '',
      r.cardTitle || '',
      r.flowName || '',
      r.stageName || '',
      buildDetail(r),
    ])
    const csv = [headers, ...rows]
      .map(row => row.map(escapeCsv).join(','))
      .join('\r\n')

    // 添加 UTF-8 BOM 以兼容 Excel
    const blob = new Blob([new Uint8Array([0xEF, 0xBB, 0xBF]), csv], { type: 'text/csv;charset=utf-8;' })
    const filename = `操作日志_${dayjs().format('YYYYMMDD_HHmmss')}.csv`
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
    message.success(`已导出 ${all.length} 条记录`)
  } catch {
    message.error('导出失败')
  } finally {
    exporting.value = false
  }
}

function escapeCsv(val: any): string {
  if (val === null || val === undefined) return ''
  const s = String(val).replace(/\r?\n/g, ' ').replace(/\t/g, ' ')
  if (s.includes(',') || s.includes('"') || s.includes('\n')) {
    return `"${s.replace(/"/g, '""')}"`
  }
  return s
}

onMounted(() => {
  fetchList()
})
</script>

<template>
  <div class="page-container audit-page">
    <PageHeader>
      <!-- 第一行：操作按钮 -->
      <template #actions>
        <a-space :size="8">
          <a-button type="primary" :loading="exporting" @click="handleExport">
            <template #icon><ExportOutlined /></template>
            导出 CSV
          </a-button>
        </a-space>
      </template>

      <!-- 第二行：筛选条件 -->
      <template #toolbar>
        <div class="filter-bar">
          <a-select
            v-model:value="filters.actionTypes"
            mode="multiple"
            placeholder="操作类型"
            allow-clear
            :max-tag-count="2"
            :options="ACTION_FILTER_OPTIONS"
            style="width: 240px"
          />
          <a-input
            v-model:value="filters.operatorName"
            placeholder="操作人"
            allow-clear
            style="width: 140px"
            @press-enter="handleSearch"
          />
          <a-input
            v-model:value="filters.cardNumber"
            placeholder="卡片编号"
            allow-clear
            style="width: 160px"
            @press-enter="handleSearch"
          />
          <a-range-picker
            v-model:value="dateRange"
            :allow-clear="false"
            format="YYYY-MM-DD"
            style="width: 240px"
          />
          <a-button type="primary" @click="handleSearch">
            <template #icon><SearchOutlined /></template>
            查询
          </a-button>
          <a-button @click="handleReset">
            <template #icon><ClearOutlined /></template>
            重置
          </a-button>
        </div>
      </template>
    </PageHeader>

    <div class="audit-body">
      <a-table
        class="audit-table"
        :columns="tableColumns"
        :data-source="dataSource"
        :loading="loading"
        size="middle"
        row-key="id"
        :scroll="{ x: 1280 }"
        :pagination="{
          current: pagination.current,
          pageSize: pagination.pageSize,
          total: pagination.total,
          showSizeChanger: true,
          showQuickJumper: true,
          pageSizeOptions: ['20', '50', '100', '200'],
          showTotal: (t: number) => `共 ${t} 条`,
        }"
        @change="handleTableChange"
      />
    </div>
  </div>
</template>

<style scoped lang="scss">
.page-container.audit-page {
  padding: 0;
}

.filter-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  width: 100%;
}

.audit-body {
  padding: 16px;
}

.audit-table {
  :deep(.ant-table) {
    background: var(--bg-card);
    border-radius: 8px;
  }
  :deep(.ant-table-thead > tr > th) {
    background: var(--bg-muted);
    font-weight: 500;
    color: var(--text-2);
  }
}

.mono {
  font-family: 'SF Mono', Menlo, Consolas, 'Courier New', monospace;
  font-size: 13px;
  color: var(--text-2);
  font-variant-numeric: tabular-nums;
}

.link {
  color: var(--text-1);
  cursor: pointer;
  &:hover { color: var(--color-primary); text-decoration: underline; }
}

.action-tag {
  font-weight: 500;
  border: none;
  margin: 0;
}

.text-muted {
  color: var(--text-3);
}

.opinion-ellipsis {
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-2);
}
</style>
