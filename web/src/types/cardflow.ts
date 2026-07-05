// ==================== CardFlow 卡片流引擎模块类型定义 ====================

// ==================== 流程定义 (FlowDefinition) ====================

export interface FlowDefinitionDto {
  id: number
  flowName: string
  flowCode: string
  description: string | null
  status: string
  numberTemplate: string | null
  titleTemplate: string | null
  allowedRolesJson: string | null
  flowGroupId: number | null
  orgId: number
  createdTime: string
  /** 导入匹配规则 JSON（如 {"fileNamePattern":"*韵达*"}），仅详情接口返回 */
  matchPattern?: string | null
  currentVersion?: number | null
  lastPublishedTime?: string | null
  hasDraft?: boolean
  draftVersion?: number | null
}

export interface FlowDefinitionQueryRequest {
  page?: number
  pageSize?: number
  status?: string | null
  /** 多状态筛选，逗号分隔（如 "draft,published"） */
  statuses?: string | null
  orgId?: number | null
  keyword?: string | null
  flowGroupId?: number | null
  /** 排序字段白名单：flowName / createdTime / lastPublishedTime */
  sortField?: string | null
  /** asc / desc */
  sortOrder?: string | null
}

export interface CreateFlowDefinitionRequest {
  flowName: string
  flowCode: string
  description?: string | null
  numberTemplate?: string | null
  titleTemplate?: string | null
  allowedRolesJson?: string | null
  flowGroupId?: number | null
  matchPattern?: string | null
  orgId?: number
}

export interface UpdateFlowDefinitionRequest {
  flowName?: string | null
  description?: string | null
  numberTemplate?: string | null
  titleTemplate?: string | null
  allowedRolesJson?: string | null
  flowGroupId?: number | null
  matchPattern?: string | null
}

// ==================== 流程版本 (FlowVersion) ====================

export interface FlowVersionDto {
  id: number
  versionNumber: number
  status: string
  isCurrentVersion: boolean
  createdTime: string
  publishTime: string | null
}

export interface FlowVersionDetailDto extends FlowVersionDto {
  cardSchemaJson: string | null
  detailSchemaJson: string | null
  flowSettingsJson: string | null
  stages: StageDefinitionDto[]
  routes: StageRouteRuleDto[]
  dynamicPolicies: DynamicStagePolicyDto[]
}

export interface SaveDraftVersionRequest {
  cardSchemaJson?: string | null
  detailSchemaJson?: string | null
  flowSettingsJson?: string | null
  stages: StageDefinitionRequest[]
  routes: StageRouteRuleRequest[]
  dynamicPolicies: DynamicStagePolicyRequest[]
}

// ==================== 阶段定义 (StageDefinition) ====================

export interface StageDefinitionDto {
  id: number
  stageKey?: string
  sortOrder: number
  stageName: string
  type: string
  approvalMode: string | null
  assigneeStrategy: string | null
  assigneeConfigJson: string | null
  conditionJson: string | null
  inputFieldsJson: string | null
  // 废除：autoPluginName / autoPluginConfigJson —— 改为插件注册+规则引用
  pluginRegistryId?: number | null
  pluginRuleId?: number | null
  failurePolicyJson: string | null
  ccConfigJson: string | null
  timeoutHours: number | null
  priorityTemplate: number | null
  processingGranularity?: 'card' | 'batch'
}

export interface StageRouteRuleRequest {
  edgeKey: string
  fromStageKey: string
  toStageKey: string
  routeName: string
  conditionJson?: string | null
  priority: number
  isDefault: boolean
  status?: string | null
  failurePolicyJson?: string | null
}

export interface StageRouteRuleDto {
  id: number
  edgeKey: string
  fromStageKey: string
  toStageKey: string
  routeName: string
  conditionJson: string | null
  priority: number
  isDefault: boolean
  status: string
  failurePolicyJson: string | null
}

