// 快递模块 API
import { get, post, put, del } from './request'

// ==================== 通用类型 ====================

export interface PagedResult<T = any> {
  items: T[]
  total: number
  pageIndex: number
  pageSize: number
}

export interface PagedRequest {
  pageIndex: number
  pageSize: number
}

// ==================== 账单 ====================

export interface InvoiceDto {
  id: number
  invoiceNo?: string
  clientId: string
  clientName?: string
  brandCode?: string
  brandName?: string
  periodStart: string
  periodEnd: string
  totalWaybills?: number
  totalWeight?: number
  avgWeight?: number
  weightCap?: number
  excessWeight?: number
  weightCapSurcharge?: number
  quotaSurcharge?: number
  totalCharge?: number
  totalChargeWithSurcharge?: number
  totalCost?: number
  totalProfit?: number
  prepayDeduction?: number
  payableAmount?: number
  reviewStatus: number
  reviewer?: string
  reviewTime?: string
  reviewRemark?: string
  status: number
  archived: boolean
  createdTime: string
  updatedTime: string
}

export interface InvoiceReviewLogDto {
  id: number
  action: number
  ruleId?: number
  ruleResult?: string
  operatorId?: number
  remark?: string
  createdTime: string
}

export interface InvoiceDetailDto extends InvoiceDto {
  reviewLogs: InvoiceReviewLogDto[]
}

export interface InvoiceQueryRequest extends PagedRequest {
  clientId?: string
  brandCode?: string
  status?: number
  reviewStatus?: number
  periodStart?: string
  periodEnd?: string
}

export interface GenerateInvoiceRequest {
  clientId: string
  brandCode: string
  periodStart: string
  periodEnd: string
}

export interface ReviewInvoiceRequest {
  approved: boolean
  remark?: string
}

export interface ReverseReviewRequest {
  remark?: string
}

export interface ReceivePaymentRequest {
  amount: number
}

// ==================== 审核规则 ====================

export interface ReviewRuleDto {
  id: number
  ruleName: string
  ruleType: number
  minValue?: number
  maxValue?: number
  threshold?: number
  clientId?: string
  brandCode?: string
  priority: number
  enabled: boolean
  createdTime: string
}

export interface CreateReviewRuleRequest {
  ruleName: string
  ruleType: number
  minValue?: number
  maxValue?: number
  threshold?: number
  clientId?: string
  brandCode?: string
  priority: number
  enabled: boolean
}

export interface UpdateReviewRuleRequest extends CreateReviewRuleRequest {}

// ==================== 预付款 ====================

export interface PrepaymentDto {
  id: number
  clientId: string
  amount: number
  paymentDate?: string
  paymentMethod?: string
  remark?: string
  createdTime: string
}

export interface PrepaymentBalanceDto {
  id: number
  clientId: string
  balance: number
  totalRecharge: number
  totalConsume: number
  updatedTime: string
}

export interface PrepaymentTransactionDto {
  id: number
  clientId: string
  transactionType: number
  amount: number
  invoiceId?: number
  balanceAfter?: number
  remark?: string
  createdTime: string
}

export interface TransactionQueryRequest extends PagedRequest {
  clientId?: string
  transactionType?: number
  startDate?: string
  endDate?: string
}

export interface RechargeRequest {
  clientId: string
  amount: number
  paymentDate?: string
  paymentMethod?: string
  remark?: string
}

// ==================== 运单编号 ====================

export interface WaybillNumberPoolDto {
  id: number
  brandCode: string
  brandName?: string
  prefix?: string
  startNo?: string
  endNo?: string
  totalCount?: number
  allocated: number
  remaining?: number
  createdTime: string
  updatedTime: string
}

export interface WaybillNumberTransactionDto {
  id: number
  clientId: string
  brandCode: string
  poolId?: number
  transactionType?: number
  quantity: number
  startNo?: string
  endNo?: string
  transactionDate?: string
  createdTime: string
}

export interface ClientWaybillBalanceDto {
  id: number
  clientId: string
  brandCode: string
  available: number
  used: number
  totalAllocated: number
  totalReturned: number
  updatedTime: string
}

export interface CreatePoolRequest {
  brandCode: string
  prefix?: string
  startNo?: string
  endNo?: string
  totalCount?: number
}

export interface AllocateRequest {
  poolId: number
  clientId: string
  brandCode: string
  quantity: number
}

export interface ReturnRequest {
  clientId: string
  brandCode: string
  quantity: number
}

// ==================== 返利政策 ====================

export interface PolicyRebateListItemDto {
  id: number
  brandCode: string
  policyName: string
  rebateMode: number
  flatRebateAmount?: number
  settlementCycle: number
  effectiveDate: string
  expiryDate?: string
  status: number
  createdTime: string
}

export interface PolicyRebateTierDto {
  id: number
  dailyVolumeFrom: number
  dailyVolumeTo?: number
  rebatePerTicket: number
  sortOrder: number
}

export interface PolicyRebateRuleItemDto {
  id: number
  thresholdLower?: number
  thresholdUpper?: number
  weightFrom?: number
  weightTo?: number
  provinceId?: number
  adjustType?: number
  adjustCalcMethod?: number
  adjustAmount?: number
  adjustRate?: number
  sortOrder: number
}

export interface PolicyRebateRuleDto {
  id: number
  ruleType: number
  ruleName: string
  enabled: boolean
  sortOrder: number
  items: PolicyRebateRuleItemDto[]
}

export interface PolicyRebateDetailDto {
  id: number
  brandCode: string
  policyName: string
  rebateMode: number
  flatRebateAmount?: number
  settlementCycle: number
  effectiveDate: string
  expiryDate?: string
  status: number
  remark?: string
  createdTime: string
  updatedTime: string
  tiers: PolicyRebateTierDto[]
  rules: PolicyRebateRuleDto[]
}

export interface CreatePolicyRebateTierRequest {
  dailyVolumeFrom: number
  dailyVolumeTo?: number
  rebatePerTicket: number
  sortOrder: number
}

export interface CreatePolicyRebateRuleItemRequest {
  thresholdLower?: number
  thresholdUpper?: number
  weightFrom?: number
  weightTo?: number
  provinceId?: number
  adjustType?: number
  adjustCalcMethod?: number
  adjustAmount?: number
  adjustRate?: number
  sortOrder: number
}

export interface CreatePolicyRebateRuleRequest {
  ruleType: number
  ruleName: string
  enabled: boolean
  sortOrder: number
  items: CreatePolicyRebateRuleItemRequest[]
}

export interface CreatePolicyRebateRequest {
  brandCode: string
  policyName: string
  rebateMode: number
  flatRebateAmount?: number
  settlementCycle: number
  effectiveDate: string
  expiryDate?: string
  remark?: string
  tiers: CreatePolicyRebateTierRequest[]
  rules: CreatePolicyRebateRuleRequest[]
}

export interface UpdatePolicyRebateRequest {
  policyName: string
  rebateMode: number
  flatRebateAmount?: number
  settlementCycle: number
  effectiveDate: string
  expiryDate?: string
  remark?: string
  tiers: CreatePolicyRebateTierRequest[]
  rules: CreatePolicyRebateRuleRequest[]
}

