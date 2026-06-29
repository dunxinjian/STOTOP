import { defineStore } from 'pinia'
import { ref, computed, watch, nextTick } from 'vue'
import { get, put } from '@/api/request'

// ---- 数据结构 ----

export interface GuidanceState {
  firstUseDate: string | null
  guidanceDismissed: boolean
  suggestionShown: boolean
}

export interface RecentPageItem {
  path: string
  icon: string
  label: string
  visitedAt: number
}

/**
 * 工作集项 —— 用户固化在侧栏的工作入口。
 * pinned=true 表示置顶到固定区；顺序由数组本身决定。
 */
export interface WorksetItem {
  path: string
  pinned: boolean
}

export interface SidebarConfig {
  collapsed: boolean
  moduleOrder?: string[]
  /** 旧版字段（仅路径数组），加载时自动迁移到 workset */
  favorites?: string[]
  /** 新版字段：有序工作集 */
  workset?: WorksetItem[]
  recentPages?: RecentPageItem[]
  guidanceState?: GuidanceState
  /** 侧栏宽度（px），用户拖拽后持久化 */
  sidebarWidth?: number
  // 迁移标记
  _migrated?: boolean
  // 以下为已废弃字段，仅用于读取旧数据并在加载阶段清除
  pinnedEntries?: any[]
  openTabs?: any[]
  activeTabId?: any
  sidebarMode?: any
}

// ---- 常量 ----

const RECENT_PAGES_MAX = 10
/** 固定区上限（产品约束） */
export const PINNED_MAX = 16

// 侧栏宽度限制（px）
export const SIDEBAR_WIDTH_DEFAULT = 168
export const SIDEBAR_WIDTH_MIN = 120
export const SIDEBAR_WIDTH_MAX = 240

function getStorageKey() {
  const userId = localStorage.getItem('stotop_user_id') || 'anonymous'
  return `sidebar-config:${userId}`
}

// ---- 服务端布局偏好 API ----

async function fetchLayoutPreference(): Promise<any | null> {
  try {
    const res = await get('/user/layout-preference', undefined, { silent: true } as any)
    return res || null
  } catch {
    return null
  }
}

async function saveLayoutPreference(data: any): Promise<void> {
  try {
    await put('/user/layout-preference', data, { silent: true } as any)
  } catch (e) {
    console.warn('保存布局偏好失败', e)
  }
}

// ---- 工具函数 ----

function debounce<T extends (...args: any[]) => any>(fn: T, delay: number): (...args: Parameters<T>) => void {
  let timer: ReturnType<typeof setTimeout> | null = null
  return (...args: Parameters<T>) => {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => { fn(...args); timer = null }, delay)
  }
}

/** 任意输入归一化为 WorksetItem[] */
function normalizeWorkset(input: any): WorksetItem[] {
  if (!Array.isArray(input)) return []
  const seen = new Set<string>()
  const result: WorksetItem[] = []
  for (const raw of input) {
    if (!raw) continue
    if (typeof raw === 'string') {
      if (!seen.has(raw)) {
        seen.add(raw)
        result.push({ path: raw, pinned: false })
      }
    } else if (typeof raw === 'object' && typeof raw.path === 'string') {
      if (!seen.has(raw.path)) {
        seen.add(raw.path)
        result.push({ path: raw.path, pinned: !!raw.pinned })
      }
    }
  }
  return result
}

// ---- Store ----

