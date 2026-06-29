/**
 * useBatchSelection — 批量选择/全选逻辑
 *
 * 职责：
 *   - 批量选择模式进入/退出
 *   - 全选/取消全选
 *   - 批量撤销操作
 *   - 选中状态与列表数据联动清理
 */
import { ref, computed, watch } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { useBatchStore } from '@/stores/batchStore'
import type { BatchItem } from '@/stores/batchStore'
import { batchRevokeBatches } from '@/api/cardflow'

export function useBatchSelection(deps?: {
  loadRecycledBatches?: () => Promise<void>
  reloadBatches?: () => Promise<void>
}) {
  const store = useBatchStore()

  const batchMode = ref(false)
  const selectedIds = ref<number[]>([])
  const batchRevoking = ref(false)

  /** 判断批次是否可选中/可删除 */
  function isBatchDeletable(batch: BatchItem): boolean {
    const s = batch.status
    if (s === 'uploading' || s === 'processing') {
      return !!batch.isStale || !!batch.errorMessage
    }
    return true
  }

  /** 可删除批次数量 */
  const deletableBatchCount = computed(() =>
    store.batches.filter(b => isBatchDeletable(b)).length,
  )

  /** 全选 checkbox 状态 */
  const selectAllChecked = computed(() => {
    const deletableIds = store.batches.filter(b => isBatchDeletable(b)).map(b => b.id)
    return deletableIds.length > 0 && selectedIds.value.length === deletableIds.length
  })

  const selectAllIndeterminate = computed(() => {
    const deletableIds = store.batches.filter(b => isBatchDeletable(b)).map(b => b.id)
    return selectedIds.value.length > 0 && selectedIds.value.length < deletableIds.length
  })

  /** 全选/取消全选 */
  function toggleSelectAll() {
    const deletableIds = store.batches
      .filter(b => isBatchDeletable(b))
      .map(b => b.id)
    if (selectedIds.value.length === deletableIds.length) {
      selectedIds.value = []
    } else {
      selectedIds.value = [...deletableIds]
    }
  }

  /** 进入批量模式 */
  function enterBatchMode() {
    batchMode.value = true
    selectedIds.value = []
  }

  /** 退出批量模式 */
  function exitBatchMode() {
    batchMode.value = false
    selectedIds.value = []
  }

  /** 批量撤销 */
  async function handleBatchRevoke() {
    if (selectedIds.value.length === 0) return
    Modal.confirm({
      title: '批量删除',
      content: `确定要删除选中的 ${selectedIds.value.length} 个批次？删除后可在回收站恢复。`,
      okText: '确定删除',
      okType: 'danger',
      async onOk() {
        batchRevoking.value = true
        try {
          const res = await batchRevokeBatches(selectedIds.value)
          // 从 store 中移除已撤销的批次
          if (res.succeeded && res.succeeded.length > 0) {
            for (const id of res.succeeded) {
              store.removeBatch(id)
            }
          }
          if (deps?.loadRecycledBatches) {
            await deps.loadRecycledBatches()
          }
          exitBatchMode()
          if (res.skipped && res.skipped.length > 0) {
            message.warning(`${res.skipped.length} 个批次因条件不满足已跳过`)
          }
        } catch (e: any) {
          message.error(e?.message || '批量删除失败')
        } finally {
          batchRevoking.value = false
        }
      },
    })
  }

  // 列表数据变化时同步清理无效选中ID
  watch(() => store.batches, (newBatches) => {
    if (batchMode.value && selectedIds.value.length > 0) {
      const validIds = new Set(newBatches.filter(b => isBatchDeletable(b)).map(b => b.id))
      selectedIds.value = selectedIds.value.filter(id => validIds.has(id))
    }
  })

  return {
    batchMode,
    selectedIds,
    batchRevoking,
    deletableBatchCount,
    selectAllChecked,
    selectAllIndeterminate,
    isBatchDeletable,
    toggleSelectAll,
    enterBatchMode,
    exitBatchMode,
    handleBatchRevoke,
  }
}
