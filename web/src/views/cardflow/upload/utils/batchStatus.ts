/**
 * batchStatus — BatchCard 状态判断纯函数模块
 *
 * 将 34+ 条件分支提取为可测试的纯函数，使模板改为数据驱动渲染。
 * 所有函数均为纯函数：无副作用，仅依赖输入参数。
 */
import type { BatchItem, BatchStatus } from '@/stores/batchStore'

// ================ 类型定义 ================

/** 有效状态（聚合 batch 多字段为单一展示状态） */
export type EffectiveStatus =
  | 'uploading' | 'pending'
  | 'processing' | 'success' | 'partial'
  | 'error' | 'revoked'

/** 角色类型 */
export type RoleType = 'uploader' | 'processor' | 'manager'

/** 操作按钮描述 */
export interface Action {
  key: string
  label: string
  type?: 'primary' | 'text' | 'default'
  danger?: boolean
  icon?: 'delete'
  className?: string
}

/** 状态标签信息 */
export interface StatusTagInfo {
  text: string
  color: string
}

/** 进度环信息
 * @deprecated 请使用 ProgressBarInfo
 */
export interface ProgressRingInfo {
  percent: number
  color: string
  cssClass: string
}

/** 线性进度条信息 */
export interface ProgressBarInfo {
  percent: number
  color: string
}

// ================ 核心纯函数 ================

/** 聚合 batch 多字段为单一展示状态 */
export function getEffectiveStatus(batch: BatchItem): EffectiveStatus {
  if (batch.isRevoked) return 'revoked'
  return batch.status as EffectiveStatus
}

/** 状态圆点 CSS class */
export function getStatusDotClass(batch: BatchItem): string {
  const map: Record<string, string> = {
    uploading: 'uploading',
    processing: 'processing',
    pending: 'pending',
    error: 'error',
    success: 'success',
    partial: 'partial',

  }
  return map[batch.status] || 'pending'
}

/** 状态文本标签：所有异常/过渡/特殊状态都会在卡片主行显示 */
export function getStatusTag(batch: BatchItem): StatusTagInfo | null {
  if (batch.isRevoked) return { text: '已撤销', color: 'default' }
  if (batch.status === 'uploading') {
    return batch.isStale
      ? { text: '上传卡住', color: 'red' }
      : { text: '上传中', color: 'blue' }
  }
  if (batch.status === 'processing') {
    return batch.isStale
      ? { text: '处理卡住', color: 'red' }
      : { text: '处理中', color: 'blue' }
  }

  if (batch.status === 'pending') return { text: '待确认', color: 'gold' }
  if (batch.status === 'error') return { text: '有异常', color: 'red' }
  if (batch.status === 'partial') return { text: '部分完成', color: 'gold' }
  return null
}

/** 是否可删除：所有已产生批次号的批次均可删除 */
export function canDelete(_batch: BatchItem): boolean {
  return true
}

/** 是否可分配 */
export function canAssign(batch: BatchItem): boolean {
  return !(['uploading', 'success'] as BatchStatus[]).includes(batch.status)
}

/** 是否显示催办按钮 */
export function showUrge(batch: BatchItem, role: RoleType): boolean {
  const s = batch.status
  if (['uploading', 'success'].includes(s)) return false
  if (role === 'uploader' && ['pending', 'error'].includes(s) && (batch.waitHours ?? 0) >= 1) return true
  if (role === 'manager' && (batch.waitHours ?? 0) >= 2) return true
  return false
}

