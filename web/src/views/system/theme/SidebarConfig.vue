<script setup lang="ts">
import type { ThemeConfig } from '@/stores/theme'

defineProps<{
  editConfig: ThemeConfig
  previewTheme: any
}>()
</script>

<template>
  <div class="config-section">
    <div class="section-header">
      <h3 class="section-title">侧边栏</h3>
      <p class="section-desc">调整侧边栏的宽度、颜色和Tab数量限制</p>
    </div>

    <div class="config-items">
      <!-- 展开宽度 -->
      <div class="config-row">
        <div class="config-label-area">
          <span class="config-label">展开宽度</span>
          <span class="config-sublabel">侧栏展开状态的宽度</span>
        </div>
        <div class="config-control slider-control">
          <a-slider v-model:value="editConfig.sidebarExpandedWidth" :min="140" :max="240" :step="10" style="width: 200px" />
          <span class="slider-value">{{ editConfig.sidebarExpandedWidth }}px</span>
        </div>
      </div>

      <!-- 折叠宽度 -->
      <div class="config-row">
        <div class="config-label-area">
          <span class="config-label">折叠宽度</span>
          <span class="config-sublabel">侧栏折叠状态的宽度</span>
        </div>
        <div class="config-control slider-control">
          <a-slider v-model:value="editConfig.sidebarCollapsedWidth" :min="40" :max="64" :step="4" style="width: 200px" />
          <span class="slider-value">{{ editConfig.sidebarCollapsedWidth }}px</span>
        </div>
      </div>

      <!-- 背景色 -->
      <div class="config-row">
        <div class="config-label-area">
          <span class="config-label">背景色</span>
          <span class="config-sublabel">侧栏整体背景色</span>
        </div>
        <div class="config-control color-control">
          <input type="color" v-model="editConfig.sidebarBgColor" class="color-input" />
          <a-input
            v-model:value="editConfig.sidebarBgColor"
            size="small"
            style="width: 140px"
            :maxlength="25"
          />
        </div>
      </div>

      <!-- 选中态背景色 -->
      <div class="config-row">
        <div class="config-label-area">
          <span class="config-label">选中态背景色</span>
          <span class="config-sublabel">Tab选中时的背景色</span>
        </div>
        <div class="config-control color-control">
          <input type="color" v-model="editConfig.sidebarActiveBgColor" class="color-input" />
          <a-input
            v-model:value="editConfig.sidebarActiveBgColor"
            size="small"
            style="width: 140px"
            :maxlength="30"
          />
        </div>
      </div>

      <!-- 最大Tab数 -->
      <div class="config-row">
        <div class="config-label-area">
          <span class="config-label">最大Tab数</span>
          <span class="config-sublabel">侧栏允许同时打开的动态Tab上限</span>
        </div>
        <div class="config-control slider-control">
          <a-slider v-model:value="editConfig.sidebarMaxTabs" :min="6" :max="20" :step="1" style="width: 200px" />
          <span class="slider-value">{{ editConfig.sidebarMaxTabs }}</span>
        </div>
      </div>
    </div>

    <a-divider style="margin: 24px 0 20px" />

    <div class="preview-area">
      <div class="preview-label">预览</div>
      <div class="sidebar-preview">
        <div
          class="sidebar-preview-bar"
          :style="{
            width: editConfig.sidebarExpandedWidth + 'px',
            background: editConfig.sidebarBgColor,
          }"
        >
          <div class="sidebar-preview-item">
            <div class="sidebar-preview-icon" />
            <span class="sidebar-preview-text">工作台</span>
          </div>
          <div
            class="sidebar-preview-item sidebar-preview-item--active"
            :style="{ background: editConfig.sidebarActiveBgColor }"
          >
            <div class="sidebar-preview-indicator" :style="{ background: editConfig.sidebarBgColor === '#001529' ? 'var(--sidebar-item-active-text)' : '#fff' }" />
            <div class="sidebar-preview-icon" />
            <span class="sidebar-preview-text">当前页面</span>
          </div>
          <div class="sidebar-preview-item">
            <div class="sidebar-preview-icon" />
            <span class="sidebar-preview-text">其他Tab</span>
          </div>
        </div>
        <div class="sidebar-preview-content">
          <div class="sidebar-preview-placeholder">内容区域</div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.section-header {
  margin-bottom: 24px;
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  color: rgba(0, 0, 0, 0.88);
  margin: 0 0 4px;
}

.section-desc {
  font-size: 14px;
  color: rgba(0, 0, 0, 0.45);
  margin: 0;
}

.config-items {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.config-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.config-label-area {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.config-label {
  font-size: 14px;
  color: rgba(0, 0, 0, 0.88);
  font-weight: 500;
}

.config-sublabel {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
}

.config-control {
  display: flex;
  align-items: center;
}

.slider-control {
  gap: 12px;
}

.slider-value {
  min-width: 40px;
  text-align: right;
  font-size: 13px;
  color: rgba(0, 0, 0, 0.65);
  font-variant-numeric: tabular-nums;
}

.color-control {
  gap: 8px;
}

.color-input {
  width: 32px;
  height: 32px;
  padding: 2px;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  cursor: pointer;
  background: none;
}

.color-input::-webkit-color-swatch-wrapper {
  padding: 2px;
}

.color-input::-webkit-color-swatch {
  border: none;
  border-radius: 4px;
}

.preview-label {
  font-size: 14px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.65);
  margin-bottom: 12px;
}

.sidebar-preview {
  display: flex;
  background: #fafafa;
  border-radius: 6px;
  overflow: hidden;
  height: 180px;
  border: 1px solid #f0f0f0;
}

.sidebar-preview-bar {
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  padding: 8px 0;
  transition: width 0.3s, background 0.3s;
}

.sidebar-preview-item {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  color: rgba(255, 255, 255, 0.65);
  font-size: 12px;
  position: relative;
  gap: 8px;
  cursor: default;
}

.sidebar-preview-item--active {
  color: #fff;
}

.sidebar-preview-indicator {
  position: absolute;
  left: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 3px;
  height: 16px;
  border-radius: 0 2px 2px 0;
}

.sidebar-preview-icon {
  width: 16px;
  height: 16px;
  border-radius: 3px;
  background: rgba(255, 255, 255, 0.25);
  flex-shrink: 0;
}

.sidebar-preview-text {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.sidebar-preview-content {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.sidebar-preview-placeholder {
  color: rgba(0, 0, 0, 0.25);
  font-size: 14px;
}
</style>
