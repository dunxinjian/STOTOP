<script setup lang="ts">
/**
 * TodoStatsPage.vue — 待办统计看板（管理员）
 *
 * 单行工具栏 + 4 张统计卡片 + 双图表（柱状/折线）+ 流程明细表
 */
import { ref, reactive, computed, onMounted, onBeforeUnmount, nextTick, watch, h } from 'vue'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { SearchOutlined } from '@ant-design/icons-vue'
import dayjs, { type Dayjs } from 'dayjs'
import * as echarts from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { BarChart, LineChart } from 'echarts/charts'
import {
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent,
} from 'echarts/components'
import PageHeader from '@/components/PageHeader.vue'
import { getTodoStats, getFlowDefinitions } from '@/api/cardflow'
import type { FlowTodoStat, TodoStatsDto, FlowDefinitionDto } from '@/types/cardflow'
import { useOrgContextStore } from '@/stores/orgContext'

echarts.use([CanvasRenderer, BarChart, LineChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

const orgStore = useOrgContextStore()

// ========== 工具栏状态 ==========
const dateRange = ref<[Dayjs, Dayjs]>([
  dayjs().subtract(30, 'day').startOf('day'),
  dayjs().endOf('day'),
])
const filters = reactive<{ flowId: number | undefined }>({ flowId: undefined })
const loading = ref(false)
const flowDefinitions = ref<FlowDefinitionDto[]>([])

// ========== 统计数据 ==========
const stats = reactive<TodoStatsDto>({
  totalPending: 0,
  avgProcessHours: 0,
  timeoutRate: 0,
  todayCompleted: 0,
  flowStats: [],
  trend: [],
})

const statCards = computed(() => [
  { key: 'total', label: '总待办数', value: stats.totalPending, suffix: '', color: 'var(--color-info)' },
  { key: 'avg', label: '平均处理时长', value: formatHours(stats.avgProcessHours), suffix: '', color: 'var(--color-success)' },
  { key: 'rate', label: '超时率', value: stats.timeoutRate, suffix: '%', color: 'var(--color-warning)' },
  { key: 'done', label: '今日完成数', value: stats.todayCompleted, suffix: '', color: 'var(--biz-waybill)' },
])

function formatHours(h: number) {
  if (!h || h <= 0) return '0h'
  if (h < 1) return `${Math.round(h * 60)}m`
  if (h < 24) return `${h.toFixed(1)}h`
  return `${(h / 24).toFixed(1)}d`
}

// ========== 数据加载 ==========
async function loadFlows() {
  try {
    const res = await getFlowDefinitions({ page: 1, pageSize: 200 })
    flowDefinitions.value = res?.items || []
  } catch {
    // 静默失败
  }
}

async function loadStats() {
  const orgId = orgStore.currentOrgId
  if (!orgId) {
    message.warning('请先选择组织')
    return
  }
  loading.value = true
  try {
    const data = await getTodoStats({
      orgId,
      flowId: filters.flowId ?? null,
      startDate: dateRange.value?.[0]?.format('YYYY-MM-DD'),
      endDate: dateRange.value?.[1]?.format('YYYY-MM-DD'),
    })
    Object.assign(stats, {
      totalPending: data?.totalPending ?? 0,
      avgProcessHours: data?.avgProcessHours ?? 0,
      timeoutRate: data?.timeoutRate ?? 0,
      todayCompleted: data?.todayCompleted ?? 0,
      flowStats: data?.flowStats ?? [],
      trend: data?.trend ?? [],
    })
    await nextTick()
    renderCharts()
  } catch {
    message.error('加载统计数据失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  loadStats()
}

// ========== 图表 ==========
const barChartRef = ref<HTMLElement>()
const lineChartRef = ref<HTMLElement>()
let barChart: echarts.ECharts | null = null
let lineChart: echarts.ECharts | null = null

function renderCharts() {
  renderBarChart()
  renderLineChart()
}

function renderBarChart() {
  if (!barChartRef.value) return
  if (!barChart) barChart = echarts.init(barChartRef.value)
  const flows = stats.flowStats
  if (flows.length === 0) {
    barChart.clear()
    barChart.setOption({
      title: { text: '暂无数据', left: 'center', top: 'middle', textStyle: { color: '#bfbfbf', fontSize: 13, fontWeight: 'normal' } },
    })
    return
  }
  barChart.setOption({
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
    grid: { left: 8, right: 16, top: 28, bottom: 8, containLabel: true },
    xAxis: {
      type: 'category',
      data: flows.map(f => f.flowName),
      axisLabel: { color: '#595959', fontSize: 12, interval: 0, rotate: flows.length > 5 ? 20 : 0 },
      axisLine: { lineStyle: { color: '#e8e8e8' } },
      axisTick: { show: false },
    },
    yAxis: {
      type: 'value',
      axisLabel: { color: '#8c8c8c', fontSize: 11 },
      splitLine: { lineStyle: { color: '#f0f0f0', type: 'dashed' } },
      axisLine: { show: false },
      axisTick: { show: false },
    },
    series: [{
      name: '待办数',
      type: 'bar',
      data: flows.map(f => f.pendingCount),
      barMaxWidth: 36,
      itemStyle: {
        borderRadius: [6, 6, 0, 0],
        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
          { offset: 0, color: '#3A6FB0' },
          { offset: 1, color: '#6B9BD1' },
        ]),
      },
      label: { show: true, position: 'top', color: '#595959', fontSize: 11 },
    }],
  }, true)
}

function renderLineChart() {
  if (!lineChartRef.value) return
  if (!lineChart) lineChart = echarts.init(lineChartRef.value)
  const trend = stats.trend
  if (trend.length === 0) {
    lineChart.clear()
    lineChart.setOption({
      title: { text: '暂无数据', left: 'center', top: 'middle', textStyle: { color: '#bfbfbf', fontSize: 13, fontWeight: 'normal' } },
    })
    return
  }
  lineChart.setOption({
    tooltip: { trigger: 'axis', valueFormatter: (v: any) => `${Number(v).toFixed(1)} h` },
    grid: { left: 8, right: 16, top: 28, bottom: 8, containLabel: true },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: trend.map(t => t.date.slice(5)),
      axisLabel: { color: '#595959', fontSize: 11 },
      axisLine: { lineStyle: { color: '#e8e8e8' } },
      axisTick: { show: false },
    },
    yAxis: {
      type: 'value',
      name: '小时',
      nameTextStyle: { color: '#bfbfbf', fontSize: 11 },
      axisLabel: { color: '#8c8c8c', fontSize: 11 },
      splitLine: { lineStyle: { color: '#f0f0f0', type: 'dashed' } },
      axisLine: { show: false },
      axisTick: { show: false },
    },
    series: [{
      name: '平均处理时长',
      type: 'line',
      smooth: true,
      data: trend.map(t => Number((t.avgProcessHours || 0).toFixed(2))),
      symbol: 'circle',
      symbolSize: 7,
      itemStyle: { color: '#2BA471' },
      lineStyle: { width: 2.5, color: '#2BA471' },
      areaStyle: {
        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
          { offset: 0, color: 'rgba(82,196,26,0.25)' },
          { offset: 1, color: 'rgba(82,196,26,0.02)' },
        ]),
      },
    }],
  }, true)
}

