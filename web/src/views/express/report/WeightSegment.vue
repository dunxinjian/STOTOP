<template>
  <div class="page-container">
    <PageHeader title="重量段分析">
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

    <!-- 分布表格 + 饼图 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :span="14">
        <a-card title="重量段分布" :bordered="false">
          <a-table
            :columns="distColumns"
            :data-source="distData"
            :loading="distLoading"
            :pagination="false"
            row-key="segmentName"
            bordered
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'ratio'">
                {{ (record.ratio * 100).toFixed(1) }}%
              </template>
              <template v-if="column.dataIndex === 'totalWeight'">
                {{ record.totalWeight.toFixed(2) }}
              </template>
              <template v-if="column.dataIndex === 'totalCharge'">
                ¥{{ record.totalCharge.toFixed(2) }}
              </template>
              <template v-if="column.dataIndex === 'avgPrice'">
                ¥{{ record.avgPrice.toFixed(2) }}
              </template>
              <template v-if="column.dataIndex === 'avgPricePerKg'">
                ¥{{ record.avgPricePerKg.toFixed(2) }}
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>
      <a-col :span="10">
        <a-card title="重量段占比" :bordered="false">
          <div ref="pieChartRef" style="width: 100%; height: 360px"></div>
        </a-card>
      </a-col>
    </a-row>

    <!-- 均重趋势图 -->
    <a-card title="均重趋势" :bordered="false">
      <template #extra>
        <a-radio-group v-model:value="granularity" size="small" @change="fetchTrend">
          <a-radio-button value="day">日</a-radio-button>
          <a-radio-button value="week">周</a-radio-button>
          <a-radio-button value="month">月</a-radio-button>
        </a-radio-group>
      </template>
      <a-spin :spinning="trendLoading">
        <div ref="trendChartRef" style="width: 100%; height: 350px"></div>
      </a-spin>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { message } from 'ant-design-vue'
import * as echarts from 'echarts'
import type { Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import {
  getWeightDistribution,
  getWeightTrend,
  type WeightSegmentReportDto,
  type WeightTrendDto,
} from '@/api/express'

const colors = ['#5B7290', '#6BA292', '#C99A6B', '#9B8AB8', '#C77B6B', '#8FB07E', '#7C9CB5', '#B0976A']

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

// 分布数据
const distLoading = ref(false)
const distData = ref<WeightSegmentReportDto[]>([])

const distColumns = [
  { title: '重量段', dataIndex: 'segmentName', width: 100 },
  { title: '运单数', dataIndex: 'waybillCount', width: 90, align: 'right' as const },
  { title: '占比', dataIndex: 'ratio', width: 80, align: 'right' as const },
  { title: '总重量(kg)', dataIndex: 'totalWeight', width: 110, align: 'right' as const },
  { title: '总金额', dataIndex: 'totalCharge', width: 110, align: 'right' as const },
  { title: '单票均价', dataIndex: 'avgPrice', width: 100, align: 'right' as const },
  { title: '单公斤均价', dataIndex: 'avgPricePerKg', width: 110, align: 'right' as const },
]

// 饼图
const pieChartRef = ref<HTMLElement>()
let pieChart: echarts.ECharts | null = null

function renderPieChart() {
  if (!pieChartRef.value || !distData.value.length) return
  if (!pieChart) pieChart = echarts.init(pieChartRef.value)
  pieChart.setOption({
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: { orient: 'vertical', right: '5%', top: 'middle', textStyle: { fontSize: 12 } },
    series: [{
      type: 'pie',
      radius: ['35%', '60%'],
      center: ['40%', '50%'],
      avoidLabelOverlap: true,
      itemStyle: { borderRadius: 6, borderColor: '#fff', borderWidth: 2 },
      label: { show: true, formatter: '{b}\n{d}%', fontSize: 11 },
      data: distData.value.map((d, i) => ({
        value: d.waybillCount,
        name: d.segmentName,
        itemStyle: { color: colors[i % colors.length] },
      })),
    }],
  }, true)
}

async function fetchDistribution() {
  distLoading.value = true
  try {
    distData.value = await getWeightDistribution(getQueryParams())
    nextTick(renderPieChart)
  } catch {
    message.error('获取重量段分布失败')
  } finally {
    distLoading.value = false
  }
}

// 趋势图
const granularity = ref('day')
const trendLoading = ref(false)
const trendData = ref<WeightTrendDto[]>([])
const trendChartRef = ref<HTMLElement>()
let trendChart: echarts.ECharts | null = null

async function fetchTrend() {
  trendLoading.value = true
  try {
    trendData.value = await getWeightTrend({ ...getQueryParams(), granularity: granularity.value })
    nextTick(renderTrendChart)
  } catch {
    message.error('获取均重趋势失败')
  } finally {
    trendLoading.value = false
  }
}

function renderTrendChart() {
  if (!trendChartRef.value) return
  if (!trendChart) trendChart = echarts.init(trendChartRef.value)
  trendChart.setOption({
    tooltip: { trigger: 'axis', valueFormatter: (v: number) => v?.toFixed(2) + ' kg' },
    grid: { left: '3%', right: '4%', bottom: '10%', top: '10%', containLabel: true },
    xAxis: { type: 'category', data: trendData.value.map(d => d.date?.slice(0, 10)), axisLabel: { color: '#666' } },
    yAxis: { type: 'value', name: '均重(kg)', axisLabel: { color: '#999' }, splitLine: { lineStyle: { type: 'dashed' } } },
    series: [
      { name: '均重', type: 'line', data: trendData.value.map(d => d.avgWeight), smooth: true, itemStyle: { color: '#3A6FB0' }, lineStyle: { width: 2 }, areaStyle: { color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1, colorStops: [{ offset: 0, color: 'rgba(58,111,176,0.25)' }, { offset: 1, color: 'rgba(58,111,176,0.02)' }] } } },
    ],
  }, true)
}

// ResizeObserver
let resizeObservers: ResizeObserver[] = []

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
    if (pieChartRef.value) {
      const obs = new ResizeObserver(() => pieChart?.resize())
      obs.observe(pieChartRef.value)
      resizeObservers.push(obs)
    }
    if (trendChartRef.value) {
      const obs = new ResizeObserver(() => trendChart?.resize())
      obs.observe(trendChartRef.value)
      resizeObservers.push(obs)
    }
  })
})

onBeforeUnmount(() => {
  resizeObservers.forEach(o => o.disconnect())
  pieChart?.dispose()
  trendChart?.dispose()
  pieChart = null
  trendChart = null
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
