import { get, post, put, del } from './request'
import type {
  BindOrganizationRequest,
  BindUserRequest,
  BindPositionRequest,
  CreatePositionRequest,
  UpdatePositionRequest,
  ChangeLogQueryRequest,
  SwitchOrganizationRequest,
  CreateOrganizationRequest,
  UpdateOrganizationRequest,
  AddUserToOrganizationRequest,
  UpdateUserOrganizationRequest,
} from '@/types/organization'

// 重新导出组织架构相关类型
export type {
  OrganizationDto,
  CreateOrganizationRequest,
  UpdateOrganizationRequest,
  PositionDto,
  PositionDepartmentDto,
  PositionUserDto,
  CreatePositionRequest,
  UpdatePositionRequest,
  AssignPositionOrganizationsRequest,
  AssignPositionUsersRequest,
  DingTalkDepartmentDto,
  DingTalkUserDto,
  DingTalkPositionDto,
  BindOrganizationRequest,
  BindUserRequest,
  BindPositionRequest,
  SyncResultDto,
  ChangeLogDto,
  ChangeLogQueryRequest,
  UserOrganizationDto,
  SwitchOrganizationRequest,
  SwitchOrganizationResponse,
  MenuDto,
  AddUserToOrganizationRequest,
  UpdateUserOrganizationRequest,
} from '@/types/organization'

// ==================== 类型定义 ====================

export interface RoleItem {
  id: number
  name: string
  code: string
  description?: string
  status: number
  createdAt?: string
}

export interface RoleSimpleItem {
  id: number
  name: string
  code: string
}

export interface UserItem {
  id: number
  name: string
  account: string
  email?: string
  phone?: string
  orgName?: string
  status: number
  roles?: RoleSimpleItem[]
  createdAt?: string
}

export interface OrgType {
  id: number
  code: string
  name: string
  level: number
  canBindAccountSet: boolean
  canSwitch: boolean
  icon?: string
  sort: number
  description?: string
  isEnabled: boolean
}

export interface OrgTreeNode {
  id: number
  name: string
  code: string
  type: string
  typeId?: number
  typeCode?: string
  typeName?: string
  typeLevel?: number
  canBindAccountSet?: boolean
  canSwitch?: boolean
  parentId?: number
  parentName?: string
  sort: number
  children?: OrgTreeNode[]
  createdAt?: string
}

export interface PermissionTreeNode {
  id: number
  name: string
  code: string
  type: number // 1:模块 2:菜单 3:按钮
  parentId?: number
  route?: string
  componentPath?: string
  icon?: string
  sort: number
  isVisible: boolean
  children?: PermissionTreeNode[]
}

// ==================== 用户管理 ====================

export function getUserList(params?: object) {
  return get('/system/users', params)
}

export function getUserDetail(id: number) {
  return get(`/system/users/${id}`)
}

export function createUser(data: Partial<UserItem>) {
  return post('/system/users', data)
}

export function updateUser(id: number, data: Partial<UserItem>) {
  return put(`/system/users/${id}`, data)
}

export function deleteUser(id: number) {
  return del(`/system/users/${id}`)
}

export function resetPassword(id: number, data: { newPassword: string }) {
  return post(`/system/users/${id}/reset-password`, data)
}

// ==================== 角色管理 ====================

export function getRoleList(params?: object) {
  return get('/system/roles', params)
}

export function getRoleDetail(id: number) {
  return get(`/system/roles/${id}`)
}

export function createRole(data: Partial<RoleItem>) {
  return post('/system/roles', data)
}

export function updateRole(id: number, data: Partial<RoleItem>) {
  return put(`/system/roles/${id}`, data)
}

export function deleteRole(id: number) {
  return del(`/system/roles/${id}`)
}

export function assignRolePermissions(roleId: number, data: { permissionIds: number[] }) {
  return post(`/system/roles/${roleId}/permissions`, data)
}

// ==================== 组织架构管理 ====================

export function getOrganizationTree() {
  return get('/system/organizations/tree', undefined, { timeout: 30000 })
}

export function getOrgTypes() {
  return get('/system/org-types')
}

export function getOrgTypesForAccountSet() {
  return get('/system/org-types/for-account-set')
}

export function createOrganization(data: Partial<OrgTreeNode>) {
  return post('/system/organizations', data)
}

export function updateOrganization(id: number, data: Partial<OrgTreeNode>) {
  return put(`/system/organizations/${id}`, data)
}

