// 宿舍管理模块 API
import { get, post, put, del } from './request'

// ==================== 类型定义 ====================

// 分页结果
export interface PagedResult<T = any> {
  items: T[]
  totalCount: number
  pageIndex: number
  pageSize: number
}

// 楼栋（字段对齐后端：totalFloors/managerId/dormitoryType；roomCount/bedCount/occupiedBeds 由后端实时统计）
export interface BuildingListItemDto {
  id: number
  code: string
  name: string
  address?: string
  totalFloors: number
  managerId?: number
  dormitoryType?: string
  roomCount: number
  bedCount: number
  occupiedBeds: number
  status: number
  createdTime?: string
}

export interface BuildingDto extends BuildingListItemDto {
  managerName?: string
  remark?: string
  updatedTime?: string
}

export interface CreateBuildingRequest {
  code: string
  name: string
  address?: string
  totalFloors: number
  managerId?: number
  dormitoryType?: string
  remark?: string
}

export interface UpdateBuildingRequest extends CreateBuildingRequest {}

// 房间（occupiedBeds 由后端按床位状态统计）
export interface RoomDto {
  id: number
  buildingId: number
  buildingName?: string
  floor: number
  roomNumber: string
  roomType: string
  bedsCount: number
  occupiedBeds: number
  status: number
  remark?: string
  createdTime?: string
  updatedTime?: string
}

export interface CreateRoomRequest {
  floor: number
  roomNumber: string
  roomType: string
  bedsCount: number
  remark?: string
}

export interface UpdateRoomRequest extends CreateRoomRequest {}

// 床位
export interface BedDto {
  id: number
  roomId: number
  roomNumber?: string
  bedNumber: string
  bedType: string
  status: number
  remark?: string
  createdTime?: string
  updatedTime?: string
}

export interface CreateBedRequest {
  bedNumber: string
  bedType: string
  remark?: string
}

export interface UpdateBedRequest extends CreateBedRequest {
  status?: number
}

// 入住记录（字段对齐后端：仅 checkInDate/checkOutDate，无预计/部门/工号等）
export interface ResidenceDto {
  id: number
  employeeId: number
  employeeName?: string
  buildingId: number
  buildingName?: string
  roomId: number
  roomNumber?: string
  bedId: number
  bedNumber?: string
  checkInDate: string
  checkOutDate?: string
  status: number
  remark?: string
  createdTime?: string
  updatedTime?: string
}

export interface CreateResidenceRequest {
  employeeId: number
  bedId: number
  checkInDate: string
  remark?: string
}

export interface CheckOutRequest {
  checkOutDate: string
  remark?: string
}

export interface UpdateResidenceRequest {
  remark?: string
}

// 费用
export interface ExpenseDto {
  id: number
  roomId: number
  roomNumber?: string
  buildingName?: string
  expenseType: string
  month: string
  amount: number
  shareMethod?: string
  status: number
  remark?: string
  createdTime?: string
  updatedTime?: string
}

export interface CreateExpenseRequest {
  roomId: number
  expenseType: string
  month: string
  amount: number
  shareMethod?: string
  remark?: string
}

export interface UpdateExpenseRequest extends CreateExpenseRequest {
  status?: number
}

// 费用分摊明细
export interface ExpenseShareDto {
  employeeId: number
  employeeName?: string
  amount: number
}

export interface ExpenseAllocationDto {
  expenseId: number
  roomId: number
  roomNumber: string
  shareMethod?: string
  expenseAmount: number
  occupantCount: number
  allocatedTotal: number
  shares: ExpenseShareDto[]
}

// 设施（字段对齐后端：仅 facilityName/quantity/status/remark）
export interface FacilityDto {
  id: number
  roomId: number
  roomNumber?: string
  facilityName: string
  quantity: number
  status: number
  remark?: string
  createdTime?: string
  updatedTime?: string
}

export interface CreateFacilityRequest {
  facilityName: string
  quantity: number
  remark?: string
}

