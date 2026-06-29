<template>
  <div class="page-container">
    <PageHeader title="质量管理看板">
      <template #left>
        <a-select
          v-model:value="selectedOrgId"
          placeholder="全部组织"
          allowClear
          style="width: 160px"
          @change="onOrgChange"
        >
          <a-select-option
            v-for="org in orgStore.organizations"
            :key="org.orgId"
            :value="org.orgId"
          >
            {{ org.orgName }}
          </a-select-option>
        </a-select>
      </template>
      <template #actions>
        <a-segmented v-model:value="timeRange" :options="timeRangeOptions" size="small" @change="onTimeRangeChange" />
      </template>
    </PageHeader>

    <!-- KPI 卡片 -->
    <a-row :gutter="16" class="kpi-row">
      <a-col :span="6">
        <a-card class="stat-card" hoverable>
          <a-statistic
            title="待处理"
            :value="summary.pending"
            :value-style="{ color: 'var(--color-warning)' }"
          >
            <template #prefix><ClockCircleOutlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card class="stat-card" hoverable>
          <a-statistic
            title="处理中"
            :value="summary.processing"
            :value-style="{ color: 'var(--color-info)' }"
          >
            <template #prefix><SyncOutlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card class="stat-card" hoverable>
          <a-statistic
            title="已解决"
            :value="summary.resolved"
            :value-style="{ color: 'var(--color-success)' }"
          >
            <template #prefix><CheckCircleOutlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card class="stat-card" hoverable>
          <a-statistic
            title="超时预警"
            :value="summary.overdueWarning"
            :value-style="{ color: 'var(--color-danger)' }"
          >
            <template #prefix><WarningOutlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
    </a-row>

    <!-- 图表区 -->
    <a-row :gutter="16" class="chart-row">
      <a-col :md="12">
        <a-card title="按错误类型分布" :bordered="false" class="chart-card">
          <a-spin :spinning="loading">
            <VChart
              v-if="typePieOption && summary.byType.length > 0"
              :option="typePieOption"
              autoresize
              style="height: 320px"
            />
            <a-empty v-else description="暂无分类数据" style="padding: 60px 0" />
          </a-spin>
        </a-card>
      </a-col>
      <a-col :md="12">
        <a-card :bordered="false" class="chart-card">
          <template #title>
            处理时效趋势
          </template>
          <template #extra>
            <a-radio-group v-model:value="trendDays" size="small" @change="loadTrend">
              <a-radio-button :value="7">7天</a-radio-button>
              <a-radio-button :value="30">30天</a-radio-button>
            </a-radio-group>
          </template>
          <a-spin :spinning="trendLoading">
            <VChart
              v-if="trendChartOption && trendData.length > 0"
              :option="trendChartOption"
              autoresize
              style="height: 320px"
            />
            <a-empty v-else description="暂无趋势数据" style="padding: 60px 0" />
          </a-spin>
        </a-card>
      </a-col>
    </a-row>

    <!-- 工作量排行 -->
    <a-card title="处理人工作量排行" :bordered="false" class="table-card">
      <a-table
        :columns="workloadColumns"
        :data-source="workloadData"
        :loading="workloadLoading"
        :pagination="false"
        size="small"
        row-key="userId"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'rank'">
            <a-tag :color="index < 3 ? 'gold' : 'default'">{{ index + 1 }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'avgHours'">
            {{ record.avgHours.toFixed(1) }}h
          </template>
          <template v-if="column.dataIndex === 'overdueCount'">
            <span :style="{ color: record.overdueCount > 0 ? 'var(--color-danger)' : 'var(--color-success)', fontWeight: 500 }">
              {{ record.overdueCount }}
            </span>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 超时预警列表 -->
    <a-card title="超时预警列表" :bordered="false" class="table-card">
      <template #extra>
        <a-tag v-if="overdueData.length > 0" color="error">{{ overdueData.length }} 条</a-tag>
      </template>
      <a-table
        :columns="overdueColumns"
        :data-source="overdueData"
        :loading="overdueLoading"
        :pagination="overdueData.length > 10 ? { pageSize: 10, showTotal: (t: number) => `共 ${t} 条` } : false"
        size="small"
        row-key="id"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'createTime'">
            {{ formatDate(record.createTime) }}
          </template>
          <template v-if="column.dataIndex === 'overdueHours'">
            <span style="color: var(--color-danger); font-weight: 500">{{ record.overdueHours.toFixed(1) }}h</span>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a @click="goToDetail(record as DashboardOverdueItemDto)">查看</a>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  ClockCircleOutlined,
  SyncOutlined,
  CheckCircleOutlined,
  WarningOutlined,
} from '@ant-design/icons-vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, PieChart, BarChart } from 'echarts/charts'
import {
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent,
} from 'echarts/components'
import PageHeader from '@/components/PageHeader.vue'
import { useOrgContextStore } from '@/stores/orgContext'
import {
  getDashboardSummary,
  getDashboardTrend,
  getDashboardWorkload,
  getDashboardOverdue,
  type DashboardSummaryDto,
  type DashboardTrendItemDto,
  type DashboardWorkloadItemDto,
  type DashboardOverdueItemDto,
} from '@/api/qualityCenter'

