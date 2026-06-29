// 目标与任务管理模块 API
import { get, post, put, del } from './request'
import type {
  GoalListDto, GoalDetailDto, GoalTreeDto,
  CreateGoalRequest, UpdateGoalRequest, DecomposeGoalRequest, GoalTreeQueryRequest,
  KeyResultListDto, CreateKeyResultRequest, UpdateKeyResultRequest, UpdateKeyResultProgressRequest,
  ProjectListDto, ProjectDetailDto, ProjectMemberDto,
  CreateProjectRequest, UpdateProjectRequest, AddProjectMemberRequest, ProjectPagedRequest,
  TaskListDto, TaskDetailDto, TaskDependencyDto, TagSimpleDto,
  CreateTaskRequest, UpdateTaskRequest, ChangeTaskStatusRequest, AssignTaskRequest,
  SetTaskTagsRequest, AddTaskDependencyRequest, SetTaskVisibilityRequest, TaskPagedRequest,
  TaskCommentListDto, ReactionSummaryDto,
  CreateTaskCommentRequest, UpdateTaskCommentRequest, ToggleReactionRequest, CommentPagedRequest,
  AttachmentListDto, UploadAttachmentRequest,
  ProgressReportListDto, CreateProgressReportRequest, ProgressReportPagedRequest,
  TaskScheduleListDto, CreateTaskScheduleRequest, UpdateTaskScheduleRequest, SchedulePagedRequest,
  TaskReminderListDto, CreateTaskReminderRequest,
  PerformancePeriodListDto, CreatePerformancePeriodRequest, UpdatePerformancePeriodRequest, PerformancePeriodPagedRequest,
  PerformanceRecordListDto, PerformanceRecordDetailDto,
  SelfEvaluateRequest, SuperiorReviewRequest, PerformanceDashboardDto,
  PerformanceDimensionListDto, CreatePerformanceDimensionRequest, UpdatePerformanceDimensionRequest,
  TagListDto, CreateTagRequest, UpdateTagRequest,
  NotificationListDto, UnreadCountDto, NotificationPagedRequest,
  ReviewListDto, ReviewDetailDto, CreateReviewRequest, UpdateReviewRequest, ExtractKnowledgeRequest, ReviewPagedRequest,
  KnowledgeListDto, KnowledgeDetailDto, KnowledgeCommentDto,
  CreateKnowledgeRequest, UpdateKnowledgeRequest, CreateKnowledgeCommentRequest, KnowledgePagedRequest,
  KanbanDataDto, KanbanQueryRequest, KanbanMoveRequest,
} from '@/types/task'

// ==================== 目标管理 ====================

// 获取目标树
export function getGoalTree(params?: GoalTreeQueryRequest) {
  return get<GoalTreeDto[]>('/task/goals/tree', params)
}

// 获取目标详情
export function getGoal(id: number) {
  return get<GoalDetailDto>(`/task/goals/${id}`)
}

// 创建目标
export function createGoal(data: CreateGoalRequest) {
  return post<GoalDetailDto>('/task/goals', data)
}

// 更新目标
export function updateGoal(id: number, data: UpdateGoalRequest) {
  return put<GoalDetailDto>(`/task/goals/${id}`, data)
}

// 分解目标到下级
export function decomposeGoal(id: number, data: DecomposeGoalRequest) {
  return post<GoalDetailDto>(`/task/goals/${id}/decompose`, data)
}

// 获取子目标
export function getGoalChildren(id: number) {
  return get<GoalListDto[]>(`/task/goals/${id}/children`)
}

// 获取目标关联的任务
export function getGoalTasks(id: number) {
  return get<TaskListDto[]>(`/task/goals/${id}/tasks`)
}

// ==================== 关键成果 ====================

// 获取目标下的KR列表
export function getKeyResults(goalId: number) {
  return get<KeyResultListDto[]>(`/task/goals/${goalId}/key-results`)
}

// 创建KR
export function createKeyResult(goalId: number, data: CreateKeyResultRequest) {
  return post<KeyResultListDto>(`/task/goals/${goalId}/key-results`, data)
}

