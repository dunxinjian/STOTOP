import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export interface VisitRecord {
  path: string
  title: string
  icon?: string
  moduleCode?: string
  visitCount: number
  lastVisitTime: number  // timestamp
  visitDays?: number[]   // 最近 30 次访问的星期几 (0-6), 用于时间模式分析
}

export interface NavigationTransition {
  from: string
  to: string
  count: number
  lastTime: number
}

const STORAGE_KEY = 'app_visit_records'
const NAV_TRANSITIONS_KEY = 'app_nav_transitions'
const MAX_RECORDS = 50
const MAX_RECENT = 8
const MAX_FREQUENT = 8

export const useRecommendationStore = defineStore('recommendation', () => {
  const visitRecords = ref<VisitRecord[]>([])
  const navigationTransitions = ref<NavigationTransition[]>([])
  const lastVisitedPath = ref<string | null>(null)

  /** 从 localStorage 加载 */
  function loadFromStorage() {
    try {
      const data = localStorage.getItem(STORAGE_KEY)
      if (data) visitRecords.value = JSON.parse(data)
    } catch { /* 静默 */ }
    try {
      const navData = localStorage.getItem(NAV_TRANSITIONS_KEY)
      if (navData) navigationTransitions.value = JSON.parse(navData)
    } catch { /* 静默 */ }
  }

  /** 保存到 localStorage */
  function saveToStorage() {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(visitRecords.value))
    } catch { /* 静默 */ }
    try {
      localStorage.setItem(NAV_TRANSITIONS_KEY, JSON.stringify(navigationTransitions.value))
    } catch { /* 静默 */ }
    // TODO: 后端持久化同步——待后端 API 就绪后，通过防抖 PUT /user/visit-records 将 visitRecords 同步到服务端
  }

  /** 记录一次页面访问 */
  function recordVisit(path: string, title: string, icon?: string, moduleCode?: string) {
    // 跳过不记录的路径
    if (['/login', '/403', '/404', '/workhub', '/'].includes(path)) return
    if (!title) return

    const existing = visitRecords.value.find(r => r.path === path)
    if (existing) {
      existing.visitCount++
      existing.lastVisitTime = Date.now()
      existing.title = title  // 更新标题
      // 追加星期记录（时间模式分析用）
      if (!existing.visitDays) existing.visitDays = []
      existing.visitDays.push(new Date().getDay())
      if (existing.visitDays.length > 30) {
        existing.visitDays = existing.visitDays.slice(-30)
      }
    } else {
      visitRecords.value.push({
        path,
        title,
        icon,
        moduleCode,
        visitCount: 1,
        lastVisitTime: Date.now(),
        visitDays: [new Date().getDay()],
      })
    }

    // 限制总数
    if (visitRecords.value.length > MAX_RECORDS) {
      visitRecords.value.sort((a, b) => b.lastVisitTime - a.lastVisitTime)
      visitRecords.value = visitRecords.value.slice(0, MAX_RECORDS)
    }

    // 记录导航转换（from → to）
    if (lastVisitedPath.value && lastVisitedPath.value !== path) {
      const trans = navigationTransitions.value.find(
        t => t.from === lastVisitedPath.value && t.to === path
      )
      if (trans) {
        trans.count++
        trans.lastTime = Date.now()
      } else {
        navigationTransitions.value.push({
          from: lastVisitedPath.value!,
          to: path,
          count: 1,
          lastTime: Date.now(),
        })
      }
      // 限制总量（保留高频 top 200）
      if (navigationTransitions.value.length > 200) {
        navigationTransitions.value.sort((a, b) => b.count - a.count)
        navigationTransitions.value = navigationTransitions.value.slice(0, 200)
      }
    }
    lastVisitedPath.value = path

    saveToStorage()
  }

  /** 最近访问（按最后访问时间排序） */
  const recentPages = computed(() => {
    return [...visitRecords.value]
      .sort((a, b) => b.lastVisitTime - a.lastVisitTime)
      .slice(0, MAX_RECENT)
  })

  /** 常用功能（按访问次数排序） */
  const frequentPages = computed(() => {
    return [...visitRecords.value]
      .sort((a, b) => b.visitCount - a.visitCount)
      .slice(0, MAX_FREQUENT)
  })

  // 初始化加载
  loadFromStorage()

  return { visitRecords, navigationTransitions, lastVisitedPath, recentPages, frequentPages, recordVisit, loadFromStorage }
})
