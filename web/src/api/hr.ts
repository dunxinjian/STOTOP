// 人资模块 API
import { get, post, put, del } from './request'

// ==================== 员工花名册 ====================

// 员工 DTO 类型定义
export interface EmployeeDto {
  id: number
  fuid: string
  userId: number
  userName: string | null
  userAccount: string | null
  name: string
  gender: string | null
  birthDate: string | null
  idCardNumber: string | null
  phone: string | null
  ethnicity: string | null
  education: string | null
  maritalStatus: string | null
  homeAddress: string | null
  householdAddress: string | null
  emergencyContact: string | null
  emergencyContactPhone: string | null
  emergencyContactRelation: string | null
  entryDate: string | null
  regularDate: string | null
  leaveDate: string | null
  employeeStatus: number
  remark: string | null
  createTime: string
  updateTime: string
}

// 分页查询员工列表
export function getEmployeeList(params?: {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  employeeStatus?: number | null
}) {
  return get('/hr/employees', params)
}

// 获取员工详情（含收款账号）
export function getEmployeeDetail(id: number) {
  return get(`/hr/employees/${id}`)
}

// 新增员工
export function createEmployee(data: object) {
  return post('/hr/employees', data)
}

// 更新员工
export function updateEmployee(id: number, data: object) {
  return put(`/hr/employees/${id}`, data)
}

// 删除员工
export function deleteEmployee(id: number) {
  return del(`/hr/employees/${id}`)
}

// 根据用户ID获取员工信息
export function getEmployeeByUserId(userId: number) {
  return get(`/hr/employees/by-user/${userId}`)
}

// 搜索可关联的用户（未被其他员工关联的）
export function searchAvailableUsers(keyword?: string) {
  return get('/hr/employees/search-users', { keyword })
}

// 获取所有在职员工（用于辅助核算等只读选择）
export function getAllEnabledEmployees() {
  return get('/hr/employees/all-enabled')
}