export interface PolicyRebateQueryRequest extends PagedRequest {
  brandCode?: string
  status?: number
  rebateMode?: number
}

// ==================== 返利结算 ====================

export interface PolicyRebateSettlementListItemDto {
  id: number
  policyRebateId: number
  brandCode: string
  periodStart: string
  periodEnd: string
  totalWaybills?: number
  totalWeight?: number
  baseRebateAmount?: number
  finalRebateAmount?: number
  status: number
  createdTime: string
}

export interface SettlementAdjustDetailDto {
  id: number
  ruleId?: number
  ruleItemId?: number
  actualValue?: number
  thresholdValue?: number
  adjustType?: number
  adjustAmount?: number
  remark?: string
}

export interface PolicyRebateSettlementDetailDto {
  id: number
  policyRebateId: number
  brandCode: string
  periodStart: string
  periodEnd: string
  totalWaybills?: number
  totalWeight?: number
  avgWeight?: number
  baseRebateAmount?: number
  totalReward?: number
  totalPenalty?: number
  finalRebateAmount?: number
  status: number
  confirmedBy?: string
  confirmedTime?: string
  remark?: string
  createdTime: string
  details: SettlementAdjustDetailDto[]
}

export interface SettlementQueryRequest extends PagedRequest {
  policyRebateId?: number
  brandCode?: string
  status?: number
  periodStartFrom?: string
  periodStartTo?: string
}

export interface ExecuteSettlementRequest {
  policyRebateId: number
  periodStart: string
  periodEnd: string
}

export interface SimulationRequest {
  policyRebateId: number
  useHistory: boolean
  periodStart?: string
  periodEnd?: string
  assumedDailyVolume?: number
  assumedDays?: number
  assumedAvgWeight?: number
}

export interface SimulationAdjustDetail {
  ruleName: string
  ruleType: number
  actualValue: number
  adjustType: number
  adjustAmount: number
}

export interface SimulationResult {
  totalWaybills: number
  totalWeight: number
  avgWeight: number
  baseRebateAmount: number
  totalReward: number
  totalPenalty: number
  finalRebateAmount: number
  adjustments: SimulationAdjustDetail[]
}

// ==================== 报表 ====================

export interface ReportQueryRequest {
  dateFrom?: string
  dateTo?: string
  brandCode?: string
  clientId?: string
  provinceId?: number
}

export interface FlowAnalysisDto {
  province: string
  waybillCount: number
  ratio: number
  totalWeight: number
  avgWeight: number
  totalCharge: number
  avgPrice: number
}

export interface FlowTrendDto {
  date: string
  waybillCount: number
  totalCharge: number
}

export interface WeightSegmentReportDto {
  segmentName: string
  waybillCount: number
  ratio: number
  totalWeight: number
  totalCharge: number
  avgPrice: number
  avgPricePerKg: number
}

export interface WeightTrendDto {
  date: string
  avgWeight: number
}

export interface ProfitByClientDto {
  clientId: string
  clientName: string
  clientType: number
  waybillCount: number
  totalWeight: number
  totalCharge: number
  totalCost: number
  profit: number
  /** 毛利率(%)；应收≤0 时为 null */
  profitRate: number | null
  avgPrice: number
  avgProfit: number
  /** 应收≤0 的运单数（未计价/零应收预警） */
  zeroChargeCount: number
}

export interface ProfitByShopDto {
  shopName: string
  clientName: string
  waybillCount: number
  totalWeight: number
  totalCharge: number
  totalCost: number
  profit: number
  profitRate: number | null
}

export interface ProfitTrendDto {
  date: string
  totalCharge: number
  totalCost: number
  profit: number
  profitRate: number | null
}

export interface ProfitByIntermediaryDto {
  clientId: string
  clientName: string
  clientType: number
  chainLevel: number
  waybillCount: number
  totalWeight: number
  downstreamRevenue: number
  upstreamCost: number
  profit: number
  profitRate: number | null
  avgProfit: number
}

export interface ProfitBySalesmanDto {
  salesmanId: string
  salesmanName: string
  networkPointId?: number
  waybillCount: number
  totalWeight: number
  commissionIncome: number
  profit: number
  avgCommission: number
}

export interface ProfitByWeightSegmentDto {
  weightSegment: string
  segmentOrder: number
  waybillCount: number
  totalWeight: number
  totalCharge: number
  totalCost: number
  profit: number
  profitRate: number | null
  avgCharge: number
  avgCost: number
  avgProfit: number
}

export interface ProfitByRegionDto {
  region: string
  regionOrder: number
  waybillCount: number
  totalWeight: number
  totalCharge: number
  totalCost: number
  profit: number
  profitRate: number | null
  avgWeight: number
  avgProfit: number
}

export interface ProfitByProvinceDto {
  provinceId: number
  provinceName: string
  region: string
  waybillCount: number
  totalWeight: number
  totalCharge: number
  totalCost: number
  profit: number
  profitRate: number | null
  avgProfit: number
}

// ==================== 看板 ====================

export interface DailyTrendItem {
  date: string
  waybillCount: number
  revenue: number
  cost: number
  profit: number
}

export interface BrandDistributionItem {
  brandCode: string
  brandName: string
  waybillCount: number
  ratio: number
}

export interface TopClientItem {
  clientId: string
  clientName: string
  waybillCount: number
  totalCharge: number
}

export interface AlertItem {
  type: string
  message: string
  count: number
}

export interface DashboardDto {
  todayWaybills: number
  monthWaybills: number
  monthRevenue: number
  monthCost: number
  monthProfit: number
  monthWaybillsChange?: number
  monthRevenueChange?: number
  monthCostChange?: number
  monthProfitChange?: number
  dailyTrend: DailyTrendItem[]
  brandDistribution: BrandDistributionItem[]
  topClients: TopClientItem[]
  alerts: AlertItem[]
}

// ==================== 归档 ====================

export interface ArchiveStatsDto {
  pendingCount: number
  archivedCount: number
}

export interface ArchiveResultDto {
  waybillCount: number
  billingResultCount: number
  costBreakdownCount: number
  elapsedMs: number
}

// ==================== 账单管理 API ====================

export function getInvoiceList(params: InvoiceQueryRequest): Promise<PagedResult<InvoiceDto>> {
  return get('/express/invoice', params)
}

export function getInvoiceDetail(id: number): Promise<InvoiceDetailDto> {
  return get(`/express/invoice/${id}`)
}

export function generateInvoice(data: GenerateInvoiceRequest): Promise<InvoiceDto> {
  return post('/express/invoice/generate', data)
}

export function confirmInvoice(id: number): Promise<InvoiceDto> {
  return put(`/express/invoice/${id}/confirm`)
}

export function sendInvoice(id: number): Promise<InvoiceDto> {
  return put(`/express/invoice/${id}/send`)
}

export function receivePayment(id: number, data: ReceivePaymentRequest): Promise<InvoiceDto> {
  return put(`/express/invoice/${id}/payment`, data)
}

export function reviewInvoice(id: number, data: ReviewInvoiceRequest): Promise<any> {
  return post(`/express/invoice/${id}/review`, data)
}

