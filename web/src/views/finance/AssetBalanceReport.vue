<template>
  <div class="page-container">
    <PageHeader title="资产余额表">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-select v-model:value="filterForm.periodId" placeholder="选择期间" allowClear style="width: 180px"
          :options="periodList.map(p => ({ label: p.label, value: p.id }))"
          @change="loadData" />
        <a-input
          v-model:value="searchKeyword"
          placeholder="搜索资产名称/编码"
          allowClear
          style="width: 200px"
        >
          <template #prefix>
            <SearchOutlined />
          </template>
        </a-input>
        <a-button @click="exportToExcel"><DownloadOutlined />导出Excel</a-button>
      </template>
    </PageHeader>

    <!-- 数据表区域 -->
    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :dataSource="filteredTableData"
        :loading="loading"
        :pagination="false"
        rowKey="assetCode"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'originalValue'">
            {{ formatAmount(record.originalValue) }}
          </template>
          <template v-if="column.dataIndex === 'accumulatedDepreciation'">
            {{ formatAmount(record.accumulatedDepreciation) }}
          </template>
          <template v-if="column.dataIndex === 'netValue'">
            {{ formatAmount(record.netValue) }}
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { DownloadOutlined, SearchOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import { getAssetBalance, getPeriods } from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'

const accountSetStore = useAccountSetStore()

// 筛选表单
const filterForm = ref({
  periodId: undefined as number | undefined
})

const loading = ref(false)
const searchKeyword = ref('')
const periodList = ref<{ id: number; label: string; year: number; month: number }[]>([])

// 表格数据
const tableData = ref<any[]>([])

const columns = [
  { title: '资产编码', dataIndex: 'assetCode', key: 'assetCode', width: 120 },
  { title: '资产名称', dataIndex: 'assetName', key: 'assetName' },
  { title: '资产类别', dataIndex: 'categoryName', key: 'categoryName', width: 120 },
  { title: '所属部门', dataIndex: 'departmentName', key: 'departmentName', width: 140 },
  { title: '原值', dataIndex: 'originalValue', key: 'originalValue', width: 140, align: 'right' as const },
  { title: '累计折旧', dataIndex: 'accumulatedDepreciation', key: 'accumulatedDepreciation', width: 140, align: 'right' as const },
  { title: '净值', dataIndex: 'netValue', key: 'netValue', width: 140, align: 'right' as const },
]

// 过滤后的数据
const filteredTableData = computed(() => {
  if (!searchKeyword.value) return tableData.value
  const keyword = searchKeyword.value.toLowerCase()
  return tableData.value.filter(item =>
    item.assetName?.toLowerCase().includes(keyword) ||
    item.assetCode?.toLowerCase().includes(keyword)
  )
})

// 金额格式化
function formatAmount(val: number | undefined) {
  if (val === undefined || val === null) return '0.00'
  return val.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 加载数据
async function loadData() {
  if (!filterForm.value.periodId) return
  loading.value = true
  try {
    const accountSetId = accountSetStore.currentAccountSetId || undefined
    const res = await getAssetBalance({
      periodId: filterForm.value.periodId,
      accountSetId
    }) as any[]
    if (res) {
      tableData.value = res
    }
  } catch (error) {
    console.error('加载资产余额数据失败', error)
  } finally {
    loading.value = false
  }
}

// 加载期间列表
async function loadPeriods() {
  try {
    const accountSetId = accountSetStore.currentAccountSetId || 0
    const res = await getPeriods(accountSetId) as any[]
    periodList.value = (res || [])
      .sort((a: any, b: any) => {
        if (a.fYear !== b.fYear) return b.fYear - a.fYear
        return b.fPeriodNo - a.fPeriodNo
      })
      .map((p: any) => ({
        id: p.id || p.fid,
        label: `${p.fYear || p.year}年第${String(p.fPeriodNo || p.periodNo).padStart(2, '0')}期`,
        year: p.fYear || p.year,
        month: p.fPeriodNo || p.periodNo
      }))
    if (periodList.value.length > 0) {
      const now = new Date()
      const currentYear = now.getFullYear()
      const currentMonth = now.getMonth() + 1
      let defaultPeriod = periodList.value.find(
        (p: any) => p.year === currentYear && p.month === currentMonth
      )
      if (!defaultPeriod) defaultPeriod = periodList.value[0]
      filterForm.value.periodId = defaultPeriod.id
    }
  } catch (e) {
    console.error('加载期间失败', e)
  }
}

onMounted(async () => {
  await loadPeriods()
  loadData()
})

watch(() => accountSetStore.currentAccountSetId, async () => {
  filterForm.value.periodId = undefined
  await loadPeriods()
  loadData()
})

// 导出Excel(CSV)
function exportToExcel() {
  const data = filteredTableData.value
  if (!data || !data.length) { message.warning('暂无数据可导出'); return }

  const selectedPeriod = periodList.value.find(p => p.id === filterForm.value.periodId)
  const periodStr = selectedPeriod ? selectedPeriod.label : new Date().toISOString().slice(0, 7)

  const headers = ['资产编码', '资产名称', '资产类别', '所属部门', '原值', '累计折旧', '净值']
  const rows = data.map((row: any) => [
    row.assetCode ?? '',
    row.assetName ?? '',
    row.categoryName ?? '',
    row.departmentName ?? '',
    row.originalValue ?? '',
    row.accumulatedDepreciation ?? '',
    row.netValue ?? ''
  ])

  const csv = [headers.join(','), ...rows.map((r: any[]) => r.map((v: any) => `"${String(v ?? '').replace(/"/g, '""')}"`).join(','))].join('\n')
  const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `资产余额表_${periodStr}.csv`
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;
</style>
