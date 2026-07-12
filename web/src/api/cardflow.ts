// CardFlow 卡片流引擎模块 API
import service, { get, post, put, del } from './request'
import type {
  FlowDefinitionDto,
  FlowDefinitionQueryRequest,
  CreateFlowDefinitionRequest,
  UpdateFlowDefinitionRequest,
  FlowVersionDto,
  FlowVersionDetailDto,
  SaveDraftVersionRequest,
  CardFlowPathPreviewRequest,
  CardFlowPathPreviewDto,
  RouteHitEstimateDto,
  CardPresentationPreviewRequest,
  CardPresentationPreviewDto,
  CardListDto,
  CardDetailDto,
  CardFileValue,
  CardQueryRequest,
  CreateCardRequest,
  UpdateCardRequest,
  CardOperationResult,
  ApproveRequest,
  RejectRequest,
  CountersignRequest,
  TransferRequest,
  CcRequest,
  CustomActionRequest,
  CreateRelationRequest,
  CardRelationDto,
  CardBalanceDto,
  AvailableFlowDto,
  ActionLogDto,
  TodoItemDto,
  TodoQueryRequest,
  TodoCountDto,
  TodoStatsDto,
  TodoStatsRequest,
  CardFlowRuntimeMonitoringDto,
  CardFlowRuntimeMonitoringRequest,
  AuditLogItemDto,
  AuditLogQueryRequest,
  DelegationDto,
  CreateDelegationRequest,
  UpdateDelegationRequest,
  FlowGroupDto,
  CreateFlowGroupRequest,
  UpdateFlowGroupRequest,
  FlowGroupLinkDto,
  SaveFlowGroupLinkRequest,
  PagedResult,
  CfBatch,
  CfBatchRow,
  BatchProgressDto,
  CfBatchQueryRequest,
  CfBatchRowQueryRequest,
  UpdateBatchRowRequest,
  AutoPluginRegistryDto,
  ProcessingIssueDto,
  ProcessingIssueQueryRequest,
  ProcessingIssueResolveRequest,
  ProcessingIssueRetryRequest,
  ProcessingIssueReportRequest,
} from '@/types/cardflow'

// ===== 流程定义 API =====

/** 获取流程定义列表 */
export function getFlowDefinitions(params?: FlowDefinitionQueryRequest) {
  return get<PagedResult<FlowDefinitionDto>>('/cardflow/definitions', params)
}

/** 获取流程定义详情 */
export function getFlowDefinition(id: number) {
  return get<FlowDefinitionDto>(`/cardflow/definitions/${id}`)
}

/** 创建流程定义 */
export function createFlowDefinition(data: CreateFlowDefinitionRequest) {
  return post<FlowDefinitionDto>('/cardflow/definitions', data)
}

/** 更新流程定义 */
export function updateFlowDefinition(id: number, data: UpdateFlowDefinitionRequest) {
  return put<FlowDefinitionDto>(`/cardflow/definitions/${id}`, data)
}

/** 删除流程定义（仅草稿状态、无版本发布记录、无卡片/批次引用） */
export function deleteFlowDefinition(id: number) {
  return del(`/cardflow/definitions/${id}`)
}

/** 发布流程定义 */
export function publishFlowDefinition(id: number) {
  return post(`/cardflow/definitions/${id}/publish`)
}

/** 归档流程定义 */
export function archiveFlowDefinition(id: number) {
  return post(`/cardflow/definitions/${id}/archive`)
}

/** 停用流程定义（published → disabled） */
export function disableFlowDefinition(id: number) {
  return post(`/cardflow/definitions/${id}/disable`)
}

/** 启用流程定义（disabled → published） */
export function enableFlowDefinition(id: number) {
  return post(`/cardflow/definitions/${id}/enable`)
}

/** 克隆流程定义（含版本和节点链） */
export function cloneFlowDefinition(
  sourceId: number,
  data: { flowName: string; flowCode: string; description?: string; orgId?: number }
) {
  return post<FlowDefinitionDto>(`/cardflow/definitions/${sourceId}/clone`, data)
}

/** 获取流程模板列表（全局共享 FOrgId=0） */
export function getFlowTemplates() {
  return get<FlowDefinitionDto[]>('/cardflow/templates')
}

/** 从模板克隆到当前组织 */
export function cloneTemplateToOrg(
  templateId: number,
  data: { flowName: string; flowCode: string; description?: string; orgId?: number }
) {
  return post<FlowDefinitionDto>(`/cardflow/templates/${templateId}/clone-to-org`, data)
}

/** 将当前流程保存为模板 */
export function saveAsTemplate(definitionId: number) {
  return post<FlowDefinitionDto>(`/cardflow/definitions/${definitionId}/save-as-template`)
}

/** 获取流程版本列表 */
export function getFlowVersions(id: number) {
  return get<FlowVersionDto[]>(`/cardflow/definitions/${id}/versions`)
}

// 流程版本一经发布内容不可变（改动走新草稿→新版本），按 definitionId:versionId 缓存 Promise，
// 供填单/审批/详情三页反复加载复用；请求失败时剔除缓存条目，避免毒化后续加载。
const flowVersionDetailCache = new Map<string, Promise<FlowVersionDetailDto>>()

/** 获取流程版本详情（内存缓存，版本不可变） */
export function getFlowVersionDetail(id: number, versionId: number) {
  const key = `${id}:${versionId}`
  let cached = flowVersionDetailCache.get(key)
  if (!cached) {
    cached = get<FlowVersionDetailDto>(`/cardflow/definitions/${id}/versions/${versionId}`).catch((e) => {
      flowVersionDetailCache.delete(key)
      throw e
    })
    flowVersionDetailCache.set(key, cached)
  }
  return cached
}

