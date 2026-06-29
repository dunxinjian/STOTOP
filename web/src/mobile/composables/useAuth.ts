import { useAuthStore } from '../stores/auth'

/**
 * 组合式 API：获取当前用户认证状态
 */
export function useAuth() {
  const authStore = useAuthStore()

  return {
    isLoggedIn: authStore.isLoggedIn,
    user: authStore.user,
    currentOrg: authStore.currentOrg,
    organizations: authStore.organizations,
    switchOrg: authStore.setCurrentOrg,
    refreshToken: authStore.doRefreshToken,
  }
}
