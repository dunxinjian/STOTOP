<script setup lang="ts">
import type { CardComponentRuntime } from '@/types/cardflow'

defineProps<{
  component: CardComponentRuntime
}>()
</script>

<template>
  <section class="cf-explain-component">
    <div class="cf-explain-component__title">{{ component.title || '流转说明' }}</div>
    <div v-if="component.snapshots.length === 0" class="cf-explain-component__empty">
      暂无流转决策记录
    </div>
    <ol v-else class="cf-explain-component__list">
      <li v-for="snapshot in component.snapshots" :key="`${snapshot.snapshotType}-${snapshot.reason}`">
        <span>{{ snapshot.title || '流转决策' }}</span>
        <p>{{ snapshot.reason }}</p>
      </li>
    </ol>
  </section>
</template>

<style scoped lang="scss">
.cf-explain-component {
  border-left: 3px solid var(--color-success);
  background: color-mix(in srgb, var(--color-success) 8%, transparent);
  padding: 10px 12px;
  border-radius: 4px;

  &__title {
    color: var(--color-success-text);
    font-size: 13px;
    font-weight: 600;
    margin-bottom: 6px;
  }

  &__empty {
    color: var(--text-3);
    font-size: 12px;
  }

  &__list {
    margin: 0;
    padding-left: 18px;
    color: var(--color-success-text);

    li + li {
      margin-top: 8px;
    }

    span {
      font-size: 12px;
      font-weight: 600;
    }

    p {
      margin: 2px 0 0;
      color: var(--text-2);
      font-size: 12px;
      line-height: 18px;
    }
  }
}
</style>