export function reverseReview(id: number, data?: ReverseReviewRequest): Promise<any> {
  return post(`/express/invoice/${id}/reverse-review`, data || {})
}

export function triggerAutoGenerate(): Promise<any> {
  return post('/express/invoice/auto-generate')
}

// ==================== 审核规则 API ====================

export function getReviewRules(): Promise<ReviewRuleDto[]> {
  return get('/express/invoice/review-rules')
}

export function createReviewRule(data: CreateReviewRuleRequest): Promise<ReviewRuleDto> {
  return post('/express/invoice/review-rules', data)
}

export function updateReviewRule(id: number, data: UpdateReviewRuleRequest): Promise<ReviewRuleDto> {
  return put(`/express/invoice/review-rules/${id}`, data)
}

export function deleteReviewRule(id: number): Promise<any> {
  return del(`/express/invoice/review-rules/${id}`)
}

// ==================== 预付款 API ====================

export function getPrepaymentBalance(clientId: string): Promise<PrepaymentBalanceDto> {
  return get(`/express/prepayment/balance/${clientId}`)
}

export function getPrepaymentTransactions(params: TransactionQueryRequest): Promise<PagedResult<PrepaymentTransactionDto>> {
  return get('/express/prepayment/transaction', params)
}

export function recharge(data: RechargeRequest): Promise<PrepaymentDto> {
  return post('/express/prepayment/recharge', data)
}

// ==================== 运单编号管理 API ====================

export function getWaybillNumberPools(): Promise<WaybillNumberPoolDto[]> {
  return get('/express/waybill-number/pool')
}

export function createWaybillNumberPool(data: CreatePoolRequest): Promise<WaybillNumberPoolDto> {
  return post('/express/waybill-number/pool', data)
}

export function allocateWaybillNumber(data: AllocateRequest): Promise<WaybillNumberTransactionDto> {
  return post('/express/waybill-number/allocate', data)
}

export function returnWaybillNumber(data: ReturnRequest): Promise<WaybillNumberTransactionDto> {
  return post('/express/waybill-number/return', data)
}

export function getClientWaybillBalance(clientId: string, brandCode: string): Promise<ClientWaybillBalanceDto> {
  return get(`/express/waybill-number/balance/${clientId}`, { brandCode })
}

// ==================== 返利政策 API ====================

export function getPolicyRebateList(params: PolicyRebateQueryRequest): Promise<PagedResult<PolicyRebateListItemDto>> {
  return get('/express/policy-rebate', params)
}

export function getPolicyRebateDetail(id: number): Promise<PolicyRebateDetailDto> {
  return get(`/express/policy-rebate/${id}`)
}

export function createPolicyRebate(data: CreatePolicyRebateRequest): Promise<PolicyRebateDetailDto> {
  return post('/express/policy-rebate', data)
}

export function updatePolicyRebate(id: number, data: UpdatePolicyRebateRequest): Promise<PolicyRebateDetailDto> {
  return put(`/express/policy-rebate/${id}`, data)
}

export function deletePolicyRebate(id: number): Promise<any> {
  return del(`/express/policy-rebate/${id}`)
}

export function enablePolicyRebate(id: number): Promise<any> {
  return put(`/express/policy-rebate/${id}/enable`)
}

export function disablePolicyRebate(id: number): Promise<any> {
  return put(`/express/policy-rebate/${id}/disable`)
}

// ==================== 返利结算 API ====================

export function getSettlementList(params: SettlementQueryRequest): Promise<PagedResult<PolicyRebateSettlementListItemDto>> {
  return get('/express/policy-rebate-settlement', params)
}

export function getSettlementDetail(id: number): Promise<PolicyRebateSettlementDetailDto> {
  return get(`/express/policy-rebate-settlement/${id}`)
}

export function executeSettlement(data: ExecuteSettlementRequest): Promise<PolicyRebateSettlementDetailDto> {
  return post('/express/policy-rebate-settlement/execute', data)
}

export function confirmSettlement(id: number): Promise<any> {
  return put(`/express/policy-rebate-settlement/${id}/confirm`)
}

export function writeOffSettlement(id: number): Promise<any> {
  return put(`/express/policy-rebate-settlement/${id}/write-off`)
}

export function simulateRebate(data: SimulationRequest): Promise<SimulationResult> {
  return post('/express/policy-rebate-settlement/simulate', data)
}

// ==================== 报表 API ====================

export function getFlowDistribution(params: ReportQueryRequest): Promise<FlowAnalysisDto[]> {
  return get('/express/report/flow-distribution', params)
}

export function getFlowTrend(params: ReportQueryRequest & { granularity?: string }): Promise<FlowTrendDto[]> {
  return get('/express/report/flow-trend', params)
}

export function getWeightDistribution(params: ReportQueryRequest): Promise<WeightSegmentReportDto[]> {
  return get('/express/report/weight-distribution', params)
}

export function getWeightTrend(params: ReportQueryRequest & { granularity?: string }): Promise<WeightTrendDto[]> {
  return get('/express/report/weight-trend', params)
}

export function getProfitByClient(params: ReportQueryRequest): Promise<ProfitByClientDto[]> {
  return get('/express/report/profit-by-client', params)
}

export function getProfitByShop(params: ReportQueryRequest): Promise<ProfitByShopDto[]> {
  return get('/express/report/profit-by-shop', params)
}

export function getProfitTrend(params: ReportQueryRequest & { granularity?: string }): Promise<ProfitTrendDto[]> {
  return get('/express/report/profit-trend', params)
}

export function getProfitByIntermediary(params?: ReportQueryRequest): Promise<ProfitByIntermediaryDto[]> {
  return get('/express/report/profit-by-intermediary', params)
}

export function getProfitBySalesman(params?: ReportQueryRequest): Promise<ProfitBySalesmanDto[]> {
  return get('/express/report/profit-by-salesman', params)
}

// 按重量段分析
export function getProfitByWeightSegment(params?: ReportQueryRequest): Promise<ProfitByWeightSegmentDto[]> {
  return get('/express/report/profit-by-weight-segment', params)
}

// 按大区分析
export function getProfitByRegion(params?: ReportQueryRequest): Promise<ProfitByRegionDto[]> {
  return get('/express/report/profit-by-region', params)
}

// 按省份分析（可选大区过滤）
export function getProfitByProvince(params?: ReportQueryRequest & { region?: string }): Promise<ProfitByProvinceDto[]> {
  return get('/express/report/profit-by-province', params)
}

export function getDashboard(brandCode?: string): Promise<DashboardDto> {
  return get('/express/report/dashboard', brandCode ? { brandCode } : undefined)
}

// 毛利报表筛选下拉选项
export interface ProfitFilterOptionsDto {
  brands: { value: string; label: string }[]
  clients: { value: string; label: string }[]
}

export function getProfitFilterOptions(): Promise<ProfitFilterOptionsDto> {
  return get('/express/report/filter-options')
}

// ==================== 归档 API ====================

export function executeArchive(): Promise<ArchiveResultDto> {
  return post('/express/archive/execute')
}

