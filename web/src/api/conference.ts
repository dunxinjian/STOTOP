// 会务管理模块 API
import { get, post, put, del } from './request'

// ==================== 通用类型 ====================

export interface PagedResult<T> {
  items: T[]
  totalCount: number
}

// ==================== 1. Event（活动）类型定义 ====================

export interface EventDto {
  id: number
  name: string
  description?: string
  startDate: string
  endDate: string
  location?: string
  status: string
  manager?: string
  managerPhone?: string
  budget: number
  remark?: string
  creator?: string
  createdTime: string
  updatedTime: string
  type?: string
  groomName?: string
  brideName?: string
}

export interface EventListItemDto {
  id: number
  name: string
  startDate: string
  endDate: string
  location?: string
  status: string
  manager?: string
  budget: number
  createdTime: string
  /** 参会人数 */
  attendeeCount: number
  type?: string
  groomName?: string
  brideName?: string
}

export interface CreateEventRequest {
  name: string
  description?: string
  startDate: string
  endDate: string
  location?: string
  manager?: string
  managerPhone?: string
  budget: number
  remark?: string
  type?: string
  groomName?: string
  brideName?: string
}

export interface UpdateEventRequest {
  name: string
  description?: string
  startDate: string
  endDate: string
  location?: string
  status?: string
  manager?: string
  managerPhone?: string
  budget: number
  remark?: string
  type?: string
  groomName?: string
  brideName?: string
}

export interface EventQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  status?: string
}

export interface DashboardDto {
  totalAttendees: number
  confirmedAttendees: number
  totalSchedules: number
  totalVehicles: number
  totalPickupTasks: number
  pendingPickupTasks: number
  usedVehicles: number
  arrangedPassengers: number
  totalPassengers: number
  totalHotels: number
  totalRooms: number
  assignedRooms: number
  totalMealPlans: number
  totalMaterials: number
  receivedMaterials: number
  totalIncome: number
  budget: number
  alertCount: number
}

export interface AlertItemDto {
  /** 告警级别：Warning/Error */
  level: string
  /** 告警类别：Transport/Accommodation/Meal/Material/Schedule */
  category: string
  title: string
  detail?: string
  relatedEntityId?: number
  relatedEntityType?: string
}

// ==================== 2. Attendee（参会人员）类型定义 ====================

export interface AttendeeDto {
  id: number
  eventId: number
  name: string
  gender?: string
  phone?: string
  organization?: string
  title?: string
  role?: string
  dietPreference?: string
  arrivalMode?: string
  arrivalFlightTrain?: string
  arrivalTime?: string
  arrivalStation?: string
  departureMode?: string
  departureFlightTrain?: string
  departureTime?: string
  departureStation?: string
  needPickup: boolean
  needAccommodation: boolean
  remark?: string
  status: string
  createdTime: string
  updatedTime: string
  primaryGuestId?: number
  relation?: string
  isChild?: boolean
  age?: number
  camp?: string
  guestType?: string
  companionCount?: number
  hasSeat?: boolean
  mealCategory?: string
  checkInStatus?: string
  preferredRoomType?: string
  companions?: CompanionDto[]
}

export interface AttendeeListItemDto {
  id: number
  eventId: number
  name: string
  gender?: string
  phone?: string
  organization?: string
  title?: string
  role?: string
  needPickup: boolean
  needAccommodation: boolean
  status: string
  arrivalTime?: string
  departureTime?: string
  primaryGuestId?: number
  relation?: string
  isChild?: boolean
  age?: number
  camp?: string
  guestType?: string
  companionCount?: number
  hasSeat?: boolean
  mealCategory?: string
  checkInStatus?: string
  preferredRoomType?: string
  companions?: CompanionDto[]
}

export interface CreateAttendeeRequest {
  name: string
  gender?: string
  phone?: string
  organization?: string
  title?: string
  role?: string
  dietPreference?: string
  arrivalMode?: string
  arrivalFlightTrain?: string
  arrivalTime?: string
  arrivalStation?: string
  departureMode?: string
  departureFlightTrain?: string
  departureTime?: string
  departureStation?: string
  needPickup?: boolean
  needAccommodation?: boolean
  remark?: string
  primaryGuestId?: number
  relation?: string
  isChild?: boolean
  age?: number
  camp?: string
  guestType?: string
  checkInStatus?: string
  preferredRoomType?: string
}

export interface UpdateAttendeeRequest {
  name: string
  gender?: string
  phone?: string
  organization?: string
  title?: string
  role?: string
  dietPreference?: string
  arrivalMode?: string
  arrivalFlightTrain?: string
  arrivalTime?: string
  arrivalStation?: string
  departureMode?: string
  departureFlightTrain?: string
  departureTime?: string
  departureStation?: string
  needPickup: boolean
  needAccommodation: boolean
  remark?: string
  status?: string
  primaryGuestId?: number
  relation?: string
  isChild?: boolean
  age?: number
  camp?: string
  guestType?: string
  checkInStatus?: string
  preferredRoomType?: string
}

