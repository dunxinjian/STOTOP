// 保险管理模块 API
import { get, post, put } from './request'

// ==================== 通用类型 ====================

export interface PagedResult<T = any> {
  items: T[]
  total: number
  pageIndex: number
  pageSize: number
}

// ==================== 保险公司 ====================

export interface InsCompanyDto {
  Id: number
  Uid: string
  CompanyName: string
  CompanyCode: string
  CompanyType: number // 1=财产险, 2=寿险, 3=综合
  ContactPerson?: string
  ContactPhone?: string
  Address?: string
  Remark?: string
  Status: number
  CreatorId?: number
  CreatedTime: string
  UpdatedTime: string
}

export interface InsCompanyListItemDto {
  Id: number
  CompanyName: string
  CompanyCode: string
  CompanyType: number
  ContactPerson?: string
  ContactPhone?: string
  Status: number
}

// ==================== 保单 ====================

export interface InsPolicyDto {
  Id: number
  Uid: string
  BusinessType: number // 1=三轮车, 2=员工, 3=资产...
  RelatedObjectId: number
  RelatedObjectName?: string
  InsuranceCategory: number // 1=商业保险, 2=共保基金
  InsuranceType?: string
  InsuranceCompanyId?: number
  InsuranceCompanyName?: string
  PolicyNumber?: string
  Premium?: number
  InsuredAmount?: number
  ContactPerson?: string
  ContactPhone?: string
  CoInsuranceFundId?: number
  CoInsuranceFundName?: string
  ParticipationNumber?: string
  PaymentCycle?: number // 1=月, 2=季, 3=年
  PerPeriodAmount?: number
  EffectiveDate: string
  ExpiryDate: string
  InsuranceStatus: number // 1=有效, 2=已过期, 3=已退保
  Remark?: string
  CreatorId?: number
  CreatedTime: string
  UpdatedTime: string
}

export interface InsPolicyListItemDto {
  Id: number
  BusinessType: number
  RelatedObjectName?: string
  InsuranceCategory: number
  InsuranceType?: string
  InsuranceCompanyName?: string
  PolicyNumber?: string
  Premium?: number
  EffectiveDate: string
  ExpiryDate: string
  InsuranceStatus: number
}

// ==================== 出险记录 ====================

export interface InsClaimDto {
  Id: number
  Uid: string
  PolicyId?: number
  BusinessType: number
  RelatedObjectId: number
  RelatedObjectName?: string
  ClaimNumber: string
  ClaimDate: string
  ClaimLocation?: string
  AccidentType: number // 1=碰撞, 2=自燃, 3=盗抢, 4=自然灾害, 5=其他
  AccidentDescription?: string
  CounterpartyInfo?: string
  EstimatedLoss?: number
  ActualLoss?: number
  LiabilityDivision?: number // 1=全责, 2=主责, 3=同责, 4=次责, 5=无责
  PartyId?: number
  PartyName?: string
  CaseNumber?: string
  ClaimImages?: string
  ClaimStatus: number // 1=已登记, 2=处理中, 3=已结案
  ClosedDate?: string
  ClosedRemark?: string
  Remark?: string
  CreatorId?: number
  CreatedTime: string
  UpdatedTime: string
}

export interface InsClaimListItemDto {
  Id: number
  ClaimNumber: string
  BusinessType: number
  RelatedObjectName?: string
  ClaimDate: string
  AccidentType: number
  EstimatedLoss?: number
  ActualLoss?: number
  ClaimStatus: number
}

// ==================== 理赔记录 ====================

