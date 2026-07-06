<script setup lang="ts">
import { computed } from 'vue'
import type { DynamicStagePolicyRequest, SchemaFieldDefinition, StageRouteRuleRequest } from '@/types/cardflow'
import type { StageDefinition } from '../StageDefinitionEditor.vue'
import { runRuleHealthChecks, type HealthItem, type DiagnosticTarget } from '@/utils/cardflowDiagnostics'

const props = defineProps<{
  stages: StageDefinition[]
  routes: StageRouteRuleRequest[]
  dynamicPolicies: DynamicStagePolicyRequest[]
  fields: SchemaFieldDefinition[]
}>()

// 点击某条诊断跳转到现场（由 FlowDefinitionEditPage 选中对应节点/边并开抽屉）
const emit = defineEmits<{ navigate: [target: DiagnosticTarget] }>()

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
        :class="[
          `cf-rule-health__item--${item.level}`,
          { 'cf-rule-health__item--clickable': item.target },
        ]"
        :role="item.target ? 'button' : undefined"
        :tabindex="item.target ? 0 : undefined"
        @click="item.target && emit('navigate', item.target)"
        @keydown.enter.prevent="item.target && emit('navigate', item.target)"
        @keydown.space.prevent="item.target && emit('navigate', item.target)"
      >
        <strong>
          {{ item.title }}
          <span v-if="item.target" class="cf-rule-health__locate">定位 →</span>
        </strong>
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

  &--clickable {
    cursor: pointer;
    transition: box-shadow .15s, transform .05s;

    &:hover { box-shadow: 0 0 0 2px var(--border-strong); }
    &:active { transform: translateY(1px); }
    &:focus-visible { outline: 2px solid var(--color-primary); outline-offset: 1px; }

    strong { display: flex; align-items: center; gap: 6px; }
  }

  .cf-rule-health__locate {
    margin-top: 0;
    margin-left: auto;
    font-size: 11px;
    font-weight: 500;
    color: var(--text-3);
    white-space: nowrap;
  }
}
</style>