export interface AttendeeQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  role?: string
  status?: string
  organization?: string
  needPickup?: boolean
  needAccommodation?: boolean
  camp?: string
  checkInStatus?: string
  hasClearTravelDate?: boolean  // true=已明确日期, false=未明确日期
}

export interface ImpactItem {
  id: number
  description: string
  /** 影响类型：Remove/Reassign/Update */
  impactType: string
}

export interface AttendeeImpactAnalysisDto {
  affectedPickupTasks: ImpactItem[]
  affectedRoomAssignments: ImpactItem[]
  affectedMealPlans: ImpactItem[]
  affectedTableSeats: ImpactItem[]
  affectedSchedules: ImpactItem[]
}

// ==================== 3. Schedule（日程）类型定义 ====================

export interface ScheduleItemDto {
  id: number
  scheduleId: number
  itemName: string
  quantity: number
  unit?: string
  status?: string
  remark?: string
}

export interface ScheduleDto {
  id: number
  eventId: number
  date: string
  startTime: string
  endTime: string
  title: string
  location?: string
  type?: string
  description?: string
  sort: number
  createdTime: string
  updatedTime: string
  attendees: AttendeeListItemDto[]
  items: ScheduleItemDto[]
}

export interface ScheduleListItemDto {
  id: number
  eventId: number
  date: string
  startTime: string
  endTime: string
  title: string
  location?: string
  type?: string
  sort: number
  attendeeCount: number
  itemCount: number
}

export interface CreateScheduleRequest {
  date: string
  startTime: string
  endTime: string
  title: string
  location?: string
  type?: string
  description?: string
  sort: number
}

export interface UpdateScheduleRequest {
  date: string
  startTime: string
  endTime: string
  title: string
  location?: string
  type?: string
  description?: string
  sort: number
}

export interface ScheduleAttendeeRequest {
  attendeeIds: number[]
}

export interface ScheduleItemInput {
  itemName: string
  quantity: number
  unit?: string
  remark?: string
}

export interface ScheduleItemRequest {
  items: ScheduleItemInput[]
}

// ==================== 4. Transport（车辆+接送）类型定义 ====================

export interface VehicleDto {
  id: number
  eventId: number
  plateNumber: string
  vehicleType?: string
  seatCount: number
  driverName?: string
  driverPhone?: string
  source?: string
  remark?: string
  createdTime: string
  updatedTime: string
}

export interface VehicleListItemDto {
  id: number
  eventId: number
  plateNumber: string
  vehicleType?: string
  seatCount: number
  driverName?: string
  driverPhone?: string
  source?: string
}

export interface CreateVehicleRequest {
  plateNumber: string
  vehicleType?: string
  seatCount: number
  driverName?: string
  driverPhone?: string
  source?: string
  remark?: string
}

export interface UpdateVehicleRequest {
  plateNumber: string
  vehicleType?: string
  seatCount: number
  driverName?: string
  driverPhone?: string
  source?: string
  remark?: string
}

export interface PickupTaskDto {
  id: number
  eventId: number
  vehicleId?: number
  vehiclePlateNumber?: string
  type?: string
  date: string
  time: string
  origin?: string
  destination?: string
  status: string
  remark?: string
  createdTime: string
  updatedTime: string
  passengers: AttendeeListItemDto[]
}

export interface PickupTaskListItemDto {
  id: number
  eventId: number
  vehicleId?: number
  vehiclePlateNumber?: string
  driverName?: string
  type?: string
  date: string
  time: string
  origin?: string
  destination?: string
  status: string
  passengerCount: number
  passengerNames: string
}

export interface CreatePickupTaskRequest {
  vehicleId?: number
  type?: string
  date: string
  time: string
  origin?: string
  destination?: string
  remark?: string
}

export interface UpdatePickupTaskRequest {
  vehicleId?: number
  type?: string
  date: string
  time: string
  origin?: string
  destination?: string
  status?: string
  remark?: string
}

export interface PickupPassengerRequest {
  attendeeIds: number[]
}

export interface PickupTaskDetailDto {
  id: number
  eventId: number
  vehicleId?: number
  vehiclePlateNumber?: string
  driverName?: string
  type?: string
  date: string
  time: string
  origin?: string
  destination?: string
  status: string
  passengerCount: number
  remark?: string
  passengers: PickupPassengerDto[]
}

export interface PickupPassengerDto {
  attendeeId: number
  name: string
  companionCount: number
}

export interface PickupTaskPreviewItem {
  type?: string
  date: string
  time: string
  origin?: string
  destination?: string
  passengerNames: string[]
  suggestedVehicleId?: number
  suggestedVehiclePlate?: string
}

export interface AutoGeneratePreviewDto {
  tasksToCreate: PickupTaskPreviewItem[]
  skippedAttendees: string[]
  unableToArrange: string[]
}

export interface MergeGroup {
  sourceTaskIds: number[]
  mergedTask: PickupTaskPreviewItem
  reason: string
}

export interface OptimizePreviewDto {
  beforeCount: number
  afterCount: number
  mergeGroups: MergeGroup[]
}

