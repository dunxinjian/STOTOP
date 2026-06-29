<!-- 动态科目映射表：匹配值→目标科目的映射配置，支持批量导入和智能提示 -->
<template>
  <div class="account-match-table">
    <!-- 操作按钮 -->
    <div class="action-bar">
      <a-button type="dashed" size="small" @click="addRow">
        <PlusOutlined /> 添加映射
      </a-button>
      <a-button size="small" @click="openBatchImport">
        <ImportOutlined /> 批量导入
      </a-button>
    </div>

    <!-- 智能提示：批次中存在但未映射的值 -->
    <a-alert
      v-if="unmappedHints.length > 0"
      type="warning"
      show-icon
      class="unmapped-hint"
    >
      <template #message>
        以下值存在于批次数据中但尚未映射：
        <a-tag
          v-for="hint in unmappedHints.slice(0, 5)"
          :key="hint"
          color="orange"
          size="small"
          class="hint-tag"
          @click="addRowForValue(hint)"
        >{{ hint }}</a-tag>
        <span v-if="unmappedHints.length > 5" style="color: var(--text-3);">
          等 {{ unmappedHints.length }} 项
        </span>
      </template>
    </a-alert>

    <!-- 映射表 -->
    <a-table
      :columns="columns"
      :data-source="rows"
      :pagination="false"
      size="small"
      bordered
      row-key="_rowKey"
      class="match-table"
    >
      <!-- 匹配值列 -->
      <template #bodyCell="{ column, record, index }">
        <template v-if="column.dataIndex === 'value'">
          <a-input
            v-model:value="record.value"
            placeholder="匹配值"
            size="small"
            @change="onRowChange"
          />
        </template>
        <!-- 目标科目列 -->
        <template v-else-if="column.dataIndex === 'accountId'">
          <a-tree-select
            v-model:value="record.accountId"
            placeholder="选择科目"
            size="small"
            style="width: 100%;"
            :tree-data="accountTreeData"
            :fieldNames="{ label: 'title', value: 'id', children: 'children' }"
            show-search
            tree-node-filter-prop="title"
            allow-clear
            @change="onRowChange"
          />
        </template>
        <!-- 操作列 -->
        <template v-else-if="column.dataIndex === 'action'">
          <a-button type="text" danger size="small" @click="removeRow(index)">
            <DeleteOutlined />
          </a-button>
        </template>
      </template>
    </a-table>

    <!-- 批量导入对话框 -->
    <a-modal
      v-model:open="batchImportVisible"
      title="批量导入映射规则"
      @ok="confirmBatchImport"
      ok-text="导入"
      cancel-text="取消"
    >
      <p style="color: var(--text-3); font-size: 12px; margin-bottom: 8px;">
        每行一条规则，格式：匹配值,科目编码（如：顺丰,1001）
      </p>
      <a-textarea
        v-model:value="batchImportText"
        placeholder="顺丰,1001&#10;圆通,1002&#10;中通,1003"
        :rows="8"
      />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { PlusOutlined, DeleteOutlined, ImportOutlined } from '@ant-design/icons-vue'
import { useAutoVoucherRuleStore } from '@/stores/autoVoucherRule'
import type { AccountMatchRule } from '@/stores/autoVoucherRule'

const store = useAutoVoucherRuleStore()

const props = defineProps<{
  /** 映射规则列表 */
  modelValue: AccountMatchRule[]
  /** 科目树数据（从 store 获取） */
  accountTreeData: any[]
  /** 批次中存在但未映射的值（来自字段分析） */
  batchValues?: string[]
}>()

const emit = defineEmits<{
  'update:modelValue': [value: AccountMatchRule[]]
}>()

// ==================== 内部行数据 ====================
interface RowItem extends AccountMatchRule {
  _rowKey: string
}

let _keyCounter = 0
function nextRowKey() {
  return `row_${++_keyCounter}`
}

const rows = ref<RowItem[]>(
  (props.modelValue || []).map((r) => ({
    ...r,
    _rowKey: nextRowKey(),
  }))
)

// 仅在父组件整体替换时同步（跳过内部 syncToParent 触发的回流）
watch(() => props.modelValue, (val) => {
  const newVal = val || []
  // 如果是内部 syncToParent 触发的回流，跳过（长度和内容相同）
  if (newVal.length === rows.value.length &&
      newVal.every((r, i) => r.value === rows.value[i].value && r.accountId === rows.value[i].accountId)) {
    return
  }
  // 外部真正替换了数据，才重建 rows（保留可复用的 key）
  rows.value = newVal.map((r, i) => ({
    ...r,
    _rowKey: rows.value[i]?._rowKey || nextRowKey(),
  }))
}, { immediate: false })

function syncToParent() {
  emit('update:modelValue', rows.value.map(({ _rowKey, ...rest }) => rest))
}

// ==================== 表格列定义 ====================
const columns = [
  { title: '匹配值', dataIndex: 'value', width: '40%' },
  { title: '目标科目', dataIndex: 'accountId', width: '50%' },
  { title: '操作', dataIndex: 'action', width: '10%', align: 'center' as const },
]

// ==================== 操作方法 ====================
function addRow() {
  rows.value.push({
    value: '',
    accountId: undefined,
    _rowKey: nextRowKey(),
  })
  syncToParent()
}

function addRowForValue(val: string) {
  rows.value.push({
    value: val,
    accountId: undefined,
    _rowKey: nextRowKey(),
  })
  syncToParent()
}

function removeRow(idx: number) {
  rows.value.splice(idx, 1)
  syncToParent()
}

function onRowChange() {
  syncToParent()
}

// ==================== 智能提示：未映射值 ====================
const unmappedHints = computed(() => {
  if (!props.batchValues || props.batchValues.length === 0) return []
  const mappedValues = new Set(rows.value.map(r => r.value).filter(Boolean))
  return props.batchValues.filter(v => !mappedValues.has(v))
})

// ==================== 批量导入 ====================
const batchImportVisible = ref(false)
const batchImportText = ref('')

function openBatchImport() {
  batchImportText.value = ''
  batchImportVisible.value = true
}

function confirmBatchImport() {
  const lines = batchImportText.value.split('\n').filter(l => l.trim())
  for (const line of lines) {
    const parts = line.split(/[,，\t]/).map(s => s.trim())
    if (parts.length >= 2 && parts[0]) {
      rows.value.push({
        value: parts[0],
        accountId: Number(parts[1]) || undefined,
        _rowKey: nextRowKey(),
      })
    }
  }
  syncToParent()
  batchImportVisible.value = false
}
</script>

<style lang="scss" scoped>
.account-match-table {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.action-bar {
  display: flex;
  gap: 8px;
}

.unmapped-hint {
  :deep(.ant-alert-message) {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 4px;
  }
}

.hint-tag {
  cursor: pointer;
  &:hover {
    opacity: 0.8;
  }
}

.match-table {
  :deep(.ant-table) {
    font-size: 12px;
  }
}
</style>
