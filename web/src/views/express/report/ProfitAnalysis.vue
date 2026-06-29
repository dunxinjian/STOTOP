<template>
  <div class="page-container">
    <PageHeader title="毛利分析">
      <template #left>
        <a-radio-group v-model:value="activeView" button-style="solid" size="small" @change="handleViewChange">
          <a-radio-button value="client">业务对象</a-radio-button>
          <a-radio-button value="shop">报价</a-radio-button>
          <a-radio-button value="intermediary">中间人</a-radio-button>
          <a-radio-button value="salesman">业务员</a-radio-button>
          <a-radio-button value="weightSegment">重量段</a-radio-button>
          <a-radio-button value="region">流向</a-radio-button>
          <a-radio-button value="trend">趋势</a-radio-button>
        </a-radio-group>
      </template>
      <template #actions>
        <div style="display: flex; align-items: center; gap: 8px;">
          <a-range-picker v-model:value="dateRange" size="middle" style="width: 260px" />
          <a-select v-model:value="searchForm.brandCode" size="middle" placeholder="品牌" allow-clear style="width: 120px" :options="brandOptions" />
          <a-select v-model:value="searchForm.clientId" size="middle" placeholder="业务对象" allow-clear show-search :filter-option="filterOption" style="width: 160px" :options="clientOptions" />
          <a-divider type="vertical" style="height: 20px; margin: 0 4px;" />
          <a-button type="primary" size="middle" @click="handleSearch">查询</a-button>
          <a-button type="text" size="middle" @click="handleReset">重置</a-button>
          <a-button size="middle" @click="handleExport">导出</a-button>
        </div>
      </template>
    </PageHeader>

    <div class="profit-content">
      <!-- KPI 指标条 -->
      <div class="kpi-strip">
        <div class="kpi-item">
          <span class="kpi-label">{{ waybillKpiLabel }}</span>
          <span class="kpi-value">{{ formatNum(activeSummary.totalWaybills) }}</span>
        </div>
        <div class="kpi-divider" />
        <div class="kpi-item">
          <span class="kpi-label">总应收</span>
          <span class="kpi-value">¥{{ formatNum(activeSummary.totalCharge, 2) }}</span>
        </div>
        <div class="kpi-divider" />
        <div class="kpi-item">
          <span class="kpi-label">总成本</span>
          <span class="kpi-value">¥{{ formatNum(activeSummary.totalCost, 2) }}</span>
        </div>
        <div class="kpi-divider" />
        <div class="kpi-item">
          <span class="kpi-label">总毛利</span>
          <span class="kpi-value" :class="activeSummary.totalProfit >= 0 ? 'kpi-positive' : 'kpi-negative'">
            ¥{{ formatNum(activeSummary.totalProfit, 2) }}
          </span>
        </div>
        <div class="kpi-divider" />
        <div class="kpi-item">
          <span class="kpi-label">毛利率</span>
          <span class="kpi-value" :class="profitRateClass(activeSummary.avgProfitRate)">
            {{ formatRate(activeSummary.avgProfitRate) }}
          </span>
        </div>
        <div class="kpi-divider" />
        <div class="kpi-item" v-if="activeView !== 'trend'">
          <span class="kpi-label">记录数</span>
          <span class="kpi-value">{{ formatNum(activeRecordCount) }}</span>
        </div>
        <template v-if="activeView === 'client' && clientAlertStats.lossCount > 0">
          <div class="kpi-divider" />
          <div
            class="kpi-item kpi-clickable"
            :class="{ 'kpi-active': showLossOnly }"
            :title="showLossOnly ? '点击恢复全部对象' : '点击只看亏损对象'"
            @click="showLossOnly = !showLossOnly"
          >
            <span class="kpi-label">亏损对象</span>
            <span class="kpi-value kpi-value-sm kpi-negative">
              {{ clientAlertStats.lossCount }} 个 · ¥{{ formatNum(clientAlertStats.lossProfit, 2) }}
            </span>
          </div>
        </template>
        <template v-if="activeView === 'client' && clientAlertStats.zeroChargeCount > 0">
          <div class="kpi-divider" />
          <div class="kpi-item" title="应收金额≤0 的运单数，通常为未匹配报价或零应收，请核查计费数据">
            <span class="kpi-label">零应收运单</span>
            <span class="kpi-value kpi-value-sm kpi-negative">{{ formatNum(clientAlertStats.zeroChargeCount) }}</span>
          </div>
        </template>
      </div>

      <!-- 内容区 -->
      <div class="view-content">
        <!-- 多视角动态表（client/shop/intermediary/salesman/weightSegment/region 合一；趋势图独立） -->
        <a-table
          v-if="activeView !== 'trend' && currentView"
          :columns="currentView.columns"
          :data-source="currentView.data"
          :loading="currentView.loading"
          :pagination="currentView.pagination"
          :row-key="currentView.rowKey"
          size="small"
          :scroll="currentView.scroll"
          :expanded-row-keys="currentView.hasExpand ? expandedRegionKeys : undefined"
          @expand="currentView.hasExpand ? handleRegionExpand : undefined"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="(column as ProfitColumn).format === 'weight'">{{ (record[(column as ProfitColumn).dataIndex] ?? 0).toFixed(2) }}</template>
            <template v-else-if="(column as ProfitColumn).format === 'money'">¥{{ (record[(column as ProfitColumn).dataIndex] ?? 0).toFixed(2) }}</template>
            <template v-else-if="(column as ProfitColumn).format === 'profit'">
              <span :class="(record[(column as ProfitColumn).dataIndex] ?? 0) >= 0 ? 'val-positive' : 'val-negative'">¥{{ (record[(column as ProfitColumn).dataIndex] ?? 0).toFixed(2) }}</span>
            </template>
            <template v-else-if="(column as ProfitColumn).format === 'rate'">
              <span :class="profitRateClass(record[(column as ProfitColumn).dataIndex])">{{ formatRate(record[(column as ProfitColumn).dataIndex]) }}</span>
            </template>
            <template v-else-if="(column as ProfitColumn).format === 'tagClient'">
              <a-tag :color="record.clientType === 1 ? 'blue' : record.clientType === 2 ? 'green' : 'default'" size="small">
                {{ clientTypeLabel(record.clientType) }}
              </a-tag>
            </template>
            <template v-else-if="(column as ProfitColumn).format === 'tagIntermediary'">
              <a-tag size="small">{{ record.clientType === 2 ? '业务代理' : record.clientType === 5 ? '承包区' : '其他' }}</a-tag>
            </template>
          </template>

          <template v-if="currentView.hasSummary" #summary>
            <a-table-summary fixed>
              <a-table-summary-row class="summary-row">
                <a-table-summary-cell :index="0">合计</a-table-summary-cell>
                <a-table-summary-cell :index="1" />
                <a-table-summary-cell :index="2" align="right">{{ formatNum(clientTotals.waybillCount) }}</a-table-summary-cell>
                <a-table-summary-cell :index="3" align="right">{{ clientTotals.totalWeight.toFixed(2) }}</a-table-summary-cell>
                <a-table-summary-cell :index="4" align="right">¥{{ clientTotals.totalCharge.toFixed(2) }}</a-table-summary-cell>
                <a-table-summary-cell :index="5" align="right">¥{{ clientTotals.totalCost.toFixed(2) }}</a-table-summary-cell>
                <a-table-summary-cell :index="6" align="right">
                  <span :class="clientTotals.profit >= 0 ? 'val-positive' : 'val-negative'">¥{{ clientTotals.profit.toFixed(2) }}</span>
                </a-table-summary-cell>
                <a-table-summary-cell :index="7" align="right">
                  <span :class="profitRateClass(clientTotals.profitRate)">{{ formatRate(clientTotals.profitRate) }}</span>
                </a-table-summary-cell>
                <a-table-summary-cell :index="8" align="right">¥{{ clientTotals.avgPrice.toFixed(2) }}</a-table-summary-cell>
                <a-table-summary-cell :index="9" align="right">
                  <span :class="clientTotals.avgProfit >= 0 ? 'val-positive' : 'val-negative'">¥{{ clientTotals.avgProfit.toFixed(2) }}</span>
                </a-table-summary-cell>
              </a-table-summary-row>
            </a-table-summary>
          </template>

          <template v-if="currentView.hasExpand" #expandedRowRender="{ record }">
            <a-table
              :columns="provinceColumns"
              :data-source="provinceMap[record.region] || []"
              :loading="provinceLoadingMap[record.region]"
              :pagination="false"
              row-key="provinceId"
              size="small"
            >
              <template #bodyCell="{ column, record: pRow }">
                <template v-if="(column as ProfitColumn).format === 'weight'">{{ (pRow[(column as ProfitColumn).dataIndex] ?? 0).toFixed(2) }}</template>
                <template v-else-if="(column as ProfitColumn).format === 'money'">¥{{ (pRow[(column as ProfitColumn).dataIndex] ?? 0).toFixed(2) }}</template>
                <template v-else-if="(column as ProfitColumn).format === 'profit'">
                  <span :class="(pRow[(column as ProfitColumn).dataIndex] ?? 0) >= 0 ? 'val-positive' : 'val-negative'">¥{{ (pRow[(column as ProfitColumn).dataIndex] ?? 0).toFixed(2) }}</span>
                </template>
                <template v-else-if="(column as ProfitColumn).format === 'rate'">
                  <span :class="profitRateClass(pRow[(column as ProfitColumn).dataIndex])">{{ formatRate(pRow[(column as ProfitColumn).dataIndex]) }}</span>
                </template>
              </template>
            </a-table>
          </template>
        </a-table>

        <!-- 趋势图 -->
        <div v-if="activeView === 'trend'" class="trend-view">
          <div class="trend-toolbar">
            <a-radio-group v-model:value="trendGranularity" size="small" @change="fetchTrend">
              <a-radio-button value="day">日</a-radio-button>
              <a-radio-button value="week">周</a-radio-button>
              <a-radio-button value="month">月</a-radio-button>
            </a-radio-group>
          </div>
          <a-spin :spinning="trendLoading">
            <div ref="trendChartRef" style="width: 100%; height: 420px"></div>
          </a-spin>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { message } from 'ant-design-vue'
