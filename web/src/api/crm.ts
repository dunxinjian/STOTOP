// CRM 模块 API
import { get, post, put, del } from './request'

// ==================== 类型定义 ====================

// --- CRM 组织/角色映射 ---

export interface CrmRoleMappingListItemDto {
  id: number
  orgId: number
  employeeId: number
  role: number
  createdTime: string
}

export interface CrmRoleMappingDto extends CrmRoleMappingListItemDto {
  creatorName?: string
}

export interface CreateRoleMappingRequest {
  orgId: number
  employeeId: number
  /** 角色类型：1=BD, 2=运维 */
  role: number
}

export interface UpdateRoleMappingRequest {
  role: number
}

export interface RoleMappingQueryRequest {
  pageIndex?: number
  pageSize?: number
  orgId?: number
  role?: number
}

// --- 客户 ---

export interface CustomerListItemDto {
  id: number
  code?: string
  /** 简称 */
  shortName: string
  /** 全称 */
  fullName?: string
  contact?: string
  phone?: string
  industry?: string
  status: number
  orgId?: number
  bdEmployeeId?: number
  serviceObjectId?: string
  createdTime: string
}

export interface CustomerStatusGroupDto {
  status: number
  statusName: string
  count: number
}

export interface CustomerStatisticsDto {
  totalCount: number
  byStatus: CustomerStatusGroupDto[]
}

export interface CustomerContactDto {
  id: number
  customerId: number
  name: string
  phone?: string
  position?: string
  roleTag?: string
  isPrimary: boolean
  remark?: string
  createdTime: string
}

export interface CustomerDto extends CustomerListItemDto {
  serviceObjectId?: string
  scale?: string
  maintenanceEmployeeId?: number
  creatorName?: string
  updaterName?: string
  updatedTime?: string
  contacts: CustomerContactDto[]
}

export interface CreateContactRequest {
  name: string
  phone?: string
  position?: string
  roleTag?: string
  isPrimary: boolean
  remark?: string
}

export interface CreateCustomerRequest {
  code?: string
  /** 简称（必填） */
  shortName: string
  /** 全称（可选） */
  fullName?: string
  contact?: string
  phone?: string
  industry?: string
  scale?: string
  orgId?: number
  bdEmployeeId?: number
  maintenanceEmployeeId?: number
  contacts?: CreateContactRequest[]
}

export interface UpdateCustomerRequest extends CreateCustomerRequest {}

export interface CustomerQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  status?: number
  orgId?: number
  bdEmployeeId?: number
  industry?: string
}

export interface TransferCustomerRequest {
  transferType: number
  newOrgId?: number
  newBdEmployeeId?: number
  reason?: string
}

export interface CustomerDuplicateCheckRequest {
  shortName?: string
  phone?: string
}

export interface CustomerTimelineItemDto {
  type: string
  id: number
  title?: string
  content?: string
  occurredTime: string
  creatorName?: string
}

// --- 拜访记录 ---

export interface VisitRecordListItemDto {
  id: number
  customerId: number
  customerName?: string
  visitorId: number
  visitDate: string
  visitMethod: number
  nextFollowUpDate?: string
  creatorName?: string
  createdTime: string
}

export interface VisitRecordDto extends VisitRecordListItemDto {
  content?: string
}

export interface CreateVisitRecordRequest {
  customerId: number
  visitorId: number
  visitDate: string
  /** 拜访方式：1=上门, 2=电话, 3=微信, 4=其他 */
  visitMethod: number
  content?: string
  nextFollowUpDate?: string
}

export interface UpdateVisitRecordRequest {
  visitDate: string
  visitMethod: number
  content?: string
  nextFollowUpDate?: string
}

export interface VisitRecordQueryRequest {
  pageIndex?: number
  pageSize?: number
  customerId?: number
  visitorId?: number
  visitMethod?: number
  startDate?: string
  endDate?: string
}

export interface VisitStatisticsDto {
  totalVisits: number
  todayVisits: number
  weekVisits: number
  monthVisits: number
  pendingFollowUp: number
}

