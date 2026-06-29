import { get, post } from './request'

// 公共查询参数
export interface CarrierQuery {
  carrier: string
  from: string
  to: string
  networkCode?: string
}

// ── 视图1 网点总览 ──
export function getNetworkKpi(params: CarrierQuery) {
  return get('/quality/carrier-dashboard/network/kpi', params)
}
export function getNetworkTrend(params: CarrierQuery) {
  return get('/quality/carrier-dashboard/network/trend', params)
}
export function getDomainDistribution(params: CarrierQuery) {
  return get('/quality/carrier-dashboard/network/domain-distribution', params)
}
export function getFeeByDomain(params: CarrierQuery) {
  return get('/quality/carrier-dashboard/network/fee-by-domain', params)
}
export interface NetworkOption { code: string; name?: string }
export function getNetworkOptions(params: { carrier: string }) {
  return get<NetworkOption[]>('/quality/carrier-dashboard/network/options', params)
}

// ── 视图2 员工质量 ──
export function getEmployeeRank(params: CarrierQuery & { dimension: string; topN?: number }) {
  return get('/quality/carrier-dashboard/employee/rank', params)
}
export function getEmployeeMetrics(params: CarrierQuery & { page?: number; size?: number }) {
  return get('/quality/carrier-dashboard/employee/metrics', params)
}
export function getEmployeeTimeline(empNo: string, params: { carrier: string; from: string; to: string }) {
  return get(`/quality/carrier-dashboard/employee/${empNo}/timeline`, params)
}

// ── 视图3 问题件追踪 ──
export interface EventQueryParams extends CarrierQuery {
  empNo?: string
  domain?: string
  platform?: string
  severity?: number
  multiDomainOnly?: boolean
  pendingOnly?: boolean
  page?: number
  size?: number
}
export function getEvents(params: EventQueryParams) {
  return get('/quality/carrier-dashboard/events', params)
}
export function getPendingCount(params: { carrier: string }) {
  return get('/quality/carrier-dashboard/pending-count', params)
}

// 鉴权 blob 下载（带 Authorization header，避免 window.open 丢 JWT）
export function exportEvents(params: EventQueryParams): Promise<Blob> {
  return get<Blob>('/quality/carrier-dashboard/events/export', params, { responseType: 'blob' })
}

// ── 认领页（复用现有 unify 接口）──
export function getPendingEmployees() {
  return get('/quality/unify/pending-employees')
}
export function rematchUnify() {
  return post('/quality/unify/rematch')
}