// ==================== 5. Accommodation（住宿）类型定义 ====================

export interface RoomDto {
  id: number
  hotelId: number
  roomNumber?: string
  roomType?: string
  checkInDate: string
  checkOutDate: string
  status: string
  remark?: string
  updatedTime: string
  guests: AttendeeListItemDto[]
}

export interface RoomListItemDto {
  id: number
  hotelId: number
  roomNumber?: string
  roomType?: string
  checkInDate: string
  checkOutDate: string
  status: string
  guestCount: number
  guestNames?: string
}

export interface HotelDto {
  id: number
  eventId: number
  hotelName: string
  address?: string
  contact?: string
  contactPhone?: string
  agreedPrice?: string
  remark?: string
  createdTime: string
  updatedTime: string
  rooms: RoomDto[]
}

export interface HotelListItemDto {
  id: number
  eventId: number
  hotelName: string
  address?: string
  contact?: string
  contactPhone?: string
  agreedPrice?: string
  totalRooms: number
  assignedRooms: number
}

export interface CreateHotelRequest {
  hotelName: string
  address?: string
  contact?: string
  contactPhone?: string
  agreedPrice?: string
  remark?: string
}

export interface UpdateHotelRequest {
  hotelName: string
  address?: string
  contact?: string
  contactPhone?: string
  agreedPrice?: string
  remark?: string
}

export interface RoomInput {
  roomNumber?: string
  roomType?: string
  checkInDate: string
  checkOutDate: string
  remark?: string
}

export interface BatchAddRoomRequest {
  rooms: RoomInput[]
}

export interface UpdateRoomRequest {
  roomNumber?: string
  roomType?: string
  checkInDate: string
  checkOutDate: string
  remark?: string
}

export interface RoomAssignRequest {
  attendeeIds: number[]
}

export interface AssignedGuestItem {
  attendeeId: number
  name: string
  gender?: string
  organization?: string
  role?: string
}

export interface RoomAssignmentPreviewItem {
  roomId: number
  roomNumber?: string
  roomType?: string
  hotelName?: string
  guests: AssignedGuestItem[]
}

export interface UnassignedAttendeeItem {
  attendeeId: number
  name: string
  reason: string
}

export interface AutoAssignPreviewDto {
  assignments: RoomAssignmentPreviewItem[]
  unassignedAttendees: UnassignedAttendeeItem[]
  satisfactionRate: number
}

// ==================== 6. Meal（餐食）类型定义 ====================

export interface MealAttendeeDto {
  attendeeId: number
  name: string
  organization?: string
  dietPreference?: string
  dietNote?: string
}

export interface MealPlanDto {
  id: number
  eventId: number
  date: string
  mealType: string
  diningMode?: string
  location?: string
  expectedCount: number
  actualCount: number
  remark?: string
  createdTime: string
  updatedTime: string
  attendees: MealAttendeeDto[]
}

export interface MealPlanListItemDto {
  id: number
  eventId: number
  date: string
  mealType: string
  diningMode?: string
  location?: string
  expectedCount: number
  actualCount: number
  tableCount: number
  attendees?: MealAttendeeDto[]
}

export interface CreateMealPlanRequest {
  date: string
  mealType: string
  diningMode?: string
  location?: string
  expectedCount: number
  remark?: string
}

export interface UpdateMealPlanRequest {
  date: string
  mealType: string
  diningMode?: string
  location?: string
  expectedCount: number
  remark?: string
}

export interface MealAttendeeInput {
  attendeeId: number
  dietNote?: string
}

export interface MealAttendeeRequest {
  attendees: MealAttendeeInput[]
}

// ==================== 7. Table（桌次）类型定义 ====================

export interface TableSeatDto {
  id: number
  tableId: number
  attendeeId: number
  attendeeName?: string
  organization?: string
  role?: string
  seatNumber: number
  remark?: string
}

export interface TableDto {
  id: number
  mealPlanId: number
  tableNumber: number
  tableName?: string
  seatCount: number
  remark?: string
  updatedTime: string
  seats: TableSeatDto[]
}

export interface TableListItemDto {
  id: number
  mealPlanId: number
  tableNumber: number
  tableName?: string
  seatCount: number
  occupiedSeats: number
}

export interface CreateTableRequest {
  tableNumber: number
  tableName?: string
  seatCount: number
  remark?: string
}

export interface UpdateTableRequest {
  tableNumber: number
  tableName?: string
  seatCount: number
  remark?: string
}

export interface SeatInput {
  attendeeId: number
  seatNumber: number
  remark?: string
}

export interface TableSeatRequest {
  seats: SeatInput[]
}

export interface AutoArrangeConfigRequest {
  seatsPerTable?: number
  groupByOrganization?: boolean
  groupByDiet?: boolean
  enableMainTable?: boolean
}

export interface TableArrangePreviewItem {
  tableNumber: number
  tableName?: string
  seatCount: number
  guests: AssignedGuestItem[]
}

