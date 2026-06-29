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
      <h3 class="section-title">尺寸配置</h3>
      <p class="section-desc">调整组件的圆角、字号、尺寸基数等全局尺寸参数</p>
    </div>

    <div class="config-items">
      <div class="config-row">
        <div class="config-label-area">
          <span class="config-label">圆角</span>
          <span class="config-sublabel">组件的圆角大小，影响按钮、卡片等</span>
        </div>
        <div class="config-control slider-control">
          <a-slider v-model:value="editConfig.borderRadius" :min="0" :max="20" style="width: 200px" />
          <span class="slider-value">{{ editConfig.borderRadius }}px</span>
        </div>
      </div>

      <div class="config-row">
        <div class="config-label-area">
          <span class="config-label">字号</span>
          <span class="config-sublabel">基础字体大小</span>
        </div>
        <div class="config-control slider-control">
          <a-slider v-model:value="editConfig.fontSize" :min="12" :max="18" style="width: 200px" />
          <span class="slider-value">{{ editConfig.fontSize }}px</span>
        </div>
      </div>

      <div class="config-row">
        <div class="config-label-area">
          <span class="config-label">尺寸步长</span>
          <span class="config-sublabel">组件尺寸递增步长</span>
        </div>
        <div class="config-control slider-control">
          <a-slider v-model:value="editConfig.sizeStep" :min="2" :max="8" style="width: 200px" />
          <span class="slider-value">{{ editConfig.sizeStep }}</span>
        </div>
      </div>

      <div class="config-row">
        <div class="config-label-area">
          <span class="config-label">尺寸单位</span>
          <span class="config-sublabel">组件基础尺寸单位</span>
        </div>
        <div class="config-control slider-control">
          <a-slider v-model:value="editConfig.sizeUnit" :min="2" :max="8" style="width: 200px" />
          <span class="slider-value">{{ editConfig.sizeUnit }}</span>
        </div>
      </div>
    </div>

    <a-divider style="margin: 24px 0 20px" />

    <div class="preview-area">
      <div class="preview-label">预览</div>
      <a-config-provider :theme="previewTheme">
        <div class="preview-content">
          <div class="preview-row">
            <span class="preview-hint">不同圆角按钮</span>
            <a-space wrap>
              <a-button type="primary">主要按钮</a-button>
              <a-button>默认按钮</a-button>
              <a-button type="dashed">虚线按钮</a-button>
            </a-space>
          </div>
          <div class="preview-row">
            <span class="preview-hint">文字尺寸示例</span>
            <div>
              <p style="margin: 0 0 4px">这是一段正文内容，展示当前字号效果。</p>
              <a-typography-text type="secondary">这是辅助说明文字。</a-typography-text>
            </div>
          </div>
          <div class="preview-row">
            <span class="preview-hint">表单控件</span>
            <a-space>
              <a-input placeholder="输入框" style="width: 140px" />
              <a-select placeholder="选择器" style="width: 120px">
                <a-select-option value="1">选项一</a-select-option>
              </a-select>
              <a-button type="primary">查询</a-button>
            </a-space>
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
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.preview-hint {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
  margin-bottom: 8px;
  display: block;
}
</style>
