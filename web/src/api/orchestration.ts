// 卡片流程编排中心 API
import { get, post, put, del } from './request'
import type {
  OrchestrationTemplate,
  OrchestrationTemplateQueryRequest,
  CreateTemplateRequest,
  UpdateTemplateRequest,
  OrchestrationInstance,
  OrchestrationInstanceDetail,
  OrchestrationInstanceQueryRequest,
  StartInstanceRequest,
  StartInstanceResponse,
  DispatchRecord,
  DispatchOption,
  AdHocDispatchRequest,
  AdHocDispatchResponse,
  PagedResult,
} from '@/types/orchestration'

// ===== 编排模板 API =====

/** 获取编排模板分页列表 */
export function getOrchestrationTemplates(params?: OrchestrationTemplateQueryRequest) {
  return get<PagedResult<OrchestrationTemplate>>('/orchestration/templates', params)
}

/** 获取编排模板详情 */
export function getOrchestrationTemplate(id: number) {
  return get<OrchestrationTemplate>(`/orchestration/templates/${id}`)
}

/** 创建编排模板 */
export function createOrchestrationTemplate(data: CreateTemplateRequest) {
  return post<OrchestrationTemplate>('/orchestration/templates', data)
}

/** 更新编排模板 */
export function updateOrchestrationTemplate(id: number, data: UpdateTemplateRequest) {
  return put<OrchestrationTemplate>(`/orchestration/templates/${id}`, data)
}

/** 发布编排模板（draft → published） */
export function publishOrchestrationTemplate(id: number) {
  return post(`/orchestration/templates/${id}/publish`)
}

/** 停用编排模板（published → disabled） */
export function disableOrchestrationTemplate(id: number) {
  return post(`/orchestration/templates/${id}/disable`)
}

// ===== 编排实例 API =====

/** 启动编排实例 */
export function startOrchestrationInstance(data: StartInstanceRequest) {
  return post<StartInstanceResponse>('/orchestration/instances', data)
}

/** 获取编排实例分页列表 */
export function getOrchestrationInstances(params?: OrchestrationInstanceQueryRequest) {
  return get<PagedResult<OrchestrationInstance>>('/orchestration/instances', params)
}

/** 获取编排实例详情（含节点实例数组） */
export function getOrchestrationInstance(id: number) {
  return get<OrchestrationInstanceDetail>(`/orchestration/instances/${id}`)
}

/** 获取编排实例的派发记录 */
export function getOrchestrationInstanceDispatches(id: number) {
  return get<DispatchRecord[]>(`/orchestration/instances/${id}/dispatches`)
}

/** 暂停编排实例 */
export function pauseOrchestrationInstance(id: number) {
  return post(`/orchestration/instances/${id}/pause`)
}

/** 恢复编排实例 */
export function resumeOrchestrationInstance(id: number) {
  return post(`/orchestration/instances/${id}/resume`)
}

/** 取消编排实例 */
export function cancelOrchestrationInstance(id: number) {
  return del(`/orchestration/instances/${id}`)
}

// ===== 自由派发 API =====

/** 获取卡片可派发的目标流程列表 */
export function getAdHocDispatchOptions(cardId: number) {
  return get<DispatchOption[]>('/adhoc-dispatch/available', { cardId })
}

/** 执行自由派发 */
export function adHocDispatch(data: AdHocDispatchRequest) {
  return post<AdHocDispatchResponse>('/adhoc-dispatch', data)
}