export function getArchiveStats(): Promise<ArchiveStatsDto> {
  return get('/express/archive/stats')
}



// ==================== 报价方案 ====================

// ==================== 报价（Quotation） ====================

export interface QuotationDto {
  id: number
  planCode: string           // F方案编号
  planName: string
  clientType: string         // KH/DL/WD/YW/CB/YZ
  clientId: string           // 自然编号
  networkPointCode?: string
  brandCode?: string
  sharedShopEnabled: boolean // 共享店铺开关
  status: number
  effectiveDate?: string
  settlementWeightStage?: number
  weightRoundingMethod?: number
  taxRate?: number
  isTaxInclusive?: boolean
  oaProcessId?: number
  approvedBy?: string
  approvedAt?: string
  // 商务条款（后端：付款方式 1预付 2后付 3混合；账单周期 1周 2月 3季 4年；费率均为小数，如 0.003 = 0.3%）
  paymentMode?: number
  prepayRatio?: number
  billingCycle?: number
  billingDay?: number
  paymentDueDay?: number
  throwRatio?: number
  insuranceRate?: number
  remark?: string
  segments?: WeightSegmentDto[]
  cells?: PriceCellDto[]
  createdTime?: string
  updatedTime?: string
}

export interface QuotationListItemDto {
  id: number
  planCode: string
  planName: string
  clientType: string
  clientId: string
  networkPointCode?: string
  sharedShopEnabled: boolean
  status: number
  effectiveDate?: string
  createdTime?: string
  updatedTime?: string
  shopCount: number
}

export interface QuotationQueryRequest extends PagedRequest {
  status?: number
  clientType?: string
  clientId?: string
  keyword?: string
}

export interface CreateQuotationRequest {
  planName: string
  clientType?: string
  clientId?: string
  planCode?: string
  networkPointCode?: string
  brandCode?: string
  sharedShopEnabled?: boolean
  settlementWeightStage?: number
  weightRoundingMethod?: number
  taxRate?: number
  isTaxInclusive?: boolean
  effectiveDate?: string
  // 商务条款（付款方式 1预付 2后付 3混合；账单周期 1周 2月 3季 4年；费率为小数）
  paymentMode?: number
  prepayRatio?: number
  billingCycle?: number
  billingDay?: number
  paymentDueDay?: number
  throwRatio?: number
  insuranceRate?: number
  remark?: string
  segments?: WeightSegmentInput[]
  allowIncomplete?: boolean
}

export interface UpdateQuotationRequest {
  planName?: string
  clientType?: string
  clientId?: string
  planCode?: string
  networkPointCode?: string
  brandCode?: string
  sharedShopEnabled?: boolean
  settlementWeightStage?: number
  weightRoundingMethod?: number
  taxRate?: number
  isTaxInclusive?: boolean
  effectiveDate?: string
  // 商务条款（同 CreateQuotationRequest）
  paymentMode?: number
  prepayRatio?: number
  billingCycle?: number
  billingDay?: number
  paymentDueDay?: number
  throwRatio?: number
  insuranceRate?: number
  remark?: string
  segments?: WeightSegmentInput[]
  allowIncomplete?: boolean
}

export interface QuotationShopDto {
  id: number
  quotationId: number
  shopName: string
  remark?: string
  createdTime: string
}

export interface QuotationAliasDto {
  id: number
  quotationId: number
  alias: string
  brandCode?: string
  isActive: boolean
  remark?: string
  createdTime: string
}

export interface WeightSegmentDto {
  id: number
  segmentIndex: number
  weightFrom: number
  weightTo?: number
  roundingMethod: number
  truncParam?: number | null
  ceilParam?: number | null
}

export interface PriceCellDto {
  id: number
  segmentId: number
  provinceId: number
  basePrice: number
  continuePrice: number
  firstWeight: number
  continueStep: number
  roundingMethodOverride?: number | null
  truncParamOverride?: number | null
  ceilParamOverride?: number | null
}

export interface WeightSegmentInput {
  segmentIndex: number
  weightFrom: number
  weightTo?: number
  roundingMethod: number
  truncParam?: number | null
  ceilParam?: number | null
  cells: PriceCellInput[]
}

export interface PriceCellInput {
  provinceId: number
  basePrice: number
  continuePrice: number
  firstWeight: number
  continueStep: number
  roundingMethodOverride?: number | null
  truncParamOverride?: number | null
  ceilParamOverride?: number | null
}

export interface ProvinceDto {
  id: number
  code: string
  name: string
  shortName: string
  region: string
  isRemote: boolean
}

// ==================== 报价（Quotation） API ====================

export function getQuotationList(params: QuotationQueryRequest): Promise<PagedResult<QuotationListItemDto>> {
  return get('/express/quotations', params)
}

export function getQuotationDetail(id: number): Promise<QuotationDto> {
  return get(`/express/quotations/${id}`)
}

export function createQuotation(data: CreateQuotationRequest): Promise<QuotationDto> {
  return post('/express/quotations', data)
}

export function updateQuotation(id: number, data: UpdateQuotationRequest): Promise<QuotationDto> {
  return put(`/express/quotations/${id}`, data)
}

export function deleteQuotation(id: number): Promise<any> {
  return del(`/express/quotations/${id}`)
}

export function copyQuotation(id: number): Promise<QuotationDto> {
  return post(`/express/quotations/${id}/copy`)
}

export function getQuotationShops(quotationId: number): Promise<QuotationShopDto[]> {
  return get(`/express/quotations/${quotationId}/shops`)
}

export function addQuotationShops(quotationId: number, data: { shopNames: string[]; remark?: string }): Promise<any> {
  return post(`/express/quotations/${quotationId}/shops`, data)
}

export function removeQuotationShop(quotationId: number, shopId: number): Promise<any> {
  return del(`/express/quotations/${quotationId}/shops/${shopId}`)
}

export interface ShopConflictItem {
  shopName: string
  clientType: string
  clientTypeName: string
  clientId: string
  brandCode: string
  quotationName: string
  effectiveDate: string | null
}

export function checkQuotationShopConflicts(quotationId: number, shopNames: string[]) {
  return post<ShopConflictItem[]>(`/express/quotations/${quotationId}/shops/check-conflicts`, { shopNames })
}

export function getQuotationAliases(quotationId: number): Promise<QuotationAliasDto[]> {
  return get(`/express/quotations/${quotationId}/aliases`)
}

export function addQuotationAlias(quotationId: number, data: { alias: string; brandCode?: string; remark?: string }): Promise<QuotationAliasDto> {
  return post(`/express/quotations/${quotationId}/aliases`, data)
}

export function removeQuotationAlias(aliasId: number): Promise<any> {
  return del(`/express/quotations/aliases/${aliasId}`)
}

export function getProvinceList(): Promise<ProvinceDto[]> {
  return get('/express/provinces')
}

export function getProvinceDetail(id: number): Promise<ProvinceDto> {
  return get(`/express/provinces/${id}`)
}

export interface CreateProvinceRequest {
  code: string
  name: string
  shortName: string
  region?: string
  isRemote: boolean
}

export type UpdateProvinceRequest = CreateProvinceRequest

