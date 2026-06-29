import { defineStore } from 'pinia'
import { ref } from 'vue'
import { login as loginApi, getUserInfo as getUserInfoApi, logout as logoutApi, dingtalkLogin as dingtalkLoginApi } from '@/api/auth'
import type { UserInfoResult } from '@/api/auth'
import { getToken, setToken, removeToken, setUserInfo, removeUserInfo, removeRefreshToken, removeSessionId, setRefreshToken, setSessionId } from '@/utils/auth'
import type { UserInfo } from '@/utils/auth'
import type { LoginParams } from '@/api/auth'

// ===== sessionStorage 缓存常量 =====
const USERINFO_CACHE_KEY = 'stotop_userinfo_cache'
const CACHE_MAX_AGE = 30 * 60 * 1000 // 30 分钟

export const useUserStore = defineStore('user', () => {
  const token = ref<string>(getToken() || '')
  const userInfo = ref<UserInfo | null>(null)
  const roles = ref<string[]>([])
  const permissions = ref<string[]>([])

  async function login(params: LoginParams) {
    const result = await loginApi(params)
    token.value = result.token
    setToken(result.token)
    if (result.refreshToken) setRefreshToken(result.refreshToken)
    if (result.sessionId) setSessionId(result.sessionId)
    // 登录成功后清除用户信息缓存，确保下次 fetchUserInfo 重新请求 API
    try { sessionStorage.removeItem(USERINFO_CACHE_KEY) } catch { /* 静默 */ }
    return result
  }

  async function fetchUserInfo() {
    // 优先从 sessionStorage 读取缓存
    try {
      const cached = sessionStorage.getItem(USERINFO_CACHE_KEY)
      if (cached) {
        const { data, timestamp } = JSON.parse(cached) as { data: UserInfoResult; timestamp: number }
        // 校验缓存有效性：TTL 未过期 且 菜单数据存在
        if (Date.now() - timestamp < CACHE_MAX_AGE && data.menus && data.menus.length > 0) {
          const info: UserInfo = {
            id: data.id,
            username: data.account,
            realName: data.name,
            avatar: data.avatar,
            email: data.email,
            roles: data.roles,
          }
          userInfo.value = info
          roles.value = data.roles
          permissions.value = data.permissions || []
          setUserInfo(info)
          localStorage.setItem('stotop_user_id', String(data.id))
          // 重新加载 sidebar 配置（确保登录后恢复用户的固定菜单）
          const { useSidebarStore } = await import('./sidebar')
          const sidebarStore = useSidebarStore()
          sidebarStore.loadFromStorage()
          return data
        }
      }
    } catch { /* 缓存解析失败时回退到 API 调用 */ }

    // 缓存未命中，请求 API
    const result = await getUserInfoApi()
    const info: UserInfo = {
      id: result.id,
      username: result.account,
      realName: result.name,
      avatar: result.avatar,
      email: result.email,
      roles: result.roles,
    }
    userInfo.value = info
    roles.value = result.roles
    permissions.value = result.permissions || []
    setUserInfo(info)
    localStorage.setItem('stotop_user_id', String(result.id))
    // 重新加载 sidebar 配置（确保登录后恢复用户的固定菜单）
    const { useSidebarStore } = await import('./sidebar')
    const sidebarStore = useSidebarStore()
    sidebarStore.loadFromStorage()

    // 存入 sessionStorage
    try {
      sessionStorage.setItem(USERINFO_CACHE_KEY, JSON.stringify({
        data: result,
        timestamp: Date.now(),
      }))
    } catch { /* sessionStorage 写入失败时静默忽略 */ }

    return result
  }

  async function logout() {
    // 停止空闲检测，避免退出后计时器继续运行
    const { useSecurityStore } = await import('./security')
    const securityStore = useSecurityStore()
    securityStore.stopIdleDetection()

    try {
      await logoutApi()
    } catch {
      // 忽略退出登录接口错误
    }
    token.value = ''
    userInfo.value = null
    roles.value = []
    permissions.value = []
    removeToken()
    removeUserInfo()
    removeRefreshToken()
    removeSessionId()
    // 清除 sessionStorage 缓存
    try { sessionStorage.removeItem(USERINFO_CACHE_KEY) } catch { /* 静默 */ }
  }

  function resetState() {
    token.value = ''
    userInfo.value = null
    roles.value = []
    permissions.value = []
  }

  function updateUserInfo(partialInfo: Partial<UserInfo>) {
    if (userInfo.value) {
      userInfo.value = { ...userInfo.value, ...partialInfo }
      setUserInfo(userInfo.value)
    }
  }

  function hasPermission(code: string): boolean {
    return permissions.value.includes(code)
  }

  async function dingtalkLogin(authCode: string) {
    const result = await dingtalkLoginApi(authCode, 0)
    token.value = result.token
    setToken(result.token)
    return result
  }

  function hasAnyPermission(...codes: string[]): boolean {
    return codes.some(c => permissions.value.includes(c))
  }

  return {
    token,
    userInfo,
    roles,
    permissions,
    login,
    fetchUserInfo,
    logout,
    resetState,
    updateUserInfo,
    dingtalkLogin,
    hasPermission,
    hasAnyPermission,
  }
})