import * as echarts from 'echarts'
import dayjs, { type Dayjs } from 'dayjs'

// 利润分析表格列：携带 format 标记单元格渲染方式，dataIndex 固定为字符串字段名
interface ProfitColumn {
  format?: string
  dataIndex: string
}
import PageHeader from '@/components/PageHeader.vue'
import { downloadBlob } from '@/utils/download'
import {
  getProfitByClient,
  getProfitByShop,
  getProfitTrend,
  getProfitByIntermediary,
  getProfitBySalesman,
  getProfitByWeightSegment,
  getProfitByRegion,
  getProfitByProvince,
  getProfitFilterOptions,
  type ProfitByClientDto,
  type ProfitByShopDto,
  type ProfitTrendDto,
  type ProfitByIntermediaryDto,
  type ProfitBySalesmanDto,
  type ProfitByWeightSegmentDto,
  type ProfitByRegionDto,
  type ProfitByProvinceDto,
} from '@/api/express'

// 搜索
const searchForm = reactive({
  brandCode: undefined as string | undefined,
  clientId: undefined as string | undefined,
})
// 默认近 30 天：避免无日期条件时全表聚合，数据量增长后首屏变慢
const defaultDateRange = (): [Dayjs, Dayjs] => [dayjs().subtract(29, 'day').startOf('day'), dayjs()]
const dateRange = ref<[Dayjs, Dayjs] | undefined>(defaultDateRange())
const activeView = ref('client')

