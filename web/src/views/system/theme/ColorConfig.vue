<script setup lang="ts">
import { THEME_PRESETS, type ThemeConfig, type ThemePreset } from '@/stores/theme'

const props = defineProps<{
  editConfig: ThemeConfig
  previewTheme: any
}>()

const presetKeys = Object.keys(THEME_PRESETS) as ThemePreset[]

function pickPreset(p: ThemePreset) {
  props.editConfig.themePreset = p
  props.editConfig.colorPrimary = THEME_PRESETS[p].shades.primary
}

const colorItems = [
  { key: 'colorSuccess', label: '成功色', desc: '成功状态、正向操作反馈' },
  { key: 'colorWarning', label: '警告色', desc: '警告状态、需要注意的操作' },
  { key: 'colorError', label: '错误色', desc: '错误状态、危险操作反馈' },
  { key: 'colorInfo', label: '信息色', desc: '一般信息提示、辅助说明' },
]
</script>

<template>
  <div class="config-section">
    <div class="section-header">
      <h3 class="section-title">色彩配置</h3>
      <p class="section-desc">选择强调色预设；状态色可按需在「高级」中自定义</p>
    </div>

    <!-- 强调色预设 -->
    <div class="preset-label">强调色预设</div>
    <div class="preset-grid">
      <div
        v-for="key in presetKeys"
        :key="key"
        class="preset-card"
        :class="{ active: editConfig.themePreset === key }"
        role="button"
        tabindex="0"
        @click="pickPreset(key)"
        @keydown.enter="pickPreset(key)"
      >
        <div class="preset-bars">
          <span class="bar" :style="{ height: '60%', background: THEME_PRESETS[key].shades.primary }" />
          <span class="bar" style="height: 100%" />
          <span class="bar" style="height: 45%" />
          <span class="bar" style="height: 78%" />
        </div>
        <div class="preset-foot">
          <span class="preset-dot" :style="{ background: THEME_PRESETS[key].shades.primary }" />
          <span class="preset-name">{{ THEME_PRESETS[key].label }}</span>
          <span v-if="editConfig.themePreset === key" class="preset-check">已选</span>
        </div>
      </div>
    </div>

    <!-- 高级：自定义状态色板 -->
    <a-collapse ghost class="advanced-collapse">
      <a-collapse-panel key="advanced" header="高级：自定义状态色板">
        <div class="config-items">
          <div v-for="item in colorItems" :key="item.key" class="config-row">
            <div class="config-label-area">
              <span class="config-label">{{ item.label }}</span>
              <span class="config-sublabel">{{ item.desc }}</span>
            </div>
            <div class="config-control color-control">
              <input type="color" v-model="(editConfig as any)[item.key]" class="color-input" />
              <a-input
                v-model:value="(editConfig as any)[item.key]"
                size="small"
                style="width: 100px"
                :maxlength="7"
              />
            </div>
          </div>
        </div>
      </a-collapse-panel>
    </a-collapse>

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
              <a-button type="text">文本按钮</a-button>
              <a-button type="link">链接按钮</a-button>
              <a-button danger type="primary">危险按钮</a-button>
            </a-space>
          </div>
          <div class="preview-row">
            <a-space>
              <a-tag color="processing">处理中</a-tag>
              <a-tag color="success">成功</a-tag>
              <a-tag color="warning">警告</a-tag>
              <a-tag color="error">错误</a-tag>
              <a-tag color="default">默认</a-tag>
            </a-space>
          </div>
          <div class="preview-row">
            <a-space direction="vertical" style="width: 100%">
              <a-alert message="信息提示" type="info" show-icon />
              <a-alert message="成功提示" type="success" show-icon />
              <a-alert message="警告提示" type="warning" show-icon />
              <a-alert message="错误提示" type="error" show-icon />
            </a-space>
          </div>
        </div>
      </a-config-provider>
    </div>
  </div>
</template>

<style scoped>
.section-header {
  margin-bottom: 20px;
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

.preset-label {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.65);
  font-weight: 500;
  margin-bottom: 10px;
}

.preset-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 220px));
  gap: 12px;
  margin-bottom: 8px;
}

.preset-card {
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-lg);
  padding: 12px;
  cursor: pointer;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.preset-card:hover {
  border-color: var(--color-primary);
}

.preset-card.active {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 1px var(--color-primary);
}

.preset-bars {
  display: flex;
  align-items: flex-end;
  gap: 5px;
  height: 30px;
  margin-bottom: 10px;
}

.preset-bars .bar {
  flex: 1;
  border-radius: 2px;
  background: var(--bg-muted);
}

.preset-foot {
  display: flex;
  align-items: center;
  gap: 8px;
}

.preset-dot {
  width: 14px;
  height: 14px;
  border-radius: 4px;
  flex-shrink: 0;
}

.preset-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-1);
}

.preset-check {
  margin-left: auto;
  font-size: 12px;
  color: var(--color-primary);
}

.advanced-collapse {
  margin-top: 4px;
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

.color-control {
  gap: 8px;
}

.color-input {
  width: 32px;
  height: 32px;
  padding: 2px;
  border: 1px solid var(--border-strong);
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

.preview-content {
  background: var(--bg-muted);
  border-radius: 6px;
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}
</style>
