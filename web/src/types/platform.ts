// 平台层（多租户）DTO —— 对应后端 STOTOP.Module.System/Dtos/PlatformDtos.cs
// 平台端点走 [PlatformOnly] 授权（SysUser.F是否平台超管），与租户内 admin 解耦。

/** 租户读视图（对应 PlatformTenantDto） */
export interface TenantDto {
  id: number
  name: string
  code: string
  /** 组织树根 FID */
  rootOrgId: number
  /** 账套绑定模式：1 按区域公司 / 2 按网点公司 */
  accountSetBindMode: number
  /** 默认待办渠道：1 钉钉 / 2 企微 / 3 双推 */
  defaultTodoChannel: number
  planId?: number | null
  activatedAt?: string | null
  expireAt?: string | null
  /** 状态：1 试用 / 2 正式 / 3 停用 / 4 欠费冻结 */
  status: number
  /** 状态中文名（后端派生） */
  statusName: string
}

/** 新租户自动开通（对应 ProvisionTenantRequest，R5）：一次建租户+组织根+初始管理员 */
export interface ProvisionTenantRequest {
  name: string
  code: string
  accountSetBindMode: number
  defaultTodoChannel: number
  planId?: number | null
  expireAt?: string | null
  /** 组织根节点名称 */
  rootOrgName: string
  /** 组织根编码（全局唯一）；缺省取 code */
  rootOrgCode?: string | null
  /** 组织根类别：0 集团 / 1 区域公司 / 2 网点公司 */
  rootOrgKind: number
  /** 初始管理员账号 */
  adminAccount: string
  adminName: string
  adminPhone?: string | null
}

/** 开通结果（对应 ProvisionTenantResult）；tempPassword 仅返回一次 */
export interface ProvisionTenantResult {
  tenantId: number
  rootOrgId: number
  adminUserId: number
  adminRoleId: number
  adminAccount: string
  tempPassword: string
}

/** 改状态（对应 UpdateTenantStatusRequest）；status=4 触发欠费冻结门禁 */
export interface UpdateTenantStatusRequest {
  status: number
}

/** 套餐读视图（对应 PlatformPlanDto） */
export interface PlanDto {
  id: number
  name: string
  code: string
  /** 最大用户数，0=不限 */
  maxUsers: number
  /** 最大网点数，0=不限 */
  maxOutlets: number
  /** 模块开关 JSON */
  moduleFlags?: string | null
  status: number
}

/** 新建/编辑套餐（对应 SavePlatformPlanRequest）；编辑时 code 服务端忽略 */
export interface SavePlanRequest {
  name: string
  code: string
  maxUsers: number
  maxOutlets: number
  moduleFlags?: string | null
}

/** 订阅读视图（对应 PlatformSubscriptionDto） */
export interface SubscriptionDto {
  id: number
  tenantId: number
  planId: number
  periodStart: string
  periodEnd: string
  status: number
}

/** 新建订阅（对应 CreateSubscriptionRequest）；成功后租户转正式(2)并回填开通/到期时间 */
export interface CreateSubscriptionRequest {
  tenantId: number
  planId: number
  periodStart: string
  periodEnd: string
}

/** 租户状态选项（用于筛选/表单） */
export const TENANT_STATUS_OPTIONS: { value: number; label: string }[] = [
  { value: 1, label: '试用' },
  { value: 2, label: '正式' },
  { value: 3, label: '停用' },
  { value: 4, label: '欠费冻结' },
]

/** 账套绑定模式选项 */
export const ACCOUNT_SET_BIND_MODE_OPTIONS: { value: number; label: string }[] = [
  { value: 1, label: '按区域公司' },
  { value: 2, label: '按网点公司' },
]

/** 默认待办渠道选项 */
export const TODO_CHANNEL_OPTIONS: { value: number; label: string }[] = [
  { value: 1, label: '钉钉' },
  { value: 2, label: '企微' },
  { value: 3, label: '双推' },
]

/** 组织根类别选项（合法根类别） */
export const ROOT_ORG_KIND_OPTIONS: { value: number; label: string }[] = [
  { value: 0, label: '集团' },
  { value: 1, label: '区域公司' },
  { value: 2, label: '网点公司' },
]