// 下拉选项
const brandOptions = ref<{ value: string; label: string }[]>([])
const clientOptions = ref<{ value: string; label: string }[]>([])
const defaultBrand = ref<string | undefined>()
const filterOption = (input: string, option: any) => option.label?.toLowerCase().includes(input.toLowerCase())

async function loadFilterOptions() {
  try {
    const opts = await getProfitFilterOptions()
    brandOptions.value = opts.brands || []
    clientOptions.value = opts.clients || []
    // 默认选择 ST-申通
    if (brandOptions.value.some(b => b.value === 'ST')) {
      defaultBrand.value = 'ST'
    }
    if (!searchForm.brandCode && defaultBrand.value) {
      searchForm.brandCode = defaultBrand.value
    }
  } catch { /* ignore */ }
}

function getQueryParams() {
  const params: any = { ...searchForm }
  if (dateRange.value) {
    params.dateFrom = dateRange.value[0].format('YYYY-MM-DD')
    params.dateTo = dateRange.value[1].format('YYYY-MM-DD')
  }
  return params
}

function formatNum(n: number | null | undefined, decimals = 0) {
  if (n == null) return '—'
  return n.toLocaleString('zh-CN', { minimumFractionDigits: decimals, maximumFractionDigits: decimals })
}

/** 毛利率显示：应收≤0 时后端返回 null，显示 — 而非误导性的 0.0% */
function formatRate(rate: number | null | undefined) {
  return rate == null ? '—' : `${rate.toFixed(1)}%`
}

function profitRateClass(rate: number | null | undefined) {
  if (rate == null) return ''
  if (rate < 0) return 'val-negative'
  if (rate < 10) return 'val-warning'
  if (rate < 20) return 'val-caution'
  return 'val-positive'
}

/** 数值列排序器；null 视为最小值沉底 */
const numSorter = (key: string) => (a: any, b: any) =>
  (Number(a?.[key] ?? Number.NEGATIVE_INFINITY)) - (Number(b?.[key] ?? Number.NEGATIVE_INFINITY))

function clientTypeLabel(type: number) {
  const labels: Record<number, string> = {
    1: '客户',
    2: '代理',
    3: '网点',
    4: '业务员',
    5: '承包区',
    6: '驿站',
  }
  return labels[type] ?? '其他'
}

// ==================== 业务对象视角 ====================
const clientLoading = ref(false)
const clientData = ref<ProfitByClientDto[]>([])
const clientColumns = [
  { title: '业务对象', dataIndex: 'clientName', width: 160, ellipsis: true, fixed: 'left' as const },
  { title: '类型', dataIndex: 'clientType', width: 70, align: 'center' as const, format: 'tagClient' },
  { title: '运单数', dataIndex: 'waybillCount', width: 80, align: 'right' as const, sorter: numSorter('waybillCount') },
  { title: '总重量(kg)', dataIndex: 'totalWeight', width: 100, align: 'right' as const, sorter: numSorter('totalWeight'), format: 'weight' },
  { title: '应收金额', dataIndex: 'totalCharge', width: 100, align: 'right' as const, sorter: numSorter('totalCharge'), format: 'money' },
  { title: '总成本', dataIndex: 'totalCost', width: 100, align: 'right' as const, sorter: numSorter('totalCost'), format: 'money' },
  { title: '毛利', dataIndex: 'profit', width: 100, align: 'right' as const, sorter: numSorter('profit'), defaultSortOrder: 'descend' as const, format: 'profit' },
  { title: '毛利率', dataIndex: 'profitRate', width: 80, align: 'right' as const, sorter: numSorter('profitRate'), format: 'rate' },
  { title: '单票均价', dataIndex: 'avgPrice', width: 90, align: 'right' as const, sorter: numSorter('avgPrice'), format: 'money' },
  { title: '单票毛利', dataIndex: 'avgProfit', width: 90, align: 'right' as const, sorter: numSorter('avgProfit'), format: 'profit' },
]
const clientSummary = computed(() => {
  const d = clientData.value
  const totalWaybills = d.reduce((s, r) => s + r.waybillCount, 0)
  const totalProfit = d.reduce((s, r) => s + r.profit, 0)
  const totalCharge = d.reduce((s, r) => s + r.totalCharge, 0)
  const totalCost = d.reduce((s, r) => s + r.totalCost, 0)
  return { totalWaybills, totalProfit, totalCharge, totalCost, avgProfitRate: totalCharge > 0 ? (totalProfit / totalCharge) * 100 : null }
})

