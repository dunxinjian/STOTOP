<template>
  <div class="page-container exception-analysis">
    <PageHeader title="异常分析" />

    <!-- 筛选区 -->
    <div class="toolbar-section">
      <a-space>
        <a-range-picker v-model:value="dateRange" :allow-clear="false" @change="onDateChange" />
        <a-radio-group v-model:value="groupBy" button-style="solid" @change="onGroupByChange">
          <a-radio-button value="day">按天</a-radio-button>
          <a-radio-button value="week">按周</a-radio-button>
          <a-radio-button value="month">按月</a-radio-button>
        </a-radio-group>
        <a-button @click="fetchAllData">刷新</a-button>
      </a-space>
    </div>

    <a-spin :spinning="loading">
      <!-- 效率概览卡片 -->
      <a-row :gutter="16" class="stat-row">
        <a-col :span="6">
          <a-card :bordered="false" class="stat-card">
            <a-statistic title="平均处理时长" :value="efficiency.avgResolutionHours" suffix="h" :precision="1" />
          </a-card>
        </a-col>
        <a-col :span="6">
          <a-card :bordered="false" class="stat-card">
            <a-statistic title="总关闭数" :value="efficiency.totalClosed" :value-style="{ color: 'var(--color-success)' }" />
          </a-card>
        </a-col>
        <a-col :span="6">
          <a-card :bordered="false" class="stat-card stat-card--danger">
            <a-statistic title="总超时数" :value="efficiency.totalOverdue" :value-style="{ color: 'var(--color-danger)' }" />
          </a-card>
        </a-col>
        <a-col :span="6">
          <a-card :bordered="false" class="stat-card">
            <a-statistic title="超时率" :value="efficiency.overdueRate" suffix="%" :precision="1" :value-style="{ color: efficiency.overdueRate > 20 ? 'var(--color-danger)' : 'var(--color-success)' }" />
          </a-card>
        </a-col>
      </a-row>

      <!-- 图表区上半 -->
      <a-row :gutter="16" class="stat-row">
        <a-col :span="14">
          <a-card title="异常趋势" :bordered="false">
            <v-chart :option="trendOption" style="height: 360px" autoresize />
          </a-card>
        </a-col>
        <a-col :span="10">
          <a-card title="异常来源分布" :bordered="false">
            <v-chart :option="sourceOption" style="height: 360px" autoresize />
          </a-card>
        </a-col>
      </a-row>

      <!-- 图表区下半 -->
      <a-row :gutter="16" class="stat-row">
        <a-col :span="12">
          <a-card title="按类型处理效率" :bordered="false">
            <v-chart :option="byTypeOption" style="height: 320px" autoresize />
          </a-card>
        </a-col>
        <a-col :span="12">
          <a-card title="按优先级处理效率" :bordered="false">
            <v-chart :option="byPriorityOption" style="height: 320px" autoresize />
          </a-card>
        </a-col>
      </a-row>

      <!-- 处理人排名表格 -->
      <a-card title="处理人效率排名" :bordered="false" class="stat-row">
        <a-table :columns="handlerColumns" :data-source="handlerStats" :pagination="false" row-key="userId" size="small">
          <template #bodyCell="{ column, index, record }">
            <template v-if="column.dataIndex === 'rank'">{{ index + 1 }}</template>
            <template v-if="column.dataIndex === 'avgResolutionHours'">{{ record.avgResolutionHours?.toFixed(1) }}</template>
            <template v-if="column.dataIndex === 'overdueRate'">
              <span :style="{ color: record.overdueRate > 20 ? 'var(--color-danger)' : 'var(--color-success)' }">{{ record.overdueRate?.toFixed(1) }}%</span>
            </template>
          </template>
        </a-table>
      </a-card>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import dayjs, { type Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, PieChart, BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent, TitleComponent } from 'echarts/components'
import {
  getAnalysisTrend,
  getAnalysisEfficiency,
  getAnalysisSource,
  getAnalysisHandlerStats,
} from '@/api/quality'

use([CanvasRenderer, LineChart, PieChart, BarChart, GridComponent, TooltipComponent, LegendComponent, TitleComponent])

const loading = ref(false)
const groupBy = ref('day')
const dateRange = ref<[Dayjs, Dayjs]>([dayjs().subtract(30, 'day'), dayjs()])

// 数据
const trendData = ref<any[]>([])
const efficiency = ref({
  avgResolutionHours: 0,
  totalClosed: 0,
  totalOverdue: 0,
  overdueRate: 0,
  byType: [] as any[],
  byPriority: [] as any[],
})
const sourceData = ref<any[]>([])
const handlerStats = ref<any[]>([])

// 颜色
const COLORS = {
  data: '#1890ff',
  process: '#faad14',
  business: '#ff4d4f',
  total: '#722ed1',
}

function getParams() {
  const [start, end] = dateRange.value
  return {
    startDate: start.format('YYYY-MM-DD'),
    endDate: end.format('YYYY-MM-DD'),
  }
}

// 趋势折线图
const trendOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  legend: { data: ['总计', '数据异常', '流程异常', '业务异常'], bottom: 0 },
  grid: { left: 50, right: 20, top: 20, bottom: 40 },
  xAxis: {
    type: 'category',
    data: trendData.value.map((d: any) => d.period),
    axisLabel: { rotate: trendData.value.length > 15 ? 45 : 0, fontSize: 11 },
  },
  yAxis: { type: 'value', minInterval: 1 },
  series: [
    { name: '总计', type: 'line', data: trendData.value.map((d: any) => d.total), smooth: true, itemStyle: { color: COLORS.total } },
    { name: '数据异常', type: 'line', data: trendData.value.map((d: any) => d.dataException), smooth: true, itemStyle: { color: COLORS.data } },
    { name: '流程异常', type: 'line', data: trendData.value.map((d: any) => d.processException), smooth: true, itemStyle: { color: COLORS.process } },
    { name: '业务异常', type: 'line', data: trendData.value.map((d: any) => d.businessException), smooth: true, itemStyle: { color: COLORS.business } },
  ],
}))

