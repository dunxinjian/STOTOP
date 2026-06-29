<template>
  <div class="page-container">
    <PageHeader title="资产负债表">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-select v-model:value="filterForm.periodId" placeholder="选择期间" style="width: 180px"
          :options="periodList.map(p => ({ label: p.label, value: p.id }))" />
        <a-select v-model:value="unitType" style="width: 100px">
          <a-select-option value="yuan">单位：元</a-select-option>
          <a-select-option value="wan">单位：万元</a-select-option>
        </a-select>
        <a-button @click="exportToExcel"><DownloadOutlined />导出Excel</a-button>
      </template>
    </PageHeader>

    <!-- 资产负债表 -->
    <div class="sheet-container">
      <a-card :bordered="false" class="sheet-section">
        <div class="section-title">资产</div>
        <a-spin :spinning="loading">
          <a-table
            :columns="sheetColumns"
            :dataSource="assetData"
            rowKey="id"
            :pagination="false"
            bordered
            :defaultExpandAllRows="false"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'itemName'">
                <span :style="{ paddingLeft: (record.level - 1) * 16 + 'px', fontWeight: record.isTotal ? 'bold' : 'normal' }">
                  {{ record.itemName }}
                </span>
              </template>
              <template v-if="column.dataIndex === 'endingBalance'">
                <a class="drill-link" @click="handleDrillDown(record)">
                  {{ formatAmount(record.endingBalance) }}
                </a>
              </template>
              <template v-if="column.dataIndex === 'openingBalance'">
                {{ formatAmount(record.openingBalance) }}
              </template>
            </template>
            <template #emptyText><EmptyState /></template>
          </a-table>
        </a-spin>
      </a-card>
      <a-card :bordered="false" class="sheet-section">
        <div class="section-title">负债及所有者权益</div>
        <a-spin :spinning="loading">
          <a-table
            :columns="sheetColumns"
            :dataSource="liabilityData"
            rowKey="id"
            :pagination="false"
            bordered
            :defaultExpandAllRows="false"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'itemName'">
                <span :style="{ paddingLeft: (record.level - 1) * 16 + 'px', fontWeight: record.isTotal ? 'bold' : 'normal' }">
                  {{ record.itemName }}
                </span>
              </template>
              <template v-if="column.dataIndex === 'endingBalance'">
                <a class="drill-link" @click="handleDrillDown(record)">
                  {{ formatAmount(record.endingBalance) }}
                </a>
              </template>
              <template v-if="column.dataIndex === 'openingBalance'">
                {{ formatAmount(record.openingBalance) }}
              </template>
            </template>
            <template #emptyText><EmptyState /></template>
          </a-table>
        </a-spin>
      </a-card>
    </div>

    <!-- 钻取明细弹窗 -->
    <DrillDownModal
      v-model:visible="drillDownVisible"
      :title="drillDownTitle"
      :data="drillDownData"
    />

    <!-- 图表分析区域 -->
    <a-card :bordered="false" style="margin-top: 16px" v-if="assetData.length > 0">
      <a-collapse v-model:activeKey="chartCollapseKey" ghost>
        <a-collapse-panel key="charts" header="资产/负债/权益构成分析">
          <div class="charts-grid">
            <div class="chart-item">
              <FinancePieChart :data="assetCompositionData" title="资产构成" />
            </div>
            <div class="chart-item">
              <FinancePieChart :data="liabilityEquityCompositionData" title="负债与权益构成" />
            </div>
          </div>
        </a-collapse-panel>
      </a-collapse>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { message } from 'ant-design-vue'
import { DownloadOutlined } from '@ant-design/icons-vue'
import { getBalanceSheet, getPeriods, getReportDrillDown } from '@/api/finance'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import DrillDownModal from '@/components/charts/DrillDownModal.vue'
import FinancePieChart from '@/components/charts/FinancePieChart.vue'
import { useAccountSetStore } from '@/stores/accountSet'

const accountSetStore = useAccountSetStore()

// 筛选表单
const filterForm = ref({
  periodId: undefined as number | undefined
})

const unitType = ref('yuan')
const periodList = ref<{ id: number; label: string; year: number; month: number }[]>([])
const loading = ref(false)

// 钻取相关
const drillDownVisible = ref(false)
const drillDownTitle = ref('')
const drillDownData = ref<any[]>([])

// 图表相关
const chartCollapseKey = ref<string[]>([])