// 亏损对象/零应收预警（解释"KPI 为负但首屏全绿"的勾稽差异）
const showLossOnly = ref(false)
const clientAlertStats = computed(() => {
  const loss = clientData.value.filter(r => r.profit < 0)
  return {
    lossCount: loss.length,
    lossProfit: loss.reduce((s, r) => s + r.profit, 0),
    zeroChargeCount: clientData.value.reduce((s, r) => s + (r.zeroChargeCount ?? 0), 0),
  }
})
const displayedClientData = computed(() =>
  showLossOnly.value ? clientData.value.filter(r => r.profit < 0) : clientData.value
)
// 合计行跟随当前过滤结果
const clientTotals = computed(() => {
  const d = displayedClientData.value
  const waybillCount = d.reduce((s, r) => s + r.waybillCount, 0)
  const totalWeight = d.reduce((s, r) => s + r.totalWeight, 0)
  const totalCharge = d.reduce((s, r) => s + r.totalCharge, 0)
  const totalCost = d.reduce((s, r) => s + r.totalCost, 0)
  const profit = totalCharge - totalCost
  return {
    waybillCount,
    totalWeight,
    totalCharge,
    totalCost,
    profit,
    profitRate: totalCharge > 0 ? (profit / totalCharge) * 100 : null,
    avgPrice: waybillCount > 0 ? totalCharge / waybillCount : 0,
    avgProfit: waybillCount > 0 ? profit / waybillCount : 0,
  }
})

async function fetchClient() {
  clientLoading.value = true
  try { clientData.value = await getProfitByClient(getQueryParams()) }
  catch { message.error('获取业务对象毛利失败') }
  finally { clientLoading.value = false }
}

// ==================== 店铺视角 ====================
const shopLoading = ref(false)
const shopData = ref<ProfitByShopDto[]>([])
// 注：行 = Role1 计费结果按报价编号聚合，"报价编号"缺失时回退显示客户名
const shopColumns = [
  { title: '报价编号', dataIndex: 'shopName', width: 160, ellipsis: true, fixed: 'left' as const },
  { title: '归属客户', dataIndex: 'clientName', width: 120, ellipsis: true },
  { title: '运单数', dataIndex: 'waybillCount', width: 80, align: 'right' as const, sorter: numSorter('waybillCount') },
  { title: '总重量(kg)', dataIndex: 'totalWeight', width: 100, align: 'right' as const, sorter: numSorter('totalWeight'), format: 'weight' },
  { title: '应收', dataIndex: 'totalCharge', width: 100, align: 'right' as const, sorter: numSorter('totalCharge'), format: 'money' },
  { title: '成本', dataIndex: 'totalCost', width: 100, align: 'right' as const, sorter: numSorter('totalCost'), format: 'money' },
  { title: '毛利', dataIndex: 'profit', width: 100, align: 'right' as const, sorter: numSorter('profit'), defaultSortOrder: 'descend' as const, format: 'profit' },
  { title: '毛利率', dataIndex: 'profitRate', width: 80, align: 'right' as const, sorter: numSorter('profitRate'), format: 'rate' },
]
// 报价编号可能跨客户重复或为空（回退客户名），组合 key 避免行渲染错乱
const shopRowKey = (record: ProfitByShopDto) => `${record.clientName}|${record.shopName}`
const shopSummary = computed(() => {
  const d = shopData.value
  const totalWaybills = d.reduce((s, r) => s + r.waybillCount, 0)
  const totalProfit = d.reduce((s, r) => s + r.profit, 0)
  const totalCharge = d.reduce((s, r) => s + r.totalCharge, 0)
  const totalCost = d.reduce((s, r) => s + r.totalCost, 0)
  return { totalWaybills, totalProfit, totalCharge, totalCost, avgProfitRate: totalCharge > 0 ? (totalProfit / totalCharge) * 100 : null }
})

async function fetchShop() {
  shopLoading.value = true
  try { shopData.value = await getProfitByShop(getQueryParams()) }
  catch { message.error('获取店铺毛利失败') }
  finally { shopLoading.value = false }
}

// ==================== 中间人视角 ====================
const intermediaryLoading = ref(false)
const intermediaryData = ref<ProfitByIntermediaryDto[]>([])
const intermediaryColumns = [
  { title: '中间人', dataIndex: 'clientName', width: 130, ellipsis: true, fixed: 'left' as const },
  { title: '角色', dataIndex: 'clientType', width: 70, align: 'center' as const, format: 'tagIntermediary' },
  { title: '层级', dataIndex: 'chainLevel', width: 60, align: 'center' as const },
  { title: '运单数', dataIndex: 'waybillCount', width: 80, align: 'right' as const, sorter: numSorter('waybillCount') },
  { title: '总重量(kg)', dataIndex: 'totalWeight', width: 100, align: 'right' as const, sorter: numSorter('totalWeight'), format: 'weight' },
  { title: '向下收入', dataIndex: 'downstreamRevenue', width: 100, align: 'right' as const, sorter: numSorter('downstreamRevenue'), format: 'money' },
  { title: '向上成本', dataIndex: 'upstreamCost', width: 100, align: 'right' as const, sorter: numSorter('upstreamCost'), format: 'money' },
  { title: '毛利', dataIndex: 'profit', width: 100, align: 'right' as const, sorter: numSorter('profit'), defaultSortOrder: 'descend' as const, format: 'profit' },
  { title: '毛利率', dataIndex: 'profitRate', width: 80, align: 'right' as const, sorter: numSorter('profitRate'), format: 'rate' },
  { title: '单票均利', dataIndex: 'avgProfit', width: 90, align: 'right' as const, sorter: numSorter('avgProfit'), format: 'profit' },
]
const intermediarySummary = computed(() => {
  const d = intermediaryData.value
  // 一单可能经多层中间人，跨行求和会重复，KPI 标签随视图改为"计费记录数"
  const totalWaybills = d.reduce((s, r) => s + r.waybillCount, 0)
  const totalProfit = d.reduce((s, r) => s + r.profit, 0)
  const totalRevenue = d.reduce((s, r) => s + r.downstreamRevenue, 0)
  const totalCost = d.reduce((s, r) => s + r.upstreamCost, 0)
  return { totalWaybills, totalProfit, totalCharge: totalRevenue, totalCost, avgProfitRate: totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : null }
})

