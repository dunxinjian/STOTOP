<script setup lang="ts">
import { computed } from 'vue'
import type { DynamicStagePolicyRequest, SchemaFieldDefinition, StageRouteRuleRequest } from '@/types/cardflow'
import type { StageDefinition } from '../StageDefinitionEditor.vue'
import { runRuleHealthChecks, type HealthItem } from '@/utils/cardflowDiagnostics'

const props = defineProps<{
  stages: StageDefinition[]
  routes: StageRouteRuleRequest[]
  dynamicPolicies: DynamicStagePolicyRequest[]
  fields: SchemaFieldDefinition[]
}>()

// 诊断逻辑单一真源：utils/cardflowDiagnostics（纯函数，与发布校验同源，杜绝两处漂移）
const items = computed<HealthItem[]>(() =>
  runRuleHealthChecks({
    stages: props.stages,
    routes: props.routes,
    dynamicPolicies: props.dynamicPolicies,
    fields: props.fields,
  }),
)
</script>

<template>
  <section class="cf-rule-health">
    <header class="cf-rule-health__head">
      <strong>规则健康检查</strong>
      <span>默认分支 · 规则重叠 · 死路节点 · 循环路径 · 无法到达 · 处理人</span>
    </header>
    <div class="cf-rule-health__list">
      <article
        v-for="item in items"
        :key="`${item.title}-${item.detail}`"
        class="cf-rule-health__item"
        :class="`cf-rule-health__item--${item.level}`"
      >
        <strong>{{ item.title }}</strong>
        <span>{{ item.detail }}</span>
      </article>
    </div>
  </section>
</template>

<style scoped lang="scss">
.cf-rule-health {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.cf-rule-health__head {
  strong,
  span {
    display: block;
  }

  strong {
    color: var(--text-1);
    font-size: 14px;
  }

  span {
    margin-top: 2px;
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-rule-health__list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.cf-rule-health__item {
  padding: 9px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-card);

  strong,
  span {
    display: block;
  }

  strong {
    color: var(--text-1);
    font-size: 13px;
  }

  span {
    margin-top: 3px;
    color: var(--text-2);
    font-size: 12px;
    line-height: 1.5;
  }

  &--error {
    border-color: var(--color-danger);
    background: var(--color-danger-light);
  }

  &--warning {
    border-color: var(--color-warning);
    background: var(--color-warning-light);
  }

  &--ok {
    border-color: var(--color-success);
    background: var(--color-success-light);
  }
}
</style>