export interface UpdateFacilityRequest extends CreateFacilityRequest {
  status?: number
}

// 报修工单（字段对齐后端：description/priority/result/handledTime）
export interface RepairOrderDto {
  id: number
  roomId: number
  roomNumber?: string
  buildingName?: string
  reporterId: number
  reporterName?: string
  description: string
  priority: number
  status: number
  handlerId?: number
  handlerName?: string
  result?: string
  handledTime?: string
  createdTime?: string
  updatedTime?: string
}

export interface CreateRepairOrderRequest {
  roomId: number
  reporterId: number
  description: string
  priority: number
}

export interface UpdateRepairOrderRequest {
  description: string
  priority: number
}

export interface HandleRepairOrderRequest {
  handlerId: number
  result: string
  status: number
}

// 访客（字段对齐后端：visitReason/visitedPersonId/arrivalTime/departureTime）
export interface VisitorDto {
  id: number
  roomId: number
  roomNumber?: string
  buildingName?: string
  visitorName: string
  visitorPhone?: string
  visitorIdCard?: string
  visitReason?: string
  visitedPersonId?: number
  visitedPersonName?: string
  arrivalTime: string
  departureTime?: string
  status: number
  remark?: string
  createdTime?: string
}

export interface CreateVisitorRequest {
  roomId: number
  visitorName: string
  visitorPhone?: string
  visitorIdCard?: string
  visitReason?: string
  visitedPersonId?: number
  arrivalTime: string
  remark?: string
}

export interface UpdateVisitorRequest {
  visitorName: string
  visitorPhone?: string
  visitorIdCard?: string
  visitReason?: string
  visitedPersonId?: number
  remark?: string
}

// 卫生检查（字段对齐后端：inspectorId/inspectorName）
export interface HygieneCheckDto {
  id: number
  roomId: number
  roomNumber?: string
  buildingName?: string
  checkDate: string
  inspectorId: number
  inspectorName?: string
  score?: number
  result?: string
  remark?: string
  createdTime?: string
}

export interface CreateHygieneCheckRequest {
  roomId: number
  inspectorId: number
  checkDate: string
  score?: number
  result?: string
  remark?: string
}

export interface UpdateHygieneCheckRequest {
  inspectorId: number
  checkDate: string
  score?: number
  result?: string
  remark?: string
}

// 统计
export interface DormitoryStatisticsDto {
  totalBuildings: number
  totalRooms: number
  totalBeds: number
  occupiedBeds: number
  occupancyRate: number
  pendingRepairOrders: number
  todayVisitors: number
  pendingExpenses: number
}

// ==================== 楼栋管理 API ====================

// 楼栋列表（分页）
export function getBuildingList(params: {
  pageIndex: number
  pageSize: number
  keyword?: string
  status?: number
}): Promise<PagedResult<BuildingListItemDto>> {
  return get('/dormitory/buildings', params)
}

// 所有楼栋
export function getAllBuildings(): Promise<BuildingListItemDto[]> {
  return get('/dormitory/buildings/all')
}

// 楼栋详情
export function getBuildingDetail(id: number): Promise<BuildingDto> {
  return get(`/dormitory/buildings/${id}`)
}

// 创建楼栋
export function createBuilding(data: CreateBuildingRequest): Promise<BuildingDto> {
  return post('/dormitory/buildings', data)
}

// 更新楼栋
export function updateBuilding(id: number, data: UpdateBuildingRequest): Promise<BuildingDto> {
  return put(`/dormitory/buildings/${id}`, data)
}

// 删除楼栋
export function deleteBuilding(id: number): Promise<boolean> {
  return del(`/dormitory/buildings/${id}`)
}

// 更新楼栋状态
export function updateBuildingStatus(id: number, status: number): Promise<boolean> {
  return put(`/dormitory/buildings/${id}/status`, { status })
}