export function createProvince(data: CreateProvinceRequest): Promise<ProvinceDto> {
  return post('/express/provinces', data)
}

export function updateProvince(id: number, data: UpdateProvinceRequest): Promise<ProvinceDto> {
  return put(`/express/provinces/${id}`, data)
}

export function deleteProvince(id: number): Promise<any> {
  return del(`/express/provinces/${id}`)
}

export function getRegionList(): Promise<string[]> {
  return get('/express/provinces/regions')
}

export function renameRegion(oldName: string, newName: string): Promise<number> {
  return put('/express/provinces/regions/rename', { oldName, newName })
}

export function downloadQuotationTemplate(): Promise<any> {
  return get('/express/quotations/template', {}, { responseType: 'blob' })
}

export function importQuotation(formData: FormData): Promise<QuotationDto> {
  return post('/express/quotations/import', formData as any)
}

// 获取品牌列表（用于下拉）
export function getExpBrandOptions(): Promise<PagedResult<{ code: string; name: string }>> {
  return get('/express/brands', { pageIndex: 1, pageSize: 999 })
}

// ==================== 计费异常 API ====================

export function getBillingErrors(brandCode?: string) {
  return get('/express/billing/errors', brandCode ? { brandCode } : undefined)
}

export function getBillingErrorDetail(params: { errorCode: string; page: number; pageSize: number }) {
  return get('/express/billing/errors/detail', params)
}

export function retryBilling(data: { errorCode?: string; shopNames?: string[]; waybillIds?: number[] }) {
  return post('/express/billing/retry', data)
}

// ==================== 对账 ====================

export interface ReconciliationLineDto {
  waybillId: number
  waybillNo: string
  waybillDate: string
  provinceName: string
  billableWeight: number
  freightCharge: number
  surchargeAmount: number
  chargeAmount: number
}

export interface ReconciliationDetailDto {
  invoiceId: number
  invoiceNo: string
  clientName: string
  periodStart: string
  periodEnd: string
  reconciliationStatus: number  // 0:未对账 1:已对账 2:有异议 3:异议已解决
  remarks?: string
  disputeReason?: string
  disputeResolution?: string
  totalCharge: number
  totalWaybills: number
  lines: ReconciliationLineDto[]
}

export function getReconciliationDetail(invoiceId: number) {
  return get(`/express/invoice/${invoiceId}/reconciliation`)
}

export function confirmReconciliation(invoiceId: number, data?: { remarks?: string }) {
  return post(`/express/invoice/${invoiceId}/reconciliation/confirm`, data)
}

export function raiseDispute(invoiceId: number, data: { reason: string }) {
  return post(`/express/invoice/${invoiceId}/reconciliation/dispute`, data)
}

export function resolveDispute(invoiceId: number, data: { resolution: string }) {
  return post(`/express/invoice/${invoiceId}/reconciliation/dispute/resolve`, data)
}

export function exportReconciliation(invoiceId: number) {
  return get(`/express/invoice/${invoiceId}/reconciliation/export`, {}, { responseType: 'blob' })
}

// ==================== 业务代理 API ====================

export interface AgentDto {
  id: number
  code: string
  name: string
  agentLevel: number
  agentRegion?: string
  contactName?: string
  contactPhone?: string
  address?: string
  cooperationStartDate?: string
  status: number
  remark?: string
  createdTime: string
  updatedTime: string
}

export interface CreateAgentRequest {
  code: string
  name: string
  agentLevel: number
  agentRegion?: string
  contactName?: string
  contactPhone?: string
  address?: string
  cooperationStartDate?: string
  status?: number
  remark?: string
}

export type UpdateAgentRequest = CreateAgentRequest

export interface AgentQueryRequest extends PagedRequest {
  status?: number
  agentLevel?: number
}

export function getAgentList(params: AgentQueryRequest): Promise<PagedResult<AgentDto>> {
  return get('/express/agents', params)
}

export function getAgentDetail(id: number): Promise<AgentDto> {
  return get(`/express/agents/${id}`)
}

export function createAgent(data: CreateAgentRequest): Promise<AgentDto> {
  return post('/express/agents', data)
}

export function updateAgent(id: number, data: UpdateAgentRequest): Promise<AgentDto> {
  return put(`/express/agents/${id}`, data)
}

export function deleteAgent(id: number): Promise<any> {
  return del(`/express/agents/${id}`)
}

// ==================== 快递网点 API ====================

export interface NetworkPointDto {
  id: number
  code?: string
  shortName?: string
  parentPointCode?: string
  orgId: number
  pointLevel: number
  isPrimaryPoint?: number
  coverageArea?: string
  dailyCapacity?: number
  storageArea?: number
  businessHours?: string
  address?: string
  manager?: string
  contactPhone?: string
  status: number
  remark?: string
  createdTime: string
  updatedTime: string
}

export interface CreateNetworkPointRequest {
  code: string
  shortName?: string
  parentPointCode?: string
  orgId: number
  pointLevel: number
  isPrimaryPoint?: number
  coverageArea?: string
  dailyCapacity?: number
  storageArea?: number
  businessHours?: string
  address?: string
  manager?: string
  contactPhone?: string
  status?: number
  remark?: string
}

export interface UpdateNetworkPointRequest {
  orgId: number
  shortName?: string
  parentPointCode?: string
  pointLevel: number
  isPrimaryPoint?: number
  coverageArea?: string
  dailyCapacity?: number
  storageArea?: number
  businessHours?: string
  address?: string
  manager?: string
  contactPhone?: string
  status?: number
  remark?: string
}

export interface NetworkPointQueryRequest extends PagedRequest {
  keyword?: string
  status?: number
}

export function getNetworkPointList(params: NetworkPointQueryRequest): Promise<PagedResult<NetworkPointDto>> {
  return get('/express/network-points', params)
}

export function getNetworkPointDetail(id: number): Promise<NetworkPointDto> {
  return get(`/express/network-points/${id}`)
}

export function createNetworkPoint(data: CreateNetworkPointRequest): Promise<NetworkPointDto> {
  return post('/express/network-points', data)
}

export function updateNetworkPoint(id: number, data: UpdateNetworkPointRequest): Promise<NetworkPointDto> {
  return put(`/express/network-points/${id}`, data)
}

export function deleteNetworkPoint(id: number): Promise<any> {
  return del(`/express/network-points/${id}`)
}

export function checkNetworkPointCodeExists(code: string): Promise<boolean> {
  return get('/express/network-points/check-code', { code })
}

// ==================== 承包区 API ====================

export interface FranchiseAreaDto {
  id: number
  code?: string
  orgId: number
  contractor?: string
  contractStartDate?: string
  contractEndDate?: string
  coverageDistrict?: string
  contractFee?: number
  contactPhone?: string
  address?: string
  status: number
  remark?: string
  createdTime: string
  updatedTime: string
}

export interface CreateFranchiseAreaRequest {
  code: string
  orgId: number
  contractor?: string
  contractStartDate?: string
  contractEndDate?: string
  coverageDistrict?: string
  contractFee?: number
  contactPhone?: string
  address?: string
  status?: number
  remark?: string
}

