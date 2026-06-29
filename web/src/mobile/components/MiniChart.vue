<template>
  <div class="mini-chart" :style="{ height }">
    <v-chart
      :option="option"
      :init-options="{ renderer: 'canvas' }"
      autoresize
      @click="$emit('chartClick', $event)"
    />
  </div>
</template>

<script setup lang="ts">
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, BarChart, PieChart } from 'echarts/charts'
import {
  TitleComponent,
  TooltipComponent,
  GridComponent,
  LegendComponent,
} from 'echarts/components'
import VChart from 'vue-echarts'
import type { EChartsOption } from 'echarts'

use([
  CanvasRenderer,
  LineChart,
  BarChart,
  PieChart,
  TitleComponent,
  TooltipComponent,
  GridComponent,
  LegendComponent,
])

defineOptions({ name: 'MiniChart' })

withDefaults(defineProps<{
  option: EChartsOption
  height?: string
}>(), {
  height: '200px',
})

defineEmits<{
  chartClick: [event: any]
}>()
</script>

<style scoped>
.mini-chart {
  width: 100%;
}
.mini-chart :deep(.vue-echarts) {
  width: 100% !important;
  height: 100% !important;
}
</style>
