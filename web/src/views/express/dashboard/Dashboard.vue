<template>
  <div class="express-dashboard">
    <PageHeader title="业务看板">
      <template #left>
        <a-segmented v-model:value="dateRange" :options="dateOptions" size="small" />
        <a-select
          v-model:value="brandCode"
          placeholder="全部品牌"
          allowClear
          :options="brandOptions"
          style="width: 120px; margin-left: 8px;"
          @change="loadDashboard"
        />
      </template>
    </PageHeader>

    <!-- KPI 指标条 -->
    <div class="kpi-bar">
      <div class="kpi-item" v-for="kpi in kpiList" :key="kpi.key">
        <span class="kpi-dot" :style="{ background: kpi.color }"></span>
        <span class="kpi-label">{{ kpi.title }}</span>
        <span class="kpi-value" :style="{ color: kpi.color }">{{ kpi.prefix }}{{ formatNumber(kpi.value) }}{{ kpi.suffix }}</span>
        <span v-if="kpi.change != null" class="kpi-change" :class="kpi.change >= 0 ? 'up' : 'down'">
          {{ kpi.change >= 0 ? '↑' : '↓' }} {{ Math.abs(kpi.change).toFixed(1) }}%
        </span>
      </div>
    </div>

    <!-- Tab 内容区 -->
    <a-tabs v-model:activeKey="activeTab" class="dashboard-tabs">
      <a-tab-pane key="overview" tab="数据概览">
        <a-row :gutter="16" class="tab-content">
          <a-col :md="14">
            <div class="content-panel">
              <h4 class="panel-title">趋势分析</h4>
              <div ref="trendChartRef" class="chart-container"></div>
            </div>
          </a-col>
          <a-col :md="10">
            <div class="content-panel">
              <h4 class="panel-title">品牌分布</h4>
              <div ref="brandChartRef" class="chart-container"></div>
            </div>
          </a-col>
        </a-row>
      </a-tab-pane>

      <a-tab-pane key="clients" tab="客户排行">
        <a-row :gutter="16" class="tab-content">
          <a-col :md="16">
            <div class="content-panel">
              <h4 class="panel-title">TOP10 客户</h4>
              <a-table
                :columns="topClientColumns"
                :data-source="dashboard?.topClients ?? []"
                :pagination="false"
                size="small"
                row-key="clientId"
              />
            </div>
          </a-col>
          <a-col :md="8">
            <div class="content-panel">
              <h4 class="panel-title">快捷操作</h4>
              <div class="quick-actions-grid">
              <div
                v-for="action in quickActions"
                :key="action.key"
                class="quick-action-item"
                @click="router.push(action.route)"
              >
                <component :is="action.icon" class="quick-action-icon" />
                <span>{{ action.label }}</span>
              </div>
              </div>
            </div>
          </a-col>
        </a-row>
      </a-tab-pane>

      <a-tab-pane key="alerts" tab="待办与预警">
        <a-row :gutter="16" class="tab-content">
          <a-col :md="12">
            <div class="content-panel">
              <h4 class="panel-title">待处理事项</h4>
              <EmptyState v-if="todoItems.length === 0" size="small" title="暂无待办" />
              <a-list v-else size="small" :data-source="todoItems">
                <template #renderItem="{ item }">
                  <a-list-item>
                    <a-badge :color="item.color" :text="item.text" />
                    <template #actions>
                      <a @click="router.push(item.route)">处理</a>
                    </template>
                  </a-list-item>
                </template>
              </a-list>
            </div>
          </a-col>
          <a-col :md="12">
            <div class="content-panel">
              <h4 class="panel-title">异常预警</h4>
              <EmptyState v-if="(dashboard?.alerts?.length ?? 0) === 0" size="small" title="暂无预警" />
              <a-list v-else size="small" :data-source="dashboard?.alerts ?? []">
                <template #renderItem="{ item }">
                  <a-list-item>
                    <a-alert :message="`${item.message}（${item.count}条）`" type="warning" show-icon banner style="width: 100%" />
                  </a-list-item>
                </template>
              </a-list>
            </div>
          </a-col>
        </a-row>
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import * as echarts from 'echarts'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { getDashboard, type DashboardDto } from '@/api/express'
import {
  PlusOutlined,
  ImportOutlined,
  TeamOutlined,
  BarChartOutlined,
} from '@ant-design/icons-vue'

const router = useRouter()
const loading = ref(false)
const dashboard = ref<DashboardDto | null>(null)
const activeTab = ref('overview')
const dateRange = ref('current')
const brandCode = ref<string | undefined>(undefined)

