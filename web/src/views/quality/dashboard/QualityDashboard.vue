<template>
  <div class="quality-dashboard">
    <PageHeader title="质量看板" />

    <a-spin :spinning="loading">
      <!-- KPI 指标条 -->
      <div class="kpi-bar">
        <div v-for="(kpi, idx) in kpiList" :key="idx" class="kpi-item">
          <span class="kpi-dot" :style="{ background: kpi.color }"></span>
          <span class="kpi-label">{{ kpi.label }}</span>
          <span class="kpi-value" :style="{ color: kpi.color }">{{ kpi.value }}</span>
        </div>
      </div>

      <!-- Tab 内容区 -->
      <a-tabs v-model:activeKey="activeTab" class="dashboard-tabs" destroyInactiveTabPane>
        <!-- Tab1: 趋势分析 -->
        <a-tab-pane key="trend" tab="趋势分析">
          <div class="tab-content">
            <a-row :gutter="16" style="height: 100%">
              <a-col :span="14">
                <div class="content-panel">
                  <div class="panel-title">异常趋势（近30天）</div>
                  <div ref="trendChartRef" class="chart-container"></div>
                </div>
              </a-col>
              <a-col :span="10">
                <div class="content-panel">
                  <div class="panel-title">来源分布</div>
                  <div ref="sourceChartRef" class="chart-container"></div>
                </div>
              </a-col>
            </a-row>
          </div>
        </a-tab-pane>

        <!-- Tab2: 处理排行 -->
        <a-tab-pane key="ranking" tab="处理排行">
          <div class="tab-content">
            <a-row :gutter="16" style="height: 100%">
              <a-col :span="12">
                <div class="content-panel">
                  <div class="panel-title">优先级分布</div>
                  <div ref="priorityChartRef" class="chart-container"></div>
                </div>
              </a-col>
              <a-col :span="12">
                <div class="content-panel">
                  <div class="panel-title">处理人排行</div>
                  <a-table
                    :columns="handlerColumns"
                    :data-source="handlerRanking"
                    :pagination="false"
                    row-key="handler"
                    size="small"
                    :scroll="{ y: 320 }"
                  >
                    <template #bodyCell="{ column, record, index }">
                      <template v-if="column.dataIndex === 'rank'">
                        <span class="rank-badge" :class="{ top: index < 3 }">{{ index + 1 }}</span>
                      </template>
                    </template>
                  </a-table>
                </div>
              </a-col>
            </a-row>
          </div>
        </a-tab-pane>

        <!-- Tab3: 异常详情 -->
        <a-tab-pane key="detail" tab="异常详情">
          <div class="tab-content">
            <div class="content-panel">
              <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 12px">
                <a-input-search
                  v-model:value="searchText"
                  placeholder="搜索异常编号/标题"
                  style="width: 260px"
                  allow-clear
                  @search="handleSearch"
                />
                <a-select
                  v-model:value="filterStatus"
                  placeholder="状态筛选"
                  style="width: 140px"
                  allow-clear
                  @change="handleSearch"
                >
                  <a-select-option :value="0">待处理</a-select-option>
                  <a-select-option :value="1">处理中</a-select-option>
                  <a-select-option :value="2">已超时</a-select-option>
                  <a-select-option :value="3">已关闭</a-select-option>
                </a-select>
                <a-select
                  v-model:value="filterPriority"
                  placeholder="优先级"
                  style="width: 120px"
                  allow-clear
                  @change="handleSearch"
                >
                  <a-select-option :value="1">紧急</a-select-option>
                  <a-select-option :value="2">高</a-select-option>
                  <a-select-option :value="3">中</a-select-option>
                  <a-select-option :value="4">低</a-select-option>
                </a-select>
              </div>
              <a-table
                :columns="columns"
                :data-source="filteredList"
                :pagination="pagination"
                row-key="id"
                size="small"
                @change="handleTableChange"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'exceptionNo'">
                    <a class="exception-link">{{ record.exceptionNo }}</a>
                  </template>
                  <template v-if="column.dataIndex === 'priorityName'">
                    <span class="priority-tag" :class="'priority-' + record.priority">
                      {{ record.priorityName }}
                    </span>
                  </template>
                  <template v-if="column.dataIndex === 'statusName'">
                    <span class="status-tag" :class="'status-' + record.status">
                      {{ record.statusName }}
                    </span>
                  </template>
                </template>
              </a-table>
            </div>
          </div>
        </a-tab-pane>
      </a-tabs>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch, nextTick, shallowRef } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import * as echarts from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, PieChart, BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import {
  getQualityDashboardStats,
  getRecentExceptions,
  getDashboardTrend,
  getTypeDist,
  getPriorityDist,
  getExceptions,
  getAnalysisSource,
  getAnalysisHandlerStats,
} from '@/api/quality'

