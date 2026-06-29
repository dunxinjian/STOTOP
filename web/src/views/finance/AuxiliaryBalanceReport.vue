<template>
  <div class="page-container">
    <PageHeader title="辅助余额表">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-button @click="exportToExcel"><DownloadOutlined />导出Excel</a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-select v-model:value="filterForm.periodId" placeholder="选择期间" style="width: 180px"
              :options="periodList.map(p => ({ label: p.label, value: p.id }))"
              @change="loadData" />
            <a-select
              v-model:value="filterForm.type"
              placeholder="辅助核算类型"
              allowClear
              style="width: 160px"
              :options="auxiliaryTypes.map(t => ({ label: auxTypeLabel(t.name), value: t.name }))"
              @change="loadData"
            />
          </div>
          <div></div>
        </div>
      </template>
    </PageHeader>

    <!-- 数据表区域 -->
    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :dataSource="tableData"
        :loading="loading"
        :pagination="false"
        rowKey="accountCode"
        bordered
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'openingDebit'">
            {{ formatAmount(record.openingDebit) }}
          </template>
          <template v-if="column.dataIndex === 'openingCredit'">
            {{ formatAmount(record.openingCredit) }}
          </template>
          <template v-if="column.dataIndex === 'periodDebit'">
            {{ formatAmount(record.periodDebit) }}
          </template>
          <template v-if="column.dataIndex === 'periodCredit'">
            {{ formatAmount(record.periodCredit) }}
          </template>
          <template v-if="column.dataIndex === 'closingDebit'">
            {{ formatAmount(record.closingDebit) }}
          </template>
          <template v-if="column.dataIndex === 'closingCredit'">
            {{ formatAmount(record.closingCredit) }}
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
import { getAuxiliaryBalance, getPeriods, getAuxiliaryTypes } from '@/api/finance'
import { auxTypeLabel } from '@/constants/auxTypes'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import { useAccountSetStore } from '@/stores/accountSet'

const accountSetStore = useAccountSetStore()

// 筛选表单
const filterForm = ref({
  periodId: undefined as number | undefined,
  type: undefined as string | undefined
})

const periodList = ref<{ id: number; label: string; year: number; month: number }[]>([])
// 注：后端 AuxiliaryTypeDto 的 name 存的是英文码（outlet…），展示时经 auxTypeLabel 翻译
const auxiliaryTypes = ref<{ id: number; name: string }[]>([])

// 表格数据
const tableData = ref<any[]>([])
const loading = ref(false)

const columns = [
  { title: '科目编码', dataIndex: 'accountCode', key: 'accountCode', width: 120 },
  { title: '科目名称', dataIndex: 'accountName', key: 'accountName', width: 180 },
  { title: '辅助信息', dataIndex: 'auxiliaryInfo', key: 'auxiliaryInfo', width: 200 },
  {
    title: '期初余额',
    children: [
      { title: '借方', dataIndex: 'openingDebit', key: 'openingDebit', width: 130, align: 'right' as const },
      { title: '贷方', dataIndex: 'openingCredit', key: 'openingCredit', width: 130, align: 'right' as const },
    ],
  },
  {
    title: '本期发生',
    children: [
      { title: '借方', dataIndex: 'periodDebit', key: 'periodDebit', width: 130, align: 'right' as const },
      { title: '贷方', dataIndex: 'periodCredit', key: 'periodCredit', width: 130, align: 'right' as const },
    ],
  },
  {
    title: '期末余额',
    children: [
      { title: '借方', dataIndex: 'closingDebit', key: 'closingDebit', width: 130, align: 'right' as const },
      { title: '贷方', dataIndex: 'closingCredit', key: 'closingCredit', width: 130, align: 'right' as const },
    ],
  },
]

// 金额格式化
function formatAmount(val: number | undefined) {
  if (val === undefined || val === null || val === 0) return '-'
  return val.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 加载数据
async function loadData() {
  if (!filterForm.value.periodId) return
  loading.value = true
  try {
    const accountSetId = accountSetStore.currentAccountSetId || undefined
    const res = await getAuxiliaryBalance({
      periodId: filterForm.value.periodId,
      type: filterForm.value.type,
      accountSetId
    }) as any[]
    if (res) {
      tableData.value = res
    }
  } catch (error) {
    console.error('加载辅助余额数据失败', error)
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

// 加载辅助核算类型
async function loadAuxiliaryTypes() {
  try {
    const res = await getAuxiliaryTypes() as any[]
    if (res) {
      auxiliaryTypes.value = res
    }
  } catch (error) {
    console.error('加载辅助核算类型失败', error)
  }
}

onMounted(async () => {
  await loadPeriods()
  loadAuxiliaryTypes()
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

  const headers = ['科目编码', '科目名称', '辅助信息', '期初余额(借)', '期初余额(贷)', '本期发生(借)', '本期发生(贷)', '期末余额(借)', '期末余额(贷)']
  const rows = tableData.value.map((row: any) => [
    row.accountCode ?? '',
    row.accountName ?? '',
    row.auxiliaryInfo ?? '',
    row.openingDebit ?? '',
    row.openingCredit ?? '',
    row.periodDebit ?? '',
    row.periodCredit ?? '',
    row.closingDebit ?? '',
    row.closingCredit ?? ''
  ])

  const csv = [headers.join(','), ...rows.map((r: any[]) => r.map((v: any) => `"${String(v ?? '').replace(/"/g, '""')}"`).join(','))].join('\n')
  const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `辅助余额表_${periodStr}.csv`
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

</style>