export interface DynamicStagePolicyRequest {
  policyKey: string
  sourceStageKey: string
  policyName: string
  strategyType: string
  strategyConfigJson?: string | null
  conditionJson?: string | null
  triggerTiming?: string
  insertPosition?: string
  continuationStageKey?: string | null
  priority: number
  maxInsertCount?: number | null
  fallbackJson?: string | null
  status?: string | null
}

export interface DynamicStagePolicyDto {
  id: number
  policyKey: string
  sourceStageKey: string
  policyName: string
  strategyType: string
  strategyConfigJson: string | null
  conditionJson: string | null
  triggerTiming: string
  insertPosition: string
  continuationStageKey: string | null
  priority: number
  maxInsertCount: number
  fallbackJson: string | null
  status: string
}

export interface CardFlowPathPreviewRequest {
  flowVersionId?: number | null
  dataJson?: string | null
  initialDataJson?: string | null
  initiatorId?: number | null
  orgId?: number | null
  sourceModule?: string | null
  sourceType?: string | null
  sourceId?: number | null
  maxSteps?: number | null
  details?: PreviewDetailRow[] | null
}

export interface PreviewDetailRow {
  detailTableKey?: string | null
  dataJson?: string | null
  sortOrder?: number
}

/** 设计期呈现预览请求：草稿定义 + 样例数据 → 节点工作视图真值 */
export interface CardPresentationPreviewRequest {
  flowVersionId?: number | null
  stageKey?: string | null
  dataJson?: string | null
  details?: PreviewDetailRow[] | null
  viewerMode?: 'assignee' | 'observer' | 'initiator' | string | null
}

/** 呈现预览响应：工作视图 + 脱敏后的数据/明细 */
export interface CardPresentationPreviewDto {
  workView: StageWorkView
  redactedDataJson: string
  redactedDetails: CardDetailRowDto[]
}

export interface CardFlowPathPreviewDto {
  flowDefinitionId: number
  flowVersionId: number
  steps: CardFlowPathPreviewStepDto[]
  warnings: string[]
}

export interface CardFlowPathPreviewStepDto {
  order: number
  stepType: 'stage' | 'dynamic' | string
  stageKey: string
  stageName: string
  type: string
  selectedEdgeKey?: string | null
  selectedRouteName?: string | null
  reason?: string | null
  policyKey?: string | null
  policyName?: string | null
  candidates: CardFlowPathPreviewCandidateDto[]
}

export interface CardFlowPathPreviewCandidateDto {
  edgeKey: string
  routeName: string
  toStageKey: string
  priority: number
  isDefault: boolean
  matched: boolean
  explanation: string
  typeErrors: string[]
}

export interface StageDefinitionRequest {
  stageKey?: string | null
  name: string
  type?: string
  sortOrder: number
  approvalMode?: string | null
  assigneeStrategy?: string | null
  assigneeConfigJson?: string | null
  conditionJson?: string | null
  inputFieldsJson?: string | null
  // 废除：autoPluginName / autoPluginConfigJson —— 改为插件注册+规则引用
  pluginRegistryId?: number | null
  pluginRuleId?: number | null
  failurePolicyJson?: string | null
  ccConfigJson?: string | null
  timeoutHours?: number | null
  priorityTemplate?: number | null
  processingGranularity?: 'card' | 'batch'
}

// ==================== 自动插件注册与规则 ====================

/** 自动插件注册项（对应后端 CF自动插件注册） */
export interface AutoPluginRegistryDto {
  id: number
  pluginCode: string
  pluginName: string
  granularity: 'card' | 'batch'
  description?: string | null
}

/** 自动插件规则项（对应后端 CF自动插件规则） */
export interface AutoPluginRuleDto {
  id: number
  pluginCode: string
  ruleName: string
  description?: string | null
}

// ==================== 异常处理 (ProcessingIssue) ====================

export type ProcessingIssueStatus = 'Pending' | 'Processing' | 'Resolved' | 'Ignored' | 'Failed' | string

