<template>
  <a-card hoverable class="point-card" :bodyStyle="{ padding: '20px' }">
    <div class="point-card__inner">
      <div class="point-card__icon" :style="{ background: bgColor }">
        <component :is="icon" :style="{ fontSize: '28px', color: color }" />
      </div>
      <div class="point-card__content">
        <div class="point-card__value">{{ value }}</div>
        <div class="point-card__label">{{ label }}</div>
      </div>
    </div>
  </a-card>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  label: string
  value: number | string
  icon: any
  color: string
}>()

const bgColor = computed(() => `${props.color}15`)
</script>

<style scoped lang="scss">
.point-card {
  border-radius: 8px;
  transition: all 0.3s;

  &:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  }

  &__inner {
    display: flex;
    align-items: center;
    gap: 16px;
  }

  &__icon {
    width: 56px;
    height: 56px;
    border-radius: 12px;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
  }

  &__value {
    font-size: 28px;
    font-weight: 700;
    color: #1a1a1a;
    line-height: 1.2;
  }

  &__label {
    font-size: 14px;
    color: #8c8c8c;
    margin-top: 4px;
  }
}
</style>
