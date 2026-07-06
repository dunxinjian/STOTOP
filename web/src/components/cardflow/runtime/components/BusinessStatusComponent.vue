<script setup lang="ts">
/**
 * 业务状态组件共用底座：吃满已配置 props —— severity 配色(走令牌) / statusText 徽标 /
 * showBadge 开关 / currencySymbol 数值前缀。供 paymentInfo/budgetStatus/invoiceStatus/loanOffset 复用。
 */
import { computed } from 'vue'
import type { CardComponentRuntime } from '@/types/cardflow'

const props = defineProps<{
  component: CardComponentRuntime
  defaultTitle: string
  defaultStatus: string
}>()

const severity = computed(() => {
  const s = String(props.component.props?.severity ?? 'default')
  return ['info', 'warning', 'danger', 'success'].includes(s) ? s : 'default'
})

// 主值：绑定值为数值 → 按 currencySymbol 格式化金额；否则显示状态文案
const mainText = computed(() => {
  const v = props.component.value
  const n = Number(v)
  if (v !== null && v !== undefined && v !== '' && !isNaN(n)) {
    const sym = (props.component.props?.currencySymbol as string) ?? '¥'
    return `${sym}${n.toLocaleString('zh-CN')}`
  }
  return (v as string) ?? (props.component.props?.statusText as string) ?? props.defaultStatus
})

const badgeText = computed(() => props.component.props?.statusText as string | undefined)
const showBadge = computed(() => props.component.props?.showBadge === true && !!badgeText.value)
</script>

<template>
  <section class="cf-status-component" :class="`cf-status-component--${severity}`">
    <span class="cf-status-component__label">{{ component.title || defaultTitle }}</span>
    <span class="cf-status-component__value">
      <strong>{{ mainText }}</strong>
      <em v-if="showBadge" class="cf-status-component__badge">{{ badgeText }}</em>
    </span>
  </section>
</template>

<style scoped lang="scss">
.cf-status-component {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 10px 12px;
  background: var(--bg-card);
  color: var(--text-2);
  font-size: 13px;

  &__value {
    display: inline-flex;
    align-items: center;
    gap: 8px;
  }

  strong {
    color: var(--text-1);
    font-weight: 600;
  }

  &__badge {
    font-style: normal;
    font-size: 11px;
    padding: 1px 8px;
    border-radius: 10px;
    background: var(--bg-muted);
    color: var(--text-2);
  }

  // severity 配色（default 保持原中性观感）
  &--info {
    border-color: var(--color-info);
    background: var(--color-info-light);
    strong { color: var(--color-info-text); }
    .cf-status-component__badge { background: var(--color-info); color: var(--bg-card); }
  }

  &--warning {
    border-color: var(--color-warning);
    background: var(--color-warning-light);
    strong { color: var(--color-warning-text); }
    .cf-status-component__badge { background: var(--color-warning); color: var(--bg-card); }
  }

  &--danger {
    border-color: var(--color-danger);
    background: var(--color-danger-light);
    strong { color: var(--color-danger-text); }
    .cf-status-component__badge { background: var(--color-danger); color: var(--bg-card); }
  }

  &--success {
    border-color: var(--color-success);
    background: var(--color-success-light);
    strong { color: var(--color-success-text); }
    .cf-status-component__badge { background: var(--color-success); color: var(--bg-card); }
  }
}
</style>
