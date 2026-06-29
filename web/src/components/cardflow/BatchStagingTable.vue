<script setup lang="ts">
/**
 * BatchStagingTable.vue — 批次暂存数据编辑表格
 *
 * - 单元格就地编辑
 * - 标红质检失败行
 * - 排除/恢复/确认提交
 * - 编辑携带 rowVersion 实现乐观锁
 */
import { ref, computed, onMounted, watch } from 'vue'
import { Modal, message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  ReloadOutlined,
  StopOutlined,
  UndoOutlined,
  CheckCircleOutlined,
} from '@ant-design/icons-vue'
import type {
  CfBatchRow,
  CfBatchRowStatus,
  SchemaFieldDefinition,
} from '@/types/cardflow'
import {
  getBatchRows,
  updateBatchRow,
  excludeBatchRows,
  restoreBatchRows,
  confirmBatch,
} from '@/api/cardflow'

interface Props {
  batchId: number
  schema: SchemaFieldDefinition[]
  /** 是否允许编辑/操作（已确认提交后只读） */
  editable?: boolean
}

const props = withDefaults(defineProps<Props>(), { editable: true })

const emit = defineEmits<{
  (e: 'confirmed', batchId: number): void
  (e: 'changed'): void
}>()

const loading = ref(false)
const rows = ref<CfBatchRow[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const filterStatus = ref<CfBatchRowStatus | undefined>(undefined)
const selectedRowKeys = ref<number[]>([])

// 编辑态：rowId -> 已修改的 data
const editingMap = ref<Record<number, Record<string, any>>>({})

// ==================== 列配置 ====================
const columns = computed<TableColumnsType>(() => {
  const base: TableColumnsType = [
    { title: '#', dataIndex: 'rowNo', width: 64, fixed: 'left' },
    {
      title: '状态',
      dataIndex: 'status',
      width: 96,
      fixed: 'left',
      customRender: ({ value }: any) => statusLabel(value),
    },
  ]
  const dynamic: TableColumnsType = props.schema.map((f) => ({
    title: f.label,
    dataIndex: `__data.${f.key}`,
    key: f.key,
    width: 160,
  }))
  const tail: TableColumnsType = [
    { title: '错误信息', dataIndex: 'errorMessage', width: 220, ellipsis: true },
  ]
  return [...base, ...dynamic, ...tail]
})

const dataSource = computed(() =>
  rows.value.map((r) => {
    let data: Record<string, any> = {}
    try {
      data = r.dataJson ? JSON.parse(r.dataJson) : {}
    } catch {
      data = {}
    }
    // 合并已编辑数据
    if (editingMap.value[r.id]) data = { ...data, ...editingMap.value[r.id] }
    return { ...r, key: r.id, __data: data }
  }),
)

function statusLabel(s: CfBatchRowStatus): string {
  return ['待处理', '质检通过', '已创建卡片', '质检失败', '已排除', '已撤销'][s] ?? '未知'
}

function rowClass(record: any): string {
  if (record.status === 3) return 'cf-staging__row--failed'
  if (record.status === 4) return 'cf-staging__row--excluded'
  return ''
}

// ==================== 数据加载 ====================
async function load() {
  loading.value = true
  try {
    const res = await getBatchRows(props.batchId, {
      page: page.value,
      pageSize: pageSize.value,
      status: filterStatus.value,
    })
    rows.value = res.items || []
    total.value = res.total || 0
    editingMap.value = {}
    selectedRowKeys.value = []
  } catch {
    message.error('加载暂存数据失败')
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(() => props.batchId, load)
watch([page, pageSize, filterStatus], load)

// ==================== 编辑 ====================
function onCellEdit(rowId: number, key: string, val: any) {
  const cur = editingMap.value[rowId] || {}
  editingMap.value[rowId] = { ...cur, [key]: val }
}

async function saveRow(row: CfBatchRow) {
  const patch = editingMap.value[row.id]
  if (!patch) return
  try {
    let original: Record<string, any> = {}
    try { original = row.dataJson ? JSON.parse(row.dataJson) : {} } catch {}
    const merged = { ...original, ...patch }
    const updated: any = await updateBatchRow(props.batchId, row.id, {
      dataJson: JSON.stringify(merged),
      rowVersion: row.rowVersion,
    })
    // 用返回值替换本地行
    const idx = rows.value.findIndex((r) => r.id === row.id)
    if (idx !== -1 && updated) rows.value[idx] = updated
    delete editingMap.value[row.id]
    message.success(`第 ${row.rowNo} 行已保存`)
    emit('changed')
  } catch (err: any) {
    if (err?.response?.status === 409) {
      message.error(`第 ${row.rowNo} 行版本已变更，请刷新后重试`)
    } else {
      message.error(err?.response?.data?.message || '保存失败')
    }
  }
}

// ==================== 操作 ====================
async function handleExclude() {
  if (selectedRowKeys.value.length === 0) {
    message.warning('请先选择行')
    return
  }
  try {
    await excludeBatchRows(props.batchId, selectedRowKeys.value)
    message.success(`已排除 ${selectedRowKeys.value.length} 行`)
    await load()
    emit('changed')
  } catch {
    message.error('排除失败')
  }
}

async function handleRestore() {
  if (selectedRowKeys.value.length === 0) {
    message.warning('请先选择行')
    return
  }
  try {
    await restoreBatchRows(props.batchId, selectedRowKeys.value)
    message.success(`已恢复 ${selectedRowKeys.value.length} 行`)
    await load()
    emit('changed')
  } catch {
    message.error('恢复失败')
  }
}

function handleConfirm() {
  Modal.confirm({
    title: '确认提交全部行？',
    content: '提交后将根据暂存数据创建卡片，过程不可逆。',
    okText: '确认提交',
    onOk: async () => {
      try {
        await confirmBatch(props.batchId)
        message.success('已确认提交')
        emit('confirmed', props.batchId)
        await load()
      } catch (err: any) {
        message.error(err?.response?.data?.message || '提交失败')
      }
    },
  })
}
</script>

<template>
  <div class="cf-staging">
    <div class="cf-staging__toolbar">
      <a-radio-group v-model:value="filterStatus" button-style="solid" size="small">
        <a-radio-button :value="undefined">全部</a-radio-button>
        <a-radio-button :value="0">待处理</a-radio-button>
        <a-radio-button :value="1">质检通过</a-radio-button>
        <a-radio-button :value="3">质检失败</a-radio-button>
        <a-radio-button :value="4">已排除</a-radio-button>
      </a-radio-group>

      <span class="cf-staging__spacer" />

      <a-button size="small" @click="load">
        <template #icon><reload-outlined /></template>
        刷新
      </a-button>
      <a-button v-if="editable" size="small" danger @click="handleExclude">
        <template #icon><stop-outlined /></template>
        排除选中
      </a-button>
      <a-button v-if="editable" size="small" @click="handleRestore">
        <template #icon><undo-outlined /></template>
        恢复选中
      </a-button>
      <a-button v-if="editable" type="primary" size="small" @click="handleConfirm">
        <template #icon><check-circle-outlined /></template>
        确认提交
      </a-button>
    </div>

    <a-table
      :loading="loading"
      :columns="columns"
      :data-source="dataSource"
      :row-key="(r: any) => r.id"
      :row-class-name="(r: any) => rowClass(r)"
      :row-selection="editable ? { selectedRowKeys, onChange: (keys: any) => (selectedRowKeys = keys as number[]) } : undefined"
      :pagination="{
        current: page,
        pageSize,
        total,
        showSizeChanger: true,
        onChange: (p: number, ps: number) => { page = p; pageSize = ps },
      }"
      :scroll="{ x: 'max-content', y: 480 }"
      size="small"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key && schema.find((f) => f.key === column.key)">
          <a-input
            v-if="editable && record.status !== 4 && record.status !== 5 && record.status !== 2"
            :value="record.__data?.[column.key]"
            size="small"
            @update:value="(v: string) => onCellEdit(record.id, column.key as string, v)"
            @blur="saveRow(record as CfBatchRow)"
          />
          <span v-else>{{ record.__data?.[column.key] ?? '-' }}</span>
        </template>
      </template>
    </a-table>
  </div>
</template>

<style scoped lang="scss">
.cf-staging {
  display: flex;
  flex-direction: column;
  gap: 12px;

  &__toolbar {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  &__spacer {
    flex: 1;
  }

  :deep(.cf-staging__row--failed) {
    background-color: var(--color-danger-light);

    &:hover > td {
      background-color: color-mix(in srgb, var(--color-danger-light) 80%, var(--color-danger) 20%) !important;
    }
  }

  :deep(.cf-staging__row--excluded) {
    color: var(--text-3);
    text-decoration: line-through;
  }
}
</style>
