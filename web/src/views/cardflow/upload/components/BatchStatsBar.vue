<template>
  <div class="kpi-bar">
    <template v-for="(item, idx) in kpiItems" :key="idx">
      <div v-if="idx > 0" class="kpi-divider"></div>
      <div class="kpi-item">
        <span class="kpi-dot" :class="item.dot"></span>
        <span class="kpi-value">{{ item.value }}</span>
        <span class="kpi-label">{{ item.label }}</span>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import type { KpiItem } from '../composables/useUploadCenter'

defineProps<{
  kpiItems: KpiItem[]
}>()
</script>

<style scoped lang="scss">
.kpi-bar {
  display: flex;
  align-items: center;
  gap: 0;
  padding: 0;
  margin-bottom: 0;
}

.kpi-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 24px;

  &:first-child {
    padding-left: 0;
  }
}

.kpi-divider {
  width: 1px;
  height: 20px;
  background: var(--border);
  flex-shrink: 0;
}

.kpi-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;

  &.blue { background: var(--color-info); }
  &.blue-pulse { background: var(--color-info); animation: pulse 1.5s infinite; }
  &.red { background: var(--color-danger); }
  &.orange { background: var(--color-warning); }
  &.green { background: var(--color-success); }
  &.purple { background: var(--biz-waybill); }
}

.kpi-value {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-1);
}

.kpi-label {
  font-size: 13px;
  color: var(--text-3);
  margin-left: 4px;
  white-space: nowrap;
}

@keyframes pulse {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.5; transform: scale(1.2); }
}
</style>
