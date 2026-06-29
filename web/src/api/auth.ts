import { get, post } from './request'

export interface LoginParams {
  account: string
  password: string
}

export interface LoginResult {
  token: string
  tokenType: string
  expiresIn: number
  refreshToken: string
  sessionId: string
}

export interface UserInfoResult {
  id: number
  name: string
  account: string
  avatar?: string
  email?: string
  roles: string[]
  permissions: string[]
  menus: MenuItem[]
}

export interface MenuItem {
  id: number
  parentId?: number
  name: string
  isVisible?: number
  code: string
  icon?: string
  route?: string
  componentPath?: string
  children?: MenuItem[]
  type: 'menu' | 'button' | 'module'
  sort?: number
}

/**
 * 登录
 */
export function login(data: LoginParams) {
  return post<LoginResult>('/auth/login', data)
}

/**
 * 获取当前用户信息及菜单权限
 */
export function getUserInfo() {
  return get<UserInfoResult>('/auth/userinfo', undefined, { silent: true, timeout: 30000 } as any)
}

/**
 * 退出登录
 */
export function logout() {
  return post('/auth/logout')
}

/**
 * 刷新 Token
 */
export function refreshToken() {
  return post<LoginResult>('/auth/refresh-token')
}

export interface DingtalkConfigResult {
  enabled: boolean
  appKey?: string
  redirectUri?: string
}

/**
 * 获取钉钉登录配置
 */
export function getDingtalkConfig() {
  return get<DingtalkConfigResult>('/auth/dingtalk/config', undefined, { silent: true } as any)
}

/**
 * 钉钉扫码登录
 */
export function dingtalkLogin(authCode: string, orgId: number = 0) {
  return post<LoginResult>('/auth/dingtalk/login', { authCode, orgId })
}
