/**
 * useUploadCenter — 上传中心组合层（模块化重写）
 *
 * 职责：组装所有子模块，提供对外统一接口，保持向后兼容。
 * 数据全部通过 Pinia batchStore 共享，SignalR 通过 useBatchSync 管理。
 *
 * 子模块：
 *   - useBatchSync       → 三层防护同步引擎
 *   - useBatchOperations → 操作型API（乐观更新+确认+回退）
 *   - useFileUpload      → 文件分片上传
 *   - useBatchFilters    → 筛选/排序/统计
 *   - useRecycleBin      → 回收站 CRUD
 *   - useBatchSelection  → 批量选择/全选
 */
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useBatchStore } from '@/stores/batchStore'
import { useUserStore } from '@/stores/user'
import { useOrgContextStore } from '@/stores/orgContext'
import { ensureConnected, onReconnected } from '@/utils/signalr'
import { usePermission, CardFlowPermissions } from '@/utils/permission'
import {
  getImportBatches,
  getImportBatchQueue,
  getPipelines,
  getBatchAutoPluginTrail,
  previewOrgBinding,
} from '@/api/cardflow'
import type { PipelineDto, OrgBindingPreviewResult, AutoPluginTrailDto } from '@/api/cardflow'

// 子模块
import { useBatchSync } from './useBatchSync'
import { useBatchOperations } from './useBatchOperations'
import { useFileUpload } from './useFileUpload'
import { useBatchFilters } from './useBatchFilters'
import type { RoleType, ViewMode, FilterState, KpiItem, WorkItemStats } from './useBatchFilters'
import { useRecycleBin } from './useRecycleBin'
import { useBatchSelection } from './useBatchSelection'

// 映射工具
import { mapPlugins } from '../utils/batchMapping'

// 从 store 重新导出类型（保持向后兼容）
export type { BatchStatus, BatchItem, ChainEvent, CommentItem, BatchError, AutoPluginTrailDto } from '@/stores/batchStore'
export type { RoleType, ViewMode, FilterState, KpiItem, WorkItemStats } from './useBatchFilters'

// ===== 常量 =====

// 头像色板——豁免令牌化：传给 CSS inline background，每色为人员身份标识色，不解析 var()
const AVATAR_COLORS = ['#5B7290', '#6BA292', '#C99A6B', '#9B8AB8', '#C77B6B', '#8FB07E'] as const
const AVATAR_COLOR_FALLBACK = '#8c8c8c' // 无名称时的中性占位头像背景色（豁免）

const STATUS_TEXT_MAP: Record<string, string> = {
  uploading: '上传中',
  processing: '处理中',
  pending: '待确认',
  error: '有异常',
  success: '已完成',
  partial: '部分完成',
}

const STATUS_DOT_MAP: Record<string, string> = {
  uploading: 'uploading',
  processing: 'processing',
  pending: 'pending',
  error: 'error',
  success: 'success',
  partial: 'partial',
}

// ===== Composable =====

