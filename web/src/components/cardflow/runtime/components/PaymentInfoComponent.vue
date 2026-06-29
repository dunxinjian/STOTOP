<script setup lang="ts">
import type { CardComponentRuntime } from '@/types/cardflow'

defineProps<{ component: CardComponentRuntime }>()
</script>

<template>
  <section class="cf-status-component">
    <span>{{ component.title || '付款信息' }}</span>
    <strong>{{ component.value ?? component.props?.statusText ?? '待付款' }}</strong>
  </section>
</template>

<style scoped lang="scss">
.cf-status-component {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 10px 12px;
  background: var(--bg-card);
  color: var(--text-2);
  font-size: 13px;

  strong {
    color: var(--text-1);
    font-weight: 600;
  }
}
</style>
