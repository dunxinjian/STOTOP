<script setup lang="ts">
import { ref, reactive, watch } from 'vue'
import { message } from 'ant-design-vue'
import { theme as antTheme } from 'ant-design-vue'
import { useThemeStore, type ThemeConfig, TABLE_DENSITY_MAP, type TableRowDensity } from '@/stores/theme'

import PageHeader from '@/components/PageHeader.vue'
import ColorConfig from './theme/ColorConfig.vue'
import SizeConfig from './theme/SizeConfig.vue'
import ModeConfig from './theme/ModeConfig.vue'
import TableConfig from './theme/TableConfig.vue'
import SpacingConfig from './theme/SpacingConfig.vue'
import SidebarConfig from './theme/SidebarConfig.vue'

const themeStore = useThemeStore()

// 左侧导航 Tab
const activeTab = ref<'color' | 'size' | 'mode' | 'table' | 'spacing' | 'sidebar'>('color')

const tabItems = [
  { key: 'color' as const, label: '色彩配置', icon: '🎨' },
  { key: 'size' as const, label: '尺寸配置', icon: '📐' },
  { key: 'mode' as const, label: '模式配置', icon: '🌗' },
  { key: 'table' as const, label: '表格密度', icon: '📊' },
  { key: 'spacing' as const, label: '页面间距', icon: '📄' },
  { key: 'sidebar' as const, label: '侧边栏', icon: '📌' },
]

// 本地编辑副本，不直接修改 store
const editConfig = reactive<ThemeConfig>({ ...themeStore.themeConfig })

// 监听 store 变化同步到本地
watch(() => themeStore.themeConfig, (val) => {
  Object.assign(editConfig, val)
}, { deep: true })

// 预览用的 antd theme 配置
const previewTheme = ref<any>({})
function updatePreview() {
  const algorithms: any[] = []
  algorithms.push(antTheme.defaultAlgorithm)
  if (editConfig.compactMode) {
    algorithms.push(antTheme.compactAlgorithm)
  }
  const density = TABLE_DENSITY_MAP[editConfig.tableRowDensity as TableRowDensity] || TABLE_DENSITY_MAP.standard
  previewTheme.value = {
    token: {
      colorPrimary: editConfig.colorPrimary,
      colorSuccess: editConfig.colorSuccess,
      colorWarning: editConfig.colorWarning,
      colorError: editConfig.colorError,
      colorInfo: editConfig.colorInfo,
      borderRadius: editConfig.borderRadius,
      fontSize: editConfig.fontSize,
      sizeStep: editConfig.sizeStep,
      sizeUnit: editConfig.sizeUnit,
      wireframe: editConfig.wireframe,
      marginXS: editConfig.marginXS,
      marginSM: editConfig.marginSM,
      margin: editConfig.margin,
      marginMD: editConfig.marginMD,
      marginLG: editConfig.marginLG,
    },
    components: {
      Table: {
        headerBg: '#fafafa',
        headerColor: 'rgba(0, 0, 0, 0.85)',
        rowHoverBg: '#f5f7fa',
        cellPaddingBlock: density.block,
        cellPaddingBlockSM: density.blockSM,
        cellPaddingBlockMD: density.block,
        cellPaddingInline: density.inline,
        cellPaddingInlineSM: density.inlineSM,
        cellPaddingInlineMD: density.inline,
      },
    },
    algorithm: algorithms,
  }
}
// 初始化预览
updatePreview()

// 深度监听编辑配置变化并更新预览
watch(editConfig, () => {
  updatePreview()
}, { deep: true })

const saving = ref(false)

async function handleSave() {
  saving.value = true
  try {
    // 先同步到 store
    Object.assign(themeStore.themeConfig, editConfig)
    await themeStore.saveTheme()
    message.success('主题配置保存成功')
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

function handleReset() {
  themeStore.resetTheme()
  Object.assign(editConfig, themeStore.themeConfig)
  message.info('已恢复默认主题')
}
</script>

<template>
  <div class="page-container theme-config-page">
    <PageHeader title="主题配置">
      <template #right>
        <a-space>
          <a-button @click="handleReset">恢复默认</a-button>
          <a-button type="primary" :loading="saving" @click="handleSave">保存配置</a-button>
        </a-space>
      </template>
    </PageHeader>

    <div class="theme-settings-card">
      <div class="settings-layout">
        <!-- 左侧导航 -->
        <div class="settings-nav">
          <div
            v-for="tab in tabItems"
            :key="tab.key"
            class="nav-item"
            :class="{ active: activeTab === tab.key }"
            @click="activeTab = tab.key"
          >
            <span class="nav-icon">{{ tab.icon }}</span>
            <span class="nav-label">{{ tab.label }}</span>
          </div>
        </div>

        <!-- 右侧内容区 -->
        <div class="settings-content">
          <ColorConfig v-show="activeTab === 'color'" :edit-config="editConfig" :preview-theme="previewTheme" />
          <SizeConfig v-show="activeTab === 'size'" :edit-config="editConfig" :preview-theme="previewTheme" />
          <ModeConfig v-show="activeTab === 'mode'" :edit-config="editConfig" :preview-theme="previewTheme" />
          <TableConfig v-show="activeTab === 'table'" :edit-config="editConfig" :preview-theme="previewTheme" />
          <SpacingConfig v-show="activeTab === 'spacing'" :edit-config="editConfig" :preview-theme="previewTheme" />
          <SidebarConfig v-show="activeTab === 'sidebar'" :edit-config="editConfig" :preview-theme="previewTheme" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.theme-config-page {
  overflow: auto !important;
}

.theme-settings-card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 8px;
  box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.03), 0 1px 6px -1px rgba(0, 0, 0, 0.02), 0 2px 4px 0 rgba(0, 0, 0, 0.02);
  overflow: hidden;
}

.settings-layout {
  display: flex;
  min-height: calc(100vh - 160px);
}

/* 左侧导航 */
.settings-nav {
  width: 160px;
  flex-shrink: 0;
  border-right: 1px solid var(--border);
  padding: 16px 0;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 20px;
  cursor: pointer;
  font-size: 14px;
  color: rgba(0, 0, 0, 0.65);
  transition: all 0.2s;
  position: relative;
  border-left: 3px solid transparent;
}

.nav-item:hover {
  color: var(--color-primary);
  background: var(--color-primary-light);
}

.nav-item.active {
  color: var(--text-1);
  background: var(--color-primary-light);
  border-left-color: var(--color-primary);
  font-weight: 500;
}

.nav-icon {
  font-size: 16px;
  line-height: 1;
}

.nav-label {
  line-height: 1.4;
}

/* 右侧内容区 */
.settings-content {
  flex: 1;
  padding: 24px 32px;
  min-width: 0;
}
</style>