// --- 服务工单 ---

export interface ServiceOrderListItemDto {
  id: number
  orderNo: string
  customerId: number
  customerName?: string
  assigneeId?: number
  category: number
  priority: number
  title: string
  status: number
  creatorName?: string
  createdTime: string
}

export interface ServiceOrderLogDto {
  id: number
  orderId: number
  operatorId: number
  operationType: number
  content?: string
  attachments?: string
  creatorName?: string
  createdTime: string
}

export interface ServiceOrderDto extends ServiceOrderListItemDto {
  description?: string
  resolvedTime?: string
  updaterName?: string
  updatedTime?: string
  logs: ServiceOrderLogDto[]
}

export interface CreateServiceOrderRequest {
  customerId: number
  assigneeId?: number
  /** 工单类别：1=咨询, 2=投诉, 3=故障, 4=需求, 5=其他 */
  category: number
  /** 优先级：1=紧急, 2=高, 3=中, 4=低 */
  priority?: number
  title: string
  description?: string
}

export interface UpdateServiceOrderRequest {
  assigneeId?: number
  category: number
  priority: number
  title: string
  description?: string
}

export interface ServiceOrderActionRequest {
  operatorId: number
  /** 操作类型：1=接单, 2=处理, 3=转派, 4=关闭 */
  operationType: number
  content?: string
  attachments?: string
  transferToId?: number
}

export interface ServiceOrderQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  customerId?: number
  assigneeId?: number
  category?: number
  priority?: number
  status?: number
}

export interface ServiceOrderStatisticsDto {
  total: number
  pending: number
  processing: number
  waitingConfirm: number
  completed: number
  closed: number
}

// --- 服务反馈 ---

export interface ServiceFeedbackListItemDto {
  id: number
  submitterId: number
  customerId?: number
  category: number
  title: string
  status: number
  handlerId?: number
  creatorName?: string
  createdTime: string
}

export interface ServiceFeedbackDto extends ServiceFeedbackListItemDto {
  orgId?: number
  orderId?: number
  description?: string
  suggestion?: string
  attachments?: string
  handleResult?: string
  updaterName?: string
  updatedTime?: string
}

export interface CreateServiceFeedbackRequest {
  submitterId: number
  orgId?: number
  customerId?: number
  orderId?: number
  /** 反馈类别：1=服务质量, 2=时效问题, 3=费用争议, 4=建议, 5=其他 */
  category: number
  title: string
  description?: string
  suggestion?: string
  attachments?: string
}

export interface UpdateServiceFeedbackRequest {
  category: number
  title: string
  description?: string
  suggestion?: string
  attachments?: string
}

export interface HandleFeedbackRequest {
  handlerId: number
  /** 目标状态：1=已受理, 2=改善中, 3=已落实, 4=已驳回 */
  newStatus: number
  handleResult?: string
}

export interface ServiceFeedbackQueryRequest {
  pageIndex?: number
  pageSize?: number
  customerId?: number
  submitterId?: number
  category?: number
  status?: number
}

// --- 推荐/返佣/外部联系人 ---

export interface ExternalContactDto {
  id: number
  name: string
  phone?: string
  company?: string
  bankAccount?: string
  bankName?: string
  remark?: string
  status: number
  creatorName?: string
  createdTime: string
  updaterName?: string
  updatedTime?: string
}

export interface CreateExternalContactRequest {
  name: string
  phone?: string
  company?: string
  bankAccount?: string
  bankName?: string
  remark?: string
}

export interface UpdateExternalContactRequest extends CreateExternalContactRequest {
  status: number
}

export interface ExternalContactQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  status?: number
}

export interface ReferralDto {
  id: number
  customerId: number
  customerName?: string
  orgId?: number
  referrerType: number
  employeeId?: number
  externalContactId?: number
  externalContactName?: string
  referralDate: string
  description?: string
  commissionRate?: number
  creatorName?: string
  createdTime: string
}

