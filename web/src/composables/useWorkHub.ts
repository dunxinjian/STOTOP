/**
 * useWorkHub — WorkHub Feed 流 + SignalR 实时更新 composable
 *
 * 功能：
 *   - 连接 /hubs/workhub 进行实时推送
 *   - 初始数据通过 HTTP 加载，后续通过 SignalR 增量更新
 *   - 支持来源/优先级/时间范围筛选
 *   - 暴露 newItemsCount 以驱动"有N条新消息"提示条
 *   - 维护当前选中项 selectedItemId，提供上一条/下一条导航
 *
 * 注：本 composable 采用模块级单例模式，多次调用 useWorkHub() 返回的是同一份共享状态，
 * 以便 index.vue / WorkHubCenter / WorkHubDetail 等多个组件协同共享选中项与列表数据。
 */
import { ref, computed, type Ref } from 'vue'
import { HubConnectionState } from '@microsoft/signalr'
import { createSignalRConnection, type SignalRManager } from '@/utils/signalr'
import {
  getWorkItems,
  getWorkHubStats,
  getWorkItemsWithStats,
  executeWorkItemAction,
  type WorkItem,
  type WorkItemAction,
  type WorkHubStats,
} from '@/api/workhub'

// ===== 筛选器类型 =====
export interface WorkHubFilters {
  sources: WorkItem['source'][]
  priority: WorkItem['priority'] | ''
  /** 工作项分类（待办页四类纵向页签：流程=approval / 任务=task / 异常=alert / 通知=notification） */
  category: WorkItem['category'] | ''
  dateRange: [string, string] | null
}

const DEFAULT_FILTERS: WorkHubFilters = {
  sources: [],
  priority: '',
  category: '',
  dateRange: null,
}

// ===== 模块级共享状态 =====
const items = ref<WorkItem[]>([])
const pendingItems = ref<WorkItem[]>([])
const newItemsCount = computed(() => pendingItems.value.length)

// ===== 终结性归档 + 稍后处理 =====
/** 已归档列表（终结性操作完成后自动归入） */
const archivedItems = ref<WorkItem[]>([])
/** 延后列表（用户主动稍后处理的项） */
const deferredItems = ref<WorkItem[]>([])

/** 将工作项归入「已归档」（不再从视图完全消失） */
function archiveItem(item: WorkItem) {
  items.value = items.value.filter(i => i.id !== item.id)
  const archived = { ...item, status: 'archived' } as WorkItem & { status: string }
  archivedItems.value = [archived, ...archivedItems.value]
}

/** 稍后处理：从活跃列表移到延后区 */
function deferItem(itemId: string) {
  const item = items.value.find(i => i.id === itemId)
  if (!item) return
  items.value = items.value.filter(i => i.id !== itemId)
  if (selectedItemId.value === itemId) {
    selectedItemId.value = null
  }
  // 去重，避免重复延后
  if (!deferredItems.value.find(i => i.id === itemId)) {
    deferredItems.value = [...deferredItems.value, item]
  }
}

/** 恢复延后项：从延后区放回活跃列表顶部 */
function restoreDeferred(itemId: string) {
  const item = deferredItems.value.find(i => i.id === itemId)
  if (!item) return
  deferredItems.value = deferredItems.value.filter(i => i.id !== itemId)
  if (!items.value.find(i => i.id === itemId)) {
    items.value = [item, ...items.value]
  }
}

const stats = ref<WorkHubStats>({
  total: 0,
  approval: 0,
  task: 0,
  alert: 0,
  notification: 0,
  reminder: 0,
  initiated: 0,
})

const loading = ref(false)
const statsLoading = ref(false)
const isConnected = ref(false)

const currentPage = ref(1)
const pageSize = ref(15)
const totalCount = ref(0)
const hasMore = computed(() => items.value.length < totalCount.value)

const filters = ref<WorkHubFilters>({ ...DEFAULT_FILTERS })

// ===== 选中项与导航 =====
/** 当前选中项 ID（用于跨组件共享、键盘导航） */
const selectedItemId = ref<string | null>(null)

/** 当前选中项在列表中的索引（找不到返回 -1） */
const currentIndex = computed(() => {
  if (!selectedItemId.value) return -1
  return items.value.findIndex(i => i.id === selectedItemId.value)
})

/** 列表总数 */
const totalItems = computed(() => items.value.length)

