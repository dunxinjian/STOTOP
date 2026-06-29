<template>
  <div class="operation-log-page">
    <PageHeader title="操作日志">
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-select v-model:value="queryForm.module" placeholder="全部模块" allowClear style="width:130px"
              :options="moduleOptions" />
            <a-select v-model:value="queryForm.operationType" placeholder="全部类型" allowClear style="width:130px"
              :options="typeOptions" />
            <a-range-picker
              v-model:value="dateRange"
              :placeholder="['开始日期', '结束日期']"
              valueFormat="YYYY-MM-DD"
              style="width:240px"
            />
            <a-input v-model:value="queryForm.keyword" placeholder="描述/操作人/编号" allowClear style="width:180px" @pressEnter="handleSearch" />
          </div>
          <div style="display:flex; align-items:center; gap:8px;">
            <a-button type="primary" @click="handleSearch"><SearchOutlined />查询</a-button>
            <a-button @click="handleReset"><ReloadOutlined />重置</a-button>
          </div>
        </div>
      </template>
    </PageHeader>

    <!-- 数据表格 -->
    <a-card class="table-card" :bordered="false">
      <a-table
        :columns="columns"
        :dataSource="tableData"
        rowKey="id"
        :loading="loading"
        :pagination="{
          current: queryForm.pageIndex,
          pageSize: queryForm.pageSize,
          total,
          showSizeChanger: true,
          pageSizeOptions: ['20', '50', '100'],
          showTotal: (t: number) => `共 ${t} 条`,
          onChange: onPageChange,
          onShowSizeChange: onSizeChange,
        }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'operationTime'">
            {{ formatDateTime(record.operationTime) }}
          </template>
          <template v-if="column.dataIndex === 'module'">
            <a-tag>{{ record.module }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'operationType'">
            <a-tag :color="getTypeTagColor(record.operationType)">{{ record.operationType }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'targetCode'">
            <span class="target-code">{{ record.targetCode || '-' }}</span>
          </template>
          <template v-if="column.dataIndex === 'ipAddress'">
            <span class="text-muted">{{ record.ipAddress || '-' }}</span>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, watch } from 'vue'
import { SearchOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { getOperationLogs } from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'

const accountSetStore = useAccountSetStore()

const loading = ref(false)
const tableData = ref<any[]>([])
const total = ref(0)
const dateRange = ref<[string, string] | undefined>(undefined)

const queryForm = reactive({
  pageIndex: 1,
  pageSize: 20,
  module: undefined as string | undefined,
  operationType: undefined as string | undefined,
  keyword: '',
  startDate: '',
  endDate: '',
})

const columns = [
  { title: '操作时间', dataIndex: 'operationTime', key: 'operationTime', width: 175, sorter: true },
  { title: '模块', dataIndex: 'module', key: 'module', width: 100, align: 'center' as const },
  { title: '操作类型', dataIndex: 'operationType', key: 'operationType', width: 110, align: 'center' as const },
  { title: '操作描述', dataIndex: 'description', key: 'description', ellipsis: true },
  { title: '目标编号', dataIndex: 'targetCode', key: 'targetCode', width: 130, align: 'center' as const },
  { title: '操作人', dataIndex: 'operatorName', key: 'operatorName', width: 120, align: 'center' as const },
  { title: 'IP地址', dataIndex: 'ipAddress', key: 'ipAddress', width: 140, align: 'center' as const },
]

const moduleOptions = [
  { label: '凭证', value: '凭证' },
  { label: '日记账', value: '日记账' },
  { label: '结账', value: '结账' },
]

const typeOptions = [
  { label: '新增', value: '新增' },
  { label: '审核', value: '审核' },
  { label: '删除', value: '删除' },
  { label: '结账', value: '结账' },
  { label: '反结账', value: '反结账' },
  { label: '调整', value: '调整' },
]

function getTypeTagColor(type: string): string {
  const map: Record<string, string> = {
    '新增': 'success',
    '审核': 'processing',
    '删除': 'error',
    '结账': 'warning',
    '反结账': 'warning',
    '调整': 'default',
  }
  return map[type] ?? 'default'
}

function formatDateTime(val: string) {
  if (!val) return '-'
  const d = new Date(val)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

async function fetchData() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: queryForm.pageIndex,
      pageSize: queryForm.pageSize,
      accountSetId: accountSetStore.currentAccountSetId || 0,
    }
    if (queryForm.module) params.module = queryForm.module
    if (queryForm.operationType) params.operationType = queryForm.operationType
    if (queryForm.keyword) params.keyword = queryForm.keyword
    if (dateRange.value && dateRange.value[0]) params.startDate = dateRange.value[0]
    if (dateRange.value && dateRange.value[1]) params.endDate = dateRange.value[1]

    const res = await getOperationLogs(params)
    tableData.value = res.items || []
    total.value = res.total || 0
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

function onPageChange(page: number, pageSize: number) {
  queryForm.pageIndex = page
  queryForm.pageSize = pageSize
  fetchData()
}

function onSizeChange(_current: number, size: number) {
  queryForm.pageIndex = 1
  queryForm.pageSize = size
  fetchData()
}

function handleSearch() {
  queryForm.pageIndex = 1
  fetchData()
}

function handleReset() {
  queryForm.module = undefined
  queryForm.operationType = undefined
  queryForm.keyword = ''
  dateRange.value = undefined
  queryForm.pageIndex = 1
  fetchData()
}

onMounted(fetchData)

// 监听账套变化，自动重新加载数据
watch(() => accountSetStore.currentAccountSetId, async (newId) => {
  if (newId) {
    tableData.value = []
    await fetchData()
  }
})
</script>

<style scoped>
.operation-log-page {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.target-code {
  font-family: monospace;
  color: var(--color-info);
  font-weight: 600;
}

.text-muted {
  color: #909399;
  font-size: 12px;
}
</style>
