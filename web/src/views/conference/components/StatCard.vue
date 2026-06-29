<template>
  <div
    class="stat-card"
    :class="{ 'stat-card--clickable': clickable }"
    @click="handleClick"
  >
    <div class="stat-card__title">{{ title }}</div>
    <div class="stat-card__value" :style="{ color: color }">
      {{ value }}<span v-if="suffix" class="stat-card__suffix">{{ suffix }}</span>
    </div>
    <div v-if="subValue" class="stat-card__sub">{{ subValue }}</div>
    <a-progress
      v-if="progress !== undefined"
      :percent="progress"
      :stroke-color="computedProgressColor"
      :show-info="false"
      size="small"
      class="stat-card__progress"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  /** 指标标题 */
  title: string
  /** 主数值 */
  value: string | number
  /** 数值后缀 */
  suffix?: string
  /** 附加展示 */
  subValue?: string
  /** 进度百分比 0-100 */
  progress?: number
  /** 进度条颜色 */
  progressColor?: string
  /** 是否可点击 */
  clickable?: boolean
  /** 数值颜色 */
  color?: string
}

const props = withDefaults(defineProps<Props>(), {
  suffix: undefined,
  subValue: undefined,
  progress: undefined,
  progressColor: undefined,
  clickable: true,
  color: 'var(--color-info)',
})

const emit = defineEmits<{
  click: [e: MouseEvent]
}>()

const computedProgressColor = computed(() => {
  if (props.progressColor) return props.progressColor
  if (props.progress === undefined) return 'var(--color-info)'
  if (props.progress >= 100) return 'var(--color-success)'
  if (props.progress >= 50) return 'var(--color-info)'
  return 'var(--color-warning)'
})

function handleClick(e: MouseEvent) {
  if (props.clickable) {
    emit('click', e)
  }
}
</script>

<style scoped lang="scss">
.stat-card {
  background: var(--bg-card);
  border-radius: var(--radius-lg);
  padding: var(--space-lg16);
  box-shadow: var(--shadow-sm);
  height: 100%;
  transition: box-shadow 0.3s, transform 0.3s;

  &--clickable {
    cursor: pointer;

    &:hover {
      box-shadow: var(--shadow-md);
      transform: translateY(-1px);
    }
  }

  &__title {
    color: var(--text-3);
    font-size: var(--font-base);
    margin-bottom: var(--space-sm8);
    line-height: 1.4;
  }

  &__value {
    font-size: var(--font-2xl);
    font-weight: 600;
    line-height: 1.3;
  }

  &__suffix {
    font-size: var(--font-base);
    font-weight: 400;
    margin-left: 2px;
  }

  &__sub {
    color: var(--text-3);
    font-size: var(--font-sm2);
    margin-top: var(--space-xs4);
  }

  &__progress {
    margin-top: var(--space-sm8);
  }
}
</style>
