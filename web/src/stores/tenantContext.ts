import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { getMyTenants, switchTenant } from '@/api/system'
import type { TenantMembershipDto, SwitchTenantResponse } from '@/types/organization'

/**
 * 多租户上下文（阶段4F·M9）。租户 = 客户（区域公司/集团），经请求头 X-Tenant-Context 传递（不进 JWT）。
 * 单客户下用户仅属一个租户 → 切换器隐藏、此 store 基本休眠；多客户上线后承载租户选择/切换。
 */
export const useTenantContextStore = defineStore('tenantContext', () => {
  const currentTenantId = ref<number | null>(null)
  const currentTenantName = ref('')
  const tenants = ref<TenantMembershipDto[]>([])

  const hasMultipleTenants = computed(() => tenants.value.length > 1)
  const primaryTenant = computed(() => tenants.value.find(t => t.isPrimary))

  /** 从 localStorage 恢复租户上下文（惰性，由路由守卫触发）。 */
  function init() {
    const savedId = localStorage.getItem('stotop_current_tenant_id')
    if (savedId) currentTenantId.value = Number(savedId)
    const savedName = localStorage.getItem('stotop_current_tenant_name')
    if (savedName) currentTenantName.value = savedName
  }

  /** 加载用户可切换的租户列表（已接受成员）。 */
  async function fetchTenants() {
    try {
      const res = await getMyTenants() as any
      tenants.value = Array.isArray(res) ? res : (res?.items || [])
    } catch {
      // 保留旧列表，避免切换器闪烁
    }
  }

  function setCurrentTenant(tenantId: number, tenantName: string) {
    currentTenantId.value = tenantId
    currentTenantName.value = tenantName
    // 同步到 localStorage 供请求拦截器注入 X-Tenant-Context
    localStorage.setItem('stotop_current_tenant_id', String(tenantId))
    localStorage.setItem('stotop_current_tenant_name', tenantName || '')
  }

  /** 切换租户：后端校验成员 + 返回本租户内可切换组织及自动选定组织上下文。 */
  async function doSwitchTenant(tenantId: number): Promise<SwitchTenantResponse | null> {
    try {
      const data = await switchTenant({ tenantId }) as unknown as SwitchTenantResponse
      const t = tenants.value.find(x => x.tenantId === tenantId)
      setCurrentTenant(tenantId, data.tenantName || t?.tenantName || '')
      return data
    } catch {
      return null
    }
  }

  function clearTenantContext() {
    currentTenantId.value = null
    currentTenantName.value = ''
    tenants.value = []
    localStorage.removeItem('stotop_current_tenant_id')
    localStorage.removeItem('stotop_current_tenant_name')
  }

  return {
    currentTenantId,
    currentTenantName,
    tenants,
    hasMultipleTenants,
    primaryTenant,
    init,
    fetchTenants,
    setCurrentTenant,
    doSwitchTenant,
    clearTenantContext,
  }
})
