import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import axios from 'axios'

// 模块定义
export interface ModuleTab {
  code: string
  name: string
  route: string
  icon?: string
  alwaysShow?: boolean // 始终显示（不受权限控制）
  noHomePage?: boolean // 标记无首页的模块，其入口路由不显示 MegaMenu
}

// 预定义模块列表
export const MODULE_TABS: ModuleTab[] = [
  { code: 'workhub', name: '工作台', route: '/workhub', icon: 'HomeOutlined', alwaysShow: true },
  { code: 'finance', name: '财务管理', route: '/finance/home', icon: 'AccountBookOutlined' },
  { code: 'cardflow', name: '卡片流转', route: '/cardflow', icon: 'ApartmentOutlined' },
  { code: 'crm', name: '客户关系', route: '/crm/dashboard', icon: 'UserOutlined' },
  { code: 'hr', name: '人资管理', route: '/hr/home', icon: 'TeamOutlined' },
  { code: 'dormitory', name: '宿舍管理', route: '/dormitory', icon: 'HomeOutlined' },
  { code: 'vehicle', name: '三轮车管理', route: '/vehicle/home', icon: 'CarOutlined' },
  { code: 'contract', name: '合同管理', route: '/contract', icon: 'FileTextOutlined' },
  { code: 'supplier', name: '供应商管理', route: '/supplier/home', icon: 'ShopOutlined' },
  { code: 'points', name: '积分管理', route: '/points', icon: 'TrophyOutlined' },
  { code: 'task', name: '任务管理', route: '/task/dashboard', icon: 'CheckSquareOutlined' },
  { code: 'express', name: '快递管理', route: '/express/dashboard', icon: 'SendOutlined' },
  { code: 'insurance', name: '保险管理', route: '/insurance/companies', icon: 'SafetyOutlined' },
  { code: 'quality', name: '质量中心', route: '/quality/dashboard', icon: 'SafetyCertificateOutlined' },
  { code: 'conference', name: '会务管理', route: '/conference/home', icon: 'ScheduleOutlined' },
  { code: 'report', name: '报表中心', route: '/report/home', icon: 'BarChartOutlined', alwaysShow: true },
  { code: 'system', name: '系统设置', route: '/system/users', icon: 'SettingOutlined', noHomePage: true }
]

export const useAppStore = defineStore('app', () => {
  const device = ref<'desktop' | 'mobile'>('desktop')
  const loading = ref<boolean>(false)
  const version = ref<string>('')
  const currentModule = ref<string>('workhub') // 当前选中的模块，'workhub' 表示工作台

  /** 从后端获取版本号，失败时静默处理 */
  async function fetchVersion() {
    try {
      const res = await axios.get('/api/version')
      if (res.data) {
        // 后端可能返回 { code, data } 或直接返回字符串
        version.value = typeof res.data === 'string' ? res.data : (res.data.data || res.data.version || '')
      }
    } catch {
      // 静默处理，不显示版本号
    }
  }

  function setCurrentModule(module: string) {
    currentModule.value = module
  }

  // 当前是否在工作台
  const isWorkhub = computed(() => currentModule.value === 'workhub')

  function setDevice(val: 'desktop' | 'mobile') {
    device.value = val
  }

  function setLoading(val: boolean) {
    loading.value = val
  }

  return {
    device,
    loading,
    version,
    currentModule,
    isWorkhub,
    setCurrentModule,
    setDevice,
    setLoading,
    fetchVersion,
  }
})
