/**
 * batchStore — 批次数据全局单一数据源
 *
 * 内部使用 Map<number, BatchItem> 实现 O(1) 查找，
 * 对外通过 computed 提供数组视图。
 * 所有状态更新均走 applyUpdate 统一入口，自带版本保护防止乱序覆盖。
 */
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

// ================ 类型定义 ================

/** 批次状态枚举 */
export type BatchStatus =
  | 'uploading'       // 上传中
  | 'processing'      // 处理中（流程执行）
  | 'pending'         // 待确认 / 待人工介入
  | 'error'           // 有异常
  | 'success'         // 已完成
  | 'partial'         // 部分完成

/** AutoPlugin 执行轨迹项 */
export interface AutoPluginTrailItemDto {
  pluginName: string
  sortIndex?: number
  status: string
  currentStepName?: string
  currentStepIndex?: number
  totalSteps?: number
  currentStepStatus?: string
  dataProgressName?: string
  dataProgressDetail?: string
  dataProcessedCount?: number
  dataProgressTotal?: number
  dataProgressPercent?: number
}

/** AutoPlugin 执行轨迹 */
export interface AutoPluginTrailDto {
  autoPlugins: AutoPluginTrailItemDto[]
  currentPluginIndex?: number
}

/** 链路事件 */
export interface ChainEvent {
  icon: string
  status: 'done' | 'current' | 'waiting'
  text: string
  duration: string
  time?: string
  operator?: string
}

/** 评论 */
export interface CommentItem {
  id?: number
  author: string
  avatarColor?: string
  time: string
  text: string
}

/** 异常行 */
export interface BatchError {
  id: number
  rowNumber: number
  errorType: string
  errorField: string | null
  errorMessage: string | null
  suggestedFix: string | null
  originalValue: string | null
}

/** 批次数据项（store 核心实体） */
export interface BatchItem {
  id: number
  uid?: string
  batchNo?: string
  title?: string
  fileName: string
  fileSize?: number
  bizType?: string
  bizTag?: string
  tagColor?: string
  status: BatchStatus
  priority?: number
  chainId?: string
  assigneeId?: number
  assigneeName?: string
  creatorId?: number
  creatorName?: string
  createTime: string
  totalRows?: number
  errorCount?: number
  processedRows?: number
  successCount?: number
  failedCount?: number
  skippedCount?: number
  progress?: number
  summary?: string
  stat?: { total: number; success: number; failed: number; skipped: number } | null
  waitTime?: string
  waitHours?: number
  // 流程信息
  pipelineId?: number
  pendingPipelineId?: number
  flowName?: string
  importType?: string
  actualTargetTable?: string
  // AutoPlugin 信息
  currentNodeName?: string
  currentStepName?: string
  currentStepIndex?: number
  totalSteps?: number
  currentStepStatus?: string
  // AutoPlugin 数据处理进度
  dataProgressName?: string
  dataProgressDetail?: string
  dataProcessedCount?: number
  dataProgressTotal?: number
  dataProgressPercent?: number
  progressPercent?: number
  // 异常摘要
  errorMessage?: string
  // 卡住状态
  isStale?: boolean
  // 撤销状态
  isRevoked?: boolean
  // 组织/账套
  orgName?: string
  accountSetName?: string
  uploadMethod?: string
  // 上传进度
  uploadProgress?: number
  uploadChunkInfo?: string
  // 文件上传完成时间
  uploadCompletedAt?: string | null
  // UI 扩展字段
  timeline?: ChainEvent[]
  comments?: CommentItem[]
  errors?: BatchError[]
  autoPluginTrail?: AutoPluginTrailDto | null
  autoPluginTrailLoading?: boolean
  partialInfo?: { successCount: number; pendingManualCount: number }
  // 版本号（防乱序）
  _version?: number
}

// ================ Store ================

export const useBatchStore = defineStore('batch', () => {
  // 内部 Map 存储（O(1) 查找）
  const _map = ref<Map<number, BatchItem>>(new Map())
  // 全局序列水位线（reconcile 用）
  const lastSyncVersion = ref<number>(0)

  // ================ 排序逻辑 ================

  /** 状态优先级：数值越小越靠前 */
  const STATUS_PRIORITY: Record<string, number> = {
    processing: 0,
    error: 1,
    partial: 1,
    uploading: 2,
    pendingPipeline: 2,
    pending: 2,
    success: 3,
  }

  function compareBatches(a: BatchItem, b: BatchItem): number {
    const pa = STATUS_PRIORITY[a.status] ?? 2
    const pb = STATUS_PRIORITY[b.status] ?? 2
    if (pa !== pb) return pa - pb
    // 同状态按创建时间倒序（最新在前）
    return new Date(b.createTime).getTime() - new Date(a.createTime).getTime()
  }

  // 对外数组视图（按状态优先级 + 创建时间倒序排列）
  const batches = computed(() => Array.from(_map.value.values()).sort(compareBatches))

  /** O(1) 查找 */
  function getBatch(id: number): BatchItem | undefined {
    return _map.value.get(id)
  }

  /**
   * 状态更新单一入口（版本保护）
   * - 有版本号时，只接受更新的版本
   * - 无版本号时直接合并（如上传进度等本地更新）
   */
  function applyUpdate(batchId: number, patch: Partial<BatchItem>, version?: number) {
    const existing = _map.value.get(batchId)
    // 版本保护：有版本号时，只接受更新的版本
    if (version !== undefined && existing?._version !== undefined && version <= existing._version) {
      return // 丢弃过时消息
    }
    const updated = { ...(existing ?? {} as BatchItem), ...patch, _version: version ?? existing?._version }
    // 确保 id 始终存在
    updated.id = batchId
    _map.value.set(batchId, updated as BatchItem)
  }

  /** 操作型 API 响应更新 */
  function applyApiResponse(batchId: number, response: { status: string; version: number }) {
    applyUpdate(batchId, { status: response.status as BatchStatus }, response.version)
  }

  /** 初始加载 */
  function loadInitial(items: BatchItem[], maxVersion: number) {
    _map.value.clear()
    for (const item of items) {
      _map.value.set(item.id, item)
    }
    lastSyncVersion.value = maxVersion
  }

  /** 移除批次（撤销/删除场景） */
  function removeBatch(batchId: number) {
    _map.value.delete(batchId)
  }

  /**
   * 原子替换批次ID（临时ID → 正式ID）
   * 确保替换过程中不会丢失数据，同时清理旧ID的所有引用
   */
  function replaceBatchId(oldId: number, newId: number, patch?: Partial<BatchItem>) {
    const existing = _map.value.get(oldId)
    if (!existing) return
    _map.value.delete(oldId)
    const updated = { ...existing, ...patch, id: newId }
    _map.value.set(newId, updated as BatchItem)
  }

  return {
    _map,
    batches,
    lastSyncVersion,
    getBatch,
    applyUpdate,
    applyApiResponse,
    loadInitial,
    removeBatch,
    replaceBatchId,
  }
})
