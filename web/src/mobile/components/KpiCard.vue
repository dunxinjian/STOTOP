<template>
  <div class="kpi-card">
    <div class="kpi-label">{{ label }}</div>
    <div class="kpi-value">
      <span class="prefix" v-if="prefix">{{ prefix }}</span>
      <span class="number">{{ formattedValue }}</span>
      <span class="suffix" v-if="suffix">{{ suffix }}</span>
    </div>
    <div class="kpi-change" :class="changeClass">
      <span class="arrow">{{ change >= 0 ? '↑' : '↓' }}</span>
      <span class="percent">{{ Math.abs(change).toFixed(1) }}%</span>
      <span class="hint">同比</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

defineOptions({ name: 'KpiCard' })

const props = withDefaults(defineProps<{
  label: string
  value: number
  change: number
  prefix?: string
  suffix?: string
}>(), {
  prefix: '',
  suffix: '',
})

const formattedValue = computed(() => {
  if (props.value >= 10000) {
    return (props.value / 10000).toFixed(1) + '万'
  }
  return props.value.toLocaleString()
})

const changeClass = computed(() => ({
  'is-up': props.change >= 0,
  'is-down': props.change < 0,
}))
</script>

<style scoped>
.kpi-card {
  background: #fff;
  border-radius: 10px;
  padding: 14px 16px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.kpi-label {
  font-size: 12px;
  color: #8c8c8c;
}
.kpi-value {
  font-size: 22px;
  font-weight: 600;
  color: #1a1a1a;
}
.kpi-value .prefix,
.kpi-value .suffix {
  font-size: 13px;
  font-weight: 400;
  color: #595959;
  margin: 0 2px;
}
.kpi-change {
  font-size: 12px;
  display: flex;
  align-items: center;
  gap: 2px;
}
.kpi-change.is-up {
  color: var(--color-success);
}
.kpi-change.is-down {
  color: var(--color-danger);
}
.kpi-change .hint {
  color: #bfbfbf;
  margin-left: 4px;
}
</style>
