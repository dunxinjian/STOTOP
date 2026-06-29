// 数据质量中心 API
import { get, post, put, del } from './request'
import type { PagedResult, PagedRequest } from './express'

// ==================== 类型定义 ====================

export interface QualityCenterOverviewDto {
  pendingShopCount: number
  autoCreatedPendingCount: number
  emptyShopRowCount: number
  unrecognizedNetworkPointCount?: number
  networkPointMismatchCount?: number
  affectedBatchCount: number
  blockedBatchIds: number[]
}

export interface PendingShopItemDto {
  name: string
  platform?: string
  isAutoCreated: boolean
  needsAssignment: boolean
  status: number
  createdTime: string
  remark?: string
  affectedWaybillCount: number
  affectedBatchIds: number[]
}

export interface PendingShopQueryRequest extends PagedRequest {
  isAutoCreated?: boolean
  keyword?: string
  batchId?: number
}

export interface CompleteShopConfigRequest {
  shopName: string
  clientId?: string
  pricePlanId?: number
  expiryDate?: string
  assignmentRemark?: string
  skipPricePlanCheck?: boolean
}

export interface CompleteShopConfigResultDto {
  shopName: string
  assignmentId: number
  completed: boolean
  pricePlanWarning?: string
}

export interface EmptyShopRowItemDto {
  errorId: number
  batchId: number
  stagingId?: number
  waybillNo?: string
  waybillDate?: string
  errorMessage?: string
  dispatchStatus?: string
  createTime: string
}

export interface EmptyShopRowQueryRequest extends PagedRequest {
  batchId?: number
  dispatchStatus?: string
}

export interface FillEmptyShopAccountRequest {
  errorIds: number[]
  shopAccount: string
}

export interface IgnoreEmptyShopRowsRequest {
  errorIds: number[]
  reason?: string
}

export interface EmptyShopRowBatchResultDto {
  affectedCount: number
  message: string
}

export interface RerunBillingRequest {
  batchId: number
}

export interface RerunBillingResultDto {
  batchId: number
  enqueued: boolean
  message: string
}

// ==================== API ====================

export function getQualityCenterOverview() {
  return get<QualityCenterOverviewDto>('/express/quality-center/overview')
}

export function getPendingShops(params: PendingShopQueryRequest) {
  return get<PagedResult<PendingShopItemDto>>('/express/quality-center/pending-shops', params)
}

export function completeShopConfig(data: CompleteShopConfigRequest) {
  return post<CompleteShopConfigResultDto>('/express/quality-center/pending-shops/complete', data)
}

export function getEmptyShopRows(params: EmptyShopRowQueryRequest) {
  return get<PagedResult<EmptyShopRowItemDto>>('/express/quality-center/empty-shop-rows', params)
}

export function fillEmptyShopAccount(data: FillEmptyShopAccountRequest) {
  return post<EmptyShopRowBatchResultDto>('/express/quality-center/empty-shop-rows/fill', data)
}

export function ignoreEmptyShopRows(data: IgnoreEmptyShopRowsRequest) {
  return post<EmptyShopRowBatchResultDto>('/express/quality-center/empty-shop-rows/ignore', data)
}

export function rerunBilling(data: RerunBillingRequest) {
  return post<RerunBillingResultDto>('/express/quality-center/rerun-billing', data)
}

// ==================== 未识别网点 ====================

export interface UnrecognizedNetworkPointItemDto {
  errorId: number
  batchId: number
  waybillNo?: string
  networkPointName: string
  dispatchStatus?: string
  createTime: string
}

export interface UnrecognizedNetworkPointQueryRequest extends PagedRequest {
  batchId?: number
  keyword?: string
}

export interface AssociateNetworkPointRequest {
  networkPointName: string
  networkPointCode: string
  batchId?: number
}

export interface AssociateNetworkPointResultDto {
  resolvedCount: number
  message: string
}

export interface IgnoreNetworkPointErrorsRequest {
  errorIds: number[]
  reason?: string
}

export interface IgnoreNetworkPointErrorsResultDto {
  affectedCount: number
  message: string
}

export function getUnrecognizedNetworkPoints(params: UnrecognizedNetworkPointQueryRequest) {
  return get<PagedResult<UnrecognizedNetworkPointItemDto>>('/express/quality-center/unrecognized-network-points', params)
}

