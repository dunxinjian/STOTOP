<template>
  <div class="point-trend" ref="chartRef"></div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch } from 'vue'
import * as echarts from 'echarts'
import type { TrendItem } from '@/types/points'

const props = defineProps<{
  data: TrendItem[]
}>()

const chartRef = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

function renderChart() {
  if (!chartRef.value || !chart) return
  const periods = props.data.map(d => d.period)
  const awards = props.data.map(d => d.awardPoints)
  const deducts = props.data.map(d => d.deductPoints)
  const nets = props.data.map(d => d.netPoints)

  chart.setOption({
    tooltip: {
      trigger: 'axis',
    },
    legend: {
      data: ['奖分', '扣分', '净积分'],
      bottom: 0,
    },
    grid: {
      left: '3%',
      right: '4%',
      bottom: '14%',
      top: '8%',
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      data: periods,
      boundaryGap: false,
    },
    yAxis: {
      type: 'value',
    },
    series: [
      {
        name: '奖分',
        type: 'line',
        data: awards,
        smooth: true,
        itemStyle: { color: '#2BA471' },
        areaStyle: { color: 'rgba(82,196,26,0.1)' },
      },
      {
        name: '扣分',
        type: 'line',
        data: deducts,
        smooth: true,
        itemStyle: { color: '#E5484D' },
        areaStyle: { color: 'rgba(255,77,79,0.1)' },
      },
      {
        name: '净积分',
        type: 'line',
        data: nets,
        smooth: true,
        itemStyle: { color: '#3A6FB0' },
        lineStyle: { width: 3 },
      },
    ],
  })
}

onMounted(() => {
  if (chartRef.value) {
    chart = echarts.init(chartRef.value)
    renderChart()
    window.addEventListener('resize', handleResize)
  }
})

function handleResize() {
  chart?.resize()
}

watch(() => props.data, renderChart, { deep: true })

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize)
  chart?.dispose()
})
</script>

<style scoped lang="scss">
.point-trend {
  width: 100%;
  height: 320px;
}
</style>