async function fetchIntermediary() {
  intermediaryLoading.value = true
  try { intermediaryData.value = await getProfitByIntermediary(getQueryParams()) }
  catch { message.error('获取中间人毛利失败') }
  finally { intermediaryLoading.value = false }
}

// ==================== 业务员视角 ====================
const salesmanLoading = ref(false)
const salesmanData = ref<ProfitBySalesmanDto[]>([])
const salesmanColumns = [
  { title: '业务员', dataIndex: 'salesmanName', width: 130, ellipsis: true, fixed: 'left' as const },
  { title: '网点', dataIndex: 'networkPointId', width: 80, align: 'center' as const },
  { title: '运单数', dataIndex: 'waybillCount', width: 80, align: 'right' as const, sorter: numSorter('waybillCount') },
  { title: '总重量(kg)', dataIndex: 'totalWeight', width: 100, align: 'right' as const, sorter: numSorter('totalWeight'), format: 'weight' },
  { title: '提成收入', dataIndex: 'commissionIncome', width: 100, align: 'right' as const, sorter: numSorter('commissionIncome'), defaultSortOrder: 'descend' as const, format: 'money' },
  { title: '单票均提成', dataIndex: 'avgCommission', width: 100, align: 'right' as const, sorter: numSorter('avgCommission'), format: 'money' },
]
const salesmanSummary = computed(() => {
  const d = salesmanData.value
  const totalWaybills = d.reduce((s, r) => s + r.waybillCount, 0)
  const totalCommission = d.reduce((s, r) => s + r.commissionIncome, 0)
  // 业务员视角是佣金口径，毛利率无意义，置 null 显示 —
  return { totalWaybills, totalProfit: totalCommission, totalCharge: totalCommission, totalCost: 0, avgProfitRate: null }
})

async function fetchSalesman() {
  salesmanLoading.value = true
  try { salesmanData.value = await getProfitBySalesman(getQueryParams()) }
  catch { message.error('获取业务员提成失败') }
  finally { salesmanLoading.value = false }
}

// ==================== 重量段视角 ====================
const weightSegmentLoading = ref(false)
const weightSegmentData = ref<ProfitByWeightSegmentDto[]>([])
const weightSegmentColumns = [
  { title: '重量段', dataIndex: 'weightSegment', width: 120, fixed: 'left' as const },
  { title: '运单数', dataIndex: 'waybillCount', width: 80, align: 'right' as const },
  { title: '总重量(kg)', dataIndex: 'totalWeight', width: 100, align: 'right' as const, format: 'weight' },
  { title: '应收', dataIndex: 'totalCharge', width: 100, align: 'right' as const, format: 'money' },
  { title: '成本', dataIndex: 'totalCost', width: 100, align: 'right' as const, format: 'money' },
  { title: '毛利', dataIndex: 'profit', width: 100, align: 'right' as const, format: 'profit' },
  { title: '毛利率', dataIndex: 'profitRate', width: 80, align: 'right' as const, format: 'rate' },
  { title: '单票毛利', dataIndex: 'avgProfit', width: 90, align: 'right' as const, format: 'profit' },
]
const weightSegmentSummary = computed(() => {
  const d = weightSegmentData.value
  const totalWaybills = d.reduce((s, r) => s + r.waybillCount, 0)
  const totalCharge = d.reduce((s, r) => s + r.totalCharge, 0)
  const totalCost = d.reduce((s, r) => s + r.totalCost, 0)
  const totalProfit = d.reduce((s, r) => s + r.profit, 0)
  return { totalWaybills, totalCharge, totalCost, totalProfit, avgProfitRate: totalCharge > 0 ? (totalProfit / totalCharge) * 100 : null }
})

async function fetchWeightSegment() {
  weightSegmentLoading.value = true
  try {
    const list = await getProfitByWeightSegment(getQueryParams())
    weightSegmentData.value = (list || []).slice().sort((a, b) => a.segmentOrder - b.segmentOrder)
  } catch { message.error('获取重量段毛利失败') }
  finally { weightSegmentLoading.value = false }
}