// ========== 明细表 ==========
const tableColumns: TableColumnsType<FlowTodoStat> = [
  { title: '流程名称', dataIndex: 'flowName', key: 'flowName', ellipsis: true },
  { title: '待办数', dataIndex: 'pendingCount', key: 'pendingCount', width: 110, align: 'right' },
  { title: '已完成', dataIndex: 'completedCount', key: 'completedCount', width: 110, align: 'right' },
  { title: '平均时长', dataIndex: 'avgProcessHours', key: 'avgProcessHours', width: 130, align: 'right',
    customRender: ({ text }) => formatHours(Number(text) || 0) },
  { title: '超时率', dataIndex: 'timeoutRate', key: 'timeoutRate', width: 130, align: 'right',
    customRender: ({ text }) => {
      const v = Number(text) || 0
      const danger = v > 20
      return h('span', {
        style: { color: danger ? 'var(--color-danger-text)' : 'var(--text-2)', fontWeight: danger ? 600 : 400 },
      }, `${v.toFixed(1)}%`)
    } },
]

// ========== resize ==========
let resizeObs: ResizeObserver | null = null
function handleResize() {
  barChart?.resize()
  lineChart?.resize()
}

onMounted(async () => {
  await loadFlows()
  await loadStats()
  resizeObs = new ResizeObserver(() => handleResize())
  if (barChartRef.value) resizeObs.observe(barChartRef.value)
  if (lineChartRef.value) resizeObs.observe(lineChartRef.value)
})

onBeforeUnmount(() => {
  resizeObs?.disconnect()
  barChart?.dispose()
  lineChart?.dispose()
  barChart = null
  lineChart = null
})

watch(() => orgStore.currentOrgId, () => loadStats())
</script>

