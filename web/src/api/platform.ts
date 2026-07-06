import { get, post, put } from './request'
import type {
  TenantDto,
  ProvisionTenantRequest,
  ProvisionTenantResult,
  UpdateTenantStatusRequest,
  PlanDto,
  SavePlanRequest,
  SubscriptionDto,
  CreateSubscriptionRequest,
} from '@/types/platform'

// 重新导出平台层类型，便于页面从 '@/api/platform' 一处引入
export type {
  TenantDto,
  ProvisionTenantRequest,
  ProvisionTenantResult,
  UpdateTenantStatusRequest,
  PlanDto,
  SavePlanRequest,
  SubscriptionDto,
  CreateSubscriptionRequest,
} from '@/types/platform'

// ==================== 租户 ====================

export function getTenants() {
  return get<TenantDto[]>('/platform/tenants')
}

export function getTenant(id: number) {
  return get<TenantDto>(`/platform/tenants/${id}`)
}

/** 开通新租户（R5）：建组织根+初始管理员+私有角色+成员+R8，返回一次性初始密码 */
export function createTenant(data: ProvisionTenantRequest) {
  return post<ProvisionTenantResult>('/platform/tenants', data)
}

/** 改租户状态（1 试用 / 2 正式 / 3 停用 / 4 冻结）。冻结/解冻/停用/转正式均走此接口 */
export function updateTenantStatus(id: number, data: UpdateTenantStatusRequest) {
  return put<boolean>(`/platform/tenants/${id}/status`, data)
}

// ==================== 套餐 ====================

export function getPlans() {
  return get<PlanDto[]>('/platform/plans')
}

export function createPlan(data: SavePlanRequest) {
  return post<number>('/platform/plans', data)
}

export function updatePlan(id: number, data: SavePlanRequest) {
  return put<boolean>(`/platform/plans/${id}`, data)
}

// ==================== 订阅 / 续费 ====================

export function getSubscriptions(tenantId?: number) {
  return get<SubscriptionDto[]>('/platform/subscriptions', tenantId ? { tenantId } : undefined)
}

/** 新建订阅 = 开通/续费/激活：成功后租户转正式并回填开通/到期时间 */
export function createSubscription(data: CreateSubscriptionRequest) {
  return post<number>('/platform/subscriptions', data)
}