export interface AutoArrangePreviewDto {
  tables: TableArrangePreviewItem[]
  unseatedAttendees: string[]
  totalTables: number
  totalPersons: number
}

// ==================== 8. Finance（收入）类型定义 ====================

export interface IncomeDto {
  id: number
  eventId: number
  attendeeId?: number
  attendeeName?: string
  type?: string
  amount: number
  paymentMethod?: string
  payerName?: string
  payerOrganization?: string
  paymentDate: string
  receiptNumber?: string
  remark?: string
  registrant?: string
  createdTime: string
  updatedTime: string
}

export interface IncomeListItemDto {
  id: number
  eventId: number
  attendeeId?: number
  attendeeName?: string
  type?: string
  amount: number
  paymentMethod?: string
  payerName?: string
  paymentDate: string
  receiptNumber?: string
  registrant?: string
}

export interface CreateIncomeRequest {
  attendeeId?: number
  type?: string
  amount: number
  paymentMethod?: string
  payerName?: string
  payerOrganization?: string
  paymentDate: string
  receiptNumber?: string
  remark?: string
}

export interface UpdateIncomeRequest {
  attendeeId?: number
  type?: string
  amount: number
  paymentMethod?: string
  payerName?: string
  payerOrganization?: string
  paymentDate: string
  receiptNumber?: string
  remark?: string
}

export interface IncomeQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  type?: string
  paymentMethod?: string
  startDate?: string
  endDate?: string
}

export interface BatchRegisterIncomeRequest {
  attendeeIds: number[]
  type: string
  amount: number
  paymentMethod?: string
  paymentDate: string
  remark?: string
}

export interface IncomeTypeSummary {
  type: string
  amount: number
  count: number
}

export interface IncomeSummaryDto {
  totalAmount: number
  totalCount: number
  typeSummaries: IncomeTypeSummary[]
}

// ==================== 9. Material（物品）类型定义 ====================

export interface MaterialDto {
  id: number
  eventId: number
  name: string
  category?: string
  specification?: string
  requiredQuantity: number
  receivedQuantity: number
  unit?: string
  acquisitionMethod?: string
  unitPrice: number
  totalPrice: number
  supplier?: string
  supplierContact?: string
  requiredDate?: string
  receivedDate?: string
  returnDate?: string
  status: string
  responsible?: string
  scheduleId?: number
  scheduleTitle?: string
  remark?: string
  createdTime: string
  updatedTime: string
}

export interface MaterialListItemDto {
  id: number
  eventId: number
  name: string
  category?: string
  requiredQuantity: number
  receivedQuantity: number
  unit?: string
  acquisitionMethod?: string
  totalPrice: number
  status: string
  responsible?: string
  requiredDate?: string
}

export interface CreateMaterialRequest {
  name: string
  category?: string
  specification?: string
  requiredQuantity: number
  unit?: string
  acquisitionMethod?: string
  unitPrice: number
  supplier?: string
  supplierContact?: string
  requiredDate?: string
  responsible?: string
  scheduleId?: number
  remark?: string
}

export interface UpdateMaterialRequest {
  name: string
  category?: string
  specification?: string
  requiredQuantity: number
  unit?: string
  acquisitionMethod?: string
  unitPrice: number
  supplier?: string
  supplierContact?: string
  requiredDate?: string
  status?: string
  responsible?: string
  scheduleId?: number
  remark?: string
}

export interface MaterialQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  category?: string
  status?: string
  acquisitionMethod?: string
}

export interface MaterialReceiveRequest {
  receivedQuantity: number
  receivedDate: string
}

export interface MaterialReturnRequest {
  returnDate: string
  remark?: string
}

export interface MaterialCategorySummary {
  category: string
  count: number
  totalCost: number
  receivedCount: number
}

export interface MaterialSummaryDto {
  totalCount: number
  receivedCount: number
  pendingCount: number
  totalCost: number
  categorySummaries: MaterialCategorySummary[]
}

// ==================== Gift 礼金 ====================
export interface GiftDto {
  id: number
  eventId: number
  attendeeId?: number
  attendeeName?: string
  guestName?: string
  camp?: string
  guestType?: string
  amount: number
  giftDescription?: string
  registrationTime: string
  registrationMethod: string
  isReturned: boolean
  returnContent?: string
  returnTime?: string
  remark?: string
}

export interface CreateGiftRequest {
  attendeeId?: number
  guestName?: string
  amount: number
  giftDescription?: string
  registrationMethod?: string
  remark?: string
}

export interface UpdateGiftRequest {
  amount?: number
  giftDescription?: string
  registrationMethod?: string
  isReturned?: boolean
  returnContent?: string
  remark?: string
}

export interface BatchRegisterGiftRequest {
  items: CreateGiftRequest[]
}

export interface GiftCampSummary {
  camp: string
  count: number
  totalAmount: number
}

export interface GiftSummaryDto {
  totalAmount: number
  totalCount: number
  cashCount: number
  transferCount: number
  giftCount: number
  returnedCount: number
  pendingReturnCount: number
  campSummaries: GiftCampSummary[]
}

