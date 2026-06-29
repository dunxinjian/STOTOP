<!-- 左栏三层覆盖率面板：按 Layer1/Layer2/Layer3 折叠展示字段值覆盖率，支持搜索和交互 -->
<template>
  <div class="field-values-panel">
    <!-- 搜索框 -->
    <div class="panel-search">
      <a-input-search
        v-model:value="searchText"
        placeholder="搜索字段值..."
        size="small"
        allow-clear
      />
    </div>

    <!-- 空状态：未选批次 -->
    <div v-if="!analysis" class="empty-state">
      <EmptyState description="请先选择批次以加载字段分析数据" />
    </div>

    <!-- 空状态：已有分析但无可见层 -->
    <div v-else-if="visibleLayers.length === 0" class="empty-state">
      <EmptyState description="未配置匹配字段，请进入「规则设置」配置 Layer1/Layer2/Layer3 字段" />
    </div>

    <!-- 三层折叠面板 -->
    <a-collapse v-else v-model:activeKey="activeLayers" :bordered="false" class="layer-collapse">
      <template v-for="layer in visibleLayers" :key="layer.layerName">
        <a-collapse-panel :header="layerHeader(layer)">
          <!-- 覆盖率进度条 -->
          <div class="coverage-bar">
            <a-progress
              :percent="layerCoveragePercent(layer)"
              :stroke-color="layerCoveragePercent(layer) >= 100 ? 'var(--color-success)' : 'var(--color-info)'"
              size="small"
              :format="() => `${layer.coveredRows}/${layer.totalRows}行`"
            />
          </div>

          <!-- 未匹配值列表（置顶，⚠ 图标） -->
          <div v-if="filteredUnmatched(layer).length > 0" class="value-section">
            <div class="section-label unmatched-label">⚠ 未匹配 ({{ filteredUnmatched(layer).length }})</div>
            <div class="value-list">
              <div
                v-for="item in filteredUnmatched(layer)"
                :key="item.value"
                class="value-item unmatched"
              >
                <span
                  class="value-text clickable"
                  :title="`点击查看“${item.value || '(空)'}\u201d的记录明细`"
                  @click="onValueClick(layer.fieldName, item.value)"
                >{{ item.value || '(空)' }}</span>
                <span class="value-count">{{ item.count }}行</span>
                <a-dropdown
                  :trigger="['click']"
                  placement="bottomRight"
                  :getPopupContainer="popupContainer"
                  :overlayStyle="{ maxHeight: '300px', overflowY: 'auto' }"
                >
                  <a-button
                    type="text"
                    size="small"
                    class="add-rule-btn"
                    @click.stop
                  >
                    <PlusOutlined /> 加规则
                  </a-button>
                  <template #overlay>
                    <a-menu>
                      <a-menu-item key="__new__" @click="onAddRuleMenuClick(layer.layerName, item.value, item.count, '__new__')">
                        <PlusOutlined /> 新建规则组
                      </a-menu-item>
                      <a-menu-divider v-if="existingGroups.length" />
                      <a-menu-item
                        v-for="g in existingGroups"
                        :key="g.id"
                        @click="onAddRuleMenuClick(layer.layerName, item.value, item.count, g.id)"
                      >
                        加入：{{ g.name }}
                      </a-menu-item>
                    </a-menu>
                  </template>
                </a-dropdown>
              </div>
            </div>
          </div>

          <!-- 已匹配值列表（✓ 灰色） -->
          <div v-if="filteredMatched(layer).length > 0" class="value-section">
            <div class="section-label matched-label">✓ 已匹配 ({{ filteredMatched(layer).length }})</div>
            <div class="value-list">
              <div
                v-for="item in filteredMatched(layer)"
                :key="item.value"
                class="value-item matched"
                @click="$emit('selectMatched', { layerName: layer.layerName, value: item.value })"
              >
                <span class="value-text" :title="item.value">{{ item.value || '(空)' }}</span>
                <span class="value-count">{{ item.count }}行</span>
              </div>
            </div>
          </div>

          <!-- Layer3 特殊：未覆盖行摘要样本 -->
          <div v-if="layer.layerName === 'Layer3' && analysis?.unmatchedRowSample?.length" class="sample-section">
            <div class="section-label">未覆盖行摘要样本：</div>
            <div class="sample-list">
              <div v-for="(row, idx) in analysis.unmatchedRowSample.slice(0, 5)" :key="idx" class="sample-item">
                {{ Object.entries(row).filter(([k]) => !k.startsWith('FId') && !k.startsWith('F批次')).slice(0, 3).map(([,v]) => v).join(' | ') }}
              </div>
            </div>
          </div>
        </a-collapse-panel>
      </template>
    </a-collapse>
  </div>

  <!-- 暂存表记录明细弹窗 -->
  <StagingDetailModal
    v-model:visible="detailModalVisible"
    :tableName="props.stagingTable || ''"
    :fieldName="detailFieldName"
    :fieldValue="detailFieldValue"
    :batchId="props.batchId"
  />
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import type { FieldAnalysisResult, LayerAnalysis, FieldValueStat } from '../api'
import StagingDetailModal from './StagingDetailModal.vue'

const props = defineProps<{
  /** 字段分析结果 */
  analysis: FieldAnalysisResult | null
  /** 加载中 */
  loading?: boolean
  /** 现有规则组列表（用于「加入某组」菜单） */
  existingGroups?: Array<{ id: string; name: string }>
  /** 暂存表名称（用于点击查看明细） */
  stagingTable?: string
  /** 批次ID（用于点击查看明细） */
  batchId?: number
}>()