// ==================== 流量流向视角 ====================
const regionLoading = ref(false)
const regionData = ref<ProfitByRegionDto[]>([])
const expandedRegionKeys = ref<string[]>([])
const provinceMap = reactive<Record<string, ProfitByProvinceDto[]>>({})
const provinceLoadingMap = reactive<Record<string, boolean>>({})
const regionColumns = [
  { title: '区域', dataIndex: 'region', width: 120, fixed: 'left' as const },
  { title: '运单数', dataIndex: 'waybillCount', width: 80, align: 'right' as const },
  { title: '总重量(kg)', dataIndex: 'totalWeight', width: 100, align: 'right' as const, format: 'weight' },
  { title: '应收', dataIndex: 'totalCharge', width: 100, align: 'right' as const, format: 'money' },
  { title: '成本', dataIndex: 'totalCost', width: 100, align: 'right' as const, format: 'money' },
  { title: '毛利', dataIndex: 'profit', width: 100, align: 'right' as const, format: 'profit' },
  { title: '毛利率', dataIndex: 'profitRate', width: 80, align: 'right' as const, format: 'rate' },
  { title: '均重(kg)', dataIndex: 'avgWeight', width: 80, align: 'right' as const, format: 'weight' },
  { title: '单票毛利', dataIndex: 'avgProfit', width: 90, align: 'right' as const, format: 'profit' },
]
const provinceColumns = [
  { title: '省份', dataIndex: 'provinceName', width: 120 },
  { title: '运单数', dataIndex: 'waybillCount', width: 80, align: 'right' as const },
  { title: '总重量(kg)', dataIndex: 'totalWeight', width: 100, align: 'right' as const, format: 'weight' },
  { title: '应收', dataIndex: 'totalCharge', width: 100, align: 'right' as const, format: 'money' },
  { title: '成本', dataIndex: 'totalCost', width: 100, align: 'right' as const, format: 'money' },
  { title: '毛利', dataIndex: 'profit', width: 100, align: 'right' as const, format: 'profit' },
  { title: '毛利率', dataIndex: 'profitRate', width: 80, align: 'right' as const, format: 'rate' },
  { title: '单票毛利', dataIndex: 'avgProfit', width: 90, align: 'right' as const, format: 'profit' },
]
const regionSummary = computed(() => {
  const d = regionData.value
  const totalWaybills = d.reduce((s, r) => s + r.waybillCount, 0)
  const totalCharge = d.reduce((s, r) => s + r.totalCharge, 0)
  const totalCost = d.reduce((s, r) => s + r.totalCost, 0)
  const totalProfit = d.reduce((s, r) => s + r.profit, 0)
  return { totalWaybills, totalCharge, totalCost, totalProfit, avgProfitRate: totalCharge > 0 ? (totalProfit / totalCharge) * 100 : null }
})

async function fetchRegion() {
  regionLoading.value = true
  try {
    const list = await getProfitByRegion(getQueryParams())
    regionData.value = (list || []).slice().sort((a, b) => a.regionOrder - b.regionOrder)
    expandedRegionKeys.value = []
    Object.keys(provinceMap).forEach(k => delete provinceMap[k])
    Object.keys(provinceLoadingMap).forEach(k => delete provinceLoadingMap[k])
  } catch { message.error('获取区域毛利失败') }
  finally { regionLoading.value = false }
}

async function handleRegionExpand(expanded: boolean, record: ProfitByRegionDto) {
  if (expanded) {
    expandedRegionKeys.value = [...expandedRegionKeys.value, record.region]
    if (!provinceMap[record.region]) {
      provinceLoadingMap[record.region] = true
      try {
        provinceMap[record.region] = await getProfitByProvince({ ...getQueryParams(), region: record.region })
      } catch {
        message.error('获取省份明细失败')
        provinceMap[record.region] = []
      } finally {
        provinceLoadingMap[record.region] = false
      }
    }
  } else {
    expandedRegionKeys.value = expandedRegionKeys.value.filter(k => k !== record.region)
  }
}

// ==================== 毛利趋势 ====================
const trendGranularity = ref('day')
const trendLoading = ref(false)
const trendData = ref<ProfitTrendDto[]>([])
const trendChartRef = ref<HTMLElement>()
let trendChart: echarts.ECharts | null = null
let resizeObserver: ResizeObserver | null = null

async function fetchTrend() {
  trendLoading.value = true
  try {
    trendData.value = await getProfitTrend({ ...getQueryParams(), granularity: trendGranularity.value })
    nextTick(renderTrendChart)
  } catch {
    message.error('获取毛利趋势失败')
  } finally {
    trendLoading.value = false
  }
}

