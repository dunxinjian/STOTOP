<template>
  <div class="datacenter-dashboard">
    <PageHeader title="首页总览" />

    <!-- KPI 指标条 -->
    <div class="kpi-bar">
      <div class="kpi-item">
        <span class="kpi-dot" style="background: var(--color-info)"></span>
        <span class="kpi-label">今日批次</span>
        <span class="kpi-value">{{ todayBatchCount }}</span>
      </div>
      <div class="kpi-item">
        <span class="kpi-dot" style="background: var(--color-warning)"></span>
        <span class="kpi-label">待处理数</span>
        <span class="kpi-value">{{ totalPending }}</span>
      </div>
      <div class="kpi-item">
        <span class="kpi-dot" style="background: var(--color-success)"></span>
        <span class="kpi-label">成功率</span>
        <span class="kpi-value">{{ successRate }}%</span>
      </div>
      <div class="kpi-item">
        <span class="kpi-dot" style="background: var(--biz-waybill)"></span>
        <span class="kpi-label">存储空间</span>
        <span class="kpi-value">{{ storageUsed }}</span>
      </div>
    </div>

    <!-- Tab 内容区 -->
    <a-tabs v-model:activeKey="activeTab" class="dashboard-tabs" @change="onTabChange">
      <a-tab-pane key="overview" tab="导入概览">
        <div class="tab-content">
          <a-row :gutter="16" style="height: 100%">
            <a-col :span="14">
              <div class="content-panel">
                <div class="panel-title">导入趋势</div>
                <div class="chart-container" ref="trendChartRef"></div>
              </div>
            </a-col>
            <a-col :span="10">
              <div class="content-panel">
                <div class="panel-title">管道状态分布</div>
                <div class="chart-container" ref="pieChartRef"></div>
              </div>
            </a-col>
          </a-row>
        </div>
      </a-tab-pane>

      <a-tab-pane key="batches" tab="最近批次">
        <div class="tab-content">
          <div class="content-panel">
            <div class="panel-title">最近导入批次</div>
            <a-table
              :columns="batchColumns"
              :dataSource="recentBatches"
              rowKey="id"
              size="small"
              :pagination="false"
              :locale="{ emptyText: '暂无数据' }"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'status'">
                  <a-tag :color="statusTagColor(record.status)">
                    {{ statusLabel(record.status) }}
                  </a-tag>
                </template>
                <template v-else-if="column.dataIndex === 'importTime'">
                  {{ formatTime(record.importTime) }}
                </template>
              </template>
            </a-table>
          </div>
        </div>
      </a-tab-pane>

      <a-tab-pane key="actions" tab="快捷操作">
        <div class="tab-content">
          <div class="content-panel">
            <div class="panel-title">快捷操作</div>
            <div class="quick-actions-grid">
              <div class="quick-action-item" @click="router.push('/cardflow/upload-center')">
                <div class="quick-action-icon">
                  <UploadOutlined />
                </div>
                <span>上传文件</span>
              </div>
              <div class="quick-action-item" @click="router.push('/cardflow/staging')">
                <div class="quick-action-icon">
                  <DatabaseOutlined />
                </div>
                <span>查看暂存</span>
              </div>
              <div class="quick-action-item" @click="router.push('/cardflow/hangfire')">
                <div class="quick-action-icon">
                  <ClockCircleOutlined />
                </div>
                <span>任务调度</span>
              </div>
            </div>
          </div>
        </div>
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { UploadOutlined, DatabaseOutlined, ClockCircleOutlined } from '@ant-design/icons-vue'
import * as echarts from 'echarts/core'
import { BarChart, PieChart } from 'echarts/charts'
import { TitleComponent, TooltipComponent, LegendComponent, GridComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import PageHeader from '@/components/PageHeader.vue'
import {
  getDataCenterHomeStats,
  getStagingStats,
  getStorageStats,
  getImportBatches,
} from '@/api/cardflow'

echarts.use([BarChart, PieChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent, CanvasRenderer])

const router = useRouter()

// ========== 数据定义 ==========
const activeTab = ref('overview')
const todayBatchCount = ref(0)
const yesterdayBatchCount = ref(0)
const successRate = ref(0)
const storageUsed = ref('0 MB')
const storageTotal = ref('0 GB')
const storagePercent = ref(0)

interface StagingStat {
  targetTable: string
  total: number
  pending: number
  processed: number
  failed: number
}
const stagingStatsMap = ref<Record<string, StagingStat>>({
  'STG极兔总部交易明细': { targetTable: 'STG极兔总部交易明细', total: 0, pending: 0, processed: 0, failed: 0 },
  'STG申通总部交易明细': { targetTable: 'STG申通总部交易明细', total: 0, pending: 0, processed: 0, failed: 0 },
  'STG韵达总部交易明细': { targetTable: 'STG韵达总部交易明细', total: 0, pending: 0, processed: 0, failed: 0 },
  'STG申通派件日明细': { targetTable: 'STG申通派件日明细', total: 0, pending: 0, processed: 0, failed: 0 },
})

const totalPending = computed(() =>
  Object.values(stagingStatsMap.value).reduce((s, v) => s + v.pending, 0)
)

interface BatchRow {
  id: number
  fileName: string
  sourceType: string
  status: number
  rowCount: number
  importTime: string
}
const recentBatches = ref<BatchRow[]>([])

// ========== 批次表格列 ==========
const batchColumns = [
  { title: '批次号', dataIndex: 'id', key: 'id', width: 80 },
  { title: '文件名', dataIndex: 'fileName', key: 'fileName', ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90 },
  { title: '行数', dataIndex: 'rowCount', key: 'rowCount', width: 80, align: 'right' as const },
  { title: '导入时间', dataIndex: 'importTime', key: 'importTime', width: 160 },
]

// ========== 状态映射 ==========
const statusMap: Record<number, { label: string; color: string }> = {
  0: { label: '待处理', color: 'default' },
  1: { label: '处理中', color: 'processing' },
  2: { label: '已完成', color: 'success' },
  3: { label: '失败', color: 'error' },
}
const statusTagColor = (s: number) => statusMap[s]?.color ?? 'default'
const statusLabel = (s: number) => statusMap[s]?.label ?? '未知'

const formatTime = (t: string) => {
  if (!t) return '-'
  const d = new Date(t)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

// ========== ECharts 管理 ==========
const trendChartRef = ref<HTMLElement | null>(null)
const pieChartRef = ref<HTMLElement | null>(null)
let trendChart: echarts.ECharts | null = null
let pieChart: echarts.ECharts | null = null

// 导入趋势数据（近7天）
const dailyTrend = ref<Array<{ date: string; importCount: number; errorCount: number }>>([])

function initCharts() {
  if (trendChartRef.value && !trendChart) {
    trendChart = echarts.init(trendChartRef.value)
    updateTrendChart()
  }
  if (pieChartRef.value && !pieChart) {
    pieChart = echarts.init(pieChartRef.value)
    updatePieChart()
  }
}

function updateTrendChart() {
  if (!trendChart) return
  const dates = dailyTrend.value.map(d => d.date.slice(5)) // MM-DD
  const imports = dailyTrend.value.map(d => d.importCount)
  const errors = dailyTrend.value.map(d => d.errorCount)

  trendChart.setOption({
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
    legend: { bottom: 0, itemWidth: 10, itemHeight: 10, textStyle: { fontSize: 12, color: '#606266' } },
    grid: { top: 16, left: 48, right: 16, bottom: 40 },
    xAxis: {
      type: 'category',
      data: dates,
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#e8e8e8' } },
      axisLabel: { color: '#909399', fontSize: 12 },
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { type: 'dashed', color: '#f0f0f0' } },
      axisLabel: { color: '#909399', fontSize: 11 },
    },
    series: [
      { name: '导入量', type: 'bar', barWidth: 20, color: '#3A6FB0', data: imports },
      { name: '异常量', type: 'bar', barWidth: 20, color: '#E5484D', data: errors },
    ],
  })
}

function updatePieChart() {
  if (!pieChart) return
  const sourceColors = ['#5B7290', '#6BA292', '#C99A6B']
  const data = Object.entries(stagingStatsMap.value).map(([name, v], i) => ({
    name: name.replace('STG', '').replace('TC', ''),
    value: v.total,
    itemStyle: { color: sourceColors[i] },
  }))

  pieChart.setOption({
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: { bottom: 0, itemWidth: 10, itemHeight: 10, textStyle: { fontSize: 12, color: '#606266' } },
    series: [
      {
        type: 'pie',
        radius: ['38%', '65%'],
        center: ['50%', '44%'],
        avoidLabelOverlap: true,
        itemStyle: { borderRadius: 5, borderColor: '#fff', borderWidth: 2 },
        label: { show: false },
        emphasis: { label: { show: true, fontSize: 13, fontWeight: 'bold' } },
        data,
      },
    ],
  })
}

function resizeCharts() {
  trendChart?.resize()
  pieChart?.resize()
}

function onTabChange(key: string | number) {
  if (key === 'overview') {
    nextTick(() => {
      if (trendChart) {
        trendChart.resize()
      } else {
        initCharts()
      }
      pieChart?.resize()
    })
  }
}

// ========== resize 监听 ==========
let resizeObserver: ResizeObserver | null = null

onMounted(async () => {
  // 加载数据
  await loadData()

  // 初始化图表（默认 overview tab）
  await nextTick()
  initCharts()

  // 监听容器 resize
  resizeObserver = new ResizeObserver(() => {
    resizeCharts()
  })
  if (trendChartRef.value) resizeObserver.observe(trendChartRef.value)
  if (pieChartRef.value) resizeObserver.observe(pieChartRef.value)
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  trendChart?.dispose()
  pieChart?.dispose()
  trendChart = null
  pieChart = null
})

// ========== 数据加载 ==========
async function loadData() {
  try {
    const res = await getDataCenterHomeStats()
    const d = res.data ?? res
    todayBatchCount.value = d.todayBatchCount ?? 0
    yesterdayBatchCount.value = d.yesterdayBatchCount ?? 0
    successRate.value = d.successRate ?? 0
    // 从首页统计中提取趋势数据
    if (d.dailyTrend && Array.isArray(d.dailyTrend)) {
      dailyTrend.value = d.dailyTrend
    }
  } catch { /* 静默 */ }

  for (const targetTable of ['STG极兔总部交易明细', 'STG申通总部交易明细', 'STG韵达总部交易明细', 'STG申通派件日明细']) {
    try {
      const res = await getStagingStats(encodeURIComponent(targetTable))
      const d = res.data ?? res
      stagingStatsMap.value[targetTable] = {
        targetTable,
        total: d.total ?? d.totalCount ?? 0,
        pending: d.pending ?? d.pendingCount ?? 0,
        processed: d.processed ?? d.processedCount ?? 0,
        failed: d.failed ?? d.failedCount ?? 0,
      }
    } catch { /* 静默 */ }
  }

  try {
    const res = await getStorageStats()
    const d = res.data ?? res
    storageUsed.value = d.usedFormatted ?? formatBytes(d.usedBytes ?? 0)
    storageTotal.value = d.totalFormatted ?? formatBytes(d.totalBytes ?? 0)
    storagePercent.value = d.usedBytes && d.totalBytes ? Math.round((d.usedBytes / d.totalBytes) * 100) : 0
  } catch { /* 静默 */ }

  try {
    const res = await getImportBatches({ pageSize: 10, page: 1 })
    const d = res.data ?? res
    recentBatches.value = (d.items ?? d.list ?? d ?? []).slice(0, 10).map((b: any) => ({
      id: b.id,
      fileName: b.fileName ?? b.originalFileName ?? '-',
      sourceType: b.sourceType ?? '',
      status: b.status ?? 0,
      rowCount: b.rowCount ?? b.totalRows ?? 0,
      importTime: b.importTime ?? b.createdAt ?? b.createTime ?? '',
    }))
  } catch { /* 静默 */ }

  // 更新图表
  await nextTick()
  updateTrendChart()
  updatePieChart()
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]
}
</script>

<style lang="scss" scoped>
/* 外层容器 */
.datacenter-dashboard {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  padding: 0 16px 16px;
  background: var(--bg-page);
}

/* KPI 指标条 */
.kpi-bar {
  display: flex;
  align-items: center;
  gap: 32px;
  padding: 14px 20px;
  background: var(--bg-card);
  border-radius: 8px;
  margin-bottom: 16px;
  flex-shrink: 0;
  box-shadow: var(--shadow-sm);
}
.kpi-item {
  display: flex;
  align-items: baseline;
  gap: 6px;
}
.kpi-item + .kpi-item {
  padding-left: 32px;
  border-left: 1px solid var(--border);
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
  color: var(--text-3);
}
.kpi-value {
  font-size: 20px;
  font-weight: 600;
  color: var(--text-1);
}

/* Tab 区 */
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

/* 面板 */
.content-panel {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 8px;
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
  border-left: 3px solid var(--color-info);
}

/* 图表容器 */
.chart-container {
  height: calc(100% - 40px);
  min-height: 300px;
}

/* 快捷操作 */
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
  border-radius: 8px;
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
</style>