use([CanvasRenderer, LineChart, PieChart, BarChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

const router = useRouter()
const orgStore = useOrgContextStore()

// ===== 筛选状态 =====
const selectedOrgId = ref<number | undefined>(undefined)
const timeRange = ref<string>('today')
const timeRangeOptions = [
  { label: '今日', value: 'today' },
  { label: '本周', value: 'week' },
  { label: '本月', value: 'month' },
]
const trendDays = ref(7)

// ===== 数据状态 =====
const loading = ref(false)
const trendLoading = ref(false)
const workloadLoading = ref(false)
const overdueLoading = ref(false)

const summary = ref<DashboardSummaryDto>({
  pending: 0,
  processing: 0,
  resolved: 0,
  overdueWarning: 0,
  byType: [],
})
const trendData = ref<DashboardTrendItemDto[]>([])
const workloadData = ref<DashboardWorkloadItemDto[]>([])
const overdueData = ref<DashboardOverdueItemDto[]>([])

// ===== 饼图配置 =====
const typeColors = ['#5B7290', '#6BA292', '#C99A6B', '#9B8AB8', '#C77B6B', '#8FB07E', '#7C9CB5', '#B0976A']
const typePieOption = computed(() => {
  if (!summary.value.byType.length) return null
  const data = summary.value.byType.map((item, i) => ({
    name: item.type,
    value: item.count,
    itemStyle: { color: typeColors[i % typeColors.length] },
  }))
  return {
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: { orient: 'vertical', left: 'left', top: 'middle' },
    series: [
      {
        type: 'pie',
        radius: ['40%', '70%'],
        avoidLabelOverlap: false,
        itemStyle: { borderRadius: 6, borderColor: '#fff', borderWidth: 2 },
        label: { show: false },
        emphasis: { label: { show: true, fontSize: 14, fontWeight: 'bold' } },
        data,
      },
    ],
  }
})

// ===== 折线图配置 =====
const trendChartOption = computed(() => {
  if (!trendData.value.length) return null
  const dates = trendData.value.map(d => d.date)
  const created = trendData.value.map(d => d.created)
  const resolved = trendData.value.map(d => d.resolved)
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['新建', '已解决'] },
    grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
    xAxis: { type: 'category', data: dates, boundaryGap: false },
    yAxis: { type: 'value', minInterval: 1 },
    series: [
      {
        name: '新建',
        type: 'line',
        data: created,
        smooth: true,
        itemStyle: { color: '#3A6FB0' },
        areaStyle: { color: 'rgba(58,111,176,0.1)' },
      },
      {
        name: '已解决',
        type: 'line',
        data: resolved,
        smooth: true,
        itemStyle: { color: '#2BA471' },
        areaStyle: { color: 'rgba(82,196,26,0.1)' },
      },
    ],
  }
})

// ===== 表格列 =====
const workloadColumns = [
  { title: '#', dataIndex: 'rank', width: 50 },
  { title: '姓名', dataIndex: 'userName', width: 120 },
  { title: '待处理', dataIndex: 'pending', width: 80, sorter: (a: any, b: any) => a.pending - b.pending },
  { title: '已处理', dataIndex: 'processed', width: 80, sorter: (a: any, b: any) => a.processed - b.processed },
  { title: '平均时效', dataIndex: 'avgHours', width: 100, sorter: (a: any, b: any) => a.avgHours - b.avgHours },
  { title: '超时数', dataIndex: 'overdueCount', width: 80, sorter: (a: any, b: any) => a.overdueCount - b.overdueCount },
]

