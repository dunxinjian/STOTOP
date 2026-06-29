export interface FeedbackCardDto {
  id: number
  uid: string
  orgId: number
  title: string
  type: number
  module: string
  severity: number
  status: number
  submitterId: number
  submitterName: string | null
  assigneeId: number | null
  assigneeName: string | null
  description: string | null
  reproduceSteps: string | null
  expectedResult: string | null
  actualResult: string | null
  attachmentLinks: string | null
  pageUrl: string | null
  clientInfo: string | null
  version: string | null
  conclusion: string | null
  createTime: string
  updateTime: string
  closedTime: string | null
}

export interface FeedbackActivityDto {
  id: number
  feedbackId: number
  actorId: number
  actorName: string | null
  action: string
  content: string | null
  fromStatus: number | null
  toStatus: number | null
  createTime: string
}

export interface FeedbackDetailDto extends FeedbackCardDto {
  activities: FeedbackActivityDto[]
}

export interface FeedbackQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  type?: number | null
  module?: string | null
  severity?: number | null
  status?: number | null
  submitterId?: number | null
  assigneeId?: number | null
  mine?: boolean
}

export interface CreateFeedbackRequest {
  title: string
  type: number
  module: string
  severity: number
  description?: string | null
  reproduceSteps?: string | null
  expectedResult?: string | null
  actualResult?: string | null
  attachmentLinks?: string | null
  pageUrl?: string | null
  clientInfo?: string | null
  version?: string | null
}

export interface UpdateFeedbackRequest extends CreateFeedbackRequest {
  assigneeId?: number | null
  conclusion?: string | null
}

export interface TransitionFeedbackRequest {
  status: number
  comment?: string | null
  conclusion?: string | null
}

export interface AssignFeedbackRequest {
  assigneeId?: number | null
  comment?: string | null
}

export interface AddFeedbackCommentRequest {
  content: string
}

export interface FeedbackStatusCountDto {
  status: number
  count: number
}
