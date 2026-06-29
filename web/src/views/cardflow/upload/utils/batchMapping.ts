/**
 * batchMapping — 批次 DTO → BatchItem 映射工具
 *
 * 将后端 batch-sync 接口返回的 DTO 转换为前端 store 的 BatchItem 格式。
 */
import type { BatchItem, BatchStatus, AutoPluginTrailItemDto } from '@/stores/batchStore'

/**
 * 后端 int 状态 → UI 显示映射
 */
export const STATUS_MAP: Record<number, { label: string; color: string }> = {
  0: { label: '解析中', color: 'processing' },
  1: { label: '已暂存', color: 'default' },
  2: { label: '质检中', color: 'processing' },
  3: { label: '已创建卡片', color: 'success' },
  4: { label: '处理中', color: 'processing' },
  5: { label: '已完成', color: 'success' },
  6: { label: '失败', color: 'error' },
  7: { label: '部分完成', color: 'warning' },
  8: { label: '已撤销', color: 'default' },
}

/**
 * 后端 int 状态 → 前端 BatchStatus 映射
 * 后端 FStatus 字段已改为 int（0-8）
 */
const INTERNAL_STATUS_MAP: Record<number, BatchStatus> = {
  0: 'processing',   // 解析中
  1: 'pending',      // 已暂存
  2: 'processing',   // 质检中
  3: 'success',      // 已创建卡片
  4: 'processing',   // 处理中
  5: 'success',      // 已完成
  6: 'error',        // 失败
  7: 'partial',      // 部分完成
  8: 'error',        // 已撤销（配合 isRevoked 使用）
}

/**
 * 后端 PluginTrailItem int 状态 → 前端显示字符串
 * 10=待处理, 11=进行中, 12=已完成, 13=失败, 14=已跳过
 */
const PLUGIN_STATUS_MAP: Record<number, string> = {
  10: 'Pending',
  11: 'Running',
  12: 'Completed',
  13: 'Failed',
  14: 'Completed', // 已跳过视为 Completed
}

/** 将后端 PluginTrailItem 数组映射为前端 AutoPluginTrailItemDto 数组 */
export function mapPlugins(plugins: Array<{ name: string; index: number; status: number }>): AutoPluginTrailItemDto[] {
  return plugins.map(p => ({
    pluginName: p.name,
    sortIndex: p.index,
    status: PLUGIN_STATUS_MAP[p.status] ?? 'Pending',
  }))
}

const SIGNALR_STATUS_MAP: Record<string, BatchStatus> = {
  Processing: 'processing',
  Completed: 'success',
  Failed: 'error',
  PartialCompleted: 'partial',
  PendingPipeline: 'pending',
}

/** 将后端状态映射为前端 BatchStatus，兼容 batch-sync 数字状态和 SignalR 字符串状态 */
export function mapStatus(status: number | string): BatchStatus {
  if (typeof status === 'string') {
    return SIGNALR_STATUS_MAP[status] ?? 'processing'
  }
  return INTERNAL_STATUS_MAP[status] ?? 'processing'
}

/** 后端 batch-sync 接口返回的单条 DTO */
export interface BatchSyncItemDto {
  batchId: number
  batchNo?: string
  fileName: string
  fileSize?: number
  status: number
  importType?: string
  flowName?: string
  createTime: string
  currentNodeName?: string
  currentStepName?: string
  progressPercent?: number
  errorMessage?: string
  isRevoked?: boolean
  isStale?: boolean
  totalRows?: number
  errorCount?: number
  processedRows?: number
  successCount?: number
  failedCount?: number
  skippedCount?: number
  bizTag?: string
  tagColor?: string
  orgName?: string
  accountSetName?: string
  creatorName?: string
  version: number
  /** GetQueueBatches / reconcile API 内嵌的插件轨迹快照 */
  plugins?: Array<{ name: string; index: number; status: number }>
}

/** 后端 batch-sync 接口响应 */
export interface BatchSyncResponse {
  batches: BatchSyncItemDto[]
  maxVersion: number
}

/** 将后端 DTO 映射为 BatchItem */
export function mapToBatchItem(dto: BatchSyncItemDto): BatchItem {
  const mappedStatus = mapStatus(dto.status)
  const isTerminalSuccess = mappedStatus === 'success' || mappedStatus === 'partial'
  return {
    id: dto.batchId,
    batchNo: dto.batchNo,
    fileName: dto.fileName,
    fileSize: dto.fileSize,
    status: mappedStatus,
    importType: dto.importType,
    flowName: dto.flowName,
    createTime: dto.createTime,
    currentNodeName: dto.currentNodeName,
    currentStepName: dto.currentStepName,
    progressPercent: dto.progressPercent,
    progress: dto.progressPercent,
    // [BugFix] success/partial 终态时 errorMessage 应为空，否则折叠态第二行被隐藏
    errorMessage: isTerminalSuccess ? undefined : dto.errorMessage,
    isRevoked: dto.isRevoked,
    isStale: dto.isStale,
    totalRows: dto.totalRows,
    errorCount: dto.errorCount,
    processedRows: dto.processedRows,
    successCount: dto.successCount,
    failedCount: dto.failedCount,
    skippedCount: dto.skippedCount,
    bizTag: dto.bizTag,
    tagColor: dto.tagColor,
    orgName: dto.orgName,
    accountSetName: dto.accountSetName,
    creatorName: dto.creatorName,
    // 内嵌 plugins 快照 → autoPluginTrail
    autoPluginTrail: dto.plugins?.length
      ? { autoPlugins: mapPlugins(dto.plugins) }
      : undefined,
    _version: dto.version,
  }
}