export interface InsSettlementDto {
  Id: number
  Uid: string
  ClaimId: number
  PolicyId: number
  SettlementNumber: string
  SettlementType: number // 1=商业保险理赔, 2=共保基金理赔
  ApplyDate: string
  ApplicantId?: number
  ApplicantName?: string
  AssessedAmount?: number
  SettlementAmount?: number
  SelfPayAmount?: number
  Deductible?: number
  SettlementStatus: number // 1=草稿, 10=审批中, 20=已通过, 99=已驳回, 30=已拨付
  CurrentStepId?: number
  CurrentStepName?: string
  PaymentDate?: string
  PaymentVoucher?: string
  Remark?: string
  CreatorId?: number
  CreatedTime: string
  UpdatedTime: string
  ClaimNumber?: string
  PolicyNumber?: string
}

export interface InsSettlementListItemDto {
  Id: number
  SettlementNumber: string
  SettlementType: number
  ApplyDate: string
  ApplicantName?: string
  AssessedAmount?: number
  SettlementAmount?: number
  SettlementStatus: number
  ClaimNumber?: string
}

// ==================== 审批记录 ====================

export interface InsApprovalRecordDto {
  Id: number
  Uid: string
  SettlementId: number
  StepConfigId: number
  StepOrder: number
  StepName: string
  ApproverId: number
  ApproverName: string
  ApprovalAction: number // 1=通过, 2=驳回, 3=转办
  ApprovalComment?: string
  TransferTargetId?: number
  TransferTargetName?: string
  ApprovalTime: string
  CreatedTime: string
}

export interface InsApprovalRecordListItemDto {
  Id: number
  StepOrder: number
  StepName: string
  ApproverName: string
  ApprovalAction: number
  ApprovalComment?: string
  ApprovalTime: string
}

// ==================== 共保基金 ====================

export interface InsCoInsuranceFundDto {
  Id: number
  Uid: string
  FundName: string
  FundCode: string
  BusinessType: number
  FundDescription?: string
  TotalContributions: number
  TotalPayouts: number
  FundBalance: number
  ContributionStandard?: number
  PaymentCycle?: number
  Deductible: number
  SinglePayoutLimit?: number
  AnnualPayoutLimit?: number
  FundStatus: number // 1=正常, 2=冻结, 3=已关闭
  EffectiveDate: string
  Remark?: string
  CreatorId?: number
  CreatedTime: string
  UpdatedTime: string
}

export interface InsCoInsuranceFundListItemDto {
  Id: number
  FundName: string
  FundCode: string
  BusinessType: number
  FundBalance: number
  TotalContributions: number
  TotalPayouts: number
  FundStatus: number
  EffectiveDate: string
}

// ==================== 基金缴费 ====================

export interface InsFundContributionDto {
  Id: number
  Uid: string
  FundId: number
  FundName?: string
  PolicyId?: number
  BusinessType: number
  RelatedObjectId: number
  RelatedObjectName?: string
  ContributionNumber: string
  ContributionAmount: number
  PeriodStart: string
  PeriodEnd: string
  PaymentDate?: string
  PaymentStatus: number // 1=待缴, 2=已缴, 3=逾期
  Remark?: string
  CreatorId?: number
  CreatedTime: string
  UpdatedTime: string
}

export interface InsFundContributionListItemDto {
  Id: number
  ContributionNumber: string
  FundName?: string
  RelatedObjectName?: string
  ContributionAmount: number
  PeriodStart: string
  PeriodEnd: string
  PaymentDate?: string
  PaymentStatus: number
}

// ==================== 审批流配置 ====================

export interface InsApprovalConfigDto {
  Id: number
  Uid: string
  StepOrder: number
  StepName: string
  StepCode: string
  ApproverType: number // 1=指定人员, 2=角色, 3=部门负责人
  ApproverId?: number
  ApproverName?: string
  ApproverRoleCode?: string
  CanReject: boolean
  RejectTargetStep?: number
  Status: number
  Remark?: string
  CreatorId?: number
  CreatedTime: string
  UpdatedTime: string
}

export interface InsApprovalConfigListItemDto {
  Id: number
  StepOrder: number
  StepName: string
  StepCode: string
  ApproverType: number
  ApproverName?: string
  Status: number
}