export interface UpdateFranchiseAreaRequest {
  orgId: number
  contractor?: string
  contractStartDate?: string
  contractEndDate?: string
  coverageDistrict?: string
  contractFee?: number
  contactPhone?: string
  address?: string
  status?: number
  remark?: string
}

export interface FranchiseAreaQueryRequest extends PagedRequest {
  status?: number
  orgId?: number
}

export function getFranchiseAreaList(params: FranchiseAreaQueryRequest): Promise<PagedResult<FranchiseAreaDto>> {
  return get('/express/franchise-areas', params)
}

export function getFranchiseAreaDetail(id: number): Promise<FranchiseAreaDto> {
  return get(`/express/franchise-areas/${id}`)
}

export function createFranchiseArea(data: CreateFranchiseAreaRequest): Promise<FranchiseAreaDto> {
  return post('/express/franchise-areas', data)
}

export function updateFranchiseArea(id: number, data: UpdateFranchiseAreaRequest): Promise<FranchiseAreaDto> {
  return put(`/express/franchise-areas/${id}`, data)
}

export function deleteFranchiseArea(id: number): Promise<any> {
  return del(`/express/franchise-areas/${id}`)
}

export function checkFranchiseAreaCodeExists(code: string): Promise<boolean> {
  return get('/express/franchise-areas/check-code', { code })
}

// ==================== 末端驿站 API ====================

export interface LastMileStationDto {
  id: string
  stationType: number
  orgId?: number
  code?: string
  name?: string
  address?: string
  businessHours?: string
  dailyVolume?: number
  shelfCount?: number
  area?: number
  contactName?: string
  contactPhone?: string
  cooperationStartDate?: string
  status: number
  remark?: string
  createdTime: string
  updatedTime: string
}

export interface CreateLastMileStationRequest {
  stationType: number
  orgId?: number
  code?: string
  name?: string
  address?: string
  businessHours?: string
  dailyVolume?: number
  shelfCount?: number
  area?: number
  contactName?: string
  contactPhone?: string
  cooperationStartDate?: string
  status?: number
  remark?: string
}

export type UpdateLastMileStationRequest = CreateLastMileStationRequest

export interface LastMileStationQueryRequest extends PagedRequest {
  status?: number
  stationType?: number
}

export function getLastMileStationList(params: LastMileStationQueryRequest): Promise<PagedResult<LastMileStationDto>> {
  return get('/express/last-mile-stations', params)
}

export function getLastMileStationDetail(id: string): Promise<LastMileStationDto> {
  return get(`/express/last-mile-stations/${id}`)
}

export function createLastMileStation(data: CreateLastMileStationRequest): Promise<LastMileStationDto> {
  return post('/express/last-mile-stations', data)
}

export function updateLastMileStation(id: string, data: UpdateLastMileStationRequest): Promise<LastMileStationDto> {
  return put(`/express/last-mile-stations/${id}`, data)
}

export function deleteLastMileStation(id: string): Promise<any> {
  return del(`/express/last-mile-stations/${id}`)
}

// ==================== 店铺列表 API ====================

export interface ShopListItemDto {
  name: string
  platform?: string
  isShared: boolean
  isAutoCreated: boolean
}

export function getExpShopList(params?: { pageIndex?: number; pageSize?: number; status?: number; keyword?: string }) {
  return get<PagedResult<ShopListItemDto>>('/express/shops', params)
}

// ==================== 店铺客户归属 API ====================

export interface ShopAssignmentListItemDto {
  id: number
  shopName: string
  clientId: string
  pricePlanId: number
  pricePlanName: string
  pricePlanStatus: number
  effectiveDate: string
  expiryDate?: string
  remark?: string
  createdTime: string
}

export function getShopAssignmentList(params: { clientId: string; shopName?: string; pageIndex?: number; pageSize?: number }) {
  return get<PagedResult<ShopAssignmentListItemDto>>('/express/shop-assignments', params)
}

export function createShopAssignment(data: any) {
  return post('/express/shop-assignments', data)
}

export function updateShopAssignment(id: number, data: any) {
  return put(`/express/shop-assignments/${id}`, data)
}

export function deleteShopAssignment(id: number) {
  return del(`/express/shop-assignments/${id}`)
}

// --- 报价批次 ---

export interface ShopAssignmentBatchDto {
  pricePlanId: number
  pricePlanName: string
  pricePlanStatus: number
  effectiveDate: string
  expiryDate?: string
  shopCount: number
  assignmentIds: number[]
}

export interface BatchShopItemDto {
  assignmentId: number
  shopName: string
  remark?: string
  createdTime: string
}

export function getShopAssignmentBatches(params: { clientId: string }) {
  return get<ShopAssignmentBatchDto[]>('/express/shop-assignments/batches', params)
}

export function getShopAssignmentBatchShops(params: {
  clientId: string
  pricePlanId: number
  effectiveDate: string
}) {
  return get<BatchShopItemDto[]>('/express/shop-assignments/batch-shops', params)
}

export function createBatchAssignment(data: {
  clientId: string
  pricePlanId?: number
  newPricePlan?: { planName: string; brandCode: string; settlementWeightStage: number }
  effectiveDate: string
  expiryDate?: string
  shopNames: string[]
  remark?: string
}) {
  return post('/express/shop-assignments/batch', data)
}

// ==================== 佣金配置 API ====================

export interface QuotationCommissionDto {
  fId: number
  fQuotationId: number
  fEnabled: boolean
  fCalcMethod: string  // 'percent' | 'fixed' | 'weight'
  fRate: number        // 小数费率（0.05 = 5%），计费引擎按 chargeAmount × fRate 直乘
  fFixedAmount: number
  fWeightAmount: number
  fTargetClientType: string
  // 业务对象编号字符串（如 KH00000001/DL-0001），后端 FTargetClientId 为 string?，发 number 会 400
  fTargetClientId?: string
}

export function getQuotationCommissions(quotationId: number) {
  return get<QuotationCommissionDto[]>(`/express/quotations/${quotationId}/commissions`)
}

export function saveQuotationCommission(quotationId: number, data: Partial<QuotationCommissionDto>) {
  return post(`/express/quotations/${quotationId}/commissions`, data)
}

export function deleteQuotationCommission(commissionId: number) {
  return del(`/express/quotations/commissions/${commissionId}`)
}

// ==================== 变更日志 API ====================

export interface QuotationChangeLogDto {
  fId: number
  fQuotationId: number
  fFieldName: string
  fOldValue: string
  fNewValue: string
  fChangedBy: string
  fChangedTime: string
}

export function getQuotationChangeLogs(quotationId: number) {
  return get<QuotationChangeLogDto[]>(`/express/quotations/${quotationId}/change-logs`)
}

// ==================== 业务对象下拉列表 API ====================

export function getExpAgentList(params?: { keyword?: string; pageIndex?: number; pageSize?: number }) {
  return get<PagedResult<AgentDto>>('/express/agents', { ...params, pageIndex: params?.pageIndex ?? 1, pageSize: params?.pageSize ?? 50 })
}

