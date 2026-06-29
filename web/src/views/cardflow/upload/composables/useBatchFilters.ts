/**
 * useBatchFilters — 筛选/排序/统计逻辑
 *
 * 职责：
 *   - 日期筛选、状态筛选、业务类型筛选
 *   - 角色 KPI 计算
 *   - 展示列表排序（按角色）
 *   - 纯 computed 过滤，切换筛选不触发网络请求
 */
import { ref, reactive, computed } from 'vue'
import { useBatchStore } from '@/stores/batchStore'
import type { BatchItem, BatchStatus } from '@/stores/batchStore'
import { getImportOverview } from '@/api/cardflow'

// ===== 类型定义 =====

/** 角色类型 */
export type RoleType = 'uploader' | 'processor' | 'manager'

/** 视图模式 */
export type ViewMode = 'list' | 'board'

/** 筛选状态 */
export interface FilterState {
  status: BatchStatus | 'all'
  bizType: string
  timeRange: 'today' | 'week' | 'month'
}

/** KPI 条目 */
export interface KpiItem {
  dot: string
  value: string | number
  label: string
}

/** KPI 统计 */
export interface WorkItemStats {
  todayUpload: number
  processing: number
  pending: number
  completed: number
  error: number
  avgDuration: string
  urgent: number
  stale: number
  efficiency: string
}

export function useBatchFilters(currentUserName: () => string) {
  const store = useBatchStore()

  // ===== 状态 =====
  const filters = ref<FilterState>({
    status: 'all',
    bizType: 'all',
    timeRange: 'today',
  })

  const currentRole = ref<RoleType>('uploader')
  const viewMode = ref<ViewMode>('list')

  const stats = reactive<WorkItemStats>({
    todayUpload: 0,
    processing: 0,
    pending: 0,
    completed: 0,
    error: 0,
    avgDuration: '-',
    urgent: 0,
    stale: 0,
    efficiency: '-',
  })

  // ===== 筛选 computed =====

  const filteredBatches = computed(() => {
    let result = store.batches
    const f = filters.value
    if (f.status !== 'all') {
      if (f.status === 'processing') {
        result = result.filter(b => b.status === 'uploading' || b.status === 'processing')
      } else {
        result = result.filter(b => b.status === f.status)
      }
    }
    if (f.bizType !== 'all') {
      result = result.filter(b => b.bizType === f.bizType)
    }
    return result
  })

  /** 按角色排序的展示列表（最新批次排最前） */
  const displayBatches = computed(() => {
    const list = [...filteredBatches.value]
    if (currentRole.value === 'processor') {
      const userName = currentUserName()
      const mine = list.filter(b => b.assigneeName === userName)
      const others = list.filter(b => b.assigneeName !== userName)
      // 各组内按创建时间降序
      const sortByTime = (a: BatchItem, b: BatchItem) =>
        new Date(b.createTime).getTime() - new Date(a.createTime).getTime()
      return [...mine.sort(sortByTime), ...others.sort(sortByTime)]
    }
    // 默认按创建时间降序，新批次排最前
    return list.sort((a, b) => new Date(b.createTime).getTime() - new Date(a.createTime).getTime())
  })

  // ===== 角色 KPI =====

  const roleKpi = computed<KpiItem[]>(() => {
    if (currentRole.value === 'uploader') {
      return [
        { dot: 'blue', value: stats.todayUpload, label: '我的上传' },
        { dot: 'blue-pulse', value: stats.processing, label: '处理中' },
        { dot: 'orange', value: stats.pending, label: '待确认' },
        { dot: 'green', value: stats.completed, label: '已完成' },
      ]
    }
    if (currentRole.value === 'processor') {
      return [
        { dot: 'blue', value: stats.pending, label: '我的待办' },
        { dot: 'red', value: stats.urgent, label: '紧急' },
        { dot: 'green', value: stats.completed, label: '今日已处理' },
        { dot: 'purple', value: stats.avgDuration, label: '平均处理时长' },
      ]
    }
    // manager
    return [
      { dot: 'blue', value: stats.todayUpload, label: '今日总计' },
      { dot: 'blue-pulse', value: stats.processing, label: '处理中' },
      { dot: 'red', value: stats.stale, label: '停滞预警' },
      { dot: 'green', value: stats.completed, label: '已完成' },
      { dot: 'purple', value: stats.efficiency, label: '人效（人均）' },
    ]
  })

  // ===== 工具函数 =====

  /** 根据 timeRange 计算日期区间 */
  function getDateRange(timeRange: string): { startDate?: string; endDate?: string } {
    const now = new Date()
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
    let startDate: Date | undefined
    let endDate: Date | undefined

    switch (timeRange) {
      case 'today':
        startDate = today
        endDate = new Date(today.getTime() + 86400000)
        break
      case 'week': {
        const day = today.getDay() || 7
        startDate = new Date(today.getTime() - (day - 1) * 86400000)
        endDate = new Date(startDate.getTime() + 7 * 86400000)
        break
      }
      case 'month':
        startDate = new Date(today.getFullYear(), today.getMonth(), 1)
        endDate = new Date(today.getFullYear(), today.getMonth() + 1, 1)
        break
      default:
        return {}
    }

    const format = (d: Date) => d.toISOString().split('T')[0]
    return { startDate: format(startDate), endDate: format(endDate) }
  }

  /** 加载统计 */
  async function loadStats() {
    try {
      const overview = await getImportOverview()
      if (overview) {
        stats.todayUpload = overview.todayBatchCount || 0
        stats.processing = overview.processingTaskCount || 0
        stats.pending = overview.pendingExceptionCount || 0
        stats.completed = Math.round(
          (overview.todayBatchCount || 0) * (overview.successRate || 0) / 100,
        )
        stats.error = overview.pendingExceptionCount || 0
      }
    } catch (e) {
      console.warn('[useBatchFilters] loadStats 失败', e)
    }
  }

  return {
    filters,
    currentRole,
    viewMode,
    stats,
    filteredBatches,
    displayBatches,
    roleKpi,
    getDateRange,
    loadStats,
  }
}
