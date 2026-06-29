<script setup lang="ts">
import type { ThemeConfig } from '@/stores/theme'

defineProps<{
  editConfig: ThemeConfig
  previewTheme: any
}>()

const marginItems = [
  { key: 'marginXS', label: 'marginXS', desc: '超小间距', min: 0, max: 24 },
  { key: 'marginSM', label: 'marginSM', desc: '小间距', min: 0, max: 24 },
  { key: 'margin', label: 'margin', desc: '标准间距', min: 0, max: 32 },
  { key: 'marginMD', label: 'marginMD', desc: '中等间距', min: 0, max: 40 },
  { key: 'marginLG', label: 'marginLG', desc: '大间距', min: 0, max: 48 },
]
</script>

<template>
  <div class="config-section">
    <div class="section-header">
      <h3 class="section-title">页面间距</h3>
      <p class="section-desc">调整页面水平/垂直间距及间距系列参数</p>
    </div>

    <div class="config-items">
      <!-- 页面间距 -->
      <div class="config-row">
        <div class="config-label-area">
          <span class="config-label">水平间距</span>
          <span class="config-sublabel">页面内容区左右内边距</span>
        </div>
        <div class="config-control slider-control">
          <a-slider v-model:value="editConfig.pagePaddingX" :min="0" :max="40" :step="2" style="width: 200px" />
          <span class="slider-value">{{ editConfig.pagePaddingX }}px</span>
        </div>
      </div>

      <div class="config-row">
        <div class="config-label-area">
          <span class="config-label">垂直间距</span>
          <span class="config-sublabel">页面内容区上下内边距</span>
        </div>
        <div class="config-control slider-control">
          <a-slider v-model:value="editConfig.pagePaddingY" :min="0" :max="40" :step="2" style="width: 200px" />
          <span class="slider-value">{{ editConfig.pagePaddingY }}px</span>
        </div>
      </div>

      <a-divider style="margin: 16px 0" />

      <!-- 间距系列 -->
      <div class="subsection-title">间距系列</div>

      <div v-for="item in marginItems" :key="item.key" class="config-row">
        <div class="config-label-area">
          <span class="config-label">{{ item.label }}</span>
          <span class="config-sublabel">{{ item.desc }}</span>
        </div>
        <div class="config-control slider-control">
          <a-slider v-model:value="(editConfig as any)[item.key]" :min="item.min" :max="item.max" style="width: 200px" />
          <span class="slider-value">{{ (editConfig as any)[item.key] }}px</span>
        </div>
      </div>
    </div>

    <a-divider style="margin: 24px 0 20px" />

    <div class="preview-area">
      <div class="preview-label">预览</div>
      <a-config-provider :theme="previewTheme">
        <div class="preview-content">
          <div class="spacing-demo">
            <div class="spacing-row">
              <div class="spacing-box" :style="{ padding: editConfig.marginXS + 'px' }">
                <div class="spacing-inner">XS: {{ editConfig.marginXS }}px</div>
              </div>
              <div class="spacing-box" :style="{ padding: editConfig.marginSM + 'px' }">
                <div class="spacing-inner">SM: {{ editConfig.marginSM }}px</div>
              </div>
              <div class="spacing-box" :style="{ padding: (editConfig as any).margin + 'px' }">
                <div class="spacing-inner">M: {{ (editConfig as any).margin }}px</div>
              </div>
            </div>
            <div class="spacing-row">
              <div class="spacing-box" :style="{ padding: editConfig.marginMD + 'px' }">
                <div class="spacing-inner">MD: {{ editConfig.marginMD }}px</div>
              </div>
              <div class="spacing-box" :style="{ padding: editConfig.marginLG + 'px' }">
                <div class="spacing-inner">LG: {{ editConfig.marginLG }}px</div>
              </div>
            </div>
          </div>
        </div>
      </a-config-provider>
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

.subsection-title {
  font-size: 14px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.65);
  margin-bottom: 4px;
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

.preview-label {
  font-size: 14px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.65);
  margin-bottom: 12px;
}

.preview-content {
  background: #fafafa;
  border-radius: 6px;
  padding: 24px;
}

.spacing-demo {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.spacing-row {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.spacing-box {
  background: var(--bg-muted);
  border: 1px dashed var(--border);
  border-radius: 4px;
  min-width: 80px;
  flex: 1;
}

.spacing-inner {
  background: #fff;
  border-radius: 2px;
  padding: 8px 12px;
  font-size: 12px;
  color: rgba(0, 0, 0, 0.65);
  text-align: center;
  white-space: nowrap;
}
</style>
