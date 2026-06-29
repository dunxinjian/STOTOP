/**
 * 智能推荐引擎
 * 纯算法模块，通过参数接收数据，无 store 依赖
 */

import type { NavigationTransition, VisitRecord } from '@/stores/recommendation'

export interface RecommendationItem {
  path: string
  title: string
  icon?: string
  moduleCode?: string
  score: number       // 0-1 归一化分数
  reason: 'chain' | 'time' | 'frequency'
}

const NINETY_DAYS = 90 * 24 * 60 * 60 * 1000

/**
 * 行为链推荐：基于当前页面，找出"之后用户最常去的页面"
 * @param transitions 全部导航转换记录
 * @param currentPath 当前路由路径
 * @param visitRecords 用于获取 title/icon 信息
 * @returns 推荐结果（按 count 降序，最多 5 项）
 */
export function analyzeChains(
  transitions: NavigationTransition[],
  currentPath: string,
  visitRecords: VisitRecord[]
): RecommendationItem[] {
  if (!currentPath || !transitions.length) return []

  const now = Date.now()

  // 1. 筛选 from === currentPath 且 90 天内有发生的转换
  const relevant = transitions.filter(
    t => t.from === currentPath && (now - t.lastTime) <= NINETY_DAYS
  )
  if (!relevant.length) return []

  // 2. 按 count 降序取 top 5
  relevant.sort((a, b) => b.count - a.count)
  const top = relevant.slice(0, 5)

  // 3. 归一化分数
  const maxCount = top[0].count
  
  // 4. 从 visitRecords 补充 title/icon/moduleCode
  return top.map(t => {
    const record = visitRecords.find(r => r.path === t.to)
    return {
      path: t.to,
      title: record?.title || t.to,
      icon: record?.icon,
      moduleCode: record?.moduleCode,
      score: t.count / maxCount,
      reason: 'chain' as const,
    }
  })
}

/**
 * 时间模式推荐：识别当前时间点用户通常访问的页面
 * 基于 VisitRecord.visitDays 字段（最近 30 次访问的星期几）分析周期规律
 * @param visitRecords 访问记录（需含 visitDays 字段）
 * @param now 当前时间
 * @returns 推荐结果（按相关度降序，最多 3 项）
 */
export function analyzeTimePatterns(
  visitRecords: VisitRecord[],
  now: Date
): RecommendationItem[] {
  const currentDay = now.getDay()
  const isMonthStart = now.getDate() <= 5

  const scored: RecommendationItem[] = []

  for (const record of visitRecords) {
    // 仅处理 visitDays 有值且长度 >= 5 的记录
    if (!record.visitDays || record.visitDays.length < 5) continue

    const days = record.visitDays
    const totalDays = days.length

    // 星期匹配: 该页面在当前星期几被访问的比例
    const dayMatchCount = days.filter(d => d === currentDay).length
    let matchScore = dayMatchCount / totalDays

    // 月初加权: 如果当前是月初且该页面月初访问频率高，额外加权
    if (isMonthStart && matchScore > 0.1) {
      // 简化：如果在当前星期几出现比例 > 30%，且当前是月初，加权 1.3x
      matchScore *= 1.3
    }

    // 仅推荐匹配度 > 0.3 的项
    if (matchScore > 0.3) {
      scored.push({
        path: record.path,
        title: record.title,
        icon: record.icon,
        moduleCode: record.moduleCode,
        score: Math.min(matchScore, 1), // 归一化到 0-1
        reason: 'time' as const,
      })
    }
  }

  // 按匹配度降序取 top 3
  scored.sort((a, b) => b.score - a.score)
  return scored.slice(0, 3)
}

/**
 * 合并多路推荐结果，去重，按综合分数排序
 * @param chainItems 行为链推荐结果
 * @param timeItems 时间模式推荐结果
 * @param excludePaths 需要排除的路径集合（如已在最近使用中的），避免循环依赖
 * @param maxTotal 最大返回数量，默认 8
 */
export function mergeRecommendations(
  chainItems: RecommendationItem[],
  timeItems: RecommendationItem[],
  excludePaths: string[],
  maxTotal: number = 8
): RecommendationItem[] {
  const excludeSet = new Set(excludePaths)
  const merged = new Map<string, RecommendationItem>()

  // 行为链结果权重 x1.2
  for (const item of chainItems) {
    if (excludeSet.has(item.path)) continue
    merged.set(item.path, { ...item, score: item.score * 1.2 })
  }

  // 时间模式权重 x1.0，去重（若已存在则取更高分）
  for (const item of timeItems) {
    if (excludeSet.has(item.path)) continue
    const existing = merged.get(item.path)
    if (existing) {
      // 保留更高分，但合并 reason 信息（优先 chain）
      if (item.score > existing.score) {
        merged.set(item.path, { ...item })
      }
    } else {
      merged.set(item.path, { ...item })
    }
  }

  // 按 score 降序取 top N
  const result = Array.from(merged.values())
  result.sort((a, b) => b.score - a.score)
  return result.slice(0, maxTotal)
}
