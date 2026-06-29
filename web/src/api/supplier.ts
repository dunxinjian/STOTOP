// 供应商模块 API
import { get, post, put, del } from './request'

// ==================== 类型定义 ====================

export interface SupplierListItemDto {
  id: number
  code: string
  /** 全称 */
  fullName: string
  /** 简称 */
  shortName?: string
  contact?: string
  phone?: string
  status: number
  createdTime?: string
}

export interface BankAccountDto {
  id: number
  supplierId: number
  accountName: string
  bankName: string
  bankAccountNumber: string
  branchName?: string
  isDefault: boolean
  status: number
  remark?: string
  createdTime?: string
  updatedTime?: string
}

export interface SupplierDto extends SupplierListItemDto {
  uid: string
  creditCode?: string
  taxNumber?: string
  email?: string
  address?: string
  remark?: string
  creatorId?: number
  updatedTime?: string
  bankAccounts: BankAccountDto[]
}

export interface CreateBankAccountRequest {
  accountName: string
  bankName: string
  bankAccountNumber: string
  branchName?: string
  isDefault?: boolean
  remark?: string
}

export interface CreateSupplierRequest {
  code: string
  /** 全称（必填） */
  fullName: string
  /** 简称（可选） */
  shortName?: string
  creditCode?: string
  taxNumber?: string
  contact?: string
  phone?: string
  email?: string
  address?: string
  remark?: string
  bankAccounts?: CreateBankAccountRequest[]
}

export interface UpdateSupplierRequest extends CreateSupplierRequest {}

// ==================== 供应商 API ====================

// 供应商列表（分页）
export function getSupplierList(params: {
  pageIndex: number
  pageSize: number
  keyword?: string
  status?: number | string
}) {
  return get('/supplier/suppliers', params)
}

// 所有启用的供应商
export function getAllSuppliers(): Promise<SupplierListItemDto[]> {
  return get('/supplier/suppliers/all')
}

// 兼容旧调用（AuxiliarySetting.vue 等使用）
export function getAllEnabledSuppliers(): Promise<SupplierListItemDto[]> {
  return getAllSuppliers()
}

// 供应商详情
export function getSupplierDetail(id: number): Promise<SupplierDto> {
  return get(`/supplier/suppliers/${id}`)
}

// 创建供应商
export function createSupplier(data: CreateSupplierRequest): Promise<SupplierDto> {
  return post('/supplier/suppliers', data)
}

// 更新供应商
export function updateSupplier(id: number, data: UpdateSupplierRequest): Promise<SupplierDto> {
  return put(`/supplier/suppliers/${id}`, data)
}

// 删除供应商
export function deleteSupplier(id: number): Promise<boolean> {
  return del(`/supplier/suppliers/${id}`)
}

// 启用/停用供应商
export function updateSupplierStatus(id: number, status: number): Promise<boolean> {
  return put(`/supplier/suppliers/${id}/status`, { status })
}

// 检查供应商编码是否重复
export function checkSupplierCode(code: string, excludeId?: number): Promise<boolean> {
  const params: any = { code }
  if (excludeId) params.excludeId = excludeId
  return get('/supplier/suppliers/check-code', params)
}

// ==================== 收款账户 API ====================

// 获取供应商收款账户列表
export function getBankAccounts(supplierId: number): Promise<BankAccountDto[]> {
  return get(`/supplier/suppliers/${supplierId}/bank-accounts`)
}

// 创建收款账户
export function createBankAccount(supplierId: number, data: CreateBankAccountRequest): Promise<BankAccountDto> {
  return post(`/supplier/suppliers/${supplierId}/bank-accounts`, data)
}

// 更新收款账户
export function updateBankAccount(supplierId: number, id: number, data: CreateBankAccountRequest): Promise<BankAccountDto> {
  return put(`/supplier/suppliers/${supplierId}/bank-accounts/${id}`, data)
}

// 删除收款账户
export function deleteBankAccount(supplierId: number, id: number): Promise<boolean> {
  return del(`/supplier/suppliers/${supplierId}/bank-accounts/${id}`)
}

// 设置默认收款账户
export function setDefaultBankAccount(supplierId: number, id: number): Promise<boolean> {
  return put(`/supplier/suppliers/${supplierId}/bank-accounts/${id}/set-default`)
}
