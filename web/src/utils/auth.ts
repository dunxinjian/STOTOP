const TOKEN_KEY = 'stotop_token'
const USER_INFO_KEY = 'stotop_user_info'
const REFRESH_TOKEN_KEY = 'stotop_refresh_token'
const SESSION_ID_KEY = 'stotop_session_id'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function removeToken(): void {
  localStorage.removeItem(TOKEN_KEY)
}

export interface UserInfo {
  id: number
  username: string
  realName: string
  avatar?: string
  email?: string
  phone?: string
  roleName?: string
  roles: string[]
}

export function getUserInfo(): UserInfo | null {
  const info = localStorage.getItem(USER_INFO_KEY)
  if (info) {
    try {
      return JSON.parse(info) as UserInfo
    } catch {
      return null
    }
  }
  return null
}

export function setUserInfo(info: UserInfo): void {
  localStorage.setItem(USER_INFO_KEY, JSON.stringify(info))
}

export function removeUserInfo(): void {
  localStorage.removeItem(USER_INFO_KEY)
}

// ===== Refresh Token 管理 =====

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY)
}

export function setRefreshToken(token: string): void {
  localStorage.setItem(REFRESH_TOKEN_KEY, token)
}

export function removeRefreshToken(): void {
  localStorage.removeItem(REFRESH_TOKEN_KEY)
}

// ===== Session ID 管理 =====

export function getSessionId(): string | null {
  return localStorage.getItem(SESSION_ID_KEY)
}

export function setSessionId(id: string): void {
  localStorage.setItem(SESSION_ID_KEY, id)
}

export function removeSessionId(): void {
  localStorage.removeItem(SESSION_ID_KEY)
}

/**
 * 清除所有认证相关数据（退出时调用）
 */
export function clearAuthData(): void {
  removeToken()
  removeUserInfo()
  removeRefreshToken()
  removeSessionId()
}