export interface ProcessingIssueDto {
  id: number
  batchId: number
  stagingId?: number | null
  rowNumber: number
  errorType: string
  issueName?: string | null
  severityLevel?: string | null
  errorField?: string | null
  errorMessage?: string | null
  suggestedFix?: string | null
  originalValue?: string | null
  qualityDimension?: string | null
  dispatchStatus?: string | null
  dispatchType?: string | null
  dispatchRecordId?: number | null
  workItemId?: number | null
  issueType: string
  processResult: number
  resolutionStatus: ProcessingIssueStatus
  resolutionPayloadJson?: string | null
  resolvedBy?: number | null
  resolvedTime?: string | null
  retryStatus?: string | null
  retryMessage?: string | null
  resolveMode?: string | null
  detailRoute?: string | null
  cardFlowCode?: string | null
  cardTemplateCode?: string | null
  actionSchemaJson?: string | null
  afterResolvedAction?: string | null
  aggregationMode?: string | null
  orgId: number
  createdTime: string
}

export interface ProcessingIssueQueryRequest {
  batchId?: number
  errorType?: string
  severityLevel?: string
  resolutionStatus?: string
  dispatchStatus?: string
  keyword?: string
  page?: number
  pageSize?: number
}

export interface ProcessingIssueReportRequest {
  batchId: number
  stagingId?: number | null
  rowNumber?: number
  errorType: string
  severityLevel?: string | null
  errorField?: string | null
  errorMessage?: string | null
  suggestedFix?: string | null
  originalValue?: string | null
  qualityDimension?: string | null
  resolutionPayloadJson?: string | null
}

export interface ProcessingIssueResolveRequest {
  payloadJson?: string | null
  message?: string | null
}

export interface ProcessingIssueRetryRequest {
  retryAction?: string | null
  message?: string | null
  payloadJson?: string | null
}

// ==================== 卡片 (Card) ====================

export interface CardListDto {
  id: number
  cardNumber: string | null
  title: string | null
  status: string
  flowName: string
  initiatorName: string
  createdTime: string
  submitTime: string | null
  completedTime: string | null
  sourceModule?: string | null
  sourceType?: string | null
  sourceId?: number | null
  returnUrl?: string | null
  sourceTitle?: string | null
}

export interface CardDetailDto extends CardListDto {
  // 预填业务载荷仅在详情下发（后端已脱敏）；列表不携带。见脱敏链重构 spec §9.1。
  initialDataJson?: string | null
  flowDefinitionId: number
  flowVersionId: number
  initiatorId: number
  currentStageInstanceId: number | null
  dataJson: string | null
  currentRound: number
  concurrencyStamp: string | null
  stageInstances: StageInstanceDto[]
  details: CardDetailRowDto[]
  auditTrail: CardFlowRuntimeAuditDto[]
  currentStageWorkView?: StageWorkView | null
}

export interface CardFlowRuntimeAuditDto {
  id: number
  snapshotType: 'routeDecision' | 'dynamicApprover' | string
  cardId: number
  round: number
  stageInstanceId?: number | null
  stageName?: string | null
  stageKey?: string | null
  edgeKey?: string | null
  policyKey?: string | null
  title?: string | null
  reason: string
  eventTime: string
  metadata: Record<string, any>
}

export interface StageInstanceDto {
  id: number
  stageDefinitionId?: number | null
  stageName: string
  type: string
  status: string
  round: number
  finalAction: string | null
  opinion: string | null
  activatedTime: string | null
  completedTime: string | null
  isTimeout: boolean
  assignees: AssigneeDto[]
}

export interface AssigneeDto {
  id: number
  userId: number
  userName: string
  status: string
  opinion: string | null
  completedTime: string | null
}

/** file 字段值：只存元数据，url 指向授权下载端点，附件内容落盘在服务端 secure-uploads */
export interface CardFileValue {
  name: string
  url: string
  size?: number
  mimeType?: string
}