// 日期选项
const dateOptions = [
  { label: '本月', value: 'current' },
  { label: '上月', value: 'last' },
  { label: '近30天', value: '30d' },
]

// 品牌选项
const brandOptions = [
  { label: '申通', value: 'STO' },
  { label: '韵达', value: 'YD' },
  { label: '极兔', value: 'JT' },
  { label: '圆通', value: 'YTO' },
]

// KPI 列表
const kpiList = computed(() => {
  const d = dashboard.value
  const profitRate = d && d.monthRevenue > 0
    ? (d.monthProfit / d.monthRevenue * 100)
    : 0
  return [
    { key: 'today', title: '今日运单', value: d?.todayWaybills ?? 0, prefix: '', suffix: '单', change: undefined, color: 'var(--color-info)' },
    { key: 'month', title: '本月运单', value: d?.monthWaybills ?? 0, prefix: '', suffix: '单', change: d?.monthWaybillsChange, color: 'var(--biz-waybill)' },
    { key: 'revenue', title: '本月收入', value: d?.monthRevenue ?? 0, prefix: '¥', suffix: '', change: d?.monthRevenueChange, color: 'var(--color-info)' },
    { key: 'cost', title: '本月成本', value: d?.monthCost ?? 0, prefix: '¥', suffix: '', change: d?.monthCostChange, color: 'var(--color-warning)' },
    { key: 'profit', title: '毛利率', value: profitRate, prefix: '', suffix: '%', change: d?.monthProfitChange, color: 'var(--color-success)' },
  ]
})

// 格式化数字（万元处理）
function formatNumber(value: number): string {
  if (Math.abs(value) >= 10000) {
    return (value / 10000).toFixed(1) + '万'
  }
  return value.toLocaleString('zh-CN', { maximumFractionDigits: 1 })
}

// TOP10 客户表格列
const topClientColumns = [
  { title: '排名', key: 'index', width: 60, customRender: ({ index }: any) => index + 1 },
  { title: '客户名称', dataIndex: 'clientName', key: 'clientName' },
  { title: '运单数', dataIndex: 'waybillCount', key: 'waybillCount', width: 100 },
  { title: '总费用', dataIndex: 'totalCharge', key: 'totalCharge', width: 120, customRender: ({ text }: any) => `¥${Number(text).toLocaleString()}` },
]

// 快捷操作
const quickActions = [
  { key: 'invoice', label: '新建账单', route: '/express/invoice', icon: PlusOutlined },
  { key: 'import', label: '导入运单', route: '/cardflow/home', icon: ImportOutlined },
  { key: 'client', label: '客户管理', route: '/crm/dashboard', icon: TeamOutlined },
  { key: 'report', label: '查看报表', route: '/express/report/profit', icon: BarChartOutlined },
]

// 待办事项
const todoItems = computed(() => {
  const alerts = dashboard.value?.alerts ?? []
  return alerts.map(a => ({
    text: `${a.message}（${a.count}条）`,
    color: a.type === 'billing_error' ? 'red' : a.type === 'pending_review' ? 'orange' : 'blue',
    route: '/express/billing',
  }))
})

// ECharts
const trendChartRef = ref<HTMLDivElement | null>(null)
const brandChartRef = ref<HTMLDivElement | null>(null)
let trendChart: echarts.ECharts | null = null
let brandChart: echarts.ECharts | null = null
let trendObserver: ResizeObserver | null = null
let brandObserver: ResizeObserver | null = null

function renderTrendChart() {
  if (!trendChartRef.value || !dashboard.value) return
  if (!trendChart) {
    trendChart = echarts.init(trendChartRef.value)
    trendObserver = new ResizeObserver(() => trendChart?.resize())
    trendObserver.observe(trendChartRef.value)
  }
  const data = dashboard.value.dailyTrend
  trendChart.setOption({
    tooltip: { trigger: 'axis' },
    legend: { data: ['运单数', '收入', '成本'], top: 0 },
    grid: { top: 36, bottom: 24, left: 48, right: 16 },
    xAxis: { type: 'category', data: data.map(d => d.date.slice(5)) },
    yAxis: [
      { type: 'value', name: '单量' },
      { type: 'value', name: '金额', position: 'right' },
    ],
    series: [
      { name: '运单数', type: 'bar', data: data.map(d => d.waybillCount), itemStyle: { color: '#3A6FB0' } },
      { name: '收入', type: 'line', yAxisIndex: 1, data: data.map(d => d.revenue), itemStyle: { color: '#2BA471' }, smooth: true },
      { name: '成本', type: 'line', yAxisIndex: 1, data: data.map(d => d.cost), itemStyle: { color: '#E5484D' }, smooth: true },
    ],
  })
}