/** 保存草稿版本 */
export function saveFlowDraftVersion(id: number, data: SaveDraftVersionRequest) {
  return put<FlowVersionDetailDto>(`/cardflow/definitions/${id}/draft-version`, data)
}

/** 获取草稿版本 */
export function getFlowDraftVersion(id: number) {
  return get<FlowVersionDetailDto>(`/cardflow/definitions/${id}/draft-version`)
}

/** 取"生效版本"详情：草稿优先，无草稿回退当前发布版（GET draft-version 已无建草稿副作用，纯读场景统一走这里）。 */
export async function getEffectiveFlowVersionDetail(flowId: number): Promise<FlowVersionDetailDto | null> {
  const draft = await getFlowDraftVersion(flowId).catch(() => null)
  if (draft) return draft
  const versions = await getFlowVersions(flowId).catch(() => [])
  const current = (versions || []).find((v) => v.isCurrentVersion)
  return current ? getFlowVersionDetail(flowId, current.id) : null
}

/** 放弃（删除）草稿版本 */
export function discardFlowDraftVersion(id: number) {
  return del(`/cardflow/definitions/${id}/draft-version`)
}

/** 预演草稿流程路径 */
export function previewFlowDraftPath(id: number, data: CardFlowPathPreviewRequest) {
  return post<CardFlowPathPreviewDto>(`/cardflow/definitions/${id}/draft-version/preview-path`, data)
}

/** 路由条件命中率试算：近 30 天采样 ≤500 张卡片干跑条件（B4/E6） */
export function estimateRouteHit(id: number, conditionJson: string) {
  return post<RouteHitEstimateDto>(`/cardflow/definitions/${id}/route-hit-estimate`, { conditionJson })
}

/** 设计期呈现预览：草稿定义 + 样例数据 → 后端真值节点工作视图（替代前端复刻 access/脱敏/聚合） */
export function previewPresentation(id: number, data: CardPresentationPreviewRequest) {
  return post<CardPresentationPreviewDto>(`/cardflow/definitions/${id}/draft-version/preview-presentation`, data)
}

// ===== 自动插件注册与规则 API =====

/** 获取自动插件注册列表（可按处理粒度过滤） */
export function getPluginRegistry(granularity?: 'card' | 'batch' | string) {
  return get<AutoPluginRegistryDto[]>('/cardflow/auto-plugin/registry', granularity ? { granularity } : undefined)
}

/** 获取指定插件代码下的规则列表 */
export function getPluginRules(pluginCode: string) {
  return get<AutoPluginRuleDto[]>('/cardflow/auto-plugin/rules', { pluginCode })
}

// ===== 异常处理 API =====

export function getProcessingIssues(params?: ProcessingIssueQueryRequest) {
  return get<PagedResult<ProcessingIssueDto>>('/cardflow/issues', params)
}

export function getProcessingIssue(id: number) {
  return get<ProcessingIssueDto>(`/cardflow/issues/${id}`)
}

export function reportProcessingIssue(data: ProcessingIssueReportRequest) {
  return post<ProcessingIssueDto>('/cardflow/issues/report', data)
}

export function dispatchProcessingIssues(batchId: number) {
  return post('/cardflow/issues/dispatch', undefined, { params: { batchId } })
}

export function resolveProcessingIssue(id: number, data: ProcessingIssueResolveRequest) {
  return post(`/cardflow/issues/${id}/resolve`, data)
}

export function ignoreProcessingIssue(id: number, data: ProcessingIssueResolveRequest) {
  return post(`/cardflow/issues/${id}/ignore`, data)
}

export function retryProcessingIssue(id: number, data: ProcessingIssueRetryRequest) {
  return post(`/cardflow/issues/${id}/retry`, data)
}

// ===== 卡片操作 API =====

/** 获取可发起的流程列表 */
export function getAvailableFlows(orgId: number) {
  return get<AvailableFlowDto[]>('/cardflow/cards/available-flows', { orgId })
}

/** 获取卡片列表 */
export function getCards(params?: CardQueryRequest) {
  return get<PagedResult<CardListDto>>('/cardflow/cards', params)
}

/** 获取我发起的卡片列表 */
export function getInitiatedCards(params?: CardQueryRequest) {
  return get<PagedResult<CardListDto>>('/cardflow/cards/initiated', params)
}

/** 创建卡片（草稿） */
export function createCard(data: CreateCardRequest) {
  return post<CardDetailDto>('/cardflow/cards', data)
}

/** 获取卡片详情 */
export function getCard(id: number) {
  return get<CardDetailDto>(`/cardflow/cards/${id}`)
}

/** 更新卡片 */
export function updateCard(id: number, data: UpdateCardRequest) {
  return put<CardDetailDto>(`/cardflow/cards/${id}`, data)
}

/** 删除卡片（草稿） */
export function deleteCard(id: number) {
  return del(`/cardflow/cards/${id}`)
}

/** 获取可关联的前置卡片列表 */
export function getAvailablePrerequisites(id: number) {
  return get<CardListDto[]>(`/cardflow/cards/${id}/available-prerequisites`)
}

/** 获取可冲抵的卡片列表 */
export function getAvailableOffsets(id: number) {
  return get<CardBalanceDto[]>(`/cardflow/cards/${id}/available-offsets`)
}

/** 获取卡片余额 */
export function getCardBalance(id: number) {
  return get<CardBalanceDto[]>(`/cardflow/cards/${id}/balance`)
}