// ==================== Ceremony 仪式流程 ====================
export interface CeremonyItemDto {
  id: number
  eventId: number
  name: string
  startTime: string
  duration: number
  responsible?: string
  music?: string
  lighting?: string
  props?: string
  remark?: string
  sort: number
  phase: string
}

export interface CreateCeremonyItemRequest {
  name: string
  startTime: string
  duration?: number
  responsible?: string
  music?: string
  lighting?: string
  props?: string
  remark?: string
  sort?: number
  phase?: string
}

export interface UpdateCeremonyItemRequest {
  name?: string
  startTime?: string
  duration?: number
  responsible?: string
  music?: string
  lighting?: string
  props?: string
  remark?: string
  sort?: number
  phase?: string
}

export interface ReorderCeremonyRequest {
  itemIds: number[]
}

// ==================== Driver Notification 司机通知 ====================
export interface DriverNotificationDto {
  driverVehicleId: number
  driverName: string
  plateNumber: string
  driverPhone: string
  taskCount: number
  passengerCount: number
  message: string
}

// ==================== Companion 随行人员 ====================
export interface CompanionDto {
  id: number
  name: string
  relation?: string
  isChild: boolean
  age?: number
  hasSeat: boolean
  mealCategory: string
}

// ==================== 10. VehicleSchedule（车辆日程）类型定义 ====================

export interface VehicleScheduleDto {
  id: number
  eventId: number
  vehicleId: number
  vehiclePlateNumber?: string
  driverName?: string
  driverPhone?: string
  date: string
  startTime: string
  endTime: string
  taskType?: string
  pickupTaskId?: number
  origin?: string
  destination?: string
  passengerCount: number
  remark?: string
  createdTime: string
  updatedTime: string
}

export interface VehicleScheduleListItemDto {
  id: number
  vehicleId: number
  vehiclePlateNumber?: string
  driverName?: string
  date: string
  startTime: string
  endTime: string
  taskType?: string
  origin?: string
  destination?: string
  passengerCount: number
}

export interface AddVehicleTaskRequest {
  vehicleId: number
  date: string
  startTime: string
  endTime: string
  taskType: string
  origin?: string
  destination?: string
  passengerCount: number
  remark?: string
}

export interface VehicleSchedulePreviewItem {
  vehicleId: number
  vehiclePlateNumber?: string
  date: string
  startTime: string
  endTime: string
  taskType?: string
  origin?: string
  destination?: string
  passengerCount: number
}

export interface VehicleScheduleGeneratePreviewDto {
  scheduleItems: VehicleSchedulePreviewItem[]
  conflicts: string[]
}

export interface DriverTaskItem {
  startTime: string
  endTime: string
  taskType?: string
  origin?: string
  destination?: string
  passengerNames?: string
  remark?: string
}

export interface DriverCardDto {
  vehicleId: number
  plateNumber: string
  vehicleType?: string
  driverName?: string
  driverPhone?: string
  date: string
  tasks: DriverTaskItem[]
  totalWorkMinutes: number
  totalTrips: number
}

// ==================== 1. Event（活动）API ====================

export function getEvents(params: EventQueryRequest) {
  return get('/conference/events', params)
}

export function getEvent(id: number) {
  return get(`/conference/events/${id}`)
}

export function createEvent(data: CreateEventRequest) {
  return post('/conference/events', data)
}

export function updateEvent(id: number, data: UpdateEventRequest) {
  return put(`/conference/events/${id}`, data)
}

export function deleteEvent(id: number) {
  return del(`/conference/events/${id}`)
}

export function getEventDashboard(eventId: number) {
  return get(`/conference/events/${eventId}/dashboard`)
}

export function getEventAlerts(eventId: number) {
  return get(`/conference/events/${eventId}/alerts`)
}

// ==================== 2. Attendee（参会人员）API ====================

export function getAttendees(eventId: number, params: AttendeeQueryRequest) {
  return get(`/conference/events/${eventId}/attendees`, params)
}

export function getAttendee(id: number) {
  return get(`/conference/attendees/${id}`)
}

export function createAttendee(eventId: number, data: CreateAttendeeRequest) {
  return post(`/conference/events/${eventId}/attendees`, data)
}

export function updateAttendee(id: number, data: UpdateAttendeeRequest) {
  return put(`/conference/attendees/${id}`, data)
}

export function deleteAttendee(id: number) {
  return del(`/conference/attendees/${id}`)
}

export function getAttendeeImpactAnalysis(id: number) {
  return post(`/conference/attendees/${id}/impact-analysis`)
}

export function applyAttendeeChanges(id: number) {
  return post(`/conference/attendees/${id}/apply-changes`)
}

export function importAttendees(eventId: number, file: File) {
  const formData = new FormData()
  formData.append('file', file)
  return post(`/conference/events/${eventId}/attendees/import`, formData as any)
}

export function exportAttendees(eventId: number) {
  return get<Blob>(`/conference/events/${eventId}/attendees/export`, undefined, { responseType: 'blob' })
}