export interface CardDetailRowDto {
  id: number
  detailTableKey: string
  sortOrder: number
  dataJson: string | null
}

export interface StageWorkView {
  sections: StageViewSection[]
  fieldAccess: Record<string, StageFieldAccess>
  detailAccess: Record<string, StageDetailAccess>
  components: CardComponentRuntime[]
  detailSummary: Record<string, any>
  actionPolicy: StageActionPolicy
  summary?: StageSummaryProfile | null
}

export interface CardComponentRuntime {
  id: string
  type: string
  title: string
  access: 'hidden' | 'masked' | 'readonly' | 'editable' | 'required' | string
  visible: boolean
  editable: boolean
  required: boolean
  masked: boolean
  binding: CardComponentBinding
  props: Record<string, any>
  value?: any
  statisticKey?: string | null
  columns: CardComponentColumnRuntime[]
  rows: CardComponentRowRuntime[]
  snapshots: CardPresentationSnapshot[]
  warnings: string[]
}

export interface CardHeaderConfig {
  titleMode: 'flowName' | 'fixed' | 'field' | 'template' | string
  titleText?: string | null
  titleFieldKey?: string | null
  titleTemplate?: string | null
  subtitleMode: 'flowCode' | 'fixed' | 'field' | 'template' | 'hidden' | string
  subtitleText?: string | null
  subtitleFieldKey?: string | null
  subtitleTemplate?: string | null
  showSubtitle?: boolean | null
  showStatus?: boolean | null
  align?: 'left' | 'center' | string | null
}

export interface CardComponentBinding {
  source: 'cardField' | 'detailTable' | 'detailSummary' | 'relation' | 'snapshot' | string
  fieldKey?: string
  detailTableKey?: string
  summaryKey?: string
  relationType?: string
  snapshotType?: string
}

export interface CardComponentDefinition {
  id: string
  type: string
  title: string
  binding: CardComponentBinding
  props?: Record<string, any>
  validation?: CardComponentValidationConfig | null
  visibilityCondition?: string
  access?: 'hidden' | 'masked' | 'readonly' | 'editable' | 'required' | string
  layout?: Record<string, any>
  aggregation?: CardComponentAggregationConfig | null
  statisticKey?: string
}

export interface CardComponentValidationConfig {
  minRows?: number
  requiredColumns?: string[]
}

export interface CardComponentAggregationConfig {
  sum?: Array<{ fieldKey: string; targetKey: string }>
}

export interface CardComponentAccessRule {
  access: 'hidden' | 'masked' | 'readonly' | 'editable' | 'required' | string
  required?: boolean | null
  maskPattern?: string | null
}

export interface CardComponentColumnRuntime {
  key: string
  label: string
  type: string
  access: string
  editable: boolean
  required: boolean
  masked: boolean
}

export interface CardComponentRowRuntime {
  id?: number | null
  sortOrder: number
  values: Record<string, any>
}

export interface CardPresentationSnapshot {
  snapshotType: string
  title?: string | null
  reason: string
  metadata: Record<string, any>
}

export interface StageViewSection {
  key: string
  title?: string | null
  type: 'fields' | 'detailTable' | string
  fields: StageViewField[]
}

export interface StageViewField {
  fieldKey: string
  label?: string | null
}

export interface StageFieldAccess {
  access: 'hidden' | 'masked' | 'readonly' | 'editable' | 'required' | string
  required?: boolean | null
  maskPattern?: string | null
}

export interface StageDetailAccess {
  access: 'hidden' | 'masked' | 'readonly' | 'editable' | 'required' | string
  required?: boolean | null
  maskPattern?: string | null
}

export interface StageActionPolicy {
  allowedActions: string[]
}

export interface StageSummaryProfile {
  fields: string[]
}

export interface ApprovalModeConfig {
  mode: 'single' | 'countersign' | 'orsign' | 'sequential' | string
}