// 检查楼栋编码是否重复
export function checkBuildingCode(code: string, excludeId?: number): Promise<boolean> {
  const params: any = { code }
  if (excludeId) params.excludeId = excludeId
  return get('/dormitory/buildings/check-code', params)
}

// ==================== 房间管理 API ====================

// 房间列表（全量，用于下拉框和列表展示）
export function getRoomList(
  buildingId: number,
  params?: { keyword?: string; status?: number }
): Promise<RoomDto[]> {
  return get(`/dormitory/buildings/${buildingId}/rooms/all`, params)
}

// 房间详情
export function getRoomDetail(buildingId: number, id: number): Promise<RoomDto> {
  return get(`/dormitory/buildings/${buildingId}/rooms/${id}`)
}

// 创建房间
export function createRoom(buildingId: number, data: CreateRoomRequest): Promise<RoomDto> {
  return post(`/dormitory/buildings/${buildingId}/rooms`, data)
}

// 更新房间
export function updateRoom(
  buildingId: number,
  id: number,
  data: UpdateRoomRequest
): Promise<RoomDto> {
  return put(`/dormitory/buildings/${buildingId}/rooms/${id}`, data)
}

// 删除房间
export function deleteRoom(buildingId: number, id: number): Promise<boolean> {
  return del(`/dormitory/buildings/${buildingId}/rooms/${id}`)
}

// ==================== 床位管理 API ====================

// 床位列表（全量）
export function getBedList(roomId: number): Promise<BedDto[]> {
  return get(`/dormitory/rooms/${roomId}/beds/all`)
}

// 创建床位
export function createBed(roomId: number, data: CreateBedRequest): Promise<BedDto> {
  return post(`/dormitory/rooms/${roomId}/beds`, data)
}

// 更新床位
export function updateBed(roomId: number, id: number, data: UpdateBedRequest): Promise<BedDto> {
  return put(`/dormitory/rooms/${roomId}/beds/${id}`, data)
}

// 删除床位
export function deleteBed(roomId: number, id: number): Promise<boolean> {
  return del(`/dormitory/rooms/${roomId}/beds/${id}`)
}

// ==================== 入住管理 API ====================

// 入住列表（分页）
export function getResidenceList(params: {
  pageIndex: number
  pageSize: number
  keyword?: string
  employeeId?: number
  buildingId?: number
  status?: number
}): Promise<PagedResult<ResidenceDto>> {
  return get('/dormitory/residences', params)
}

// 创建入住
export function createResidence(data: CreateResidenceRequest): Promise<ResidenceDto> {
  return post('/dormitory/residences', data)
}

// 更新入住
export function updateResidence(id: number, data: UpdateResidenceRequest): Promise<ResidenceDto> {
  return put(`/dormitory/residences/${id}`, data)
}

// 删除入住
export function deleteResidence(id: number): Promise<boolean> {
  return del(`/dormitory/residences/${id}`)
}

// 退宿
export function checkOut(id: number, data: CheckOutRequest): Promise<ResidenceDto> {
  return put(`/dormitory/residences/${id}/checkout`, data)
}

// ==================== 费用管理 API ====================

// 费用列表（分页）
export function getExpenseList(params: {
  pageIndex: number
  pageSize: number
  roomId?: number
  expenseType?: string
  month?: string
  status?: number
}): Promise<PagedResult<ExpenseDto>> {
  return get('/dormitory/expenses', params)
}

// 创建费用
export function createExpense(data: CreateExpenseRequest): Promise<ExpenseDto> {
  return post('/dormitory/expenses', data)
}

// 更新费用
export function updateExpense(id: number, data: UpdateExpenseRequest): Promise<ExpenseDto> {
  return put(`/dormitory/expenses/${id}`, data)
}

// 删除费用
export function deleteExpense(id: number): Promise<boolean> {
  return del(`/dormitory/expenses/${id}`)
}

// 费用分摊明细（按房间当前在住人分摊）
export function getExpenseAllocation(id: number): Promise<ExpenseAllocationDto> {
  return get(`/dormitory/expenses/${id}/allocation`)
}

