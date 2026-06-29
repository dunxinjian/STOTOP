// 三轮车管理模块 API
import { get, post, put, del } from './request'

// ==================== 类型定义 ====================

// 分页结果
export interface PagedResult<T = any> {
  items: T[]
  totalCount: number
  pageIndex: number
  pageSize: number
}

// ==================== 车辆台账 ====================

export interface VehicleDto {
  id: number
  uid: string
  code: string
  plateNumber?: string
  brand?: string
  frameNumber?: string
  ownershipType: number // 1=公司, 2=员工个人
  ownerId?: number
  ownerName?: string
  purchaseDate?: string
  purchasePrice?: number
  vehicleStatus: number // 1=闲置, 2=使用中, 3=维修中, 4=报废
  color?: string
  gpsDeviceNo?: string
  image?: string
  remark?: string
  status: number
  creatorId?: number
  createdTime: string
  updatedTime: string
  assignmentCount: number
  maintenanceCount: number
}

export interface VehicleListItemDto {
  id: number
  code: string
  plateNumber?: string
  brand?: string
  ownershipType: number
  ownerName?: string
  vehicleStatus: number
  color?: string
  status: number
  createdTime: string
}

export interface VehicleStatisticsDto {
  totalCount: number
  statusGroups: { status: number; count: number }[]
  ownershipGroups: { ownershipType: number; count: number }[]
}

// ==================== 车辆分配 ====================

export interface VehicleAssignmentDto {
  id: number
  uid: string
  vehicleId: number
  employeeId: number
  employeeName?: string
  assignmentType: number // 1=免费使用, 2=租赁
  startDate: string
  endDate?: string
  assignmentStatus: number // 1=使用中, 2=已归还
  remark?: string
  createdTime: string
  vehicleCode?: string
}

export interface VehicleAssignmentListItemDto {
  id: number
  vehicleId: number
  vehicleCode?: string
  employeeId: number
  employeeName?: string
  assignmentType: number
  startDate: string
  endDate?: string
  assignmentStatus: number
  createdTime: string
}

// ==================== 租赁费用标准 ====================

export interface RentalStandardDto {
  id: number
  uid: string
  name: string
  amount: number
  chargeCycle: number // 1=月, 2=季, 3=年
  effectiveDate: string
  expiryDate?: string
  remark?: string
  status: number
  createdTime: string
}

export interface RentalStandardListItemDto {
  id: number
  name: string
  amount: number
  chargeCycle: number
  effectiveDate: string
  expiryDate?: string
  status: number
}

// ==================== 租赁收费记录 ====================

export interface RentalChargeDto {
  id: number
  uid: string
  vehicleId: number
  assignmentId: number
  employeeId: number
  employeeName?: string
  rentalStandardId?: number
  chargePeriodStart: string
  chargePeriodEnd: string
  amountDue: number
  amountPaid?: number
  chargeStatus: number // 1=待收, 2=已收, 3=逾期, 4=减免
  chargeDate?: string
  voucherId?: number
  remark?: string
  createdTime: string
}

export interface RentalChargeListItemDto {
  id: number
  vehicleId: number
  vehicleCode?: string
  employeeId: number
  employeeName?: string
  chargePeriodStart: string
  chargePeriodEnd: string
  amountDue: number
  amountPaid?: number
  chargeStatus: number
  chargeDate?: string
}

// ==================== 维修记录 ====================

export interface VehicleMaintenanceDto {
  id: number
  uid: string
  vehicleId: number
  maintenanceDate: string
  maintenanceType?: string
  maintenanceItem: string
  maintenanceUnit?: string
  maintenanceCost?: number
  costBearer: number // 1=公司, 2=员工
  completionDate?: string
  maintenanceStatus: number // 1=维修中, 2=已完成
  remark?: string
  createdTime: string
}

export interface VehicleMaintenanceListItemDto {
  id: number
  vehicleId: number
  vehicleCode?: string
  maintenanceDate: string
  maintenanceType?: string
  maintenanceItem: string
  maintenanceCost?: number
  costBearer: number
  maintenanceStatus: number
}

// ==================== GPS ====================

export interface VehicleLocationDto {
  vehicleId: number
  vehicleCode?: string
  longitude: number
  latitude: number
  speed?: number
  reportTime?: string
  address?: string
}

export interface VehicleTrackPointDto {
  longitude: number
  latitude: number
  speed?: number
  reportTime: string
}

// ==================== 车辆台账 API ====================

// 车辆列表（分页）
export function getVehicleList(params: {
  pageIndex: number
  pageSize: number
  keyword?: string
  vehicleStatus?: number
  ownershipType?: number
}): Promise<PagedResult<VehicleListItemDto>> {
  return get('/vehicle/vehicles', params)
}

// 车辆详情
export function getVehicleDetail(id: number): Promise<VehicleDto> {
  return get(`/vehicle/vehicles/${id}`)
}