export interface CreateReferralRequest {
  customerId: number
  orgId?: number
  referrerType: number
  employeeId?: number
  externalContactId?: number
  referralDate: string
  description?: string
  commissionRate?: number
}

export interface ReferralQueryRequest {
  pageIndex?: number
  pageSize?: number
  customerId?: number
  orgId?: number
  referrerType?: number
  employeeId?: number
  externalContactId?: number
  startDate?: string
  endDate?: string
}

export interface ReferralStatisticsDto {
  referrerId?: number
  referrerName?: string
  referralCount: number
  totalCommission: number
  paidCommission: number
}

export interface CommissionDto {
  id: number
  referralId: number
  customerId: number
  customerName?: string
  contractId?: number
  commissionAmount: number
  calcBasis?: string
  applicantId: number
  status: number
  oaProcessInstanceId?: number
  paymentOrderId?: number
  creatorName?: string
  createdTime: string
}

export interface CreateCommissionRequest {
  referralId: number
  customerId: number
  contractId?: number
  commissionAmount: number
  calcBasis?: string
  applicantId: number
}

export interface CommissionQueryRequest {
  pageIndex?: number
  pageSize?: number
  referralId?: number
  customerId?: number
  status?: number
  orgId?: number
  startDate?: string
  endDate?: string
}

// --- 号段池/预付款 ---

export interface WaybillPoolDto {
  id: number
  brandCode: string
  prefix?: string
  startNo: string
  endNo: string
  totalCount: number
  allocatedCount: number
  remainingCount: number
  purchaseDate?: string
  unitPrice: number
  status: number
  creatorName?: string
  createdTime: string
}

export interface CreateWaybillPoolRequest {
  brandCode: string
  prefix?: string
  startNo: string
  endNo: string
  totalCount: number
  purchaseDate?: string
  unitPrice: number
}

export interface WaybillPoolQueryRequest {
  pageIndex?: number
  pageSize?: number
  brandCode?: string
  status?: number
}

export interface WaybillAllocationDto {
  id: number
  prepaymentId: number
  customerId: number
  poolId: number
  startNo: string
  endNo: string
  allocatedCount: number
  allocationDate: string
  operatorId: number
  status: number
  creatorName?: string
  createdTime: string
}

export interface AllocateWaybillRequest {
  prepaymentId: number
  customerId: number
  poolId: number
  count: number
  operatorId: number
}

export interface CustomerAccountDto {
  id: number
  customerId: number
  brandCode: string
  balance: number
  totalRecharge: number
  totalConsumption: number
  frozenAmount: number
  creatorName?: string
  createdTime: string
}

export interface PrepaymentDto {
  id: number
  customerId: number
  customerAccountId: number
  orgId?: number
  brandCode: string
  prepayAmount: number
  receivedAmount: number
  expectedWaybillCount: number
  allocatedWaybillCount: number
  status: number
  bankTransactionId?: number
  remark?: string
  creatorName?: string
  createdTime: string
}

export interface CreatePrepaymentRequest {
  customerId: number
  customerAccountId: number
  orgId?: number
  brandCode: string
  prepayAmount: number
  expectedWaybillCount: number
  remark?: string
}

export interface PrepaymentQueryRequest {
  pageIndex?: number
  pageSize?: number
  customerId?: number
  brandCode?: string
  status?: number
  startDate?: string
  endDate?: string
}

// --- 毛利 ---

export interface CustomerProfitDto {
  id: number
  customerId: number
  customerName?: string
  orgId?: number
  period: string
  revenue: number
  cost: number
  profit: number
  profitRate: number
  dataSource: number
  creatorName?: string
  createdTime: string
}

export interface CreateProfitRequest {
  customerId: number
  orgId?: number
  period: string
  revenue: number
  cost: number
}

export interface ProfitQueryRequest {
  pageIndex?: number
  pageSize?: number
  customerId?: number
  orgId?: number
  period?: string
  startDate?: string
  endDate?: string
}

