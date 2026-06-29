import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import {
  getMyOrganizations,
  switchOrganization,
  getCurrentOrgContext,
} from '@/api/system'
import type { UserOrganizationDto, SwitchOrganizationResponse } from '@/types/organization'
import { useAccountSetStore } from './accountSet'
import { usePermissionStore } from './permission'

export const useOrgContextStore = defineStore('orgContext', () => {
  const currentOrgId = ref<number | null>(null)
  const currentOrgName = ref('')
  const currentOrgType = ref('')
  const organizations = ref<UserOrganizationDto[]>([])
  const orgRoles = ref<string[]>([])
  const orgPermissions = ref<string[]>([])
  // 组织切换版本号：每次 setCurrentOrg 递增，供 MainLayout 作为 router-view key 的一部分，
  // 用于使 keep-alive 缓存失效，强制页面重新挂载并刷新数据。
  const orgSwitchVersion = ref(0)
  // 页面刷新版本号：用于顶栏刷新按钮触发组件重载（不会退出全屏）
  const pageRefreshVersion = ref(0)

  function triggerPageRefresh() {
    pageRefreshVersion.value++
  }

  const hasMultipleOrgs = computed(() => organizations.value.length > 1)
  const primaryOrg = computed(() => organizations.value.find(o => o.isPrimaryOrg === 1))

  /**
   * 从 localStorage 恢复组织上下文（惰性初始化，由路由守卫触发）
   */
  function init() {
    const savedOrgId = localStorage.getItem('stotop_current_org_id')
    if (savedOrgId) currentOrgId.value = Number(savedOrgId)
    const savedName = localStorage.getItem('stotop_current_org_name')
    if (savedName) currentOrgName.value = savedName
    const savedType = localStorage.getItem('stotop_current_org_type')
    if (savedType) currentOrgType.value = savedType
  }

  /**
   * 加载用户所有任职组织列表
   */
  async function fetchOrganizations() {
    try {
      const res = await getMyOrganizations() as any
      let orgs: UserOrganizationDto[] = Array.isArray(res) ? res : (res?.items || [])
      // 防守层：确保只保留有 switchableOrgId 的记录
      orgs = orgs.filter((org: any) => org.switchableOrgId != null)
      organizations.value = orgs
    } catch {
      // 如果已有当前组织信息，保留 organizations 不清空，避免切换器闪烁
      if (!currentOrgId.value) {
        organizations.value = []
      }
    }
  }

  /**
   * 切换当前组织
   */
  async function doSwitchOrganization(orgId: number): Promise<SwitchOrganizationResponse | null> {
    try {
      const data = await switchOrganization({ orgId }) as unknown as SwitchOrganizationResponse
      // 先更新菜单权限数据，再递增 orgSwitchVersion（在 setCurrentOrg 内递增）
      // 确保 GlobalSearch 等依赖 menus 的 computed 在版本号触发重算时已读到最新菜单
      const permissionStore = usePermissionStore()
      permissionStore.generateRoutes((data.menus || []) as any)

      setCurrentOrg(data)
      
      // 组织切换成功后，异步刷新账套列表
      // 使用 setTimeout 避免在 store 初始化阶段产生循环依赖
      setTimeout(() => {
        const accountSetStore = useAccountSetStore()
        accountSetStore.fetchAccountSets()
      }, 0)
      
      return data
    } catch {
      return null
    }
  }

  function setCurrentOrg(data: SwitchOrganizationResponse) {
    const changed = currentOrgId.value !== data.orgId
    currentOrgId.value = data.orgId
    currentOrgName.value = data.orgName
    currentOrgType.value = data.orgType
    orgRoles.value = data.roles || []
    orgPermissions.value = data.permissions || []
    // 同步到 localStorage 供请求拦截器使用
    localStorage.setItem('stotop_current_org_id', String(data.orgId))
    localStorage.setItem('stotop_current_org_name', data.orgName || '')
    localStorage.setItem('stotop_current_org_type', data.orgType || '')
    // 确保当前组织存在于 organizations 列表中（避免列表未加载时切换器无法显示）
    if (data.orgId && !organizations.value.some(o => o.orgId === data.orgId)) {
      organizations.value.push({
        id: data.orgId,
        orgId: data.orgId,
        orgName: data.orgName,
        orgType: data.orgType,
        isPrimaryOrg: 0,
        switchableOrgId: data.orgId,
      } as UserOrganizationDto)
    }
    // 仅当组织确实变化时递增版本号，避免页面刷新恢复上下文时误触发 keep-alive 失效
    if (changed) {
      orgSwitchVersion.value++
    }
  }

  /**
   * 获取当前组织上下文（页面刷新恢复用）
   */
  async function fetchCurrentContext() {
    try {
      const data = await getCurrentOrgContext() as SwitchOrganizationResponse
      if (data && data.orgId) {
        // 先更新菜单权限数据，再递增 orgSwitchVersion（与 doSwitchOrganization 保持一致）
        if (data.menus && Array.isArray(data.menus)) {
          const permissionStore = usePermissionStore()
          permissionStore.generateRoutes(data.menus as any)
        }

        setCurrentOrg(data)

        // 异步刷新账套列表
        setTimeout(() => {
          const accountSetStore = useAccountSetStore()
          accountSetStore.fetchAccountSets()
        }, 0)
      }
    } catch (e) {
      console.warn('[orgContext] fetchCurrentContext 失败', e)
    }
  }

  /**
   * 清除组织上下文状态
   */
  function clearOrgContext() {
    currentOrgId.value = null
    currentOrgName.value = ''
    currentOrgType.value = ''
    organizations.value = []
    orgRoles.value = []
    orgPermissions.value = []
    localStorage.removeItem('stotop_current_org_id')
    localStorage.removeItem('stotop_current_org_name')
    localStorage.removeItem('stotop_current_org_type')
  }

  return {
    currentOrgId,
    currentOrgName,
    currentOrgType,
    organizations,
    orgRoles,
    orgPermissions,
    orgSwitchVersion,
    pageRefreshVersion,
    hasMultipleOrgs,
    primaryOrg,
    fetchOrganizations,
    doSwitchOrganization,
    fetchCurrentContext,
    clearOrgContext,
    triggerPageRefresh,
    init,
  }
})