// 来源分布饼图
const sourceOption = computed(() => ({
  tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
  legend: { bottom: 0 },
  series: [
    {
      type: 'pie',
      radius: ['40%', '65%'],
      center: ['50%', '45%'],
      data: sourceData.value.map((d: any) => ({ name: d.source, value: d.count })),
      label: { formatter: '{b}: {c}' },
      emphasis: { itemStyle: { shadowBlur: 10, shadowOffsetX: 0, shadowColor: 'rgba(0,0,0,0.2)' } },
    },
  ],
}))

// 按类型效率柱状图
const byTypeOption = computed(() => ({
  tooltip: {
    trigger: 'axis',
    formatter: (params: any) => {
      const p = params[0]
      const item = efficiency.value.byType[p.dataIndex]
      return `${p.name}<br/>平均时长: ${p.value?.toFixed(1)}h<br/>数量: ${item?.count ?? 0}`
    },
  },
  grid: { left: 50, right: 20, top: 20, bottom: 30 },
  xAxis: {
    type: 'category',
    data: efficiency.value.byType.map((d: any) => d.typeName),
  },
  yAxis: { type: 'value', name: '平均时长(h)' },
  series: [
    {
      type: 'bar',
      data: efficiency.value.byType.map((d: any) => d.avgHours),
      itemStyle: { color: '#3A6FB0', borderRadius: [4, 4, 0, 0] },
      label: { show: true, position: 'top', formatter: (p: any) => efficiency.value.byType[p.dataIndex]?.count ?? '' },
    },
  ],
}))

// 按优先级效率柱状图
const byPriorityOption = computed(() => ({
  tooltip: {
    trigger: 'axis',
    formatter: (params: any) => {
      const p = params[0]
      const item = efficiency.value.byPriority[p.dataIndex]
      return `${p.name}<br/>平均时长: ${p.value?.toFixed(1)}h<br/>数量: ${item?.count ?? 0}`
    },
  },
  grid: { left: 50, right: 20, top: 20, bottom: 30 },
  xAxis: {
    type: 'category',
    data: efficiency.value.byPriority.map((d: any) => d.priorityName),
  },
  yAxis: { type: 'value', name: '平均时长(h)' },
  series: [
    {
      type: 'bar',
      data: efficiency.value.byPriority.map((d: any) => d.avgHours),
      itemStyle: { color: '#E6A700', borderRadius: [4, 4, 0, 0] },
      label: { show: true, position: 'top', formatter: (p: any) => efficiency.value.byPriority[p.dataIndex]?.count ?? '' },
    },
  ],
}))

// 处理人排名表格列
const handlerColumns = [
  { title: '排名', dataIndex: 'rank', width: 70 },
  { title: '用户名', dataIndex: 'userName', width: 120 },
  { title: '处理数', dataIndex: 'handleCount', width: 90 },
  { title: '已关闭', dataIndex: 'closedCount', width: 90 },
  { title: '超时数', dataIndex: 'overdueCount', width: 90 },
  { title: '平均时长(h)', dataIndex: 'avgResolutionHours', width: 120 },
  { title: '超时率(%)', dataIndex: 'overdueRate', width: 110 },
]

async function fetchTrendData() {
  const params = { ...getParams(), groupBy: groupBy.value }
  const res = await getAnalysisTrend(params)
    trendData.value = res ?? []
}

async function fetchAllData() {
  loading.value = true
  try {
    const params = getParams()
    const [trendRes, effRes, sourceRes, handlerRes] = await Promise.all([
      getAnalysisTrend({ ...params, groupBy: groupBy.value }),
      getAnalysisEfficiency(params),
      getAnalysisSource(params),
      getAnalysisHandlerStats({ ...params, top: 10 }),
    ])
    trendData.value = trendRes.data ?? []
    if (effRes.data) {
      const d = effRes.data
      efficiency.value = {
        avgResolutionHours: d.avgResolutionHours ?? 0,
        totalClosed: d.totalClosed ?? 0,
        totalOverdue: d.totalOverdue ?? 0,
        overdueRate: d.overdueRate ?? 0,
        byType: d.byType ?? [],
        byPriority: d.byPriority ?? [],
      }
    }
    sourceData.value = sourceRes.data ?? []
    handlerStats.value = handlerRes.data ?? []
  } finally {
    loading.value = false
  }
}

function onDateChange() {
  fetchAllData()
}

async function onGroupByChange() {
  loading.value = true
  try {
    await fetchTrendData()
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchAllData()
})
</script>

<style scoped lang="scss">
.exception-analysis {
  // toolbar-section 的 min-height/display/align-items 已由全局 index.scss 提供
  // 此处仅补充非卡片场景的局部样式
  .toolbar-section {
    margin-bottom: 16px;
    padding: 8px 16px;
    background: #fff;
    border-radius: 0;
    box-shadow: 0 1px 4px rgba(0, 21, 41, 0.06);
    display: flex;
    align-items: center;
  }

  .stat-row {
    margin-bottom: 16px;
  }

  .stat-card {
    border-radius: 8px;
    box-shadow: 0 1px 4px rgba(0, 21, 41, 0.06);
    transition: box-shadow 0.3s ease;

    &:hover {
      box-shadow: 0 4px 12px rgba(0, 21, 41, 0.1);
    }

    &--danger {
      border-left: 3px solid var(--color-danger);
    }
  }
}
</style>