export function deleteOrganization(id: number) {
  return del(`/system/organizations/${id}`)
}

// 获取组织关联的账套列表
export function getOrgAccountSets(orgId: number) {
  return get(`/system/organizations/${orgId}/account-sets`)
}

// ==================== 权限/菜单管理 ====================

export function getPermissionTree() {
  return get('/system/permissions/tree', undefined, { timeout: 30000 })
}

export function getMenuTree() {
  return get('/system/permissions/menu-tree', undefined, { timeout: 30000 })
}

export function createPermission(data: Partial<PermissionTreeNode>) {
  return post('/system/permissions', data)
}

export function updatePermission(id: number, data: Partial<PermissionTreeNode>) {
  return put(`/system/permissions/${id}`, data)
}

export function deletePermission(id: number) {
  return del(`/system/permissions/${id}`)
}

// ==================== 数据库状态 ====================

export function getDatabaseStatus() {
  return get('/system/database/status', {}, { silent: true } as any)
}

// ==================== 数据库配置 ====================

export interface DatabaseConfig {
  connectionString?: string
  server?: string
  database?: string
  port?: number
  username?: string
  password?: string
  useWindowsAuth?: boolean
  trustServerCertificate?: boolean
}

export interface TestConnectionResult {
  success: boolean
  message?: string
}

export interface InitializeResult {
  success: boolean
  message?: string
}

export function testDatabaseConnection(data: Partial<DatabaseConfig>) {
  return post('/system/database/test-connection', data)
}

export function initializeDatabase(data?: Partial<DatabaseConfig>) {
  return post('/system/database/initialize', data)
}

export function getDatabaseConfig() {
  return get('/system/database/config')
}

export function updateDatabaseConfig(data: Partial<DatabaseConfig>) {
  return put('/system/database/config', data)
}

// 分析数据库表状态
export function analyzeDatabaseTables(data: { connectionString?: string; connectionId?: number }) {
  return post('/system/database/analyze', data, { timeout: 60000 })
}

// 全新初始化
export function fullInitializeDatabase(data: { connectionString?: string; connectionId?: number }) {
  return post('/system/database/full-initialize', data, { timeout: 300000 })
}

// 保留初始化
export function preserveInitializeDatabase(data: {
  connectionString?: string,
  connectionId?: number,
  tableActions: Record<string, string>,  // 表名 -> "clear" | "preserve"
  backupPath?: string                    // SQL Server 服务器上的备份目录，为空则不备份
}) {
  return post('/system/database/preserve-initialize', data, { timeout: 300000 })
}

// 保留初始化 dry-run 预览
export interface PreserveDryRunResult {
  tablesToDelete: string[]
  tablesToPreserve: string[]
  tablesToRebuild: string[]
  tablesToCreate: string[]
  hangfireTablesToDelete: string[]
  warnings: string[]
  estimatedDataLossRows: number
}

export function previewPreserveInitialize(data: {
  connectionString?: string,
  connectionId?: number,
  tableActions: Record<string, string>
}) {
  return post('/system/database/preserve-initialize/dry-run', data)
}

// 查询 SQL Server 默认备份目录
export function getDefaultBackupDirectory(data: { connectionString?: string; connectionId?: number }) {
  return post('/system/database/backup-directory', data)
}

// 自动备份配置
export interface BackupConfig {
  enabled: boolean
  cronExpression: string
  backupDirectory: string
  fileNamePattern: string
  retentionCount: number
}

export function getBackupConfig() {
  return get('/system/database/backup-config')
}

export function saveBackupConfig(data: BackupConfig) {
  return post('/system/database/backup-config', data)
}

// ==================== 数据库连接管理 ====================

export interface DbConnection {
  id: number
  name: string
  databaseType: string
  connectionType: string
  server: string
  port: number | null
  databaseName: string
  username: string
  password: string
  filePath: string
  windowsAuth: boolean
  trustServerCertificate: boolean
  connectionString: string
  description: string
  status: number
  createdTime: string
  updatedTime: string | null
}

export interface DbConnectionCreate {
  name: string
  databaseType: string
  connectionType?: string
  server?: string
  port?: number | null
  databaseName?: string
  username?: string
  password?: string
  filePath?: string
  windowsAuth?: boolean
  trustServerCertificate?: boolean
  connectionString?: string
  description?: string
  status?: number
}

export interface DbConnectionUpdate extends DbConnectionCreate {}

