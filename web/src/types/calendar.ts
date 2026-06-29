// 日历事件状态枚举
export enum CalendarEventStatus {
  Pending = 0,      // 未召开
  InProgress = 1,   // 进行中
  Completed = 2,    // 已召开
  Early = 3,        // 提前
  Delayed = 4,      // 延后
  Cancelled = 5     // 取消
}

// 优先级枚举
export enum CalendarEventPriority {
  Normal = 0,
  Important = 1,
  Urgent = 2
}

// 参与者回复状态
export enum AttendeeResponseStatus {
  Pending = 0,
  Accepted = 1,
  Declined = 2,
  Tentative = 3
}

// 参与者出席状态
export enum AttendeeAttendStatus {
  Unknown = 0,
  Attended = 1,
  Absent = 2,
  Late = 3
}

// 同步状态
export enum SyncStatus {
  NotSynced = 0,
  Synced = 1,
  SyncFailed = 2
}

// 日历事件 DTO
export interface CalendarEventDto {
  id: number
  title: string
  description?: string
  location?: string
  startTime: string
  endTime: string
  actualStartTime?: string
  actualEndTime?: string
  status: CalendarEventStatus
  statusText: string
  priority: CalendarEventPriority
  priorityText: string
  isAllDay: boolean
  isRecurring: boolean
  recurrenceRule?: string
  recurrenceEndDate?: string
  parentEventId?: number
  organizerId: number
  organizerName: string
  orgId: number
  orgName: string
  dingTalkEventId?: string
  syncStatus: SyncStatus
  lastSyncTime?: string
  color?: string
  remindMinutes: number
  attendees: CalendarEventAttendeeDto[]
  attendeeCount: number
  createTime: string
  updateTime: string
}

// 参与者 DTO
export interface CalendarEventAttendeeDto {
  id: number
  eventId: number
  userId: number
  userName: string
  responseStatus: AttendeeResponseStatus
  responseStatusText: string
  attendStatus: AttendeeAttendStatus
  attendStatusText: string
  isRequired: boolean
}

// 创建请求
export interface CreateCalendarEventRequest {
  title: string
  description?: string
  location?: string
  startTime: string
  endTime: string
  isAllDay: boolean
  priority: CalendarEventPriority
  isRecurring: boolean
  recurrenceRule?: string
  recurrenceEndDate?: string
  orgId: number
  attendeeUserIds: number[]
  remindMinutes: number
  color?: string
  syncToDingTalk: boolean
}

// 更新请求
export interface UpdateCalendarEventRequest {
  title: string
  description?: string
  location?: string
  startTime: string
  endTime: string
  isAllDay: boolean
  priority: CalendarEventPriority
  isRecurring: boolean
  recurrenceRule?: string
  recurrenceEndDate?: string
  remindMinutes: number
  color?: string
}

// 看板视图数据
export interface CalendarBoardData {
  pending: CalendarEventDto[]
  inProgress: CalendarEventDto[]
  completed: CalendarEventDto[]
  early: CalendarEventDto[]
  delayed: CalendarEventDto[]
  cancelled: CalendarEventDto[]
}

// 状态颜色映射
export const STATUS_COLOR_MAP: Record<CalendarEventStatus, string> = {
  [CalendarEventStatus.Pending]: 'var(--color-info)',    // 待处理（蓝→信息态）
  [CalendarEventStatus.InProgress]: 'var(--color-success)', // 进行中（绿）
  [CalendarEventStatus.Completed]: '#8c8c8c',            // 已召开（中性灰）
  [CalendarEventStatus.Early]: 'var(--color-info)',      // 提前（青→收敛至信息态）
  [CalendarEventStatus.Delayed]: 'var(--color-warning)', // 延后（橙黄）
  [CalendarEventStatus.Cancelled]: 'var(--color-danger)' // 取消（红）
}

// 状态标签映射
export const STATUS_LABEL_MAP: Record<CalendarEventStatus, string> = {
  [CalendarEventStatus.Pending]: '未召开',
  [CalendarEventStatus.InProgress]: '进行中',
  [CalendarEventStatus.Completed]: '已召开',
  [CalendarEventStatus.Early]: '提前',
  [CalendarEventStatus.Delayed]: '延后',
  [CalendarEventStatus.Cancelled]: '取消'
}

export const PRIORITY_LABEL_MAP: Record<CalendarEventPriority, string> = {
  [CalendarEventPriority.Normal]: '普通',
  [CalendarEventPriority.Important]: '重要',
  [CalendarEventPriority.Urgent]: '紧急'
}