/** 创建卡片关联 */
export function createCardRelation(id: number, data: CreateRelationRequest) {
  return post<CardRelationDto>(`/cardflow/cards/${id}/relations`, data)
}

/** 获取卡片关联列表 */
export function getCardRelations(id: number) {
  return get<CardRelationDto[]>(`/cardflow/cards/${id}/relations`)
}

/** 获取关联快照 */
export function getRelationSnapshot(id: number, relationId: number) {
  return get<string>(`/cardflow/cards/${id}/relations/${relationId}/snapshot`)
}

/** 获取卡片操作日志 */
export function getCardLogs(id: number) {
  return get<ActionLogDto[]>(`/cardflow/cards/${id}/logs`)
}

/** 重推通知 */
export function retryPush(id: number) {
  return post(`/cardflow/cards/${id}/retry-push`)
}

// ===== 卡片流程操作 API =====

/** 提交卡片（发起审批） */
export function submitCard(id: number) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/submit`)
}

/** 审批通过 */
export function approveCard(id: number, data: ApproveRequest) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/approve`, data)
}

/** 退回 */
export function rejectCard(id: number, data: RejectRequest) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/reject`, data)
}

/** 撤回 */
export function withdrawCard(id: number) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/withdraw`)
}

/** 重新提交 */
export function resubmitCard(id: number) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/resubmit`)
}

/** 废除 */
export function voidCard(id: number, opinion?: string) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/void`, { opinion })
}

/** 恢复 */
export function resumeCard(id: number) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/resume`)
}

/** 加签 */
export function countersignCard(id: number, data: CountersignRequest) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/countersign`, data)
}

/** 转交 */
export function transferCard(id: number, data: TransferRequest) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/transfer`, data)
}

/** 抄送 */
export function ccCard(id: number, data: CcRequest) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/cc`, data)
}