// 更新KR
export function updateKeyResult(id: number, data: UpdateKeyResultRequest) {
  return put<KeyResultListDto>(`/task/key-results/${id}`, data)
}

// 更新KR进度
export function updateKeyResultProgress(id: number, data: UpdateKeyResultProgressRequest) {
  return put<KeyResultListDto>(`/task/key-results/${id}/progress`, data)
}

// 删除KR
export function deleteKeyResult(id: number) {
  return del<boolean>(`/task/key-results/${id}`)
}

// ==================== 项目管理 ====================

// 项目列表
export function getProjects(params?: ProjectPagedRequest) {
  return get<{ items: ProjectListDto[]; total: number }>('/task/projects', params)
}

// 项目详情
export function getProject(id: number) {
  return get<ProjectDetailDto>(`/task/projects/${id}`)
}

// 创建项目
export function createProject(data: CreateProjectRequest) {
  return post<ProjectDetailDto>('/task/projects', data)
}

// 更新项目
export function updateProject(id: number, data: UpdateProjectRequest) {
  return put<ProjectDetailDto>(`/task/projects/${id}`, data)
}

// 项目下的任务列表
export function getProjectTasks(id: number, params?: TaskPagedRequest) {
  return get<{ items: TaskListDto[]; total: number }>(`/task/projects/${id}/tasks`, params)
}

// 项目看板视图
export function getProjectKanban(id: number, params?: KanbanQueryRequest) {
  return get<KanbanDataDto>(`/task/projects/${id}/kanban`, params)
}

// 获取项目成员列表
export function getProjectMembers(id: number) {
  return get<ProjectMemberDto[]>(`/task/projects/${id}/members`)
}

// 添加项目成员
export function addProjectMember(id: number, data: AddProjectMemberRequest) {
  return post<ProjectMemberDto>(`/task/projects/${id}/members`, data)
}

// 移除项目成员
export function removeProjectMember(id: number, userId: number) {
  return del<boolean>(`/task/projects/${id}/members/${userId}`)
}

// ==================== 任务管理 ====================

// 任务列表
export function getTasks(params?: TaskPagedRequest) {
  return get<{ items: TaskListDto[]; total: number }>('/task/tasks', params)
}

// 任务详情
export function getTask(id: number) {
  return get<TaskDetailDto>(`/task/tasks/${id}`)
}

// 创建任务
export function createTask(data: CreateTaskRequest) {
  return post<TaskDetailDto>('/task/tasks', data)
}

// 更新任务
export function updateTask(id: number, data: UpdateTaskRequest) {
  return put<TaskDetailDto>(`/task/tasks/${id}`, data)
}

// 变更任务状态
export function changeTaskStatus(id: number, data: ChangeTaskStatusRequest) {
  return put<TaskDetailDto>(`/task/tasks/${id}/status`, data)
}

// 设置优先级
export function setTaskPriority(id: number, priority: number) {
  return put<TaskDetailDto>(`/task/tasks/${id}/priority`, priority as any)
}

// 分配执行人
export function assignTask(id: number, data: AssignTaskRequest) {
  return put<TaskDetailDto>(`/task/tasks/${id}/assign`, data)
}

// 创建子任务
export function createSubtask(id: number, data: CreateTaskRequest) {
  return post<TaskDetailDto>(`/task/tasks/${id}/subtasks`, data)
}

// 获取子任务列表
export function getSubtasks(id: number) {
  return get<TaskListDto[]>(`/task/tasks/${id}/subtasks`)
}

// 设置可见范围
export function setTaskVisibility(id: number, data: SetTaskVisibilityRequest) {
  return put<boolean>(`/task/tasks/${id}/visibility`, data)
}

// 获取任务依赖关系
export function getTaskDependencies(id: number) {
  return get<TaskDependencyDto[]>(`/task/tasks/${id}/dependencies`)
}

// 添加任务依赖
export function addTaskDependency(id: number, data: AddTaskDependencyRequest) {
  return post<TaskDependencyDto>(`/task/tasks/${id}/dependencies`, data)
}