// ==================== 报表类型 ====================

export interface PremiumSummaryDto {
  BusinessType?: number
  InsuranceCategory?: number
  TotalPremium: number
  PolicyCount: number
  AvgPremium: number
}

export interface ClaimAnalysisDto {
  AccidentType?: number
  Month?: string
  ClaimCount: number
  TotalLoss: number
  ClaimRate: number
}

export interface SettlementAnalysisDto {
  SettlementType?: number
  Month?: string
  TotalAmount: number
  AvgCycle: number
  SettlementRate: number
}

export interface FundBalanceDto {
  FundId: number
  FundName: string
  OpeningBalance: number
  Contributions: number
  Payouts: number
  ClosingBalance: number
}

export interface OverviewDto {
  ActivePolicies: number
  ExpiringPolicies: number
  PendingClaims: number
  PendingSettlements: number
  TotalFundBalance: number
}

// ==================== 保险公司 API ====================

export function getCompanyList(params: {
  pageIndex: number
  pageSize: number
  keyword?: string
  companyType?: number
  status?: number
}): Promise<PagedResult<InsCompanyListItemDto>> {
  return get('/insurance/companies', params)
}

export function createCompany(data: object): Promise<InsCompanyDto> {
  return post('/insurance/companies', data)
}

export function updateCompany(id: number, data: object): Promise<InsCompanyDto> {
  return put(`/insurance/companies/${id}`, data)
}

export function updateCompanyStatus(id: number, status: number): Promise<boolean> {
  return put(`/insurance/companies/${id}/status`, { Status: status })
}

// ==================== 保单 API ====================

export function getPolicyList(params: {
  pageIndex: number
  pageSize: number
  businessType?: number
  insuranceCategory?: number
  insuranceStatus?: number
  insuranceCompanyId?: number
  relatedObjectId?: number
}): Promise<PagedResult<InsPolicyListItemDto>> {
  return get('/insurance/policies', params)
}

export function createPolicy(data: object): Promise<InsPolicyDto> {
  return post('/insurance/policies', data)
}

export function updatePolicy(id: number, data: object): Promise<InsPolicyDto> {
  return put(`/insurance/policies/${id}`, data)
}

export function getExpiringPolicies(days?: number): Promise<InsPolicyListItemDto[]> {
  return get('/insurance/policies/expiring', { days })
}

export function getPoliciesByObject(bizType: number, objectId: number): Promise<InsPolicyListItemDto[]> {
  return get('/insurance/policies/by-object', { bizType, objectId })
}

// ==================== 出险 API ====================

export function getClaimList(params: {
  pageIndex: number
  pageSize: number
  businessType?: number
  accidentType?: number
  claimStatus?: number
  policyId?: number
}): Promise<PagedResult<InsClaimListItemDto>> {
  return get('/insurance/claims', params)
}

export function createClaim(data: object): Promise<InsClaimDto> {
  return post('/insurance/claims', data)
}

export function updateClaim(id: number, data: object): Promise<InsClaimDto> {
  return put(`/insurance/claims/${id}`, data)
}

export function closeClaim(id: number, data: { ClosedRemark?: string }): Promise<boolean> {
  return put(`/insurance/claims/${id}/close`, data)
}

// ==================== 理赔 API ====================

export function getSettlementList(params: {
  pageIndex: number
  pageSize: number
  claimId?: number
  policyId?: number
  settlementType?: number
  settlementStatus?: number
}): Promise<PagedResult<InsSettlementListItemDto>> {
  return get('/insurance/settlements', params)
}

export function createSettlement(data: object): Promise<InsSettlementDto> {
  return post('/insurance/settlements', data)
}

export function updateSettlement(id: number, data: object): Promise<InsSettlementDto> {
  return put(`/insurance/settlements/${id}`, data)
}

export function submitSettlement(id: number): Promise<boolean> {
  return put(`/insurance/settlements/${id}/submit`, {})
}

