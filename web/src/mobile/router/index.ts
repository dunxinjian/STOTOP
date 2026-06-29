import { createRouter, createWebHashHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const routes: RouteRecordRaw[] = [
  {
    path: '/m/home',
    name: 'MobileHome',
    component: () => import('../views/Home.vue'),
    meta: { title: '工作台' },
  },
  {
    path: '/m/submit/:defId',
    name: 'MobileSubmit',
    component: () => import('../views/Submit.vue'),
    meta: { title: '提交卡片' },
  },
  {
    path: '/m/card/:id',
    name: 'MobileCardDetail',
    component: () => import('../views/CardDetail.vue'),
    meta: { title: '卡片详情' },
  },
  {
    path: '/m/history',
    name: 'MobileHistory',
    component: () => import('../views/History.vue'),
    meta: { title: '已处理' },
  },
  {
    path: '/m/scan',
    name: 'MobileScan',
    component: () => import('../views/Scan.vue'),
    meta: { title: '扫一扫' },
  },
  {
    path: '/m/dashboard',
    name: 'MobileDashboard',
    component: () => import('../views/Dashboard.vue'),
    meta: { title: '经营看板' },
  },
  {
    path: '/m/report/amoeba',
    name: 'MobileReportAmoeba',
    component: () => import('../views/ReportAmoeba.vue'),
    meta: { title: '阿米巴损益' },
  },
  {
    path: '/m/report/profit',
    name: 'MobileReportProfit',
    component: () => import('../views/ReportProfit.vue'),
    meta: { title: '毛利分析' },
  },
  {
    path: '/m/report/cost',
    name: 'MobileReportCost',
    component: () => import('../views/ReportCost.vue'),
    meta: { title: '成本明细' },
  },
  {
    path: '/m/report/express',
    name: 'MobileReportExpress',
    component: () => import('../views/ReportExpress.vue'),
    meta: { title: '快递业务统计' },
  },
  {
    path: '/m/mine',
    name: 'MobileMine',
    component: () => import('../views/Mine.vue'),
    meta: { title: '我的' },
  },
  {
    path: '/',
    redirect: '/m/home',
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/m/home',
  },
]

const router = createRouter({
  history: createWebHashHistory(),
  routes,
})

// 路由守卫: 鉴权检查
router.beforeEach(async (to, _from, next) => {
  const authStore = useAuthStore()

  // 如果没有 token，尝试免登
  if (!authStore.token) {
    try {
      await authStore.loginByDingTalk()
    } catch (e) {
      console.error('[Mobile] 免登失败:', e)
      // 免登失败仍放行，由页面自行处理
    }
  }

  // 设置标题
  if (to.meta.title) {
    document.title = to.meta.title as string
  }

  next()
})

export default router