/** 导航到下一条 */
function navigateNext() {
  if (items.value.length === 0) return
  const idx = currentIndex.value
  if (idx < 0) {
    selectedItemId.value = items.value[0].id
    return
  }
  if (idx < items.value.length - 1) {
    selectedItemId.value = items.value[idx + 1].id
  }
}

/** 导航到上一条 */
function navigatePrev() {
  if (items.value.length === 0) return
  const idx = currentIndex.value
  if (idx > 0) {
    selectedItemId.value = items.value[idx - 1].id
  }
}

/** SignalR 连接管理器（模块级，确保多组件复用同一连接） */
let manager: SignalRManager | null = null

/** 标记初始数据是否已加载（防止 SignalR 首次连接时重复 init） */
let initialDataLoaded = false

/** 是否已成功连接过 SignalR（用于区分首次连接与断线重连，仅重连时才需要重新拉数据） */
let hasConnectedBefore = false

/** 防抖定时器 */
let statsDebounceTimer: ReturnType<typeof setTimeout> | null = null

/** 上次 StatsUpdated 对账兜底的时间戳（节流用，Date.now 毫秒） */
let lastStatsSync = 0
/** StatsUpdated 对账兜底最小间隔（毫秒） */
const STATS_SYNC_THROTTLE_MS = 8000

// ===== 内部工具方法 =====
function debouncedFetchStats() {
  if (statsDebounceTimer) clearTimeout(statsDebounceTimer)
  statsDebounceTimer = setTimeout(() => {
    fetchStats()
    statsDebounceTimer = null
  }, 600)
}

/**
 * 对账兜底：仅在页面可见 + 8s 节流窗口外才全量重拉一次 stats，收敛本地增量的漂移。
 * StatsUpdated 推送与"移除时找不到 category"的兜底共用，避免高频推送/移除触发全量重拉。
 */
function throttledStatsSync() {
  if (typeof document !== 'undefined' && document.visibilityState !== 'visible') return
  const now = Date.now()
  if (now - lastStatsSync < STATS_SYNC_THROTTLE_MS) return
  lastStatsSync = now
  fetchStats()
}

/**
 * 本地增量维护角标：即时更新 UI，避免每次推送/操作都全量重拉后端 8 源统计。
 * category 为工作项分类（approval/task/alert/notification/reminder/initiated），
 * 与 stats 的同名数字字段一一对应；delta 为 +1（新增）/ -1（移除）。
 * 用 Math.max(0, …) 兜底，杜绝负数。
 */
function adjustStat(category: WorkItem['category'] | '' | undefined, delta: number) {
  if (!category) return
  const s = stats.value
  if (typeof s[category] === 'number') {
    s[category] = Math.max(0, s[category] + delta)
  }
  s.total = Math.max(0, s.total + delta)
}

function buildParams(): Parameters<typeof getWorkItems>[0] {
  const params: Parameters<typeof getWorkItems>[0] = {
    page: currentPage.value,
    pageSize: pageSize.value,
  }
  if (filters.value.sources.length > 0) {
    params.sources = filters.value.sources.join(',')
  }
  if (filters.value.priority) {
    params.priority = filters.value.priority
  }
  if (filters.value.category) {
    params.category = filters.value.category
  }
  if (filters.value.dateRange) {
    params.startDate = filters.value.dateRange[0]
    params.endDate = filters.value.dateRange[1]
  }
  return params
}

// ===== 数据加载 =====

/** 初始/刷新加载（替换列表） */
async function fetchItems(resetPage = false) {
  if (resetPage) {
    currentPage.value = 1
  }
  loading.value = true
  try {
    const result = await getWorkItems(buildParams())
    if (result) {
      items.value = result.items || []
      totalCount.value = result.total || 0
    }
  } catch (e) {
    console.warn('[useWorkHub] fetchItems 失败', e)
  } finally {
    loading.value = false
  }
}

/** 加载更多（追加到列表末尾） */
async function loadMore() {
  if (!hasMore.value || loading.value) return
  currentPage.value += 1
  loading.value = true
  try {
    const result = await getWorkItems(buildParams())
    if (result) {
      items.value.push(...(result.items || []))
      totalCount.value = result.total || 0
    }
  } catch (e) {
    console.warn('[useWorkHub] loadMore 失败', e)
    currentPage.value -= 1
  } finally {
    loading.value = false
  }
}

/** 加载统计信息 */
async function fetchStats() {
  statsLoading.value = true
  try {
    const result = await getWorkHubStats()
    if (result) {
      stats.value = result
    }
  } catch (e) {
    console.warn('[useWorkHub] fetchStats 失败', e)
  } finally {
    statsLoading.value = false
  }
}

