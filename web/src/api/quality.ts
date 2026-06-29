import { get, post, put, del } from './request'

// ====== 质量看板 ======
export function getQualityDashboardStats(params?: { orgId?: number; startDate?: string; endDate?: string }) {
  return get('/quality/dashboard/stats', params)
}

export function getRecentExceptions(params?: { count?: number }) {
  return get('/quality/dashboard/recent', params)
}

export function getDashboardTrend(params?: { days?: number }) {
  return get('/quality/dashboard/trend', params)
}

export function getTypeDist() {
  return get('/quality/dashboard/type-dist')
}

export function getPriorityDist() {
  return get('/quality/dashboard/priority-dist')
}

// ====== 异常管理 ======
export function getExceptions(params?: any) {
  return get('/quality/exceptions', params)
}

export function getExceptionDetail(id: number) {
  return get(`/quality/exceptions/${id}`)
}

export function dispatchException(id: number, data: any) {
  return post(`/quality/exceptions/${id}/dispatch`, data)
}

export function closeException(id: number, data: any) {
  return post(`/quality/exceptions/${id}/close`, data)
}

export function reassignException(id: number, data: any) {
  return post(`/quality/exceptions/${id}/reassign`, data)
}

export function createException(data: any) {
  return post('/quality/exceptions', data)
}

export function updateException(id: number, data: any) {
  return put(`/quality/exceptions/${id}`, data)
}

export function deleteException(id: number) {
  return del(`/quality/exceptions/${id}`)
}

export function getExceptionCountByStatus() {
  return get('/quality/exceptions/count-by-status', undefined, { silent: true } as any)
}

// ====== 规则管理 ======
export function getQualityRules(params?: any) {
  return get('/quality/rules', params)
}

export function getQualityRuleDetail(id: number) {
  return get(`/quality/rules/${id}`)
}

export function createQualityRule(data: any) {
  return post('/quality/rules', data)
}

export function updateQualityRule(id: number, data: any) {
  return put(`/quality/rules/${id}`, data)
}

export function deleteQualityRule(id: number) {
  return del(`/quality/rules/${id}`)
}

export function toggleQualityRule(id: number) {
  return post(`/quality/rules/${id}/toggle`)
}

// ====== 复盘管理 ======
export function getReviews(params?: any) {
  return get('/quality/reviews', params)
}

export function createReview(data: any) {
  return post('/quality/reviews', data)
}

export function updateReview(id: number, data: any) {
  return put(`/quality/reviews/${id}`, data)
}

export function getImprovements(reviewId: number) {
  return get(`/quality/reviews/${reviewId}/improvements`)
}

export function getReviewDetail(id: number) {
  return get(`/quality/reviews/${id}`)
}

export function deleteReview(id: number) {
  return del(`/quality/reviews/${id}`)
}

export function getReviewStats() {
  return get('/quality/reviews/stats')
}

export function getImprovementList(params?: any) {
  return get('/quality/improvements', params)
}

export function updateImprovement(id: number, data: any) {
  return put(`/quality/improvements/${id}`, data)
}

export function completeImprovement(id: number, data: any) {
  return post(`/quality/improvements/${id}/complete`, data)
}

// ====== 知识库 ======
export function getKnowledgeArticles(params?: any) {
  return get('/quality/knowledge', params)
}

export function createKnowledgeArticle(data: any) {
  return post('/quality/knowledge', data)
}

export function updateKnowledgeArticle(id: number, data: any) {
  return put(`/quality/knowledge/${id}`, data)
}

export function getKnowledgeDetail(id: number) {
  return get(`/quality/knowledge/${id}`)
}

export function deleteKnowledgeArticle(id: number) {
  return del(`/quality/knowledge/${id}`)
}

export function getKnowledgeCategories() {
  return get('/quality/knowledge/categories')
}

export function getKnowledgeTags() {
  return get('/quality/knowledge/tags')
}

export function getKnowledgeStats() {
  return get('/quality/knowledge/stats')
}

// ====== 绩效 ======
export function getPerformanceRecords(params?: any) {
  return get('/quality/performance', params)
}

export function getPerformanceStats(params?: any) {
  return get('/quality/performance/stats', params)
}

export function getMyPerformance(params?: any) {
  return get('/quality/performance/my', params)
}

export function getPerformanceRanking(params?: any) {
  return get('/quality/performance/ranking', params)
}

export function getPerformanceTrend(params?: any) {
  return get('/quality/performance/trend', params)
}

// ====== 异常分析 ======
export function getAnalysisTrend(params?: any) {
  return get('/quality/dashboard/analysis/trend', params)
}

export function getAnalysisEfficiency(params?: any) {
  return get('/quality/dashboard/analysis/efficiency', params)
}

export function getAnalysisSource(params?: any) {
  return get('/quality/dashboard/analysis/source', params)
}

export function getAnalysisHandlerStats(params?: any) {
  return get('/quality/dashboard/analysis/handler-stats', params)
}

// ====== 预警配置 ======
export function getAlertConfigs(params?: any) {
  return get('/quality/alert-configs', params)
}

export function getAlertConfigDetail(id: number) {
  return get(`/quality/alert-configs/${id}`)
}

export function createAlertConfig(data: any) {
  return post('/quality/alert-configs', data)
}

export function updateAlertConfig(id: number, data: any) {
  return put(`/quality/alert-configs/${id}`, data)
}

export function deleteAlertConfig(id: number) {
  return del(`/quality/alert-configs/${id}`)
}

export function toggleAlertConfig(id: number) {
  return post(`/quality/alert-configs/${id}/toggle`)
}
