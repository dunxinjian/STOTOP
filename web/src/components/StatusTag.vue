<!--
  StatusTag —— 状态/业务标签统一组件
  统一替代各页手写的 a-tag :color="'success'|'processing'|'error'" 字面量。
  type 走语义状态色（浅底+深字，对应 --color-*-light/--color-*-text）；
  biz 走业务域色（运单/合同/质量/审批/积分/财务），优先级高于 type。
-->
<template>
  <span class="status-tag" :class="bizClass ? bizClass : `status-tag--${type}`">
    <span v-if="dot" class="status-tag__dot" />
    <slot>{{ text }}</slot>
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue'

type StatusType = 'success' | 'warning' | 'danger' | 'info' | 'default'
type BizType = 'waybill' | 'contract' | 'quality' | 'approval' | 'points' | 'finance'

const props = withDefaults(defineProps<{
  /** 语义状态类型 */
  type?: StatusType
  /** 业务域类型（优先于 type） */
  biz?: BizType
  /** 文案（也可用默认插槽） */
  text?: string
  /** 是否显示前置圆点 */
  dot?: boolean
}>(), {
  type: 'default',
  biz: undefined,
  text: undefined,
  dot: false,
})

const bizClass = computed(() => (props.biz ? `status-tag--biz-${props.biz}` : ''))
</script>

<style scoped lang="scss">
.status-tag {
  display: inline-flex;
  align-items: center;
  gap: var(--space-xs4);
  padding: 1px var(--space-sm8);
  font-size: var(--font-sm);
  line-height: 20px;
  border-radius: var(--radius-sm);
  white-space: nowrap;
}

.status-tag__dot {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: currentColor;
}

// —— 语义状态：浅底 + 深字
.status-tag--success { background: var(--color-success-light); color: var(--color-success-text); }
.status-tag--warning { background: var(--color-warning-light); color: var(--color-warning-text); }
.status-tag--danger  { background: var(--color-danger-light);  color: var(--color-danger-text); }
.status-tag--info    { background: var(--color-info-light);    color: var(--color-info-text); }
.status-tag--default { background: var(--bg-muted);            color: var(--text-2); }

// —— 业务域：业务色字 + 浅底（用主色浅底统一柔和）
.status-tag--biz-waybill  { background: var(--color-primary-light); color: var(--biz-waybill); }
.status-tag--biz-contract { background: var(--color-primary-light); color: var(--biz-contract); }
.status-tag--biz-quality  { background: var(--color-primary-light); color: var(--biz-quality); }
.status-tag--biz-approval { background: var(--color-info-light);    color: var(--biz-approval); }
.status-tag--biz-points   { background: var(--color-warning-light); color: var(--biz-points); }
.status-tag--biz-finance  { background: var(--color-warning-light); color: var(--biz-finance); }
</style>