/** 初始化：合并接口一次加载列表 + 统计（列表按当前 category 过滤，统计为全类角标） */
async function init() {
  loading.value = true
  try {
    const result = await getWorkItemsWithStats({
      page: 1,
      pageSize: pageSize.value,
      category: filters.value.category || undefined,
    })
    if (result) {
      items.value = result.items?.items || []
      totalCount.value = result.items?.total || 0
      currentPage.value = 1
      if (result.stats) {
        stats.value = result.stats
      }
    }
  } catch (e) {
    console.warn('[useWorkHub] init 失败，回退到分离调用', e)
    await Promise.allSettled([fetchItems(true), fetchStats()])
  } finally {
    loading.value = false
  }
  initialDataLoaded = true
}

/** 将待展示的新项追加到列表顶部（用户点击"查看新消息"时调用） */
function flushPendingItems() {
  if (pendingItems.value.length === 0) return
  items.value.unshift(...pendingItems.value)
  totalCount.value += pendingItems.value.length
  pendingItems.value = []
}

/** 忽略待展示的新项（不感兴趣） */
function dismissPendingItems() {
  pendingItems.value = []
}

// ===== 筛选器操作 =====

// 注：stats 是全类全量统计、不受任何筛选参数影响，因此切换筛选只需重取列表；
// 角标数字由本地增量 adjustStat 即时维护，低频 fetchStats 对账兜底收敛漂移。
function setFilter<K extends keyof WorkHubFilters>(key: K, value: WorkHubFilters[K]) {
  filters.value[key] = value
  fetchItems(true)
}

function resetFilters() {
  filters.value = { ...DEFAULT_FILTERS }
  fetchItems(true)
}

// ===== 工作项操作 =====

async function handleAction(itemId: string, actionKey: string) {
  await executeWorkItemAction(itemId, actionKey)
  // 从列表中移除已处理的项（移除前取其 category 以本地增量减角标）
  const removed = items.value.find(i => i.id === itemId)
  items.value = items.value.filter(i => i.id !== itemId)
  totalCount.value = Math.max(0, totalCount.value - 1)
  // 若移除的是当前选中项，清空选中
  if (selectedItemId.value === itemId) {
    selectedItemId.value = null
  }
  adjustStat(removed?.category, -1)
}

// ===== 可逆性分级安全机制 =====

/** Undo 队列项 */
export interface PendingAction {
  id: string
  itemId: string
  actionKey: string
  label: string
  timer: ReturnType<typeof setTimeout>
  rollback: () => void
}

/** 待提交的撤销队列（5 秒延迟） */
const pendingActions = ref<PendingAction[]>([])

/** 二次确认弹窗状态（不可逆操作专用） */
export interface ConfirmDialogState {
  visible: boolean
  item: WorkItem
  action: WorkItemAction
  summaryLines: string[]
}
const confirmDialog = ref<ConfirmDialogState | null>(null)

/** 真正提交到后端：调用 API 并更新统计 */
async function commitAction(item: WorkItem, action: WorkItemAction) {
  try {
    await executeWorkItemAction(item.id, action.key)
    totalCount.value = Math.max(0, totalCount.value - 1)
    // 终结性操作：归入「已归档」折叠区，而非彻底从视图消失
    if (action.finalizes === true) {
      archiveItem(item)
    }
    // 角标已在 executeAction 乐观移除时 adjustStat(-1)，此处不再重复减
  } catch (e) {
    console.warn('[useWorkHub] commitAction 失败', e)
  }
}

/** 从 pending 队列中移除指定项 */
function removePending(id: string) {
  pendingActions.value = pendingActions.value.filter(p => p.id !== id)
}

/**
 * 操作执行统一入口：根据可逆性分流
 * - needsConfirm=true：弹出二次确认（不可逆）
 * - 否则：乐观更新 + 5 秒撤销窗口（可逆）
 */
