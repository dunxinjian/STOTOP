import type { RouteRecordRaw } from 'vue-router'

/**
 * 视图组件映射 - 用于动态路由加载
 */
export const viewModules = import.meta.glob('../views/**/*.vue')

/**
 * 移动端路由（不使用主框架 Layout）
 */
export const mobileRoutes: RouteRecordRaw[] = [
  // ===== 移动端 卡片流转 =====
  {
    path: '/m/cardflow/fill/:id',
    name: 'MobileCardFill',
    component: () => import('@/views/cardflow-mobile/MobileCardFillPage.vue'),
    meta: { title: '填写', permission: '*' },
  },
  {
    path: '/m/cardflow/approval/:id',
    name: 'MobileCardApproval',
    component: () => import('@/views/cardflow-mobile/MobileCardApprovalPage.vue'),
    meta: { title: '审批', permission: '*' },
  },
]

/**
 * 静态路由 - 所有用户均可访问
 */
export const staticRoutes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/login/index.vue'),
    meta: { title: '登录', hidden: true },
  },
  {
    path: '/setup',
    name: 'DatabaseSetup',
    component: () => import('@/views/system/DatabaseSetup.vue'),
    meta: { title: '数据库配置', hidden: true },
  },
  {
    path: '/403',
    name: 'Forbidden',
    component: () => import('@/views/403/index.vue'),
    meta: { title: '权限不足', hidden: true },
  },
  {
    path: '/404',
    name: 'NotFound',
    component: () => import('@/views/404/index.vue'),
    meta: { title: '页面不存在', hidden: true },
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/404',
    meta: { hidden: true },
  },
]

/**
 * 主框架路由（需要认证）
 */