export interface ProfitSummaryDto {
  orgId?: number
  period?: string
  totalRevenue: number
  totalCost: number
  totalProfit: number
  avgProfitRate: number
  customerCount: number
}

export interface ProfitRankingDto {
  customerId: number
  customerName?: string
  totalProfit: number
  totalRevenue: number
  avgProfitRate: number
}

// --- 奖金 ---

export interface BonusDetailDto {
  id: number
  planId: number
  employeeId: number
  amount: number
  bonusType: number
  creatorName?: string
  createdTime: string
}

export interface BonusPlanDto {
  id: number
  orgId?: number
  period: string
  totalAmount: number
  calcRules?: string
  status: number
  oaProcessInstanceId?: number
  creatorName?: string
  createdTime: string
  updaterName?: string
  updatedTime?: string
  details: BonusDetailDto[]
}

export interface CreateBonusDetailRequest {
  employeeId: number
  amount: number
  bonusType: number
}

export interface CreateBonusPlanRequest {
  orgId?: number
  period: string
  totalAmount: number
  calcRules?: string
  details?: CreateBonusDetailRequest[]
}

export interface UpdateBonusPlanRequest extends CreateBonusPlanRequest {}

export interface BonusPlanQueryRequest {
  pageIndex?: number
  pageSize?: number
  orgId?: number
  period?: string
  status?: number
}

export interface BonusDetailQueryRequest {
  pageIndex?: number
  pageSize?: number
  planId?: number
  employeeId?: number
  orgId?: number
}

// ==================== CRM 组织/角色映射 API ====================

export function getRoleMappings(params: RoleMappingQueryRequest) {
  return get('/crm/orgs/role-mappings', params)
}

export function getRoleMappingById(id: number) {
  return get(`/crm/orgs/role-mappings/${id}`)
}

export function createRoleMapping(data: CreateRoleMappingRequest) {
  return post('/crm/orgs/role-mappings', data)
}

export function updateRoleMapping(id: number, data: UpdateRoleMappingRequest) {
  return put(`/crm/orgs/role-mappings/${id}`, data)
}

export function deleteRoleMapping(id: number) {
  return del(`/crm/orgs/role-mappings/${id}`)
}

export function getBdList(orgId: number) {
  return get(`/crm/orgs/${orgId}/bd-list`)
}

export function getMaintenanceList(orgId: number) {
  return get(`/crm/orgs/${orgId}/maintenance-list`)
}

// ==================== 客户 API ====================

export function getCustomerList(params: CustomerQueryRequest) {
  return get('/crm/customers', params)
}

export function getCustomerStatistics() {
  return get('/crm/customers/statistics')
}

export function getCustomerById(id: number) {
  return get(`/crm/customers/${id}`)
}

export function createCustomer(data: CreateCustomerRequest) {
  return post('/crm/customers', data)
}

export function updateCustomer(id: number, data: UpdateCustomerRequest) {
  return put(`/crm/customers/${id}`, data)
}

export function deleteCustomer(id: number) {
  return del(`/crm/customers/${id}`)
}

export function updateCustomerStatus(id: number, status: number) {
  return put(`/crm/customers/${id}/status`, { status })
}

export function transferCustomer(id: number, data: TransferCustomerRequest) {
  return post(`/crm/customers/${id}/transfer`, data)
}

export function checkCustomerDuplicate(data: CustomerDuplicateCheckRequest) {
  return post('/crm/customers/duplicate-check', data)
}

export function getCustomerTimeline(id: number, count: number = 20) {
  return get(`/crm/customers/${id}/timeline`, { count })
}

export function getAllEnabledCustomers() {
  return get('/crm/customers/all-enabled')
}

// ==================== 拜访记录 API ====================

export function getVisitRecordList(params: VisitRecordQueryRequest) {
  return get('/crm/visits', params)
}

export function getVisitRecordById(id: number) {
  return get(`/crm/visits/${id}`)
}

export function createVisitRecord(data: CreateVisitRecordRequest) {
  return post('/crm/visits', data)
}

