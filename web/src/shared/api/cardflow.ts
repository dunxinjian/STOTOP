import { get, post } from '@/api/request'
import type { TodoCard } from '../types'

// ===== 后端线格式 DTO（对应 CardFlow TodoItemDto / PagedResult，camelCase）=====

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

interface PagedResultDto<T> {
  items: T[]
  total: number
  pageIndex: number
  pageSize: number
}

// id 取 cardId：详情路由 /m/card/:id 按卡片 ID 取数，待办项 ID 无详情端点
function toTodoCard(item: TodoItemDto): TodoCard {
  return {
    id: item.cardId,
    title: item.title || item.cardNumber || '未命名',
    flowName: item.flowName,
    applicant: item.initiatorName,
    createdAt: item.createdTime,
    status: item.status === 'pending' ? 'pending' : 'completed',
  }
}

/** 获取待办列表 */
export async function getTodos(params?: { status?: string; page?: number; pageSize?: number }) {
  const res = await get<PagedResultDto<TodoItemDto>>('/cardflow/todos/mine', params)
  return { items: (res?.items || []).map(toTodoCard), total: res?.total || 0 }
}

/** 获取卡片详情 */
export function getCardDetail(id: number) {
  return get(`/cardflow/cards/${id}`)
}

/** 审批通过 */
export function approveCard(id: number, opinion: string) {
  return post(`/cardflow/cards/${id}/approve`, { opinion })
}

/** 退回 */
export function rejectCard(id: number, opinion: string) {
  return post(`/cardflow/cards/${id}/reject`, { opinion })
}

/** 加签 */
export function signCard(
  id: number,
  data: { userId: number; insertMode?: 'before' | 'after'; opinion?: string | null }
) {
  return post(`/cardflow/cards/${id}/countersign`, data)
}

/** 已处理列表（后端无独立 history 端点，读本人已完成待办） */
export async function getHistory(params: { page: number; pageSize: number }) {
  const res = await get<PagedResultDto<TodoItemDto>>('/cardflow/todos/mine', {
    ...params,
    status: 'completed',
  })
  return { items: res?.items || [], total: res?.total || 0 }
}