function renderBrandChart() {
  if (!brandChartRef.value || !dashboard.value) return
  if (!brandChart) {
    brandChart = echarts.init(brandChartRef.value)
    brandObserver = new ResizeObserver(() => brandChart?.resize())
    brandObserver.observe(brandChartRef.value)
  }
  const data = dashboard.value.brandDistribution
  brandChart.setOption({
    tooltip: { trigger: 'item', formatter: '{b}: {c}单 ({d}%)' },
    legend: { bottom: 0 },
    series: [{
      type: 'pie',
      radius: ['40%', '70%'],
      center: ['50%', '45%'],
      data: data.map(d => ({ name: d.brandName, value: d.waybillCount })),
      emphasis: { itemStyle: { shadowBlur: 10, shadowOffsetX: 0, shadowColor: 'rgba(0, 0, 0, 0.5)' } },
    }],
  })
}

// 加载数据
async function loadDashboard() {
  loading.value = true
  try {
    dashboard.value = await getDashboard(brandCode.value || undefined)
    await nextTick()
    if (activeTab.value === 'overview') {
      renderTrendChart()
      renderBrandChart()
    }
  } catch (e) {
    console.error('Dashboard load failed:', e)
  } finally {
    loading.value = false
  }
}

// Tab 切换时处理图表
watch(activeTab, async (val) => {
  if (val === 'overview') {
    await nextTick()
    renderTrendChart()
    renderBrandChart()
    setTimeout(() => {
      trendChart?.resize()
      brandChart?.resize()
    }, 100)
  }
})

onMounted(() => {
  loadDashboard()
})

onBeforeUnmount(() => {
  trendObserver?.disconnect()
  brandObserver?.disconnect()
  trendChart?.dispose()
  brandChart?.dispose()
  trendChart = null
  brandChart = null
})
</script>

<style scoped>
.express-dashboard {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  padding: 0 16px 16px;
  background: var(--bg-page);
}

.kpi-bar {
  display: flex;
  align-items: center;
  gap: 32px;
  padding: 14px 20px;
  background: var(--bg-card);
  border-radius: var(--radius-lg);
  margin-bottom: 16px;
  flex-shrink: 0;
  box-shadow: var(--shadow-sm);
}

.kpi-item {
  display: flex;
  align-items: center;
  gap: 6px;
}

.kpi-item + .kpi-item {
  padding-left: 32px;
  border-left: 1px solid var(--border);
}

.kpi-label {
  font-size: var(--font-sm2);
  color: var(--text-3);
}

.kpi-value {
  font-size: 20px;
  font-weight: 600;
  color: var(--text-1);
}

.kpi-change {
  font-size: 12px;
}

.kpi-change.up {
  color: var(--color-success);
}

.kpi-change.down {
  color: var(--color-danger);
}

.dashboard-tabs {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.dashboard-tabs :deep(.ant-tabs-tab) {
  font-size: 15px;
  padding: 12px 16px;
}

.dashboard-tabs :deep(.ant-tabs-ink-bar) {
  height: 3px;
  border-radius: 2px;
}

.dashboard-tabs :deep(.ant-tabs-content-holder) {
  flex: 1;
  display: flex;
  min-height: 0;
}

.dashboard-tabs :deep(.ant-tabs-content) {
  flex: 1;
  min-height: 0;
}

.dashboard-tabs :deep(.ant-tabs-tabpane) {
  height: 100%;
}

.tab-content {
  height: 100%;
}

.chart-container {
  height: 100%;
  min-height: 300px;
}

.quick-actions-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}

.quick-action-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 20px 8px;
  border-radius: var(--radius-lg);
  cursor: pointer;
  transition: all 0.3s;
  border: 1px solid var(--border);
}

.quick-action-item:hover {
  background: var(--color-primary-light);
  border-color: var(--color-primary-border);
  box-shadow: 0 2px 8px var(--color-primary-border);
  transform: translateY(-2px);
}

.quick-action-icon {
  font-size: 24px;
  margin-bottom: 8px;
  color: var(--text-2);
  background: var(--bg-muted);
  width: 48px;
  height: 48px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.content-panel {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  padding: 16px;
  height: 100%;
  box-shadow: var(--shadow-sm);
}

.panel-title {
  font-size: 15px;
  font-weight: 500;
  color: var(--text-1);
  margin-bottom: 12px;
  padding-left: 10px;
  border-left: 3px solid var(--border);
}

.kpi-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
  flex-shrink: 0;
}
</style>
