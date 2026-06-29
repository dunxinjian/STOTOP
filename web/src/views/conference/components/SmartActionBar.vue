<template>
  <div class="smart-action-bar" :style="{ backgroundColor: bgColor }">
    <div class="smart-action-bar__left">
      <slot />
    </div>
    <div class="smart-action-bar__right">
      <slot name="extra" />
      <span v-if="description" class="smart-action-bar__desc">{{ description }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  /** 右侧说明文字 */
  description?: string
  /** 背景色，默认蓝底 */
  bgColor?: string
}

withDefaults(defineProps<Props>(), {
  description: undefined,
  bgColor: '#f0f5ff',
})
</script>

<style scoped lang="scss">
.smart-action-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-radius: 8px;
  margin-bottom: 12px;

  &__left {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-shrink: 0;
  }

  &__right {
    display: flex;
    align-items: center;
    gap: 12px;
    min-width: 0;
  }

  &__desc {
    color: #8c8c8c;
    font-size: 13px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
}
</style>