export function downloadAttendeeTemplate() {
  return get<Blob>(`/conference/attendees/import-template`, undefined, { responseType: 'blob' })
}

export function batchUpdateAttendeeStatus(data: { attendeeIds: number[], checkInStatus?: string, status?: string }) {
  return put('/conference/attendees/batch-status', data)
}

// ==================== 3. Schedule（日程）API ====================

export function getSchedules(eventId: number) {
  return get(`/conference/events/${eventId}/schedules`)
}

export function createSchedule(eventId: number, data: CreateScheduleRequest) {
  return post(`/conference/events/${eventId}/schedules`, data)
}

export function updateSchedule(id: number, data: UpdateScheduleRequest) {
  return put(`/conference/schedules/${id}`, data)
}

export function deleteSchedule(id: number) {
  return del(`/conference/schedules/${id}`)
}

export function setScheduleAttendees(id: number, data: ScheduleAttendeeRequest) {
  return put(`/conference/schedules/${id}/attendees`, data)
}

export function setScheduleItems(id: number, data: ScheduleItemRequest) {
  return put(`/conference/schedules/${id}/items`, data)
}

// ==================== 4. Transport（车辆+接送）API ====================

// --- Vehicle ---

export function getVehicles(eventId: number) {
  return get(`/conference/events/${eventId}/vehicles`)
}

export function createVehicle(eventId: number, data: CreateVehicleRequest) {
  return post(`/conference/events/${eventId}/vehicles`, data)
}

export function updateVehicle(id: number, data: UpdateVehicleRequest) {
  return put(`/conference/vehicles/${id}`, data)
}

export function deleteVehicle(id: number) {
  return del(`/conference/vehicles/${id}`)
}

// --- PickupTask ---

export function getPickupTasks(eventId: number) {
  return get(`/conference/events/${eventId}/pickups`)
}

export function createPickupTask(eventId: number, data: CreatePickupTaskRequest) {
  return post(`/conference/events/${eventId}/pickups`, data)
}

export function updatePickupTask(id: number, data: UpdatePickupTaskRequest) {
  return put(`/conference/pickups/${id}`, data)
}

export function deletePickupTask(id: number) {
  return del(`/conference/pickups/${id}`)
}

export function getPickupTaskDetail(taskId: number) {
  return get(`/conference/pickups/${taskId}`)
}

export function setPickupPassengers(id: number, data: PickupPassengerRequest) {
  return put(`/conference/pickups/${id}/passengers`, data)
}

// --- Smart Algorithms ---

export function autoGeneratePickups(eventId: number) {
  return post(`/conference/events/${eventId}/pickups/auto-generate`)
}

export function commitAutoGeneratePickups(eventId: number, tasks: any[]) {
  return post(`/conference/events/${eventId}/pickups/auto-generate/commit`, { tasks })
}

export function optimizePickups(eventId: number) {
  return post(`/conference/events/${eventId}/pickups/optimize`)
}

// --- Export ---

export function exportPickupsPdf(eventId: number) {
  return get<Blob>(`/conference/events/${eventId}/pickups/export-pdf`, undefined, { responseType: 'blob' })
}

export function exportPickupsImage(eventId: number) {
  return get<Blob>(`/conference/events/${eventId}/pickups/export-image`, undefined, { responseType: 'blob' })
}

// ==================== 5. Accommodation（住宿）API ====================

// --- Hotel ---

export function getHotels(eventId: number) {
  return get(`/conference/events/${eventId}/hotels`)
}

export function createHotel(eventId: number, data: CreateHotelRequest) {
  return post(`/conference/events/${eventId}/hotels`, data)
}

export function updateHotel(id: number, data: UpdateHotelRequest) {
  return put(`/conference/hotels/${id}`, data)
}

export function deleteHotel(id: number) {
  return del(`/conference/hotels/${id}`)
}

// --- Room ---

export function getRooms(hotelId: number) {
  return get(`/conference/hotels/${hotelId}/rooms`)
}

export function batchAddRooms(hotelId: number, data: BatchAddRoomRequest) {
  return post(`/conference/hotels/${hotelId}/rooms/batch`, data)
}

export function updateRoom(id: number, data: UpdateRoomRequest) {
  return put(`/conference/rooms/${id}`, data)
}

export function assignRoom(id: number, data: RoomAssignRequest) {
  return put(`/conference/rooms/${id}/assign`, data)
}

// --- Smart Algorithms ---

export function autoAssignAccommodation(eventId: number) {
  return post(`/conference/events/${eventId}/accommodation/auto-assign`)
}

export function getUnassignedAttendees(eventId: number) {
  return get(`/conference/events/${eventId}/accommodation/unassigned`)
}

// --- Demand Stats ---

export interface RoomTypeStat {
  demand: number
  allocated: number
  available: number
}

export interface DailyDemandItem {
  date: string
  roomTypes: Record<string, RoomTypeStat>
  totalDemand: number
}

export interface AccommodationDemandStatsDto {
  dailyStats: DailyDemandItem[]
  totalByRoomType: Record<string, number>
  totalNeedAccommodation: number
}

