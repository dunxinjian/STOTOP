import { createRouter, createWebHistory } from 'vue-router'
import type { RouteLocationNormalized, NavigationGuardNext, RouteRecordRaw } from 'vue-router'
import NProgress from 'nprogress'
import 'nprogress/nprogress.css'
import { staticRoutes, layoutRoute, mobileRoutes } from './routes'
import { getToken } from '@/utils/auth'
import { inferModuleFromPath } from '@/config/modules'

NProgress.configure({ showSpinner: false })

const router = createRouter({
  history: createWebHistory(),
  routes: [...staticRoutes, ...mobileRoutes, layoutRoute],
  scrollBehavior: () => ({ top: 0 }),
})

// 路由守卫
let routesLoaded = false
let routesLoadingPromise: Promise<void> | null = null

// ─── 辅助函数：带超时的 Promise ───
function fetchWithTimeout<T>(promise: Promise<T>, ms: number): Promise<T> {
  return Promise.race([
    promise,
    new Promise<never>((_, reject) =>
      setTimeout(() => reject(new Error(`请求超时（${ms}ms）`)), ms)
    ),
  ])
}

// ─── 辅助函数：带重试的用户信息获取 ───
async function fetchUserInfoWithRetry(userStore: { fetchUserInfo: () => Promise<any> }, maxRetries = 2) {
  let lastError: Error | null = null
  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      return await fetchWithTimeout(userStore.fetchUserInfo(), 30000)
    } catch (error) {
      lastError = error as Error
      console.error(`[路由守卫] 第 ${attempt + 1} 次获取用户信息失败:`, lastError.message)
      if (attempt < maxRetries) {
        const delay = (attempt + 1) * 1000
        await new Promise(resolve => setTimeout(resolve, delay))
      }
    }
  }
  throw lastError
}

// ─── 辅助函数：加载动态路由及初始化上下文 ───
async function loadDynamicRoutes() {
  const { useUserStore } = await import('@/stores/user')
  const { usePermissionStore } = await import('@/stores/permission')
  const { useOrgContextStore } = await import('@/stores/orgContext')
  const { useTenantContextStore } = await import('@/stores/tenantContext')
  const { useAccountSetStore } = await import('@/stores/accountSet')

  const userStore = useUserStore()
  const permissionStore = usePermissionStore()
  const orgContextStore = useOrgContextStore()
  const tenantContextStore = useTenantContextStore()
  const accountSetStore = useAccountSetStore()

  // 惰性初始化：从 localStorage 恢复上次保存的状态
  orgContextStore.init()
  tenantContextStore.init()
  accountSetStore.init()

  // 获取用户信息和菜单（带超时和重试）
  const userInfoResult = await fetchUserInfoWithRetry(userStore)
  const dynamicRoutes = permissionStore.generateRoutes(userInfoResult.menus || [])

  // 加载组织上下文（await 避免竞态条件导致 OrgSwitcher 显示异常）
  await orgContextStore.fetchOrganizations()
  if (!orgContextStore.currentOrgId) {
    await orgContextStore.fetchCurrentContext()
  }

  // 加载可切换租户列表（多客户时 TenantSwitcher 显示；单客户为 1 个则切换器隐藏）。非阻塞。
  tenantContextStore.fetchTenants()

  // 确保账套列表加载（无论 org context 是否从 localStorage 恢复）
  if (accountSetStore.accountSets.length === 0) {
    await accountSetStore.fetchAccountSets()
  }

  // 动态添加路由到主布局
  dynamicRoutes.forEach((route) => {
    router.addRoute('Layout', route)
  })

  // 动态添加 catch-all 路由（确保它在所有路由之后）
  if (!router.hasRoute('CatchAll')) {
    router.addRoute({
      path: '/:pathMatch(.*)*',
      name: 'CatchAll',
      redirect: '/404',
      meta: { hidden: true },
    })
  }
}

// ─── 辅助函数：处理首次加载失败 ───
async function handleLoadFailure(to: RouteLocationNormalized, next: NavigationGuardNext) {
  console.error('获取用户信息失败')
  const { removeToken, removeUserInfo, removeRefreshToken, removeSessionId } = await import('@/utils/auth')
  removeToken()
  removeUserInfo()
  removeRefreshToken()
  removeSessionId()
  routesLoaded = false
  next(`/login?redirect=${to.path}`)
}