// ==================== 设施管理 API ====================

// 设施列表
export function getFacilityList(roomId: number): Promise<FacilityDto[]> {
  return get(`/dormitory/rooms/${roomId}/facilities`)
}

// 创建设施
export function createFacility(roomId: number, data: CreateFacilityRequest): Promise<FacilityDto> {
  return post(`/dormitory/rooms/${roomId}/facilities`, data)
}

// 更新设施
export function updateFacility(
  roomId: number,
  id: number,
  data: UpdateFacilityRequest
): Promise<FacilityDto> {
  return put(`/dormitory/rooms/${roomId}/facilities/${id}`, data)
}

// 删除设施
export function deleteFacility(roomId: number, id: number): Promise<boolean> {
  return del(`/dormitory/rooms/${roomId}/facilities/${id}`)
}

// ==================== 报修工单 API ====================

// 报修工单列表（分页）
export function getRepairOrderList(params: {
  pageIndex: number
  pageSize: number
  roomId?: number
  status?: number
  urgency?: number
}): Promise<PagedResult<RepairOrderDto>> {
  return get('/dormitory/repair-orders', params)
}

// 创建报修工单
export function createRepairOrder(data: CreateRepairOrderRequest): Promise<RepairOrderDto> {
  return post('/dormitory/repair-orders', data)
}

// 更新报修工单（描述/紧急程度）
export function updateRepairOrder(id: number, data: UpdateRepairOrderRequest): Promise<RepairOrderDto> {
  return put(`/dormitory/repair-orders/${id}`, data)
}

// 处理报修工单
export function handleRepairOrder(
  id: number,
  data: HandleRepairOrderRequest
): Promise<RepairOrderDto> {
  return put(`/dormitory/repair-orders/${id}/handle`, data)
}

// 删除报修工单
export function deleteRepairOrder(id: number): Promise<boolean> {
  return del(`/dormitory/repair-orders/${id}`)
}

// ==================== 访客登记 API ====================

// 访客列表（分页）
export function getVisitorList(params: {
  pageIndex: number
  pageSize: number
  roomId?: number
  status?: number
}): Promise<PagedResult<VisitorDto>> {
  return get('/dormitory/visitors', params)
}

// 创建访客登记
export function createVisitor(data: CreateVisitorRequest): Promise<VisitorDto> {
  return post('/dormitory/visitors', data)
}

// 更新访客登记
export function updateVisitor(id: number, data: UpdateVisitorRequest): Promise<VisitorDto> {
  return put(`/dormitory/visitors/${id}`, data)
}

// 登记离开
export function visitorLeave(id: number): Promise<boolean> {
  return put(`/dormitory/visitors/${id}/leave`)
}

// 删除访客登记
export function deleteVisitor(id: number): Promise<boolean> {
  return del(`/dormitory/visitors/${id}`)
}

// ==================== 卫生检查 API ====================

// 卫生检查列表（分页）
export function getHygieneCheckList(params: {
  pageIndex: number
  pageSize: number
  roomId?: number
  result?: string
}): Promise<PagedResult<HygieneCheckDto>> {
  return get('/dormitory/hygiene-checks', params)
}

// 创建卫生检查
export function createHygieneCheck(data: CreateHygieneCheckRequest): Promise<HygieneCheckDto> {
  return post('/dormitory/hygiene-checks', data)
}

// 更新卫生检查
export function updateHygieneCheck(id: number, data: UpdateHygieneCheckRequest): Promise<HygieneCheckDto> {
  return put(`/dormitory/hygiene-checks/${id}`, data)
}

// 删除卫生检查
export function deleteHygieneCheck(id: number): Promise<boolean> {
  return del(`/dormitory/hygiene-checks/${id}`)
}

// ==================== 统计 API ====================

// 获取宿舍统计信息
export function getStatistics(): Promise<DormitoryStatisticsDto> {
  return get('/dormitory/statistics')
}
