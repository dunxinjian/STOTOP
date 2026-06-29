<template>
  <div class="empty-state" :class="`empty-state--${size}`">
    <!-- 自定义图标 -->
    <div v-if="icon" class="empty-icon" :style="{ fontSize: resolvedIconSize + 'px', color: iconColor }">
      <component :is="icon" />
    </div>
    <!-- 默认 a-empty 图片 -->
    <a-empty v-else :description="false" :image-style="{ height: resolvedImageSize + 'px' }" />

    <!-- 主标题 -->
    <div class="empty-title">{{ title }}</div>

    <!-- 描述文字 -->
    <div v-if="description" class="empty-description">{{ description }}</div>

    <!-- 操作按钮 -->
    <div v-if="showAction && actionText" class="empty-action">
      <a-button type="primary" @click="handleAction">{{ actionText }}</a-button>
    </div>

    <!-- 自定义内容插槽 -->
    <slot />
  </div>
</template>

<script setup lang="ts">
import type { Component } from 'vue'
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'

const props = withDefaults(defineProps<{
  /** 主标题 */
  title?: string
  /** 描述文字 */
  description?: string
  /** 自定义图标组件 */
  icon?: Component
  /** 图标大小（px），未传时按 size 推导 */
  iconSize?: number
  /** 图标颜色 */
  iconColor?: string
  /** 默认图片大小（px），未传时按 size 推导 */
  imageSize?: number
  /** 操作按钮文字 */
  actionText?: string
  /** 操作跳转路由 */
  actionRoute?: string | RouteLocationRaw
  /** 是否显示操作区 */
  showAction?: boolean
  /** 尺寸：default=整页空态；small=表格 #emptyText 内嵌 */
  size?: 'default' | 'small'
}>(), {
  title: '暂无数据',
  description: undefined,
  icon: undefined,
  iconSize: undefined,
  iconColor: 'var(--text-disabled)',
  imageSize: undefined,
  actionText: undefined,
  actionRoute: undefined,
  showAction: false,
  size: 'default',
})

const resolvedIconSize = computed(() => props.iconSize ?? (props.size === 'small' ? 32 : 48))
const resolvedImageSize = computed(() => props.imageSize ?? (props.size === 'small' ? 60 : 120))

const router = useRouter()

function handleAction() {
  if (props.actionRoute) {
    router.push(props.actionRoute)
  }
}
</script>

<style scoped lang="scss">
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 24px;
  min-height: 300px;
  background: transparent;

  // 表格 #emptyText 内嵌：更紧凑
  &--small {
    padding: var(--space-xl24);
    min-height: 160px;
  }
}

.empty-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 16px;
}

.empty-title {
  margin-top: 12px;
  font-size: var(--font-lg);
  font-weight: 500;
  color: var(--text-1);
}

.empty-state--small .empty-title {
  font-size: var(--font-base);
}

.empty-description {
  margin-top: 8px;
  font-size: var(--font-base);
  color: var(--text-3);
}

.empty-action {
  margin-top: 20px;
}
</style>
