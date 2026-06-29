import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { dingtalkLogin, refreshToken as refreshTokenApi } from '@shared/api/auth'
import type { UserInfo } from '@shared/types'
import { bridge } from '../utils/dingtalk-bridge'

const TOKEN_KEY = 'stotop_mobile_token'
const REFRESH_TOKEN_KEY = 'stotop_mobile_refresh_token'
const ORG_KEY = 'stotop_current_org_id'

export const useAuthStore = defineStore('mobile-auth', () => {
  const token = ref(localStorage.getItem(TOKEN_KEY) || '')
  const refreshTokenValue = ref(localStorage.getItem(REFRESH_TOKEN_KEY) || '')
  const user = ref<UserInfo | null>(null)
  const currentOrgId = ref<number>(Number(localStorage.getItem(ORG_KEY)) || 0)

  const isLoggedIn = computed(() => !!token.value)
  const organizations = computed(() => user.value?.organizations || [])
  const currentOrg = computed(
    () => organizations.value.find(o => o.id === currentOrgId.value) || organizations.value[0]
  )

  function setToken(newToken: string, newRefreshToken: string) {
    token.value = newToken
    refreshTokenValue.value = newRefreshToken
    localStorage.setItem(TOKEN_KEY, newToken)
    localStorage.setItem(REFRESH_TOKEN_KEY, newRefreshToken)
  }

  function clearToken() {
    token.value = ''
    refreshTokenValue.value = ''
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
  }

  function setCurrentOrg(orgId: number) {
    currentOrgId.value = orgId
    localStorage.setItem(ORG_KEY, String(orgId))
  }

  /** 钉钉免登 */
  async function loginByDingTalk() {
    const authCode = await bridge.requestAuthCode()
    const result = await dingtalkLogin(authCode)
    setToken(result.token, result.refreshToken)
    user.value = result.user
    if (result.user.defaultOrgId && !currentOrgId.value) {
      setCurrentOrg(result.user.defaultOrgId)
    }
  }

  /** Token 刷新 — 含竞态防护 */
  let refreshPromise: Promise<void> | null = null

  async function doRefreshToken(): Promise<void> {
    if (refreshPromise) return refreshPromise

    refreshPromise = (async () => {
      try {
        if (!refreshTokenValue.value) {
          // 没有 refreshToken，重新免登
          await loginByDingTalk()
          return
        }
        const result = await refreshTokenApi(refreshTokenValue.value)
        setToken(result.token, result.refreshToken)
      } catch {
        // 刷新失败，重新免登
        clearToken()
        await loginByDingTalk()
      } finally {
        refreshPromise = null
      }
    })()

    return refreshPromise
  }

  return {
    token,
    refreshTokenValue,
    user,
    currentOrgId,
    isLoggedIn,
    organizations,
    currentOrg,
    setToken,
    clearToken,
    setCurrentOrg,
    loginByDingTalk,
    doRefreshToken,
  }
})