// 移除任务依赖
export function removeTaskDependency(id: number, depId: number) {
  return del<boolean>(`/task/tasks/${id}/dependencies/${depId}`)
}

// 获取任务标签
export function getTaskTags(id: number) {
  return get<TagSimpleDto[]>(`/task/tasks/${id}/tags`)
}

// 设置任务标签
export function setTaskTags(id: number, data: SetTaskTagsRequest) {
  return post<boolean>(`/task/tasks/${id}/tags`, data)
}

// 删除任务
export function deleteTask(id: number) {
  return del<boolean>(`/task/tasks/${id}`)
}

// 批量更新任务
export function batchUpdateTasks(taskIds: number[], data: { status?: number; assigneeId?: number | null }) {
  return put<boolean>('/task/tasks/batch', { taskIds, ...data })
}

// 我的任务
export function getMyTasks() {
  return get<{ items: TaskListDto[]; total: number }>('/task/tasks/my')
}

// ==================== 任务评论 ====================

// 获取评论列表
export function getTaskComments(taskId: number, params?: CommentPagedRequest) {
  return get<{ items: TaskCommentListDto[]; total: number }>(`/task/tasks/${taskId}/comments`, params)
}

// 添加评论
export function createTaskComment(taskId: number, data: CreateTaskCommentRequest) {
  return post<TaskCommentListDto>(`/task/tasks/${taskId}/comments`, data)
}

// 编辑评论
export function updateTaskComment(taskId: number, cid: number, data: UpdateTaskCommentRequest) {
  return put<TaskCommentListDto>(`/task/tasks/${taskId}/comments/${cid}`, data)
}

// 删除评论
export function deleteTaskComment(taskId: number, cid: number) {
  return del<boolean>(`/task/tasks/${taskId}/comments/${cid}`)
}

// 添加/切换表情回应
export function toggleCommentReaction(taskId: number, cid: number, data: ToggleReactionRequest) {
  return post<ReactionSummaryDto[]>(`/task/tasks/${taskId}/comments/${cid}/reactions`, data)
}

// 移除表情
export function removeCommentReaction(taskId: number, cid: number, emoji: string) {
  return del<boolean>(`/task/tasks/${taskId}/comments/${cid}/reactions/${emoji}`)
}

// 发送评论到钉钉
export function pushCommentToDingTalk(taskId: number, cid: number) {
  return post<boolean>(`/task/tasks/${taskId}/comments/${cid}/push-dingtalk`)
}

// ==================== 附件管理 ====================

// 上传附件（FormData）
export function uploadAttachment(data: FormData) {
  return post<AttachmentListDto>('/task/attachments', data)
}

// 获取附件列表
export function getAttachments(relationType: number, relationId: number) {
  return get<AttachmentListDto[]>(`/task/attachments/${relationType}/${relationId}`)
}

// 删除附件
export function deleteAttachment(id: number) {
  return del<boolean>(`/task/attachments/${id}`)
}

// 下载附件（返回文件流，使用 window.open 或 a 标签方式下载）
export function getAttachmentDownloadUrl(id: number) {
  return `/task/attachments/${id}/download`
}

// ==================== 进度上报 ====================

// 提交进度上报
export function createProgressReport(taskId: number, data: CreateProgressReportRequest) {
  return post<ProgressReportListDto>(`/task/tasks/${taskId}/progress`, data)
}

// 获取进度上报历史
export function getProgressReports(taskId: number, params?: ProgressReportPagedRequest) {
  return get<{ items: ProgressReportListDto[]; total: number }>(`/task/tasks/${taskId}/progress`, params)
}

// 发送进度上报到钉钉
export function pushProgressToDingTalk(taskId: number, pid: number) {
  return post<boolean>(`/task/tasks/${taskId}/progress/${pid}/push-dingtalk`)
}

// ==================== 任务调度 ====================

// 调度列表
export function getSchedules(params?: SchedulePagedRequest) {
  return get<{ items: TaskScheduleListDto[]; total: number }>('/task/schedules', params)
}

