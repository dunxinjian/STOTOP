<script setup lang="ts">
/**
 * 多人摘要：「王芳、李强 等 5 人」（设计 D3）。点击 emit expand 由调用方开浮层。
 */
import { computed } from 'vue'
import { summarizeMembers } from '@/utils/cardflowConditionFormat'

const props = withDefaults(defineProps<{
  members: Array<{ id: string; name: string }>
  max?: number
}>(), {
  max: 2,
})

const emit = defineEmits<{ expand: [] }>()

const summary = computed(() => summarizeMembers(props.members, props.max))
</script>

<template>
  <span class="member-summary">
    <template v-if="summary.shown.length">
      <span class="member-summary__names">{{ summary.shown.join('、') }}</span>
      <a v-if="summary.more > 0" class="member-summary__more" @click.stop="emit('expand')">等 {{ members.length }} 人</a>
    </template>
    <span v-else class="member-summary__empty">—</span>
  </span>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.member-summary {
  font-size: $font-size-sm2;
  color: $text-primary;

  &__more {
    margin-left: 4px;
    font-size: $font-size-sm;
    color: $color-primary;
    cursor: pointer;
  }

  &__empty {
    color: $text-placeholder;
  }
}
</style>
