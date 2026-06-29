<script setup lang="ts">
import { computed } from 'vue'
import type { CardComponentRuntime } from '@/types/cardflow'

const props = defineProps<{
  component: CardComponentRuntime
}>()

const amount = computed(() => {
  const value = props.component.value
  const numberValue = Number(value)
  if (!Number.isFinite(numberValue)) return String(value ?? '-')
  return `¥${numberValue.toLocaleString('zh-CN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`
})
</script>

<template>
  <section class="cf-amount-summary" :class="{ 'cf-amount-summary--masked': component.masked }">
    <div class="cf-amount-summary__label">{{ component.title || '金额摘要' }}</div>
    <div class="cf-amount-summary__value">{{ amount }}</div>
    <div v-if="component.statisticKey" class="cf-amount-summary__key">{{ component.statisticKey }}</div>
  </section>
</template>

<style scoped lang="scss">
.cf-amount-summary {
  border: 1px solid var(--border);
  background: var(--bg-muted);
  border-radius: 6px;
  padding: 12px 14px;
  min-width: 0;

  &__label {
    color: var(--text-2);
    font-size: 12px;
    line-height: 18px;
  }

  &__value {
    color: var(--text-1);
    font-size: 24px;
    line-height: 32px;
    font-weight: 700;
    margin-top: 2px;
  }

  &__key {
    color: var(--text-3);
    font-size: 11px;
    margin-top: 4px;
  }

  &--masked &__value {
    letter-spacing: 0;
  }
}
</style>
