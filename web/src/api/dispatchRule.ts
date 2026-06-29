import { get, post, put, del } from './request'

// ==================== 派发规则 API ====================

export const getDispatchRules = (params?: { status?: number; handlerType?: string; targetTable?: string }) =>
  get('/cardflow/dispatch-rules', params)

export const getDispatchRule = (id: number) =>
  get(`/cardflow/dispatch-rules/${id}`)

export const createDispatchRule = (data: any) =>
  post('/cardflow/dispatch-rules', data)

export const updateDispatchRule = (id: number, data: any) =>
  put(`/cardflow/dispatch-rules/${id}`, data)

export const deleteDispatchRule = (id: number) =>
  del(`/cardflow/dispatch-rules/${id}`)

export const getDispatchHandlerTypes = () =>
  get<string[]>('/cardflow/dispatch-rules/handler-types')

export const getEntryRulesByDispatchRule = (id: number) =>
  get(`/cardflow/dispatch-rules/${id}/entry-rules`)

export const testDispatchRule = (id: number) =>
  post(`/cardflow/dispatch-rules/${id}/test`)
