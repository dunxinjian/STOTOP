<script setup lang="ts">
import type { ThemeConfig } from '@/stores/theme'

defineProps<{
  editConfig: ThemeConfig
  previewTheme: any
}>()

const switchItems = [
  { key: 'compactMode', label: '紧凑模式', desc: '减小组件间距和尺寸，在有限空间展示更多内容' },
  { key: 'wireframe', label: '线框风格', desc: '组件使用线框风格渲染，更简洁素雅' },
] as const
</script>

<template>
  <div class="config-section">
    <div class="section-header">
      <h3 class="section-title">模式配置</h3>
      <p class="section-desc">切换紧凑、线框风格（主题色在「色彩配置」中选择）</p>
    </div>

    <div class="config-items">
      <div v-for="item in switchItems" :key="item.key" class="mode-row">
        <div class="config-label-area">
          <span class="config-label">{{ item.label }}</span>
          <span class="config-sublabel">{{ item.desc }}</span>
        </div>
        <a-switch v-model:checked="(editConfig as any)[item.key]" />
      </div>
    </div>

    <a-divider style="margin: 24px 0 20px" />

    <div class="preview-area">
      <div class="preview-label">预览</div>
      <a-config-provider :theme="previewTheme">
        <div class="preview-content">
          <div class="preview-row">
            <a-space wrap>
              <a-button type="primary">主要按钮</a-button>
              <a-button>默认按钮</a-button>
              <a-button type="dashed">虚线按钮</a-button>
            </a-space>
          </div>
          <div class="preview-row">
            <a-space>
              <a-input placeholder="输入框" style="width: 160px" />
              <a-select placeholder="选择器" style="width: 120px">
                <a-select-option value="1">启用</a-select-option>
                <a-select-option value="0">禁用</a-select-option>
              </a-select>
            </a-space>
          </div>
          <div class="preview-row">
            <a-space>
              <a-switch checked />
              <a-switch />
              <a-checkbox checked>选中</a-checkbox>
              <a-checkbox>未选</a-checkbox>
              <a-radio checked>选项A</a-radio>
              <a-radio>选项B</a-radio>
            </a-space>
          </div>
          <div class="preview-row">
            <a-progress :percent="75" />
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
  gap: 0;
}

.mode-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 0;
  border-bottom: 1px solid var(--border);
}

.mode-row:first-child {
  padding-top: 0;
}

.mode-row:last-child {
  border-bottom: none;
  padding-bottom: 0;
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

.preview-label {
  font-size: 14px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.65);
  margin-bottom: 12px;
}

.preview-content {
  background: var(--bg-muted);
  border-radius: 6px;
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}
</style>