export interface DbConnectionTest {
  databaseType: string
  server?: string
  port?: number | null
  databaseName?: string
  username?: string
  password?: string
  filePath?: string
  windowsAuth?: boolean
  trustServerCertificate?: boolean
  connectionString?: string
}

export interface DbConnectionOption {
  id: number
  name: string
}

export function getDbConnections() {
  return get('/system/db-connections')
}

export function getDbConnection(id: number) {
  return get(`/system/db-connections/${id}`)
}

export function createDbConnection(data: DbConnectionCreate) {
  return post('/system/db-connections', data)
}

export function updateDbConnection(id: number, data: DbConnectionUpdate) {
  return put(`/system/db-connections/${id}`, data)
}

export function deleteDbConnection(id: number) {
  return del(`/system/db-connections/${id}`)
}

export function testDbConnection(data: DbConnectionTest) {
  return post('/system/db-connections/test', data)
}

export function getDbConnectionOptions() {
  return get('/system/db-connections/options')
}

// 检查系统数据库连接状态（免认证）
export function checkDbConnectionStatus() {
  return get('/system/db-connections/status', undefined, { silent: true } as any)
}

// 获取指定数据库连接的表列表
export function getDbTables(connectionId: number) {
  return get(`/system/db-connections/${connectionId}/tables`)
}

// 获取指定表的字段列表
export function getDbColumns(connectionId: number, tableName: string) {
  return get(`/system/db-connections/${connectionId}/tables/${encodeURIComponent(tableName)}/columns`)
}

// ==================== 系统配置 ====================

// 获取系统配置
export function getSettingByKey(key: string) {
  return get(`/system/settings/${key}`)
}

// 更新系统配置
export function updateSetting(key: string, value: string) {
  return put(`/system/settings/${key}`, { value })
}

// ===================== 钉钉管理 =====================

// 钉钉配置类型
export interface DingTalkConfig {
  id?: number
  appKey: string
  appSecret: string
  corpId: string
  agentId: string
  autoSync: number      // 0=关闭 1=开启
  syncCron: string      // Cron表达式
  lastSyncTime?: string
}

// 获取钉钉全局配置
export function getDingTalkConfig() {
  return get<DingTalkConfig>('/system/dingtalk/config', undefined, { silent: true } as any)
}

// 保存钉钉配置
export function saveDingTalkConfig(data: { appKey: string; appSecret: string; corpId: string; agentId: string }) {
  return post('/system/dingtalk/config', data)
}

/** 获取定时同步配置 */
export function getAutoSyncConfig() {
  return get<{ enabled: boolean; cronExpression: string }>('/system/dingtalk/auto-sync', undefined, { silent: true } as any)
}

/** 更新定时同步配置（即时生效） */
export function updateAutoSync(data: { enabled: boolean; cronExpression: string }) {
  return post('/system/dingtalk/auto-sync', data)
}

export function pullDingTalkDepartments() {
  return post('/system/dingtalk/pull/departments', {}, { timeout: 300000 })
}

export function pullDingTalkUsers() {
  return post('/system/dingtalk/pull/users', {}, { timeout: 300000 })
}

export function pullDingTalkPositions() {
  return post('/system/dingtalk/pull/positions', {}, { timeout: 300000 })
}

export function fullSyncFromDingTalk() {
  return post('/system/dingtalk/sync/full', {}, { timeout: 300000 })
}

/** 同步指定钉钉用户 */
export function syncSpecificDingTalkUsers(dingTalkUserIds: string[]) {
  return post<{ totalCount: number; successCount: number; failCount: number; skipCount: number; errors: string[] | null }>(
    '/system/dingtalk/sync/specific-users',
    { dingTalkUserIds },
    { timeout: 60000 }
  )
}

// 测试钉钉连接
export function testDingTalkConnection() {
  return post('/system/dingtalk/test-connection', {}, { timeout: 30000 })
}

/** 获取钉钉同步状态 */
export function getDingTalkSyncStatus() {
  return get<{
    isSyncing: boolean
    stage?: string
    message?: string
    current?: number
    total?: number
    percent?: number
    startTime?: string
  }>('/system/dingtalk/sync-status', undefined, { silent: true } as any)
}

export function bindOrganization(data: BindOrganizationRequest) {
  return post('/system/dingtalk/bind/organization', data)
}

export function unbindOrganization(id: number) {
  return post(`/system/dingtalk/unbind/organization/${id}`)
}

