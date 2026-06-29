<template>
  <div ref="chartRef" :style="{ width: '100%', height: height + 'px' }"></div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import * as echarts from 'echarts'

const props = withDefaults(defineProps<{
  data: { itemName: string; currentValue: number; previousValue: number; changeAmount: number; changeRate: number }[]
  title?: string
  height?: number
  currentLabel?: string
  previousLabel?: string
}>(), {
  title: '同比/环比对比',
  height: 350,
  currentLabel: '本期',
  previousLabel: '上期'
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
  const names = props.data.map(d => d.itemName)
  chart.setOption({
    title: { text: props.title, left: 'center', textStyle: { fontSize: 14, color: '#303133' } },
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, valueFormatter: (v: number) => v?.toLocaleString('zh-CN', { minimumFractionDigits: 2 }) },
    legend: { bottom: 0, data: [props.currentLabel, props.previousLabel] },
    grid: { left: '3%', right: '4%', bottom: '12%', top: '15%', containLabel: true },
    xAxis: { type: 'category', data: names, axisLabel: { color: '#666', rotate: names.length > 4 ? 15 : 0 } },
    yAxis: { type: 'value', axisLabel: { formatter: (v: number) => formatAmount(v), color: '#999' }, splitLine: { lineStyle: { type: 'dashed' } } },
    series: [
      {
        name: props.currentLabel, type: 'bar', data: props.data.map(d => d.currentValue),
        itemStyle: { color: '#1890ff', borderRadius: [4, 4, 0, 0] }, barGap: '20%',
        label: {
          show: true, position: 'top', fontSize: 10, color: '#666',
          formatter: (p: any) => {
            const item = props.data[p.dataIndex]
            const rate = item?.changeRate
            return rate ? `${rate > 0 ? '+' : ''}${rate}%` : ''
          }
        }
      },
      {
        name: props.previousLabel, type: 'bar', data: props.data.map(d => d.previousValue),
        itemStyle: { color: '#91d5ff', borderRadius: [4, 4, 0, 0] }
      }
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
