<template>
  <!-- v-if: 排除不需要工具栏的页面 + 有 PageHeader 注册时显示 -->
  <div
    v-if="showToolbar && toolbarStore.visible"
    class="app-toolbar"
  >
    <!-- 第一行：操作工具栏（有 row1 注册内容时显示） -->
    <div
      class="toolbar-primary"
      v-show="toolbarStore.hasRow1"
    >
      <div class="toolbar-left">
        <div class="toolbar-left-teleport" id="page-toolbar-left"></div>
      </div>
      <div class="toolbar-center" id="page-toolbar-center"></div>
      <div class="toolbar-right" id="page-toolbar-actions"></div>
    </div>
    <!-- 第二行：筛选工具栏（仅在有内容时显示） -->
    <div class="toolbar-secondary" v-show="toolbarStore.hasRow2" id="page-toolbar-row2"></div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useToolbarStore } from '@/stores/toolbar'

const route = useRoute()
const toolbarStore = useToolbarStore()

// 排除不需要工具栏的页面（登录、首页、错误页）
const showToolbar = computed(() => {
  const p = route.path
  return p !== '/workhub' && p !== '/' && p !== '/login' && p !== '/403' && p !== '/404'
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.app-toolbar {
  background: var(--bg-card);
  border-bottom: 1px solid var(--border);
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.04);
  flex-shrink: 0;
  position: relative;
  z-index: 1;
}

.toolbar-primary {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  min-height: 48px;
  padding: 4px 16px;
  gap: 8px 16px;
  position: relative;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
  min-width: 0;
  position: relative;
  z-index: 1;
}

.toolbar-left-teleport {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.toolbar-left-teleport:empty {
  display: none;
}

.toolbar-center {
  position: absolute;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  display: flex;
  align-items: center;
  justify-content: center;
  width: max-content;
  max-width: calc(100% - 320px);
  min-width: 0;
  pointer-events: none;
}

.toolbar-center:empty {
  display: none;
}

.toolbar-center :deep(.page-toolbar-center-content) {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 0;
  max-width: 100%;
}

.toolbar-right {
  display: flex;
  align-items: center;
  flex-shrink: 0;
  gap: 8px;
  position: relative;
  z-index: 1;
}

.toolbar-right :deep(.ant-btn) {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 32px;
  font-size: 13px;
  border-radius: $border-radius-sm;
}

// 工具栏内分段控件（a-radio-group button）统一 32px 高，与右侧筛选/按钮对齐。
// 控件经 PageHeader Teleport 到此，页面 scoped :deep 够不到，必须在面包屑全局设；
// 用 size="small" 保持紧凑宽度（不挤换行），仅强制高度。
.app-toolbar :deep(.ant-radio-button-wrapper) {
  height: 32px;
  line-height: 30px;
}

.toolbar-secondary {
  border-top: 1px solid $border-color-lighter;
  min-height: 40px;
  padding: 0 16px;
  display: flex;
  align-items: center;
}

.toolbar-secondary:empty {
  display: none;
  padding: 0;
}

// 确保工具栏内所有控件垂直居中
.toolbar-primary :deep(.ant-input),
.toolbar-primary :deep(.ant-select),
.toolbar-primary :deep(.ant-picker),
.toolbar-primary :deep(.ant-input-search) {
  vertical-align: middle;
  display: inline-flex;
  align-items: center;
}

.toolbar-secondary :deep(.ant-tabs) {
  margin-bottom: 0;
}

.toolbar-secondary :deep(.ant-tabs-nav) {
  margin-bottom: 0;
}

.toolbar-secondary :deep(.ant-tabs-tab + .ant-tabs-tab) {
  margin-left: 16px;
}
</style>
