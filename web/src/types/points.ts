// ==================== 积分模块类型定义 ====================

// ==================== 积分来源 ====================

export interface PointSourceDto {
  id: number
  orgId: number
  sourceName: string
  sourceCode: string
  icon: string | null
  color: string | null
  description: string | null
  sortOrder: number
  isEnabled: boolean
}

export interface CreatePointSourceRequest {
  sourceName: string
  sourceCode: string
  icon?: string | null
  color?: string | null
  description?: string | null
  sortOrder: number
}

export interface UpdatePointSourceRequest {
  sourceName: string
  icon?: string | null
  color?: string | null
  description?: string | null
  sortOrder: number
  isEnabled: boolean
}

// ==================== 积分规则 ====================

export interface PointRuleListDto {
  id: number
  orgId: number
  sourceId: number
  sourceName: string | null
  ruleName: string
  ruleCode: string
  eventType: string
  pointValue: number
  conditionDescription: string | null
  cycleLimit: number
  requireApproval: boolean
  sortOrder: number
  isEnabled: boolean
  createTime: string
  updateTime: string
}

export interface PointRuleDetailDto {
  id: number
  orgId: number
  sourceId: number
  sourceName: string | null
  ruleName: string
  ruleCode: string
  eventType: string
  pointValue: number
  conditionExpression: string | null
  conditionDescription: string | null
  multiplierRule: string | null
  cycleLimit: number
  requireApproval: boolean
  sortOrder: number
  isEnabled: boolean
  /** 账户类型（1=A / 2=B） */
  accountType: number
  /** 清算策略（0=不清算 / 1=月清 / 2=年清） */
  resetStrategy: number
  /** 转换比例 */
  convertRatio: number
  createTime: string
  updateTime: string
}

export interface CreatePointRuleRequest {
  sourceId: number
  ruleName: string
  ruleCode: string
  eventType: string
  pointValue: number
  conditionExpression?: string | null
  conditionDescription?: string | null
  multiplierRule?: string | null
  cycleLimit: number
  requireApproval: boolean
  sortOrder: number
  /** 账户类型（1=A / 2=B） */
  accountType?: number
  /** 清算策略（0=不清算 / 1=月清 / 2=年清） */
  resetStrategy?: number
  /** 转换比例 */
  convertRatio?: number
}

export interface UpdatePointRuleRequest {
  sourceId: number
  ruleName: string
  eventType: string
  pointValue: number
  conditionExpression?: string | null
  conditionDescription?: string | null
  multiplierRule?: string | null
  cycleLimit: number
  requireApproval: boolean
  sortOrder: number
  isEnabled: boolean
  /** 账户类型（1=A / 2=B） */
  accountType?: number
  /** 清算策略（0=不清算 / 1=月清 / 2=年清） */
  resetStrategy?: number
  /** 转换比例 */
  convertRatio?: number
}

export interface PointRulePagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  sourceId?: number | null
  isEnabled?: boolean | null
}

// ==================== 积分申请 ====================

export interface PointApplicationListDto {
  id: number
  orgId: number
  applicantId: number
  applicantName: string | null
  ruleId: number
  ruleName: string | null
  pointValue: number
  applicationNote: string
  status: number
  approverId: number | null
  approverName: string | null
  approvalTime: string | null
  createTime: string
}

export interface PointApplicationDetailDto {
  id: number
  orgId: number
  applicantId: number
  applicantName: string | null
  ruleId: number
  ruleName: string | null
  sourceName: string | null
  pointValue: number
  applicationNote: string
  attachment: string | null
  status: number
  approverId: number | null
  approverName: string | null
  approvalComment: string | null
  approvalTime: string | null
  createTime: string
}

export interface SubmitPointApplicationRequest {
  ruleId: number
  applicationNote: string
  attachment?: string | null
}

export interface ApprovePointApplicationRequest {
  approvalComment?: string | null
}

// ==================== 积分记录 ====================

export interface PointRecordListDto {
  id: number
  orgId: number
  userId: number
  userName: string | null
  sourceId: number
  sourceName: string | null
  ruleId: number | null
  ruleName: string | null
  type: number
  pointValue: number
  balance: number
  relatedModule: string | null
  relatedEntityType: string | null
  relatedEntityId: number | null
  operatorId: number
  operatorName: string | null
  remark: string
  /** 账户类型（1=A / 2=B） */
  accountType: number
  createTime: string
}

export interface ManualAwardRequest {
  userId: number
  sourceId: number
  pointValue: number
  remark: string
}

