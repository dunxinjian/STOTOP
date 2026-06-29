<script setup lang="ts">
import type { CardComponentRuntime } from '@/types/cardflow'

defineProps<{ component: CardComponentRuntime }>()
</script>

<template>
  <section class="cf-risk-component">
    <strong>{{ component.title || '风险提示' }}</strong>
    <p>{{ component.value ?? component.props?.message ?? '暂无风险提示' }}</p>
  </section>
</template>

<style scoped lang="scss">
.cf-risk-component {
  border: 1px solid var(--color-warning);
  border-radius: 6px;
  padding: 10px 12px;
  background: var(--color-warning-light);

  strong {
    color: var(--color-warning-text);
    font-size: 13px;
  }

  p {
    margin: 4px 0 0;
    color: var(--color-warning-text);
    font-size: 12px;
    line-height: 18px;
  }
}
</style>
