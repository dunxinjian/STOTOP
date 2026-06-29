import { ref, onUnmounted } from 'vue'
import {
  getDashboardOverview,
  getByStatus,
  getByModule,
  getByAssignee,
  getTrend,
  getOverdueItems,
  type DashboardOverview,
  type StatusGroup,
  type ModuleGroup,
  type AssigneeStats,
  type TrendData,
  type OverduePage,
} from '@/api/workflow'

export function useDashboard() {
  const loading = ref(false)
  const overview = ref<DashboardOverview>()
  const statusGroups = ref<StatusGroup[]>([])
  const moduleGroups = ref<ModuleGroup[]>([])
  const assigneeStats = ref<AssigneeStats[]>([])
  const trend = ref<TrendData>()
  const overduePage = ref<OverduePage>()

  let refreshTimer: ReturnType<typeof setInterval> | null = null

  const loadOverview = async () => {
    try {
      overview.value = await getDashboardOverview()
    } catch (e) {
      console.error('[Dashboard] loadOverview failed:', e)
    }
  }

  const loadStatusGroups = async () => {
    try {
      statusGroups.value = await getByStatus()
    } catch (e) {
      console.error('[Dashboard] loadStatusGroups failed:', e)
    }
  }

  const loadModuleGroups = async () => {
    try {
      moduleGroups.value = await getByModule()
    } catch (e) {
      console.error('[Dashboard] loadModuleGroups failed:', e)
    }
  }

  const loadAssigneeStats = async () => {
    try {
      assigneeStats.value = await getByAssignee()
    } catch (e) {
      console.error('[Dashboard] loadAssigneeStats failed:', e)
    }
  }

  const loadTrend = async (days = 7) => {
    try {
      trend.value = await getTrend(days)
    } catch (e) {
      console.error('[Dashboard] loadTrend failed:', e)
    }
  }

  const loadOverdueItems = async (page = 1, pageSize = 20) => {
    try {
      overduePage.value = await getOverdueItems(page, pageSize)
    } catch (e) {
      console.error('[Dashboard] loadOverdueItems failed:', e)
    }
  }

  const loadAll = async () => {
    loading.value = true
    try {
      await Promise.all([
        loadOverview(),
        loadStatusGroups(),
        loadModuleGroups(),
        loadAssigneeStats(),
        loadTrend(),
        loadOverdueItems(),
      ])
    } finally {
      loading.value = false
    }
  }

  const startAutoRefresh = (intervalMs = 60000) => {
    stopAutoRefresh()
    refreshTimer = setInterval(() => {
      loadAll()
    }, intervalMs)
  }

  const stopAutoRefresh = () => {
    if (refreshTimer) {
      clearInterval(refreshTimer)
      refreshTimer = null
    }
  }

  onUnmounted(() => {
    stopAutoRefresh()
  })

  return {
    loading,
    overview,
    statusGroups,
    moduleGroups,
    assigneeStats,
    trend,
    overduePage,
    loadOverview,
    loadStatusGroups,
    loadModuleGroups,
    loadAssigneeStats,
    loadTrend,
    loadOverdueItems,
    loadAll,
    startAutoRefresh,
    stopAutoRefresh,
  }
}