/** 催办 */
export function urgeCard(id: number, message?: string) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/urge`, { message })
}

/** 执行自定义动作（M8-C：designer 配置的 autoApprove/autoReject/notify 处理器） */
export function executeCustomAction(id: number, data: CustomActionRequest) {
  return post<CardOperationResult>(`/cardflow/cards/${id}/custom-action`, data)
}

// ===== 待办 API =====

/** 获取我的待办列表 */
export function getMyTodos(params?: TodoQueryRequest) {
  return get<PagedResult<TodoItemDto>>('/cardflow/todos/mine', params)
}

/** 获取我的抄送列表 */
export function getMyCc(params?: TodoQueryRequest) {
  return get<PagedResult<TodoItemDto>>('/cardflow/todos/cc', params)
}

/** 获取待办数量 */
export function getTodoCount() {
  return get<TodoCountDto>('/cardflow/todos/count')
}

/** 获取待办统计 */
export function getTodoStats(params: TodoStatsRequest) {
  return get<TodoStatsDto>('/cardflow/todos/stats', params)
}

/** 获取条件路由与动态审批运行监控 */
export function getRuntimeMonitoring(params: CardFlowRuntimeMonitoringRequest) {
  return get<CardFlowRuntimeMonitoringDto>('/cardflow/audit/runtime-monitoring', params)
}

/** 查询全局审计日志 */
export function searchAuditLogs(params: AuditLogQueryRequest) {
  return get<PagedResult<AuditLogItemDto>>('/cardflow/audit/logs', params)
}

// ===== 委托 API =====

/** 获取我的委托列表 */
export function getMyDelegations() {
  return get<DelegationDto[]>('/cardflow/delegations')
}

/** 创建委托 */
export function createDelegation(data: CreateDelegationRequest) {
  return post<DelegationDto>('/cardflow/delegations', data)
}

/** 更新委托 */
export function updateDelegation(id: number, data: UpdateDelegationRequest) {
  return put<DelegationDto>(`/cardflow/delegations/${id}`, data)
}

/** 取消委托 */
export function cancelDelegation(id: number) {
  return del(`/cardflow/delegations/${id}`)
}

// ===== 流程组 API =====

/** 获取流程组列表 */
export function getFlowGroups(orgId: number) {
  return get<FlowGroupDto[]>('/cardflow/flow-groups', { orgId })
}

/** 创建流程组 */
export function createFlowGroup(data: CreateFlowGroupRequest) {
  return post<FlowGroupDto>('/cardflow/flow-groups', data)
}

/** 更新流程组 */
export function updateFlowGroup(id: number, data: UpdateFlowGroupRequest) {
  return put<FlowGroupDto>(`/cardflow/flow-groups/${id}`, data)
}

/** 删除流程组 */
export function deleteFlowGroup(id: number) {
  return del(`/cardflow/flow-groups/${id}`)
}

/** 获取流程组连接列表 */
export function getFlowGroupLinks(id: number) {
  return get<FlowGroupLinkDto[]>(`/cardflow/flow-groups/${id}/links`)
}

/** 保存流程组连接 */
export function saveFlowGroupLinks(id: number, links: SaveFlowGroupLinkRequest[]) {
  return put(`/cardflow/flow-groups/${id}/links`, links)
}

// ===== 通知回调 API =====

/** 处理通知回调 */
export function handleNotificationCallback(channel: string, body: string) {
  return post(`/cardflow/callback/${channel}`, body as unknown as object)
}

// ===== 批次管理 API =====

/** 上传卡片附件（multipart/form-data），返回可写入卡片 dataJson 的 CardFileValue */
export function uploadCardAttachment(file: File) {
  const formData = new FormData()
  formData.append('file', file)
  return service.post<any, CardFileValue>('/cardflow/cards/attachments', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
}

/** 上传文件创建批次（multipart/form-data） */
export function uploadBatch(data: FormData) {
  return service.post<any, CfBatch>('/cardflow/batches/upload', data, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 60_000,
  })
}

// ===== 多文件智能导入（内容路由）API =====

/** 已路由文件项（命中唯一流程，已触发导入） */
export interface AutoRoutedItem {
  fileName: string
  batchId: number
  flowDefinitionId: number
}

/** 待认领文件项（未命中任何流程，需人工指派） */
export interface AutoUnmatchedItem {
  fileName: string
  columns: string[]
}

/** 多义候选流程项 */
export interface AutoAmbiguousCandidate {
  flowDefinitionId: number
  pluginRuleId: number
}

/** 多义文件项（命中多个流程，需人工抉择） */
export interface AutoAmbiguousItem {
  fileName: string
  candidates: AutoAmbiguousCandidate[]
}

/** 读取失败文件项（损坏/非表格） */
export interface AutoReadErrorItem {
  fileName: string
  error: string
}

/** 触发失败文件项（命中流程但触发导入异常） */
export interface AutoTriggerErrorItem {
  fileName: string
  flowDefinitionId: number
  error: string
}

/** 多文件智能导入结果 */
export interface UploadAutoResult {
  routed: AutoRoutedItem[]
  unmatched: AutoUnmatchedItem[]
  ambiguous: AutoAmbiguousItem[]
  readErrors: AutoReadErrorItem[]
  triggerErrors: AutoTriggerErrorItem[]
}

/**
 * 多文件智能导入：一次上传多个文件，后端逐个按表头内容路由分类。
 * 组织上下文头由请求拦截器自动注入，无需手动传 orgId。
 */
export function uploadAutoBatch(files: File[]) {
  const formData = new FormData()
  files.forEach((file) => formData.append('files', file))
  return service.post<any, UploadAutoResult>('/cardflow/batches/upload-auto', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 120_000,
  })
}

/** 获取批次列表 */
export function getBatches(params?: CfBatchQueryRequest) {
  return get<PagedResult<CfBatch>>('/cardflow/batches', params)
}

/** 获取批次详情 */
export function getBatchDetail(id: number) {
  return get<CfBatch>(`/cardflow/batches/${id}`)
}

/** 获取批次进度 */
export function getBatchProgress(id: number) {
  return get<BatchProgressDto>(`/cardflow/batches/${id}/progress`)
}

/** 获取批次明细行 */
export function getBatchRows(id: number, params?: CfBatchRowQueryRequest) {
  return get<PagedResult<CfBatchRow>>(`/cardflow/batches/${id}/rows`, params)
}

/** 编辑暂存行（乐观锁） */
export function updateBatchRow(batchId: number, rowId: number, data: UpdateBatchRowRequest) {
  // Axios 默认 export 不包含 patch，使用 service 实例调用
  return service.patch<any, CfBatchRow>(`/cardflow/batches/${batchId}/rows/${rowId}`, data)
}

/** 排除指定行 */
export function excludeBatchRows(batchId: number, rowIds: number[]) {
  return post(`/cardflow/batches/${batchId}/rows/exclude`, { rowIds })
}

/** 恢复被排除的行 */
export function restoreBatchRows(batchId: number, rowIds: number[]) {
  return post(`/cardflow/batches/${batchId}/rows/restore`, { rowIds })
}

/** 确认提交批次 */
export function confirmBatch(batchId: number) {
  return post<{ batchId: number; cardCount: number }>(`/cardflow/batches/${batchId}/confirm`)
}

/** 撤销批次 */
export function revokeBatch(batchId: number) {
  return del(`/cardflow/batches/${batchId}`)
}

// ===== Schema 选择器数据源 API =====
// TODO: 以下接口需后端补充，当前仅在前端封装调用路径

/** 按账套查询财务科目树 */
export function getAccounts(params: { accountSetId: number; keyword?: string; onlyLeaf?: boolean }) {
  return get<Array<{ id: number; code: string; name: string; parentId?: number; isLeaf?: boolean }>>(
    '/finance/accounts',
    params
  )
}

/** 按类型查询辅助核算项 */
export function getAuxiliaryItems(params: {
  accountSetId: number
  auxType: string
  keyword?: string
}) {
  return get<Array<{ id: number; code?: string; name: string }>>('/finance/auxiliary-items', params)
}

/** 按组织查询银行账户 */
export function getBankAccounts(params: { orgId: number; keyword?: string }) {
  return get<
    Array<{
      id: number
      accountNo: string
      accountName?: string
      bankName?: string
      accountId?: number
      accountCode?: string
    }>
  >('/finance/bank-accounts', params)
}

// ===== 上传相关 API =====

/** 初始化分片上传 */
export const initChunkUpload = (data: { fileName: string; fileSize: number; totalChunks: number; fileHash?: string; targetOrgId?: number }) =>
  post('/cardflow/import/upload/init', data)

/** 上传分片 */
export const uploadChunk = (data: FormData, config?: Parameters<typeof post>[2]) =>
  post('/cardflow/import/upload/chunk', data, config)

/** 完成分片上传（返回多流程触发结果列表） */
export const completeChunkUpload = (data: { uploadId: string; fileName: string; totalChunks: number }) =>
  post<BatchTriggerResultDto[]>('/cardflow/import/upload/complete', data)

/** 多流程触发结果 DTO */
export interface BatchTriggerResultDto {
  batchId: number
  flowDefinitionId: number
  flowName: string
}

/** 流程定义候选项 DTO（匹配失败时供前端选择） */
export interface FlowDefinitionCandidateDto {
  FID: number
  FFlowName: string
  FFlowCode: string
  FDescription: string | null
}

/** 获取可选流程定义列表 */
export const getFlowDefinitionCandidates = () =>
  get<FlowDefinitionCandidateDto[]>('/cardflow/import/flow-definition-candidates')

/** 手动为批次指定流程定义 */
export const assignPipeline = (batchId: number, flowDefinitionId: number) =>
  post(`/cardflow/import/batches/${batchId}/assign-pipeline`, { flowDefinitionId })

// ===== 导入批次管理 API =====

/** 获取导入批次列表 */
export const getImportBatches = (params?: any) =>
  get('/cardflow/import/batches', params)

/** 变更感知对账：获取 since 版本后有变更的批次 */
export const fetchBatchSync = (since: number) =>
  get<{ batches: any[]; maxVersion: number }>('/cardflow/import/batch-sync', { since })

/** 获取批次详情 */
export const getImportBatchDetail = (id: number) =>
  get(`/cardflow/import/batches/${id}`)

/** 获取批次异常 */
export const getImportBatchErrors = (id: number) =>
  get(`/cardflow/import/batches/${id}/errors`)

/** 处理批次 */
export const processBatch = (id: number) =>
  post(`/cardflow/import/batches/${id}/process`, undefined, { timeout: 120000 })

/** 获取上传队列（未完成的批次） */
export const getImportBatchQueue = (params?: { batchIds?: string }) =>
  get('/cardflow/import/batches/queue', params)

/** 重试批次 */
export const retryImportBatch = (id: number) =>
  post(`/cardflow/import/batches/${id}/retry`)

/** 重新计费（部分完成批次） */
export const recalculateImportBatch = (batchId: number) =>
  post(`/cardflow/import/batches/${batchId}/recalculate`)

/** 批次删除预检查 */
export interface BatchDeletePreCheck {
  canDelete: boolean
  hasAuditedVouchers: boolean
  hasClosedPeriod: boolean
  affectedVoucherCount: number
  affectedRowCount: number
  /** 在途卡片数（draft/active/returned，撤销时被级联取消，仅知情提示不阻止） */
  activeCardCount: number
  blockReason?: string
}

export const preDeleteCheckBatch = (batchId: number) =>
  get<BatchDeletePreCheck>(`/cardflow/import/batches/${batchId}/pre-delete-check`)

/** 删除批次（支持软删除/物理删除） */
export const deleteBatch = (batchId: number, options?: { mode?: 'revoke' | 'delete'; force?: boolean }) =>
  del(`/cardflow/import/batches/${batchId}`, { params: options, timeout: 120000 })

/** 获取每日批次统计 */
export const getDailyBatchCounts = (params?: { startDate?: string; endDate?: string }) =>
  get('/cardflow/import/batches/daily-counts', params)

export const getBatchHeaders = (batchId: number) =>
  get(`/cardflow/import/batch/${batchId}/headers`)

export function getBatchColumns(batchId: number, headerRow?: number) {
  return get<{ columnNames: string[], columnIdentifier: string, headerRowNumber: number }>(`/cardflow/import/batches/${batchId}/columns`, { headerRow })
}

/** 批量撤销批次 */
export const batchRevokeBatches = (batchIds: number[]) =>
  post<{ succeeded: number[]; skipped: { id: number; reason: string }[] }>(
    '/cardflow/import/batches/batch-revoke', { batchIds }, { timeout: 120000 })

// ===== 文件类型管理 API =====

export const extractColumnsFromExcel = (data: FormData, headerRow?: number) => {
  const params = headerRow != null ? `?headerRow=${headerRow}` : ''
  return post('/cardflow/file-types/extract-columns' + params, data, { headers: { 'Content-Type': 'multipart/form-data' } })
}

// ===== 文件管理 API =====

export const getUploadedFiles = (params?: any) =>
  get('/cardflow/files', params)

export const getFileDetail = (batchId: number) =>
  get(`/cardflow/files/${batchId}`)

export const deleteFiles = (batchIds: number[]) =>
  del('/cardflow/files', { data: batchIds })

export const getStorageStats = () =>
  get('/cardflow/files/stats')

export const getCleanupPolicies = () =>
  get('/cardflow/files/cleanup-policies')

export const saveCleanupPolicy = (data: any) =>
  post('/cardflow/files/cleanup-policies', data)

export const executeCleanup = () =>
  post('/cardflow/files/cleanup')

export const previewCleanup = () =>
  get('/cardflow/files/cleanup-preview')

// ===== 暂存表管理 API =====

export const getStagingData = (sourceType: string, params?: any) =>
  get(`/cardflow/staging/${sourceType}`, params)

export const getStagingRecord = (sourceType: string, id: number) =>
  get(`/cardflow/staging/${sourceType}/${id}`)

export const updateStagingRecord = (sourceType: string, id: number, data: any) =>
  put(`/cardflow/staging/${sourceType}/${id}`, data)

export const batchDeleteStaging = (sourceType: string, ids: number[]) =>
  del(`/cardflow/staging/${sourceType}`, { data: ids })

export const batchUpdateStagingStatus = (sourceType: string, data: { ids: number[]; newStatus: number }) =>
  post(`/cardflow/staging/${sourceType}/batch-update-status`, data)

export const getStagingStats = (sourceType: string) =>
  get(`/cardflow/staging/${sourceType}/stats`)

// ===== 自动下载 API =====

export const getDownloadTasks = (params?: any) =>
  get('/cardflow/download-tasks', params)

export const getDownloadTask = (id: number) =>
  get(`/cardflow/download-tasks/${id}`)

export const createDownloadTask = (data: any) =>
  post('/cardflow/download-tasks', data)

export const updateDownloadTask = (id: number, data: any) =>
  put(`/cardflow/download-tasks/${id}`, data)

export const deleteDownloadTask = (id: number) =>
  del(`/cardflow/download-tasks/${id}`)

export const triggerDownloadTask = (id: number) =>
  post(`/cardflow/download-tasks/${id}/trigger`)

export const getDownloadLogs = (taskId: number, params?: any) =>
  get(`/cardflow/download-tasks/${taskId}/logs`, params)

// ===== 公司/经营单元 API =====

export interface ImportCompany {
  fid: number
  fName: string
  fIsBusinessUnit?: boolean
  fSortOrder?: number
}

export const getCompanies = () =>
  get('/cardflow/companies')

// ===== 首页统计 API =====

export const getDataCenterHomeStats = () =>
  get('/cardflow/home/stats')

// ===== 导入总览 API =====

export interface ImportOverviewDto {
  todayBatchCount: number
  todayTotalRows: number
  successRate: number
  pendingExceptionCount: number
  processingTaskCount: number
  dailyTrend: Array<{
    date: string
    importCount: number
    errorCount: number
  }>
}

export const getImportOverview = () =>
  get<ImportOverviewDto>('/cardflow/import/overview')

// ===== 异常数据 API =====

export interface ImportErrorDto {
  id: number
  batchId: number
  batchNo: string | null
  fileName: string | null
  businessDispatchRecordId: number | null
  rowNumber: number
  errorType: string
  severityLevel: string
  errorField: string | null
  errorMessage: string | null
  suggestedFix: string | null
  originalValue: string | null
  qualityDimension: string | null
  dispatchStatus: string | null
  dispatchType: string | null
  createTime: string
}

export const getImportErrors = (params?: {
  page?: number; pageSize?: number; batchId?: number;
  errorType?: string; severityLevel?: string; dispatchStatus?: string;
  startDate?: string; endDate?: string;
}) => get<{ items: ImportErrorDto[]; total: number }>('/cardflow/import/errors', params)

export const batchIgnoreErrors = (errorIds: number[]) =>
  put('/cardflow/import/errors/ignore', { errorIds })

export const createDispatch = (data: {
  errorIds: number[]; dispatchType: string;
  assignee: string; assigneeName: string;
  description: string; deadline?: string;
}) => post('/cardflow/import/dispatch', data)

// ===== 派发记录 API =====

export interface BusinessDispatchRecordDto {
  id: number
  batchId: number
  batchNo: string | null
  errorId: number | null
  dispatchType: string
  targetType: string | null
  targetId: number | null
  assignee: string | null
  assigneeName: string | null
  status: string
  exceptionType: string | null
  severityLevel: string | null
  description: string | null
  result: string | null
  deadline: string | null
  completedTime: string | null
  operator: string | null
  createTime: string
  updateTime: string | null
}

export const getBusinessDispatchRecords = (params: {
  page?: number; pageSize?: number; status?: string; batchId?: number;
}) => get<{ items: BusinessDispatchRecordDto[]; total: number }>('/cardflow/import/dispatch', params)

export const updateDispatchStatus = (id: number, data: { status: string; result?: string }) =>
  put(`/cardflow/import/dispatch/${id}`, data)

// ===== 暂存表发现 API =====

export const getStagingTables = (prefix?: string) =>
  get<StagingTableInfo[]>('/cardflow/pipeline/staging-tables', prefix ? { prefix } : undefined)

export const getStagingTableColumns = (tableName: string) =>
  get<StagingColumnInfo[]>(`/cardflow/pipeline/staging-tables/${tableName}/columns`)

export interface StagingTableInfo {
  tableName: string
  columns: StagingColumnInfo[]
}

export interface StagingColumnInfo {
  columnName: string
  dataType: string
  isNullable: boolean
  maxLength: number | null
}

// ===== 凭证生成记录 API =====

export function getVoucherGenerations(params?: { batchId?: number }) {
  return get('/cardflow/voucher-generations', params)
}

export function getVoucherGeneration(id: number) {
  return get(`/cardflow/voucher-generations/${id}`)
}

export function retryVoucherGeneration(id: number) {
  return post(`/cardflow/voucher-generations/${id}/retry`)
}

// ===== 管道配置 API =====

export interface PipelineDto {
  id: number
  name: string
  description: string
  bizTag: string
  tagColor: string | null
  enableSubBatchParallel: boolean
  status: number
  orgId: number
  autoPluginCount: number
  createTime: string
  updateTime: string | null
}

export interface PipelineAutoPluginDto {
  id: number
  pipelineId: number
  pluginName: string
  pluginType: string
  pluginImplType: string
  sortOrder: number
  sourceType: string | null
  targetType: string | null
  targetTable: string | null
  sourceTable: string | null
  configJson: string | null
  enableRollback: boolean
  status: number
  ruleId: number | null
  ruleName: string | null
  createTime: string
  updateTime: string | null
}

export interface PipelineDetailDto extends PipelineDto {
  plugins: PipelineAutoPluginDto[]
}

export const getPipelines = () =>
  get<PipelineDto[]>('/cardflow/pipeline')

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const getPipeline = (id: number) =>
  get<PipelineDetailDto>(`/cardflow/pipeline/${id}`)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const createPipeline = (data: any) =>
  post('/cardflow/pipeline', data)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const updatePipeline = (id: number, data: any) =>
  put(`/cardflow/pipeline/${id}`, data)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const deletePipeline = (id: number) =>
  del(`/cardflow/pipeline/${id}`)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const getPipelineAutoPlugins = (pipelineId: number) =>
  get<PipelineAutoPluginDto[]>(`/cardflow/pipeline/${pipelineId}/auto-plugins`)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const createPipelineAutoPlugin = (pipelineId: number, data: any) =>
  post(`/cardflow/pipeline/${pipelineId}/auto-plugins`, data)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const updatePipelineAutoPlugin = (autoPluginId: number, data: any) =>
  put(`/cardflow/pipeline/auto-plugins/${autoPluginId}`, data)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const deletePipelineAutoPlugin = (autoPluginId: number) =>
  del(`/cardflow/pipeline/auto-plugins/${autoPluginId}`)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const reorderPipelineAutoPlugins = (pipelineId: number, items: { autoPluginId: number; sortOrder: number }[]) =>
  post(`/cardflow/pipeline/${pipelineId}/auto-plugins/reorder`, items)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const clonePipeline = (id: number) =>
  post(`/cardflow/pipeline/${id}/clone`)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const getPipelinesForOrg = (orgId: number) =>
  get<PipelineDto[]>('/cardflow/pipelines', { orgId })

/** 获取指定组织的规则列表 */
export const getRulesForOrg = (orgId: number, typeCode?: string) =>
  get<AutoPluginRuleDto[]>('/cardflow/auto-plugin-rules', { orgId, typeCode })

/** 复制规则到当前组织 */
export const copyRule = (ruleId: number) =>
  post(`/cardflow/auto-plugin-rules/${ruleId}/copy`)

// ===== 批次 AutoPlugin 轨迹与回撤 API =====

export interface AutoPluginTrailItemDto {
  pluginName: string
  pluginType: string
  pluginImplType: string
  sortIndex: number
  supportsRollback: boolean
  status: string
  hasSnapshot: boolean
  snapshotTime: string | null
  currentStepIndex?: number
  totalSteps?: number
  currentStepName?: string
  currentStepStatus?: string
}

export interface AutoPluginTrailDto {
  batchId: number
  flowName: string
  currentPluginIndex: number
  batchStatus: string
  autoPlugins: AutoPluginTrailItemDto[]
}

export interface BatchSnapshotDto {
  id: number
  pluginIndex: number
  pluginName: string
  snapshotType: string
  createTime: string
}

export interface RollbackResult {
  success: boolean
  rolledBackPlugins: string[]
  currentPluginIndex: number
  message: string | null
}

export const getBatchAutoPluginTrail = (batchId: number) =>
  get<AutoPluginTrailDto>(`/cardflow/pipeline/${batchId}/auto-plugin-trail`)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const getBatchSnapshots = (batchId: number) =>
  get<BatchSnapshotDto[]>(`/cardflow/pipeline/${batchId}/snapshots`)

/** @deprecated 后端无此端点（CfPipelineController 仅保留只读查询），调用即 404；孤儿组件引用待清理 */
export const rollbackBatch = (batchId: number, targetPluginIndex: number) =>
  post<RollbackResult>(`/cardflow/pipeline/${batchId}/rollback`, { targetPluginIndex })

// ===== AutoPlugin 类型元数据 API =====

export interface SelectOption {
  label: string
  value: string
}

export interface ConfigFieldSchema {
  key: string
  label: string
  fieldType: string
  required: boolean
  defaultValue?: any
  placeholder?: string
  description?: string
  group?: string
  options?: SelectOption[]
  component?: string
  extra?: string
}

export interface AutoPluginMetadata {
  implementationType: string
  displayName: string
  pluginType: string
  description: string
  configSchema: ConfigFieldSchema[]
}

/** 获取所有 Agent 类型元数据 */
export function getAutoPluginTypes(config?: any) {
  return get<AutoPluginMetadata[]>('/cardflow/pipeline/auto-plugin-types', undefined, config)
}

// ===== 自动插件规则管理 =====

export interface AutoPluginRuleDto {
  id: number
  typeCode: string
  ruleName: string
  configJson: string | null
  status: number
  orgId: number
  description: string | null
  referenceCount: number
  /** 乐观锁戳（后端 FConcurrencyStamp），用于更新时的并发检查与回写 */
  concurrencyStamp: string
  createTime: string
  updateTime: string | null
}

export interface AutoPluginRuleSelectDto {
  id: number
  ruleName: string
  orgId: number
}

/** 查询规则列表 */
export const getAutoPluginRules = (typeCode?: string) =>
  get<AutoPluginRuleDto[]>('/cardflow/auto-plugin-rules', { typeCode })

/** 查询规则列表（静默模式） */
export const getAutoPluginRulesSilent = (typeCode?: string) =>
  get<AutoPluginRuleDto[]>('/cardflow/auto-plugin-rules', { typeCode }, { silent: true } as any)

/** 获取规则详情 */
export const getAutoPluginRule = (id: number) =>
  get<AutoPluginRuleDto>(`/cardflow/auto-plugin-rules/${id}`)

/** 获取规则详情（静默模式） */
export const getAutoPluginRuleSilent = (id: number) =>
  get<AutoPluginRuleDto>(`/cardflow/auto-plugin-rules/${id}`, undefined, { silent: true } as any)

/** 创建规则 */
export const createAutoPluginRule = (data: { typeCode: string; ruleName: string; configJson?: string; description?: string }) =>
  post('/cardflow/auto-plugin-rules', data)

/** 更新规则 */
export const updateAutoPluginRule = (id: number, data: { typeCode?: string; ruleName: string; configJson?: string; description?: string; status?: number; concurrencyStamp?: string }) =>
  put(`/cardflow/auto-plugin-rules/${id}`, data)

/** 删除规则 */
export const deleteAutoPluginRule = (id: number) =>
  del(`/cardflow/auto-plugin-rules/${id}`)

/** 按类型获取启用的规则 */
export const getAutoPluginRulesByType = (typeCode: string) =>
  get<AutoPluginRuleSelectDto[]>(`/cardflow/auto-plugin-rules/by-type/${typeCode}`)

/** DryRun 预演请求参数 */
export interface DryRunRequest {
  batchId: number
  configJson?: string
  groupField?: string
}

/** DryRun 预演（新规则，无 id） */
export const dryRunAutoPluginRuleNew = (data: DryRunRequest) =>
  post<DryRunResult>('/cardflow/auto-plugin-rules/dry-run', data)

/** DryRun 预演（已保存规则） */
export const dryRunAutoPluginRule = (id: number, data: DryRunRequest) =>
  post<DryRunResult>(`/cardflow/auto-plugin-rules/${id}/dry-run`, data)

export interface DryRunResult {
  totalRows: number
  matchedRows: number
  unmatchedRows: number
  estimatedVouchers: number
  groupDetails: Array<{ groupIndex: number; lineNo: number; matched: boolean }>
  unmatchedDetails: Array<Record<string, any>>
  hasMoreUnmatched: boolean
  groupedSummary: Array<{ fieldValue: string; count: number; totalAmount: number | null }>
}

// ===== 回收站 API =====

export interface RecycledBatchItem {
  id: number
  batchNo: string
  fileName: string
  fileSize: number
  totalRows: number
  status: number
  revokedTime: string
  revokedById: number
  createTime: string
}

/** 获取回收站批次列表 */
export const getRecycledBatches = () =>
  get<RecycledBatchItem[]>('/cardflow/import/batches/recycled')

/** 恢复批次 */
export const restoreBatch = (batchId: number) =>
  post(`/cardflow/import/batches/${batchId}/restore`)

/** 清空回收站 */
export const clearRecycleBin = () =>
  post<{ deletedCount: number; failedCount: number }>(
    '/cardflow/import/batches/recycled/clear', undefined, { timeout: 120000 })

// ===== 组织绑定预检 API =====

export interface OrgBindingPreviewResult {
  targetOrgId: number
  targetOrgName: string
  resolvedAccountSetId: number | null
  resolvedAccountSetName: string | null
  warnings: string[]
}

/** 上传前预检：校验目标组织与账套绑定 */
export const previewOrgBinding = (targetOrgId: number) =>
  post<OrgBindingPreviewResult>('/cardflow/import/preview-org-binding', { targetOrgId })

// ==================== M6 发布与版本 ====================
import type { InflightSummaryDto, PublishFlowDefinitionResultDto } from '@/types/cardflow'

/** 发布（带在途卡片策略：keepOld 默认 / migrate 迁移被删节点在途卡片） */
export function publishFlowDefinitionWithPolicy(id: number, inflightPolicy: 'keepOld' | 'migrate') {
  return post<PublishFlowDefinitionResultDto>(`/cardflow/definitions/${id}/publish`, { inflightPolicy })
}

/** 在途卡片计数（按版本分组） */
export function getInflightSummary(id: number) {
  return get<InflightSummaryDto>(`/cardflow/definitions/${id}/inflight-summary`)
}

/** 回滚：以指定历史版本快照新建草稿（不绕发布门禁） */
export function createDraftFromVersion(id: number, versionId: number) {
  return post<FlowVersionDetailDto>(`/cardflow/definitions/${id}/versions/${versionId}/create-draft`)
}

// ── M5-1 样例卡片（append-only，避免与主会话并发编辑冲突） ──
import type { SampleCardDto } from '@/types/cardflow'

/** 样例卡片：取该 definition 近 30 天卡片采样（≤20 张）供干跑代入，敏感字段脱敏（路由引用字段保留原值） */
export function getSampleCards(id: number, keyword?: string) {
  return get<SampleCardDto[]>(`/cardflow/definitions/${id}/sample-cards`, keyword ? { keyword } : undefined)
}

// ── M5-3 凭证试算（append-only，避免与主会话并发编辑冲突） ──
import type { VoucherPreviewDto, VoucherPreviewRequest } from '@/types/cardflow'

/** 凭证试算：自动凭证节点 + 样例卡片数据 → 借贷分录预览（真试算需运行时上下文时 success=false 降级） */
export function previewVoucher(id: number, data: VoucherPreviewRequest) {
  return post<VoucherPreviewDto>(`/cardflow/definitions/${id}/draft-version/preview-voucher`, data)
}

// ── M7-1 编辑锁 + 接管协议（append-only，设计 E7） ──
import type { LockStateDto } from '@/types/cardflow'

/** 获取编辑锁（进入编辑页时调用）。无锁/死锁→抢占成 holder；他人活锁→返回 holder 信息 */
export function acquireEditLock(id: number) {
  return post<LockStateDto>(`/cardflow/definitions/${id}/lock/acquire`)
}

/** 持锁端心跳续期（30s 一次），顺带回传是否有待响应接管请求 */
export function heartbeatEditLock(id: number) {
  return post<LockStateDto>(`/cardflow/definitions/${id}/lock/heartbeat`)
}

/** 只读端申请接管（全局唯一；已有请求处理中会 Fail） */
export function requestEditLockTakeover(id: number) {
  return post<LockStateDto>(`/cardflow/definitions/${id}/lock/takeover-request`)
}

/** 持锁端响应接管请求（accept=true 原子移交 / false 拒绝） */
export function respondEditLockTakeover(id: number, accept: boolean) {
  return post<LockStateDto>(`/cardflow/definitions/${id}/lock/takeover-respond`, { accept })
}