// 资产构成饼图数据
const assetCompositionData = computed(() => {
  const items = flattenForChart(assetData.value).filter(i => !i.isTotal && Math.abs(i.endingBalance || 0) > 0)
  const total = items.reduce((sum: number, d: any) => sum + Math.abs(d.endingBalance || 0), 0)
  return items.map((d: any) => ({
    name: d.itemName,
    value: Math.abs(d.endingBalance || 0),
    percentage: total ? Math.round(Math.abs(d.endingBalance || 0) / total * 100 * 100) / 100 : 0
  }))
})

// 负债与权益构成饼图数据
const liabilityEquityCompositionData = computed(() => {
  const items = flattenForChart(liabilityData.value).filter(i => !i.isTotal && Math.abs(i.endingBalance || 0) > 0)
  const total = items.reduce((sum: number, d: any) => sum + Math.abs(d.endingBalance || 0), 0)
  return items.map((d: any) => ({
    name: d.itemName,
    value: Math.abs(d.endingBalance || 0),
    percentage: total ? Math.round(Math.abs(d.endingBalance || 0) / total * 100 * 100) / 100 : 0
  }))
})

function flattenForChart(data: any[]): any[] {
  const result: any[] = []
  data.forEach(item => {
    if (item.children?.length) {
      item.children.forEach((c: any) => result.push(c))
    } else {
      result.push(item)
    }
  })
  return result
}

const sheetColumns = [
  { title: '项目', dataIndex: 'itemName', key: 'itemName', width: 220 },
  { title: '期末余额', dataIndex: 'endingBalance', key: 'endingBalance', width: 140, align: 'right' as const },
  { title: '期初余额', dataIndex: 'openingBalance', key: 'openingBalance', width: 140, align: 'right' as const },
]

// 资产数据
const assetData = ref([
  {
    id: '1', itemName: '流动资产：', level: 1, isTotal: false, endingBalance: 0, openingBalance: 0,
    children: [
      { id: '1-1', itemName: '货币资金', level: 2, endingBalance: 5000000, openingBalance: 4500000 },
      { id: '1-2', itemName: '应收账款', level: 2, endingBalance: 8000000, openingBalance: 7500000 },
      { id: '1-3', itemName: '存货', level: 2, endingBalance: 6000000, openingBalance: 5500000 }
    ]
  },
  {
    id: '2', itemName: '非流动资产：', level: 1, isTotal: false, endingBalance: 0, openingBalance: 0,
    children: [
      { id: '2-1', itemName: '固定资产', level: 2, endingBalance: 15000000, openingBalance: 16000000 },
      { id: '2-2', itemName: '无形资产', level: 2, endingBalance: 3000000, openingBalance: 3200000 }
    ]
  },
  { id: '3', itemName: '资产总计', level: 1, isTotal: true, endingBalance: 37000000, openingBalance: 36700000 }
])

// 负债数据
const liabilityData = ref([
  {
    id: '1', itemName: '流动负债：', level: 1, isTotal: false, endingBalance: 0, openingBalance: 0,
    children: [
      { id: '1-1', itemName: '短期借款', level: 2, endingBalance: 3000000, openingBalance: 3500000 },
      { id: '1-2', itemName: '应付账款', level: 2, endingBalance: 5000000, openingBalance: 4800000 },
      { id: '1-3', itemName: '应交税费', level: 2, endingBalance: 800000, openingBalance: 750000 }
    ]
  },
  {
    id: '2', itemName: '非流动负债：', level: 1, isTotal: false, endingBalance: 0, openingBalance: 0,
    children: [
      { id: '2-1', itemName: '长期借款', level: 2, endingBalance: 5000000, openingBalance: 5500000 }
    ]
  },
  {
    id: '3', itemName: '所有者权益：', level: 1, isTotal: false, endingBalance: 0, openingBalance: 0,
    children: [
      { id: '3-1', itemName: '实收资本', level: 2, endingBalance: 10000000, openingBalance: 10000000 },
      { id: '3-2', itemName: '未分配利润', level: 2, endingBalance: 13200000, openingBalance: 12150000 }
    ]
  },
  { id: '4', itemName: '负债及所有者权益总计', level: 1, isTotal: true, endingBalance: 37000000, openingBalance: 36700000 }
])