export const layoutRoute: RouteRecordRaw = {
  path: '/',
  name: 'Layout',
  component: () => import('@/layouts/MainLayout.vue'),
  redirect: '/workhub/todo',
  children: [
    {
      path: 'home',
      redirect: '/workhub/todo',
      meta: { hidden: true, module: 'workhub' },
    },
    {
      path: 'workhub',
      redirect: '/workhub/todo',
      meta: { hidden: true, module: 'workhub' },
    },
    {
      path: 'workhub/todo',
      name: 'WorkHubTodo',
      component: () => import('@/views/workhub/index.vue'),
      meta: { title: '待办', icon: 'HomeOutlined', module: 'workhub' },
    },
    {
      path: 'workhub/initiate',
      name: 'WorkHubInitiate',
      component: () => import('@/views/workhub/InitiatePage.vue'),
      meta: { title: '发起', icon: 'PlusCircleOutlined', module: 'workhub' },
    },
    {
      path: 'workhub/import-issue/:id',
      name: 'ImportIssueDetail',
      component: () => import('@/views/workhub/ImportIssueDetail.vue'),
      meta: { title: '导入异常处理', hidden: true, module: 'workhub' },
    },
    // 个人设置（用户下拉菜单入口，不在侧栏展示）
    {
      path: 'profile',
      name: 'UserProfile',
      component: () => import('@/views/system/UserProfile.vue'),
      meta: { title: '个人设置', permission: '*', hidden: true },
    },
    // 租户邀请（多租户阶段4F；不在侧栏，经直链/待确认提示进入）
    {
      path: 'tenant-invites',
      name: 'TenantInvites',
      component: () => import('@/views/system/TenantInvites.vue'),
      meta: { title: '租户邀请', permission: '*', hidden: true },
    },
    // 系统管理子路由
    {
      path: 'system/users',
      name: 'UserManage',
      component: () => import('@/views/system/UserManage.vue'),
      meta: { title: '用户管理', icon: 'User', module: 'system' },
    },
    {
      path: 'system/roles',
      name: 'RoleManage',
      component: () => import('@/views/system/RoleManage.vue'),
      meta: { title: '角色管理', icon: 'UserFilled', module: 'system' },
    },
    {
      path: 'system/organizations',
      name: 'Organization',
      component: () => import('@/views/system/Organization.vue'),
      meta: { title: '组织架构', icon: 'BankOutlined', module: 'system' },
    },
    {
      path: 'system/menus',
      name: 'MenuManage',
      component: () => import('@/views/system/MenuManage.vue'),
      meta: { title: '菜单管理', icon: 'Menu', module: 'system' },
    },
    {
      path: 'system/database',
      name: 'DatabaseConfig',
      component: () => import('@/views/system/DatabaseConfig.vue'),
      meta: { title: '数据库配置', icon: 'Coin', module: 'system' },
    },
    {
      path: 'system/db-connections',
      name: 'DbConnectionManage',
      component: () => import('@/views/system/DbConnectionManage.vue'),
      meta: { title: '数据库连接', icon: 'ApiOutlined', module: 'system' },
    },
    // ===== 安全管理 =====
    {
      path: 'system/security/config',
      name: 'SecurityConfig',
      component: () => import('@/views/system/security/SecurityConfig.vue'),
      meta: { title: '安全配置', icon: 'SafetyCertificateOutlined', module: 'system' },
    },
    {
      path: 'system/security/audit',
      name: 'AuditLog',
      component: () => import('@/views/system/security/AuditLog.vue'),
      meta: { title: '审计日志', icon: 'FileSearchOutlined', module: 'system' },
    },
    {
      path: 'system/security/sessions',
      name: 'OnlineSessions',
      component: () => import('@/views/system/security/OnlineSessions.vue'),
      meta: { title: '在线会话', icon: 'TeamOutlined', module: 'system' },
    },
    {
      path: 'system/change-logs',
      name: 'ChangeLogList',
      component: () => import('@/views/system/ChangeLogList.vue'),
      meta: { title: '变更记录', icon: 'HistoryOutlined', module: 'system' },
    },
    {
      path: 'system/feedback',
      name: 'FeedbackCenter',
      component: () => import('@/views/system/FeedbackCenter.vue'),
      meta: { title: '反馈中心', icon: 'MessageOutlined', module: 'system' },
    },
    {
      path: 'system/config-center',
      name: 'AdminConfigCenter',
      component: () => import('@/views/system/AdminConfigCenter.vue'),
      meta: { title: '配置中心', icon: 'SettingOutlined', module: 'system' },
    },
    {
      path: 'system/org-chart',
      name: 'OrgChart',
      component: () => import('@/views/system/OrgChart.vue'),
      meta: { title: '组织架构图', icon: 'ApartmentOutlined', module: 'system' },
    },
    {
      path: 'system/positions',
      name: 'PositionManage',
      component: () => import('@/views/system/PositionManage.vue'),
      meta: { title: '岗位管理', icon: 'IdcardOutlined', module: 'system' },
    },
    {
      path: 'system/dingtalk',
      name: 'DingTalkConfig',
      component: () => import('@/views/system/DingTalkConfig.vue'),
      meta: { title: '钉钉同步', icon: 'DingtalkOutlined', module: 'system' },
    },
    {
      path: 'system/theme',
      name: 'ThemeConfig',
      component: () => import('@/views/system/ThemeConfig.vue'),
      meta: { title: '主题设置', icon: 'BgColorsOutlined', module: 'system' },
    },
    {
      path: 'system/enterprise',
      name: 'EnterpriseInfo',
      component: () => import('@/views/system/EnterpriseInfo.vue'),
      meta: { title: '企业信息', icon: 'ShopOutlined', module: 'system' },
    },
    {
      path: 'system/code-rules',
      name: 'CodeRuleManage',
      component: () => import('@/views/system/CodeRuleManage.vue'),
      meta: { title: '编码规则', icon: 'FieldNumberOutlined', module: 'system' },
    },
    {
      path: 'system/db-migration',
      name: 'DbMigrationManage',
      component: () => import('@/views/system/DbMigrationManage.vue'),
      meta: { title: '数据库迁移管理', icon: 'CloudServerOutlined', module: 'system' },
    },
    // 财务管理首页
    {
      path: 'finance/home',
      name: 'FinanceHome',
      component: () => import('@/views/finance/FinanceHome.vue'),
      meta: { title: '财务首页', icon: 'HomeFilled', module: 'finance' },
    },
    {
      path: 'finance/budget/versions',
      name: 'BudgetVersionManage',
      component: () => import('@/views/finance/BudgetVersionManage.vue'),
      meta: { title: '预算版本', icon: 'ProfileOutlined', module: 'finance' },
    },
    {
      path: 'finance/budget/editor/:versionId',
      name: 'BudgetLineEditor',
      component: () => import('@/views/finance/BudgetLineEditor.vue'),
      meta: { title: '预算编制', icon: 'EditOutlined', hidden: true, module: 'finance' },
    },
    {
      path: 'finance/budget/expense-mapping',
      name: 'BudgetExpenseMapping',
      component: () => import('@/views/finance/BudgetExpenseMapping.vue'),
      meta: { title: '费用预算映射', icon: 'BranchesOutlined', module: 'finance' },
    },
    {
      path: 'finance/treasury/account-bindings',
      name: 'TreasuryAccountBinding',
      component: () => import('@/views/finance/TreasuryAccountBinding.vue'),
      meta: { title: '资金账户绑定', icon: 'BankOutlined', module: 'finance' },
    },
    {
      path: 'finance/treasury/rolling-13-weeks',
      name: 'TreasuryRollingForecast',
      component: () => import('@/views/finance/TreasuryRollingForecast.vue'),
      meta: { title: '13周资金预测', icon: 'LineChartOutlined', module: 'finance' },
    },
    // ===== 宿舍管理（静态路由，提供稳定 name + 菜单种子缺失时兜底，镜像 finance 模式）=====
    // 模块入口：进入 /dormitory 重定向到楼栋管理
    {
      path: 'dormitory',
      redirect: '/dormitory/buildings',
      meta: { hidden: true, module: 'dormitory' },
    },
    {
      path: 'dormitory/dashboard',
      name: 'DormitoryDashboard',
      component: () => import('@/views/dormitory/DormitoryDashboard.vue'),
      meta: { title: '宿舍统计', icon: 'DashboardOutlined', module: 'dormitory' },
    },
    {
      path: 'dormitory/buildings',
      name: 'BuildingManage',
      component: () => import('@/views/dormitory/BuildingManage.vue'),
      meta: { title: '楼栋管理', icon: 'BankOutlined', module: 'dormitory' },
    },
    {
      // 房间管理：楼栋的子页，由楼栋列表带 buildingId 跳入（hidden，不在侧栏）
      path: 'dormitory/buildings/:buildingId/rooms',
      name: 'RoomManage',
      component: () => import('@/views/dormitory/RoomManage.vue'),
      meta: { title: '房间管理', icon: 'AppstoreOutlined', hidden: true, module: 'dormitory' },
    },
    {
      path: 'dormitory/residences',
      name: 'ResidenceManage',
      component: () => import('@/views/dormitory/ResidenceManage.vue'),
      meta: { title: '入住管理', icon: 'UserAddOutlined', module: 'dormitory' },
    },
    {
      path: 'dormitory/expenses',
      name: 'ExpenseManage',
      component: () => import('@/views/dormitory/ExpenseManage.vue'),
      meta: { title: '费用管理', icon: 'DollarOutlined', module: 'dormitory' },
    },
    {
      path: 'dormitory/facilities',
      name: 'FacilityManage',
      component: () => import('@/views/dormitory/FacilityManage.vue'),
      meta: { title: '设施管理', icon: 'ToolOutlined', module: 'dormitory' },
    },
    {
      path: 'dormitory/repairs',
      name: 'RepairOrderManage',
      component: () => import('@/views/dormitory/RepairOrderManage.vue'),
      meta: { title: '报修工单', icon: 'WarningOutlined', module: 'dormitory' },
    },
    {
      path: 'dormitory/visitors',
      name: 'VisitorManage',
      component: () => import('@/views/dormitory/VisitorManage.vue'),
      meta: { title: '访客登记', icon: 'TeamOutlined', module: 'dormitory' },
    },
    {
      path: 'dormitory/hygiene',
      name: 'HygieneCheckManage',
      component: () => import('@/views/dormitory/HygieneCheckManage.vue'),
      meta: { title: '卫生检查', icon: 'SafetyCertificateOutlined', module: 'dormitory' },
    },
    // 三轮车管理首页
    {
      path: 'vehicle/home',
      name: 'VehicleHome',
      component: () => import('@/views/vehicle/VehicleDashboard.vue'),
      meta: { title: '三轮车管理', icon: 'CarOutlined', module: 'vehicle' },
    },
    // 账套管理
    {
      path: 'finance/account-sets',
      name: 'AccountSets',
      component: () => import('@/views/finance/AccountSetManage.vue'),
      meta: { title: '账套管理', icon: 'FolderOpenOutlined', module: 'finance' },
    },

    // 财务管理子路由
    {
      path: 'finance/accounts',
      name: 'AccountManage',
      component: () => import('@/views/finance/AccountManage.vue'),
      meta: { title: '科目管理', icon: 'Coin', module: 'finance' },
    },
    {
      path: 'finance/auxiliary-setting',
      name: 'AuxiliarySetting',
      component: () => import('@/views/finance/AuxiliarySetting.vue'),
      meta: { title: '辅助设置', icon: 'Grid', module: 'finance' },
    },
    {
      path: 'finance/account-set-rules',
      name: 'AccountSetRuleConfig',
      component: () => import('@/views/finance/AccountSetRuleConfig.vue'),
      meta: { title: '账套规则', icon: 'ControlOutlined', module: 'finance' },
    },
    {
      path: 'finance/auxiliary-aliases',
      name: 'AuxiliaryAliasConfig',
      component: () => import('@/views/finance/AuxiliaryAliasConfig.vue'),
      meta: { title: '辅助别名配置', icon: 'LinkOutlined', module: 'finance' },
    },
    {
      path: 'finance/voucher/list',
      name: 'VoucherList',
      component: () => import('@/views/finance/VoucherList.vue'),
      meta: { title: '凭证管理', icon: 'Document', module: 'finance' },
    },
    {
      path: 'finance/voucher/entry',
      name: 'VoucherEntry',
      component: () => import('@/views/finance/VoucherEntry.vue'),
      meta: { title: '凭证录入', icon: 'Edit', hidden: true, module: 'finance' },
    },
    {
      path: 'finance/voucher/entry/:id',
      name: 'VoucherEdit',
      component: () => import('@/views/finance/VoucherEntry.vue'),
      meta: { title: '凭证编辑', icon: 'Edit', hidden: true, module: 'finance' },
    },
    {
      path: 'finance/voucher/print/:ids',
      name: 'VoucherPrint',
      component: () => import('@/views/finance/VoucherPrint.vue'),
      meta: { title: '凭证打印', hidden: true, module: 'finance' },
    },
    // ===== 凭证模板 =====
    {
      path: 'finance/voucher-template',
      name: 'VoucherTemplateManage',
      component: () => import('@/views/finance/VoucherTemplateManage.vue'),
      meta: { title: '凭证模板', icon: 'SnippetsOutlined', module: 'finance' },
    },
    // ===== 汇率管理 =====
    {
      path: 'finance/exchange-rate',
      name: 'ExchangeRateManage',
      component: () => import('@/views/finance/ExchangeRateManage.vue'),
      meta: { title: '汇率管理', icon: 'Money', module: 'finance' },
    },
    // ===== 结账管理 =====
    {
      path: 'finance/period-closing',
      name: 'PeriodClosing',
      component: () => import('@/views/finance/PeriodClosing.vue'),
      meta: { title: '结账', icon: 'Lock', module: 'finance' },
    },
    // ===== 日记账 =====
    {
      path: 'finance/journal',
      name: 'Journal',
      component: () => import('@/views/finance/Journal.vue'),
      meta: { title: '日记账', icon: 'BookOutlined', module: 'finance' },
    },
    // ===== 操作日志 =====
    {
      path: 'finance/operation-log',
      name: 'OperationLog',
      component: () => import('@/views/finance/OperationLog.vue'),
      meta: { title: '操作日志', icon: 'Document', module: 'finance' },
    },
    // ===== 阿米巴经营模块 =====
    {
      path: 'finance/amoeba/report',
      name: 'AmoebaReport',
      component: () => import('@/views/finance/AmoebaPL.vue'),
      meta: { title: '阿米巴经营报表', icon: 'TrendCharts', module: 'finance' },
    },
    {
      path: 'finance/reports/amoeba-pl',
      redirect: '/finance/amoeba/report',
      meta: { hidden: true, module: 'finance' },
    },

    {
      path: 'finance/amoeba/templates',
      name: 'AmoebaTemplates',
      component: () => import('@/views/finance/AmoebaPLTemplate.vue'),
      meta: { title: '阿米巴报表模板', icon: 'ProfileOutlined', module: 'finance' },
    },
    {
      path: 'finance/amoeba/classify',
      name: 'AmoebaClassify',
      component: () => import('@/views/finance/AmoebaClassify.vue'),
      meta: { title: '待分类管理', icon: 'SortAscendingOutlined', module: 'finance' },
    },
    {
      path: 'finance/reports/profit',
      name: 'ProfitStatement',
      component: () => import('@/views/finance/ProfitStatement.vue'),
      meta: { title: '利润表', icon: 'Document', module: 'finance' },
    },
    {
      path: 'finance/reports/account-balance',
      name: 'AccountBalanceReport',
      component: () => import('@/views/finance/AccountBalanceReport.vue'),
      meta: { title: '科目余额表', icon: 'List', module: 'finance' },
    },
    {
      path: 'finance/reports/account-detail/:accountId',
      name: 'AccountDetailReport',
      component: () => import('@/views/finance/AccountDetailReport.vue'),
      meta: { title: '科目明细账', icon: 'List', hidden: true, module: 'finance' },
    },
    {
      path: 'finance/reports/auxiliary-balance',
      name: 'AuxiliaryBalanceReport',
      component: () => import('@/views/finance/AuxiliaryBalanceReport.vue'),
      meta: { title: '辅助余额表', icon: 'Grid', module: 'finance' },
    },
    {
      path: 'finance/asset-settings',
      name: 'AssetSettings',
      component: () => import('@/views/finance/AssetSettings.vue'),
      meta: { title: '资产设置', icon: 'Setting', module: 'finance' },
    },
    {
      path: 'finance/reports/asset-balance',
      name: 'AssetBalanceReport',
      component: () => import('@/views/finance/AssetBalanceReport.vue'),
      meta: { title: '资产余额表', icon: 'Box', module: 'finance' },
    },
    {
      path: 'finance/reports/balance-sheet',
      name: 'BalanceSheet',
      component: () => import('@/views/finance/BalanceSheet.vue'),
      meta: { title: '资产负债表', icon: 'ScaleToOriginal', module: 'finance' },
    },
    {
      path: 'finance/reports/cash-flow',
      name: 'CashFlowReport',
      component: () => import('@/views/finance/CashFlowReport.vue'),
      meta: { title: '资金流量表', icon: 'Money', module: 'finance' },
    },
    {
      path: 'finance/reports/tax-payable',
      name: 'TaxPayableReport',
      component: () => import('@/views/finance/TaxPayableReport.vue'),
      meta: { title: '应交税费表', icon: 'Coin', module: 'finance' },
    },
    // ===== 迁移映射配置 =====
    {
      path: 'finance/migration-config',
      name: 'MigrationConfig',
      component: () => import('@/views/finance/MigrationConfig.vue'),
      meta: { title: '迁移映射配置', icon: 'SwapOutlined', module: 'finance' },
    },

    // ===== 快递-快递报价查询 =====
    {
      path: 'express/quotation-workbench',
      name: 'QuotationWorkbench',
      component: () => import('@/views/express/quotation/QuotationWorkbench.vue'),
      meta: { title: '快递报价查询' },
    },
    // ===== 快递-报价 =====
    {
      path: 'express/quotation',
      name: 'QuotationList',
      component: () => import('@/views/express/quotation/QuotationList.vue'),
      meta: { title: '快递报价' },
    },
    // ===== 快递-新建报价（必须在 :id 之前）=====
    {
      path: 'express/quotation/create',
      name: 'QuotationCreate',
      component: () => import('@/views/express/quotation/QuotationEdit.vue'),
      meta: { title: '新建报价', hidden: true },
    },
    // ===== 快递-编辑报价（必须在 :id 之前）=====
    {
      path: 'express/quotation/edit/:id',
      name: 'QuotationEdit',
      component: () => import('@/views/express/quotation/QuotationEdit.vue'),
      meta: { title: '编辑报价', hidden: true },
    },
    // ===== 快递-网点名称映射 =====
    {
      path: 'express/network-point-aliases',
      name: 'NetworkPointAliasManage',
      component: () => import('@/views/express/network-point/AliasManagement.vue'),
      meta: { title: '网点名称映射' },
    },
    // ===== 快递-出港加收管理 =====
    {
      path: 'express/surcharge',
      name: 'SurchargeList',
      component: () => import('@/views/express/surcharge/SurchargeList.vue'),
      meta: { title: '出港加收管理' },
    },
    {
      path: 'express/surcharge/create',
      name: 'SurchargeCreate',
      component: () => import('@/views/express/surcharge/SurchargeEdit.vue'),
      meta: { title: '新建加收', hidden: true },
    },
    {
      path: 'express/surcharge/edit/:id',
      name: 'SurchargeEdit',
      component: () => import('@/views/express/surcharge/SurchargeEdit.vue'),
      meta: { title: '编辑加收', hidden: true },
    },
    // ===== 快递成本方案 =====
    {
      path: 'express/cost-plan',
      name: 'CostPlanList',
      component: () => import('@/views/express/cost-plan/CostPlanList.vue'),
      meta: { title: '快递成本方案' },
    },
    // ===== 快递成本方案-新建 =====
    {
      path: 'express/cost-plan/create',
      name: 'CostPlanCreate',
      component: () => import('@/views/express/cost-plan/CostPlanEdit.vue'),
      meta: { title: '新建快递成本方案', hidden: true },
    },
    // ===== 快递成本方案-编辑/详情 =====
    {
      path: 'express/cost-plan/edit/:id',
      name: 'CostPlanEdit',
      component: () => import('@/views/express/cost-plan/CostPlanEdit.vue'),
      meta: { title: '快递成本方案详情', hidden: true },
    },
    {
      path: 'express/cost-plan/:planId/item/:itemId',
      name: 'CostItemDetail',
      component: () => import('@/views/express/cost-plan/CostItemDetail.vue'),
      meta: { title: '成本项编辑', hidden: true },
    },
    // ===== 快递-成本项目 =====
    {
      path: 'express/cost-item',
      name: 'CostItemList',
      component: () => import('@/views/express/cost-plan/CostItemList.vue'),
      meta: { title: '成本项目' },
    },
    // ===== 快递-政策返利 =====
    {
      path: 'express/policy-rebate',
      name: 'PolicyRebateList',
      component: () => import('@/views/express/policy-rebate/PolicyRebateList.vue'),
      meta: { title: '政策返利' },
    },
    {
      path: 'express/policy-rebate/settlement',
      name: 'PolicyRebateSettlement',
      component: () => import('@/views/express/policy-rebate/RebateSettlement.vue'),
      meta: { title: '返利结算' },
    },
    {
      path: 'express/policy-rebate/simulation',
      name: 'PolicyRebateSimulation',
      component: () => import('@/views/express/policy-rebate/RebateSimulation.vue'),
      meta: { title: '返利模拟' },
    },
    // ===== 快递-账单管理 =====
    {
      path: 'express/billing',
      name: 'ExpressBilling',
      redirect: '/express/billing/recalc',
      meta: { title: '账单管理' },
      children: [
        {
          path: 'recalc',
          name: 'ExpressBillingRecalc',
          component: () => import('@/views/express/billing/BillingRecalc.vue'),
          meta: { title: '账单重算' },
        },
        {
          path: 'dispute',
          name: 'ExpressBillingDispute',
          component: () => import('@/views/express/billing/BillingDispute.vue'),
          meta: { title: '账单异议' },
        },
      ],
    },
    // ===== 快递-质量管理看板 =====
    {
      path: 'express/quality-center/dashboard',
      name: 'QualityCenterDashboard',
      component: () => import('@/views/express/quality-center/Dashboard.vue'),
      meta: { title: '质量管理看板' },
    },
    // ===== 快递-报表分析 =====
    {
      path: 'express/report/profit',
      name: 'ExpressProfitReport',
      component: () => import('@/views/express/report/ProfitAnalysis.vue'),
      meta: { title: '毛利分析报表' },
    },
    {
      path: 'express/report/weight',
      name: 'ExpressWeightReport',
      component: () => import('@/views/express/report/WeightSegment.vue'),
      meta: { title: '重量分析报表' },
    },
    {
      path: 'express/report/flow',
      name: 'ExpressFlowReport',
      component: () => import('@/views/express/report/FlowAnalysis.vue'),
      meta: { title: '流量分析报表' },
    },
    // ===== 积分管理 =====
    {
      path: 'points',
      name: 'Points',
      redirect: '/points/dashboard',
      meta: { title: '积分管理', icon: 'TrophyOutlined' },
      children: [
        {
          path: 'dashboard',
          name: 'PointDashboard',
          component: () => import('@/views/points/PointDashboard.vue'),
          meta: { title: '积分总览' },
        },
        {
          path: 'records',
          name: 'PointRecords',
          component: () => import('@/views/points/PointRecords.vue'),
          meta: { title: '积分明细' },
        },
        {
          path: 'applications',
          name: 'PointApplication',
          component: () => import('@/views/points/PointApplication.vue'),
          meta: { title: '积分申请' },
        },
        {
          path: 'rankings',
          name: 'PointRanking',
          component: () => import('@/views/points/PointRanking.vue'),
          meta: { title: '积分排行榜' },
        },
        {
          path: 'redeem',
          name: 'PointRedeem',
          component: () => import('@/views/points/RedeemShop.vue'),
          meta: { title: '积分商城' },
        },
        {
          path: 'rules',
          name: 'PointRules',
          component: () => import('@/views/points/PointRules.vue'),
          meta: { title: '规则管理' },
        },
        {
          path: 'sources',
          name: 'PointSources',
          component: () => import('@/views/points/PointSources.vue'),
          meta: { title: '来源管理' },
        },
        {
          path: 'redeem/manage',
          name: 'PointRedeemManage',
          component: () => import('@/views/points/RedeemManage.vue'),
          meta: { title: '商品管理' },
        },
        {
          path: 'quotas',
          name: 'PointManagerQuota',
          component: () => import('@/views/points/ManagerQuota.vue'),
          meta: { title: '配额管理' },
        },
      ],
    },
    // ===== KSF 管理 =====
    {
      path: 'ksf',
      name: 'Ksf',
      redirect: '/ksf/dashboard',
      meta: { title: 'KSF管理', icon: 'LineChartOutlined' },
      children: [
        {
          path: 'dashboard',
          name: 'KsfDashboard',
          component: () => import('@/views/ksf/KsfDashboard.vue'),
          meta: { title: 'KSF总览' },
        },
        {
          path: 'indicators',
          name: 'KsfIndicators',
          component: () => import('@/views/ksf/KsfIndicators.vue'),
          meta: { title: '指标库' },
        },
        {
          path: 'plans',
          name: 'KsfPlans',
          component: () => import('@/views/ksf/KsfPlans.vue'),
          meta: { title: '岗位方案' },
        },
        {
          path: 'results',
          name: 'KsfResults',
          component: () => import('@/views/ksf/KsfResults.vue'),
          meta: { title: '核算结果' },
        },
        {
          path: 'my-progress',
          name: 'KsfMyProgress',
          component: () => import('@/views/ksf/KsfMyProgress.vue'),
          meta: { title: '我的KSF' },
        },
      ],
    },
    // ===== PPV 管理 =====
    {
      path: 'ppv',
      name: 'Ppv',
      redirect: '/ppv/dashboard',
      meta: { title: 'PPV管理', icon: 'FundOutlined' },
      children: [
        {
          path: 'dashboard',
          name: 'PpvDashboard',
          component: () => import('@/views/ppv/PpvDashboard.vue'),
          meta: { title: 'PPV总览' },
        },
        {
          path: 'templates',
          name: 'PpvTemplates',
          component: () => import('@/views/ppv/PpvTemplates.vue'),
          meta: { title: '产值模板' },
        },
        {
          path: 'records',
          name: 'PpvRecords',
          component: () => import('@/views/ppv/PpvRecords.vue'),
          meta: { title: '产值记录' },
        },
        {
          path: 'results',
          name: 'PpvResults',
          component: () => import('@/views/ppv/PpvResults.vue'),
          meta: { title: '月度汇总' },
        },
        {
          path: 'my-progress',
          name: 'PpvMyProgress',
          component: () => import('@/views/ppv/PpvMyProgress.vue'),
          meta: { title: '我的产值' },
        },
      ],
    },
    // ===== 薪酬管理 =====
    {
      path: 'salary',
      name: 'Salary',
      redirect: '/salary/dashboard',
      meta: { title: '薪酬管理', icon: 'DollarOutlined' },
      children: [
        { path: 'dashboard', name: 'SalaryDashboard', component: () => import('@/views/salary/SalaryDashboard.vue'), meta: { title: '薪酬总览' } },
        { path: 'grades', name: 'SalaryGrades', component: () => import('@/views/salary/SalaryGrades.vue'), meta: { title: '薪酬档位' } },
        { path: 'archives', name: 'SalaryArchives', component: () => import('@/views/salary/SalaryArchives.vue'), meta: { title: '员工薪酬档案' } },
        { path: 'payrolls', name: 'SalaryPayrolls', component: () => import('@/views/salary/SalaryPayrolls.vue'), meta: { title: '月度工资单' } },
        { path: 'my-payslip', name: 'SalaryMyPayslip', component: () => import('@/views/salary/SalaryMyPayslip.vue'), meta: { title: '我的工资条' } },
        { path: 'promotion-rules', name: 'PromotionRules', component: () => import('@/views/salary/PromotionRules.vue'), meta: { title: '晋升规则' } },
        { path: 'promotion-reviews', name: 'PromotionReviews', component: () => import('@/views/salary/PromotionReviews.vue'), meta: { title: '晋升评审' } },
      ],
    },
    // ===== 会务管理-活动详情工作台 =====
    {
      path: 'conference/events/:id',
      name: 'EventWorkbench',
      component: () => import('@/views/conference/EventWorkbench.vue'),
      meta: { title: '活动详情', hidden: true },
    },
    // ===== 卡片流转管理 =====
    {
      path: 'cardflow/definitions',
      name: 'FlowDefinitionList',
      component: () => import('@/views/cardflow/FlowDefinitionListPage.vue'),
      meta: { title: '卡片流程管理', module: 'cardflow' },
    },
    {
      path: 'cardflow/groups',
      name: 'FlowGroupList',
      component: () => import('@/views/cardflow/FlowGroupListPage.vue'),
      meta: { title: '流程分组管理', module: 'cardflow' },
    },
    {
      path: 'cardflow/monitor',
      name: 'CardFlowMonitor',
      component: () => import('@/views/cardflow/CardFlowMonitorPage.vue'),
      meta: { title: '流程监控看板', module: 'cardflow' },
    },
    {
      path: 'cardflow/upload-center',
      name: 'CardFlowUploadCenter',
      component: () => import('@/views/cardflow/upload/UploadCenter.vue'),
      meta: { title: '上传中心', menuCode: 'cardflow:upload-center', module: 'cardflow' },
    },
    {
      path: 'cardflow/issues',
      name: 'CardFlowIssues',
      component: () => import('@/views/cardflow/issues/IssueWorktable.vue'),
      meta: { title: '异常处理', module: 'cardflow' },
    },
    {
      path: 'cardflow/import-validation/:batchId',
      name: 'CardFlowImportValidation',
      component: () => import('@/views/cardflow/import-validation/ImportCalculationValidationWorkbench.vue'),
      meta: { title: '导入计算验证', hidden: true, module: 'cardflow' },
    },
    {
      path: 'cardflow/batches',
      name: 'CardFlowBatches',
      redirect: '/cardflow/upload-center',
      meta: { title: '卡片流程上传中心', hidden: true, module: 'cardflow' },
    },
    // ===== CardFlow 文件管理 =====
    {
      path: 'cardflow/file-manager',
      name: 'CardFlowFileManager',
      component: () => import('@/views/cardflow/file-manager/FileManager.vue'),
      meta: { title: '文件管理', menuCode: 'cardflow:file-manager', module: 'cardflow' },
    },
    // V2 冻结（2026-06 简化）：编排中心 / 下载自动化 / 质量规则 路由暂下线，组件保留于磁盘，详见 docs/superpowers/specs/2026-06-16-cardflow-简化瘦身-design.md
    // ===== CardFlow 暂存数据 =====
    {
      path: 'cardflow/staging',
      name: 'CardFlowStaging',
      component: () => import('@/views/cardflow/staging/StagingBrowser.vue'),
      meta: { title: '暂存数据', menuCode: 'cardflow:staging', module: 'cardflow' },
    },
    // ===== 卡片流转编辑页 =====
    {
      path: 'cardflow/cards/:id',
      name: 'CardDetail',
      component: () => import('@/views/cardflow/CardDetailPage.vue'),
      meta: { title: '卡片详情', hidden: true, module: 'cardflow' },
    },
    {
      path: 'cardflow/definition/edit',
      name: 'FlowDefinitionEdit',
      component: () => import('@/views/cardflow/FlowDefinitionEditPage.vue'),
      meta: { title: '编辑流程定义', hidden: true, module: 'cardflow' },
    },
    {
      path: 'cardflow/group/edit',
      name: 'FlowGroupEdit',
      component: () => import('@/views/cardflow/FlowGroupEditPage.vue'),
      meta: { title: '编辑流程分组', hidden: true, module: 'cardflow' },
    },
    {
      path: 'cardflow/versions',
      name: 'VersionHistory',
      component: () => import('@/views/cardflow/VersionHistoryPage.vue'),
      meta: { title: '版本历史', hidden: true, module: 'cardflow' },
    },
    {
      path: 'cardflow/definitions/:id/versions',
      name: 'CardFlowVersionHistory',
      component: () => import('@/views/cardflow/VersionHistoryPage.vue'),
      meta: { title: '版本历史', hidden: true, permission: 'cardflow:definition:manage', module: 'cardflow' },
    },
    // ===== 卡片流转：统一审批入口与管理页 =====
    {
      path: 'cardflow/approve/:id',
      name: 'CardFlowApprove',
      component: () => import('@/views/cardflow/CardApprovePage.vue'),
      meta: { title: '审批', hidden: true, permission: '*', module: 'cardflow' },
    },
    // V2 冻结：审批委托引擎未消费（设计决策 D1，2026-06-16 简化瘦身），入口暂下线
    // {
    //   path: 'cardflow/delegations',
    //   name: 'CardFlowDelegations',
    //   component: () => import('@/views/cardflow/DelegationPage.vue'),
    //   meta: { title: '委托管理', permission: '*', module: 'cardflow' },
    // },
    {
      path: 'cardflow/settings/notification',
      name: 'CardFlowNotification',
      component: () => import('@/views/cardflow/NotificationSettingsPage.vue'),
      meta: { title: '通知配置', permission: 'cardflow:admin', module: 'cardflow' },
    },
    {
      path: 'cardflow/stats',
      name: 'CardFlowStats',
      component: () => import('@/views/cardflow/TodoStatsPage.vue'),
      meta: { title: '待办统计', permission: 'cardflow:admin', module: 'cardflow' },
    },
    {
      path: 'cardflow/logs',
      name: 'CardFlowLogs',
      component: () => import('@/views/cardflow/AuditLogPage.vue'),
      meta: { title: '操作日志', permission: 'cardflow:admin', module: 'cardflow' },
    },
    // ===== 数据导入 =====
    {
      path: 'dataimport',
      name: 'DataImport',
      redirect: '/dataimport/hangfire',
      meta: { title: '数据导入', icon: 'Upload', module: 'cardflow' },
      children: [
        {
          path: 'hangfire',
          name: 'HangfirePanel',
          component: () => import('@/views/dataimport/hangfire/HangfirePanel.vue'),
          meta: { title: '任务调度', module: 'cardflow' },
        },
      ],
    },
    // ===== CardFlow 首页总览 =====
    {
      path: 'cardflow/home',
      name: 'CardFlowHome',
      component: () => import('@/views/cardflow/CardFlowHome.vue'),
      meta: { title: '首页总览', menuCode: 'cardflow:home', module: 'cardflow' },
    },
    // ===== CardFlow 自动插件管理 =====
    {
      path: 'cardflow/auto-plugin',
      name: 'CardFlowAutoPlugin',
      component: () => import('@/views/cardflow/auto-plugin/AutoPluginList.vue'),
      meta: { title: '自动插件管理', menuCode: 'cardflow:auto-plugin', module: 'cardflow' },
    },
    {
      path: 'cardflow/auto-plugin/:typeCode/rules',
      name: 'CardFlowAutoPluginRuleList',
      component: () => import('@/views/cardflow/auto-plugin/AutoPluginRuleList.vue'),
      meta: { title: '自动插件规则', hidden: true, module: 'cardflow' },
    },
    {
      path: 'cardflow/auto-plugin/auto-voucher-wizard/:id',
      name: 'AutoVoucherWizard',
      component: () => import('@/views/cardflow/auto-plugin/auto-voucher/AutoVoucherWizard.vue'),
      meta: { title: 'AutoVoucher 规则配置', hidden: true, module: 'cardflow' },
    },
    // ===== CardFlow 任务调度 =====
    {
      path: 'cardflow/hangfire',
      name: 'CardFlowHangfire',
      component: () => import('@/views/cardflow/hangfire/HangfirePanel.vue'),
      meta: { title: '任务调度', menuCode: 'cardflow:hangfire', module: 'cardflow' },
    },
    // ===== CardFlow 费用分类 =====
    {
      path: 'cardflow/expense-classification/:batchId',
      name: 'ExpenseClassification',
      component: () => import('@/views/cardflow/ExpenseClassification.vue'),
      meta: { title: '费用分类确认', hidden: true, module: 'cardflow' },
    },
    // ===== CardFlow 导入规则 =====
    {
      path: 'cardflow/import-rules',
      name: 'CardFlowImportRules',
      component: () => import('@/views/cardflow/import-rules/ImportRulesEditor.vue'),
      meta: { title: '导入规则配置', menuCode: 'cardflow:import-rules', module: 'cardflow' },
    },
    // ===== 旧路径重定向（兼容收藏链接）=====
    {
      path: 'datacenter',
      redirect: '/cardflow/home',
    },
    {
      path: 'datacenter/:_(.+)+',
      redirect: to => `/cardflow/${to.params._}`,
    },
    // ===== 质量中心模块 =====
    {
      path: 'quality',
      name: 'Quality',
      redirect: '/quality/dashboard',
      meta: { title: '质量中心', module: 'quality' },
      children: [
        { path: 'dashboard', name: 'QualityDashboard', component: () => import('@/views/quality/dashboard/QualityDashboard.vue'), meta: { title: '质量看板', module: 'quality' } },
        { path: 'exceptions', name: 'QualityExceptions', component: () => import('@/views/quality/exceptions/ExceptionList.vue'), meta: { title: '异常管理', module: 'quality' } },
        { path: 'exceptions/analysis', name: 'QualityExceptionAnalysis', component: () => import('@/views/quality/exceptions/ExceptionAnalysis.vue'), meta: { title: '异常分析', module: 'quality' } },
        { path: 'rules/detection', name: 'QualityDetectionRules', component: () => import('@/views/quality/rules/DetectionRules.vue'), meta: { title: '检测规则', module: 'quality' } },
        { path: 'rules/alert', name: 'QualityAlertRules', component: () => import('@/views/quality/rules/AlertRules.vue'), meta: { title: '预警规则', module: 'quality' } },
        { path: 'rules/dispatch', name: 'QualityDispatchRules', component: () => import('@/views/quality/rules/DispatchRules.vue'), meta: { title: '派发规则', module: 'quality' } },
        { path: 'rules/quality-check', name: 'QualityCheckRules', component: () => import('@/views/quality/rules/QualityRuleList.vue'), meta: { title: '质量规则', module: 'quality' } },
        { path: 'rules/issue-types', name: 'QualityIssueTypes', component: () => import('@/views/quality/rules/IssueTypeConfig.vue'), meta: { title: '问题类型配置', module: 'quality' } },
        { path: 'review/plan', name: 'QualityReviewPlan', component: () => import('@/views/quality/review/ReviewPlan.vue'), meta: { title: '复盘计划', module: 'quality' } },
        { path: 'review/records', name: 'QualityReviewRecords', component: () => import('@/views/quality/review/ReviewRecord.vue'), meta: { title: '复盘记录', module: 'quality' } },
        { path: 'review/improvements', name: 'QualityImprovements', component: () => import('@/views/quality/review/ImprovementTrack.vue'), meta: { title: '改进跟踪', module: 'quality' } },
        { path: 'knowledge/cases', name: 'QualityCases', component: () => import('@/views/quality/knowledge/CaseLibrary.vue'), meta: { title: '案例库', module: 'quality' } },
        { path: 'knowledge/sop', name: 'QualitySop', component: () => import('@/views/quality/knowledge/SopDocument.vue'), meta: { title: 'SOP文档', module: 'quality' } },
        { path: 'performance/log', name: 'QualityProcessingLog', component: () => import('@/views/quality/performance/ProcessingLog.vue'), meta: { title: '处理记录', module: 'quality' } },
        { path: 'performance/stats', name: 'QualityPerformanceStats', component: () => import('@/views/quality/performance/PerformanceStats.vue'), meta: { title: '绩效统计', module: 'quality' } },
        { path: 'carrier/network', name: 'CarrierNetworkOverview', component: () => import('@/views/quality/carrier/NetworkOverview.vue'), meta: { title: '网点总览', module: 'quality' } },
        { path: 'carrier/employee', name: 'CarrierEmployeeQuality', component: () => import('@/views/quality/carrier/EmployeeQuality.vue'), meta: { title: '员工质量', module: 'quality' } },
        { path: 'carrier/issues', name: 'CarrierIssueTracking', component: () => import('@/views/quality/carrier/IssueTracking.vue'), meta: { title: '问题件追踪', module: 'quality' } },
        { path: 'carrier/claim', name: 'CarrierMasterDataClaim', component: () => import('@/views/quality/carrier/MasterDataClaim.vue'), meta: { title: '主数据认领', module: 'quality' } },
      ],
    },
  ],
}
