import { get, post, put } from './request'
import type {
  AddFeedbackCommentRequest,
  AssignFeedbackRequest,
  CreateFeedbackRequest,
  FeedbackActivityDto,
  FeedbackCardDto,
  FeedbackDetailDto,
  FeedbackQueryRequest,
  FeedbackStatusCountDto,
  TransitionFeedbackRequest,
  UpdateFeedbackRequest,
} from '@/types/feedback'

export function getFeedbackList(params?: FeedbackQueryRequest) {
  return get<{ items: FeedbackCardDto[]; total: number; pageIndex: number; pageSize: number }>('/system/feedback', params)
}

export function getFeedbackBoard(params?: FeedbackQueryRequest) {
  return get<FeedbackCardDto[]>('/system/feedback/board', params)
}

export function getFeedbackCounts(params?: FeedbackQueryRequest) {
  return get<FeedbackStatusCountDto[]>('/system/feedback/counts', params)
}

export function getFeedbackDetail(id: number) {
  return get<FeedbackDetailDto>(`/system/feedback/${id}`)
}

export function createFeedback(data: CreateFeedbackRequest) {
  return post<FeedbackDetailDto>('/system/feedback', data)
}

export function updateFeedback(id: number, data: UpdateFeedbackRequest) {
  return put<FeedbackDetailDto>(`/system/feedback/${id}`, data)
}

export function assignFeedback(id: number, data: AssignFeedbackRequest) {
  return put<FeedbackDetailDto>(`/system/feedback/${id}/assign`, data)
}

export function transitionFeedback(id: number, data: TransitionFeedbackRequest) {
  return put<FeedbackDetailDto>(`/system/feedback/${id}/transition`, data)
}

export function addFeedbackComment(id: number, data: AddFeedbackCommentRequest) {
  return post<FeedbackActivityDto>(`/system/feedback/${id}/comments`, data)
}