export function getExpNetworkPointOptions(params?: { keyword?: string; pageIndex?: number; pageSize?: number }) {
  return get<PagedResult<NetworkPointDto>>('/express/network-points', { ...params, pageIndex: params?.pageIndex ?? 1, pageSize: params?.pageSize ?? 50 })
}

export function getExpFranchiseAreaOptions(params?: { keyword?: string; pageIndex?: number; pageSize?: number }) {
  return get<PagedResult<FranchiseAreaDto>>('/express/franchise-areas', { ...params, pageIndex: params?.pageIndex ?? 1, pageSize: params?.pageSize ?? 50 })
}

export function getExpLastMileStationOptions(params?: { keyword?: string; pageIndex?: number; pageSize?: number }) {
  return get<PagedResult<LastMileStationDto>>('/express/last-mile-stations', { ...params, pageIndex: params?.pageIndex ?? 1, pageSize: params?.pageSize ?? 50 })
}

// 业务员下拉列表
export interface SalesmanOptionDto {
  employeeNo: string
  name: string
  networkPointCode?: string
  status?: number
}

export function getSalesmenList(params?: { keyword?: string; networkPointCode?: string; pageIndex?: number; pageSize?: number }) {
  return get<PagedResult<SalesmanOptionDto>>('/express/salesmen', { ...params, pageIndex: params?.pageIndex ?? 1, pageSize: params?.pageSize ?? 50 })
}



// ==================== 出港加收 ====================

export interface SurchargeItemDest {
  destType: number
  provinceId?: number
  cityName?: string
}

export interface SurchargeItem {
  calcMethod: number
  weightRoundingMethod?: number
  weightFrom?: number
  weightTo?: number
  weightType?: number
  dailyVolumeFrom?: number
  dailyVolumeTo?: number
  amount?: number
  sortOrder: number
  destinations: SurchargeItemDest[]
}

export interface SurchargeScopeDto {
  linkedType: string   // KH/DL/WD/YW/CB/YZ/QUOTATION
  linkedId: string
}

export interface PriceSurchargeListItem {
  id: number
  surchargeType: string
  scope: number        // 0=全局, 1=业务对象级, 2=报价级
  brandCode: string
  networkPointCode?: string
  name?: string
  effectiveDate?: string
  isActive: boolean
  scopes: SurchargeScopeDto[]
  createdTime: string
  updatedTime: string
}

export interface PriceSurchargeDetail {
  id: number
  surchargeType: string
  scope: number        // 0=全局, 1=业务对象级, 2=报价级
  brandCode: string
  networkPointCode?: string
  name?: string
  effectiveDate?: string
  isActive: boolean
  remark?: string
  scopes: SurchargeScopeDto[]
  items: SurchargeItem[]
  createdTime: string
  updatedTime: string
}

export interface CreatePriceSurchargeRequest {
  surchargeType: string
  scope: number
  brandCode: string
  networkPointCode?: string
  effectiveDate?: string
  scopes: SurchargeScopeDto[]
  items: SurchargeItem[]
}

export interface UpdatePriceSurchargeRequest {
  surchargeType: string
  scope: number
  brandCode: string
  networkPointCode?: string
  effectiveDate?: string
  scopes: SurchargeScopeDto[]
  items: SurchargeItem[]
}

export interface PriceSurchargeQuery extends PagedRequest {
  brandCode?: string
  surchargeType?: string
  scope?: number
  isActive?: boolean
}

// ==================== 出港加收 API ====================

export function getPriceSurcharges(params: PriceSurchargeQuery): Promise<PagedResult<PriceSurchargeListItem>> {
  return get('/express/price-surcharges', params)
}

export function getPriceSurchargeDetail(id: number): Promise<PriceSurchargeDetail> {
  return get(`/express/price-surcharges/${id}`)
}

export function createPriceSurcharge(data: CreatePriceSurchargeRequest): Promise<PriceSurchargeDetail> {
  return post('/express/price-surcharges', data)
}

export function updatePriceSurcharge(id: number, data: UpdatePriceSurchargeRequest): Promise<PriceSurchargeDetail> {
  return put(`/express/price-surcharges/${id}`, data)
}

export function deletePriceSurcharge(id: number): Promise<any> {
  return del(`/express/price-surcharges/${id}`)
}

export function togglePriceSurchargeActive(id: number): Promise<any> {
  return put(`/express/price-surcharges/${id}/toggle-active`)
}

// ==================== 业务对象报价汇总 ====================

export interface ClientQuotationSummaryQuery {
  keyword?: string
  type?: string
  hasQuotation?: boolean
  pageIndex?: number
  pageSize?: number
}

export interface ClientQuotationSummaryItem {
  id: string
  name: string
  code: string
  type: string
  quotationCount: number
}

export function getClientQuotationSummary(params: ClientQuotationSummaryQuery): Promise<PagedResult<ClientQuotationSummaryItem>> {
  return get('/express/client-quotation-summary', params)
}

// ==================== 成本方案 ====================

export interface CostPlanListItem {
  id: number
  brandCode: string
  brandName?: string
  planName: string
  status: number  // 0=草稿 1=启用 2=停用
  itemCount: number
  createdTime: string
}

export interface CostDetailDto {
  id?: number
  costItemId: number
  costItemName?: string
  provinceId?: number | null
  provinceName?: string
  weightFrom?: number | null
  weightTo?: number | null
  calcMethod: number  // 1按单 2按重量 3首续重 4扣除重量
  basePrice?: number | null
  firstWeight?: number | null
  continueWeight?: number | null
  continuePrice?: number | null
  weightDeduction?: number | null
}

// ==================== 成本矩阵结构化DTO ====================

export interface CostPlanMatrixDto {
  costItems: CostItemEntryDto[]
}

export interface CostItemEntryDto {
  costItemId: number
  costItemName?: string
  pricingScope: 'national' | 'province' | 'city'
  segments: CostSegmentDto[]
}

export interface CostSegmentDto {
  segmentIndex: number
  weightFrom?: number
  weightTo?: number | null
  calcMethod: number
  roundingMethod: number
  truncParam?: number | null
  ceilParam?: number | null
  cells: CostCellDto[]
}

export interface CostCellDto {
  provinceId?: number | null
  cityId?: number | null
  cityName?: string
  basePrice: number
  continuePrice: number
  firstWeight: number
  continueStep: number
  roundingMethodOverride?: number | null
  truncParamOverride?: number | null
  ceilParamOverride?: number | null
}

// ==================== 成本矩阵请求Input类型 ====================

export interface CostItemInput {
  costItemId: number
  pricingScope: 'national' | 'province' | 'city'
  segments: CostSegmentInput[]
}

export interface CostSegmentInput {
  segmentIndex: number
  weightFrom?: number
  weightTo?: number | null
  calcMethod: number
  roundingMethod: number
  truncParam?: number | null
  ceilParam?: number | null
  cells: CostCellInput[]
}

export interface CostCellInput {
  provinceId?: number | null
  cityId?: number | null
  cityName?: string
  basePrice: number
  continuePrice: number
  firstWeight: number
  continueStep: number
  roundingMethodOverride?: number | null
  truncParamOverride?: number | null
  ceilParamOverride?: number | null
}