function executeAction(item: WorkItem, action: WorkItemAction) {
  if (action.needsConfirm) {
    confirmDialog.value = {
      visible: true,
      item,
      action,
      summaryLines: action.confirmSummary || [],
    }
    return
  }

  // 可逆操作：乐观更新 + 5 秒 Undo 窗口
  const originalItems = [...items.value]
  const originalSelectedId = selectedItemId.value
  items.value = items.value.filter(i => i.id !== item.id)
  if (selectedItemId.value === item.id) {
    selectedItemId.value = null
  }
  // 乐观移除即刻减角标；撤销时在 rollback 里加回
  adjustStat(item.category, -1)

  const pendingId = `${item.id}-${Date.now()}`
  const timer = setTimeout(() => {
    commitAction(item, action)
    removePending(pendingId)
  }, 5000)

  pendingActions.value.push({
    id: pendingId,
    itemId: item.id,
    actionKey: action.key,
    label: `已${action.label}「${item.title}」`,
    timer,
    rollback: () => {
      items.value = originalItems
      selectedItemId.value = originalSelectedId
      // 恢复列表的同时加回角标
      adjustStat(item.category, +1)
    },
  })
}

/** 撤销指定的 pending 操作 */
function undoAction(pendingId: string) {
  const pending = pendingActions.value.find(p => p.id === pendingId)
  if (!pending) return
  clearTimeout(pending.timer)
  pending.rollback()
  removePending(pendingId)
}

/** 确认执行不可逆操作 */
function confirmAction() {
  if (!confirmDialog.value) return
  const { item, action } = confirmDialog.value
  // 不可逆操作：直接从列表移除并提交（无 Undo）
  items.value = items.value.filter(i => i.id !== item.id)
  if (selectedItemId.value === item.id) {
    selectedItemId.value = null
  }
  // 移除即减角标（无 Undo，commitAction 内不再重复减）
  adjustStat(item.category, -1)
  commitAction(item, action)
  confirmDialog.value = null
}

/** 取消二次确认 */
function cancelConfirm() {
  confirmDialog.value = null
}

// ===== 显式多选模式 =====
/** 是否处于多选模式 */
const isMultiSelectMode = ref(false)
/** 已选中的工作项 ID 集合 */
const selectedItemIds = ref<Set<string>>(new Set())

/** 进入多选模式 */
function enterMultiSelect() {
  isMultiSelectMode.value = true
  selectedItemIds.value = new Set()
}

/** 退出多选模式（清空已选） */
function exitMultiSelect() {
  isMultiSelectMode.value = false
  selectedItemIds.value = new Set()
}

/** 切换某项的勾选状态 */
function toggleSelectItem(itemId: string) {
  const set = new Set(selectedItemIds.value)
  if (set.has(itemId)) {
    set.delete(itemId)
  } else {
    set.add(itemId)
  }
  selectedItemIds.value = set
}

/** 全选当前列表 */
function selectAll() {
  selectedItemIds.value = new Set(items.value.map(i => i.id))
}

/** 取消全选 */
function deselectAll() {
  selectedItemIds.value = new Set()
}

/**
 * 批量执行操作：对所有已选项依次走 executeAction 通用入口。
 * 通用入口会根据 needsConfirm 自动分流为「二次确认」或「乐观更新+Undo」。
 * 执行后退出多选模式。
 */
function batchExecuteAction(action: WorkItemAction) {
  const ids = selectedItemIds.value
  const targets = items.value.filter(i => ids.has(i.id))
  targets.forEach(item => {
    executeAction(item, action)
  })
  exitMultiSelect()
}

/** 页面离开前 flush：立即提交所有 pending（同步 / 兜底） */
if (typeof window !== 'undefined') {
  window.addEventListener('beforeunload', () => {
    pendingActions.value.forEach(p => {
      clearTimeout(p.timer)
      // 兜底提交（fire-and-forget；浏览器可能在卸载期间中止）
      try {
        executeWorkItemAction(p.itemId, p.actionKey)
      } catch {
        // ignore
      }
    })
    pendingActions.value = []
  })
}

// ===== SignalR =====