// 创建车辆
export function createVehicle(data: object): Promise<VehicleDto> {
  return post('/vehicle/vehicles', data)
}

// 更新车辆
export function updateVehicle(id: number, data: object): Promise<VehicleDto> {
  return put(`/vehicle/vehicles/${id}`, data)
}

// 删除车辆
export function deleteVehicle(id: number): Promise<boolean> {
  return del(`/vehicle/vehicles/${id}`)
}

// 车辆统计
export function getVehicleStatistics(): Promise<VehicleStatisticsDto> {
  return get('/vehicle/vehicles/statistics')
}

// ==================== 车辆分配 API ====================

// 分配列表（分页）
export function getAssignmentList(params: {
  pageIndex: number
  pageSize: number
  vehicleId?: number
  employeeId?: number
  assignmentStatus?: number
}): Promise<PagedResult<VehicleAssignmentListItemDto>> {
  return get('/vehicle/assignments', params)
}

// 分配详情
export function getAssignmentDetail(id: number): Promise<VehicleAssignmentDto> {
  return get(`/vehicle/assignments/${id}`)
}

// 创建分配
export function createAssignment(data: object): Promise<VehicleAssignmentDto> {
  return post('/vehicle/assignments', data)
}

// 归还车辆
export function returnVehicle(id: number, data: object): Promise<boolean> {
  return put(`/vehicle/assignments/${id}/return`, data)
}

// ==================== 租赁费用标准 API ====================

// 标准列表（分页）
export function getRentalStandardList(params: {
  pageIndex: number
  pageSize: number
  keyword?: string
  status?: number
}): Promise<PagedResult<RentalStandardListItemDto>> {
  return get('/vehicle/rental-standards', params)
}

// 标准详情
export function getRentalStandardDetail(id: number): Promise<RentalStandardDto> {
  return get(`/vehicle/rental-standards/${id}`)
}

// 创建标准
export function createRentalStandard(data: object): Promise<RentalStandardDto> {
  return post('/vehicle/rental-standards', data)
}

// 更新标准
export function updateRentalStandard(id: number, data: object): Promise<RentalStandardDto> {
  return put(`/vehicle/rental-standards/${id}`, data)
}

// 更新标准状态
export function updateRentalStandardStatus(id: number, status: number): Promise<boolean> {
  return put(`/vehicle/rental-standards/${id}/status`, { status })
}

// 获取启用的标准列表
export function getEnabledRentalStandards(): Promise<RentalStandardListItemDto[]> {
  return get('/vehicle/rental-standards/enabled')
}

// ==================== 租赁收费 API ====================

// 收费列表（分页）
export function getRentalChargeList(params: {
  pageIndex: number
  pageSize: number
  vehicleId?: number
  employeeId?: number
  chargeStatus?: number
}): Promise<PagedResult<RentalChargeListItemDto>> {
  return get('/vehicle/rental-charges', params)
}

// 收费详情
export function getRentalChargeDetail(id: number): Promise<RentalChargeDto> {
  return get(`/vehicle/rental-charges/${id}`)
}

// 生成收费记录
export function generateRentalCharges(data: object): Promise<number> {
  return post('/vehicle/rental-charges/generate', data)
}

// 确认收费
export function confirmRentalCharge(id: number, data: object): Promise<boolean> {
  return put(`/vehicle/rental-charges/${id}/confirm`, data)
}

// 减免收费
export function waiveRentalCharge(id: number, data: object): Promise<boolean> {
  return put(`/vehicle/rental-charges/${id}/waive`, data)
}

// ==================== 维修 API ====================

// 维修列表（分页）
export function getMaintenanceList(params: {
  pageIndex: number
  pageSize: number
  vehicleId?: number
  maintenanceStatus?: number
}): Promise<PagedResult<VehicleMaintenanceListItemDto>> {
  return get('/vehicle/maintenances', params)
}

// 维修详情
export function getMaintenanceDetail(id: number): Promise<VehicleMaintenanceDto> {
  return get(`/vehicle/maintenances/${id}`)
}

// 创建维修
export function createMaintenance(data: object): Promise<VehicleMaintenanceDto> {
  return post('/vehicle/maintenances', data)
}

// 完成维修
export function completeMaintenance(id: number, data: object): Promise<boolean> {
  return put(`/vehicle/maintenances/${id}/complete`, data)
}

// ==================== GPS API ====================

// 获取车辆位置
export function getVehicleLocation(vehicleId: number): Promise<VehicleLocationDto> {
  return get(`/vehicle/gps/${vehicleId}/location`)
}

// 获取车辆轨迹
export function getVehicleTrack(
  vehicleId: number,
  params: { startTime: string; endTime: string }
): Promise<VehicleTrackPointDto[]> {
  return get(`/vehicle/gps/${vehicleId}/track`, params)
}