export function associateNetworkPoint(data: AssociateNetworkPointRequest) {
  return post<AssociateNetworkPointResultDto>('/express/quality-center/associate-network-point', data)
}

export function ignoreNetworkPointErrors(data: IgnoreNetworkPointErrorsRequest) {
  return post<IgnoreNetworkPointErrorsResultDto>('/express/quality-center/ignore-network-point-errors', data)
}

// ==================== 网点不一致 ====================

export interface NetworkPointMismatchItemDto {
  errorId: number
  batchId: number
  waybillNo?: string
  mappedNpCode?: string
  quotationNpCode?: string
  dispatchStatus?: string
  createTime: string
}

export interface NetworkPointMismatchQueryRequest {
  batchId?: number
  waybillNo?: string
  pageIndex?: number
  pageSize?: number
}

export function getNetworkPointMismatches(params: NetworkPointMismatchQueryRequest) {
  return get<PagedResult<NetworkPointMismatchItemDto>>('/express/quality-center/network-point-mismatches', params)
}

export function ignoreMismatchErrors(errorIds: number[]) {
  return post<{ affectedCount: number; message: string }>('/express/quality-center/network-point-mismatches/ignore', { errorIds })
}

// ==================== 问题类型配置 ====================

export interface IssueTypeDto {
  id: number
  code: string
  name: string
  description?: string
  module?: string
  sourceAgent?: string
  category?: string
  severityLevel?: string
  isBuiltIn: boolean
  suggestedFix?: string
  detailRoute?: string
  dispatchMode?: string | null
  dispatchTarget?: string | null
  orgScoped: boolean
  timeoutHours: number
  status: number
  createdAt?: string
  updatedAt?: string
}

export interface IssueTypeQueryRequest extends PagedRequest {
  category?: string
  sourceAgent?: string
  status?: number | null
}

export interface IssueTypeCreateRequest {
  code: string
  name: string
  description?: string
  module?: string
  sourceAgent?: string
  category?: string
  severityLevel?: string
  dispatchMode?: string | null
  dispatchTarget?: string | null
  orgScoped?: boolean
  timeoutHours?: number
  suggestedFix?: string
  detailRoute?: string
  status?: number
}

export interface IssueTypeUpdateRequest {
  name?: string
  description?: string
  module?: string
  sourceAgent?: string
  category?: string
  severityLevel?: string
  dispatchMode?: string | null
  dispatchTarget?: string | null
  orgScoped?: boolean
  timeoutHours?: number
  suggestedFix?: string
  detailRoute?: string
  status?: number
}

export function getIssueTypes(params?: IssueTypeQueryRequest) {
  return get<PagedResult<IssueTypeDto>>('/quality-center/issue-types', params)
}

export function createIssueType(data: IssueTypeCreateRequest) {
  return post<IssueTypeDto>('/quality-center/issue-types', data)
}

export function updateIssueType(id: number, data: IssueTypeUpdateRequest) {
  return put<IssueTypeDto>(`/quality-center/issue-types/${id}`, data)
}

export function deleteIssueType(id: number) {
  return del(`/quality-center/issue-types/${id}`)
}

// ==================== 质量管理看板 Dashboard ====================

export interface DashboardSummaryDto {
  pending: number
  processing: number
  resolved: number
  overdueWarning: number
  byType: { type: string; count: number }[]
}

export interface DashboardTrendItemDto {
  date: string
  created: number
  resolved: number
  avgHours: number
}

export interface DashboardWorkloadItemDto {
  userId: number
  userName: string
  pending: number
  processed: number
  avgHours: number
  overdueCount: number
}

export interface DashboardOverdueItemDto {
  id: number
  title: string
  type: string
  createTime: string
  overdueHours: number
  assigneeName?: string
}

export function getDashboardSummary(params?: { orgId?: number; range?: string }) {
  return get<DashboardSummaryDto>('/express/quality-center/dashboard/summary', params)
}

export function getDashboardTrend(params?: { orgId?: number; days?: number }) {
  return get<DashboardTrendItemDto[]>('/express/quality-center/dashboard/trend', params)
}

export function getDashboardWorkload(params?: { orgId?: number }) {
  return get<DashboardWorkloadItemDto[]>('/express/quality-center/dashboard/workload', params)
}

export function getDashboardOverdue(params?: { orgId?: number }) {
  return get<DashboardOverdueItemDto[]>('/express/quality-center/dashboard/overdue', params)
}