echarts.use([CanvasRenderer, LineChart, PieChart, BarChart, GridComponent, TooltipComponent, LegendComponent])

const loading = ref(false)
const activeTab = ref('trend')

// --- KPI ---
const stats = ref({
  totalExceptions: 0,
  pendingCount: 0,
  processingCount: 0,
  overdueCount: 0,
  closedCount: 0,
  todayNewCount: 0,
  weekNewCount: 0,
  avgResolutionHours: 0,
  ruleCount: 0,
  knowledgeCount: 0,
  overdueRate: 0,
})

const kpiList = computed(() => [
  { label: '异常总数', value: stats.value.totalExceptions.toLocaleString(), color: 'var(--color-danger)' },
  { label: '待处理', value: stats.value.pendingCount.toLocaleString(), color: 'var(--color-warning)' },
  { label: '超时', value: stats.value.overdueCount.toLocaleString(), color: 'var(--color-danger)' },
  { label: '今日新增', value: stats.value.todayNewCount.toLocaleString(), color: 'var(--color-info)' },
  { label: '平均处理时长', value: stats.value.avgResolutionHours.toFixed(1) + 'h', color: 'var(--color-info)' },
  { label: '超时率', value: stats.value.overdueRate.toFixed(1) + '%', color: 'var(--color-danger)' },
])

// --- Chart refs ---
const trendChartRef = ref<HTMLElement | null>(null)
const sourceChartRef = ref<HTMLElement | null>(null)
const priorityChartRef = ref<HTMLElement | null>(null)

let trendChart: echarts.ECharts | null = null
let sourceChart: echarts.ECharts | null = null
let priorityChart: echarts.ECharts | null = null

// --- Data ---
const trendData = ref<{ date: string; count: number }[]>([])
const typeDistData = ref<{ name: string; value: number }[]>([])
const priorityDistData = ref<{ name: string; value: number }[]>([])
const sourceDistData = ref<{ name: string; value: number }[]>([])
const handlerRanking = ref<any[]>([])
const recentList = ref<any[]>([])

// --- Tab3 筛选/分页 ---
const searchText = ref('')
const filterStatus = ref<number | undefined>(undefined)
const filterPriority = ref<number | undefined>(undefined)
const pagination = ref({ current: 1, pageSize: 15, total: 0 })

const filteredList = computed(() => {
  let list = recentList.value
  if (searchText.value) {
    const kw = searchText.value.toLowerCase()
    list = list.filter(
      (r) =>
        (r.exceptionNo && r.exceptionNo.toLowerCase().includes(kw)) ||
        (r.title && r.title.toLowerCase().includes(kw))
    )
  }
  if (filterStatus.value !== undefined) {
    list = list.filter((r) => r.status === filterStatus.value)
  }
  if (filterPriority.value !== undefined) {
    list = list.filter((r) => r.priority === filterPriority.value)
  }
  pagination.value.total = list.length
  const start = (pagination.value.current - 1) * pagination.value.pageSize
  return list.slice(start, start + pagination.value.pageSize)
})

function handleSearch() {
  pagination.value.current = 1
}

function handleTableChange(pag: any) {
  pagination.value.current = pag.current
  pagination.value.pageSize = pag.pageSize
}

// --- 表格列 ---
const columns = [
  { title: '异常编号', dataIndex: 'exceptionNo', width: 140 },
  { title: '标题', dataIndex: 'title', ellipsis: true },
  { title: '类型', dataIndex: 'typeName', width: 100 },
  { title: '优先级', dataIndex: 'priorityName', width: 90 },
  { title: '状态', dataIndex: 'statusName', width: 90 },
  { title: '处理人', dataIndex: 'handlerName', width: 100 },
  { title: '创建时间', dataIndex: 'createdTime', width: 170 },
]

const handlerColumns = [
  { title: '排名', dataIndex: 'rank', width: 60 },
  { title: '处理人', dataIndex: 'handler', ellipsis: true },
  { title: '处理数', dataIndex: 'count', width: 80 },
  { title: '平均时长', dataIndex: 'avgHours', width: 100 },
]

// --- ResizeObserver ---
let resizeObserver: ResizeObserver | null = null

