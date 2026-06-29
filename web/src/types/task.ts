// ==================== 目标与任务管理模块类型定义 ====================

// ==================== 目标 (Goal) ====================

export interface GoalListDto {
  id: number
  uid: string
  title: string
  orgId: number
  goalOrgId: number
  responsibleId: number | null
  responsibleName: string | null
  parentId: number
  level: string
  startDate: string
  endDate: string
  progress: number
  weight: number
  status: number
  keyResultCount: number
  childrenCount: number
  createTime: string
  updateTime: string
}

export interface GoalDetailDto {
  id: number
  uid: string
  title: string
  description: string | null
  orgId: number
  goalOrgId: number
  responsibleId: number | null
  responsibleName: string | null
  parentId: number
  level: string
  startDate: string
  endDate: string
  progress: number
  weight: number
  status: number
  creatorId: number
  creatorName: string | null
  createTime: string
  updateTime: string
  keyResults: KeyResultListDto[]
  children: GoalListDto[]
}

export interface GoalTreeDto {
  id: number
  uid: string
  title: string
  responsibleId: number | null
  responsibleName: string | null
  parentId: number
  level: string
  startDate?: string
  endDate?: string
  progress: number
  status: number
  keyResultCount: number
  childrenCount?: number
  children: GoalTreeDto[]
}

export interface CreateGoalRequest {
  title: string
  description?: string | null
  goalOrgId: number
  responsibleId?: number | null
  parentId?: number
  level: string
  startDate: string
  endDate: string
  weight?: number
}

export interface UpdateGoalRequest {
  title: string
  description?: string | null
  goalOrgId: number
  responsibleId?: number | null
  level: string
  startDate: string
  endDate: string
  weight?: number
  status: number
}

export interface DecomposeGoalRequest {
  title: string
  description?: string | null
  goalOrgId: number
  responsibleId?: number | null
  level: string
  startDate: string
  endDate: string
  weight?: number
}

export interface GoalTreeQueryRequest {
  level?: string | null
  goalOrgId?: number | null
  responsibleId?: number | null
  status?: number | null
  keyword?: string | null
}

// ==================== 关键成果 (KeyResult) ====================

export interface KeyResultListDto {
  id: number
  uid: string
  goalId: number
  title: string
  measureType: number
  targetValue: number
  currentValue: number
  startValue: number
  unit: string | null
  weight: number
  progress: number
  status: number
  responsibleId: number | null
  responsibleName: string | null
  sort: number
  createTime: string
  updateTime: string
}

export interface CreateKeyResultRequest {
  title: string
  measureType: number
  targetValue: number
  startValue?: number
  unit?: string | null
  weight?: number
  responsibleId?: number | null
  sort?: number
}

export interface UpdateKeyResultRequest {
  title: string
  measureType: number
  targetValue: number
  startValue?: number
  unit?: string | null
  weight?: number
  responsibleId?: number | null
  sort?: number
  status: number
}

export interface UpdateKeyResultProgressRequest {
  currentValue: number
  remark?: string | null
}

// ==================== 项目 (Project) ====================

export interface ProjectListDto {
  id: number
  uid: string
  name: string
  description: string | null
  orgId: number
  goalId: number | null
  goalTitle: string | null
  managerId: number
  managerName: string | null
  startDate: string | null
  endDate: string | null
  status: number
  memberCount: number
  taskCount: number
  completedTaskCount: number
  createTime: string
  updateTime: string
}

export interface ProjectDetailDto {
  id: number
  uid: string
  name: string
  description: string | null
  orgId: number
  goalId: number | null
  goalTitle: string | null
  managerId: number
  managerName: string | null
  startDate: string | null
  endDate: string | null
  status: number
  creatorId: number
  creatorName: string | null
  createTime: string
  updateTime: string
  taskCount: number
  completedTaskCount: number
  inProgressTaskCount: number
  overdueTaskCount: number
  members: ProjectMemberDto[]
}

