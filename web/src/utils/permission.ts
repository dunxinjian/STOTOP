import { useUserStore } from '@/stores/user'

/**
 * 财务模块权限编码常量
 */
export const FinancePermissions = {
  // 凭证权限
  VoucherView:   'finance:voucher:view',
  VoucherCreate: 'finance:voucher:create',
  VoucherEdit:   'finance:voucher:edit',
  VoucherDelete: 'finance:voucher:delete',
  VoucherAudit:  'finance:voucher:audit',
  VoucherPrint:  'finance:voucher:print',

  // 结账权限
  PeriodClose:  'finance:period:close',
  PeriodReopen: 'finance:period:reopen',

  // 日记账权限
  JournalView:   'finance:journal:view',
  JournalCreate: 'finance:journal:create',
  JournalAdjust: 'finance:journal:adjust',

  // 报表权限
  ReportView:   'finance:report:view',
  ReportExport: 'finance:report:export',

  // 科目/账套/模板管理
  AccountManage:    'finance:account:manage',
  AccountSetManage: 'finance:accountset:manage',
  TemplateManage:   'finance:template:manage',

  // 银行对账
  BankReconciliationView:   'finance:bank:view',
  BankReconciliationImport: 'finance:bank:import',
  BankReconciliationMatch:  'finance:bank:match',

  // 发票管理
  InvoiceManage: 'finance:invoice:manage',

  // 公式配置管理
  FormulaManage: 'finance:formula:manage',

  // 资产折旧操作
  AssetDepreciate: 'finance:asset:depreciate',

  // 资金管理
  BankChannelView: 'finance:bankchannel:view',
  BankChannelManage: 'finance:bankchannel:manage',
  BankTransactionView: 'finance:banktransaction:view',
  BankTransactionImport: 'finance:banktransaction:import',
  BankTransactionMatch: 'finance:banktransaction:match',
  VoucherRuleView: 'finance:voucherrule:view',
  VoucherRuleManage: 'finance:voucherrule:manage',
} as const

export type FinancePermissionCode = typeof FinancePermissions[keyof typeof FinancePermissions]

/**
 * CRM 模块权限编码常量
 */
export const CrmPermissions = {
  // 小组管理
  GroupView: 'crm:group:view',
  GroupManage: 'crm:group:manage',

  // 客户管理
  CustomerView: 'crm:customer:view',
  CustomerCreate: 'crm:customer:create',
  CustomerEdit: 'crm:customer:edit',
  CustomerDelete: 'crm:customer:delete',

  // 拜访记录
  VisitView: 'crm:visit:view',
  VisitCreate: 'crm:visit:create',
  VisitEdit: 'crm:visit:edit',

  // 服务工单
  OrderView: 'crm:order:view',
  OrderCreate: 'crm:order:create',
  OrderEdit: 'crm:order:edit',
  OrderAssign: 'crm:order:assign',

  // 服务反馈
  FeedbackView: 'crm:feedback:view',
  FeedbackCreate: 'crm:feedback:create',
  FeedbackHandle: 'crm:feedback:handle',

  // 推荐返佣
  ReferralView: 'crm:referral:view',
  ReferralCreate: 'crm:referral:create',
  CommissionApply: 'crm:commission:apply',

  // 号段池
  WaybillPoolView: 'crm:waybillpool:view',
  WaybillPoolManage: 'crm:waybillpool:manage',

  // 预付款
  PrepaymentView: 'crm:prepayment:view',
  PrepaymentCreate: 'crm:prepayment:create',
  PrepaymentAllocate: 'crm:prepayment:allocate',

  // 毛利
  ProfitView: 'crm:profit:view',
  ProfitCalc: 'crm:profit:calc',

  // 奖金
  BonusView: 'crm:bonus:view',
  BonusManage: 'crm:bonus:manage',
} as const

export type CrmPermissionCode = typeof CrmPermissions[keyof typeof CrmPermissions]

/**
 * 系统模块权限编码常量
 */
export const SystemPermissions = {
  FeedbackManage: 'sys:feedback:manage',
} as const

export type SystemPermissionCode = typeof SystemPermissions[keyof typeof SystemPermissions]

/**
 * 合同模块权限编码常量
 */
export const ContractPermissions = {
  TypeView: 'contract:type:view',
  TypeManage: 'contract:type:manage',
  TemplateView: 'contract:template:view',
  TemplateManage: 'contract:template:manage',
  ContractView: 'contract:view',
  ContractCreate: 'contract:create',
  ContractEdit: 'contract:edit',
  ContractDelete: 'contract:delete',
  ContractApprove: 'contract:approve',
  ReminderView: 'contract:reminder:view',
  ReminderManage: 'contract:reminder:manage',
  ESignView: 'contract:esign:view',
  ESignManage: 'contract:esign:manage',
} as const

export type ContractPermissionCode = typeof ContractPermissions[keyof typeof ContractPermissions]

/**
 * CardFlow 模块权限编码常量
 */
export const CardFlowPermissions = {
  // 模块级
  Manage: 'cardflow:manage',

  // 菜单级
  Home: 'cardflow:home',
  UploadCenter: 'cardflow:upload-center',
  FileManager: 'cardflow:file-manager',
  Automation: 'cardflow:automation',
  Staging: 'cardflow:staging',
  Hangfire: 'cardflow:hangfire',

  // 按钮/操作级 - 导入操作
  ImportUpload: 'cardflow:import:upload',
  ImportProcess: 'cardflow:import:process',

  // 按钮/操作级 - 文件管理
  FileManagerView: 'cardflow:file-manager:view',
  FileManagerDelete: 'cardflow:file-manager:delete',

  // 按钮/操作级 - 自动下载
  AutomationManage: 'cardflow:automation:manage',

  // 按钮/操作级 - 异常派发
  DispatchManage: 'cardflow:dispatch:manage',

  // 菜单级 - 派发规则
  DispatchRule: 'cardflow:dispatch-rules',

  // 按钮/操作级 - 派发规则
  DispatchRuleView: 'cardflow:dispatch-rule:view',
  DispatchRuleManage: 'cardflow:dispatch-rule:manage',

  // 质量中心
  QualityCenter: 'cardflow:quality',
  QualityDashboard: 'cardflow:quality:dashboard',
  QualityIssueTypeView: 'cardflow:quality:issue-type:view',
  QualityIssueTypeManage: 'cardflow:quality:issue-type:manage',
} as const

export type CardFlowPermissionCode = typeof CardFlowPermissions[keyof typeof CardFlowPermissions]

/**
 * 权限检查组合式函数
 * 
 * @example
 * const { has, hasAny } = usePermission()
 * // 在模板中：v-if="has(FinancePermissions.VoucherCreate)"
 */
export function usePermission() {
  const userStore = useUserStore()

  return {
    /**
     * 检查是否拥有指定权限
     */
    has: (code: string) => userStore.hasPermission(code),

    /**
     * 检查是否拥有任意一个指定权限
     */
    hasAny: (...codes: string[]) => userStore.hasAnyPermission(...codes),

    /**
     * 检查是否同时拥有所有指定权限
     */
    hasAll: (...codes: string[]) => codes.every(c => userStore.hasPermission(c)),
  }
}