export function updateVisitRecord(id: number, data: UpdateVisitRecordRequest) {
  return put(`/crm/visits/${id}`, data)
}

export function deleteVisitRecord(id: number) {
  return del(`/crm/visits/${id}`)
}

export function getPendingFollowUp(visitorId?: number) {
  return get('/crm/visits/pending-follow-up', visitorId ? { visitorId } : undefined)
}

export function getVisitStatistics(visitorId?: number, orgId?: number) {
  const params: any = {}
  if (visitorId) params.visitorId = visitorId
  if (orgId) params.orgId = orgId
  return get('/crm/visits/statistics', Object.keys(params).length > 0 ? params : undefined)
}

// ==================== 服务工单 API ====================

export function getServiceOrderList(params: ServiceOrderQueryRequest) {
  return get('/crm/service-orders', params)
}

export function getServiceOrderById(id: number) {
  return get(`/crm/service-orders/${id}`)
}

export function createServiceOrder(data: CreateServiceOrderRequest) {
  return post('/crm/service-orders', data)
}

export function updateServiceOrder(id: number, data: UpdateServiceOrderRequest) {
  return put(`/crm/service-orders/${id}`, data)
}

export function deleteServiceOrder(id: number) {
  return del(`/crm/service-orders/${id}`)
}

export function executeServiceOrderAction(id: number, data: ServiceOrderActionRequest) {
  return post(`/crm/service-orders/${id}/action`, data)
}

export function getServiceOrderStatistics(assigneeId?: number) {
  return get('/crm/service-orders/statistics', assigneeId ? { assigneeId } : undefined)
}

// ==================== 服务反馈 API ====================

export function getServiceFeedbackList(params: ServiceFeedbackQueryRequest) {
  return get('/crm/feedback', params)
}

export function getServiceFeedbackById(id: number) {
  return get(`/crm/feedback/${id}`)
}

export function createServiceFeedback(data: CreateServiceFeedbackRequest) {
  return post('/crm/feedback', data)
}

export function updateServiceFeedback(id: number, data: UpdateServiceFeedbackRequest) {
  return put(`/crm/feedback/${id}`, data)
}

export function deleteServiceFeedback(id: number) {
  return del(`/crm/feedback/${id}`)
}

export function handleServiceFeedback(id: number, data: HandleFeedbackRequest) {
  return post(`/crm/feedback/${id}/handle`, data)
}

// ==================== 推荐 API ====================

export function getReferralList(params: ReferralQueryRequest) {
  return get('/crm/referrals', params)
}

export function getReferralById(id: number) {
  return get(`/crm/referrals/${id}`)
}

export function createReferral(data: CreateReferralRequest) {
  return post('/crm/referrals', data)
}

export function deleteReferral(id: number) {
  return del(`/crm/referrals/${id}`)
}

export function getReferralStatistics(orgId?: number, startDate?: string, endDate?: string) {
  const params: any = {}
  if (orgId) params.orgId = orgId
  if (startDate) params.startDate = startDate
  if (endDate) params.endDate = endDate
  return get('/crm/referrals/statistics', Object.keys(params).length > 0 ? params : undefined)
}

// ==================== 返佣 API ====================

export function getCommissionList(params: CommissionQueryRequest) {
  return get('/crm/commissions', params)
}

export function getCommissionById(id: number) {
  return get(`/crm/commissions/${id}`)
}

export function createCommission(data: CreateCommissionRequest) {
  return post('/crm/commissions', data)
}

export function updateCommissionStatus(id: number, status: number) {
  return put(`/crm/commissions/${id}/status`, { status })
}

// ==================== 外部联系人 API ====================

export function getExternalContactList(params: ExternalContactQueryRequest) {
  return get('/crm/external-contacts', params)
}

export function getExternalContactById(id: number) {
  return get(`/crm/external-contacts/${id}`)
}

export function createExternalContact(data: CreateExternalContactRequest) {
  return post('/crm/external-contacts', data)
}

export function updateExternalContact(id: number, data: UpdateExternalContactRequest) {
  return put(`/crm/external-contacts/${id}`, data)
}

