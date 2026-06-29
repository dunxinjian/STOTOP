import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { getEnterpriseInfo, updateEnterpriseInfo, type EnterpriseInfo } from '@/api/enterpriseInfo'

// 默认值
const DEFAULT_NAME = 'MDSTO'
const DEFAULT_SHORT_NAME = 'MDSTO'

export const useEnterpriseInfoStore = defineStore('enterpriseInfo', () => {
  // 状态
  const name = ref<string>(DEFAULT_NAME)
  const shortName = ref<string>(DEFAULT_SHORT_NAME)
  const logoUrl = ref<string>('')
  const loading = ref(false)
  const loaded = ref(false)

  // 计算属性：显示名称（优先使用简称，为空则使用全称）
  const displayName = computed(() => {
    return shortName.value || name.value || DEFAULT_NAME
  })

  // 计算属性：是否有Logo图片
  const hasLogo = computed(() => {
    return !!logoUrl.value && logoUrl.value.trim() !== ''
  })

  /**
   * 从API加载企业信息
   * 应用启动时调用，API失败时使用默认值
   */
  async function fetchEnterpriseInfo() {
    try {
      const data = await getEnterpriseInfo()
      name.value = data.name || DEFAULT_NAME
      shortName.value = data.shortName || DEFAULT_SHORT_NAME
      logoUrl.value = data.logoUrl || ''
      loaded.value = true
    } catch (error: unknown) {
      // API 返回 404/500 或网络异常，优雅降级为默认配置
      console.warn('Failed to load enterprise info, using defaults:', error)
      name.value = DEFAULT_NAME
      shortName.value = DEFAULT_SHORT_NAME
      logoUrl.value = ''
      loaded.value = true
    }
  }

  /**
   * 更新企业信息（管理员操作）
   */
  async function updateInfo(data: EnterpriseInfo) {
    loading.value = true
    try {
      const result = await updateEnterpriseInfo({
        name: data.name,
        shortName: data.shortName,
        logoUrl: data.logoUrl,
      })
      // 更新本地状态
      name.value = result.name || DEFAULT_NAME
      shortName.value = result.shortName || DEFAULT_SHORT_NAME
      logoUrl.value = result.logoUrl || ''
      return true
    } catch (error: unknown) {
      console.error('Failed to update enterprise info:', error)
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * 重置为默认值
   */
  function reset() {
    name.value = DEFAULT_NAME
    shortName.value = DEFAULT_SHORT_NAME
    logoUrl.value = ''
    loaded.value = false
  }

  return {
    // 状态
    name,
    shortName,
    logoUrl,
    loading,
    loaded,
    // 计算属性
    displayName,
    hasLogo,
    // 方法
    fetchEnterpriseInfo,
    updateInfo,
    reset,
  }
})
