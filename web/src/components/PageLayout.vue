<!--
  PageLayout —— 页面容器基元
  统一页面外层容器：
  - variant="table"（默认）：渲染全局 .page-container，复用其表格 flex 滚动链（适合单表列表页，表体独立滚动）。
  - variant="flow"：渲染独立类 .page-flow（自带 token 内边距 + 卡片间距 + 整页滚动），不施加表格滚动链，
    收编各页手写的「解除全局 .page-container 锁定」覆写 hack（多卡片纵向流式详情页）。
  与 PageHeader（工具栏 Teleport）正交：标题→面包屑、操作→PageHeader、内容→PageLayout。
-->
<template>
  <div :class="[variant === 'flow' ? 'page-flow' : 'page-container', { 'page-container--flush': flush && variant !== 'flow' }]">
    <slot />
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  /** table=单表列表页（保留全局表格滚动链）；flow=多卡片纵向流式 */
  variant?: 'table' | 'flow'
  /** 列表贴边：去 .page-container 左右留白让表格满宽（仅 table variant 生效） */
  flush?: boolean
}>(), {
  variant: 'table',
  flush: false,
})
</script>

<style scoped lang="scss">
.page-flow {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  gap: var(--space-md12);
  padding: var(--page-pad-y) var(--page-pad-x);
  background: var(--bg-page);
  overflow-x: hidden;
  overflow-y: auto;
}
</style>
