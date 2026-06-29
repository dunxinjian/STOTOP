<script setup lang="ts">
import type { CardComponentRuntime } from '@/types/cardflow'

defineProps<{
  component: CardComponentRuntime
}>()
</script>

<template>
  <section class="cf-dynamic-approver">
    <div class="cf-dynamic-approver__title">{{ component.title || '动态审批' }}</div>
    <div v-if="component.snapshots.length === 0" class="cf-dynamic-approver__empty">
      暂无动态审批记录
    </div>
    <div v-else class="cf-dynamic-approver__items">
      <article v-for="snapshot in component.snapshots" :key="`${snapshot.snapshotType}-${snapshot.reason}`">
        <strong>{{ snapshot.title || snapshot.metadata?.policyName || '动态审批策略' }}</strong>
        <p>{{ snapshot.reason }}</p>
      </article>
    </div>
  </section>
</template>

<style scoped lang="scss">
.cf-dynamic-approver {
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 10px 12px;
  background: var(--bg-card);

  &__title {
    color: var(--text-1);
    font-size: 13px;
    font-weight: 600;
    margin-bottom: 8px;
  }

  &__empty {
    color: var(--text-3);
    font-size: 12px;
  }

  &__items {
    display: grid;
    gap: 8px;
  }

  article {
    border: 1px dashed var(--border);
    border-radius: 4px;
    padding: 8px;
    background: var(--bg-muted);
  }

  strong {
    color: var(--text-1);
    font-size: 12px;
  }

  p {
    margin: 3px 0 0;
    color: var(--text-2);
    font-size: 12px;
    line-height: 18px;
  }
}
</style>
