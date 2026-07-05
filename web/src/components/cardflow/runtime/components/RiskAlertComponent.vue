<script setup lang="ts">
import { computed } from 'vue'
import type { CardComponentRuntime } from '@/types/cardflow'

const props = defineProps<{ component: CardComponentRuntime }>()

// 吃满已配置 props：severity 配色（走令牌）/ statusText 徽标 / showBadge 开关
const severity = computed(() => {
  const s = String(props.component.props?.severity ?? 'warning')
  return ['info', 'warning', 'danger', 'success'].includes(s) ? s : 'warning'
})
const message = computed(
  () => props.component.value ?? props.component.props?.message ?? props.component.props?.statusText ?? '暂无风险提示',
)
const badgeText = computed(() => props.component.props?.statusText as string | undefined)
const showBadge = computed(() => props.component.props?.showBadge !== false && !!badgeText.value)
</script>

<template>
  <section class="cf-risk-component" :class="`cf-risk-component--${severity}`">
    <div class="cf-risk-component__head">
      <strong>{{ component.title || '风险提示' }}</strong>
      <span v-if="showBadge" class="cf-risk-component__badge">{{ badgeText }}</span>
    </div>
    <p>{{ message }}</p>
  </section>
</template>

<style scoped lang="scss">
.cf-risk-component {
  border: 1px solid var(--color-warning);
  border-radius: 6px;
  padding: 10px 12px;
  background: var(--color-warning-light);

  &__head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
  }

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

  &__badge {
    flex-shrink: 0;
    padding: 1px 8px;
    border-radius: 10px;
    background: var(--color-warning);
    color: var(--bg-card);
    font-size: 11px;
  }

  // severity 配色（默认 warning，上面已给）
  &--info {
    border-color: var(--color-info);
    background: var(--color-info-light);

    strong,
    p {
      color: var(--color-info-text);
    }

    .cf-risk-component__badge {
      background: var(--color-info);
    }
  }

  &--danger {
    border-color: var(--color-danger);
    background: var(--color-danger-light);

    strong,
    p {
      color: var(--color-danger-text);
    }

    .cf-risk-component__badge {
      background: var(--color-danger);
    }
  }

  &--success {
    border-color: var(--color-success);
    background: var(--color-success-light);

    strong,
    p {
      color: var(--color-success-text);
    }

    .cf-risk-component__badge {
      background: var(--color-success);
    }
  }
}
</style>