/** 启动 SignalR 连接 */
async function connect(userId: number | string) {
  if (manager && manager.connection.state !== HubConnectionState.Disconnected) return

  manager = createSignalRConnection({
    url: '/hubs/workhub',
    onStateChange: (state) => {
      isConnected.value = state === HubConnectionState.Connected
      if (state === HubConnectionState.Connected) {
        // 仅断线重连时刷新数据（首次连接由页面 init() 已完成加载，勿重复拉取）
        if (hasConnectedBefore && initialDataLoaded) {
          init()
        }
        hasConnectedBefore = true
      }
    },
  })

  const conn = manager.connection

  // 统计更新：后端「数字变了」信号。不再直接全量重拉，
  // 改为节流对账兜底——仅页面可见且距上次对账 ≥ 阈值时才拉一次，收敛本地增量漂移。
  conn.on('StatsUpdated', throttledStatsSync)

  // 新工作项到达：推入待展示队列而非直接插入列表；本地增量加角标
  conn.on('WorkItemAdded', (item: WorkItem) => {
    // 避免重复
    if (!pendingItems.value.find(i => i.id === item.id) && !items.value.find(i => i.id === item.id)) {
      pendingItems.value.unshift(item)
    }
    adjustStat(item.category, +1)
  })

  // 移除工作项：先从列表/待展示队列按 id 找 category 再本地减；找不到则退化为对账兜底
  conn.on('WorkItemRemoved', (itemId: string) => {
    const known = items.value.find(i => i.id === itemId) ?? pendingItems.value.find(i => i.id === itemId)
    items.value = items.value.filter(i => i.id !== itemId)
    pendingItems.value = pendingItems.value.filter(i => i.id !== itemId)
    totalCount.value = Math.max(0, totalCount.value - 1)
    if (selectedItemId.value === itemId) {
      selectedItemId.value = null
    }
    if (known) {
      adjustStat(known.category, -1)
    } else {
      // 跨类项不在当前列表，无法确定 category，退化为低频对账兜底，不瞎减
      throttledStatsSync()
    }
  })

  // 更新工作项
  conn.on('WorkItemUpdated', (updated: WorkItem) => {
    const idx = items.value.findIndex(i => i.id === updated.id)
    if (idx >= 0) {
      items.value[idx] = updated
    }
  })

  // 工作项状态变更：终态（已完成/已取消）从待办列表移除；本地增量减角标，找不到 category 则对账兜底
  conn.on('WorkItemStatusChanged', (payload: { id: string; status: string; source: string }) => {
    if (payload.status === 'completed' || payload.status === 'cancelled') {
      const known = items.value.find(i => i.id === payload.id) ?? pendingItems.value.find(i => i.id === payload.id)
      items.value = items.value.filter(i => i.id !== payload.id)
      pendingItems.value = pendingItems.value.filter(i => i.id !== payload.id)
      totalCount.value = Math.max(0, totalCount.value - 1)
      if (selectedItemId.value === payload.id) {
        selectedItemId.value = null
      }
      if (known) {
        adjustStat(known.category, -1)
      } else {
        // 跨类项不在当前列表，无法确定 category，退化为低频对账兜底，不瞎减
        fetchStats()
      }
    }
    // 非终态变更（如仅状态流转）不触碰角标
  })

  try {
    await manager.start()
    await conn.invoke('JoinUserChannel', String(userId))
  } catch (err) {
    console.warn('[useWorkHub] SignalR 初始连接失败，将自动重试', err)
  }
}

/** 断开 SignalR 连接 */
async function disconnect() {
  if (statsDebounceTimer) { clearTimeout(statsDebounceTimer); statsDebounceTimer = null }
  if (manager) {
    await manager.stop()
    manager = null
    isConnected.value = false
  }
}

/**
 * 返回模块级共享状态与方法。
 * 注意：不再注册 onUnmounted(disconnect)，连接生命周期由显式调用方（如 WorkHubCenter）管理，
 * 避免多组件场景下因任一组件卸载就提前断开连接。
 */
export function useWorkHub() {
  return {
    // 数据
    items,
    stats,
    loading,
    statsLoading,
    isConnected,
    // 分页
    currentPage,
    pageSize,
    totalCount,
    hasMore,
    // 新消息提示
    newItemsCount,
    pendingItems,
    flushPendingItems,
    dismissPendingItems,
    // 筛选
    filters,
    setFilter,
    resetFilters,
    // 操作
    init,
    fetchItems,
    loadMore,
    fetchStats,
    handleAction,
    // 可逆性分级安全机制
    pendingActions,
    confirmDialog,
    executeAction,
    undoAction,
    confirmAction,
    cancelConfirm,
    // 显式多选 + 批量操作
    isMultiSelectMode,
    selectedItemIds,
    enterMultiSelect,
    exitMultiSelect,
    toggleSelectItem,
    selectAll,
    deselectAll,
    batchExecuteAction,
    // 终结性归档 + 稍后处理
    archivedItems,
    deferredItems,
    archiveItem,
    deferItem,
    restoreDeferred,
    // SignalR
    connect,
    disconnect,
    // 选中项与导航
    selectedItemId,
    currentIndex,
    totalItems,
    navigateNext,
    navigatePrev,
  }
}

// 类型导出（便于消费方做类型标注）
export type UseWorkHubReturn = ReturnType<typeof useWorkHub>
export type SelectedItemIdRef = Ref<string | null>