export interface AvailableFlowDto {
  id: number
  flowName: string
  flowCode: string
  description: string | null
}

export interface CardBalanceDto {
  id: number
  cardId: number
  cardNumber: string | null
  cardTitle: string | null
  originalAmount: number
  offsetAmount: number
  remainingAmount: number
  status: string
}

export interface CardRelationDto {
  id: number
  sourceCardId: number
  sourceCardNumber: string | null
  targetCardId: number
  targetCardNumber: string | null
  relationType: string
  description: string | null
  offsetAmount: number | null
}

export interface CardOperationResult {
  success: boolean
  message: string | null
  cardId: number | null
  cardNumber: string | null
  newStatus: string | null
}

// ==================== 卡片请求 ====================

export interface CardQueryRequest {
  page?: number
  pageSize?: number
  status?: string | null
  flowId?: number | null
  orgId?: number | null
  initiatorId?: number | null
  sourceModule?: string | null
  sourceType?: string | null
  sourceId?: number | null
  startDate?: string | null
  endDate?: string | null
}

export interface CreateCardRequest {
  flowDefinitionId: number
  dataJson?: string | null
  orgId: number
  sourceModule?: string | null
  sourceType?: string | null
  sourceId?: number | null
  returnUrl?: string | null
  initialDataJson?: string | null
  sourceTitle?: string | null
}

export interface UpdateCardRequest {
  dataJson?: string | null
  concurrencyStamp?: string | null
  details?: UpdateCardDetailRequest[] | null
}

export interface UpdateCardDetailRequest {
  id?: number | null
  detailTableKey?: string
  sortOrder: number
  dataJson?: string | null
}

export interface CreateRelationRequest {
  targetCardId: number
  relationType: string
  description?: string | null
  offsetAmount?: number | null
}

// ==================== 审批操作请求 ====================

export interface ApproveRequest {
  opinion?: string | null
  supplementData?: Record<string, unknown> | null
  detailEdits?: DetailRowEditRequest[] | null
  concurrencyStamp?: string | null
}

export interface DetailRowEditRequest {
  detailId?: number | null
  detailTableKey?: string
  rowIndex: number
  values: Record<string, unknown>
}

export interface RejectRequest {
  opinion?: string | null
  targetStageId?: number | null
  returnMode?: 'toInitiator' | 'toPrevious' | 'toSpecified' | string | null
  concurrencyStamp?: string | null
}

export interface CountersignRequest {
  userId: number
  insertMode?: 'before' | 'after' | string
  opinion?: string | null
}

export interface TransferRequest {
  newUserId: number
  opinion?: string | null
}

export interface CcRequest {
  userIds: number[]
  opinion?: string | null
}

// ==================== 待办 (Todo) ====================

export interface TodoItemDto {
  id: number
  cardId: number
  cardNumber: string | null
  title: string | null
  flowName: string
  type: string
  status: string
  priority: number
  initiatorName: string
  createdTime: string
}

export interface TodoQueryRequest {
  page?: number
  pageSize?: number
  status?: string | null
  flowId?: number | null
}

export interface TodoCountDto {
  todo: number
  initiated: number
  cc: number
}

export interface TodoStatsDto {
  totalPending: number
  avgProcessHours: number
  timeoutRate: number
  todayCompleted: number
  flowStats: FlowTodoStat[]
  trend: TodoTrendPoint[]
}

export interface FlowTodoStat {
  flowName: string
  pendingCount: number
  completedCount: number
  avgProcessHours: number
  timeoutRate: number
}

export interface TodoTrendPoint {
  date: string
  avgProcessHours: number
}

export interface TodoStatsRequest {
  orgId: number
  flowId?: number | null
  startDate?: string | null
  endDate?: string | null
  timeoutHours?: number
}

export interface CardFlowRuntimeMonitoringRequest {
  orgId?: number | null
  flowDefinitionId?: number | null
  flowVersionId?: number | null
  startDate?: string | null
  endDate?: string | null
}

