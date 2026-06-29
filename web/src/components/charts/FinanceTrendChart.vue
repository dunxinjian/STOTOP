<template>
  <div ref="chartRef" :style="{ width: '100%', height: height + 'px' }"></div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import * as echarts from 'echarts'

const props = withDefaults(defineProps<{
  data: { month: number; revenue: number; cost: number; expense: number; profit: number }[]
  title?: string
  height?: number
}>(), {
  title: '利润趋势',
  height: 350
})

const chartRef = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

function formatAmount(val: number) {
  if (Math.abs(val) >= 10000) return (val / 10000).toFixed(1) + '万'
  return val.toFixed(0)
}

function renderChart() {
  if (!chartRef.value || !props.data?.length) return
  if (!chart) chart = echarts.init(chartRef.value)
  const months = props.data.map(d => d.month + '月')
  chart.setOption({
    title: { text: props.title, left: 'center', textStyle: { fontSize: 14, color: '#303133' } },
    tooltip: { trigger: 'axis', valueFormatter: (v: number) => v?.toLocaleString('zh-CN', { minimumFractionDigits: 2 }) },
    legend: { bottom: 0, data: ['收入', '成本', '费用', '利润'] },
    grid: { left: '3%', right: '4%', bottom: '12%', top: '15%', containLabel: true },
    xAxis: { type: 'category', data: months, axisLabel: { color: '#666' } },
    yAxis: { type: 'value', axisLabel: { formatter: (v: number) => formatAmount(v), color: '#999' }, splitLine: { lineStyle: { type: 'dashed' } } },
    series: [
      { name: '收入', type: 'line', data: props.data.map(d => d.revenue), smooth: true, itemStyle: { color: '#1890ff' }, lineStyle: { width: 2 } },
      { name: '成本', type: 'line', data: props.data.map(d => d.cost), smooth: true, itemStyle: { color: '#f5222d' }, lineStyle: { width: 2 } },
      { name: '费用', type: 'line', data: props.data.map(d => d.expense), smooth: true, itemStyle: { color: '#E6A700' }, lineStyle: { width: 2 } },
      { name: '利润', type: 'line', data: props.data.map(d => d.profit), smooth: true, itemStyle: { color: '#2BA471' }, lineStyle: { width: 2 }, areaStyle: { color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1, colorStops: [{ offset: 0, color: 'rgba(43,164,113,0.25)' }, { offset: 1, color: 'rgba(43,164,113,0.02)' }] } } }
    ]
  })
}

const resizeObserver = ref<ResizeObserver | null>(null)

onMounted(() => {
  nextTick(() => {
    renderChart()
    if (chartRef.value) {
      resizeObserver.value = new ResizeObserver(() => chart?.resize())
      resizeObserver.value.observe(chartRef.value)
    }
  })
})

watch(() => props.data, () => nextTick(renderChart), { deep: true })

onBeforeUnmount(() => {
  resizeObserver.value?.disconnect()
  chart?.dispose()
  chart = null
})
</script>