export interface ProjectMemberDto {
  id: number
  projectId: number
  userId: number
  userName: string | null
  role: number
  joinTime: string
}

export interface CreateProjectRequest {
  name: string
  description?: string | null
  goalId?: number | null
  managerId: number
  startDate?: string | null
  endDate?: string | null
}

export interface UpdateProjectRequest {
  name: string
  description?: string | null
  goalId?: number | null
  managerId: number
  startDate?: string | null
  endDate?: string | null
  status: number
}

export interface AddProjectMemberRequest {
  userId: number
  role?: number
}

export interface ProjectPagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string | null
  sortField?: string | null
  sortOrder?: string | null
  status?: number | null
  managerId?: number | null
}

// ==================== 任务 (Task) ====================

export interface TaskListDto {
  id: number
  uid: string
  title: string
  orgId: number
  projectId: number | null
  projectName: string | null
  goalId: number | null
  krId: number | null
  parentTaskId: number
  type: number
  priority: number
  status: number
  assigneeId: number | null
  assigneeName: string | null
  creatorId: number
  creatorName: string | null
  planStart: string | null
  planEnd: string | null
  progress: number
  code: string | null
  subTaskCount: number
  completedSubTaskCount: number
  tags: TagSimpleDto[]
  createTime: string
  updateTime: string
}

export interface TaskDetailDto {
  id: number
  uid: string
  title: string
  description: string | null
  orgId: number
  projectId: number | null
  projectName: string | null
  goalId: number | null
  goalTitle: string | null
  krId: number | null
  krTitle: string | null
  parentTaskId: number
  type: number
  priority: number
  status: number
  assigneeId: number | null
  assigneeName: string | null
  creatorId: number
  creatorName: string | null
  planStart: string | null
  planEnd: string | null
  actualStart: string | null
  actualEnd: string | null
  estimatedHours: number | null
  actualHours: number | null
  progress: number
  visibility: number
  isTemplate: boolean
  code: string | null
  sort: number
  createTime: string
  updateTime: string
  subTasks: TaskListDto[]
  members: TaskMemberDto[]
  tags: TagSimpleDto[]
  dependencies: TaskDependencyDto[]
}

export interface TaskMemberDto {
  id: number
  taskId: number
  userId: number
  userName: string | null
  role: number
}

export interface TaskDependencyDto {
  id: number
  taskId: number
  dependsOnTaskId: number
  dependsOnTaskTitle: string | null
  dependsOnTaskStatus: number
  dependencyType: number
}

export interface TagSimpleDto {
  id: number
  name: string
  color: string
}

export interface CreateTaskRequest {
  title: string
  description?: string | null
  projectId?: number | null
  goalId?: number | null
  krId?: number | null
  parentTaskId?: number
  type?: number
  priority?: number
  assigneeId?: number | null
  planStart?: string | null
  planEnd?: string | null
  estimatedHours?: number | null
  visibility?: number
  tagIds?: number[] | null
  memberUserIds?: number[] | null
}

export interface UpdateTaskRequest {
  title: string
  description?: string | null
  projectId?: number | null
  goalId?: number | null
  krId?: number | null
  type: number
  priority: number
  assigneeId?: number | null
  planStart?: string | null
  planEnd?: string | null
  estimatedHours?: number | null
  visibility: number
  tagIds?: number[] | null
}

export interface ChangeTaskStatusRequest {
  status: number
  remark?: string | null
}

export interface AssignTaskRequest {
  assigneeId?: number | null
}

export interface SetTaskTagsRequest {
  tagIds: number[]
}

export interface AddTaskDependencyRequest {
  dependsOnTaskId: number
  dependencyType?: number
}

export interface SetTaskVisibilityRequest {
  visibility: number
  rules?: TaskVisibilityRuleDto[] | null
}

export interface TaskVisibilityRuleDto {
  targetType: number
  targetId: number
}