function setupResizeObserver() {
  resizeObserver = new ResizeObserver(() => {
    trendChart?.resize()
    sourceChart?.resize()
    priorityChart?.resize()
  })
  if (trendChartRef.value) resizeObserver.observe(trendChartRef.value)
  if (sourceChartRef.value) resizeObserver.observe(sourceChartRef.value)
  if (priorityChartRef.value) resizeObserver.observe(priorityChartRef.value)
}

// --- Chart init ---
function initTrendChart() {
  if (!trendChartRef.value) return
  trendChart = echarts.init(trendChartRef.value)
  trendChart.setOption({
    tooltip: { trigger: 'axis', backgroundColor: '#fff', borderColor: '#d9d9d9', textStyle: { color: 'rgba(0,0,0,0.88)' } },
    grid: { left: 40, right: 20, top: 20, bottom: 30 },
    xAxis: {
      type: 'category',
      data: trendData.value.map((d) => d.date),
      axisLine: { lineStyle: { color: '#f0f0f0' } },
      axisLabel: { formatter: (v: string) => v.slice(5), color: 'rgba(0,0,0,0.45)' },
    },
    yAxis: {
      type: 'value',
      minInterval: 1,
      splitLine: { lineStyle: { color: '#f5f5f5' } },
      axisLabel: { color: 'rgba(0,0,0,0.45)' },
    },
    series: [{
      type: 'line',
      data: trendData.value.map((d) => d.count),
      smooth: true,
      areaStyle: {
        color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1, colorStops: [{ offset: 0, color: 'rgba(58,111,176,0.25)' }, { offset: 1, color: 'rgba(58,111,176,0.02)' }] },
      },
      lineStyle: { color: '#3A6FB0', width: 2 },
      itemStyle: { color: '#3A6FB0' },
      symbol: 'circle',
      symbolSize: 6,
    }],
  })
}

function initSourceChart() {
  if (!sourceChartRef.value) return
  sourceChart = echarts.init(sourceChartRef.value)
  const colors = ['#5B7290', '#6BA292', '#C99A6B', '#9B8AB8', '#C77B6B', '#8FB07E']
  sourceChart.setOption({
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: { bottom: 0, textStyle: { color: 'rgba(0,0,0,0.65)' } },
    series: [{
      type: 'pie',
      radius: ['40%', '65%'],
      center: ['50%', '45%'],
      data: sourceDistData.value.length ? sourceDistData.value : typeDistData.value,
      label: { formatter: '{b}: {c}', color: 'rgba(0,0,0,0.88)' },
      emphasis: { itemStyle: { shadowBlur: 10, shadowOffsetX: 0, shadowColor: 'rgba(0,0,0,0.1)' } },
      itemStyle: { color: (params: any) => colors[params.dataIndex % colors.length] },
    }],
  })
}

function initPriorityChart() {
  if (!priorityChartRef.value) return
  priorityChart = echarts.init(priorityChartRef.value)
  const colors = ['#5B7290', '#6BA292', '#C99A6B', '#9B8AB8']
  priorityChart.setOption({
    tooltip: { trigger: 'axis' },
    grid: { left: 60, right: 20, top: 20, bottom: 40 },
    xAxis: {
      type: 'category',
      data: priorityDistData.value.map((d) => d.name),
      axisLine: { lineStyle: { color: '#f0f0f0' } },
      axisLabel: { color: 'rgba(0,0,0,0.65)' },
    },
    yAxis: {
      type: 'value',
      minInterval: 1,
      splitLine: { lineStyle: { color: '#f5f5f5' } },
      axisLabel: { color: 'rgba(0,0,0,0.45)' },
    },
    series: [{
      type: 'bar',
      data: priorityDistData.value.map((d, i) => ({ value: d.value, itemStyle: { color: colors[i % colors.length] } })),
      barWidth: 36,
      itemStyle: { borderRadius: [4, 4, 0, 0] },
    }],
  })
}

// --- Tab切换时渲染图表 ---
watch(activeTab, async (tab) => {
  await nextTick()
  if (tab === 'trend') {
    if (!trendChart) initTrendChart()
    else trendChart.resize()
    if (!sourceChart) initSourceChart()
    else sourceChart.resize()
  } else if (tab === 'ranking') {
    if (!priorityChart) initPriorityChart()
    else priorityChart.resize()
  }
})