function renderTrendChart() {
  const el = trendChartRef.value
  if (!el) return
  // 趋势容器由 v-if 控制：切走再切回时 DOM 已重建，旧实例绑定在脱离文档的节点上会画成空白
  if (trendChart && trendChart.getDom() !== el) {
    trendChart.dispose()
    trendChart = null
  }
  if (!trendChart) {
    trendChart = echarts.init(el)
    resizeObserver?.disconnect()
    resizeObserver = new ResizeObserver(() => trendChart?.resize())
    resizeObserver.observe(el)
  }
  const dates = trendData.value.map(d => d.date?.slice(0, 10))
  trendChart.setOption({
    tooltip: { trigger: 'axis' },
    legend: { data: ['应收', '成本', '毛利', '毛利率'], bottom: 0 },
    grid: { left: '3%', right: '4%', bottom: '12%', top: '10%', containLabel: true },
    xAxis: { type: 'category', data: dates, axisLabel: { color: '#5A6068' } },
    yAxis: [
      { type: 'value', name: '金额(¥)', axisLabel: { color: '#8A9099' }, splitLine: { lineStyle: { type: 'dashed' } } },
      { type: 'value', name: '毛利率(%)', axisLabel: { color: '#8A9099', formatter: (v: number) => v.toFixed(0) + '%' }, splitLine: { show: false } },
    ],
    series: [
      { name: '应收', type: 'line', data: trendData.value.map(d => d.totalCharge), smooth: true, itemStyle: { color: '#5B7290' }, lineStyle: { width: 2 } },
      { name: '成本', type: 'line', data: trendData.value.map(d => d.totalCost), smooth: true, itemStyle: { color: '#D6584E' }, lineStyle: { width: 2 } },
      { name: '毛利', type: 'line', data: trendData.value.map(d => d.profit), smooth: true, itemStyle: { color: '#3E9E6E' }, lineStyle: { width: 2 }, areaStyle: { color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1, colorStops: [{ offset: 0, color: 'rgba(62,158,110,0.22)' }, { offset: 1, color: 'rgba(62,158,110,0.02)' }] } } },
      { name: '毛利率', type: 'bar', yAxisIndex: 1, data: trendData.value.map(d => d.profitRate), barWidth: 20, itemStyle: { color: '#D49A2E', borderRadius: [4, 4, 0, 0] } },
    ],
  }, true)
}

// ==================== KPI 统一汇总 ====================
// 趋势视角的 KPI 从趋势数据自身汇总（无运单数维度，显示 —）
const trendSummary = computed(() => {
  const d = trendData.value
  const totalCharge = d.reduce((s, r) => s + r.totalCharge, 0)
  const totalCost = d.reduce((s, r) => s + r.totalCost, 0)
  const totalProfit = totalCharge - totalCost
  return { totalWaybills: null, totalCharge, totalCost, totalProfit, avgProfitRate: totalCharge > 0 ? (totalProfit / totalCharge) * 100 : null }
})

const summaryMap: Record<string, any> = {
  client: clientSummary,
  shop: shopSummary,
  intermediary: intermediarySummary,
  salesman: salesmanSummary,
  weightSegment: weightSegmentSummary,
  region: regionSummary,
  trend: trendSummary,
}

const activeSummary = computed(() => summaryMap[activeView.value]?.value ?? { totalWaybills: 0, totalCharge: 0, totalCost: 0, totalProfit: 0, avgProfitRate: null })

// 中间人视角一单跨层级会产生多条计费记录，不能叫"运单总数"
const waybillKpiLabel = computed(() => (activeView.value === 'intermediary' ? '计费记录数' : '运单总数'))

const recordCountMap: Record<string, () => number> = {
  client: () => clientData.value.length,
  shop: () => shopData.value.length,
  intermediary: () => intermediaryData.value.length,
  salesman: () => salesmanData.value.length,
  weightSegment: () => weightSegmentData.value.length,
  region: () => regionData.value.length,
}
const activeRecordCount = computed(() => recordCountMap[activeView.value]?.() ?? 0)

// ==================== 视角配置（6 表合一驱动） ====================
const PAGE50 = { pageSize: 50, showSizeChanger: true, showQuickJumper: true, showTotal: (t: number) => `共 ${t} 条` }
const TBL_Y = 'calc(100vh - 280px)'

const VIEW_CONFIGS: Record<string, {
  columns: any[]; rowKey: any; pagination: any; scroll: Record<string, any>
  hasSummary?: boolean; hasExpand?: boolean
}> = {
  client:        { columns: clientColumns,        rowKey: 'clientId',      pagination: PAGE50, scroll: { x: 1100, y: TBL_Y }, hasSummary: true },
  shop:          { columns: shopColumns,          rowKey: shopRowKey,      pagination: PAGE50, scroll: { x: 1000, y: TBL_Y } },
  intermediary:  { columns: intermediaryColumns,  rowKey: 'clientId',      pagination: PAGE50, scroll: { x: 1100, y: TBL_Y } },
  salesman:      { columns: salesmanColumns,      rowKey: 'salesmanId',    pagination: PAGE50, scroll: { x: 800,  y: TBL_Y } },
  weightSegment: { columns: weightSegmentColumns, rowKey: 'weightSegment', pagination: false,  scroll: { x: 900,  y: TBL_Y } },
  region:        { columns: regionColumns,        rowKey: 'region',        pagination: false,  scroll: { x: 1000, y: TBL_Y }, hasExpand: true },
}
const dataMap: Record<string, any> = {
  client: displayedClientData, shop: shopData, intermediary: intermediaryData,
  salesman: salesmanData, weightSegment: weightSegmentData, region: regionData,
}
const loadingMap: Record<string, any> = {
  client: clientLoading, shop: shopLoading, intermediary: intermediaryLoading,
  salesman: salesmanLoading, weightSegment: weightSegmentLoading, region: regionLoading,
}
const currentView = computed(() => {
  const cfg = VIEW_CONFIGS[activeView.value]
  if (!cfg) return null
  return { ...cfg, data: dataMap[activeView.value].value, loading: loadingMap[activeView.value].value }
})

// ==================== 视图切换 & 搜索 ====================
const viewFetchMap: Record<string, () => void> = {
  client: fetchClient,
  shop: fetchShop,
  intermediary: fetchIntermediary,
  salesman: fetchSalesman,
  weightSegment: fetchWeightSegment,
  region: fetchRegion,
  trend: fetchTrend,
}