// 金额格式化
function formatAmount(val: number | undefined) {
  if (val === undefined || val === null) return '0.00'
  const divisor = unitType.value === 'wan' ? 10000 : 1
  const value = val / divisor
  return value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 加载数据
async function loadData() {
  loading.value = true
  try {
    const accountSetId = accountSetStore.currentAccountSetId || undefined
    const res = await getBalanceSheet({
      periodId: filterForm.value.periodId,
      accountSetId
    }) as any[]
    if (res && res.length > 0) {
      const mapItem = (item: any, level: number = 1) => ({
        id: String(item.rowIndex),
        itemName: item.itemName,
        level,
        isTotal: item.itemName?.includes('合计') || item.itemName?.includes('总计'),
        endingBalance: item.endAmount ?? 0,
        openingBalance: item.beginAmount ?? 0
      })
      const assets = res.filter((item: any) => item.category === '资产').map(i => mapItem(i))
      const liabilitiesAndEquity = res.filter((item: any) => item.category === '负债' || item.category === '权益').map(i => mapItem(i))
      if (assets.length > 0) assetData.value = assets
      if (liabilitiesAndEquity.length > 0) liabilityData.value = liabilitiesAndEquity
    }
  } catch (error) {
    console.error('加载资产负债表数据失败', error)
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
        if (a.year !== b.year) return b.year - a.year
        return b.periodNo - a.periodNo
      })
      .map((p: any) => ({
        id: p.id,
        label: `${p.year}年第${String(p.periodNo).padStart(2, '0')}期`,
        year: p.year,
        month: p.periodNo
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

onMounted(() => {
  loadPeriods()
  loadData()
})

watch(() => accountSetStore.currentAccountSetId, async () => {
  filterForm.value.periodId = undefined
  await loadPeriods()
  loadData()
})

// 钻取处理函数
async function handleDrillDown(record: any) {
  if (!record.id || record.isTotal) return
  const selectedPeriod = periodList.value.find(p => p.id === filterForm.value.periodId)
  const year = selectedPeriod?.year || new Date().getFullYear()
  const month = selectedPeriod?.month || (new Date().getMonth() + 1)
  const rowIndex = parseInt(String(record.id)) || 0
  drillDownTitle.value = `${record.itemName} - 凭证明细`
  try {
    const res = await getReportDrillDown({
      reportType: '资产负债表',
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

// 导出Excel(CSV)
function exportToExcel() {
  const allData = [...assetData.value, ...liabilityData.value]
  if (!allData.length) { message.warning('暂无数据可导出'); return }

  const selectedPeriod = periodList.value.find(p => p.id === filterForm.value.periodId)
  const periodStr = selectedPeriod ? selectedPeriod.label : new Date().toISOString().slice(0, 7)

  function flattenRows(rows: any[]): any[] {
    const result: any[] = []
    rows.forEach(row => {
      result.push(row)
      if (row.children && row.children.length) result.push(...flattenRows(row.children))
    })
    return result
  }

  const headers = ['资产项目', '期末余额', '期初余额', '', '负债及权益项目', '期末余额', '期初余额']
  const assetFlat = flattenRows(assetData.value)
  const liabilityFlat = flattenRows(liabilityData.value)
  const maxLen = Math.max(assetFlat.length, liabilityFlat.length)
  const rows: string[][] = []
  for (let i = 0; i < maxLen; i++) {
    const a = assetFlat[i]
    const l = liabilityFlat[i]
    rows.push([
      a?.itemName ?? '', String(a?.endingBalance ?? ''), String(a?.openingBalance ?? ''),
      '',
      l?.itemName ?? '', String(l?.endingBalance ?? ''), String(l?.openingBalance ?? '')
    ])
  }

  const csv = [headers.join(','), ...rows.map((r: string[]) => r.map((v: string) => `"${v.replace(/"/g, '""')}"`).join(','))].join('\n')
  const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `资产负债表_${periodStr}.csv`
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.sheet-container {
  display: flex;
  gap: $section-gap;

  .sheet-section {
    flex: 1;

    .section-title {
      font-size: 16px;
      font-weight: bold;
      color: #303133;
      margin-bottom: $section-gap;
      padding-bottom: 12px;
      border-bottom: 1px solid #e4e7ed;
    }
  }
}

.drill-link {
  color: var(--text-1);
  cursor: pointer;
  &:hover { color: var(--color-primary); text-decoration: underline; }
}

.charts-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  .chart-item {
    min-height: 350px;
  }
}

@media (max-width: 1200px) {
  .sheet-container {
    flex-direction: column;
  }
}

.charts-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 24px;

  .chart-item {
    min-height: 300px;
    background: #fafafa;
    border-radius: 8px;
    padding: 16px;
  }
}

.drill-link {
  color: var(--text-1);
  cursor: pointer;
  text-decoration: none;

  &:hover {
    color: var(--color-primary);
    text-decoration: underline;
  }
}
</style>
