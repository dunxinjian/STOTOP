/**
 * useRecycleBin — 回收站 CRUD 逻辑
 *
 * 职责：
 *   - 加载回收站列表
 *   - 恢复批次
 *   - 彻底删除批次
 *   - 清空回收站
 */
import { ref } from 'vue'
import { message, Modal } from 'ant-design-vue'
import {
  getRecycledBatches,
  restoreBatch,
  deleteBatch,
  clearRecycleBin,
} from '@/api/cardflow'
import type { RecycledBatchItem } from '@/api/cardflow'

export function useRecycleBin(deps?: { reloadBatches?: () => Promise<void> }) {
  const recycledBatches = ref<RecycledBatchItem[]>([])
  const showRecycleBin = ref(false)
  const recycleBinLoading = ref(false)
  const deletingBatchId = ref<number | null>(null)
  const clearingRecycleBin = ref(false)

  /** 加载回收站列表 */
  async function loadRecycledBatches() {
    recycleBinLoading.value = true
    try {
      const res = await getRecycledBatches()
      recycledBatches.value = res || []
    } catch (e) {
      console.warn('[useRecycleBin] loadRecycledBatches 失败', e)
      message.error('加载回收站失败')
    } finally {
      recycleBinLoading.value = false
    }
  }

  /** 恢复批次 */
  async function handleRestoreBatch(batchId: number) {
    try {
      await restoreBatch(batchId)
      message.success('批次已恢复')
      await loadRecycledBatches()
      if (deps?.reloadBatches) {
        await deps.reloadBatches()
      }
    } catch (e: any) {
      message.error(e?.message || '恢复失败')
    }
  }

  /** 彻底删除批次 */
  async function handlePermanentDelete(batchId: number) {
    await new Promise<void>((resolve, reject) => {
      Modal.confirm({
        title: '彻底删除',
        content: '此操作不可恢复，确定要彻底删除此批次及所有关联数据？',
        okText: '确定删除',
        okType: 'danger',
        cancelText: '取消',
        onOk: () => resolve(),
        onCancel: () => reject('cancel'),
      })
    }).then(async () => {
      deletingBatchId.value = batchId
      try {
        await deleteBatch(batchId, { mode: 'delete', force: true })
        await loadRecycledBatches()
      } catch (e: any) {
        message.error(e?.message || '删除失败，请重试')
      } finally {
        deletingBatchId.value = null
      }
    }).catch(e => {
      if (e !== 'cancel') console.warn(e)
    })
  }

  /** 清空回收站 */
  async function handleClearRecycleBin() {
    const count = recycledBatches.value.length
    if (count === 0) return
    Modal.confirm({
      title: '清空回收站',
      content: `确定要清空回收站吗？共 ${count} 个批次将被彻底删除，此操作不可恢复。`,
      okText: '确定清空',
      okType: 'danger',
      async onOk() {
        clearingRecycleBin.value = true
        try {
          const res = await clearRecycleBin()
          if (res.failedCount === 0) {
            recycledBatches.value = []
          } else {
            await loadRecycledBatches()
            message.warning(`${res.failedCount} 个批次删除失败`)
          }
        } catch (e: any) {
          message.error(e?.message || '清空回收站失败')
        } finally {
          clearingRecycleBin.value = false
        }
      },
    })
  }

  return {
    recycledBatches,
    showRecycleBin,
    recycleBinLoading,
    deletingBatchId,
    clearingRecycleBin,
    loadRecycledBatches,
    handleRestoreBatch,
    handlePermanentDelete,
    handleClearRecycleBin,
  }
}
