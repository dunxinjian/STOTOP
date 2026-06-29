<template>
  <div class="page-container">
    <PageHeader title="应交税费表">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-select v-model:value="filterForm.periodId" placeholder="选择期间" style="width: 180px"
          :options="periodList.map(p => ({ label: p.label, value: p.id }))"
          @change="loadData" />
        <a-button @click="exportToExcel"><DownloadOutlined />导出Excel</a-button>
      </template>
    </PageHeader>

    <!-- 数据表区域 -->
    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :dataSource="tableData"
        :loading="loading"
        :pagination="false"
        rowKey="taxName"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'openingBalance'">
            {{ formatAmount(record.openingBalance) }}
          </template>
          <template v-if="column.dataIndex === 'periodIncrease'">
            {{ formatAmount(record.periodIncrease) }}
          </template>
          <template v-if="column.dataIndex === 'periodDecrease'">
            {{ formatAmount(record.periodDecrease) }}
          </template>
          <template v-if="column.dataIndex === 'closingBalance'">
            {{ formatAmount(record.closingBalance) }}
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { DownloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import { getTaxPayable, getPeriods } from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'

const accountSetStore = useAccountSetStore()

// 筛选表单
const filterForm = ref({
  periodId: undefined as number | undefined
})

const loading = ref(false)
const periodList = ref<{ id: number; label: string; year: number; month: number }[]>([])

const columns = [
  { title: '税种', dataIndex: 'taxName', key: 'taxName' },
  { title: '期初余额', dataIndex: 'openingBalance', key: 'openingBalance', width: 140, align: 'right' as const },
  { title: '本期增加', dataIndex: 'periodIncrease', key: 'periodIncrease', width: 140, align: 'right' as const },
  { title: '本期减少', dataIndex: 'periodDecrease', key: 'periodDecrease', width: 140, align: 'right' as const },
  { title: '期末余额', dataIndex: 'closingBalance', key: 'closingBalance', width: 140, align: 'right' as const },
]

// 表格数据
const tableData = ref<any[]>([])

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
    const res = await getTaxPayable({
      periodId: filterForm.value.periodId,
      accountSetId
    }) as any[]
    if (res) {
      tableData.value = res
    }
  } catch (error) {
    console.error('加载应交税费数据失败', error)
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
  if (!tableData.value || !tableData.value.length) { message.warning('暂无数据可导出'); return }

  const selectedPeriod = periodList.value.find(p => p.id === filterForm.value.periodId)
  const periodStr = selectedPeriod ? selectedPeriod.label : new Date().toISOString().slice(0, 7)

  const headers = ['税种', '期初余额', '本期增加', '本期减少', '期末余额']
  const rows = (tableData.value as any[]).map((row: any) => [
    row.taxName ?? '',
    row.openingBalance ?? '',
    row.periodIncrease ?? '',
    row.periodDecrease ?? '',
    row.closingBalance ?? ''
  ])

  const csv = [headers.join(','), ...rows.map((r: any[]) => r.map((v: any) => `"${String(v ?? '').replace(/"/g, '""')}"`).join(','))].join('\n')
  const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `应交税费表_${periodStr}.csv`
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

</style>