export interface CostPlanDetail {
  id: number
  brandCode: string
  planName: string
  status: number  // 0=草稿 1=启用 2=停用
  orgId: number
  createdTime: string
  updatedTime: string
  items: CostPlanItemDto[]
  exclusions: CostPlanExclusionDto[]
}

export interface CostPlanItemDto {
  id: number
  planId: number
  itemName: string
  itemType: number  // 1=全国单价 2=省份矩阵 3=城市加收 4=一口价
  settlementWeightStage?: number
  sortOrder: number
  outletIds: number[]
  shopNames: string[]
  periodCount: number
}

export interface CostPlanItemPeriodDto {
  id: number
  itemId: number
  effectiveDate: string
  /** 失效日期（可空）。非空时超过该日期的运单不再命中本期间，避免缺月价沿用旧价。 */
  expiryDate?: string | null
  matrixJson?: string
  createdTime: string
  updatedTime: string
}

export interface CostPlanExclusionDto {
  id: number
  planId: number
  effectiveDate: string
  exclusionRuleJson?: string
  createdTime: string
  updatedTime: string
}

// CostPlanStatistics 已废弃 — 新API不再提供统计端点

export interface CostItemDto {
  id: number
  code: string
  name: string
  isRebate: boolean
  sortOrder: number
}

export interface CreateCostPlanRequest {
  brandCode: string
  planName: string
}

export interface UpdateCostPlanRequest {
  brandCode: string
  planName: string
}

export interface CreateCostItemRequest {
  code: string
  name: string
  isRebate: boolean
  sortOrder: number
}

export interface UpdateCostItemRequest {
  name: string
  isRebate: boolean
  sortOrder: number
}

export interface CostPlanQueryRequest {
  pageIndex?: number
  pageSize?: number
  brandCode?: string
  status?: number
  keyword?: string
}

// ==================== 成本方案 API（重构后） ====================

export function getCostPlanList(params: any) {
  return get('/express/cost-plans', params)
}

export function getCostPlanDetail(id: number) {
  return get(`/express/cost-plans/${id}`)
}

export function createCostPlan(data: { brandCode: string; planName: string }) {
  return post('/express/cost-plans', data)
}

export function updateCostPlan(id: number, data: { brandCode: string; planName: string }) {
  return put(`/express/cost-plans/${id}`, data)
}

export function deleteCostPlan(id: number) {
  return del(`/express/cost-plans/${id}`)
}

export function activateCostPlan(id: number) {
  return put(`/express/cost-plans/${id}/activate`)
}

export function deactivateCostPlan(id: number) {
  return put(`/express/cost-plans/${id}/deactivate`)
}

// 成本项管理
export function getCostPlanItems(planId: number) {
  return get(`/express/cost-plans/${planId}/items`)
}

export function createCostPlanItem(planId: number, data: any) {
  return post(`/express/cost-plans/${planId}/items`, data)
}

export function updateCostPlanItem(planId: number, itemId: number, data: any) {
  return put(`/express/cost-plans/${planId}/items/${itemId}`, data)
}

export function deleteCostPlanItem(planId: number, itemId: number) {
  return del(`/express/cost-plans/${planId}/items/${itemId}`)
}

// 应用网点
export function getCostPlanItemOutlets(planId: number, itemId: number) {
  return get(`/express/cost-plans/${planId}/items/${itemId}/outlets`)
}

export function setCostPlanItemOutlets(planId: number, itemId: number, outletIds: number[]) {
  return put(`/express/cost-plans/${planId}/items/${itemId}/outlets`, outletIds)
}

// 关联店铺
export function getCostPlanItemShops(planId: number, itemId: number) {
  return get(`/express/cost-plans/${planId}/items/${itemId}/shops`)
}

export function setCostPlanItemShops(planId: number, itemId: number, shopNames: string[]) {
  return put(`/express/cost-plans/${planId}/items/${itemId}/shops`, shopNames)
}

// 时间段
export function getCostPlanItemPeriods(planId: number, itemId: number): Promise<CostPlanItemPeriodDto[]> {
  return get(`/express/cost-plans/${planId}/items/${itemId}/periods`)
}

export function createCostPlanItemPeriod(planId: number, itemId: number, data: { effectiveDate: string; expiryDate?: string | null; matrixJson?: string }): Promise<CostPlanItemPeriodDto> {
  return post(`/express/cost-plans/${planId}/items/${itemId}/periods`, data)
}

export function updateCostPlanItemPeriod(planId: number, itemId: number, periodId: number, data: { effectiveDate?: string; expiryDate?: string | null; matrixJson?: string }): Promise<CostPlanItemPeriodDto> {
  return put(`/express/cost-plans/${planId}/items/${itemId}/periods/${periodId}`, data)
}

export function deleteCostPlanItemPeriod(planId: number, itemId: number, periodId: number): Promise<void> {
  return del(`/express/cost-plans/${planId}/items/${itemId}/periods/${periodId}`)
}

// 互斥配置
export function getCostPlanExclusions(planId: number) {
  return get(`/express/cost-plans/${planId}/exclusions`)
}

export function createCostPlanExclusion(planId: number, data: any) {
  return post(`/express/cost-plans/${planId}/exclusions`, data)
}

export function updateCostPlanExclusion(planId: number, exclusionId: number, data: any) {
  return put(`/express/cost-plans/${planId}/exclusions/${exclusionId}`, data)
}

export function deleteCostPlanExclusion(planId: number, exclusionId: number) {
  return del(`/express/cost-plans/${planId}/exclusions/${exclusionId}`)
}

// ==================== 成本项目 API ====================

export function getCostItems(): Promise<CostItemDto[]> {
  return get('/express/cost-items')
}

export function createCostItem(data: CreateCostItemRequest): Promise<CostItemDto> {
  return post('/express/cost-items', data)
}

export function updateCostItem(id: number, data: UpdateCostItemRequest): Promise<CostItemDto> {
  return put(`/express/cost-items/${id}`, data)
}

// ==================== 成本项矩阵 API ====================

export function getCostItemMatrix(planId: number, itemId: number, effectiveDate?: string): Promise<CostItemEntryDto> {
  return get(`/express/cost-plans/${planId}/items/${itemId}/matrix`, effectiveDate ? { effectiveDate } : undefined)
}

export function saveCostItemMatrix(planId: number, itemId: number, data: CostItemInput): Promise<void> {
  return put(`/express/cost-plans/${planId}/items/${itemId}/matrix`, data)
}

// ==================== 城市列表 ====================

export interface CityDto {
  id: number
  cityName: string
  provinceId: number
  provinceName: string
}

export function getCityList(keyword?: string): Promise<CityDto[]> {
  return get('/express/cities', { keyword })
}

// ==================== 按店铺查询报价 API ====================

export interface QuotationByShopGroupDto {
  clientType: string | null
  clientTypeName: string
  clientId: string | null
  clientName: string
  quotations: QuotationListItemDto[]
}

/** 按店铺查询报价（分组结果） */
export function getQuotationsByShop(shopName: string) {
  return get<QuotationByShopGroupDto[]>('/express/quotations/by-shop', { shopName })
}