export interface CardFlowRuntimeMonitoringDto {
  routeHitCount: number
  fallbackCount: number
  handlerUnresolvedCount: number
  dynamicInsertCount: number
  ruleWarningCount: number
  buckets: CardFlowRuntimeMonitoringBucketDto[]
}

export interface CardFlowRuntimeMonitoringBucketDto {
  flowCode: string
  flowVersionId: number
  stageKey?: string | null
  edgeKey?: string | null
  policyKey?: string | null
  sourceModule?: string | null
  sourceType?: string | null
  orgId: number
  dateBucket: string
  routeHitCount: number
  fallbackCount: number
  handlerUnresolvedCount: number
  dynamicInsertCount: number
  ruleWarningCount: number
}

// ==================== 委托 (Delegation) ====================

export interface DelegationDto {
  id: number
  delegatorId: number
  delegatorName: string
  trusteeId: number
  trusteeName: string
  startTime: string
  endTime: string
  applicableFlowsJson: string | null
  status: string
}

export interface CreateDelegationRequest {
  trusteeId: number
  trusteeName: string
  startTime: string
  endTime: string
  applicableFlowsJson?: string | null
}

export interface UpdateDelegationRequest {
  trusteeId?: number | null
  trusteeName?: string | null
  startTime?: string | null
  endTime?: string | null
  applicableFlowsJson?: string | null
}

// ==================== 流程组 (FlowGroup) ====================

export interface FlowGroupDto {
  id: number
  groupName: string
  groupCode: string
  description: string | null
  status: string
  flowCount: number
  linkCount: number
}

export interface CreateFlowGroupRequest {
  groupName: string
  groupCode: string
  description?: string
  orgId: number
}

export interface UpdateFlowGroupRequest {
  groupName?: string | null
  description?: string | null
  status?: string | null
}

export interface FlowGroupLinkDto {
  id: number
  sourceFlowId: number
  sourceFlowName: string
  targetFlowId: number
  targetFlowName: string
  triggerCondition: string | null
  fieldMappingJson: string | null
  triggerMode: string
  sortOrder: number
}

export interface SaveFlowGroupLinkRequest {
  sourceFlowId: number
  targetFlowId: number
  triggerCondition?: string | null
  fieldMappingJson?: string | null
  triggerMode?: string
  sortOrder?: number
}

// ==================== 操作日志 (ActionLog) ====================

export interface ActionLogDto {
  id: number
  actionType: string
  operatorName: string
  operationTime: string
  opinion: string | null
  detailJson: string | null
}

export interface AuditLogItemDto {
  id: number
  cardId: number
  cardNumber: string | null
  cardTitle: string | null
  flowName: string
  stageName: string | null
  actionType: string
  operatorName: string
  operationTime: string
  opinion: string | null
  detailJson: string | null
}

export interface AuditLogQueryRequest {
  page?: number
  pageSize?: number
  /** 逗号分隔的操作类型列表 */
  actionTypes?: string | null
  operatorName?: string | null
  cardNumber?: string | null
  startDate?: string | null
  endDate?: string | null
}

// ==================== Schema 字段定义 ====================

export type SchemaFieldType =
  | 'text'
  | 'money'
  | 'enum'
  | 'date'
  | 'file'
  | 'user'
  | 'org'
  | 'cardRef'
  | 'account'        // 财务科目
  | 'auxiliary'      // 辅助核算
  | 'bankAccount'    // 银行账户
  | 'voucherRef'     // 凭证引用（只读链接）

export type AuxiliaryType = 'employee' | 'supplier' | 'customer' | 'department' | 'project'
export type SchemaAutoSource = 'currentUser' | 'currentOrg' | 'currentUserDepartment'

