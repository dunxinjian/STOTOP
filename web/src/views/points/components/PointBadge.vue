<template>
  <span class="point-badge" :class="badgeClass">
    <StarOutlined class="point-badge__icon" />
    <span class="point-badge__value">{{ value }}</span>
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { StarOutlined } from '@ant-design/icons-vue'

const props = defineProps<{
  value: number
  size?: 'small' | 'default' | 'large'
}>()

const badgeClass = computed(() => `point-badge--${props.size || 'default'}`)
</script>

<style scoped lang="scss">
.point-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  background: linear-gradient(135deg, var(--color-warning-light), #ffe7ba);
  border: 1px solid #ffd591;
  border-radius: 16px;
  color: #d46b08;
  font-weight: 600;

  &--small {
    padding: 0 8px;
    font-size: 12px;
    height: 22px;
  }

  &--default {
    padding: 2px 12px;
    font-size: 14px;
    height: 28px;
  }

  &--large {
    padding: 4px 16px;
    font-size: 18px;
    height: 36px;
  }

  &__icon {
    font-size: inherit;
  }
}
</style>
