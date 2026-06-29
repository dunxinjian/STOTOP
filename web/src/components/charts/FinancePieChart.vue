<template>
  <div ref="chartRef" :style="{ width: '100%', height: height + 'px' }"></div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import * as echarts from 'echarts'

const props = withDefaults(defineProps<{
  data: { name: string; value: number; percentage: number }[]
  title?: string
  height?: number
}>(), {
  title: '构成分析',
  height: 320
})

const chartRef = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

const colors = ['#5B7290', '#6BA292', '#C99A6B', '#9B8AB8', '#C77B6B', '#8FB07E', '#7C9CB5', '#B0976A']

function renderChart() {
  if (!chartRef.value || !props.data?.length) return
  if (!chart) chart = echarts.init(chartRef.value)
  chart.setOption({
    title: { text: props.title, left: 'center', textStyle: { fontSize: 14, color: '#303133' } },
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: { orient: 'vertical', right: '5%', top: 'middle', textStyle: { fontSize: 12 } },
    series: [{
      type: 'pie',
      radius: ['40%', '65%'],
      center: ['40%', '55%'],
      avoidLabelOverlap: true,
      itemStyle: { borderRadius: 6, borderColor: '#fff', borderWidth: 2 },
      label: { show: true, formatter: '{b}\n{d}%', fontSize: 11 },
      labelLine: { show: true },
      data: props.data.map((d, i) => ({
        value: d.value,
        name: d.name,
        itemStyle: { color: colors[i % colors.length] }
      }))
    }]
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
