<template>
  <div class="page-container">
    <PageHeader title="毛利分析" description="客户毛利数据分析与报表">
      <template #actions>
        <a-space>
          <a-button v-if="has(CrmPermissions.ProfitCalc)" @click="handleCalc">
            <template #icon><CalculatorOutlined /></template>手动汇算
          </a-button>
          <a-button @click="handleExport">
            <template #icon><DownloadOutlined /></template>导出 Excel
          </a-button>
        </a-space>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-date-picker v-model:value="filterPeriod" picker="month" placeholder="选择期间" size="small" style="width: 160px" @change="handleFilter" />
          <a-select v-model:value="filterOrgId" placeholder="选择组织" allow-clear size="small" style="width: 160px" @change="handleFilter" />
          <a-select v-model:value="filterCustomerId" placeholder="搜索客户" show-search :filter-option="false" allow-clear size="small" style="width: 160px" @change="handleFilter" />
        </div>
      </template>
    </PageHeader>

    <!-- 统计卡片 -->
    <a-row :gutter="16" class="stat-cards">
      <a-col :span="6">
        <a-card :bordered="false"><a-statistic title="总收入" :value="summary.totalRevenue" prefix="¥" :precision="2" /></a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false"><a-statistic title="总成本" :value="summary.totalCost" prefix="¥" :precision="2" /></a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false"><a-statistic title="总毛利" :value="summary.totalProfit" prefix="¥" :precision="2" /></a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false"><a-statistic title="平均毛利率" :value="summary.avgProfitRate" suffix="%" :precision="1" /></a-card>
      </a-col>
    </a-row>

    <!-- 图表区 -->
    <a-row :gutter="16" class="chart-section">
      <a-col :span="12">
        <a-card title="毛利趋势" :bordered="false">
          <v-chart :option="trendChartOption" autoresize style="height: 360px" />
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card title="客户毛利排行 Top 10" :bordered="false">
          <v-chart :option="rankingChartOption" autoresize style="height: 360px" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 明细表格 -->
    <a-card title="毛利明细" :bordered="false" class="detail-section">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1100 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'revenue'">¥{{ record.revenue?.toFixed(2) }}</template>
          <template v-if="column.dataIndex === 'cost'">¥{{ record.cost?.toFixed(2) }}</template>
          <template v-if="column.dataIndex === 'profit'">
            <span :style="{ color: record.profit >= 0 ? 'var(--color-success)' : 'var(--color-danger)' }">¥{{ record.profit?.toFixed(2) }}</span>
          </template>
          <template v-if="column.dataIndex === 'profitRate'">
            <span :style="{ color: record.profitRate >= 0 ? 'var(--color-success)' : 'var(--color-danger)' }">{{ record.profitRate?.toFixed(1) }}%</span>
          </template>
          <template v-if="column.dataIndex === 'dataSource'">
            <a-tag :color="record.dataSource === 1 ? 'blue' : 'green'">
              {{ record.dataSource === 1 ? '自动' : '手动' }}
            </a-tag>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无毛利数据" />
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { DownloadOutlined, CalculatorOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { CrmPermissions, usePermission } from '@/utils/permission'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, BarChart } from 'echarts/charts'
import { TitleComponent, TooltipComponent, LegendComponent, GridComponent } from 'echarts/components'
import {
  getProfitList, getProfitSummary, getProfitRanking,
  type CustomerProfitDto, type ProfitSummaryDto, type ProfitRankingDto,
} from '@/api/crm'

use([CanvasRenderer, LineChart, BarChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

const { has } = usePermission()

// 筛选
const filterPeriod = ref(null as any)
const filterOrgId = ref<number | undefined>(undefined)
const filterCustomerId = ref<number | undefined>(undefined)
const loading = ref(false)

// 汇总
const summary = reactive<ProfitSummaryDto>({
  totalRevenue: 0, totalCost: 0, totalProfit: 0, avgProfitRate: 0, customerCount: 0,
})

// 排行
const rankingData = ref<ProfitRankingDto[]>([])

// 表格
const tableColumns = [
  { title: '客户名称', dataIndex: 'customerName', width: 150, ellipsis: true },
  { title: '组织', dataIndex: 'orgId', width: 100 },
  { title: '期间', dataIndex: 'period', width: 100 },
  { title: '收入', dataIndex: 'revenue', width: 120, align: 'right' as const },
  { title: '成本', dataIndex: 'cost', width: 120, align: 'right' as const },
  { title: '毛利', dataIndex: 'profit', width: 120, align: 'right' as const },
  { title: '毛利率', dataIndex: 'profitRate', width: 100, align: 'right' as const },
  { title: '数据来源', dataIndex: 'dataSource', width: 100, align: 'center' as const },
]

const tableData = ref<CustomerProfitDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })
const paginationConfig = computed(() => ({
  current: pagination.pageIndex, pageSize: pagination.pageSize, total: pagination.total,
  showSizeChanger: true, pageSizeOptions: ['10', '20', '50'], showTotal: (t: number) => `共 ${t} 条`,
}))