const overdueColumns = [
  { title: '标题', dataIndex: 'title', ellipsis: true },
  { title: '类型', dataIndex: 'type', width: 120 },
  { title: '处理人', dataIndex: 'assigneeName', width: 100 },
  { title: '创建时间', dataIndex: 'createTime', width: 160 },
  { title: '超时时长', dataIndex: 'overdueHours', width: 100 },
  { title: '操作', dataIndex: 'action', width: 80 },
]

// ===== 数据加载 =====
async function loadSummary() {
  loading.value = true
  try {
    const res = await getDashboardSummary({
      orgId: selectedOrgId.value,
      range: timeRange.value,
    }) as any
    if (res) {
      summary.value = res
    }
  } catch (e) {
    console.error('[Dashboard] loadSummary error:', e)
  } finally {
    loading.value = false
  }
}

async function loadTrend() {
  trendLoading.value = true
  try {
    const res = await getDashboardTrend({
      orgId: selectedOrgId.value,
      days: trendDays.value,
    }) as any
    trendData.value = res || []
  } catch (e) {
    console.error('[Dashboard] loadTrend error:', e)
  } finally {
    trendLoading.value = false
  }
}

async function loadWorkload() {
  workloadLoading.value = true
  try {
    const res = await getDashboardWorkload({
      orgId: selectedOrgId.value,
    }) as any
    workloadData.value = res || []
  } catch (e) {
    console.error('[Dashboard] loadWorkload error:', e)
  } finally {
    workloadLoading.value = false
  }
}

async function loadOverdue() {
  overdueLoading.value = true
  try {
    const res = await getDashboardOverdue({
      orgId: selectedOrgId.value,
    }) as any
    overdueData.value = res || []
  } catch (e) {
    console.error('[Dashboard] loadOverdue error:', e)
  } finally {
    overdueLoading.value = false
  }
}

async function loadAll() {
  await Promise.allSettled([
    loadSummary(),
    loadTrend(),
    loadWorkload(),
    loadOverdue(),
  ])
}

function onOrgChange() {
  loadAll()
}

function onTimeRangeChange() {
  loadSummary()
}

function goToDetail(record: DashboardOverdueItemDto) {
  router.push(`/express/quality-center?tab=pending-shops`)
}

function formatDate(dateStr: string) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  return d.toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

onMounted(() => {
  loadAll()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.page-container {
  padding: 0;

  // Dashboard 是纵向滚动看板页面：
  // 1. 取消 flex:1 填满模式，让页面取自然高度
  // 2. 由父级 .content-scroll 提供页面级滚动（避免与 .content-scroll 的 overflow-y:auto 嵌套冲突）
  // 3. 解除全局 .page-container 内表格 flex 滚动链的所有 flex:1 + overflow:hidden
  flex: 0 0 auto !important;
  overflow: visible !important;
  min-height: 0 !important;

  :deep(> .ant-card),
  :deep(> .table-card) {
    flex: none !important;
    overflow: visible !important;
    min-height: auto !important;

    .ant-card-body {
      flex: none !important;
      overflow: visible !important;
      min-height: auto !important;
    }
  }

  :deep(.ant-table-wrapper),
  :deep(.ant-spin-nested-loading),
  :deep(.ant-spin-container),
  :deep(.ant-table-container),
  :deep(.ant-table-body) {
    flex: none !important;
    overflow: visible !important;
    min-height: auto !important;
  }
}

.kpi-row {
  padding: 16px 16px 0;
}

.stat-card {
  border-radius: $border-radius-lg;
  transition: $transition-base;

  &:hover {
    box-shadow: $shadow-card-hover;
  }
}

.chart-row {
  padding: 16px;
}

.chart-card {
  border-radius: $border-radius-lg;
}

.table-card {
  margin: 0 16px 16px;
  border-radius: $border-radius-lg;
}

@media (max-width: $breakpoint-md) {
  .kpi-row .ant-col {
    flex: 0 0 50%;
    max-width: 50%;
    margin-bottom: 12px;
  }

  .chart-row .ant-col {
    flex: 0 0 100%;
    max-width: 100%;
    margin-bottom: 12px;
  }
}

@media (max-width: $breakpoint-sm) {
  .kpi-row .ant-col {
    flex: 0 0 100%;
    max-width: 100%;
  }
}
</style>