export function useUploadCenter() {
  const store = useBatchStore()
  const userStore = useUserStore()
  const orgContextStore = useOrgContextStore()
  const { has } = usePermission()

  // 用户信息
  const currentUserName = computed(() => userStore.userInfo?.realName || '当前用户')
  const currentUserId = computed(() => userStore.userInfo?.id || 0)

  // ===== 子模块实例化 =====
  const sync = useBatchSync()
  const filtersModule = useBatchFilters(() => currentUserName.value)
  const recycleBin = useRecycleBin({ reloadBatches: loadBatches })
  const operations = useBatchOperations({ loadRecycledBatches: recycleBin.loadRecycledBatches })
  const upload = useFileUpload()
  const selection = useBatchSelection({ loadRecycledBatches: recycleBin.loadRecycledBatches })

  // ===== 基础状态 =====
  const loading = ref(false)
  const uploadCollapsed = ref(true)
  const flowList = ref<PipelineDto[]>([])

  // ===== 组织预检 =====
  const orgPreviewResult = ref<OrgBindingPreviewResult | null>(null)
  const orgPreviewLoading = ref(false)
  const orgPreviewWarnings = ref<string[]>([])
  const uploadDisabled = computed(() => !orgContextStore.currentOrgId)

  async function checkOrgBinding(orgId: number | null) {
    if (!orgId) {
      orgPreviewResult.value = null
      orgPreviewWarnings.value = []
      return
    }
    orgPreviewLoading.value = true
    try {
      const res = await previewOrgBinding(orgId) as OrgBindingPreviewResult
      orgPreviewResult.value = res
      orgPreviewWarnings.value = res?.warnings || []
    } catch {
      orgPreviewResult.value = null
      orgPreviewWarnings.value = ['预检请求失败，请重试']
    } finally {
      orgPreviewLoading.value = false
    }
  }

  watch(() => orgContextStore.currentOrgId, (newOrgId) => {
    checkOrgBinding(newOrgId)
  }, { immediate: true })

  // ===== 数据映射辅助 =====

  function detectBizType(fileName: string): string {
    if (/费用|报销/.test(fileName)) return 'expense'
    if (/运费|快递|极兔|申通|韵达|圆通|中通|顺丰/.test(fileName)) return 'express'
    if (/客户/.test(fileName)) return 'customer'
    return 'other'
  }

  function getWaitHours(createTime: string): number {
    if (!createTime) return 0
    const diff = Date.now() - new Date(createTime).getTime()
    return Math.max(0, diff / (1000 * 60 * 60))
  }

  function formatWaitTime(hours: number): string | undefined {
    if (hours < 0.1) return undefined
    if (hours < 1) return `${Math.round(hours * 60)}分钟`
    if (hours < 24) return `${hours.toFixed(1)}小时`
    return `${Math.floor(hours / 24)}天`
  }

  function mapQueueToBatch(q: any) {
    const intStatusMap: Record<number, string> = {
      0: 'processing', 1: 'pending', 2: 'processing', 3: 'success', 4: 'processing', 5: 'success', 6: 'error', 7: 'partial', 8: 'error',
    }
    const strStatusMap: Record<string, string> = {
      waiting: 'uploading', uploading: 'uploading', uploaded: 'processing',
      recognizing: 'processing', importing: 'processing', completed: 'success',
      failed: 'error', partial: 'partial',
    }
    const status = (typeof q.status === 'number' ? intStatusMap[q.status] : strStatusMap[q.status]) || 'processing'
    // 如果后端返回了 plugins 快照，直接映射为 autoPluginTrail
    const autoPluginTrail = q.plugins?.length
      ? { autoPlugins: mapPlugins(q.plugins) }
      : undefined
    return {
      id: q.batchId || q.id || 0,
      uid: q.id || String(q.batchId),
      batchNo: q.batchNo || '',
      title: q.name || q.fileName || '',
      fileName: q.name || q.fileName || '',
      bizType: detectBizType(q.name || q.fileName || ''),
      bizTag: q.bizTag ?? '未知类型',
      tagColor: q.tagColor ?? 'gray',
      status: status as any,
      priority: 0,
      createTime: q.createTime || new Date().toISOString(),
      totalRows: q.totalRows || 0,
      errorCount: q.errorCount || q.failRows || 0,
      processedRows: q.processedRows || ((q.successCount || q.successRows || 0) + (q.failedCount || q.failRows || 0) + (q.skippedCount || q.skipRows || 0)),
      successCount: q.successCount || q.successRows || 0,
      failedCount: q.failedCount || q.failRows || 0,
      skippedCount: q.skippedCount || q.skipRows || 0,
      progress: q.progressPercent ?? q.progress ?? (q.totalRows > 0 ? Math.round(((q.successCount || q.successRows || 0) / q.totalRows) * 100) : 0),
      summary: (status === 'success' || status === 'partial') ? '' : (q.errorMessage || (q.errorSummary as string) || ''),
      errorMessage: (status === 'success' || status === 'partial') ? undefined : (q.errorMessage || (q.errorSummary as string) || undefined),
      waitHours: 0,
      comments: [],
      uploadProgress: status === 'uploading' ? (q.progress || 0) : undefined,
      partialInfo: q.partialInfo,
      pipelineId: q.pipelineId ?? q.flowId,
      pendingPipelineId: q.pendingPipelineId ?? q.pendingFlowId,
      isStale: !!q.isStale,
      fileSize: q.fileSize || 0,
      flowName: q.fileTypeName || q.flowName || '',
      currentNodeName: q.currentStepName || q.currentPhase,
      currentStepName: q.currentStepName || q.currentPhase,
      progressPercent: q.progressPercent ?? q.progress,
      autoPluginTrail,
    }
  }

  function mapHistoryToBatch(h: any) {
    const statusMap: Record<number, string> = {
      0: 'processing', 1: 'pending', 2: 'processing', 3: 'success', 4: 'processing', 5: 'success', 6: 'error', 7: 'partial', 8: 'error',
    }
    const status = statusMap[h.status] ?? 'processing'
    const totalRows = h.totalRows || 0
    const successCount = h.successCount || h.successRows || 0
    const failedCount = h.failedCount || h.errorCount || h.failRows || 0
    const skippedCount = h.skippedCount || h.skipRows || 0
    let stat: { total: number; success: number; failed: number; skipped: number } | null = null
    if (status === 'success' || status === 'partial') {
      stat = {
        total: totalRows || 0,
        success: successCount || 0,
        failed: failedCount || 0,
        skipped: skippedCount || 0,
      }
    }
    const hours = getWaitHours(h.createTime)
    return {
      id: h.id,
      uid: String(h.id),
      batchNo: h.batchNo || '',
      title: h.fileName || '',
      fileName: h.fileName || '',
      bizType: detectBizType(h.fileName || ''),
      bizTag: h.bizTag ?? '未知类型',
      tagColor: h.tagColor ?? 'gray',
      status: status as any,
      priority: 0,
      createTime: h.createTime || '',
      totalRows, errorCount: failedCount,
      processedRows: h.processedRows || successCount,
      successCount, failedCount, skippedCount,
      progress: totalRows > 0 ? Math.round(successCount / totalRows * 100) : 0,
      summary: (status === 'success' || status === 'partial') ? '' : (h.errorMessage || (h.errorSummary as string) || ''),
      errorMessage: (status === 'success' || status === 'partial') ? undefined : (h.errorMessage || (h.errorSummary as string) || undefined),
      stat,
      waitTime: formatWaitTime(hours),
      waitHours: hours,
      comments: [],
      assigneeName: h.assigneeName || undefined,
      actualTargetTable: h.actualTargetTable,
      isStale: !!h.isStale,
      orgName: h.orgName || h.f组织名称 || undefined,
      accountSetName: h.accountSetName || h.f账套名称 || undefined,
      uploadMethod: h.uploadMethod || h.f上传方式 || undefined,
    }
  }

  // ===== 数据加载 =====

  async function loadBatches() {
    loading.value = true
    try {
      const { startDate, endDate } = filtersModule.getDateRange(filtersModule.filters.value.timeRange)
      const batchParams: any = { page: 1, pageSize: 50 }
      if (startDate) batchParams.startDate = startDate
      if (endDate) batchParams.endDate = endDate

      const [queue, history] = await Promise.allSettled([
        getImportBatchQueue(),
        getImportBatches(batchParams),
      ])

      const items: any[] = []
      if (queue.status === 'fulfilled' && queue.value) {
        for (const q of (queue.value as any[]) || []) {
          items.push(mapQueueToBatch(q))
        }
      }
      if (history.status === 'fulfilled' && history.value) {
        const historyData = history.value as any
        const historyItems = historyData.items || historyData || []
        for (const h of historyItems) {
          if (!items.find((i: any) => i.id === h.id)) {
            items.push(mapHistoryToBatch(h))
          }
        }
      }

      const maxVersion = Math.max(...items.map((i: any) => i._version ?? 0), 0)
      store.loadInitial(items, maxVersion)

      // 重新订阅所有活跃批次
      sync.subscribeActiveBatches()
    } catch (e) {
      console.warn('[useUploadCenter] loadBatches 失败', e)
    } finally {
      loading.value = false
    }
  }

  async function loadFlows() {
    try {
      const list = await getPipelines()
      flowList.value = list || []
    } catch {
      // 静默
    }
  }

  async function loadAutoPluginTrail(batchId: number, forceRefresh = false) {
    const batch = store.getBatch(batchId)
    if (!batch) return
    if (!['processing', 'error', 'partial', 'success'].includes(batch.status)) return

    // 判断 trail 是否过期：
    // 1. 批次已到终态但 trail 中仍有 Running 的 autoPlugin（说明 OnAutoPluginCompleted 消息丢失）
    // 2. 批次已 success 但 trail 仍显示 Running
    const isTerminal = batch.status === 'success' || batch.status === 'error' || batch.status === 'partial'
    const hasStaleRunning = isTerminal && batch.autoPluginTrail?.autoPlugins.some(a => a.status === 'Running')
    const needsRefresh = forceRefresh || hasStaleRunning

    if (!needsRefresh && batch.autoPluginTrail !== undefined && batch.status !== 'processing') return
    store.applyUpdate(batchId, { autoPluginTrailLoading: true })
    try {
      const res = await getBatchAutoPluginTrail(batchId)
      const trail = (res as AutoPluginTrailDto) ?? null
      // 规范化 status 为首字母大写（后端API返回小写，SignalR推送首字母大写）
      if (trail?.autoPlugins) {
        for (const p of trail.autoPlugins) {
          if (p.status) p.status = p.status.charAt(0).toUpperCase() + p.status.slice(1).toLowerCase()
        }
      }
      store.applyUpdate(batchId, { autoPluginTrail: trail, autoPluginTrailLoading: false })
      // 恢复步骤级进度
      if (trail) {
        const runningAutoPlugin = trail.autoPlugins.find((a: any) => a.status === 'Running')
        if (runningAutoPlugin?.currentStepName) {
          store.applyUpdate(batchId, {
            currentStepName: runningAutoPlugin.currentStepName,
            currentStepIndex: runningAutoPlugin.currentStepIndex ?? undefined,
            totalSteps: runningAutoPlugin.totalSteps ?? undefined,
            currentStepStatus: runningAutoPlugin.currentStepStatus ?? undefined,
          })
        }
      }
    } catch {
      store.applyUpdate(batchId, { autoPluginTrail: null, autoPluginTrailLoading: false })
    }
  }

  // ===== 工具方法 =====

  function getStatusText(status: string): string {
    return STATUS_TEXT_MAP[status] || status
  }

  function getStatusDotClass(status: string): string {
    return STATUS_DOT_MAP[status] || 'pending'
  }

  function avatarColor(name: string | undefined): string {
    if (!name) return AVATAR_COLOR_FALLBACK
    let hash = 0
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash)
    }
    return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length]
  }

  function cardClasses(batch: any): string[] {
    const cls: string[] = []
    if (filtersModule.currentRole.value === 'processor') {
      if (batch.assigneeName === currentUserName.value) cls.push('card-mine')
      else cls.push('card-dimmed')
    }
    if (filtersModule.currentRole.value === 'manager' && (batch.waitHours ?? 0) >= 4) cls.push('card-stale')
    return cls
  }

  function canAssign(batch: any): boolean {
    return !['uploading', 'success'].includes(batch.status)
  }

  function showUrgeBtn(batch: any): boolean {
    if (['uploading', 'success'].includes(batch.status)) return false
    if (filtersModule.currentRole.value === 'uploader' && ['pending', 'error'].includes(batch.status) && (batch.waitHours ?? 0) >= 1) return true
    if (filtersModule.currentRole.value === 'manager' && (batch.waitHours ?? 0) >= 2) return true
    return false
  }

  // ===== 生命周期 =====

  let removeReconnectCb: (() => void) | null = null

  async function init() {
    // 防御性检查：确保 orgContext 已初始化（路由守卫的 fetchCurrentContext 可能静默失败）
    if (!orgContextStore.currentOrgId) {
      try {
        await orgContextStore.fetchCurrentContext()
      } catch (e) {
        console.warn('[useUploadCenter] orgContext fetchCurrentContext 失败', e)
      }
    }

    await Promise.allSettled([loadBatches(), filtersModule.loadStats(), loadFlows(), recycleBin.loadRecycledBatches()])

    // 建立 SignalR 连接
    try {
      const conn = await ensureConnected()
      sync.setupSignalR(conn)
      sync.subscribeActiveBatches()
      sync.startReconcileTimer(15000)
      // 注册重连回调
      removeReconnectCb = onReconnected(() => {
        sync.onSignalRReconnected()
      })
    } catch (e) {
      console.warn('[useUploadCenter] SignalR 连接失败', e)
    }
  }

  // 日期筛选变化时重新加载数据
  watch(() => filtersModule.filters.value.timeRange, () => {
    loadBatches()
  })

  onMounted(() => {
    init()
  })

  onUnmounted(() => {
    sync.stopReconcileTimer()
    sync.teardownSignalR()
    if (removeReconnectCb) removeReconnectCb()
  })

  // ===== 返回（兼容旧接口） =====
  return {
    // 状态
    loading,
    batches: computed(() => store.batches),
    stats: filtersModule.stats,
    filters: filtersModule.filters,
    currentRole: filtersModule.currentRole,
    viewMode: filtersModule.viewMode,
    uploadCollapsed,
    flowList,
    currentUserName,
    currentUserId,

    // 组织预检
    orgPreviewResult,
    orgPreviewLoading,
    orgPreviewWarnings,
    uploadDisabled,

    // 批量操作
    batchMode: selection.batchMode,
    selectedIds: selection.selectedIds,
    batchRevoking: selection.batchRevoking,
    clearingRecycleBin: recycleBin.clearingRecycleBin,
    deletableBatchCount: selection.deletableBatchCount,
    selectAllChecked: selection.selectAllChecked,
    selectAllIndeterminate: selection.selectAllIndeterminate,
    isBatchDeletable: selection.isBatchDeletable,
    enterBatchMode: selection.enterBatchMode,
    exitBatchMode: selection.exitBatchMode,
    toggleSelectAll: selection.toggleSelectAll,
    handleBatchRevoke: selection.handleBatchRevoke,
    handleClearRecycleBin: recycleBin.handleClearRecycleBin,

    // 回收站
    recycledBatches: recycleBin.recycledBatches,
    showRecycleBin: recycleBin.showRecycleBin,
    recycleBinLoading: recycleBin.recycleBinLoading,
    deletingBatchId: recycleBin.deletingBatchId,

    // 计算
    roleKpi: filtersModule.roleKpi,
    filteredBatches: filtersModule.filteredBatches,
    displayBatches: filtersModule.displayBatches,

    // 数据加载
    loadBatches,
    loadStats: filtersModule.loadStats,
    init,

    // 工具方法
    getStatusText,
    getStatusDotClass,
    avatarColor,
    cardClasses,
    canAssign,
    showUrgeBtn,

    // AutoPlugin 轨迹
    loadAutoPluginTrail,

    // 操作
    handleRetry: operations.handleRetry,
    handleRecalculate: operations.handleRecalculate,
    handleDelete: operations.handleDelete,
    handleFileUpload: upload.handleFileUpload,
    onUnmatchedBatch: upload.onUnmatchedBatch,

    // 回收站操作
    loadRecycledBatches: recycleBin.loadRecycledBatches,
    handleRestoreBatch: recycleBin.handleRestoreBatch,
    handlePermanentDelete: recycleBin.handlePermanentDelete,
  }
}