export function bindUser(data: BindUserRequest) {
  return post('/system/dingtalk/bind/user', data)
}

export function unbindUser(id: number) {
  return post(`/system/dingtalk/unbind/user/${id}`)
}

export function bindPosition(data: BindPositionRequest) {
  return post('/system/dingtalk/bind/position', data)
}

export function unbindPosition(id: number) {
  return post(`/system/dingtalk/unbind/position/${id}`)
}

// ===================== 岗位管理 =====================

export function getPositionList(params?: { pageIndex?: number; pageSize?: number; keyword?: string }) {
  return get('/system/positions', params)
}

export function getPositionDetail(id: number) {
  return get(`/system/positions/${id}`)
}

export function createPosition(data: CreatePositionRequest) {
  return post('/system/positions', data)
}

export function updatePosition(id: number, data: UpdatePositionRequest) {
  return put(`/system/positions/${id}`, data)
}

export function deletePosition(id: number) {
  return del(`/system/positions/${id}`)
}

export function assignPositionOrganizations(id: number, data: { organizationIds: number[] }) {
  return post(`/system/positions/${id}/organizations`, data)
}

export function assignPositionUsers(id: number, data: { userIds: number[] }) {
  return post(`/system/positions/${id}/users`, data)
}

export function getPositionsByOrganization(orgId: number) {
  return get(`/system/positions/by-organization/${orgId}`)
}

export function getPositionsByUser(userId: number) {
  return get(`/system/positions/by-user/${userId}`)
}

// ===================== 变更记录 =====================

export function getChangeLogs(params?: ChangeLogQueryRequest) {
  return get('/system/change-logs', params)
}

export function getChangeLogsByBusiness(businessType: string, businessId: number) {
  return get('/system/change-logs/by-business', { businessType, businessId })
}

// ===================== 组织上下文 =====================

export function getMyOrganizations() {
  return get('/system/org-context/my-organizations', undefined, { silent: true } as any)
}

export function switchOrganization(data: SwitchOrganizationRequest) {
  return post('/system/org-context/switch', data)
}

export function getCurrentOrgContext() {
  return get('/system/org-context/current', undefined, { silent: true } as any)
}

// ===================== 组织架构（扩展） =====================

export function createOrg(data: CreateOrganizationRequest) {
  return post('/system/organizations', data)
}

export function updateOrg(id: number, data: UpdateOrganizationRequest) {
  return put(`/system/organizations/${id}`, data)
}

export function getOrgChart() {
  return get('/system/organizations/chart')
}

export function getAllDepartments() {
  return get('/system/organizations/departments')
}

// ===================== 用户组织任职 =====================

export function getUserOrganizations(userId: number) {
  return get(`/system/users/${userId}/organizations`)
}

export function addUserToOrganization(data: AddUserToOrganizationRequest) {
  return post('/system/org-context/user-organizations', data)
}

export function updateUserOrganization(id: number, data: UpdateUserOrganizationRequest) {
  return put(`/system/org-context/user-organizations/${id}`, data)
}

export function removeUserFromOrganization(id: number) {
  return del(`/system/org-context/user-organizations/${id}`)
}

// ==================== 数据库迁移管理 ====================

export interface SchemaSyncStatus {
  hasPendingChanges: boolean
  pendingCount: number
  seederStatus: string
  lastSyncTime: string | null
}

export interface SchemaChangeItem {
  id: number
  tableName: string
  columnName: string
  changeType: string
  sqlStatement: string
  detectedAt: string
}

export interface SchemaWarningItem {
  tableName: string
  columnName: string
  message: string
}

export interface MigrationHistoryItem {
  id: number
  module: string
  version: number
  description: string
  status: string
  executedTime: string
  durationMs: number | null
}

export function getSchemaSyncStatus() {
  return get('/system/schema-sync/status')
}

export function getPendingChanges() {
  return get('/system/schema-sync/pending')
}

export function executeSchemaSyncChanges(changeIds: number[]) {
  return post('/system/schema-sync/execute', { changeIds })
}

export function skipSchemaSyncChanges(changeIds: number[]) {
  return post('/system/schema-sync/skip', { changeIds })
}

export function getSchemaWarnings() {
  return get('/system/schema-sync/warnings')
}

export function getMigrationHistory(pageIndex: number, pageSize: number) {
  return get('/system/schema-sync/history', { pageIndex, pageSize })
}
