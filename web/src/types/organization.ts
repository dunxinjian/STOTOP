// ===================== 组织架构相关类型 =====================

export interface OrganizationDto {
  id: number
  fuid: string
  name: string
  code: string
  parentId: number
  type: string                    // 兼容字段，实际显示优先使用 typeName
  typeId?: number
  typeCode?: string
  typeName?: string
  typeLevel?: number
  canBindAccountSet?: boolean
  canSwitch?: boolean
  sort: number
  status: number
  dingTalkDeptId: string | null
  dingTalkBindStatus: number
  dingTalkDeptName: string | null
  managerId: number | null
  managerName: string | null
  headcount: number | null        // 编制人数
  actualCount: number | null      // 实际人数
  description: string | null
  isSwitchable: boolean
  createTime: string
  children: OrganizationDto[]
}

export interface CreateOrganizationRequest {
  name: string
  code: string
  parentId?: number
  typeId?: number
  type?: string
  sort?: number
  status?: number
  dingTalkDeptId?: string
  managerId?: number
  headcount?: number
  description?: string
  isSwitchable?: boolean
}

export interface UpdateOrganizationRequest {
  name: string
  code: string
  parentId: number
  typeId: number
  type?: string
  sort: number
  status?: number
  dingTalkDeptId?: string
  managerId?: number
  headcount?: number
  description?: string
  isSwitchable?: boolean
}

// ===================== 岗位相关类型 =====================

export interface PositionDto {
  id: number
  uid: string
  name: string
  code: string
  description: string | null
  status: number
  dingTalkPositionId: string | null
  dingTalkBindStatus: number
  sort: number
  createTime: string
  updateTime: string
  departmentCount: number
  userCount: number
  departments?: PositionDepartmentDto[]
  users?: PositionUserDto[]
}

export interface PositionDepartmentDto {
  organizationId: number
  organizationName: string
}

export interface PositionUserDto {
  userId: number
  userName: string
  isPrimary: number
}

export interface CreatePositionRequest {
  name: string
  code: string
  description?: string
  status?: number
  sort?: number
  organizationIds?: number[]
}

export interface UpdatePositionRequest {
  name: string
  code: string
  description?: string
  status: number
  sort: number
}

export interface AssignPositionOrganizationsRequest {
  organizationIds: number[]
}

export interface AssignPositionUsersRequest {
  userIds: number[]
}

// ===================== 钉钉相关类型 =====================

export interface DingTalkDepartmentDto {
  deptId: number
  name: string
  parentId: number
  isBound: boolean
  localOrgId: number | null
}

export interface DingTalkUserDto {
  userId: string
  name: string
  mobile: string | null
  email: string | null
  title: string | null
  jobNumber: string | null
  deptIdList: number[] | null
  isBound: boolean
  localUserId: number | null
}

export interface DingTalkPositionDto {
  positionId: string
  name: string
  isBound: boolean
  localPositionId: number | null
}

export interface BindOrganizationRequest {
  orgId: number
  dingTalkDeptId: string
}

export interface BindUserRequest {
  userId: number
  dingTalkUserId: string
}

export interface BindPositionRequest {
  positionId: number
  dingTalkPositionId: string
}

export interface SyncResultDto {
  totalCount: number
  successCount: number
  failCount: number
  skipCount: number
  errors: string[] | null
}

// ===================== 变更记录类型 =====================

export interface ChangeLogDto {
  id: number
  businessType: string
  businessId: number
  businessName: string
  operationType: string
  changeContent: string    // JSON 字符串
  operatorId: number | null
  operatorName: string | null
  operationTime: string
  dingTalkSyncStatus: number
  dingTalkSyncTime: string | null
  dingTalkSyncResult: string | null
  remark: string | null
}

export interface ChangeLogQueryRequest {
  businessType?: string
  businessId?: number
  operationType?: string
  operatorId?: number
  startTime?: string
  endTime?: string
  pageIndex?: number
  pageSize?: number
}

// ===================== 组织上下文类型 =====================

export interface UserOrganizationDto {
  id: number
  userId: number
  orgId: number
  orgName: string
  orgType: string
  switchableOrgId?: number
  switchableOrgName?: string
  directSuperiorId: number | null
  directSuperiorName: string | null
  isPrimaryOrg: number
  position: string | null
  jobNumber: string | null
  entryDate: string | null
  status: number
}

export interface SwitchOrganizationRequest {
  orgId: number
}

export interface SwitchOrganizationResponse {
  orgId: number
  orgName: string
  orgType: string
  roles: string[]
  permissions: string[]
  menus: MenuDto[]
}

export interface MenuDto {
  id: number
  name: string
  code: string
  type: string
  icon: string | null
  route: string | null
  componentPath: string | null
  sort: number
  parentId: number
  isVisible: number
  children: MenuDto[]
}

export interface AddUserToOrganizationRequest {
  userId: number
  orgId: number
  directSuperiorId?: number
  isPrimaryOrg?: number
  position?: string
  jobNumber?: string
  entryDate?: string
}

export interface UpdateUserOrganizationRequest {
  directSuperiorId?: number
  isPrimaryOrg?: number
  position?: string
  jobNumber?: string
  entryDate?: string
  status?: number
}