export interface ManualDeductRequest {
  userId: number
  sourceId: number
  pointValue: number
  remark: string
}

export interface PointRecordPagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  userId?: number | null
  sourceId?: number | null
  type?: number | null
  /** 账户类型筛选（1=A / 2=B） */
  accountType?: number | null
  relatedModule?: string | null
  relatedEntityType?: string | null
  startTime?: string | null
  endTime?: string | null
}

// ==================== 积分账户 ====================

export interface PointAccountDto {
  id: number
  orgId: number
  userId: number
  userName: string | null
  /** 账户类型（1=A / 2=B / 0=聚合视图） */
  accountType: number
  totalPoints: number
  usedPoints: number
  availablePoints: number
  monthlyAward: number
  monthlyDeduct: number
  yearlyPoints: number
  /** A 分账户可用余额（双账户聚合视图中填充） */
  fAPoints: number
  /** B 分账户可用余额（双账户聚合视图中填充） */
  fBPoints: number
  /** 期初余额快照日期 */
  snapshotDate: string | null
  /** 期初余额快照值 */
  snapshotValue: number
  updateTime: string
}

export interface PointStatisticsDto {
  totalPoints: number
  availablePoints: number
  monthlyAward: number
  monthlyDeduct: number
  yearlyPoints: number
  currentRank: number | null
  bySource: SourceStatItem[]
  monthlyTrend: TrendItem[]
}

export interface SourceStatItem {
  sourceId: number
  sourceName: string
  color: string | null
  totalPoints: number
  count: number
}

export interface TrendItem {
  period: string
  awardPoints: number
  deductPoints: number
  netPoints: number
}

// ==================== 兑换商品 ====================

export interface RedeemItemListDto {
  id: number
  fuid: string
  orgId: number
  name: string
  category: number
  image: string | null
  requiredPoints: number
  stock: number
  redeemedCount: number
  status: number
  sortOrder: number
}

export interface RedeemItemDetailDto {
  id: number
  fuid: string
  orgId: number
  name: string
  category: number
  description: string
  image: string | null
  requiredPoints: number
  stock: number
  redeemedCount: number
  status: number
  sortOrder: number
  createTime: string
  updateTime: string
}

export interface CreateRedeemItemRequest {
  name: string
  category: number
  description: string
  image?: string | null
  requiredPoints: number
  stock?: number
  sortOrder: number
}

export interface UpdateRedeemItemRequest {
  name: string
  category: number
  description: string
  image?: string | null
  requiredPoints: number
  stock?: number
  sortOrder: number
  status: number
}

export interface ExchangeRequest {
  itemId: number
  remark?: string | null
}

// ==================== 兑换记录 ====================

export interface RedeemRecordListDto {
  id: number
  orgId: number
  userId: number
  userName: string | null
  itemId: number
  itemName: string | null
  deductedPoints: number
  status: number
  issuerId: number | null
  issuerName: string | null
  issueTime: string | null
  remark: string | null
  createTime: string
}

// ==================== 管理层配额 ====================

export interface ManagerQuotaListDto {
  id: number
  orgId: number
  managerId: number
  managerName: string | null
  yearMonth: string
  awardQuota: number
  deductQuota: number
  usedAward: number
  usedDeduct: number
  status: number
  createTime: string
}

export interface SaveManagerQuotaRequest {
  managerId: number
  yearMonth: string
  awardQuota: number
  deductQuota: number
}

export interface MyQuotaDto {
  id: number
  yearMonth: string
  awardQuota: number
  deductQuota: number
  usedAward: number
  usedDeduct: number
  remainingAward: number
  remainingDeduct: number
  status: number
}

// ==================== 排行榜 ====================

export interface RankingListDto {
  id: number
  userId: number
  userName: string | null
  departmentName: string | null
  totalPoints: number
  awardPoints: number
  deductPoints: number
  rank: number
  period: string
  dimension: number
}

export interface DepartmentRankingDto {
  departmentId: number
  departmentName: string
  totalPoints: number
  awardPoints: number
  deductPoints: number
  memberCount: number
  avgPoints: number
  rank: number
}

export interface MyRankingDto {
  totalPoints: number
  awardPoints: number
  deductPoints: number
  rank: number
  totalUsers: number
  period: string
  dimension: number
  trends: RankTrendItem[]
}

export interface RankTrendItem {
  period: string
  totalPoints: number
  rank: number
}

// ==================== 积分事件 ====================

export interface PointEventDto {
  eventType: string
  userId: number
  orgId: number
  sourceModule: string
  entityType?: string | null
  entityId?: number | null
  context?: any
}
