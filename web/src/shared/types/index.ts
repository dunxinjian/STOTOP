// 共享类型定义 — PC 和移动端共用

/** API 统一返回格式 */
export interface ApiResult<T = any> {
  code: number
  data: T
  message: string
}

/** 用户信息 */
export interface UserInfo {
  id: number
  name: string
  avatar?: string
  roles: string[]
  organizations: OrgInfo[]
  defaultOrgId: number
}

/** 组织信息 */
export interface OrgInfo {
  id: number
  name: string
  code: string
}

/** 待办卡片 */
export interface TodoCard {
  id: number
  title: string
  flowName: string
  applicant: string
  amount?: number
  createdAt: string
  status: 'pending' | 'processing' | 'completed'
}

/** KPI 数据 */
export interface KpiData {
  volume: { value: number; change: number }
  revenue: { value: number; change: number }
  cost: { value: number; change: number }
  profit: { value: number; change: number }
  cachedAt: string
}

/** 趋势数据点 */
export interface TrendPoint {
  date: string
  value: number
}