// 创建调度
export function createSchedule(data: CreateTaskScheduleRequest) {
  return post<TaskScheduleListDto>('/task/schedules', data)
}

// 更新调度
export function updateSchedule(id: number, data: UpdateTaskScheduleRequest) {
  return put<TaskScheduleListDto>(`/task/schedules/${id}`, data)
}

// 启用/禁用调度
export function toggleSchedule(id: number) {
  return put<boolean>(`/task/schedules/${id}/toggle`)
}

// ==================== 任务提醒 ====================

// 获取任务提醒列表
export function getReminders(taskId: number) {
  return get<TaskReminderListDto[]>(`/task/reminders/${taskId}`)
}

// 创建提醒
export function createReminder(taskId: number, data: CreateTaskReminderRequest) {
  return post<TaskReminderListDto>(`/task/reminders/${taskId}`, data)
}

// 删除提醒
export function deleteReminder(id: number) {
  return del<boolean>(`/task/reminders/${id}`)
}

// ==================== 绩效考核 ====================

// 考核周期列表
export function getPerformancePeriods(params?: PerformancePeriodPagedRequest) {
  return get<{ items: PerformancePeriodListDto[]; total: number }>('/task/performance/periods', params)
}

// 创建考核周期
export function createPerformancePeriod(data: CreatePerformancePeriodRequest) {
  return post<PerformancePeriodListDto>('/task/performance/periods', data)
}

// 更新考核周期
export function updatePerformancePeriod(id: number, data: UpdatePerformancePeriodRequest) {
  return put<PerformancePeriodListDto>(`/task/performance/periods/${id}`, data)
}

// 触发绩效自动计算
export function calculatePerformance(id: number) {
  return post<boolean>(`/task/performance/periods/${id}/calculate`)
}

// 获取周期内所有考核记录
export function getPerformanceRecords(periodId: number) {
  return get<PerformanceRecordListDto[]>(`/task/performance/periods/${periodId}/records`)
}

// 获取个人考核详情
export function getPerformanceRecordDetail(id: number) {
  return get<PerformanceRecordDetailDto>(`/task/performance/records/${id}`)
}

// 提交自评
export function selfEvaluate(id: number, data: SelfEvaluateRequest) {
  return put<boolean>(`/task/performance/records/${id}/self-evaluate`, data)
}

// 上级评分
export function superiorReview(id: number, data: SuperiorReviewRequest) {
  return put<boolean>(`/task/performance/records/${id}/review`, data)
}

// 我的绩效
export function getMyPerformance() {
  return get<PerformanceRecordListDto[]>('/task/performance/my')
}

// 绩效看板
export function getPerformanceDashboard(periodId?: number) {
  return get<PerformanceDashboardDto>('/task/performance/dashboard', periodId != null ? { periodId } : undefined)
}

// 获取评价维度配置列表
export function getPerformanceDimensions() {
  return get<PerformanceDimensionListDto[]>('/task/performance/dimensions')
}

// 创建评价维度
export function createPerformanceDimension(data: CreatePerformanceDimensionRequest) {
  return post<PerformanceDimensionListDto>('/task/performance/dimensions', data)
}

// 更新评价维度
export function updatePerformanceDimension(id: number, data: UpdatePerformanceDimensionRequest) {
  return put<PerformanceDimensionListDto>(`/task/performance/dimensions/${id}`, data)
}

// 删除评价维度
export function deletePerformanceDimension(id: number) {
  return del<boolean>(`/task/performance/dimensions/${id}`)
}

// ==================== 标签管理 ====================

// 获取标签列表
export function getTags() {
  return get<TagListDto[]>('/task/tags')
}

// 创建标签
export function createTag(data: CreateTagRequest) {
  return post<TagListDto>('/task/tags', data)
}

// 更新标签
export function updateTag(id: number, data: UpdateTagRequest) {
  return put<TagListDto>(`/task/tags/${id}`, data)
}

// 删除标签
export function deleteTag(id: number) {
  return del<boolean>(`/task/tags/${id}`)
}

// ==================== 通知中心 ====================

