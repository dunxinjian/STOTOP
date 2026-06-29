// 积分模块 API
import { get, post, put, del } from './request'
import type {
  PointSourceDto, CreatePointSourceRequest, UpdatePointSourceRequest,
  PointRuleListDto, PointRuleDetailDto, CreatePointRuleRequest, UpdatePointRuleRequest, PointRulePagedRequest,
  PointApplicationListDto, PointApplicationDetailDto, SubmitPointApplicationRequest, ApprovePointApplicationRequest,
  PointRecordListDto, ManualAwardRequest, ManualDeductRequest, PointRecordPagedRequest,
  PointAccountDto, PointStatisticsDto,
  RedeemItemListDto, RedeemItemDetailDto, CreateRedeemItemRequest, UpdateRedeemItemRequest, ExchangeRequest,
  RedeemRecordListDto,
  ManagerQuotaListDto, SaveManagerQuotaRequest, MyQuotaDto,
  RankingListDto, DepartmentRankingDto, MyRankingDto,
  PointEventDto,
} from '@/types/points'

// ==================== 来源管理 ====================

// 获取来源列表
export function getPointSources() {
  return get<PointSourceDto[]>('/points/sources')
}

// 创建来源
export function createPointSource(data: CreatePointSourceRequest) {
  return post<PointSourceDto>('/points/sources', data)
}

// 更新来源
export function updatePointSource(id: number, data: UpdatePointSourceRequest) {
  return put<PointSourceDto>(`/points/sources/${id}`, data)
}

// 启用/禁用来源
export function togglePointSource(id: number) {
  return put<boolean>(`/points/sources/${id}/toggle`)
}

// ==================== 规则管理 ====================

// 规则列表
export function getPointRules(params?: PointRulePagedRequest) {
  return get<{ items: PointRuleListDto[]; total: number }>('/points/rules', params)
}

// 规则详情
export function getPointRule(id: number) {
  return get<PointRuleDetailDto>(`/points/rules/${id}`)
}

// 创建规则
export function createPointRule(data: CreatePointRuleRequest) {
  return post<PointRuleDetailDto>('/points/rules', data)
}

// 更新规则
export function updatePointRule(id: number, data: UpdatePointRuleRequest) {
  return put<PointRuleDetailDto>(`/points/rules/${id}`, data)
}

// 删除规则
export function deletePointRule(id: number) {
  return del<boolean>(`/points/rules/${id}`)
}

// 启用/禁用规则
export function togglePointRule(id: number) {
  return put<boolean>(`/points/rules/${id}/toggle`)
}

// ==================== 核心积分 ====================

// 手动奖分
export function awardPoints(data: ManualAwardRequest) {
  return post<PointRecordListDto>('/points/award', data)
}

// 手动扣分
export function deductPoints(data: ManualDeductRequest) {
  return post<PointRecordListDto>('/points/deduct', data)
}

// 事件触发积分
export function triggerPointEvent(data: PointEventDto) {
  return post<boolean>('/points/trigger', data)
}

// 积分流水列表
export function getPointRecords(params?: PointRecordPagedRequest) {
  return get<{ items: PointRecordListDto[]; total: number }>('/points/records', params)
}

// 我的积分明细
export function getMyPointRecords(params?: PointRecordPagedRequest) {
  return get<{ items: PointRecordListDto[]; total: number }>('/points/records/my', params)
}

// 查询账户
export function getPointAccount(userId: number) {
  return get<PointAccountDto>('/points/account', { userId })
}

// 我的账户
export function getMyPointAccount() {
  return get<PointAccountDto>('/points/account/my')
}

// 统计看板
export function getPointStatistics() {
  return get<PointStatisticsDto>('/points/statistics')
}

// ==================== 申请审批 ====================

// 提交申请
export function submitPointApplication(data: SubmitPointApplicationRequest) {
  return post<PointApplicationDetailDto>('/points/applications', data)
}

// 申请列表（管理员）
export function getPointApplications(params?: {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  status?: number | null
  applicantId?: number | null
  ruleId?: number | null
}) {
  return get<{ items: PointApplicationListDto[]; total: number }>('/points/applications', params)
}

// 我的申请
export function getMyApplications(params?: {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  status?: number | null
}) {
  return get<{ items: PointApplicationListDto[]; total: number }>('/points/applications/my', params)
}

// 待审批列表
export function getPendingApplications(params?: {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  applicantId?: number | null
}) {
  return get<{ items: PointApplicationListDto[]; total: number }>('/points/applications/pending', params)
}

// 审批通过
export function approveApplication(id: number, data: ApprovePointApplicationRequest) {
  return put<boolean>(`/points/applications/${id}/approve`, data)
}

// 审批拒绝
export function rejectApplication(id: number, reason: string) {
  return put<boolean>(`/points/applications/${id}/reject`, reason as any)
}

// ==================== 兑换 ====================

// 商品列表
export function getRedeemItems(params?: {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  category?: number | null
  status?: number | null
}) {
  return get<{ items: RedeemItemListDto[]; total: number }>('/points/redeem/items', params)
}

// 创建商品
export function createRedeemItem(data: CreateRedeemItemRequest) {
  return post<RedeemItemDetailDto>('/points/redeem/items', data)
}

// 更新商品
export function updateRedeemItem(id: number, data: UpdateRedeemItemRequest) {
  return put<RedeemItemDetailDto>(`/points/redeem/items/${id}`, data)
}

// 上下架商品
export function toggleRedeemItem(id: number) {
  return put<boolean>(`/points/redeem/items/${id}/toggle`)
}

// 执行兑换
export function exchangePoints(data: ExchangeRequest) {
  return post<RedeemRecordListDto>('/points/redeem/exchange', data)
}

// 兑换记录
export function getRedeemRecords(params?: {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  userId?: number | null
  itemId?: number | null
  status?: number | null
}) {
  return get<{ items: RedeemRecordListDto[]; total: number }>('/points/redeem/records', params)
}

// 我的兑换
export function getMyRedeemRecords(params?: {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  status?: number | null
}) {
  return get<{ items: RedeemRecordListDto[]; total: number }>('/points/redeem/records/my', params)
}

// 确认发放
export function deliverRedeem(id: number, data?: { remark?: string | null }) {
  return put<boolean>(`/points/redeem/records/${id}/deliver`, data)
}

// 取消兑换
export function cancelRedeem(id: number) {
  return put<boolean>(`/points/redeem/records/${id}/cancel`)
}

// ==================== 配额 ====================

// 配额列表
export function getManagerQuotas(params?: {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  managerId?: number | null
  yearMonth?: string | null
  status?: number | null
}) {
  return get<{ items: ManagerQuotaListDto[]; total: number }>('/points/quotas', params)
}

// 创建/更新配额
export function saveManagerQuota(data: SaveManagerQuotaRequest) {
  return post<ManagerQuotaListDto>('/points/quotas', data)
}

// 我的当月配额
export function getMyQuota() {
  return get<MyQuotaDto>('/points/quotas/my')
}

// ==================== 排行榜 ====================

// 排行榜列表
export function getRankings(params?: {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  dimension?: number
  period?: string | null
}) {
  return get<{ items: RankingListDto[]; total: number }>('/points/rankings', params)
}

// 部门排名
export function getDepartmentRankings(params?: {
  dimension?: number
  period?: string | null
}) {
  return get<DepartmentRankingDto[]>('/points/rankings/department', params)
}

// 我的排名
export function getMyRanking(params?: {
  dimension?: number
  period?: string | null
}) {
  return get<MyRankingDto>('/points/rankings/my', params)
}
