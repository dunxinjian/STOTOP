/**
 * useBatchSync — 三层防护同步引擎
 *
 * 职责：
 *   第1层：SignalR 实时推送 — 毫秒级状态更新
 *   第2层：变更感知对账（15s 间隔）— 补偿丢失消息
 *   第3层：重连时立即对账 + 重订阅 — 恢复一致性
 *
 * Bug 修复记录：
 *   - [Fix#2] ID替换清理：维护 tempId→realId 映射，确保所有数据结构完整更新
 *   - [Fix#6] reconcile 性能优化：防并发、限制批次数量、空闲降频
 *
 * 使用方式：
 *   const { setupSignalR, startReconcileTimer, stopReconcileTimer, ... } = useBatchSync()
 *   setupSignalR(connection)           // 注册 SignalR handler
 *   startReconcileTimer(15000)         // 启动定时对账
 *   onSignalRReconnected()             // 重连回调中调用
 */
import { useBatchStore } from '@/stores/batchStore'
import { fetchBatchSync, getImportBatchQueue } from '@/api/cardflow'
import { mapToBatchItem, mapPlugins, mapStatus } from '../utils/batchMapping'
import type { BatchSyncItemDto } from '../utils/batchMapping'
import type { BatchItem, BatchStatus } from '@/stores/batchStore'
import type { HubConnection } from '@microsoft/signalr'

/** 活跃状态列表（需要订阅 SignalR 推送的状态）
 *  包含流程中间状态，非终态批次均需订阅
 */
const SUBSCRIBABLE_STATUSES: BatchStatus[] = ['processing', 'uploading', 'pending']

/** [Fix#6] 单次对账最大批次数量 */
const MAX_RECONCILE_BATCH_COUNT = 50

/** [Fix#6] 空闲检测阈值：连续N次对账无变更则降低频率 */
const IDLE_THRESHOLD = 3

// ===== [BugFix] 模块级共享状态（确保所有调用者共享同一个实例） =====
// 之前 useBatchSync 是普通 composable，每次调用创建新实例。
// useFileUpload/useBatchOperations 中的 subscribeBatch/registerIdMapping 使用独立实例，
// connection 为 undefined，导致新批次不订阅 SignalR Group → 推送全部丢失！

let _connection: HubConnection | undefined
const _tempToRealIdMap = new Map<number, number>()
let _isReconciling = false
let _idleCount = 0
let _normalIntervalMs = 15000
let _currentIntervalMs = 15000
let _reconcileTimer: ReturnType<typeof setInterval> | null = null