<template>
  <div class="page-container stats-page">
    <PageHeader>
      <template #toolbar>
        <div class="filter-bar">
          <a-range-picker
            v-model:value="dateRange"
            :allow-clear="false"
            format="YYYY-MM-DD"
            style="width: 240px"
          />
          <a-select
            v-model:value="filters.flowId"
            placeholder="流程类型"
            allow-clear
            show-search
            :filter-option="(input: string, option: any) => String(option?.label ?? '').toLowerCase().includes(input.toLowerCase())"
            :options="flowDefinitions.map(f => ({ label: f.flowName, value: f.id }))"
            style="width: 200px"
          />
          <a-button type="primary" @click="handleSearch">
            <template #icon><SearchOutlined /></template>
            查询
          </a-button>
        </div>
      </template>
    </PageHeader>

    <div class="stats-body">
      <!-- 统计卡片 -->
      <div class="stat-grid">
        <div
          v-for="card in statCards"
          :key="card.key"
          class="stat-card"
          :style="{ '--stat-color': card.color }"
        >
          <span class="stat-accent" />
          <div class="stat-meta">
            <span class="stat-label">{{ card.label }}</span>
            <span class="stat-value">
              {{ card.value }}<span v-if="card.suffix" class="stat-suffix">{{ card.suffix }}</span>
            </span>
          </div>
        </div>
      </div>

      <!-- 图表行 -->
      <a-row :gutter="16" class="chart-row">
        <a-col :span="12">
          <div class="chart-card">
            <div class="chart-head">
              <span class="chart-title">按流程类型的待办数量</span>
              <span class="chart-sub">柱状图</span>
            </div>
            <div ref="barChartRef" class="chart-body"></div>
          </div>
        </a-col>
        <a-col :span="12">
          <div class="chart-card">
            <div class="chart-head">
              <span class="chart-title">处理时长趋势</span>
              <span class="chart-sub">按完成日</span>
            </div>
            <div ref="lineChartRef" class="chart-body"></div>
          </div>
        </a-col>
      </a-row>

      <!-- 明细表 -->
      <div class="detail-card">
        <div class="detail-head">
          <span class="detail-title">流程明细</span>
          <span class="detail-tip">超时率 &gt; 20% 红色高亮</span>
        </div>
        <a-table
          :columns="tableColumns"
          :data-source="stats.flowStats"
          :loading="loading"
          size="middle"
          row-key="flowName"
          :pagination="false"
          :scroll="{ y: 360 }"
        />
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
.page-container.stats-page {
  padding: 0;
}

.filter-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  width: 100%;
}

.stats-body {
  padding: 16px;
}

/* ===== 统计卡片 ===== */
.stat-grid {
  display: flex;
  gap: 16px;
  margin-bottom: 16px;
}

.stat-card {
  position: relative;
  flex: 1;
  height: 80px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 12px 16px 12px 22px;
  display: flex;
  align-items: center;
  transition: box-shadow 0.18s ease, border-color 0.18s ease, transform 0.18s ease;

  .stat-accent {
    position: absolute;
    left: 0;
    top: 12px;
    bottom: 12px;
    width: 3px;
    background: var(--stat-color);
    border-radius: 0 2px 2px 0;
    opacity: 0.45;
    transition: opacity 0.18s ease;
  }

  &:hover {
    border-color: var(--stat-color);
    box-shadow: var(--shadow-sm);
    transform: translateY(-1px);
    .stat-accent { opacity: 1; }
  }
}

.stat-meta {
  display: flex;
  flex-direction: column;
  gap: 4px;
  width: 100%;
}

.stat-label {
  font-size: 12px;
  color: var(--text-3);
  letter-spacing: 0.04em;
}

.stat-value {
  font-size: 28px;
  font-weight: 500;
  line-height: 1.1;
  color: var(--stat-color);
  font-variant-numeric: tabular-nums;
  font-feature-settings: 'tnum';
  letter-spacing: -0.02em;
}

.stat-suffix {
  font-size: 14px;
  margin-left: 2px;
  font-weight: 400;
  color: var(--stat-color);
  opacity: 0.85;
}

/* ===== 图表 ===== */
.chart-row {
  margin-bottom: 16px;
}

.chart-card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 16px 16px 8px;
  height: 320px;
  display: flex;
  flex-direction: column;
  transition: box-shadow 0.18s ease;

  &:hover {
    box-shadow: var(--shadow-sm);
  }
}

.chart-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  margin-bottom: 8px;
}

.chart-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-1);
}

.chart-sub {
  font-size: 12px;
  color: var(--text-3);
}

.chart-body {
  flex: 1;
  min-height: 0;
  width: 100%;
}

/* ===== 明细表 ===== */
.detail-card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 16px;
}

.detail-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  margin-bottom: 12px;
}

.detail-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-1);
}

.detail-tip {
  font-size: 12px;
  color: var(--text-3);
}

:deep(.ant-table) {
  background: var(--bg-card);
}
:deep(.ant-table-thead > tr > th) {
  background: var(--bg-muted);
  font-weight: 500;
  color: var(--text-2);
}
</style>
