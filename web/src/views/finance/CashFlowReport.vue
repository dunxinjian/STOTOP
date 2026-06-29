<template>
  <div class="page-container">
    <PageHeader title="现金流量表">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-select v-model:value="unitType" style="width: 100px">
          <a-select-option value="yuan">单位：元</a-select-option>
          <a-select-option value="wan">单位：万元</a-select-option>
        </a-select>
        <a-button @click="chartDrawerVisible = true"><BarChartOutlined />图表分析</a-button>
        <a-button @click="exportToExcel"><DownloadOutlined />导出Excel</a-button>
      </template>
      <template #toolbar>
        <div style="display:flex; align-items:center; justify-content:space-between; width:100%;">
          <div style="display:flex; align-items:center; gap:8px;">
            <a-select v-model:value="filterForm.startPeriodId" placeholder="开始期间" style="width: 160px"
              :options="periodList.map(p => ({ label: p.label, value: p.id }))"
              @change="handlePeriodChange" />
            <span class="to-text">至</span>
            <a-select v-model:value="filterForm.endPeriodId" placeholder="结束期间" style="width: 160px"
              :options="periodList.map(p => ({ label: p.label, value: p.id }))"
              @change="handlePeriodChange" />
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
        rowKey="id"
        :loading="loading"
        :pagination="false"
        bordered
        :defaultExpandAllRows="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'itemName'">
            <span :style="{ paddingLeft: (record.level - 1) * 20 + 'px', fontWeight: record.isTotal ? 'bold' : 'normal' }">
              {{ record.itemName }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'currentAmount'">
            <a class="drill-link" @click="handleDrillDown(record)">
              {{ formatAmount(record.currentAmount) }}
            </a>
          </template>
          <template v-if="column.dataIndex === 'previousAmount'">
            {{ formatAmount(record.previousAmount) }}
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>

    <!-- 钻取明细弹窗 -->
    <DrillDownModal
      v-model:visible="drillDownVisible"
      :title="drillDownTitle"
      :data="drillDownData"
    />

    <!-- 图表分析抽屉 -->
    <a-drawer
      v-model:open="chartDrawerVisible"
      title="图表分析"
      :width="900"
      placement="right"
      :destroyOnClose="true"
    >
      <div v-if="tableData.length > 0">
        <FinanceTrendChart :data="trendData" title="现金流量趋势" :height="500" />
      </div>
      <div v-else style="text-align: center; padding: 40px 0; color: #999;">
        暂无图表数据，请先查询报表
      </div>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { DownloadOutlined, BarChartOutlined } from '@ant-design/icons-vue'
import { getCashFlowReport, getPeriods, getReportDrillDown, getProfitTrend } from '@/api/finance'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import DrillDownModal from '@/components/charts/DrillDownModal.vue'
import FinanceTrendChart from '@/components/charts/FinanceTrendChart.vue'
import { useAccountSetStore } from '@/stores/accountSet'

const accountSetStore = useAccountSetStore()

// 筛选表单
const filterForm = ref({
  startPeriodId: undefined as number | undefined,
  endPeriodId: undefined as number | undefined
})

const unitType = ref('yuan')
const periodList = ref<{ id: number; label: string; year: number; month: number }[]>([])
const loading = ref(false)

// 钻取相关
const drillDownVisible = ref(false)
const drillDownTitle = ref('')
const drillDownData = ref<any[]>([])

// 图表相关
const chartDrawerVisible = ref(false)
const trendData = ref<any[]>([])

const columns = [
  { title: '项目', dataIndex: 'itemName', key: 'itemName', width: 300 },
  { title: '本期金额', dataIndex: 'currentAmount', key: 'currentAmount', width: 160, align: 'right' as const },
  { title: '上期金额', dataIndex: 'previousAmount', key: 'previousAmount', width: 160, align: 'right' as const },
]

// 表格数据
const tableData = ref<any[]>([])

// 金额格式化
function formatAmount(val: number | undefined) {
  if (val === undefined || val === null) return '0.00'
  const divisor = unitType.value === 'wan' ? 10000 : 1
  const value = val / divisor
  return value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 加载数据
async function loadData() {
  if (!filterForm.value.startPeriodId || !filterForm.value.endPeriodId) return
  loading.value = true
  try {
    const accountSetId = accountSetStore.currentAccountSetId || undefined
    const res = await getCashFlowReport({
      startPeriodId: filterForm.value.startPeriodId,
      endPeriodId: filterForm.value.endPeriodId,
      accountSetId
    }) as any[]
    if (res) {
      tableData.value = res
    }
  } catch (error) {
    console.error('加载现金流量表数据失败', error)
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
        const yearA = a.year ?? a.fYear ?? 0
        const yearB = b.year ?? b.fYear ?? 0
        if (yearA !== yearB) return yearB - yearA
        const periodA = a.periodNo ?? a.fPeriodNo ?? 0
        const periodB = b.periodNo ?? b.fPeriodNo ?? 0
        return periodB - periodA
      })
      .map((p: any) => ({
        id: p.id,
        label: `${p.year ?? p.fYear ?? 0}年第${String(p.periodNo ?? p.fPeriodNo ?? 1).padStart(2, '0')}期`,
        year: p.year ?? p.fYear ?? 0,
        month: p.periodNo ?? p.fPeriodNo ?? 1
      }))
    if (periodList.value.length > 0) {
      const now = new Date()
      const currentYear = now.getFullYear()
      const currentMonth = now.getMonth() + 1
      let defaultPeriod = periodList.value.find(
        (p: any) => p.year === currentYear && p.month === currentMonth
      )
      if (!defaultPeriod) defaultPeriod = periodList.value[0]
      filterForm.value.endPeriodId = defaultPeriod.id
      filterForm.value.startPeriodId = periodList.value[periodList.value.length - 1].id
    }
  } catch (e) {
    console.error('加载期间失败', e)
  }
}