// 图表 - 毛利趋势（模拟数据）
const trendChartOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  legend: { data: ['毛利金额', '毛利率'] },
  grid: { left: 60, right: 60, top: 40, bottom: 30 },
  xAxis: { type: 'category', data: ['1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '10月', '11月', '12月'] },
  yAxis: [
    { type: 'value', name: '毛利金额', axisLabel: { formatter: '¥{value}' } },
    { type: 'value', name: '毛利率', axisLabel: { formatter: '{value}%' } },
  ],
  series: [
    { name: '毛利金额', type: 'line', data: [12000, 15000, 13000, 18000, 20000, 17000, 22000, 19000, 25000, 23000, 28000, 26000], smooth: true, itemStyle: { color: '#3A6FB0' } },
    { name: '毛利率', type: 'line', yAxisIndex: 1, data: [15, 18, 16, 20, 22, 19, 24, 21, 26, 25, 28, 27], smooth: true, itemStyle: { color: '#2BA471' } },
  ],
}))

// 图表 - 客户排行
const rankingChartOption = computed(() => {
  const names = rankingData.value.slice(0, 10).map(d => d.customerName || '').reverse()
  const profits = rankingData.value.slice(0, 10).map(d => d.totalProfit).reverse()
  return {
    tooltip: { trigger: 'axis' },
    grid: { left: 100, right: 20, top: 20, bottom: 30 },
    xAxis: { type: 'value', axisLabel: { formatter: '¥{value}' } },
    yAxis: { type: 'category', data: names, axisLabel: { width: 80, overflow: 'truncate' } },
    series: [{ type: 'bar', data: profits, itemStyle: { color: '#2BA471' }, barMaxWidth: 30 }],
  }
})

async function fetchList() {
  loading.value = true
  try {
    const params: any = { pageIndex: pagination.pageIndex, pageSize: pagination.pageSize }
    if (filterCustomerId.value) params.customerId = filterCustomerId.value
    if (filterOrgId.value) params.orgId = filterOrgId.value
    if (filterPeriod.value) params.period = filterPeriod.value.format('YYYY-MM')
    const res = await getProfitList(params) as any
    tableData.value = res?.items || res || []
    pagination.total = res?.total || 0
  } finally { loading.value = false }
}

async function fetchSummary() {
  try {
    const period = filterPeriod.value ? filterPeriod.value.format('YYYY-MM') : undefined
    const res = await getProfitSummary(filterOrgId.value, period) as any
    if (res) Object.assign(summary, res)
  } catch { /* use defaults */ }
}

async function fetchRanking() {
  try {
    const period = filterPeriod.value ? filterPeriod.value.format('YYYY-MM') : undefined
    const res = await getProfitRanking(filterOrgId.value, period, 10) as any
    rankingData.value = Array.isArray(res) ? res : res?.items || []
  } catch {
    // 模拟排行数据
    rankingData.value = [
      { customerId: 1, customerName: '客户A', totalProfit: 58000, totalRevenue: 200000, avgProfitRate: 29 },
      { customerId: 2, customerName: '客户B', totalProfit: 45000, totalRevenue: 160000, avgProfitRate: 28.1 },
      { customerId: 3, customerName: '客户C', totalProfit: 38000, totalRevenue: 140000, avgProfitRate: 27.1 },
      { customerId: 4, customerName: '客户D', totalProfit: 32000, totalRevenue: 120000, avgProfitRate: 26.7 },
      { customerId: 5, customerName: '客户E', totalProfit: 28000, totalRevenue: 100000, avgProfitRate: 28 },
    ]
  }
}

function handleFilter() { pagination.pageIndex = 1; fetchList(); fetchSummary(); fetchRanking() }
function handleTableChange(pag: any) { pagination.pageIndex = pag.current; pagination.pageSize = pag.pageSize; fetchList() }

function handleCalc() { message.info('汇算功能开发中') }
function handleExport() { message.info('导出功能开发中') }

onMounted(() => { fetchList(); fetchSummary(); fetchRanking() })
</script>

<style scoped lang="scss">
.stat-cards {
  margin-top: 16px;
}

.chart-section {
  margin-top: 16px;
}

.detail-section {
  margin-top: 16px;
}
</style>
