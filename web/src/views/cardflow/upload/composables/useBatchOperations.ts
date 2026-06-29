/**
 * useBatchOperations — 批次操作函数集
 *
 * 所有操作统一采用"乐观更新 → API确认 → 失败回退"模式，
 * 消除 setTimeout(() => loadBatches(), 1500) 补丁。
 */
import { message, Modal } from 'ant-design-vue'
import { useBatchStore } from '@/stores/batchStore'
import { useBatchSync } from './useBatchSync'
import {
  retryImportBatch,
  recalculateImportBatch,
  deleteBatch,
  preDeleteCheckBatch,
} from '@/api/cardflow'

export function useBatchOperations(deps?: { loadRecycledBatches?: () => Promise<void> }) {
  const store = useBatchStore()
  const { subscribeBatch } = useBatchSync()

  /**
   * 重试批次（error/pending → processing）
   * 乐观更新 → API → applyApiResponse → subscribeBatch → catch回退
   */
  async function handleRetry(batchId: number) {
    if (batchId < 0) {
      message.error('批次ID无效，请刷新页面后重试')
      return
    }
    const prev = store.getBatch(batchId)
    const prevStatus = prev?.status
    const prevError = prev?.errorMessage
    const prevSummary = prev?.summary
    // 乐观更新
    store.applyUpdate(batchId, { status: 'processing', errorMessage: '', summary: '', isStale: false })
    try {
      const result = await retryImportBatch(batchId)
      // API 响应确认
      if (result && typeof result === 'object' && 'version' in (result as any)) {
        store.applyApiResponse(batchId, result as any)
      }
      subscribeBatch(batchId)
      message.success('已重新提交处理')
    } catch (e: any) {
      // 失败回退
      store.applyUpdate(batchId, { status: prevStatus, errorMessage: prevError, summary: prevSummary })
      message.error(`重试失败: ${e.response?.data?.message || e.message || '未知错误'}`)
    }
  }

  /**
   * 重新计费（partial → processing）
   */
  async function handleRecalculate(batchId: number) {
    const prev = store.getBatch(batchId)
    const prevStatus = prev?.status
    // 乐观更新
    store.applyUpdate(batchId, { status: 'processing', summary: '重新计费中...' })
    try {
      const result = await recalculateImportBatch(batchId)
      if (result && typeof result === 'object' && 'version' in (result as any)) {
        store.applyApiResponse(batchId, result as any)
      }
      subscribeBatch(batchId)
      message.success('已重新计费')
    } catch (e: any) {
      store.applyUpdate(batchId, { status: prevStatus, summary: '' })
      message.error('重新计费失败')
    }
  }

  /**
   * 删除/撤销批次（预检查 → 确认弹窗 → 执行 → 从store移除）
   */
  async function handleDelete(batchId: number) {
    try {
      // 1. 预检查
      const check = await preDeleteCheckBatch(batchId)

      if (!check.canDelete) {
        message.error(check.blockReason || '当前批次状态不允许删除')
        return
      }

      if (check.hasClosedPeriod) {
        message.error(check.blockReason || '该批次关联的会计期间已结账，无法删除')
        return
      }

      // 2. 确定提示信息和删除模式
      let confirmMessage = '确定要撤销此批次？撤销后可恢复。'
      let mode: 'revoke' | 'delete' = 'revoke'
      let force = false

      if (check.hasAuditedVouchers) {
        confirmMessage = `此批次包含已审核凭证（共 ${check.affectedVoucherCount} 张），需要彻底删除（不可恢复）。确定继续？`
        mode = 'delete'
        force = true
      } else if (check.affectedVoucherCount > 0) {
        confirmMessage = `此批次关联 ${check.affectedVoucherCount} 张凭证，撤销后凭证将被标记无效。确定继续？`
      }

      if (check.affectedRowCount > 0) {
        confirmMessage += `\n影响数据行数：${check.affectedRowCount} 行`
      }

      // 3. 确认弹窗
      await new Promise<void>((resolve, reject) => {
        Modal.confirm({
          title: force ? '彻底删除' : '撤销批次',
          content: confirmMessage,
          okText: '确定',
          cancelText: '取消',
          okType: force ? 'danger' : 'primary',
          onOk: () => resolve(),
          onCancel: () => reject('cancel'),
        })
      })

      // 4. 执行删除
      await deleteBatch(batchId, { mode, force })
      message.success(force ? '已彻底删除' : '已撤销')

      // 5. 从 store 移除
      store.removeBatch(batchId)

      // 6. 刷新回收站（更新角标）
      if (!force && deps?.loadRecycledBatches) {
        await deps.loadRecycledBatches()
      }
    } catch (e: any) {
      if (e !== 'cancel' && e?.toString() !== 'cancel') {
        const errMsg = e?.response?.data?.message || e?.message || '操作失败'
        console.warn('[useBatchOperations] handleDelete 失败:', batchId, errMsg)
        message.error(errMsg)
      }
    }
  }

  return {
    handleRetry,
    handleRecalculate,
    handleDelete,
  }
}
