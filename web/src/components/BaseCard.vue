<!--
  BaseCard —— 卡片范式定型组件
  评估记录：全局 .page-card 类只能表达「纯容器」，无法承载「标题+操作区+无内边距+hover」等变体
  （a-card 的 head/hover 已被 ant-override 接管，但自造卡片游离在外）。故抽此组件承接渐进替换，
  根 .base-card 套 .page-card 同款令牌范式（--radius-lg/--border/--shadow-sm/--space-lg16），
  hoverable 时 hover 升 --shadow-md。轻量纯容器场景仍可直接用全局 .page-card 类。
-->
<template>
  <div
    class="base-card"
    :class="{
      'base-card--bordered': bordered,
      'base-card--hoverable': hoverable,
      'base-card--no-padding': noPadding,
    }"
  >
    <div v-if="title || $slots.title || $slots.extra" class="base-card__head">
      <div class="base-card__title">
        <slot name="title">{{ title }}</slot>
      </div>
      <div v-if="$slots.extra" class="base-card__extra">
        <slot name="extra" />
      </div>
    </div>
    <div class="base-card__body">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  /** 卡片标题（也可用 #title 插槽） */
  title?: string
  /** 是否显示边框 */
  bordered?: boolean
  /** 是否启用 hover 升起阴影 */
  hoverable?: boolean
  /** 主体是否去内边距（如内嵌表格） */
  noPadding?: boolean
}>(), {
  title: undefined,
  bordered: true,
  hoverable: false,
  noPadding: false,
})
</script>

<style scoped lang="scss">
.base-card {
  background: var(--bg-card);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  transition: box-shadow 0.2s ease;

  &--bordered {
    border: 1px solid var(--border);
  }

  &--hoverable:hover {
    box-shadow: var(--shadow-md);
  }
}

.base-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-sm8);
  padding: var(--space-md12) var(--space-lg16);
  border-bottom: 1px solid var(--border);
}

.base-card__title {
  font-size: var(--font-lg);
  font-weight: 600;
  color: var(--text-1);
  min-width: 0;
}

.base-card__extra {
  flex-shrink: 0;
}

.base-card__body {
  padding: var(--space-lg16);
}

.base-card--no-padding .base-card__body {
  padding: 0;
}
</style>
