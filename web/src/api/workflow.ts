import { get } from '@/api/request'

// ===== 触发动作 =====

export interface TriggerAction {
  id: number
  key: string
  label: string
  icon: string | null
  module: string
  route: string
  category: 'upload' | 'create' | 'apply'
  description: string | null
  order: number
}

/** 获取当前用户可用的触发动作列表 */
export function getAvailableTriggerActions() {
  return get<TriggerAction[]>('/workflow/trigger-actions/available', {}, { silent: true } as any)
}

// ===== 仪表盘 =====

export interface DashboardOverview {
  totalPending: number
  totalInProgress: number
  completedToday: number
  overdueCount: number
  avgProcessHours: number
  slaRate: number
}

export interface StatusGroup {
  status: number
  statusText: string
  count: number
}

export interface ModuleGroup {
  module: string
  pendingCount: number
  inProgressCount: number
  completedCount: number
}

export interface AssigneeStats {
  assigneeId: number
  assigneeName: string
  pendingCount: number
  inProgressCount: number
  completedCount: number
  avgProcessHours: number
}

export interface TrendData {
  dates: string[]
  createdCounts: number[]
  completedCounts: number[]
}

export interface OverdueItem {
  id: number
  uid: string
  title: string
  module: string | null
  assigneeId: number | null
  assigneeName: string | null
  status: number
  priority: number
  deadline: string
  createTime: string
  overdueHours: number
}

export interface OverduePage {
  total: number
  page: number
  pageSize: number
  items: OverdueItem[]
}

const BASE = '/workflow/dashboard'

export function getDashboardOverview() {
  return get<DashboardOverview>(`${BASE}/overview`)
}

export function getByStatus() {
  return get<StatusGroup[]>(`${BASE}/by-status`)
}

export function getByModule() {
  return get<ModuleGroup[]>(`${BASE}/by-module`)
}

export function getByAssignee() {
  return get<AssigneeStats[]>(`${BASE}/by-assignee`)
}

export function getTrend(days = 7) {
  return get<TrendData>(`${BASE}/trend`, { days })
}

export function getOverdueItems(page = 1, pageSize = 20) {
  return get<OverduePage>(`${BASE}/overdue-items`, { page, pageSize })
}