export function reviewSettlement(id: number, data: { ApprovalAction: number; ApprovalComment?: string; TransferTargetId?: number; TransferTargetName?: string }): Promise<boolean> {
  return put(`/insurance/settlements/${id}/review`, data)
}

export function approveSettlement(id: number, data: { ApprovalComment?: string }): Promise<boolean> {
  return put(`/insurance/settlements/${id}/approve`, data)
}

export function paySettlement(id: number, data: { PaymentVoucher?: string }): Promise<boolean> {
  return put(`/insurance/settlements/${id}/pay`, data)
}

export function getMyPendingSettlements(params?: {
  pageIndex?: number
  pageSize?: number
}): Promise<PagedResult<InsSettlementListItemDto>> {
  return get('/insurance/settlements/pending-my', params)
}

export function getApprovalHistory(id: number): Promise<InsApprovalRecordListItemDto[]> {
  return get(`/insurance/settlements/${id}/approval-history`)
}

// ==================== 共保基金 API ====================

export function getFundList(params: {
  pageIndex: number
  pageSize: number
  keyword?: string
  businessType?: number
  fundStatus?: number
}): Promise<PagedResult<InsCoInsuranceFundListItemDto>> {
  return get('/insurance/funds', params)
}

export function createFund(data: object): Promise<InsCoInsuranceFundDto> {
  return post('/insurance/funds', data)
}

export function updateFund(id: number, data: object): Promise<InsCoInsuranceFundDto> {
  return put(`/insurance/funds/${id}`, data)
}

export function generateContributions(fundId: number): Promise<number> {
  return post(`/insurance/funds/${fundId}/contributions/generate`, {})
}

export function confirmContribution(id: number): Promise<boolean> {
  return put(`/insurance/funds/contributions/${id}/confirm`, {})
}

export function getFundContributions(fundId: number, params?: {
  pageIndex?: number
  pageSize?: number
  paymentStatus?: number
}): Promise<PagedResult<InsFundContributionListItemDto>> {
  return get(`/insurance/funds/${fundId}/contributions`, params)
}

// ==================== 审批流配置 API ====================

export function getApprovalConfigList(params?: {
  pageIndex?: number
  pageSize?: number
}): Promise<PagedResult<InsApprovalConfigListItemDto>> {
  return get('/insurance/approval-config', params)
}

export function createApprovalConfig(data: object): Promise<InsApprovalConfigDto> {
  return post('/insurance/approval-config', data)
}

export function updateApprovalConfig(id: number, data: object): Promise<InsApprovalConfigDto> {
  return put(`/insurance/approval-config/${id}`, data)
}

export function updateApprovalConfigStatus(id: number, status: number): Promise<boolean> {
  return put(`/insurance/approval-config/${id}/status`, { Status: status })
}

export function reorderApprovalConfig(data: { Items: { Id: number; StepOrder: number }[] }): Promise<boolean> {
  return put('/insurance/approval-config/reorder', data)
}

// ==================== 报表 API ====================

export function getPremiumSummary(params?: {
  businessType?: number
  startDate?: string
  endDate?: string
}): Promise<PremiumSummaryDto[]> {
  return get('/insurance/reports/premium-summary', params)
}

export function getClaimAnalysis(params?: {
  businessType?: number
  startDate?: string
  endDate?: string
}): Promise<ClaimAnalysisDto[]> {
  return get('/insurance/reports/claim-analysis', params)
}

export function getSettlementAnalysis(params?: {
  businessType?: number
  startDate?: string
  endDate?: string
}): Promise<SettlementAnalysisDto[]> {
  return get('/insurance/reports/settlement-analysis', params)
}

export function getFundBalance(params?: {
  fundId?: number
}): Promise<FundBalanceDto[]> {
  return get('/insurance/reports/fund-balance', params)
}

export function getOverview(): Promise<OverviewDto> {
  return get('/insurance/reports/overview')
}
