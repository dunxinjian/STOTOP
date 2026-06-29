import { get, post } from '@/api/request'
import type { TodoCard } from '../types'

/** 获取待办列表 */
export function getTodos(params?: { status?: string; page?: number; pageSize?: number }) {
  return get<{ items: TodoCard[]; total: number }>('/cardflow/todos', params)
}

/** 获取卡片详情 */
export function getCardDetail(id: number) {
  return get(`/cardflow/cards/${id}`)
}

/** 审批通过 */
export function approveCard(id: number, comment: string) {
  return post(`/cardflow/cards/${id}/approve`, { comment })
}

/** 退回 */
export function rejectCard(id: number, comment: string) {
  return post(`/cardflow/cards/${id}/reject`, { comment })
}

/** 加签 */
export function signCard(id: number, data: { userId: number; comment: string }) {
  return post(`/cardflow/cards/${id}/sign`, data)
}

/** 已处理列表 */
export function getHistory(params: { page: number; pageSize: number; type?: string; days?: number }) {
  return get<{ items: any[]; total: number }>('/cardflow/todos/history', params)
}