export function getAccommodationDemandStats(eventId: number) {
  return get<AccommodationDemandStatsDto>(`/conference/events/${eventId}/accommodation/demand-stats`)
}

// --- Room Type Guests ---

export interface RoomTypeGuestDto {
  attendeeId: number
  name: string
  gender: string
  organization: string
  phone: string
  preferredRoomType: string
  roomNumber: string
  hotelName: string
  primaryGuestName?: string
  relation?: string
}

export function getRoomTypeGuests(eventId: number, date: string, roomType: string) {
  return get<RoomTypeGuestDto[]>(`/conference/events/${eventId}/accommodation/room-type-guests`, { date, roomType })
}

export function updateAttendeeRoomPreference(attendeeId: number, preferredRoomType: string) {
  return put(`/conference/attendees/${attendeeId}/room-preference`, { preferredRoomType })
}

// --- Export ---

export function exportAccommodationPdf(eventId: number) {
  return get<Blob>(`/conference/events/${eventId}/accommodation/export-pdf`, undefined, { responseType: 'blob' })
}

export function exportAccommodationDemandStatsExcel(eventId: number) {
  return get<Blob>(`/conference/events/${eventId}/accommodation/demand-stats/export-excel`, undefined, { responseType: 'blob' })
}

// ==================== 6. Meal（餐食）API ====================

export function getMealPlans(eventId: number) {
  return get(`/conference/events/${eventId}/meals`)
}

export function createMealPlan(eventId: number, data: CreateMealPlanRequest) {
  return post(`/conference/events/${eventId}/meals`, data)
}

export function updateMealPlan(id: number, data: UpdateMealPlanRequest) {
  return put(`/conference/meals/${id}`, data)
}

export function deleteMealPlan(id: number) {
  return del(`/conference/meals/${id}`)
}

export function setMealAttendees(id: number, data: MealAttendeeRequest) {
  return put(`/conference/meals/${id}/attendees`, data)
}

export function autoGenerateMealPlans(eventId: number) {
  return post(`/conference/events/${eventId}/meals/auto-generate`)
}

// ==================== 7. Table（桌次）API ====================

export function getTables(mealId: number) {
  return get(`/conference/meals/${mealId}/tables`)
}

export function createTable(mealId: number, data: CreateTableRequest) {
  return post(`/conference/meals/${mealId}/tables`, data)
}

export function updateTable(id: number, data: UpdateTableRequest) {
  return put(`/conference/tables/${id}`, data)
}

export function deleteTable(id: number) {
  return del(`/conference/tables/${id}`)
}

export function setTableSeats(id: number, data: TableSeatRequest) {
  return put(`/conference/tables/${id}/seats`, data)
}

export function autoArrangeTables(mealId: number, data: AutoArrangeConfigRequest) {
  return post(`/conference/meals/${mealId}/tables/auto-arrange`, data)
}

export function exportTablesImage(mealId: number) {
  return get<Blob>(`/conference/meals/${mealId}/tables/export-image`, undefined, { responseType: 'blob' })
}

export function exportTablesPdf(mealId: number) {
  return get<Blob>(`/conference/meals/${mealId}/tables/export-pdf`, undefined, { responseType: 'blob' })
}

export function exportTablesExcel(mealId: number) {
  return get<Blob>(`/conference/meals/${mealId}/tables/export-excel`, undefined, { responseType: 'blob' })
}

// ==================== 8. Finance（收入）API ====================

export function getIncomes(eventId: number, params: IncomeQueryRequest) {
  return get(`/conference/events/${eventId}/incomes`, params)
}

export function getIncome(id: number) {
  return get(`/conference/incomes/${id}`)
}

export function createIncome(eventId: number, data: CreateIncomeRequest) {
  return post(`/conference/events/${eventId}/incomes`, data)
}

export function updateIncome(id: number, data: UpdateIncomeRequest) {
  return put(`/conference/incomes/${id}`, data)
}

export function deleteIncome(id: number) {
  return del(`/conference/incomes/${id}`)
}

export function getIncomeSummary(eventId: number) {
  return get(`/conference/events/${eventId}/incomes/summary`)
}

export function exportIncomes(eventId: number) {
  return get<Blob>(`/conference/events/${eventId}/incomes/export`, undefined, { responseType: 'blob' })
}

export function batchRegisterIncomes(eventId: number, data: BatchRegisterIncomeRequest) {
  return post(`/conference/events/${eventId}/incomes/batch-register`, data)
}

// ==================== 9. Material（物品）API ====================

export function getMaterials(eventId: number, params: MaterialQueryRequest) {
  return get(`/conference/events/${eventId}/materials`, params)
}

export function getMaterial(id: number) {
  return get(`/conference/materials/${id}`)
}

export function createMaterial(eventId: number, data: CreateMaterialRequest) {
  return post(`/conference/events/${eventId}/materials`, data)
}

export function updateMaterial(id: number, data: UpdateMaterialRequest) {
  return put(`/conference/materials/${id}`, data)
}