export interface TaskPagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string | null
  sortField?: string | null
  sortOrder?: string | null
  status?: number | null
  priority?: number | null
  assigneeId?: number | null
  projectId?: number | null
  goalId?: number | null
  krId?: number | null
  type?: number | null
  parentTaskId?: number | null
  tagIds?: number[] | null
  planStartFrom?: string | null
  planStartTo?: string | null
  planEndFrom?: string | null
  planEndTo?: string | null
  isTemplate?: boolean | null
}

// ==================== 任务评论 (TaskComment) ====================

export interface TaskCommentListDto {
  id: number
  taskId: number
  userId: number
  userName: string | null
  content: string
  type: number
  parentCommentId: number
  pushedToDingTalk: boolean
  createTime: string
  updateTime: string
  reactions: ReactionSummaryDto[]
  attachments: AttachmentListDto[]
  replies: TaskCommentListDto[]
}

export interface ReactionSummaryDto {
  emojiCode: string
  count: number
  hasReacted: boolean
  userIds: number[]
}

export interface CreateTaskCommentRequest {
  content: string
  type?: number
  parentCommentId?: number
  mentionUserIds?: number[] | null
}

export interface UpdateTaskCommentRequest {
  content: string
}

export interface ToggleReactionRequest {
  emojiCode: string
}

export interface CommentPagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string | null
  sortField?: string | null
  sortOrder?: string | null
  type?: number | null
}

// ==================== 附件 (Attachment) ====================

export interface AttachmentListDto {
  id: number
  relationType: number
  relationId: number
  userId: number
  userName: string | null
  originalFileName: string
  storagePath: string
  fileSize: number
  fileType: string
  createTime: string
}

export interface UploadAttachmentRequest {
  relationType: number
  relationId: number
}

// ==================== 进度上报 (ProgressReport) ====================

export interface ProgressReportListDto {
  id: number
  taskId: number
  reporterId: number
  reporterName: string | null
  progress: number
  content: string
  hours: number | null
  pushedToDingTalk: boolean
  createTime: string
  attachments: AttachmentListDto[]
}

export interface CreateProgressReportRequest {
  progress: number
  content: string
  hours?: number | null
}

export interface ProgressReportPagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string | null
  sortField?: string | null
  sortOrder?: string | null
  reporterId?: number | null
}

// ==================== 任务调度 (TaskSchedule) ====================

export interface TaskScheduleListDto {
  id: number
  templateTaskId: number
  templateTaskTitle: string | null
  scheduleType: number
  cronExpression: string | null
  scheduledTime: string | null
  nextExecution: string | null
  lastExecution: string | null
  isEnabled: boolean
  createTime: string
}

export interface CreateTaskScheduleRequest {
  templateTaskId: number
  scheduleType: number
  cronExpression?: string | null
  scheduledTime?: string | null
}

export interface UpdateTaskScheduleRequest {
  scheduleType: number
  cronExpression?: string | null
  scheduledTime?: string | null
  isEnabled: boolean
}

export interface SchedulePagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  sortField?: string | null
  sortOrder?: string | null
  scheduleType?: number
  isEnabled?: boolean
}

// ==================== 任务提醒 (TaskReminder) ====================

export interface TaskReminderListDto {
  id: number
  taskId: number
  taskTitle: string | null
  userId: number
  reminderTime: string
  reminderType: number
  isRead: boolean
  isSent: boolean
  createTime: string
}

export interface CreateTaskReminderRequest {
  taskId: number
  userId: number
  reminderTime: string
  reminderType: number
}

// ==================== 绩效考核 (Performance) ====================

export interface PerformancePeriodListDto {
  id: number
  uid: string
  name: string
  orgId: number
  type: number
  startDate: string
  endDate: string
  status: number
  recordCount: number
  createTime: string
  updateTime: string
}

export interface CreatePerformancePeriodRequest {
  name: string
  type: number
  startDate: string
  endDate: string
}

export interface UpdatePerformancePeriodRequest {
  name: string
  type: number
  startDate: string
  endDate: string
  status: number
}

export interface PerformancePeriodPagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  sortField?: string | null
  sortOrder?: string | null
  type?: number
  status?: number
}

