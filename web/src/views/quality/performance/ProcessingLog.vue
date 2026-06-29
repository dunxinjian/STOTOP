<template>
  <div class="page-container processing-log">
    <PageHeader title="处理记录">
      <template #actions>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input v-if="activeTab === 'all'" v-model:value="searchForm.userId" size="small" placeholder="用户名/ID" allow-clear style="width: 160px" @pressEnter="handleSearch" />
          <a-input v-model:value="searchForm.period" size="small" placeholder="如: 2026-Q1" allow-clear style="width: 120px" @pressEnter="handleSearch" />
          <a-range-picker v-model:value="searchForm.dateRange" size="small" :placeholder="['开始日期', '结束日期']" value-format="YYYY-MM-DD" style="width: 240px" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <a-tabs v-model:activeKey="activeTab" @change="handleTabChange" style="margin-bottom: 16px">
      <a-tab-pane key="all" tab="全部记录" />
      <a-tab-pane key="my" tab="我的记录" />
    </a-tabs>

    <a-card :bordered="false">
      <a-spin :spinning="loading">
        <a-table
          :columns="columns"
          :data-source="tableData"
          :pagination="pagination"
          row-key="id"
          size="small"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'score'">
              <span :style="{ color: scoreColor(record.score), fontWeight: 600 }">
                {{ record.score }}
              </span>
            </template>
            <template v-if="column.dataIndex === 'createTime'">
              {{ record.createTime?.slice(0, 16) }}
            </template>
          </template>
        </a-table>
      </a-spin>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import { getPerformanceRecords, getMyPerformance } from '@/api/quality'

const activeTab = ref('all')
const loading = ref(false)
const tableData = ref<any[]>([])

const searchForm = reactive({
  userId: '',
  period: '',
  dateRange: undefined as [string, string] | undefined,
})

const pagination = reactive({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50'],
  showTotal: (total: number) => `共 ${total} 条`,
})

const columns = [
  { title: '用户名', dataIndex: 'userName', width: 120 },
  { title: '周期', dataIndex: 'period', width: 120 },
  { title: '异常处理数', dataIndex: 'exceptionCount', width: 110, align: 'center' as const },
  { title: '已解决数', dataIndex: 'resolvedCount', width: 100, align: 'center' as const },
  { title: '超时数', dataIndex: 'overdueCount', width: 90, align: 'center' as const },
  { title: '评分', dataIndex: 'score', width: 90, align: 'center' as const },
  { title: '备注', dataIndex: 'remark', ellipsis: true },
  { title: '记录时间', dataIndex: 'createTime', width: 160 },
]

function scoreColor(score: number): string {
  if (score >= 90) return 'var(--color-success)'
  if (score >= 70) return 'var(--color-info)'
  if (score >= 60) return 'var(--color-warning)'
  return 'var(--color-danger)'
}

function handleTabChange() {
  searchForm.userId = ''
  searchForm.period = ''
  searchForm.dateRange = undefined
  pagination.current = 1
  fetchData()
}

function handleSearch() {
  pagination.current = 1
  fetchData()
}

function handleReset() {
  searchForm.userId = ''
  searchForm.period = ''
  searchForm.dateRange = undefined
  pagination.current = 1
  fetchData()
}

function handleTableChange(pag: any) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  fetchData()
}

async function fetchData() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
    }
    if (searchForm.period) params.period = searchForm.period
    if (searchForm.userId && activeTab.value === 'all') params.userId = searchForm.userId
    if (searchForm.dateRange?.[0]) params.startDate = searchForm.dateRange[0]
    if (searchForm.dateRange?.[1]) params.endDate = searchForm.dateRange[1]

    let res: any
    if (activeTab.value === 'my') {
      res = await getMyPerformance(params)
    } else {
      res = await getPerformanceRecords(params)
    }

    if (res) {
      if (Array.isArray(res)) {
        tableData.value = res
        pagination.total = res.length
      } else {
        tableData.value = res.items ?? res.list ?? []
        pagination.total = res.totalCount ?? res.total ?? 0
      }
    } else {
      tableData.value = []
      pagination.total = 0
    }
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchData()
})
</script>

<style scoped lang="scss">
.processing-log {
  :deep(.ant-tabs-nav) {
    margin-bottom: 0;
  }
}
</style>