export function deleteMaterial(id: number) {
  return del(`/conference/materials/${id}`)
}

export function receiveMaterial(id: number, data: MaterialReceiveRequest) {
  return put(`/conference/materials/${id}/receive`, data)
}

export function returnMaterial(id: number, data: MaterialReturnRequest) {
  return put(`/conference/materials/${id}/return`, data)
}

export function getMaterialSummary(eventId: number) {
  return get(`/conference/events/${eventId}/materials/summary`)
}

export function exportMaterials(eventId: number) {
  return get<Blob>(`/conference/events/${eventId}/materials/export`, undefined, { responseType: 'blob' })
}

export function getMaterialChecklist(eventId: number) {
  return get(`/conference/events/${eventId}/materials/checklist`)
}

// ==================== 10. VehicleSchedule（车辆日程）API ====================

export function getVehicleSchedules(eventId: number) {
  return get(`/conference/events/${eventId}/vehicle-schedules`)
}

export function getVehicleScheduleByVehicle(eventId: number, vehicleId: number) {
  return get(`/conference/events/${eventId}/vehicle-schedules/by-vehicle/${vehicleId}`)
}

export function getVehicleScheduleByDate(eventId: number, date: string) {
  return get(`/conference/events/${eventId}/vehicle-schedules/by-date/${date}`)
}

export function generateVehicleSchedules(eventId: number) {
  return post(`/conference/events/${eventId}/vehicle-schedules/generate`)
}

export function addVehicleTask(eventId: number, data: AddVehicleTaskRequest) {
  return post(`/conference/events/${eventId}/vehicle-schedules/add-task`, data)
}

export function updateVehicleSchedule(id: number, data: AddVehicleTaskRequest) {
  return put(`/conference/vehicle-schedules/${id}`, data)
}

export function deleteVehicleSchedule(id: number) {
  return del(`/conference/vehicle-schedules/${id}`)
}

export function exportVehicleSchedulePdf(eventId: number) {
  return get<Blob>(`/conference/events/${eventId}/vehicle-schedules/export-pdf`, undefined, { responseType: 'blob' })
}

export function getDriverCards(eventId: number) {
  return get(`/conference/events/${eventId}/vehicle-schedules/driver-cards`)
}

// ==================== Gift API ====================
export function getGifts(eventId: number) {
  return get(`/conference/events/${eventId}/gifts`)
}

export function getGiftById(id: number) {
  return get(`/conference/gifts/${id}`)
}

export function createGift(eventId: number, data: CreateGiftRequest) {
  return post(`/conference/events/${eventId}/gifts`, data)
}

export function updateGift(id: number, data: UpdateGiftRequest) {
  return put(`/conference/gifts/${id}`, data)
}

export function deleteGift(id: number) {
  return del(`/conference/gifts/${id}`)
}

export function getGiftSummary(eventId: number) {
  return get(`/conference/events/${eventId}/gifts/summary`)
}

export function batchRegisterGifts(eventId: number, data: BatchRegisterGiftRequest) {
  return post(`/conference/events/${eventId}/gifts/batch`, data)
}

export function exportGifts(eventId: number) {
  return get<Blob>(`/conference/events/${eventId}/gifts/export`, undefined, { responseType: 'blob' })
}

// ==================== Ceremony API ====================
export function getCeremonyItems(eventId: number) {
  return get(`/conference/events/${eventId}/ceremony`)
}

export function getCeremonyItemById(id: number) {
  return get(`/conference/ceremony/${id}`)
}

export function createCeremonyItem(eventId: number, data: CreateCeremonyItemRequest) {
  return post(`/conference/events/${eventId}/ceremony`, data)
}

export function updateCeremonyItem(id: number, data: UpdateCeremonyItemRequest) {
  return put(`/conference/ceremony/${id}`, data)
}

export function deleteCeremonyItem(id: number) {
  return del(`/conference/ceremony/${id}`)
}

export function reorderCeremony(eventId: number, data: ReorderCeremonyRequest) {
  return put(`/conference/events/${eventId}/ceremony/reorder`, data)
}

export function exportRundown(eventId: number) {
  return get(`/conference/events/${eventId}/ceremony/export-rundown`)
}

// ==================== Driver Notification API ====================
export function getDriverNotifications(eventId: number, date: string) {
  return get(`/conference/events/${eventId}/vehicle-schedules/driver-notifications`, { date })
}

// ==================== Companion API ====================
export function createCompanion(eventId: number, primaryGuestId: number, data: Partial<CreateAttendeeRequest>) {
  return post(`/conference/events/${eventId}/attendees/${primaryGuestId}/companions`, data)
}

export function getCompanions(attendeeId: number) {
  return get(`/conference/attendees/${attendeeId}/companions`)
}

export function updateCompanion(companionId: number, data: Partial<CreateAttendeeRequest>) {
  return put(`/conference/attendees/${companionId}`, data)
}

export function deleteCompanion(companionId: number) {
  return del(`/conference/attendees/${companionId}`)
}