function handleViewChange() {
  viewFetchMap[activeView.value]?.()
}

function handleSearch() {
  viewFetchMap[activeView.value]?.()
}

function handleReset() {
  // 恢复进入页面时的默认条件（默认品牌 + 近 30 天），而不是清空后查全部
  searchForm.brandCode = defaultBrand.value
  searchForm.clientId = undefined
  dateRange.value = defaultDateRange()
  showLossOnly.value = false
  handleSearch()
}

// ==================== 导出 ====================
const trendExportColumns = [
  { title: '日期', dataIndex: 'date' },
  { title: '应收', dataIndex: 'totalCharge' },
  { title: '成本', dataIndex: 'totalCost' },
  { title: '毛利', dataIndex: 'profit' },
  { title: '毛利率(%)', dataIndex: 'profitRate' },
]

function csvEscape(value: unknown): string {
  if (value == null) return ''
  const s = String(value)
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s
}

function handleExport() {
  const configs: Record<string, { name: string; columns: { title: string; dataIndex: string }[]; rows: any[] }> = {
    client: { name: '业务对象', columns: clientColumns, rows: displayedClientData.value },
    shop: { name: '报价', columns: shopColumns, rows: shopData.value },
    intermediary: { name: '中间人', columns: intermediaryColumns, rows: intermediaryData.value },
    salesman: { name: '业务员', columns: salesmanColumns, rows: salesmanData.value },
    weightSegment: { name: '重量段', columns: weightSegmentColumns, rows: weightSegmentData.value },
    region: { name: '流向', columns: regionColumns, rows: regionData.value },
    trend: { name: '趋势', columns: trendExportColumns, rows: trendData.value },
  }
  const cfg = configs[activeView.value]
  if (!cfg || cfg.rows.length === 0) {
    message.warning('当前视图没有可导出的数据')
    return
  }
  const header = cfg.columns.map(c => c.title).join(',')
  const lines = cfg.rows.map(row =>
    cfg.columns
      .map(c => {
        const v = row[c.dataIndex]
        if (c.dataIndex === 'clientType') return csvEscape(clientTypeLabel(v))
        return csvEscape(v)
      })
      .join(','),
  )
  // BOM 保证 Excel 正确识别 UTF-8 中文
  const csv = '\ufeff' + [header, ...lines].join('\n')
  downloadBlob(new Blob([csv], { type: 'text/csv;charset=utf-8' }), `毛利分析-${cfg.name}-${dayjs().format('YYYYMMDD-HHmmss')}.csv`)
}

onMounted(async () => {
  await loadFilterOptions()
  await fetchClient()
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  trendChart?.dispose()
  trendChart = null
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.profit-content {
  padding: 0; // 去掉本层冗余留白——两侧/上下统一由 .page-container 提供，避免与其 16px 叠成 32px
  display: flex;
  flex-direction: column;
  height: calc(100vh - 48px); // 减去工具栏高度
}

/* KPI 指标条 */
.kpi-strip {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0;
  row-gap: 8px;
  padding: 12px 20px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 8px;
  margin-bottom: 12px;
  flex-shrink: 0;
}

.kpi-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0 24px;
  min-width: 100px;
}

.kpi-label {
  font-size: 12px;
  color: var(--text-3);
  line-height: 18px;
  white-space: nowrap;
}

.kpi-value {
  font-size: 20px;
  font-weight: 600;
  color: $text-primary;
  line-height: 28px;
  font-variant-numeric: tabular-nums;
}

.kpi-divider {
  width: 1px;
  height: 32px;
  background: var(--border);
  flex-shrink: 0;
}

.kpi-value-sm {
  font-size: 15px;
}

.kpi-clickable {
  cursor: pointer;
  border-radius: 6px;
  transition: background-color 0.2s;

  &:hover {
    background: var(--color-danger-light);
  }

  &.kpi-active {
    background: var(--color-danger-light);
    box-shadow: inset 0 0 0 1px var(--color-danger-border);
  }
}

.kpi-positive { color: var(--color-success); }
.kpi-negative { color: var(--color-danger); }
.val-positive { color: var(--color-success); }
.val-negative { color: var(--color-danger); }
/* 低正毛利用橙/黄，与亏损的红区分开 */
.val-warning { color: var(--color-warning); }
.val-caution { color: var(--color-warning-text); }

/* 内容区 */
.view-content {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

/* 趋势图 */
.trend-view {
  background: var(--bg-card);
  border-radius: 8px;
  padding: 16px;
}

.trend-toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 12px;
}

/* 表格优化 */
:deep(.ant-table) {
  .ant-table-thead > tr > th {
    font-size: 13px;
    padding: 8px 12px;
  }
  .ant-table-tbody > tr > td {
    font-size: 13px;
    padding: 6px 12px;
  }
  // 表头底色/字重/行 hover 交由全局 ant-override 中性样式接管(去本页旧灰底 + 旧蓝 hover)
  .ant-table-summary > tr > td,
  .ant-table-summary > tr > th {
    background: var(--bg-muted);
    font-weight: 600;
    font-size: 13px;
    padding: 8px 12px;
  }
}

/* 分页样式 */
:deep(.ant-pagination) {
  margin: 12px 0 0;
}
</style>
