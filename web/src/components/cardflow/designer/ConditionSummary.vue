<script setup lang="ts">
/**
 * 条件摘要：「首条 + 等 N 条条件」（设计 D3）。点击计数 emit expand 进条件编辑器。
 */
import { computed } from 'vue'
import { summarizeConditions } from '@/utils/cardflowConditionFormat'
import type { ConditionGroup, FieldOption } from '@/components/cardflow/ConditionBuilder.vue'

const props = defineProps<{
  conditions: ConditionGroup
  fields: FieldOption[]
}>()

const emit = defineEmits<{ expand: [] }>()

const summary = computed(() => summarizeConditions(props.conditions, props.fields))
</script>

<template>
  <span class="condition-summary">
    <span class="condition-summary__head" :class="{ 'is-empty': summary.head === '—' }">{{ summary.head }}</span>
    <a v-if="summary.rest > 0" class="condition-summary__more" @click.stop="emit('expand')">等 {{ summary.rest + 1 }} 条条件</a>
  </span>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.condition-summary {
  font-family: $font-family-mono;
  font-size: $font-size-sm;
  color: $text-regular;

  &__head.is-empty {
    color: $text-placeholder;
  }

  &__more {
    margin-left: 6px;
    font-family: $font-family;
    color: $color-primary;
    cursor: pointer;
  }
}
</style>