export interface PerformanceRecordListDto {
  id: number
  periodId: number
  periodName: string | null
  employeeId: number
  employeeName: string | null
  orgId: number
  taskTotal: number
  completedCount: number
  onTimeCount: number
  overdueCount: number
  completionRate: number
  onTimeRate: number
  goalAchievementRate: number
  overallScore: number | null
  grade: string | null
  status: number
  updateTime: string
}

export interface PerformanceRecordDetailDto {
  id: number
  periodId: number
  periodName: string | null
  employeeId: number
  employeeName: string | null
  orgId: number
  taskTotal: number
  completedCount: number
  onTimeCount: number
  overdueCount: number
  completionRate: number
  onTimeRate: number
  goalAchievementRate: number
  qualityScore: number | null
  selfScore: number | null
  overallScore: number | null
  grade: string | null
  comment: string | null
  selfComment: string | null
  status: number
  createTime: string
  updateTime: string
  dimensionScores: PerformanceScoreDto[]
}

export interface PerformanceScoreDto {
  id: number
  recordId: number
  dimensionId: number
  dimensionName: string | null
  dimensionCode: string | null
  dataSource: number
  weight: number
  maxScore: number
  score: number | null
  evaluator: string | null
  remark: string | null
}

export interface SelfEvaluateRequest {
  selfComment?: string | null
  dimensionScores: DimensionScoreInput[]
}

export interface SuperiorReviewRequest {
  comment?: string | null
  grade?: string | null
  dimensionScores: DimensionScoreInput[]
}

export interface DimensionScoreInput {
  dimensionId: number
  score: number
  remark?: string | null
}

export interface PerformanceDimensionListDto {
  id: number
  orgId: number
  dimensionName: string
  dimensionCode: string
  dataSource: number
  weight: number
  maxScore: number
  sort: number
  isEnabled: boolean
}

export interface CreatePerformanceDimensionRequest {
  dimensionName: string
  dimensionCode: string
  dataSource: number
  weight?: number
  maxScore?: number
  sort?: number
}

export interface UpdatePerformanceDimensionRequest {
  dimensionName: string
  dimensionCode: string
  dataSource: number
  weight?: number
  maxScore?: number
  sort?: number
  isEnabled?: boolean
}

export interface PerformanceDashboardDto {
  periodId: number
  periodName: string | null
  totalEmployees: number
  evaluatedCount: number
  pendingSelfCount: number
  pendingReviewCount: number
  avgCompletionRate: number
  avgOnTimeRate: number
  avgOverallScore: number
  gradeDistribution: GradeDistributionDto[]
}

export interface GradeDistributionDto {
  grade: string
  count: number
  percentage: number
}

// ==================== 标签 (Tag) ====================

export interface TagListDto {
  id: number
  name: string
  color: string
  orgId: number
  sort: number
  taskCount: number
}

export interface CreateTagRequest {
  name: string
  color: string
  sort?: number
}

export interface UpdateTagRequest {
  name: string
  color: string
  sort?: number
}

// ==================== 通知 (Notification) ====================

export interface NotificationListDto {
  id: number
  receiverId: number
  eventType: number
  title: string
  content: string
  relationType: number
  relationId: number
  isRead: boolean
  pushedToDingTalk: boolean
  createTime: string
}

export interface UnreadCountDto {
  total: number
  byEventType: Record<number, number>
}

export interface NotificationPagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string | null
  sortField?: string | null
  sortOrder?: string | null
  isRead?: boolean | null
  eventType?: number | null
  relationType?: number | null
}

// ==================== 复盘 (Review) ====================

export interface ReviewListDto {
  id: number
  uid: string
  relationType: number
  relationId: number
  relationTitle: string | null
  orgId: number
  title: string
  reviewerId: number
  reviewerName: string | null
  status: number
  createTime: string
  updateTime: string
}

