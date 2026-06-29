<!-- 右栏规则组配置：规则组名称、聚合方式、分录行概览列表、快捷模板、底部操作栏 -->
<template>
  <div class="rule-config-panel">
    <!-- 顶部：当前规则组名称 -->
    <div class="panel-header">
      <span v-if="currentGroup" class="current-group-name">
        {{ currentGroup.name }}
      </span>
      <span v-else class="no-group-hint">请在左侧选择规则组</span>
    </div>

    <!-- 快捷模板 -->
    <div v-if="currentGroup && currentGroup.lines.length === 0" class="quick-templates">
      <span class="template-label">快捷创建：</span>
      <a-button size="small" @click="applyTemplate('one_one')">一借一贷</a-button>
      <a-button size="small" @click="applyTemplate('one_many')">一借多贷</a-button>
      <a-button size="small" @click="applyTemplate('many_one')">多借一贷</a-button>
      <a-button size="small" @click="applyTemplate('custom')">自定义</a-button>
    </div>

    <!-- 分录行概览列表 -->
    <div class="entry-list">
      <div v-if="!currentGroup" class="empty-state">
        <EmptyState description="请选择或新建规则组" />
      </div>
      <template v-else>
        <div
          v-for="(entry, idx) in currentGroup.lines"
          :key="entry.id"
          :ref="(el) => { if (el) entryRefs[entry.id] = el as any }"
          class="entry-item"
          :class="{ 'is-selected': selectedLineId === entry.id, 'is-disabled': entry.status === 0, 'has-error': hasLineError(entry.id) }"
          @click="selectLine(entry.id)"
        >
          <div class="entry-summary">
            <StatusTag :type="entry.direction === '借' ? 'info' : 'success'" size="small">
              {{ entry.direction }}
            </StatusTag>
            <span class="entry-index">行{{ idx + 1 }}</span>
            <span class="entry-account">
              {{ entry.accountMode === 'fixed' ? (entry.accountId ? `科目#${entry.accountId}` : '未设科目') : `动态: ${entry.accountMatchField || '?'}` }}
            </span>
            <span class="entry-amount">{{ entry.amountField || '未设金额' }}</span>
            <a-button
              v-if="store.formData.ruleGroups.length >= 2"
              type="text"
              size="small"
              title="分发到其他组..."
              @click.stop="openDistributeDialog(entry)"
            >
              <SendOutlined />
            </a-button>
            <a-button type="text" size="small" danger @click.stop="store.removeLine(entry.id)">
              <DeleteOutlined />
            </a-button>
          </div>
          <!-- 展开编辑区 -->
          <div v-if="selectedLineId === entry.id" class="entry-detail" @click.stop>
            <EntryLineEditor
              :line="entry"
              :fields="fields"
              :account-tree-data="store.accountTreeData"
              :account-set-id="store.formData.accountSetId"
              :batch-values="batchValues"
              :sample-row="sampleRow"
              @update:line="(patch) => store.updateLine(entry.id, patch)"
            />
          </div>
        </div>
      </template>
    </div>

    <!-- 分发到其他组弹窗 -->
    <DistributeLineDialog
      v-model:visible="distributeDialogVisible"
      :line-id="distributeLineId"
      :line-direction="distributeLineDirection"
    />

    <!-- 底部固定操作栏 -->
    <div class="panel-footer">
      <div class="footer-left">
        <span v-if="currentGroup" class="balance-hint" :class="balanceClass">
          {{ balanceHint }}
        </span>
      </div>
      <div class="footer-actions">
        <a-button
          v-if="currentGroup"
          size="small"
          @click="store.addLine()"
        >
          <PlusOutlined /> 添加分录
        </a-button>
        <a-button size="small" @click="$emit('dryRun')">
          <ExperimentOutlined /> 试运行
        </a-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, watch } from 'vue'
import { PlusOutlined, DeleteOutlined, ExperimentOutlined, SendOutlined } from '@ant-design/icons-vue'
import { useAutoVoucherRuleStore, type EntryLine } from '@/stores/autoVoucherRule'
import EntryLineEditor from './EntryLineEditor.vue'
import DistributeLineDialog from './DistributeLineDialog.vue'
import StatusTag from '@/components/StatusTag.vue'

const store = useAutoVoucherRuleStore()

const props = defineProps<{
  /** 可用字段列表 */
  fields: string[]
  /** 批次中的值（用于智能提示） */
  batchValues?: string[]
  /** 预览用第一行数据 */
  sampleRow?: Record<string, any> | null
}>()

defineEmits<{
  dryRun: []
}>()

// ==================== 状态 ====================
const selectedLineId = computed(() => store.selectedLineId)
const currentGroup = computed(() => store.currentGroup)

// ==================== 校验错误状态 ====================
const entryRefs = ref<Record<string, HTMLElement>>({})

function hasLineError(lineId: string): boolean {
  return !!store.entryErrors[lineId] && Object.keys(store.entryErrors[lineId]).length > 0
}

// 监听 selectedLineId 变化，如果有错误则滚动到对应行
watch(() => store.selectedLineId, async (newId) => {
  if (newId && hasLineError(newId)) {
    await nextTick()
    const el = entryRefs.value[newId]
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
    }
  }
})