export const useSidebarStore = defineStore('sidebar', () => {
  // ---------- State ----------

  let _suppressSave = false

  const collapsed = ref<boolean>(localStorage.getItem('sidebar-collapsed') === 'true')
  const moduleOrder = ref<string[]>([])
  /** 侧栏宽度（px），用户可拖拽调整 */
  const sidebarWidth = ref<number>(SIDEBAR_WIDTH_DEFAULT)
  /** 工作集：有序，pinned 项视为固定到顶部 */
  const workset = ref<WorksetItem[]>([])
  /** 脏页面集合：仅会话内有效（不持久化、不上行） */
  const dirtyPages = ref<Set<string>>(new Set())
  const recentPages = ref<RecentPageItem[]>([])
  const guidanceState = ref<GuidanceState>({
    firstUseDate: null,
    guidanceDismissed: false,
    suggestionShown: false,
  })

  // ---------- Computed（兼容旧 API & 派生分组） ----------

  /** 兼容旧 API：返回 workset 中所有路径 */
  const favorites = computed<string[]>(() => workset.value.map((w) => w.path))
  const pinnedItems = computed<WorksetItem[]>(() => workset.value.filter((w) => w.pinned))
  const unpinnedItems = computed<WorksetItem[]>(() => workset.value.filter((w) => !w.pinned))
  const pinnedFull = computed<boolean>(() => pinnedItems.value.length >= PINNED_MAX)

  // ---------- 持久化 ----------

  function loadFromStorage() {
    try {
      const raw = localStorage.getItem(getStorageKey())
      if (raw) {
        const data: SidebarConfig = JSON.parse(raw)

        // ✅ 旧字段先合并到 favorites（仅做一次性数据修补）
        if (Array.isArray(data.pinnedEntries) && !data._migrated) {
          const pinnedPaths = data.pinnedEntries
            .map((e: any) => e?.routePath)
            .filter((p: any) => typeof p === 'string')
          data.favorites = Array.from(
            new Set([...(data.favorites || []), ...pinnedPaths]),
          )
          data._migrated = true
        }

        // 清除已废弃字段
        delete data.openTabs
        delete data.activeTabId
        delete data.pinnedEntries
        delete data.sidebarMode

        // 应用到 store
        if (typeof data.collapsed === 'boolean') collapsed.value = data.collapsed
        if (Array.isArray(data.moduleOrder)) moduleOrder.value = data.moduleOrder
        if (typeof data.sidebarWidth === 'number') {
          sidebarWidth.value = Math.max(SIDEBAR_WIDTH_MIN, Math.min(SIDEBAR_WIDTH_MAX, data.sidebarWidth))
        }

        // workset 加载优先级：workset > favorites（旧）
        if (Array.isArray(data.workset)) {
          workset.value = normalizeWorkset(data.workset)
        } else if (Array.isArray(data.favorites)) {
          workset.value = normalizeWorkset(data.favorites)
        }

        if (Array.isArray(data.recentPages)) {
          recentPages.value = data.recentPages
            .filter((r: any) => r && typeof r.path === 'string' && !r.path.startsWith('/workhub'))
            .map((r: any) => ({
              path: r.path,
              icon: typeof r.icon === 'string' ? r.icon : '',
              label: typeof r.label === 'string' ? r.label : '',
              visitedAt: typeof r.visitedAt === 'number' ? r.visitedAt : 0,
            }))
        }
        if (data.guidanceState) {
          guidanceState.value = { ...guidanceState.value, ...data.guidanceState }
        }
      }
    } catch {
      // 解析失败时保留默认值
    }

    // 异步从服务端拉取布局偏好，有数据则覆盖本地
    fetchLayoutPreference().then((serverData) => {
      if (!serverData) return
      _suppressSave = true
      if (Array.isArray(serverData.moduleOrder)) {
        moduleOrder.value = serverData.moduleOrder
      }
      if (typeof serverData.sidebarWidth === 'number') {
        sidebarWidth.value = Math.max(SIDEBAR_WIDTH_MIN, Math.min(SIDEBAR_WIDTH_MAX, serverData.sidebarWidth))
      }
      // 服务端 workset 优先于 favorites
      if (Array.isArray(serverData.workset)) {
        workset.value = normalizeWorkset(serverData.workset)
      } else if (Array.isArray(serverData.favorites)) {
        workset.value = normalizeWorkset(serverData.favorites)
      }
      if (Array.isArray(serverData.recentPages)) {
        recentPages.value = serverData.recentPages
          .filter((r: any) => r && typeof r.path === 'string' && !r.path.startsWith('/workhub'))
          .map((r: any) => ({
            path: r.path,
            icon: typeof r.icon === 'string' ? r.icon : '',
            label: typeof r.label === 'string' ? r.label : '',
            visitedAt: typeof r.visitedAt === 'number' ? r.visitedAt : 0,
          }))
      }
      if (serverData.guidanceState) {
        guidanceState.value = { ...guidanceState.value, ...serverData.guidanceState }
      }
      try {
        localStorage.setItem(getStorageKey(), JSON.stringify(buildPersistPayload()))
      } catch { /* silent */ }
      nextTick(() => { _suppressSave = false })
    })
  }

  /** 构造 localStorage 持久化负载（兼容字段同时写入） */
  function buildPersistPayload(): SidebarConfig {
    return {
      collapsed: collapsed.value,
      moduleOrder: moduleOrder.value.length > 0 ? moduleOrder.value : undefined,
      workset: workset.value,
      // 兼容字段：旧版客户端仅识别 favorites
      favorites: workset.value.map((w) => w.path),
      recentPages: recentPages.value,
      guidanceState: guidanceState.value,
      sidebarWidth: sidebarWidth.value,
      _migrated: true,
    }
  }

  function saveToStorage() {
    try {
      localStorage.setItem(getStorageKey(), JSON.stringify(buildPersistPayload()))
    } catch {
      // 存储失败静默处理
    }
    debouncedSyncToServer()
  }

  const debouncedSyncToServer = debounce(() => {
    const data = {
      moduleOrder: moduleOrder.value,
      workset: workset.value,
      // 兼容字段：服务端仍保留旧格式可读
      favorites: workset.value.map((w) => w.path),
      recentPages: recentPages.value,
      guidanceState: guidanceState.value,
      sidebarWidth: sidebarWidth.value,
    }
    saveLayoutPreference(data)
  }, 2000)

  // 初始化加载
  loadFromStorage()
  initGuidance()

  // 监听变更自动保存（dirtyPages 不持久化）
  watch(
    () => [
      collapsed.value,
      moduleOrder.value,
      workset.value,
      recentPages.value,
      guidanceState.value,
      sidebarWidth.value,
    ] as const,
    () => { if (!_suppressSave) saveToStorage() },
    { deep: true },
  )

  // ---------- Actions ----------

  /** 切换折叠/展开 */
  function toggleCollapsed() {
    collapsed.value = !collapsed.value
  }

  /** 切换侧栏面板折叠（独立 localStorage key） */
  function toggleCollapse() {
    collapsed.value = !collapsed.value
    localStorage.setItem('sidebar-collapsed', String(collapsed.value))
  }

  /** 设置侧栏宽度（自动夹取到 [MIN, MAX] 区间） */
  function setSidebarWidth(width: number) {
    sidebarWidth.value = Math.max(SIDEBAR_WIDTH_MIN, Math.min(SIDEBAR_WIDTH_MAX, width))
  }

  /** 拖拽排序模块菜单项 */
  function reorderModules(dragCode: string, targetCode: string) {
    if (dragCode === targetCode) return
    const order = [...moduleOrder.value]
    const fromIdx = order.indexOf(dragCode)
    const toIdx = order.indexOf(targetCode)
    if (fromIdx === -1 || toIdx === -1) return
    const [moved] = order.splice(fromIdx, 1)
    order.splice(toIdx, 0, moved)
    moduleOrder.value = order
  }

  // ---------- 工作集（兼容旧 favorites API） ----------

  function addFavorite(routePath: string) {
    if (!routePath) return
    if (workset.value.some((w) => w.path === routePath)) return
    workset.value.push({ path: routePath, pinned: false })
  }

  function removeFavorite(routePath: string) {
    const idx = workset.value.findIndex((w) => w.path === routePath)
    if (idx === -1) return
    workset.value.splice(idx, 1)
  }

  function isFavorite(routePath: string): boolean {
    return workset.value.some((w) => w.path === routePath)
  }

  /**
   * 切换 pin 状态。返回 ok=false 表示因超过 PINNED_MAX 拒绝。
   * 若路径不在 workset 中，自动加入并 pin。
   */
  function togglePin(routePath: string): { ok: boolean; reason?: 'max' } {
    if (!routePath) return { ok: false }
    const item = workset.value.find((w) => w.path === routePath)
    if (!item) {
      if (pinnedItems.value.length >= PINNED_MAX) return { ok: false, reason: 'max' }
      // 新加入并 pin —— 直接置于固定区末尾
      const pinnedCount = pinnedItems.value.length
      workset.value.splice(pinnedCount, 0, { path: routePath, pinned: true })
      return { ok: true }
    }
    if (item.pinned) {
      // 取消 pin：移到非固定区开头
      const idx = workset.value.indexOf(item)
      workset.value.splice(idx, 1)
      const pinnedCount = pinnedItems.value.length
      workset.value.splice(pinnedCount, 0, { ...item, pinned: false })
      return { ok: true }
    } else {
      if (pinnedItems.value.length >= PINNED_MAX) return { ok: false, reason: 'max' }
      // 加 pin：移到固定区末尾
      const idx = workset.value.indexOf(item)
      workset.value.splice(idx, 1)
      const pinnedCount = pinnedItems.value.length
      workset.value.splice(pinnedCount, 0, { ...item, pinned: true })
      return { ok: true }
    }
  }

  /** 拖拽排序后更新整个工作集顺序（pinned 项必须保持在前） */
  function reorderWorkset(newOrder: WorksetItem[]) {
    const seen = new Set<string>()
    const cleaned: WorksetItem[] = []
    for (const item of newOrder) {
      if (!item || typeof item.path !== 'string') continue
      if (seen.has(item.path)) continue
      seen.add(item.path)
      cleaned.push({ path: item.path, pinned: !!item.pinned })
    }
    // 强制 pinned 项排在前面（保留各自相对顺序）
    const pinned = cleaned.filter((i) => i.pinned)
    const rest = cleaned.filter((i) => !i.pinned)
    workset.value = [...pinned, ...rest]
  }

  // ---------- Dirty 状态（仅会话内） ----------

  function markDirty(path: string) {
    if (!path || dirtyPages.value.has(path)) return
    const next = new Set(dirtyPages.value)
    next.add(path)
    dirtyPages.value = next
  }

  function clearDirty(path: string) {
    if (!path || !dirtyPages.value.has(path)) return
    const next = new Set(dirtyPages.value)
    next.delete(path)
    dirtyPages.value = next
  }

  function isDirty(path: string): boolean {
    return dirtyPages.value.has(path)
  }

  /**
   * 根据路由有效性清理无效收藏：传入合法路径白名单，
   * 不在白名单内的工作集项将被剔除。
   */
  function cleanupInvalidFavorites(validPaths: string[]) {
    const set = new Set(validPaths)
    workset.value = workset.value.filter((w) => set.has(w.path))
    saveToStorage()
  }

  // ---------- 最近访问 ----------

  /**
   * 记录页面访问：将路径置顶到 recentPages，最多保留 RECENT_PAGES_MAX 条。
   */
  function recordVisit(path: string, meta: { icon?: string; label?: string }) {
    if (!path) return
    // 工作台页面不记录到最近访问
    if (path === '/workhub' || path.startsWith('/workhub')) return
    const index = recentPages.value.findIndex((p) => p.path === path)
    if (index > -1) {
      recentPages.value.splice(index, 1)
    }
    recentPages.value.unshift({
      path,
      icon: meta.icon || '',
      label: meta.label || '',
      visitedAt: Date.now(),
    })
    if (recentPages.value.length > RECENT_PAGES_MAX) {
      recentPages.value.pop()
    }
    saveToStorage()
  }

  function removeRecentPage(path: string) {
    const idx = recentPages.value.findIndex((p) => p.path === path)
    if (idx > -1) {
      recentPages.value.splice(idx, 1)
      saveToStorage()
    }
  }

  function removeAllRecentExcept(path: string) {
    recentPages.value = recentPages.value.filter((p) => p.path === path)
    saveToStorage()
  }

  /** 重置侧栏状态（登出时调用） */
  function reset() {
    _suppressSave = true
    collapsed.value = false
    moduleOrder.value = []
    sidebarWidth.value = SIDEBAR_WIDTH_DEFAULT
    workset.value = []
    dirtyPages.value = new Set()
    recentPages.value = []
    guidanceState.value = { firstUseDate: null, guidanceDismissed: false, suggestionShown: false }
    nextTick(() => { _suppressSave = false })
  }

  // ---------- 新用户引导 ----------

  function initGuidance() {
    if (!guidanceState.value.firstUseDate) {
      guidanceState.value.firstUseDate = new Date().toISOString().slice(0, 10)
    }
  }

  const daysSinceFirstUse = computed(() => {
    if (!guidanceState.value.firstUseDate) return 0
    const first = new Date(guidanceState.value.firstUseDate).getTime()
    const now = Date.now()
    return Math.floor((now - first) / (24 * 60 * 60 * 1000))
  })

  const shouldShowHint = computed(() => {
    const days = daysSinceFirstUse.value
    return days >= 0 && days <= 7 && !guidanceState.value.guidanceDismissed
  })

  const shouldShowSuggestion = computed(() => {
    return (
      daysSinceFirstUse.value >= 8 &&
      !guidanceState.value.suggestionShown &&
      !guidanceState.value.guidanceDismissed
    )
  })

  function dismissGuidance() {
    guidanceState.value.guidanceDismissed = true
  }

  function acceptSuggestion() {
    guidanceState.value.suggestionShown = true
  }

  function declineSuggestion() {
    guidanceState.value.suggestionShown = true
  }

  return {
    // State
    collapsed,
    moduleOrder,
    sidebarWidth,
    workset,
    dirtyPages,
    recentPages,
    guidanceState,
    // Getters
    favorites,
    pinnedItems,
    unpinnedItems,
    pinnedFull,
    daysSinceFirstUse,
    shouldShowHint,
    shouldShowSuggestion,
    // Actions
    toggleCollapsed,
    toggleCollapse,
    setSidebarWidth,
    reorderModules,
    addFavorite,
    removeFavorite,
    isFavorite,
    togglePin,
    reorderWorkset,
    markDirty,
    clearDirty,
    isDirty,
    cleanupInvalidFavorites,
    recordVisit,
    removeRecentPage,
    removeAllRecentExcept,
    reset,
    initGuidance,
    dismissGuidance,
    acceptSuggestion,
    declineSuggestion,
    loadFromStorage,
  }
})