router.beforeEach(async (to, _from, next) => {
  NProgress.start()

  const isSetupPage = to.path === '/setup'
  const isLoginPage = to.path === '/login'
  const token = getToken()

  // /setup 页面免登录访问（认证由页面组件内部处理）
  if (isSetupPage) { next(); return }

  // 未登录处理
  if (!token) {
    if (isLoginPage) {
      next()
    } else {
      next(`/login?redirect=${to.path}`)
    }
    return
  }

  // 已登录且访问登录页，跳转首页
  if (isLoginPage) { next('/'); return }

  // 已登录，动态路由尚未加载
  if (!routesLoaded) {
    // 如果已经有加载在进行中，等待它完成
    if (routesLoadingPromise) {
      await routesLoadingPromise
      next({ ...to, replace: true })
      return
    }

    routesLoadingPromise = loadDynamicRoutes()

    try {
      await routesLoadingPromise
      routesLoaded = true

      // 获取角标数量（不阻塞导航）
      const { usePermissionStore } = await import('@/stores/permission')
      usePermissionStore().fetchBadgeCounts()

      // 若因动态路由未就绪而被 staticRoutes 的 catch-all 抢先匹配（to.path 被改写为 /404），
      // 使用 redirectedFrom 恢复用户最初请求的路径，避免 F5 刷新后误跳 404
      const originalFullPath = to.redirectedFrom?.fullPath
      if (originalFullPath && originalFullPath !== to.fullPath) {
        next({ path: originalFullPath, replace: true })
      } else {
        next({ ...to, replace: true })
      }
    } catch {
      await handleLoadFailure(to, next)
    } finally {
      routesLoadingPromise = null
    }
    return
  }

  // 模块级权限验证（已登录且路由已加载）
  // 豁免：permission:'*' 审批类页面（权限由业务数据层控制）、移动端路由
  const isExempt = to.meta?.permission === '*' || to.path.startsWith('/m/')
  if (!isExempt) {
    const moduleCode = inferModuleFromPath(to.path)
    if (moduleCode && moduleCode !== 'workhub') {
      const { usePermissionStore } = await import('@/stores/permission')
      const permissionStore = usePermissionStore()
      const hasAccess = permissionStore.getCurrentModuleMenus(moduleCode).length > 0
      if (!hasAccess) {
        next({ path: '/403', replace: true })
        return
      }
    }
  }

  next()
})

router.afterEach((to) => {
  NProgress.done()

  // 跳过登录页等非业务页面
  if (to.path === '/login' || to.path === '/404' || to.path === '/setup') return

  // 导航链路Tab管理：所有导航均追加新Tab（不重置）。内存软上限见 navChain.CHAIN_MAX（超出按 LRU 淘汰）；
  // 顶栏实际可见几个由 TopBarNavChain 按宽度自适应，放不下的收进 “»N” 下拉。
  // 工作台（/workhub/*：发起、待办）由顶栏固定页签承载，不进多页签链；
  // 落到固定页签时须清掉链内激活态（activeIndex=-1），否则残留的“已激活”下标会让
  // 该页签既点不动（onTabClick 认为已激活）又关不掉（被当作最后一个激活页签保护）。
  import('@/stores/navChain').then(({ useNavChainStore, consumeNavSource }) => {
    try {
      const navChainStore = useNavChainStore()
      if (to.path.startsWith('/workhub')) {
        navChainStore.deselect()
        return
      }
      consumeNavSource() // 消费来源标志（不再区分 menu/internal）
      const tab = {
        path: to.path,
        label: (to.meta?.label as string) || (to.meta?.title as string) || '',
        icon: (to.meta?.icon as string) || undefined,
      }
      navChainStore.pushToChain(tab)
    } catch { /* pinia 未初始化时静默处理 */ }
  }).catch(() => { /* 静默 */ })

  // 记录到 sidebar.recentPages（侧栏最近访问列表）
  import('@/stores/sidebar').then(({ useSidebarStore }) => {
    try {
      const sidebarStore = useSidebarStore()
      const icon = (to.meta?.icon as string) || ''
      const label = (to.meta?.label as string) || (to.meta?.title as string) || ''
      sidebarStore.recordVisit(to.path, { icon, label })
    } catch { /* pinia 未初始化时静默处理 */ }
  }).catch(() => { /* 静默 */ })

  // 记录页面访问（仅记录有标题的页面）
  if (to.meta?.title) {
    import('@/stores/recommendation').then(({ useRecommendationStore }) => {
      try {
        const recommendationStore = useRecommendationStore()
        const moduleCode = to.path.split('/')[1] || ''
        recommendationStore.recordVisit(
          to.path,
          to.meta.title as string,
          (to.meta.icon as string) || undefined,
          moduleCode
        )
      } catch { /* pinia 未初始化时静默处理 */ }
    }).catch(() => { /* 静默 */ })
  }
})

// 重置路由（退出登录时调用）
export function resetRouter() {
  routesLoaded = false
  routesLoadingPromise = null
  const newRouter = createRouter({
    history: createWebHistory(),
    routes: [...staticRoutes, ...mobileRoutes, layoutRoute],
  })
  ;(router as unknown as { matcher: unknown }).matcher = (newRouter as unknown as { matcher: unknown }).matcher
}

/**
 * 替换动态路由（组织切换时调用）
 * 重置 matcher 移除旧动态路由，再注入新路由，避免路由表残留旧权限。
 * 不重置 routesLoaded 标志，防止路由守卫重复加载。
 */
export function replaceDynamicRoutes(newRoutes: RouteRecordRaw[]) {
  // 1. 重置 matcher → 仅保留静态路由
  const newRouter = createRouter({
    history: createWebHistory(),
    routes: [...staticRoutes, ...mobileRoutes, layoutRoute],
  })
  ;(router as unknown as { matcher: unknown }).matcher = (newRouter as unknown as { matcher: unknown }).matcher

  // 2. 注入新动态路由
  newRoutes.forEach((route) => {
    router.addRoute('Layout', route)
  })

  // 3. 重新注册 catch-all 路由
  router.addRoute({
    path: '/:pathMatch(.*)*',
    name: 'CatchAll',
    redirect: '/404',
    meta: { hidden: true },
  })
}

export default router