export interface ReviewDetailDto {
  id: number
  uid: string
  relationType: number
  relationId: number
  relationTitle: string | null
  orgId: number
  title: string
  wentWell: string | null
  toImprove: string | null
  lessonsLearned: string | null
  actionPlan: string | null
  reviewerId: number
  reviewerName: string | null
  participantIds: string | null
  participants: ParticipantDto[] | null
  status: number
  createTime: string
  updateTime: string
  attachments: AttachmentListDto[]
}

export interface ParticipantDto {
  userId: number
  userName: string | null
}

export interface CreateReviewRequest {
  relationType: number
  relationId: number
  title: string
  wentWell?: string | null
  toImprove?: string | null
  lessonsLearned?: string | null
  actionPlan?: string | null
  participantIds?: number[] | null
}

export interface UpdateReviewRequest {
  title: string
  wentWell?: string | null
  toImprove?: string | null
  lessonsLearned?: string | null
  actionPlan?: string | null
  participantIds?: number[] | null
}

export interface ExtractKnowledgeRequest {
  title: string
  category: number
  tagIds?: number[] | null
}

export interface ReviewPagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  sortField?: string | null
  sortOrder?: string | null
  relationType?: number
  relationId?: number | null
  reviewerId?: number | null
  status?: number
}

// ==================== 知识库 (Knowledge) ====================

export interface KnowledgeListDto {
  id: number
  uid: string
  title: string
  category: number
  orgId: number
  authorId: number
  authorName: string | null
  sourceReviewId: number | null
  viewCount: number
  likeCount: number
  collectCount: number
  status: number
  isPinned: boolean
  createTime: string
  updateTime: string
  tags: TagSimpleDto[]
}

export interface KnowledgeDetailDto {
  id: number
  uid: string
  title: string
  content: string | null
  category: number
  orgId: number
  authorId: number
  authorName: string | null
  sourceReviewId: number | null
  sourceTaskId: number | null
  sourceProjectId: number | null
  viewCount: number
  likeCount: number
  collectCount: number
  status: number
  isPinned: boolean
  createTime: string
  updateTime: string
  hasLiked: boolean
  hasCollected: boolean
  tags: TagSimpleDto[]
  attachments: AttachmentListDto[]
  comments: KnowledgeCommentDto[]
}

export interface KnowledgeCommentDto {
  id: number
  knowledgeId: number
  userId: number
  userName: string | null
  content: string
  parentCommentId: number
  createTime: string
  replies: KnowledgeCommentDto[]
}

export interface CreateKnowledgeRequest {
  title: string
  content?: string | null
  category: number
  sourceReviewId?: number | null
  sourceTaskId?: number | null
  sourceProjectId?: number | null
  tagIds?: number[] | null
}

export interface UpdateKnowledgeRequest {
  title: string
  content?: string | null
  category: number
  status: number
  isPinned: boolean
  tagIds?: number[] | null
}

export interface CreateKnowledgeCommentRequest {
  content: string
  parentCommentId?: number
}

export interface KnowledgePagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  sortField?: string
  sortOrder?: string | null
  category?: number
  tagIds?: number[] | null
  authorId?: number | null
  status?: number | null
  isPinned?: boolean | null
}

// ==================== 看板 (Kanban) ====================

export interface KanbanDataDto {
  projectId: number | null
  columns: KanbanColumnDto[]
}

export interface KanbanColumnDto {
  status: number
  statusName: string
  count: number
  cards: KanbanCardDto[]
}

export interface KanbanCardDto {
  id: number
  uid: string
  title: string
  code: string | null
  priority: number
  status: number
  assigneeId: number | null
  assigneeName: string | null
  planEnd: string | null
  progress: number
  subTaskCount: number
  completedSubTaskCount: number
  sort: number
  tags: TagSimpleDto[]
}

export interface KanbanMoveRequest {
  taskId: number
  targetStatus: number
  targetSort: number
}

export interface KanbanQueryRequest {
  projectId?: number | null
  assigneeId?: number | null
  tagIds?: number[] | null
  priority?: number | null
}
