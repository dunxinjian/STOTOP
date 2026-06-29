import axios from 'axios'
import { get, post, put, del } from './request'
import { getToken, getRefreshToken } from '@/utils/auth'

// ===== 类型定义 =====

export interface SecurityConfig {
  idleWarningMinutes: number
  idleLockscreenMinutes: number
  idleLogoutMinutes: number
  maxDevicesPerUser: number
  accessTokenMinutes: number
  refreshTokenDays: number
  ipChangeForceReauth: boolean
  fingerprintChangeForceReauth: boolean
  loginFailLockCount: number
  loginFailLockMinutes: number
}

export interface SessionInfo {
  sessionId: string
  deviceFingerprint: string
  ipAddress: string
  userAgent: string
  loginTime: string
  lastActiveTime: string
  isCurrent: boolean
}

export interface RefreshTokenResult {
  token: string
  refreshToken: string
}

// ===== API 方法 =====

/**
 * 获取安全配置（供前端空闲检测使用）
 */
export function getSecurityConfig(): Promise<SecurityConfig> {
  return get<SecurityConfig>('/auth/security-config', undefined, { silent: true } as any)
}

/**
 * 刷新Token
 * 注意：不经过需要认证的拦截器，直接用 axios 发请求
 */
export async function refreshTokenApi(refreshToken: string): Promise<RefreshTokenResult> {
  const baseURL = import.meta.env.VITE_API_BASE_URL || '/api'
  const currentToken = getToken()
  const response = await axios.post(`${baseURL}/auth/refresh-token`, {
    refreshToken,
  }, {
    headers: {
      'Content-Type': 'application/json',
      ...(currentToken ? { Authorization: `Bearer ${currentToken}` } : {}),
    },
  })
  const res = response.data
  // 兼容后端统一返回格式 { code, data, message }
  if (res.code !== undefined && res.code !== 200 && res.code !== 0) {
    throw new Error(res.message || '刷新Token失败')
  }
  return res.data || res
}

/**
 * 心跳
 */
export function heartbeat(): Promise<void> {
  return post('/auth/heartbeat', undefined, { silent: true } as any)
}

/**
 * 获取当前用户会话列表
 */
export function getUserSessions(): Promise<SessionInfo[]> {
  return get<SessionInfo[]>('/auth/sessions')
}

/**
 * 终止指定会话
 */
export function terminateSession(sessionId: string): Promise<void> {
  return post(`/auth/sessions/${sessionId}/terminate`)
}

/**
 * 终止除当前外所有会话
 */
export function terminateOtherSessions(): Promise<void> {
  return post('/auth/sessions/terminate-others')
}

/**
 * 验证密码（锁屏解锁用）
 */
export function verifyPassword(password: string): Promise<boolean> {
  return post<boolean>('/auth/verify-password', { password })
}

// ===== 管理端安全配置 API =====

export interface SecurityConfigItem {
  id: number
  configKey: string
  configValue: string
  description: string
  updateTime: string
  updatedBy: string
}

export interface AuditLogQuery {
  page: number
  pageSize: number
  startTime?: string
  endTime?: string
  eventType?: string
  eventResult?: string
  account?: string
}

export interface AuditLogItem {
  id: number
  userId: number | null
  account: string
  eventType: string
  eventResult: string
  ipAddress: string
  deviceInfo: string
  failReason: string
  sessionId: string
  createTime: string
}

export interface LoginStatistics {
  dailyStats: Array<{ date: string; successCount: number; failedCount: number }>
  totalSuccess: number
  totalFailed: number
}

export interface OnlineUser {
  userId: number
  account: string
  userName: string
  sessionId: string
  ipAddress: string
  deviceInfo: string
  loginTime: string
  lastActiveTime: string
}

/**
 * 获取全部安全配置（管理端）
 */
export function getSecurityConfigs(): Promise<SecurityConfigItem[]> {
  return get<SecurityConfigItem[]>('/system/security/config')
}

/**
 * 批量更新安全配置
 */
export function updateSecurityConfigs(configs: Record<string, string>): Promise<void> {
  return put('/system/security/config', { configs })
}

/**
 * 获取审计日志（分页）
 */
export function getAuditLogs(params: AuditLogQuery): Promise<{ items: AuditLogItem[]; total: number }> {
  return get<{ items: AuditLogItem[]; total: number }>('/system/security/audit-logs', params as any)
}

/**
 * 获取登录统计
 */
export function getLoginStatistics(days?: number): Promise<LoginStatistics> {
  return get<LoginStatistics>('/system/security/audit-logs/statistics', { days: days || 7 })
}

/**
 * 获取在线用户列表
 */
export function getOnlineUsers(): Promise<OnlineUser[]> {
  return get<OnlineUser[]>('/system/security/online-users')
}

/**
 * 管理员强制下线
 */
export function adminTerminateSession(sessionId: string): Promise<void> {
  return del(`/system/security/sessions/${sessionId}`)
}