/** 按角色返回可用操作列表（替代 BatchCard 内 v-if 判断链） */
export function getStatusActions(batch: BatchItem, role: RoleType, currentUserName?: string): Action[] {
  const actions: Action[] = []
  const s = batch.status

  // error: 处理异常 + 重试
  if (s === 'error') {
    actions.push({ key: 'viewErrors', label: '处理异常', type: 'primary', danger: true })
    actions.push({ key: 'retry', label: '重 试' })
  }

  // pending: 重试
  if (s === 'pending') {
    actions.push({ key: 'retry', label: '重试', type: 'primary' })
  }

  // processing 卡住或有异常: 强制重试
  if (s === 'processing' && (batch.isStale || batch.errorMessage)) {
    actions.push({ key: 'retry', label: '强制重试', type: 'primary', danger: true })
  }

  // uploading: 卡住或有异常时显示取消上传，正常上传中不额外加操作（删除兜底）
  if (s === 'uploading' && (batch.isStale || batch.errorMessage)) {
    actions.push({ key: 'cancelUpload', label: '取消上传', danger: true })
  }

  // partial: 重新计费 + 查看异常
  if (s === 'partial') {
    actions.push({ key: 'recalculate', label: '重新计费', type: 'primary' })
    actions.push({ key: 'viewErrors', label: '查看异常' })
  }

  // 催办
  if (showUrge(batch, role)) {
    actions.push({ key: 'urge', label: '催办', className: 'btn-urge' })
  }

  // 分配 / 转交（仅 manager）
  if (role === 'manager' && canAssign(batch)) {
    if (!batch.assigneeName || batch.assigneeName === '系统自动') {
      actions.push({ key: 'assign', label: '分配' })
    } else {
      actions.push({ key: 'transfer', label: '转交' })
    }
  }

  // 删除
  if (canDelete(batch)) {
    actions.push({ key: 'delete', label: '删除', type: 'text', danger: true, icon: 'delete' })
  }

  return actions
}

/** 线性进度条信息 */
export function getProgressBarInfo(batch: BatchItem): ProgressBarInfo {
  let percent = 0
  let color = 'var(--border)'

  if (batch.status === 'uploading') {
    percent = batch.uploadProgress || 0
    color = 'var(--color-info)'
  } else if (batch.status === 'processing') {
    percent = getFlowProgress(batch)
    color = (batch.waitHours ?? 0) >= 4 ? 'var(--biz-waybill)' : 'var(--color-info)'
  } else if (batch.status === 'success') {
    percent = 100
    color = 'var(--color-success)'
  } else if (batch.status === 'error') {
    percent = 100
    color = 'var(--color-danger)'
  } else if (batch.status === 'partial') {
    percent = 100
    color = 'var(--color-warning)'
  }

  return { percent, color }
}

/**
 * 圆环进度信息
 * @deprecated 请使用 getProgressBarInfo
 */
export function getProgressRing(batch: BatchItem): ProgressRingInfo {
  let percent = 0
  let color = 'var(--border)'
  let cssClass = ''

  if (batch.status === 'uploading') {
    percent = batch.uploadProgress || 0
    color = 'var(--color-info)'
  } else if (batch.status === 'processing') {
    percent = getFlowProgress(batch)
    color = (batch.waitHours ?? 0) >= 4 ? 'var(--biz-waybill)' : 'var(--color-info)'
  } else if (batch.status === 'success') {
    percent = 100
    color = 'var(--color-success)'
    cssClass = 'completed'
  } else if (batch.status === 'error') {
    percent = 100
    color = 'var(--color-danger)'
    cssClass = 'error'
  } else if (batch.status === 'partial') {
    percent = 100
    color = 'var(--color-warning)'
  }

  return { percent, color, cssClass }
}

/** 整体管道进度百分比 */
export function getFlowProgress(batch: BatchItem): number {
  if (batch.status === 'success') return 100
  if (batch.status === 'error' || batch.status === 'partial') return 100
  if (!batch.autoPluginTrail || !batch.autoPluginTrail.autoPlugins.length) {
    return batch.progress || 0
  }
  const autoPlugins = batch.autoPluginTrail.autoPlugins
  const total = autoPlugins.length
  const completedCount = autoPlugins.filter(a => a.status === 'Completed' || a.status === 'Failed').length
  const runningWeight = autoPlugins
    .filter(a => a.status === 'Running')
    .reduce((sum, plugin) => sum + Math.max(0.05, Math.min((plugin.dataProgressPercent ?? 50) / 100, 0.99)), 0)
  return Math.round(((completedCount + runningWeight) / total) * 100)
}

/** 是否显示停滞预警图标 */
export function showStaleIcon(batch: BatchItem, role: RoleType): boolean {
  return role === 'manager' && (batch.waitHours ?? 0) >= 4
}

/** 卡片根元素 CSS class 列表 */
export function getCardClasses(batch: BatchItem, role: RoleType, currentUserName: string): string[] {
  const cls: string[] = []
  if (role === 'processor') {
    if (batch.assigneeName === currentUserName) cls.push('card-mine')
    else cls.push('card-dimmed')
  }
  if (role === 'manager' && (batch.waitHours ?? 0) >= 4) cls.push('card-stale')
  return cls
}

/** 管道是否已匹配 */
export function isFlowMatched(batch: BatchItem): boolean {
  return ['processing', 'success', 'completed'].includes(batch.status)
}