// --- 数据获取 ---
async function fetchData() {
  loading.value = true
  try {
    const [statsRes, recentRes, trendRes, typeRes, priorityRes] = await Promise.all([
      getQualityDashboardStats(),
      getRecentExceptions({ count: 100 }),
      getDashboardTrend({ days: 30 }),
      getTypeDist(),
      getPriorityDist(),
    ])

    if (statsRes.data) {
      const d = statsRes.data
      stats.value = {
        totalExceptions: d.totalExceptions ?? 0,
        pendingCount: d.pendingCount ?? 0,
        processingCount: d.processingCount ?? 0,
        overdueCount: d.overdueCount ?? 0,
        closedCount: d.closedCount ?? 0,
        todayNewCount: d.todayNewCount ?? 0,
        weekNewCount: d.weekNewCount ?? 0,
        avgResolutionHours: d.avgResolutionHours ?? 0,
        ruleCount: d.ruleCount ?? 0,
        knowledgeCount: d.knowledgeCount ?? 0,
        overdueRate: d.overdueRate ?? 0,
      }
    }

    recentList.value = recentRes.data ?? []
    trendData.value = (trendRes.data ?? []).map((d: any) => ({ date: d.date, count: d.count }))
    typeDistData.value = (typeRes.data ?? []).map((d: any) => ({ name: d.name, value: d.value }))
    priorityDistData.value = (priorityRes.data ?? []).map((d: any) => ({ name: d.name, value: d.value }))

    // 尝试获取来源分布和处理人排行
    try {
      const sourceRes = await getAnalysisSource()
      sourceDistData.value = (sourceRes.data ?? []).map((d: any) => ({ name: d.name, value: d.value }))
    } catch { /* fallback to typeDistData */ }

    try {
      const handlerRes = await getAnalysisHandlerStats()
      handlerRanking.value = (handlerRes.data ?? []).map((d: any) => ({
        handler: d.handler || d.handlerName || d.name,
        count: d.count ?? d.total ?? 0,
        avgHours: d.avgHours != null ? d.avgHours.toFixed(1) + 'h' : '-',
      }))
    } catch { /* ignore */ }

    // 初始化默认 tab 图表
    await nextTick()
    initTrendChart()
    initSourceChart()
    setupResizeObserver()
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchData()
})

onBeforeUnmount(() => {
  trendChart?.dispose()
  sourceChart?.dispose()
  priorityChart?.dispose()
  resizeObserver?.disconnect()
})
</script>

<style scoped>
.quality-dashboard {
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
  background: #fff;
  border-radius: 8px;
  margin-bottom: 16px;
  flex-shrink: 0;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}
.kpi-item {
  display: flex;
  align-items: baseline;
  gap: 6px;
}
.kpi-item + .kpi-item {
  padding-left: 32px;
  border-left: 1px solid #d9d9d9;
}
.kpi-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
  flex-shrink: 0;
}
.kpi-label {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
}
.kpi-value {
  font-size: 20px;
  font-weight: 600;
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
.dashboard-tabs :deep(.ant-tabs-content) {
  flex: 1;
  min-height: 0;
}
.dashboard-tabs :deep(.ant-tabs-content-holder) {
  flex: 1;
  display: flex;
  min-height: 0;
}
.dashboard-tabs :deep(.ant-tabs-tabpane) {
  height: 100%;
}
.tab-content {
  height: 100%;
}

.content-panel {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 16px;
  height: 100%;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}
.panel-title {
  font-size: 15px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.85);
  margin-bottom: 12px;
  padding-left: 10px;
  border-left: 3px solid var(--color-info);
}

.chart-container {
  height: 100%;
  min-height: 300px;
}

/* 异常详情表格样式 */
.exception-link {
  color: var(--text-1);
  font-weight: 500;
  cursor: pointer;
}
.exception-link:hover {
  color: var(--color-primary);
  text-decoration: underline;
}

.priority-tag {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
}
.priority-1 {
  background: var(--color-danger-light);
  color: var(--color-danger);
}
.priority-2 {
  background: var(--color-warning-light);
  color: var(--color-warning);
}
.priority-3 {
  background: var(--color-info-light);
  color: var(--color-info);
}
.priority-4 {
  background: rgba(0, 0, 0, 0.06);
  color: rgba(0, 0, 0, 0.45);
}

.status-tag {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
}
.status-0 {
  background: rgba(0, 0, 0, 0.06);
  color: rgba(0, 0, 0, 0.45);
}
.status-1 {
  background: var(--color-info-light);
  color: var(--color-info);
}
.status-2 {
  background: var(--color-warning-light);
  color: var(--color-warning);
}
.status-3 {
  background: rgba(82, 196, 26, 0.1);
  color: var(--color-success);
}

.rank-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  font-size: 12px;
  font-weight: 600;
  background: #f0f0f0;
  color: rgba(0, 0, 0, 0.45);
}
.rank-badge.top {
  background: var(--color-info);
  color: #fff;
}
</style>