onMounted(async () => {
  await loadPeriods()
  loadData()
  loadChartData()
})

watch(() => accountSetStore.currentAccountSetId, async () => {
  filterForm.value.startPeriodId = undefined
  filterForm.value.endPeriodId = undefined
  await loadPeriods()
  loadData()
  loadChartData()
})

function handlePeriodChange() {
  loadData()
  loadChartData()
}

// 钻取处理
async function handleDrillDown(record: any) {
  if (!record.id) return
  const selectedPeriod = periodList.value.find(p => p.id === filterForm.value.endPeriodId)
  const year = selectedPeriod?.year || new Date().getFullYear()
  const month = selectedPeriod?.month || (new Date().getMonth() + 1)
  drillDownTitle.value = `${record.itemName} - 凭证明细`
  try {
    const rowIndex = parseInt(String(record.id).replace(/[^\d]/g, '')) || 0
    const res = await getReportDrillDown({
      reportType: '现金流量表',
      rowIndex,
      year,
      month,
      accountSetId: accountSetStore.currentAccountSetId || undefined
    })
    drillDownData.value = res || []
    drillDownVisible.value = true
  } catch (e) {
    console.error('钻取失败', e)
  }
}

// 加载图表数据
async function loadChartData() {
  const selectedPeriod = periodList.value.find(p => p.id === filterForm.value.endPeriodId)
  const year = selectedPeriod?.year || new Date().getFullYear()
  const accountSetId = accountSetStore.currentAccountSetId || undefined
  try {
    const trend = await getProfitTrend({ year, accountSetId }).catch(() => [])
    trendData.value = trend || []
  } catch (e) {
    console.error('加载图表数据失败', e)
  }
}

// 导出Excel(CSV)
function exportToExcel() {
  if (!tableData.value || !tableData.value.length) { message.warning('暂无数据可导出'); return }

  function flattenRows(rows: any[]): any[] {
    const result: any[] = []
    rows.forEach(row => {
      result.push(row)
      if (row.children && row.children.length) result.push(...flattenRows(row.children))
    })
    return result
  }

  const flat = flattenRows(tableData.value)
  const startPeriod = periodList.value.find(p => p.id === filterForm.value.startPeriodId)
  const endPeriod = periodList.value.find(p => p.id === filterForm.value.endPeriodId)
  const periodStr = `${startPeriod?.label || ''}至${endPeriod?.label || ''}`

  const headers = ['项目', '本期金额', '上期金额']
  const rows = flat.map((row: any) => [row.itemName, row.currentAmount ?? '', row.previousAmount ?? ''])

  const csv = [headers.join(','), ...rows.map((r: any[]) => r.map((v: any) => `"${String(v ?? '').replace(/"/g, '""')}"`).join(','))].join('\n')
  const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `现金流量表_${periodStr}.csv`
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.to-text {
  color: #606266;
  padding: 0 4px;
}

.drill-link {
  color: var(--text-1);
  cursor: pointer;
  &:hover { color: var(--color-primary); text-decoration: underline; }
}
</style>