export function useBatchSync() {
  const store = useBatchStore()

  // ===== 第1层：SignalR 实时推送 =====

  /**
   * [Fix#2] 解析真实 batchId：先检查是否是临时ID的映射
   */
  function resolveRealBatchId(batchId: number): number {
    return _tempToRealIdMap.get(batchId) ?? batchId
  }

  function normalizePluginName(name?: string | null): string {
    return (name || '').replace(/\s+/g, '').toLowerCase()
  }

  function isSamePluginName(left?: string | null, right?: string | null): boolean {
    const a = normalizePluginName(left)
    const b = normalizePluginName(right)
    if (!a || !b) return false
    if (a.includes(b) || b.includes(a)) return true
    if ((a.includes('价格') || a.includes('计费')) && (b.includes('价格') || b.includes('计费'))) return true
    if (a.includes('成本') && b.includes('成本')) return true
    if ((a.includes('excel') || a.includes('导入')) && (b.includes('excel') || b.includes('导入'))) return true
    return false
  }

  function withPluginDataProgress(
    batch: BatchItem,
    data: {
      pluginName?: string
      phase?: string
      stepName?: string
      detail?: string
      processedCount?: number
      totalCount?: number
      percent?: number
    }
  ) {
    const trail = batch.autoPluginTrail
    if (!trail?.autoPlugins?.length) return undefined

    const plugins = trail.autoPlugins
    const progressName = data.pluginName || data.stepName || data.phase
    const runningNameIndex = progressName
      ? plugins.findIndex(p => p.status === 'Running' && isSamePluginName(p.pluginName, progressName))
      : -1
    const runningIndex = plugins.findIndex(p => p.status === 'Running')
    const nameIndex = progressName
      ? plugins.findIndex(p => isSamePluginName(p.pluginName, progressName))
      : -1
    const currentIndex = typeof trail.currentPluginIndex === 'number'
      ? plugins.findIndex((p, index) => (p.sortIndex ?? index) === trail.currentPluginIndex || index === trail.currentPluginIndex)
      : -1
    const targetIndex = [runningNameIndex, runningIndex, nameIndex, currentIndex].find(index => index >= 0)
    if (targetIndex == null || targetIndex < 0) return undefined

    const percent = data.percent ?? (
      data.totalCount && data.totalCount > 0 && data.processedCount != null
        ? Math.round((data.processedCount / data.totalCount) * 100)
        : undefined
    )
    const stepName = data.detail || data.stepName || data.phase || plugins[targetIndex].currentStepName

    const autoPlugins = plugins.map((plugin, index) => {
      if (index !== targetIndex) return plugin
      return {
        ...plugin,
        dataProgressName: progressName || plugin.dataProgressName,
        dataProgressDetail: data.detail || data.phase || plugin.dataProgressDetail,
        dataProcessedCount: data.processedCount ?? plugin.dataProcessedCount,
        dataProgressTotal: data.totalCount ?? plugin.dataProgressTotal,
        dataProgressPercent: percent ?? plugin.dataProgressPercent,
        currentStepName: stepName,
      }
    })

    return { ...trail, autoPlugins, currentPluginIndex: trail.currentPluginIndex ?? targetIndex }
  }

  /**
   * 注册 SignalR handler（调用一次即可）
   * @param conn - HubConnection 实例
   */
  function setupSignalR(conn: HubConnection) {
    _connection = conn

    // ===== 新增事件：批次流程启动（初始化 autoPluginTrail）=====
    _connection.on('BatchPipelineStarted', (data: {
      batchId: number
      plugins: Array<{ name: string; index: number; status: number }>
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      if (!batch) return
      const autoPlugins = mapPlugins(data.plugins || [])
      store.applyUpdate(realId, {
        autoPluginTrail: { autoPlugins },
        status: 'processing',
      })
      console.debug('[BatchSync] BatchPipelineStarted:', { batchId: realId, pluginCount: autoPlugins.length })
    })

    // ===== 新增事件：插件状态变更 =====
    _connection.on('PluginStatusChanged', (data: {
      batchId: number
      pluginIndex: number
      pluginName: string
      status: string
      error?: string
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      if (!batch) return

      const trail = batch.autoPluginTrail
      if (trail && trail.autoPlugins.length > 0) {
        const updatedAutoPlugins = trail.autoPlugins.map((a, i) => {
          if (i === data.pluginIndex) {
            return {
              ...a,
              status: data.status,
              currentStepName: data.error ? `错误: ${data.error}` : a.currentStepName,
              dataProgressDetail: data.status === 'Running' ? '开始执行' : a.dataProgressDetail,
              dataProgressPercent: data.status === 'Running' ? 0 : a.dataProgressPercent,
              dataProcessedCount: data.status === 'Running' ? undefined : a.dataProcessedCount,
              dataProgressTotal: data.status === 'Running' ? undefined : a.dataProgressTotal,
            }
          }
          return a
        })
        store.applyUpdate(realId, {
          autoPluginTrail: { ...trail, autoPlugins: updatedAutoPlugins, currentPluginIndex: data.pluginIndex },
          currentNodeName: data.status === 'Running' ? data.pluginName : batch.currentNodeName,
          summary: data.status === 'Running'
            ? `${data.pluginName}: 正在执行`
            : data.status === 'Failed'
              ? `${data.pluginName} 失败${data.error ? ': ' + data.error : ''}`
              : batch.summary,
        })
      } else {
        // 无 trail 时仅更新顶层字段
        store.applyUpdate(realId, {
          currentNodeName: data.status === 'Running' ? data.pluginName : batch.currentNodeName,
        })
      }
      console.debug('[BatchSync] PluginStatusChanged:', { batchId: realId, plugin: data.pluginName, status: data.status })
    })

    // ===== 新增事件：批次行级进度更新 =====
    _connection.on('BatchProgressUpdate', (data: {
      batchId: number
      processedRows: number
      totalRows: number
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      if (!batch) return
      const isTerminal = batch.status === 'success' || batch.status === 'error' || batch.status === 'partial'
      if (isTerminal) return

      const progress = data.totalRows > 0 ? Math.round((data.processedRows / data.totalRows) * 100) : 0
      store.applyUpdate(realId, {
        processedRows: data.processedRows,
        totalRows: data.totalRows,
        progress,
        progressPercent: progress,
      })
    })

    // 批次状态变更（仅更新已知批次）
    // 后端 summary 字段是 BatchSummaryDto（{success, failed, skipped}），不是 errorSummary
    _connection.on('BatchStatusChanged', (data: {
      batchId: number
      status: number | string
      statusText?: string
      summary?: { success?: number; failed?: number; skipped?: number } | null
      version: number
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      if (!batch) return

      const mappedStatus = mapStatus(data.status)
      const terminalStatuses: BatchStatus[] = ['success', 'error', 'partial']
      const isTerminal = terminalStatuses.includes(mappedStatus)

      const patch: Partial<import('@/stores/batchStore').BatchItem> = {
        status: mappedStatus,
      }

      // 解析统计摘要（data.summary 是 BatchSummaryDto 对象）
      if (data.summary && typeof data.summary === 'object') {
        if (data.summary.success != null) patch.successCount = data.summary.success
        if (data.summary.failed != null) patch.failedCount = data.summary.failed
        if (data.summary.skipped != null) patch.skippedCount = data.summary.skipped
      }

      // 终态处理
      if (isTerminal) {
        // 终态时用 statusText 更新摘要
        if (data.statusText) patch.summary = data.statusText
        // 终态时设置 stat 统计对象（BatchCard 折叠态第二行显示）
        if (data.summary && typeof data.summary === 'object') {
          patch.stat = {
            total: batch.totalRows || 0,
            success: data.summary.success ?? batch.successCount ?? 0,
            failed: data.summary.failed ?? batch.failedCount ?? 0,
            skipped: data.summary.skipped ?? batch.skippedCount ?? 0,
          }
        }
        // [BugFix] 成功/部分完成时清除 errorMessage，否则折叠态第二行 !errorMessage 条件导致整个摘要区消失
        if (mappedStatus === 'success' || mappedStatus === 'partial') {
          patch.errorMessage = undefined
        }
        // 仅失败时设置 errorMessage
        if (mappedStatus === 'error' && data.statusText) {
          patch.errorMessage = data.statusText
        }
        // 清理 autoPluginTrail 中仍为 Running 的 AutoPlugin
        if (batch.autoPluginTrail) {
          const allCompleted = batch.autoPluginTrail.autoPlugins.every(a => a.status === 'Completed' || a.status === 'Failed')
          if (!allCompleted) {
            patch.autoPluginTrail = {
              ...batch.autoPluginTrail,
              autoPlugins: batch.autoPluginTrail.autoPlugins.map(a => {
                if (a.status === 'Running') {
                  return { ...a, status: mappedStatus === 'success' ? 'Completed' : 'Failed' }
                }
                return a
              }),
            }
          }
        }
        // 终态时清理进度数据
        patch.dataProgressPercent = undefined
        patch.dataProcessedCount = undefined
        patch.dataProgressTotal = undefined
        patch.dataProgressDetail = undefined
        patch.dataProgressName = undefined
        patch.progress = 100
      }

      console.debug('[BatchSync] BatchStatusChanged:', {
        batchId: realId, status: mappedStatus, isTerminal,
        hasSummary: !!data.summary, version: data.version,
      })

      store.applyUpdate(realId, patch, data.version)
    })

    // AutoPlugin 数据处理进度
    _connection.on('OnAutoPluginDataProgress', (data: {
      batchId: number
      percent: number
      pluginName?: string
      stepName?: string
      processedCount?: number
      totalCount?: number
      detail?: string
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      // 非终态均可接受进度（流程中间状态也应更新进度条）
      if (!batch) return
      const isTerminal = batch.status === 'success' || batch.status === 'error' || batch.status === 'partial'
      if (isTerminal) return

      const autoPluginTrail = withPluginDataProgress(batch, {
        pluginName: data.pluginName || data.stepName,
        detail: data.detail,
        processedCount: data.processedCount,
        totalCount: data.totalCount,
        percent: data.percent,
      })

      store.applyUpdate(realId, {
        progress: data.percent,
        progressPercent: data.percent,
        currentStepName: data.stepName || data.pluginName,
        dataProgressName: data.pluginName || data.stepName,
        dataProcessedCount: data.processedCount,
        dataProgressTotal: data.totalCount,
        dataProgressDetail: data.detail,
        dataProgressPercent: data.percent,
        ...(autoPluginTrail ? { autoPluginTrail } : {}),
      })
    })

    // AutoPlugin 阶段切换（AutoPluginProgressReporter.ReportPhaseAsync）
    _connection.on('OnAgentProgress', (data: {
      batchId: number
      phase: string
      current: number
      total: number
      percent: number
      message?: string
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      if (!batch) return
      const isTerminal = batch.status === 'success' || batch.status === 'error' || batch.status === 'partial'
      if (isTerminal) return

      const autoPluginTrail = withPluginDataProgress(batch, {
        phase: data.phase,
        detail: data.message,
        percent: data.percent,
      })

      store.applyUpdate(realId, {
        currentNodeName: data.phase,
        summary: data.message ? `${data.phase}: ${data.message}` : data.phase,
        progress: data.percent ?? batch.progress,
        progressPercent: data.percent ?? batch.progressPercent,
        dataProgressName: data.phase,
        dataProgressDetail: data.message,
        ...(autoPluginTrail ? { autoPluginTrail } : {}),
      })
      console.debug('[BatchSync] OnAgentProgress:', { batchId: realId, phase: data.phase, message: data.message })
    })

    // AutoPlugin 开始执行
    _connection.on('OnAutoPluginStarted', (data: {
      batchId: number
      pluginName: string
      stepName?: string
      pluginIndex?: number
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      // 终态批次不再接受新 AutoPlugin 启动事件
      if (!batch) return
      const isTerminal = batch.status === 'success' || batch.status === 'error' || batch.status === 'partial'
      if (isTerminal) return

      // 更新 autoPluginTrail：将之前仍为 Running 的 AutoPlugin 标记为 Completed，
      // 并将当前 AutoPlugin 标记为 Running
      if (batch.autoPluginTrail && batch.autoPluginTrail.autoPlugins.length > 0) {
        const updatedAutoPlugins = batch.autoPluginTrail.autoPlugins.map((a, i) => {
          // 前一个 Running 的 AutoPlugin 强制置为 Completed（防止 OnAutoPluginCompleted 消息晚到导致多个 AutoPlugin 同时 Running）
          if (a.status === 'Running') {
            return {
              ...a,
              status: 'Completed' as const,
              dataProgressPercent: undefined,
              dataProcessedCount: undefined,
              dataProgressTotal: undefined,
              dataProgressDetail: undefined,
            }
          }
          return a
        })
        // 如果有 pluginIndex，标记当前 AutoPlugin 为 Running
        if (data.pluginIndex !== undefined && data.pluginIndex < updatedAutoPlugins.length) {
          updatedAutoPlugins[data.pluginIndex] = {
            ...updatedAutoPlugins[data.pluginIndex],
            status: 'Running' as const,
            currentStepName: data.stepName,
            dataProgressName: data.pluginName,
            dataProgressDetail: data.stepName || '正在执行',
            dataProgressPercent: 0,
            dataProcessedCount: undefined,
            dataProgressTotal: undefined,
          }
        }
        store.applyUpdate(realId, {
          autoPluginTrail: { ...batch.autoPluginTrail, autoPlugins: updatedAutoPlugins, currentPluginIndex: data.pluginIndex },
          currentNodeName: data.pluginName,
          // [BugFix] 新 AutoPlugin 启动时清除上一个 AutoPlugin 的步骤级进度
          currentStepName: undefined,
          currentStepIndex: undefined,
          totalSteps: undefined,
          currentStepStatus: undefined,
          progress: 0,
          progressPercent: 0,
          dataProgressPercent: 0,
          dataProcessedCount: undefined,
          dataProgressTotal: undefined,
          // 折叠态摘要：AutoPlugin 启动时更新 summary
          summary: `${data.pluginName}: ${data.stepName || '正在执行'}`,
        })
      } else {
        // 无 autoPluginTrail 时仅更新顶层字段
        store.applyUpdate(realId, {
          currentNodeName: data.pluginName,
          // [BugFix] 新 AutoPlugin 启动时清除上一个 AutoPlugin 的步骤级进度
          currentStepName: undefined,
          currentStepIndex: undefined,
          totalSteps: undefined,
          currentStepStatus: undefined,
          progress: 0,
          progressPercent: 0,
          dataProgressPercent: 0,
          dataProcessedCount: undefined,
          dataProgressTotal: undefined,
          // 折叠态摘要：AutoPlugin 启动时更新 summary
          summary: `${data.pluginName}: ${data.stepName || '正在执行'}`,
        })
      }
    })

    // AutoPlugin 步骤推进（不限制 processing，终态前的最后一步可能晚到）
    _connection.on('OnAutoPluginStep', (data: {
      batchId: number
      stepIndex: number
      totalSteps: number
      stepName: string
      status: string
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      if (batch) {
        let autoPluginTrail = batch.autoPluginTrail
        if (autoPluginTrail?.autoPlugins?.length) {
          const targetIndex = autoPluginTrail.autoPlugins.findIndex(p => p.status === 'Running')
          if (targetIndex >= 0) {
            autoPluginTrail = {
              ...autoPluginTrail,
              autoPlugins: autoPluginTrail.autoPlugins.map((plugin, index) => index === targetIndex
                ? {
                    ...plugin,
                    currentStepIndex: data.stepIndex,
                    totalSteps: data.totalSteps,
                    currentStepName: data.stepName,
                    currentStepStatus: data.status,
                  }
                : plugin),
            }
          }
        }

        store.applyUpdate(realId, {
          currentStepIndex: data.stepIndex,
          totalSteps: data.totalSteps,
          currentStepName: data.stepName,
          currentStepStatus: data.status,
          ...(autoPluginTrail ? { autoPluginTrail } : {}),
        })
      }
    })

    // AutoPlugin 完成（关键事件：更新 autoPluginTrail 中对应 AutoPlugin 的终态 + 折叠态摘要）
    _connection.on('OnAutoPluginCompleted', (data: {
      batchId: number
      pluginName: string
      pluginIndex: number
      success: boolean
      message?: string
      autoPluginTrail?: any
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      if (!batch) return

      const updatePatch: Partial<import('@/stores/batchStore').BatchItem> = {
        // 折叠态摘要：AutoPlugin 完成时更新 summary
        summary: data.success ? `${data.pluginName} 已完成` : `${data.pluginName} 失败${data.message ? ': ' + data.message : ''}`,
        // 清除进度数据
        dataProgressPercent: undefined,
        dataProcessedCount: undefined,
        dataProgressTotal: undefined,
        dataProgressDetail: undefined,
        dataProgressName: undefined,
      }

      // 如果后端推送了 autoPluginTrail 快照，直接替换（最可靠）
      // [BugFix] 快照中对应 AutoPlugin 的状态可能不正确（数据库时序问题），
      // 必须根据 data.success 强制修正为 Completed/Failed
      if (data.autoPluginTrail) {
        const snapshot = data.autoPluginTrail as any
        const autoPlugins = snapshot.autoPlugins ? [...snapshot.autoPlugins] : []
        if (data.pluginIndex >= 0 && data.pluginIndex < autoPlugins.length) {
          autoPlugins[data.pluginIndex] = {
            ...autoPlugins[data.pluginIndex],
            status: data.success ? 'Completed' : 'Failed',
            dataProgressPercent: undefined,
            dataProcessedCount: undefined,
            dataProgressTotal: undefined,
            dataProgressDetail: undefined,
          }
        }
        updatePatch.autoPluginTrail = { ...snapshot, autoPlugins }
        store.applyUpdate(realId, updatePatch)
        return
      }

      // 无快照时，手动更新 trail 中对应 AutoPlugin 的状态
      if (batch.autoPluginTrail && batch.autoPluginTrail.autoPlugins.length > 0) {
        const updatedAutoPlugins = batch.autoPluginTrail.autoPlugins.map((a, i) => {
          if (i === data.pluginIndex) {
            return {
              ...a,
              status: data.success ? 'Completed' : 'Failed',
              dataProgressPercent: undefined,
              dataProcessedCount: undefined,
              dataProgressTotal: undefined,
              dataProgressDetail: undefined,
            }
          }
          return a
        })
        updatePatch.autoPluginTrail = { ...batch.autoPluginTrail, autoPlugins: updatedAutoPlugins }
      }

      console.debug('[BatchSync] OnAutoPluginCompleted:', { batchId: realId, pluginName: data.pluginName, success: data.success, hasTrail: !!data.autoPluginTrail })
      store.applyUpdate(realId, updatePatch)
    })

    // 流程阶段进度（折叠态卡片进度和摘要文本的关键更新源）
    _connection.on('PipelineProgress', (data: {
      batchId: number
      currentPhase: string
      progressPercent?: number
      processedRows?: number
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      if (!batch) return
      // 仅在非终态时更新，防止迟到的中间消息覆盖最终状态
      const isTerminal = batch.status === 'success' || batch.status === 'error' || batch.status === 'partial'
      if (isTerminal) return

      const patch: Partial<import('@/stores/batchStore').BatchItem> = {}
      if (data.progressPercent != null) patch.progress = data.progressPercent
      if (data.processedRows != null) patch.processedRows = data.processedRows
      // 仅在没有 autoPluginTrail 时才用 PipelineProgress 更新 summary（折叠态依赖）
      // 有 autoPluginTrail 时头部将直接从 currentRunningAutoPlugin 构建显示文本
      if (data.currentPhase && !batch.autoPluginTrail) {
        patch.summary = data.currentPhase
      }
      console.debug('[BatchSync] FlowProgress:', { batchId: realId, phase: data.currentPhase, percent: data.progressPercent })
      if (Object.keys(patch).length > 0) {
        store.applyUpdate(realId, patch)
      }
    })

    // [BugFix] 批次级进度更新（旧版有此 handler，重构时遗漏）
    // 后端 NotifyImportProgressAsync 发送此事件，包含 progressPercent/processedRows/totalRows/stageLabel
    // 主要在 ExcelInputAgent / DynamicSourceParser / StagingImportStage 期间触发
    _connection.on('BatchProgress', (data: {
      batchId: number
      progressPercent: number
      processedRows: number
      totalRows: number
      stageLabel: string
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      if (!batch) return
      const isTerminal = batch.status === 'success' || batch.status === 'error' || batch.status === 'partial'
      if (isTerminal) return

      const patch: Partial<import('@/stores/batchStore').BatchItem> = {
        progress: data.progressPercent,
        progressPercent: data.progressPercent,
        processedRows: data.processedRows,
        totalRows: data.totalRows,
      }
      // stageLabel 更新 summary（折叠态卡片显示当前阶段文本）
      // 但如果已有 autoPluginTrail，summary 由 autoPlugin 级 handler 控制
      if (data.stageLabel && !batch.autoPluginTrail) {
        patch.summary = data.stageLabel
      }
      console.debug('[BatchSync] BatchProgress:', { batchId: realId, percent: data.progressPercent, stage: data.stageLabel })
      store.applyUpdate(realId, patch)
    })

    // [BugFix] AutoPlugin 里程碑进度（AutoPluginProgressReporter 发送）
    // 后端 ExcelInputAutoPlugin/AutoVoucherAutoPlugin/VoucherMigrationAutoPlugin 等使用 IAutoPluginProgressReporter 推送
    _connection.on('OnAutoPluginProgress', (data: {
      batchId: number
      phase: string
      current: number
      total: number
      percent: number
      message?: string
    }) => {
      const realId = resolveRealBatchId(data.batchId)
      const batch = store.getBatch(realId)
      if (!batch) return
      const isTerminal = batch.status === 'success' || batch.status === 'error' || batch.status === 'partial'
      if (isTerminal) return

      const autoPluginTrail = withPluginDataProgress(batch, {
        phase: data.phase,
        processedCount: data.current,
        totalCount: data.total,
        percent: data.percent,
      })
      const patch: Partial<import('@/stores/batchStore').BatchItem> = {
        progress: data.percent,
        progressPercent: data.percent,
        ...(autoPluginTrail ? { autoPluginTrail } : {}),
      }
      // phase 文本更新 summary（优先级低于 autoPluginTrail）
      const phaseText = data.message || data.phase
      if (phaseText && !batch.autoPluginTrail) {
        patch.summary = phaseText
      }
      console.debug('[BatchSync] OnAutoPluginProgress:', { batchId: realId, phase: data.phase, percent: data.percent })
      if (data.current > 0 || data.total > 0) {
        patch.dataProcessedCount = data.current
        patch.dataProgressTotal = data.total
        patch.dataProgressPercent = data.percent
      }
      store.applyUpdate(realId, patch)
    })
  }

  // ===== 第2层：变更感知对账（定时轮询） =====

  /**
   * [Fix#6] 执行一次对账：拉取自 lastSyncVersion 以来的变更
   * 添加防并发锁 + 批次数量限制 + 空闲自适应频率
   */
  async function reconcile() {
    // [Fix#6] 防止并发对账
    if (_isReconciling) return
    _isReconciling = true

    try {
      const result = await fetchBatchSync(store.lastSyncVersion) as any
      if (!result || !result.batches) return

      // [Fix#6] 限制单次处理的批次数量，优先处理最近的
      const allBatches = result.batches as BatchSyncItemDto[]
      const batchesToProcess = allBatches.length > MAX_RECONCILE_BATCH_COUNT
        ? allBatches.slice(-MAX_RECONCILE_BATCH_COUNT) // 取最后（最新）的N条
        : allBatches

      let hasChanges = false

      for (const item of batchesToProcess) {
        // 已撤销的批次从 Map 中移除
        if (item.isRevoked) {
          store._map.delete(item.batchId)
          hasChanges = true
          continue
        }

        const isNew = !store._map.has(item.batchId)
        if (isNew) {
          // 新批次：完整映射
          store.applyUpdate(item.batchId, mapToBatchItem(item), item.version)
          hasChanges = true
        } else {
          // 已有批次：增量合并（版本保护）
          const existing = store.getBatch(item.batchId)
          // 只有实际有变更时才更新
          if (!existing?._version || item.version > existing._version) {
            const mappedItemStatus = mapStatus(item.status)
            const isItemTerminal = ['success', 'error', 'partial'].includes(mappedItemStatus)
            const reconcilePatch: Partial<import('@/stores/batchStore').BatchItem> = {
              status: mappedItemStatus,
              fileName: item.fileName ?? existing?.fileName,
              currentNodeName: item.currentNodeName,
              currentStepName: item.currentStepName,
              progress: item.progressPercent ?? existing?.progress,
              progressPercent: item.progressPercent ?? existing?.progressPercent,
              totalRows: item.totalRows ?? existing?.totalRows,
              errorCount: item.errorCount ?? existing?.errorCount,
              processedRows: item.processedRows ?? existing?.processedRows,
              successCount: item.successCount ?? existing?.successCount,
              failedCount: item.failedCount ?? existing?.failedCount,
              skippedCount: item.skippedCount ?? existing?.skippedCount,
              isStale: item.isStale ?? existing?.isStale,
              flowName: item.flowName ?? existing?.flowName,
            }
            if (item.plugins?.length) {
              reconcilePatch.autoPluginTrail = { autoPlugins: mapPlugins(item.plugins) }
            }
            // [BugFix] reconcile 的 errorMessage 处理：
            // - 终态（success/partial）时清除 errorMessage，防止折叠态第二行被隐藏
            // - error 时保留 errorMessage
            // - 非终态时使用 API 返回值
            if (isItemTerminal && mappedItemStatus !== 'error') {
              reconcilePatch.errorMessage = undefined
            } else if (item.errorMessage !== undefined) {
              reconcilePatch.errorMessage = item.errorMessage
            }
            store.applyUpdate(item.batchId, reconcilePatch, item.version)
            hasChanges = true
          }
        }
      }

      // 更新水位线
      if (result.maxVersion > store.lastSyncVersion) {
        store.lastSyncVersion = result.maxVersion
      }

      // 节点内进度可能只更新 CfBatch.F当前节点名称/F进度百分比，不一定递增变更版本号。
      // 定时按活跃 batchId 拉队列快照，补偿 SignalR 丢包和版本水位看不到的进度变化。
      const hasActiveSnapshots = await reconcileByBatchIds()
      hasChanges = hasChanges || hasActiveSnapshots

      // [Fix#6] 空闲自适应频率
      if (hasChanges) {
        _idleCount = 0
        // 有变更时恢复正常频率
        if (_currentIntervalMs !== _normalIntervalMs) {
          _currentIntervalMs = _normalIntervalMs
          restartReconcileTimer()
        }
      } else {
        _idleCount++
        // 连续无变更超过阈值，降低轮询频率（最多降到2倍间隔）
        if (_idleCount >= IDLE_THRESHOLD && _currentIntervalMs < _normalIntervalMs * 2) {
          _currentIntervalMs = _normalIntervalMs * 2
          restartReconcileTimer()
        }
      }
    } catch (e) {
      console.debug('[BatchSync] reconcile skipped:', e)
    } finally {
      _isReconciling = false
    }
  }

  /** [Fix#6] 内部方法：重新启动定时器（频率变更时使用） */
  function restartReconcileTimer() {
    if (!_reconcileTimer) return // 尚未启动，无需重启
    stopReconcileTimer()
    _reconcileTimer = setInterval(reconcile, _currentIntervalMs)
  }

  /**
   * 按 batchId 列表对账：收集本地非终态批次，调用 GetQueueBatches?batchIds=...
   * 用于：重连后立即同步最新状态（包括 plugins 数据）
   */
  async function reconcileByBatchIds(): Promise<boolean> {
    const TERMINAL_STATUSES: BatchStatus[] = ['success', 'partial', 'error']
    const nonTerminalBatches = store.batches.filter(b => !TERMINAL_STATUSES.includes(b.status))
    if (nonTerminalBatches.length === 0) return false

    const batchIds = nonTerminalBatches.map(b => b.id).join(',')
    try {
      const result = await getImportBatchQueue({ batchIds }) as any[]
      if (!Array.isArray(result)) return false

      for (const q of result) {
        const batchId = q.batchId ?? q.id
        if (!batchId) continue

        const intStatusMap: Record<number, BatchStatus> = {
          0: 'processing', 1: 'pending', 2: 'processing', 3: 'success',
          4: 'processing', 5: 'success', 6: 'error', 7: 'partial', 8: 'error',
        }
        const status: BatchStatus = (typeof q.status === 'number' ? intStatusMap[q.status] : q.status) as BatchStatus || 'processing'
        const isTerminal = ['success', 'partial', 'error'].includes(status)

        const patch: Partial<import('@/stores/batchStore').BatchItem> = {
          status,
          totalRows: q.totalRows ?? undefined,
          processedRows: q.processedRows ?? undefined,
          successCount: q.successCount ?? undefined,
          failedCount: q.failedCount ?? undefined,
          skippedCount: q.skippedCount ?? undefined,
          progress: q.progressPercent ?? q.progress ?? undefined,
          progressPercent: q.progressPercent ?? q.progress ?? undefined,
          currentNodeName: q.currentStepName ?? q.currentPhase ?? undefined,
          currentStepName: q.currentStepName ?? q.currentPhase ?? undefined,
          errorMessage: (isTerminal && status !== 'error') ? undefined : (q.errorMessage || undefined),
        }

        // 如果后端返回了 plugins 快照，更新 autoPluginTrail
        if (q.plugins?.length) {
          patch.autoPluginTrail = { autoPlugins: mapPlugins(q.plugins) }
        }

        store.applyUpdate(batchId, patch)
      }
      console.debug('[BatchSync] reconcileByBatchIds 完成:', { count: result.length })
      return result.length > 0
    } catch (e) {
      console.debug('[BatchSync] reconcileByBatchIds 失败:', e)
      return false
    }
  }

  /** 启动定时对账 */
  function startReconcileTimer(ms = 15000) {
    stopReconcileTimer()
    _normalIntervalMs = ms
    _currentIntervalMs = ms
    _idleCount = 0
    _reconcileTimer = setInterval(reconcile, ms)
  }

  /** 停止定时对账 */
  function stopReconcileTimer() {
    if (_reconcileTimer) {
      clearInterval(_reconcileTimer)
      _reconcileTimer = null
    }
  }

  // ===== 第3层：重连时立即对账 + 重订阅 =====

  /** 订阅单个批次（供外部操作后调用）
   *  [BugFix] 订阅后延迟触发对账，补偿订阅前遗漏的 SignalR 事件（竞态窗口）
   */
  function subscribeBatch(batchId: number) {
    if (!_connection) return
    _connection.invoke('SubscribeImportBatch', batchId).catch((err) => { console.warn('[SignalR] invoke failed:', err?.message) })
    // 订阅后延迟对账：给 SignalR 订阅生效留出时间，同时避免 subscribeActiveBatches 多次触发
    setTimeout(() => reconcile(), 500)
  }

  /**
   * [Fix#2] 注册临时ID到正式ID的映射
   * 用于确保 SignalR 消息在ID替换过渡期也能正确路由
   */
  function registerIdMapping(tempId: number, realId: number) {
    _tempToRealIdMap.set(tempId, realId)
    // 延迟清理映射（60秒后过渡期结束）
    setTimeout(() => {
      _tempToRealIdMap.delete(tempId)
    }, 60_000)
  }

  /** 重新订阅所有活跃批次 */
  function subscribeActiveBatches() {
    if (!_connection) return
    for (const batch of store.batches) {
      if (SUBSCRIBABLE_STATUSES.includes(batch.status)) {
        _connection.invoke('SubscribeImportBatch', batch.id).catch((err) => { console.warn('[SignalR] invoke failed:', err?.message) })
      }
    }
  }

  /** SignalR 重连后回调：先重订阅，再立即对账 */
  function onSignalRReconnected() {
    // 重连后重置空闲计数，恢复正常频率
    _idleCount = 0
    if (_currentIntervalMs !== _normalIntervalMs) {
      _currentIntervalMs = _normalIntervalMs
      restartReconcileTimer()
    }
    subscribeActiveBatches()
    // 重连后用 batchIds 对账（覆盖本地非终态批次的最新状态）
    reconcileByBatchIds()
  }

  /** 清理所有 SignalR handler */
  function teardownSignalR() {
    if (!_connection) return
    _connection.off('BatchPipelineStarted')
    _connection.off('PluginStatusChanged')
    _connection.off('BatchProgressUpdate')
    _connection.off('BatchStatusChanged')
    _connection.off('OnAutoPluginDataProgress')
    _connection.off('OnAutoPluginStarted')
    _connection.off('OnAutoPluginStep')
    _connection.off('OnAutoPluginCompleted')
    _connection.off('PipelineProgress')
    _connection.off('BatchProgress')
    _connection.off('OnAutoPluginProgress')
    _connection.off('OnAgentProgress')
    // [Fix#2] 清理ID映射
    _tempToRealIdMap.clear()
  }

  return {
    setupSignalR,
    teardownSignalR,
    reconcile,
    reconcileByBatchIds,
    startReconcileTimer,
    stopReconcileTimer,
    subscribeBatch,
    subscribeActiveBatches,
    onSignalRReconnected,
    registerIdMapping, // [Fix#2] 暴露给 useFileUpload 使用
  }
}