const emit = defineEmits<{
  /** 点击已匹配值，切换到对应规则组 */
  selectMatched: [payload: { layerName: string; value: string }]
  /** 为未匹配值添加规则：target = 'new' 表示新建组；其他为目标组 id */
  addRuleForValue: [payload: { layerName: string; value: string; count: number; target: string }]
}>()

const existingGroups = computed(() => props.existingGroups ?? [])

// Ant Design Vue dropdown 的 getPopupContainer 需要显式引用 document，避免 Volar 类型报错
const popupContainer = () => document.body

// ==================== 未匹配值点击查看明细 ====================
const detailModalVisible = ref(false)
const detailFieldName = ref('')
const detailFieldValue = ref('')

function onValueClick(fieldName: string, value: string) {
  detailFieldName.value = fieldName
  detailFieldValue.value = value
  detailModalVisible.value = true
}

function onAddRuleMenuClick(layerName: string, value: string, count: number, key: string) {
  const target = key === '__new__' ? 'new' : key
  emit('addRuleForValue', { layerName, value, count, target })
}

// ==================== 搜索 ====================
const searchText = ref('')

// ==================== 折叠面板状态 ====================
const activeLayers = ref<string[]>(['Layer1', 'Layer2', 'Layer3'])

// ==================== 层可见性：未配置的层不渲染 ====================
const visibleLayers = computed(() => {
  if (!props.analysis) return []
  return props.analysis.layers.filter(layer => {
    // 字段名为空则视为未配置，不渲染
    return !!layer.fieldName
  })
})

// ==================== 覆盖率计算 ====================
function layerCoveragePercent(layer: LayerAnalysis): number {
  if (layer.totalRows === 0) return 100
  return Math.round((layer.coveredRows / layer.totalRows) * 100)
}

function layerHeader(layer: LayerAnalysis): string {
  const pct = layerCoveragePercent(layer)
  const nameMap: Record<string, string> = {
    Layer1: 'Layer1 精确编码',
    Layer2: 'Layer2 分类',
    Layer3: 'Layer3 摘要关键词',
  }
  const label = nameMap[layer.layerName] || layer.layerName
  return `${label} — 覆盖 ${pct}%`
}

// ==================== 搜索过滤 ====================
function filteredUnmatched(layer: LayerAnalysis): FieldValueStat[] {
  if (!searchText.value) return layer.unmatchedValues
  const q = searchText.value.toLowerCase()
  return layer.unmatchedValues.filter(v => v.value.toLowerCase().includes(q))
}

function filteredMatched(layer: LayerAnalysis): FieldValueStat[] {
  if (!searchText.value) return layer.matchedValues
  const q = searchText.value.toLowerCase()
  return layer.matchedValues.filter(v => v.value.toLowerCase().includes(q))
}
</script>

<style lang="scss" scoped>
.field-values-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
  background: var(--bg-card);
  border-radius: 8px;
  border: 1px solid var(--border);
  overflow: hidden;
}

.panel-search {
  padding: 12px 12px 8px;
  border-bottom: 1px solid var(--border);
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  padding: 40px;
}

.layer-collapse {
  flex: 1;
  overflow-y: auto;

  :deep(.ant-collapse-header) {
    padding: 10px 12px !important;
    font-size: 13px;
    font-weight: 500;
  }
  :deep(.ant-collapse-content-box) {
    padding: 8px 12px !important;
  }
}

.coverage-bar {
  margin-bottom: 8px;

  :deep(.ant-progress.ant-progress-line) {
    display: flex;
    align-items: center;
    width: 100%;
    margin-bottom: 0;
  }
  :deep(.ant-progress-outer) {
    flex: 1;
    min-width: 0;
    margin-inline-end: 8px !important;
    padding-inline-end: 0 !important;
  }
  :deep(.ant-progress-text) {
    flex-shrink: 0;
    width: auto;
    min-width: auto;
    white-space: nowrap;
    text-align: right;
  }
}

.value-section {
  margin-bottom: 8px;
}

.section-label {
  font-size: 11px;
  font-weight: 600;
  margin-bottom: 4px;
  padding-left: 2px;

  &.unmatched-label { color: var(--color-warning-text); }
  &.matched-label { color: var(--color-success-text); }
}

.value-list {
  /* 高度自适应，由外层 .layer-collapse 统一滚动 */
}

.value-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 4px 8px;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.15s;
  gap: 6px;

  &:hover {
    background: var(--bg-muted);
    .add-rule-btn { opacity: 1; }
  }

  &.unmatched {
    cursor: default;
    .value-text.clickable {
      color: var(--color-danger-text);
      font-weight: 500;
      cursor: pointer;
      &:hover {
        text-decoration: underline;
        color: var(--color-danger-text);
      }
    }
  }

  &.matched {
    .value-text { color: var(--text-3); }
  }
}

.add-rule-btn {
  opacity: 0;
  transition: opacity 0.15s;
  flex-shrink: 0;
  padding: 0 6px !important;
  height: 22px;
  font-size: 12px;
}

.value-text {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12px;
}

.value-count {
  font-size: 11px;
  color: var(--text-3);
  white-space: nowrap;
  margin-left: 8px;
}

.sample-section {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px dashed var(--border);
}

.sample-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.sample-item {
  font-size: 11px;
  color: var(--text-3);
  padding: 2px 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