// ==================== 分发弹窗状态 ====================
const distributeDialogVisible = ref(false)
const distributeLineId = ref('')
const distributeLineDirection = ref<'借' | '贷'>('借')

function openDistributeDialog(line: EntryLine) {
  distributeLineId.value = line.id
  distributeLineDirection.value = line.direction
  distributeDialogVisible.value = true
}

// ==================== 借贷平衡指示 ====================
const balanceHint = computed(() => {
  if (!currentGroup.value) return ''
  const lines = currentGroup.value.lines.filter(l => l.status === 1)
  const debitCount = lines.filter(l => l.direction === '借').length
  const creditCount = lines.filter(l => l.direction === '贷').length
  return `${debitCount}借 ${creditCount}贷`
})

const balanceClass = computed(() => {
  if (!currentGroup.value) return ''
  const lines = currentGroup.value.lines.filter(l => l.status === 1)
  const hasDebit = lines.some(l => l.direction === '借')
  const hasCredit = lines.some(l => l.direction === '贷')
  if (!hasDebit || !hasCredit) return 'unbalanced'
  return 'balanced'
})

// ==================== 行选择 ====================
function selectLine(lineId: string) {
  if (store.selectedLineId === lineId) {
    store.selectedLineId = null
  } else {
    store.selectedLineId = lineId
  }
}

// ==================== 快捷模板 ====================
function applyTemplate(type: string) {
  if (!currentGroup.value) return
  // 清除已有行
  store.removeLines(currentGroup.value.lines.map(l => l.id))

  switch (type) {
    case 'one_one':
      // 一借一贷
      store.addLine() // 默认为借
      if (currentGroup.value.lines.length === 1) {
        store.updateLine(currentGroup.value.lines[0].id, { direction: '借' })
      }
      store.addLine()
      if (currentGroup.value.lines.length === 2) {
        store.updateLine(currentGroup.value.lines[1].id, { direction: '贷' })
      }
      break
    case 'one_many':
      // 一借多贷
      store.addLine()
      if (currentGroup.value.lines.length >= 1) {
        store.updateLine(currentGroup.value.lines[0].id, { direction: '借' })
      }
      store.addLine()
      if (currentGroup.value.lines.length >= 2) {
        store.updateLine(currentGroup.value.lines[1].id, { direction: '贷' })
      }
      store.addLine()
      if (currentGroup.value.lines.length >= 3) {
        store.updateLine(currentGroup.value.lines[2].id, { direction: '贷' })
      }
      break
    case 'many_one':
      // 多借一贷
      store.addLine()
      if (currentGroup.value.lines.length >= 1) {
        store.updateLine(currentGroup.value.lines[0].id, { direction: '借' })
      }
      store.addLine()
      if (currentGroup.value.lines.length >= 2) {
        store.updateLine(currentGroup.value.lines[1].id, { direction: '借' })
      }
      store.addLine()
      if (currentGroup.value.lines.length >= 3) {
        store.updateLine(currentGroup.value.lines[2].id, { direction: '贷' })
      }
      break
    case 'custom':
      // 自定义：添加一个空行
      store.addLine()
      break
  }
}
</script>

<style lang="scss" scoped>
.rule-config-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
  background: var(--bg-card);
  border-radius: 8px;
  border: 1px solid var(--border);
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border);
}

.current-group-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-1);
}

.no-group-hint {
  font-size: 13px;
  color: var(--text-3);
}

.quick-templates {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: var(--bg-muted);
  border-bottom: 1px solid var(--border);
}

.template-label {
  font-size: 12px;
  color: var(--text-3);
  white-space: nowrap;
}

.entry-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  padding: 40px;
}

.entry-item {
  border: 1px solid var(--border);
  border-radius: 6px;
  margin-bottom: 8px;
  cursor: pointer;
  transition: border-color 0.2s;

  &:hover {
    border-color: var(--color-primary);
  }

  &.is-selected {
    border-color: var(--border-strong);
    box-shadow: 0 0 0 1px var(--border-strong);
  }

  &.is-disabled {
    opacity: 0.5;
  }

  &.has-error {
    border-color: var(--color-danger);
    background: var(--color-danger-light);

    &:hover {
      border-color: var(--color-danger);
    }
  }

  &.has-error.is-selected {
    border-color: var(--color-danger);
    box-shadow: 0 0 0 1px var(--color-danger);
  }
}

.entry-summary {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
}

.entry-index {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-1);
}

.entry-account {
  font-size: 11px;
  color: var(--text-2);
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.entry-amount {
  font-size: 11px;
  color: var(--text-3);
}

.entry-detail {
  border-top: 1px solid var(--border);
  padding: 8px;
}

.panel-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-top: 1px solid var(--border);
  background: var(--bg-muted);
  border-radius: 0 0 8px 8px;
}

.footer-left {
  display: flex;
  align-items: center;
}

.balance-hint {
  font-size: 12px;
  font-weight: 500;

  &.balanced { color: var(--color-success-text); }
  &.unbalanced { color: var(--color-warning-text); }
}

.footer-actions {
  display: flex;
  gap: 8px;
}
</style>
