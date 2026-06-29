<script setup lang="ts">
import { computed } from 'vue'
import type { CardComponentRuntime } from '@/types/cardflow'

interface RelationCardValue {
  id: number
  sourceCardNumber?: string | null
  sourceCardTitle?: string | null
  targetCardNumber?: string | null
  targetCardTitle?: string | null
  relationType: string
  description?: string | null
  offsetAmount?: number | null
}

const props = defineProps<{ component: CardComponentRuntime }>()

const relations = computed<RelationCardValue[]>(() =>
  Array.isArray(props.component.value) ? props.component.value : [],
)

function formatAmount(value?: number | null) {
  if (value === null || value === undefined) return ''
  const amount = Number(value)
  if (!Number.isFinite(amount)) return ''
  return `¥${amount.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}
</script>

<template>
  <section class="cf-relation-component">
    <div class="cf-relation-component__title">{{ component.title || '关联卡片' }}</div>
    <div v-if="relations.length === 0" class="cf-relation-component__empty">
      暂无关联卡片
    </div>
    <div v-else class="cf-relation-component__list">
      <article
        v-for="relation in relations"
        :key="relation.id"
        class="cf-relation-component__item"
      >
        <div class="cf-relation-component__main">
          <span>{{ relation.targetCardNumber || relation.sourceCardNumber || '-' }}</span>
          <strong v-if="relation.offsetAmount">{{ formatAmount(relation.offsetAmount) }}</strong>
        </div>
        <div class="cf-relation-component__sub">
          {{ relation.targetCardTitle || relation.sourceCardTitle || relation.description || relation.relationType }}
        </div>
        <div v-if="relation.description" class="cf-relation-component__desc">
          {{ relation.description }}
        </div>
      </article>
    </div>
  </section>
</template>

<style scoped lang="scss">
.cf-relation-component {
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 10px 12px;
  background: var(--bg-card);

  &__title {
    color: var(--text-1);
    font-size: 13px;
    font-weight: 600;
    margin-bottom: 6px;
  }

  &__empty {
    color: var(--text-2);
    font-size: 12px;
  }

  &__list {
    display: grid;
    gap: 8px;
  }

  &__item {
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 8px 10px;
    background: var(--bg-muted);
  }

  &__main {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    color: var(--text-1);
    font-size: 13px;
    font-weight: 600;

    strong {
      // 抵销金额的暖色强调（原 #aa5d18 暖棕），取品牌主色而非告警金，避免误导为"警示"语义
      color: var(--color-primary);
      font-size: 13px;
      font-weight: 700;
    }
  }

  &__sub,
  &__desc {
    margin-top: 4px;
    color: var(--text-2);
    font-size: 12px;
    line-height: 1.5;
  }
}
</style>