export interface SchemaFieldDefinition {
  key: string
  label: string
  type: SchemaFieldType
  required?: boolean
  readonly?: boolean
  sensitive?: boolean
  maskPattern?: string | null
  defaultValue?: any
  options?: string[]            // enum 类型的选项
  placeholder?: string
  dataSource?: 'none' | 'auto' | 'computed'
  autoSource?: SchemaAutoSource
  computeExpr?: string
  // file 类型
  accept?: string
  maxSize?: number              // 文件最大体积(MB)
  // cardRef 类型
  targetFlowCode?: string
  displayFields?: string[]
  // auxiliary 类型
  auxType?: AuxiliaryType
}

// ==================== Schema 字段值（结构化字段） ====================

/** 财务科目字段值 */
export interface AccountFieldValue {
  id: number
  code: string
  name?: string
}

/** 辅助核算字段值 */
export interface AuxiliaryFieldValue {
  id: number
  code?: string
  name: string
  auxType?: AuxiliaryType
}

/** 银行账户字段值 */
export interface BankAccountFieldValue {
  id: number
  accountNo: string
  accountName?: string
  bankName?: string
  /** 关联财务科目 ID（选择后自动带出） */
  accountId?: number
  accountCode?: string
}

/** 凭证引用字段值 */
export interface VoucherRefFieldValue {
  id: number
  voucherNumber?: string
  period?: string
}

// ==================== 批次管理 (CfBatch) ====================

/** 批次状态：0=解析中 1=已暂存 2=质检中 3=已创建卡片 4=处理中 5=已完成 */
export type CfBatchStatus = 0 | 1 | 2 | 3 | 4 | 5

/** 批次行状态：0=待处理 1=质检通过 2=已创建卡片 3=质检失败 4=已排除 5=已撤销 */
export type CfBatchRowStatus = 0 | 1 | 2 | 3 | 4 | 5

/** 批次触发类型 */
export type CfBatchTriggerType = 'human' | 'fileUpload' | 'scheduled' | 'conditional'

export interface CfBatch {
  id: number
  flowDefinitionId: number
  flowName?: string
  orgId: number
  triggeredById: number
  triggeredByName?: string
  triggeredTime: string
  triggerType: CfBatchTriggerType
  filePath?: string
  fileName?: string
  columnMappingJson?: string
  totalRows: number
  successRows: number
  failedRows: number
  status: CfBatchStatus
  isRevoked: boolean
  revokedTime?: string
  revokedById?: number
}

export interface CfBatchRow {
  id: number
  batchId: number
  rowNo: number
  dataJson: string
  status: CfBatchRowStatus
  errorMessage?: string
  cardId?: number
  /** 乐观锁版本（Base64 字符串） */
  rowVersion: string
}

/** 批次进度 DTO */
export interface BatchProgressDto {
  batchId: number
  totalCards: number
  /** 各阶段进度，key 为阶段名/编码，value 为完成数 */
  stageProgress: Record<string, number>
  completed: number
  failed: number
}

/** 批次列表查询参数 */
export interface CfBatchQueryRequest {
  flowDefinitionId?: number
  status?: CfBatchStatus
  page?: number
  pageSize?: number
}

/** 批次行查询参数 */
export interface CfBatchRowQueryRequest {
  page?: number
  pageSize?: number
  status?: CfBatchRowStatus
}

/** 批次行更新请求 */
export interface UpdateBatchRowRequest {
  dataJson: string
  rowVersion: string
}

/** 文件上传触发配置 */
export interface FileUploadTriggerConfig {
  enabled: boolean
  accept?: string
  /** 默认列映射：Excel 列头 -> Schema 字段 key */
  defaultMapping?: Record<string, string>
  /** 模板下载地址 */
  templateUrl?: string
}

/** 触发器配置（解析自 flowDefinition.triggerConfigJson） */
export interface TriggerConfig {
  fileUpload?: FileUploadTriggerConfig
  scheduled?: { enabled: boolean; cron?: string }
  conditional?: { enabled: boolean; expression?: string }
}

// ==================== 分页结果 ====================

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}
