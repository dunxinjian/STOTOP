import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { getAccountSets } from '@/api/finance'
import { get } from '@/api/request'
import type { AccountSetDto } from '@/api/finance'
import { useOrgContextStore } from './orgContext'

const STORAGE_KEY = 'currentAccountSetId'

export const useAccountSetStore = defineStore('accountSet', () => {
  const accountSets = ref<AccountSetDto[]>([])
  const currentAccountSetId = ref<number>(0)
  const loading = ref(false)

  // 权限状态
  const currentPermissions = ref<string[]>([])
  const permissionsLoading = ref(false)

  /**
   * 从 localStorage 恢复上次保存的账套选择（惰性初始化，由路由守卫触发）
   */
  function init() {
    const savedId = localStorage.getItem(STORAGE_KEY)
    if (savedId) {
      currentAccountSetId.value = Number(savedId)
    }
  }

  // 是否有可用账套
  const hasAvailableAccountSets = computed(() => accountSets.value.length > 0)

  // 当前账套对象
  const currentAccountSet = computed(() => {
    return accountSets.value.find(a => a.id === currentAccountSetId.value) || null
  })

  // 获取当前账套ID（未选择则返回默认账套ID）
  function getCurrentAccountSetId(): number {
    if (currentAccountSetId.value) {
      return currentAccountSetId.value
    }
    const defaultSet = accountSets.value.find(a => a.fIsDefault)
    return defaultSet?.id || 0
  }

  // 加载当前用户对指定账套的权限
  async function loadMyPermissions(accountSetId: number | string) {
    if (!accountSetId) {
      currentPermissions.value = []
      return
    }
    permissionsLoading.value = true
    try {
      const res = await get(`/finance/account-set-auth/my-permissions/${accountSetId}`) as any
      // get() 已经解包了 res.data，但如果后端返回的是 { code, data } 则需要兼容
      if (Array.isArray(res)) {
        currentPermissions.value = res
      } else if (res?.code === 0) {
        currentPermissions.value = res.data || []
      } else {
        currentPermissions.value = res || []
      }
    } catch (e) {
      currentPermissions.value = []
    } finally {
      permissionsLoading.value = false
    }
  }

  // 检查当前用户是否拥有指定的账套权限
  function hasAccountSetPermission(permissionCode: string): boolean {
    return currentPermissions.value.includes(permissionCode)
  }

  // 批量检查权限（任一满足即可）
  function hasAnyAccountSetPermission(...permissionCodes: string[]): boolean {
    return permissionCodes.some(code => currentPermissions.value.includes(code))
  }

  // 从API加载账套列表
  async function fetchAccountSets() {
    loading.value = true
    try {
      const orgContextStore = useOrgContextStore()
      const orgId = orgContextStore.currentOrgId || undefined
      const res = await getAccountSets(orgId) as any
      accountSets.value = res || []

      // 当前组织无账套时，清空选中状态
      if (accountSets.value.length === 0) {
        currentAccountSetId.value = 0
        localStorage.removeItem(STORAGE_KEY)
        currentPermissions.value = []
        return
      }

      // 如果当前没有选中，或选中的ID不在列表中，则选默认
      const ids = accountSets.value.map(a => a.id)
      if (!currentAccountSetId.value || !ids.includes(currentAccountSetId.value)) {
        const defaultSet = accountSets.value.find(a => a.fIsDefault)
        if (defaultSet) {
          currentAccountSetId.value = defaultSet.id
          localStorage.setItem(STORAGE_KEY, String(defaultSet.id))
        } else if (accountSets.value.length > 0) {
          currentAccountSetId.value = accountSets.value[0].id
          localStorage.setItem(STORAGE_KEY, String(accountSets.value[0].id))
        }
      }

      // 账套确定后立即加载当前用户对该账套的权限。
      // 初始加载/刷新/组织切换都走这里，若不加载，currentPermissions 始终为空，
      // hasAccountSetPermission 恒为 false，会导致科目管理等页面的增删改查按钮全部隐藏
      // （此前权限仅在用户手动切换账套选择器时才加载）。
      await loadMyPermissions(currentAccountSetId.value)
    } catch (error) {
      console.error('加载账套列表失败:', error)
    } finally {
      loading.value = false
    }
  }

  // 切换当前账套
  async function setCurrentAccountSet(id: number) {
    currentAccountSetId.value = id
    localStorage.setItem(STORAGE_KEY, String(id))

    // 切换账套后加载权限
    if (id) {
      await loadMyPermissions(id)
    } else {
      currentPermissions.value = []
    }
  }

  return {
    accountSets,
    currentAccountSetId,
    currentAccountSet,
    hasAvailableAccountSets,
    loading,
    currentPermissions,
    permissionsLoading,
    getCurrentAccountSetId,
    fetchAccountSets,
    setCurrentAccountSet,
    init,
    loadMyPermissions,
    hasAccountSetPermission,
    hasAnyAccountSetPermission,
  }
})
