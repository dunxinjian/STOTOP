<template>
  <div class="page-container">
    <PageHeader title="流量流向分析">
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-range-picker v-model:value="dateRange" size="small" style="width: 240px" />
          <a-input v-model:value="searchForm.brandCode" size="small" placeholder="品牌编码" allow-clear style="width: 120px" />
          <a-input-number v-model:value="searchForm.clientId" size="small" placeholder="客户ID" style="width: 120px" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <!-- 流量分布表格 -->
    <a-card title="流量分布" :bordered="false" style="margin-bottom: 16px">
      <a-table
        :columns="distColumns"
        :data-source="distData"
        :loading="distLoading"
        :pagination="false"
        row-key="province"
        bordered
        size="small"
        :scroll="{ x: 900 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'ratio'">
            <a-progress :percent="Number((record.ratio * 100).toFixed(1))" :stroke-width="8" size="small" style="width: 120px" />
          </template>
          <template v-if="column.dataIndex === 'totalWeight'">
            {{ record.totalWeight.toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'avgWeight'">
            {{ record.avgWeight.toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'totalCharge'">
            ¥{{ record.totalCharge.toFixed(2) }}
          </template>
          <template v-if="column.dataIndex === 'avgPrice'">
            ¥{{ record.avgPrice.toFixed(2) }}
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 流量趋势图 -->
    <a-card title="流量趋势" :bordered="false">
      <template #extra>
        <a-radio-group v-model:value="granularity" size="small" @change="fetchTrend">
          <a-radio-button value="day">日</a-radio-button>
          <a-radio-button value="week">周</a-radio-button>
          <a-radio-button value="month">月</a-radio-button>
        </a-radio-group>
      </template>
      <a-spin :spinning="trendLoading">
        <div ref="trendChartRef" style="width: 100%; height: 380px"></div>
      </a-spin>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import { message } from 'ant-design-vue'
import * as echarts from 'echarts'
import type { Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import {
  getFlowDistribution,
  getFlowTrend,
  type FlowAnalysisDto,
  type FlowTrendDto,
} from '@/api/express'

// 搜索
const searchForm = reactive({
  brandCode: undefined as string | undefined,
  clientId: undefined as number | undefined,
})
const dateRange = ref<[Dayjs, Dayjs] | undefined>(undefined)

function getQueryParams() {
  const params: any = { ...searchForm }
  if (dateRange.value) {
    params.dateFrom = dateRange.value[0].format('YYYY-MM-DD')
    params.dateTo = dateRange.value[1].format('YYYY-MM-DD')
  }
  return params
}

// 分布表格
const distLoading = ref(false)
const distData = ref<FlowAnalysisDto[]>([])

const distColumns = [
  { title: '省份', dataIndex: 'province', width: 100, sorter: (a: FlowAnalysisDto, b: FlowAnalysisDto) => a.province.localeCompare(b.province) },
  { title: '运单数', dataIndex: 'waybillCount', width: 100, align: 'right' as const, sorter: (a: FlowAnalysisDto, b: FlowAnalysisDto) => a.waybillCount - b.waybillCount },
  { title: '占比', dataIndex: 'ratio', width: 160 },
  { title: '总重量(kg)', dataIndex: 'totalWeight', width: 110, align: 'right' as const, sorter: (a: FlowAnalysisDto, b: FlowAnalysisDto) => a.totalWeight - b.totalWeight },
  { title: '平均重量(kg)', dataIndex: 'avgWeight', width: 120, align: 'right' as const },
  { title: '应收金额', dataIndex: 'totalCharge', width: 120, align: 'right' as const, sorter: (a: FlowAnalysisDto, b: FlowAnalysisDto) => a.totalCharge - b.totalCharge },
  { title: '单票均价', dataIndex: 'avgPrice', width: 100, align: 'right' as const },
]

async function fetchDistribution() {
  distLoading.value = true
  try {
    distData.value = await getFlowDistribution(getQueryParams())
  } catch {
    message.error('获取流量分布失败')
  } finally {
    distLoading.value = false
  }
}

// 趋势图
const granularity = ref('day')
const trendLoading = ref(false)
const trendData = ref<FlowTrendDto[]>([])
const trendChartRef = ref<HTMLElement>()
let trendChart: echarts.ECharts | null = null
let resizeObserver: ResizeObserver | null = null

async function fetchTrend() {
  trendLoading.value = true
  try {
    trendData.value = await getFlowTrend({ ...getQueryParams(), granularity: granularity.value })
    nextTick(renderTrendChart)
  } catch {
    message.error('获取流量趋势失败')
  } finally {
    trendLoading.value = false
  }
}

function renderTrendChart() {
  if (!trendChartRef.value) return
  if (!trendChart) trendChart = echarts.init(trendChartRef.value)
  const dates = trendData.value.map(d => d.date?.slice(0, 10))
  trendChart.setOption({
    tooltip: { trigger: 'axis' },
    legend: { data: ['运单数', '应收金额'], bottom: 0 },
    grid: { left: '3%', right: '4%', bottom: '12%', top: '10%', containLabel: true },
    xAxis: { type: 'category', data: dates, axisLabel: { color: '#666' } },
    yAxis: [
      { type: 'value', name: '运单数', axisLabel: { color: '#999' }, splitLine: { lineStyle: { type: 'dashed' } } },
      { type: 'value', name: '金额(¥)', axisLabel: { color: '#999' }, splitLine: { show: false } },
    ],
    series: [
      { name: '运单数', type: 'line', data: trendData.value.map(d => d.waybillCount), smooth: true, itemStyle: { color: '#3A6FB0' }, lineStyle: { width: 2 } },
      { name: '应收金额', type: 'line', yAxisIndex: 1, data: trendData.value.map(d => d.totalCharge), smooth: true, itemStyle: { color: '#2BA471' }, lineStyle: { width: 2 } },
    ],
  }, true)
}

function handleSearch() {
  fetchDistribution()
  fetchTrend()
}

function handleReset() {
  searchForm.brandCode = undefined
  searchForm.clientId = undefined
  dateRange.value = undefined
  handleSearch()
}

onMounted(() => {
  handleSearch()
  nextTick(() => {
    if (trendChartRef.value) {
      resizeObserver = new ResizeObserver(() => trendChart?.resize())
      resizeObserver.observe(trendChartRef.value)
    }
  })
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  trendChart?.dispose()
  trendChart = null
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
