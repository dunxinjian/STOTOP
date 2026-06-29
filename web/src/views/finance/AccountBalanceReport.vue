<template>
  <div class="page-container">
    <PageHeader title="科目余额表">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-select
          v-model:value="selectedPeriodId"
          placeholder="选择账期"
          style="width: 160px"
          @change="onPeriodChange"
          :options="periodList.map(p => ({ label: p.label, value: p.id }))"
        />
        <a-select
          v-model:value="filterForm.accountId"
          placeholder="选择科目"
          allowClear
          showSearch
          :filterOption="accountFilterOption"
          style="width: 200px"
          :options="accountList.map(a => ({ label: a.code + ' ' + a.name, value: a.id }))"
        />
        <a-select v-model:value="levelFilter" placeholder="级次" allowClear style="width: 100px">
          <a-select-option :value="1">1级</a-select-option>
          <a-select-option :value="2">2级</a-select-option>
          <a-select-option :value="3">3级</a-select-option>
        </a-select>
        <a-button @click="chartDrawerVisible = true"><BarChartOutlined />图表分析</a-button>
        <a-button @click="exportToExcel"><DownloadOutlined />导出Excel</a-button>
      </template>
    </PageHeader>

    <!-- 数据表区域 -->
    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :dataSource="filteredTableData"
        rowKey="accountId"
        :loading="loading"
        :pagination="false"
        bordered
        :defaultExpandAllRows="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'accountName'">
            <span
              v-if="record.accountName !== '合计'"
              class="account-link"
              @click="drillToDetail(record)"
            >
              {{ record.accountName }}
            </span>
            <span v-else style="font-weight: bold">{{ record.accountName }}</span>
          </template>
          <template v-if="column.dataIndex === 'beginDebit'">
            <span :class="{ 'total-row': record.accountName === '合计' }">
              {{ formatAmount(record.beginDebit) }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'beginCredit'">
            <span :class="{ 'total-row': record.accountName === '合计' }">
              {{ formatAmount(record.beginCredit) }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'currentDebit'">
            <span :class="{ 'total-row': record.accountName === '合计' }">
              {{ formatAmount(record.currentDebit) }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'currentCredit'">
            <span :class="{ 'total-row': record.accountName === '合计' }">
              {{ formatAmount(record.currentCredit) }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'endDebit'">
            <span :class="{ 'total-row': record.accountName === '合计' }">
              {{ formatAmount(record.endDebit) }}
            </span>
          </template>
          <template v-if="column.dataIndex === 'endCredit'">
            <span :class="{ 'total-row': record.accountName === '合计' }">
              {{ formatAmount(record.endCredit) }}
            </span>
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
      :loading="drillDownLoading"
    />

    <!-- 图表分析 Drawer -->
    <a-drawer
      v-model:open="chartDrawerVisible"
      title="图表分析"
      :width="900"
      placement="right"
      :destroyOnClose="true"
    >
      <div v-if="filteredTableData.length > 0" style="display: flex; flex-direction: column; gap: 24px;">
        <FinanceBarChart :data="yoyComparisonData" title="同比对比" currentLabel="本期" previousLabel="去年同期" :height="400" />
        <FinanceBarChart :data="momComparisonData" title="环比对比" currentLabel="本期" previousLabel="上期" :height="400" />
      </div>
      <div v-else style="text-align: center; padding: 40px 0; color: #999;">
        暂无图表数据，请先查询报表
      </div>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { DownloadOutlined, BarChartOutlined } from '@ant-design/icons-vue'
import { getAccountBalanceByYearMonth, getAccountTree, getPeriods, getYoYComparison, getMoMComparison, getReportDrillDown } from '@/api/finance'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import FinanceBarChart from '@/components/charts/FinanceBarChart.vue'
import DrillDownModal from '@/components/charts/DrillDownModal.vue'
import { useAccountSetStore } from '@/stores/accountSet'

const router = useRouter()
const accountSetStore = useAccountSetStore()

// 筛选表单
const now = new Date()
const filterForm = ref({
  year: now.getFullYear(),
  month: now.getMonth() + 1,
  accountId: undefined as number | undefined
})

// 期间选择
const selectedPeriodId = ref<number | undefined>(undefined)
const periodList = ref<{ id: number; label: string; year: number; month: number }[]>([])

const levelFilter = ref<number | undefined>(undefined)
const accountList = ref<{ id: number; code: string; name: string }[]>([])

// 表格数据
const tableData = ref<any[]>([])
const loading = ref(false)

// 图表相关
const chartDrawerVisible = ref(false)
const yoyComparisonData = ref<any[]>([])
const momComparisonData = ref<any[]>([])

const columns = [
  { title: '科目编码', dataIndex: 'accountCode', key: 'accountCode', width: 120 },
  { title: '科目名称', dataIndex: 'accountName', key: 'accountName', width: 200 },
  {
    title: '期初余额',
    children: [
      { title: '借方', dataIndex: 'beginDebit', key: 'beginDebit', width: 140, align: 'right' as const },
      { title: '贷方', dataIndex: 'beginCredit', key: 'beginCredit', width: 140, align: 'right' as const },
    ],
  },
  {
    title: '本期发生',
    children: [
      { title: '借方', dataIndex: 'currentDebit', key: 'currentDebit', width: 140, align: 'right' as const },
      { title: '贷方', dataIndex: 'currentCredit', key: 'currentCredit', width: 140, align: 'right' as const },
    ],
  },
  {
    title: '期末余额',
    children: [
      { title: '借方', dataIndex: 'endDebit', key: 'endDebit', width: 140, align: 'right' as const },
      { title: '贷方', dataIndex: 'endCredit', key: 'endCredit', width: 140, align: 'right' as const },
    ],
  },
]

// 过滤后的数据
const filteredTableData = computed(() => {
  if (levelFilter.value === undefined) return tableData.value
  return filterByLevel(tableData.value, levelFilter.value as number)
})

function filterByLevel(data: any[], maxLevel: number): any[] {
  return data.filter(item => item.level <= maxLevel).map(item => {
    const newItem = { ...item }
    if (item.children && item.children.length > 0) {
      newItem.children = filterByLevel(item.children, maxLevel)
    }
    return newItem
  })
}

function accountFilterOption(input: string, option: any) {
  return option.label.toLowerCase().includes(input.toLowerCase())
}

// 期间变化处理
function onPeriodChange() {
  const p = periodList.value.find(x => x.id === selectedPeriodId.value)
  if (p) {
    filterForm.value.year = p.year
    filterForm.value.month = p.month
    loadData()
  }
}

// 金额格式化
function formatAmount(val: number | undefined) {
  if (val === undefined || val === null || val === 0) return '-'
  return val.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 加载数据
async function loadData() {
  loading.value = true
  try {
    const res = await getAccountBalanceByYearMonth({
      year: filterForm.value.year,
      month: filterForm.value.month,
      accountId: filterForm.value.accountId,
      accountSetId: accountSetStore.currentAccountSetId || undefined
    }) as any[]
    if (res) {
      tableData.value = res
    }
  } catch (error) {
    console.error('加载科目余额数据失败', error)
  } finally {
    loading.value = false
  }
}

// 加载科目列表
async function loadAccounts() {
  try {
    const res = await getAccountTree() as any[]
    if (res) {
      accountList.value = flattenAccountTree(res)
    }
  } catch (error) {
    console.error('加载科目列表失败', error)
  }
}

function flattenAccountTree(tree: any[]): { id: number; code: string; name: string }[] {
  const result: { id: number; code: string; name: string }[] = []
  function traverse(nodes: any[]) {
    nodes.forEach(node => {
      result.push({ id: node.id, code: node.code, name: node.name })
      if (node.children && node.children.length > 0) {
        traverse(node.children)
      }
    })
  }
  traverse(tree)
  return result
}

// 钻取弹窗状态
const drillDownVisible = ref(false)
const drillDownTitle = ref('')
const drillDownData = ref<any[]>([])
const drillDownLoading = ref(false)

// 钻取到明细账
async function drillToDetail(row: any) {
  if (!row.accountId && !row.id) return
  drillDownTitle.value = `${row.accountCode} ${row.accountName} - 凭证明细`
  drillDownVisible.value = true
  drillDownLoading.value = true
  try {
    const accountSetId = accountSetStore.currentAccountSetId || undefined
    const res = await getReportDrillDown({
      reportType: 'account-balance',
      rowIndex: 0,
      year: filterForm.value.year,
      month: filterForm.value.month,
      accountSetId,
      accountCode: row.accountCode
    })
    drillDownData.value = res || []
  } catch (e) {
    console.error('加载钻取明细失败', e)
    drillDownData.value = []
  } finally {
    drillDownLoading.value = false
  }
}

// 导出Excel(CSV)
function exportToExcel() {
  const data = filteredTableData.value
  if (!data || !data.length) { message.warning('暂无数据可导出'); return }

  function flattenRows(rows: any[]): any[] {
    const result: any[] = []
    rows.forEach(row => {
      result.push(row)
      if (row.children && row.children.length) {
        result.push(...flattenRows(row.children))
      }
    })
    return result
  }

  const flat = flattenRows(data)
  const headers = ['科目编码', '科目名称', '期初余额(借)', '期初余额(贷)', '本期发生(借)', '本期发生(贷)', '期末余额(借)', '期末余额(贷)']
  const rows = flat.map((row: any) => [
    row.accountCode || '',
    row.accountName || '',
    row.beginDebit ?? '',
    row.beginCredit ?? '',
    row.currentDebit ?? '',
    row.currentCredit ?? '',
    row.endDebit ?? '',
    row.endCredit ?? ''
  ])

  const csv = [headers.join(','), ...rows.map((r: any[]) => r.map((v: any) => `"${String(v).replace(/"/g, '""')}"`).join(','))].join('\n')
  const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `科目余额表_${filterForm.value.year}年第${String(filterForm.value.month).padStart(2, '0')}期.csv`
  a.click()
  URL.revokeObjectURL(url)
}

onMounted(async () => {
  loadAccounts()
  await loadPeriods()
  loadData()
  loadChartData()
})

watch(() => accountSetStore.currentAccountSetId, async () => {
  selectedPeriodId.value = undefined
  await loadPeriods()
  loadData()
  loadChartData()
})

// 加载图表数据
async function loadChartData() {
  const year = filterForm.value.year
  const month = filterForm.value.month
  const accountSetId = accountSetStore.currentAccountSetId || undefined
  try {
    const [yoy, mom] = await Promise.all([
      getYoYComparison({ year, month, accountSetId }).catch(() => []),
      getMoMComparison({ year, month, accountSetId }).catch(() => [])
    ])
    yoyComparisonData.value = yoy || []
    momComparisonData.value = mom || []
  } catch (e) {
    console.error('加载图表数据失败', e)
  }
}

// 加载期间列表
async function loadPeriods() {
  try {
    const accountSetId = accountSetStore.currentAccountSetId || undefined
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
      selectedPeriodId.value = defaultPeriod.id
      filterForm.value.year = defaultPeriod.year
      filterForm.value.month = defaultPeriod.month
    }
  } catch (e) { console.error('加载期间失败', e) }
}
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.total-row {
  font-weight: bold;
}

.account-link {
  color: var(--text-1);
  cursor: pointer;
  &:hover { color: var(--color-primary); text-decoration: underline; }
}

</style>