// 获取通知列表
export function getNotifications(params?: NotificationPagedRequest) {
  return get<{ items: NotificationListDto[]; total: number }>('/task/notifications', params)
}

// 获取未读通知数
export function getUnreadCount() {
  return get<UnreadCountDto>('/task/notifications/unread-count', undefined, { silent: true } as any)
}

// 标记已读
export function markNotificationRead(id: number) {
  return put<boolean>(`/task/notifications/${id}/read`)
}

// 全部标记已读
export function markAllNotificationsRead() {
  return put<boolean>('/task/notifications/read-all')
}

// ==================== 复盘管理 ====================

// 复盘列表
export function getReviews(params?: ReviewPagedRequest) {
  return get<{ items: ReviewListDto[]; total: number }>('/task/reviews', params)
}

// 复盘详情
export function getReview(id: number) {
  return get<ReviewDetailDto>(`/task/reviews/${id}`)
}

// 创建复盘
export function createReview(data: CreateReviewRequest) {
  return post<ReviewDetailDto>('/task/reviews', data)
}

// 更新复盘
export function updateReview(id: number, data: UpdateReviewRequest) {
  return put<ReviewDetailDto>(`/task/reviews/${id}`, data)
}

// 发布复盘
export function publishReview(id: number) {
  return put<boolean>(`/task/reviews/${id}/publish`)
}

// 删除复盘
export function deleteReview(id: number) {
  return del<boolean>(`/task/reviews/${id}`)
}

// 从复盘提炼知识
export function extractKnowledge(id: number, data: ExtractKnowledgeRequest) {
  return post<KnowledgeDetailDto>(`/task/reviews/${id}/extract-knowledge`, data)
}

// 获取指定实体的复盘列表
export function getEntityReviews(type: number, entityId: number) {
  return get<ReviewListDto[]>(`/task/${type}/${entityId}/reviews`)
}

// ==================== 知识库 ====================

// 知识库列表
export function getKnowledgeList(params?: KnowledgePagedRequest) {
  return get<{ items: KnowledgeListDto[]; total: number }>('/task/knowledge', params)
}

// 知识详情
export function getKnowledge(id: number) {
  return get<KnowledgeDetailDto>(`/task/knowledge/${id}`)
}

// 创建知识文章
export function createKnowledge(data: CreateKnowledgeRequest) {
  return post<KnowledgeDetailDto>('/task/knowledge', data)
}

// 更新知识文章
export function updateKnowledge(id: number, data: UpdateKnowledgeRequest) {
  return put<KnowledgeDetailDto>(`/task/knowledge/${id}`, data)
}

// 删除知识文章
export function deleteKnowledge(id: number) {
  return del<boolean>(`/task/knowledge/${id}`)
}

// 点赞/取消点赞
export function toggleKnowledgeLike(id: number) {
  return post<boolean>(`/task/knowledge/${id}/like`)
}

// 收藏/取消收藏
export function toggleKnowledgeCollect(id: number) {
  return post<boolean>(`/task/knowledge/${id}/collect`)
}

// 获取知识评论列表
export function getKnowledgeComments(id: number) {
  return get<KnowledgeCommentDto[]>(`/task/knowledge/${id}/comments`)
}

// 添加知识评论
export function createKnowledgeComment(id: number, data: CreateKnowledgeCommentRequest) {
  return post<KnowledgeCommentDto>(`/task/knowledge/${id}/comments`, data)
}

// 我的收藏
export function getMyCollections() {
  return get<{ items: KnowledgeListDto[]; total: number }>('/task/knowledge/my-collections')
}

// 热门知识
export function getHotKnowledge() {
  return get<KnowledgeListDto[]>('/task/knowledge/hot')
}

// ==================== 看板 ====================

// 获取看板数据（按状态分组）
export function getKanbanData(params?: KanbanQueryRequest) {
  return get<KanbanDataDto>('/task/kanban', params)
}

// 拖拽移动（变更状态+排序）
export function kanbanMove(data: KanbanMoveRequest) {
  return put<boolean>('/task/kanban/move', data)
}