export function deleteExternalContact(id: number) {
  return del(`/crm/external-contacts/${id}`)
}

// ==================== 号段池 API ====================

export function getWaybillPoolList(params: WaybillPoolQueryRequest) {
  return get('/crm/waybill-pools', params)
}

export function getWaybillPoolById(id: number) {
  return get(`/crm/waybill-pools/${id}`)
}

export function createWaybillPool(data: CreateWaybillPoolRequest) {
  return post('/crm/waybill-pools', data)
}

export function deleteWaybillPool(id: number) {
  return del(`/crm/waybill-pools/${id}`)
}

export function getWaybillAllocations(poolId: number) {
  return get(`/crm/waybill-pools/${poolId}/allocations`)
}

export function allocateWaybill(data: AllocateWaybillRequest) {
  return post('/crm/waybill-pools/allocate', data)
}

export function recycleWaybill(allocationId: number) {
  return post(`/crm/waybill-pools/allocations/${allocationId}/recycle`)
}

// ==================== 预付款 API ====================

export function getPrepaymentList(params: PrepaymentQueryRequest) {
  return get('/crm/prepayments', params)
}

export function getPrepaymentById(id: number) {
  return get(`/crm/prepayments/${id}`)
}

export function createPrepayment(data: CreatePrepaymentRequest) {
  return post('/crm/prepayments', data)
}

export function confirmPrepayment(id: number, receivedAmount: number, bankTransactionId?: number) {
  return put(`/crm/prepayments/${id}/confirm`, { receivedAmount, bankTransactionId })
}

export function getCustomerAccount(customerId: number, brandCode: string) {
  return get('/crm/prepayments/account', { customerId, brandCode })
}

export function getCustomerAllocations(customerId: number) {
  return get(`/crm/prepayments/allocations/customer/${customerId}`)
}

// ==================== 毛利 API ====================

export function getProfitList(params: ProfitQueryRequest) {
  return get('/crm/profits', params)
}

export function getProfitById(id: number) {
  return get(`/crm/profits/${id}`)
}

export function createProfit(data: CreateProfitRequest) {
  return post('/crm/profits', data)
}

export function updateProfit(id: number, data: CreateProfitRequest) {
  return put(`/crm/profits/${id}`, data)
}

export function deleteProfit(id: number) {
  return del(`/crm/profits/${id}`)
}

export function getProfitSummary(orgId?: number, period?: string) {
  const params: any = {}
  if (orgId) params.orgId = orgId
  if (period) params.period = period
  return get('/crm/profits/summary', Object.keys(params).length > 0 ? params : undefined)
}

export function getProfitRanking(orgId?: number, period?: string, top: number = 20) {
  const params: any = { top }
  if (orgId) params.orgId = orgId
  if (period) params.period = period
  return get('/crm/profits/ranking', params)
}

// ==================== 奖金 API ====================

export function getBonusPlanList(params: BonusPlanQueryRequest) {
  return get('/crm/bonus/plans', params)
}

export function getBonusPlanById(id: number) {
  return get(`/crm/bonus/plans/${id}`)
}

export function createBonusPlan(data: CreateBonusPlanRequest) {
  return post('/crm/bonus/plans', data)
}

export function updateBonusPlan(id: number, data: UpdateBonusPlanRequest) {
  return put(`/crm/bonus/plans/${id}`, data)
}

export function deleteBonusPlan(id: number) {
  return del(`/crm/bonus/plans/${id}`)
}

export function updateBonusPlanStatus(id: number, status: number) {
  return put(`/crm/bonus/plans/${id}/status`, { status })
}

export function getBonusDetailList(params: BonusDetailQueryRequest) {
  return get('/crm/bonus/details', params)
}

export function addBonusDetail(planId: number, data: CreateBonusDetailRequest) {
  return post(`/crm/bonus/plans/${planId}/details`, data)
}

export function deleteBonusDetail(detailId: number) {
  return del(`/crm/bonus/details/${detailId}`)
}
