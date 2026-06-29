<script setup lang="ts">
import { computed } from 'vue'
import { TABLE_DENSITY_LABELS, TABLE_DENSITY_MAP, type ThemeConfig, type TableRowDensity } from '@/stores/theme'

const props = defineProps<{
  editConfig: ThemeConfig
  previewTheme: any
}>()

const densityOptions = computed(() => {
  return Object.entries(TABLE_DENSITY_LABELS).map(([key, label]) => ({
    key: key as TableRowDensity,
    label,
  }))
})

const densityDescriptions: Record<TableRowDensity, string> = {
  compact: '紧凑模式，适合数据密集型页面，上下内边距 4px',
  standard: '标准模式（默认），上下内边距 6px',
  relaxed: '宽松模式，适合阅读型页面，上下内边距 8px',
  comfortable: '舒适模式，行间距较大，上下内边距 12px',
}

const densityDescription = computed(() => densityDescriptions[props.editConfig.tableRowDensity as TableRowDensity] || '')

const previewTableData = [
  { key: '1', name: '张三', department: '技术部', status: '在职' },
  { key: '2', name: '李四', department: '财务部', status: '在职' },
  { key: '3', name: '王五', department: '人事部', status: '离职' },
]

const previewColumns = [
  { title: '姓名', dataIndex: 'name', key: 'name' },
  { title: '部门', dataIndex: 'department', key: 'department' },
  { title: '状态', dataIndex: 'status', key: 'status' },
]

const previewTableStyle = computed(() => {
  const d = TABLE_DENSITY_MAP[props.editConfig.tableRowDensity as TableRowDensity]
  if (!d) return ''
  return `--preview-table-block: ${d.block}px; --preview-table-inline: ${d.inline}px;`
})
</script>

<template>
  <div class="config-section">
    <div class="section-header">
      <h3 class="section-title">表格密度</h3>
      <p class="section-desc">调整表格行高密度，控制数据展示的紧凑程度</p>
    </div>

    <div class="density-cards">
      <div
        v-for="opt in densityOptions"
        :key="opt.key"
        class="density-card"
        :class="{ active: editConfig.tableRowDensity === opt.key }"
        @click="editConfig.tableRowDensity = opt.key"
      >
        <div class="density-card-icon">
          <div class="density-lines" :class="'density-' + opt.key">
            <span></span><span></span><span></span>
          </div>
        </div>
        <span class="density-card-label">{{ opt.label }}</span>
      </div>
    </div>

    <div class="density-desc" v-if="densityDescription">
      <a-typography-text type="secondary">{{ densityDescription }}</a-typography-text>
    </div>

    <a-divider style="margin: 24px 0 20px" />

    <div class="preview-area">
      <div class="preview-label">预览（当前密度：{{ TABLE_DENSITY_LABELS[editConfig.tableRowDensity as TableRowDensity] }}）</div>
      <a-config-provider :theme="previewTheme">
        <div class="preview-content">
          <a-table
            :columns="previewColumns"
            :data-source="previewTableData"
            :pagination="false"
            size="small"
            bordered
            :style="previewTableStyle"
          />
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

.density-cards {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
}

.density-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 16px 24px;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  min-width: 100px;
}

.density-card:hover {
  border-color: var(--color-primary-border);
}

.density-card.active {
  border-color: var(--border-strong);
  background: var(--bg-muted);
}

.density-card.active .density-card-label {
  color: var(--text-1);
  font-weight: 500;
}

.density-card-icon {
  width: 40px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.density-lines {
  display: flex;
  flex-direction: column;
  width: 100%;
  align-items: stretch;
}

.density-lines span {
  height: 2px;
  background: rgba(0, 0, 0, 0.25);
  border-radius: 1px;
}

.density-compact span { margin-bottom: 3px; }
.density-standard span { margin-bottom: 5px; }
.density-relaxed span { margin-bottom: 7px; }
.density-comfortable span { margin-bottom: 9px; }

.density-lines span:last-child { margin-bottom: 0; }

.density-card.active .density-lines span {
  background: var(--text-1);
}

.density-card-label {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.65);
}

.density-desc {
  margin-top: 16px;
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
</style>
