// WorkHub 智能工作台 API
import { get, post } from './request'

// ===== 类型定义 =====

export interface WorkItemAction {
  key: string
  label: string
  /**
   * 操作分层：
   * - 'primary'：始终可见的主操作按钮（最多2个）
   * - 'secondary'：收纳到“...”下拉菜单中的次操作
   * 兼容历史值 'default' | 'danger'（旧后端遗留），UI 不再依赖其样式语义。
   */
  type?: 'primary' | 'secondary' | 'default' | 'danger'
  /** 是否为终结性操作（执行后任务进入归档/不可逆状态），用于二次确认与归档样式 */
  finalizes?: boolean
  /** 执行前是否需要二次确认 */
  needsConfirm?: boolean
  /** 二次确认弹窗中的影响摘要清单 */
  confirmSummary?: string[]
  route?: string
  conversationSessionCode?: string
}

/** 工作项相关链接（侧栏“相关业务”等位置使用） */
export interface RelatedLink {
  label: string
  route: string
  icon?: string
  summary?: string
  permission?: string
}

export interface WorkItem {
  id: string
  source: 'oa' | 'quality' | 'task' | 'datacenter' | 'cardflow' | 'contract' | 'points' | 'finance' | 'system' | 'workflow'
  category: 'approval' | 'task' | 'alert' | 'notification' | 'reminder' | 'initiated'
  /** 业务类型键（后端派生，用于上色/分组，如 'voucher' | 'quality' | 'flow:费用报销'） */
  bizTypeKey: string
  /** 业务类型中文标签（用户可见，如 '凭证复核' | '质量异常' | '费用报销'） */
  bizTypeLabel: string
  priority: 'urgent' | 'high' | 'normal' | 'low'
  title: string
  summary: string
  timestamp: string
  deadline?: string
  actions: WorkItemAction[]
  /** 关联的相关业务链接 */
  relatedLinks?: RelatedLink[]
  conversationSessionCode?: string
  detailRoute?: string
  metadata: Record<string, any>
}

export interface WorkHubStats {
  total: number
  approval: number
  task: number
  alert: number
  notification: number
  reminder: number
  initiated: number
}

export interface WorkItemPageResult {
  items: WorkItem[]
  total: number
  pageIndex: number
  pageSize: number
}

// ===== API 调用 =====

/** 获取工作项列表（分页 + 筛选） */
export function getWorkItems(params: {
  page: number
  pageSize: number
  category?: string
  priority?: string
  sources?: string
  startDate?: string
  endDate?: string
}) {
  return get<WorkItemPageResult>('/workhub/items', params, { silent: true } as any)
}

/** 获取工作台统计信息 */
export function getWorkHubStats() {
  return get<WorkHubStats>('/workhub/stats', {}, { silent: true } as any)
}

/** 执行工作项操作（如：同意、拒绝等） */
export function executeWorkItemAction(itemId: string, actionKey: string) {
  return post<void>(`/workhub/items/${itemId}/actions/${actionKey}`)
}

export interface WorkItemsWithStatsResult {
  items: WorkItemPageResult
  stats: WorkHubStats
}

/** 获取工作项列表 + 统计信息（合并接口，用于初始化） */
export function getWorkItemsWithStats(params: {
  page: number
  pageSize: number
  priority?: string
}) {
  return get<WorkItemsWithStatsResult>('/workhub/items-with-stats', params, { silent: true } as any)
}

// ===== 导入异常处理 API =====

export interface AffectedRow {
  id: number
  batchName: string
  originalRowNumber: number
  bizKey: string
  errorMessage: string
  status: string
  rawData?: Record<string, any>
}

export interface AffectedRowsResult {
  items: AffectedRow[]
  total: number
  page: number
  pageSize: number
}

/** 获取异常影响行（分页） */
export function getAffectedRows(workItemId: number, page = 1, pageSize = 20) {
  return get<AffectedRowsResult>(`/workitem/${workItemId}/affected-rows`, { page, pageSize })
}

/** 修正后重跑 */
export function rerunWorkItem(workItemId: number, startAgentCode?: string) {
  return post<void>(`/workitem/${workItemId}/rerun`, { startAgentCode })
}

/** 标记跳过 */
export function skipWorkItem(workItemId: number, remark?: string) {
  return post<void>(`/workitem/${workItemId}/skip`, { remark })
}

/** 批次整体重跑 */
export function rerunBatch(batchId: number) {
  return post<void>(`/workitem/batch/${batchId}/rerun`)
}
