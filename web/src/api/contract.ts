// 合同模块 API
import { get, post, put, del } from './request'

// ==================== 类型定义 ====================

// --- 合同类型 ---

export interface ContractTypeDto {
  id: number
  name: string
  code: string
  description?: string
  sortOrder: number
  status: number
  creatorName?: string
  createdTime: string
  updaterName?: string
  updatedTime?: string
}

export interface CreateContractTypeRequest {
  name: string
  code: string
  description?: string
  sortOrder: number
}

export interface UpdateContractTypeRequest extends CreateContractTypeRequest {}

export interface ContractTypeQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  status?: number
}

// --- 合同模板 ---

export interface ContractTemplateListItemDto {
  id: number
  typeId: number
  typeName?: string
  templateName: string
  version: number
  status: number
  createdTime: string
}

export interface ContractTemplateDto extends ContractTemplateListItemDto {
  templateContent?: string
  creatorName?: string
  updaterName?: string
  updatedTime?: string
}

export interface CreateContractTemplateRequest {
  typeId: number
  templateName: string
  templateContent?: string
}

export interface UpdateContractTemplateRequest {
  templateName: string
  templateContent?: string
}

export interface ContractTemplateQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  typeId?: number
  status?: number
}

// --- 合同 ---

export interface ContractPartyDto {
  id: number
  contractId: number
  partyRole: number
  relatedBusinessType?: string
  relatedBusinessId?: number
  partyName: string
  contact?: string
  phone?: string
  address?: string
}

export interface ContractClauseDto {
  id: number
  contractId: number
  clauseOrder: number
  clauseTitle: string
  clauseContent?: string
  isKeyClause: boolean
}

export interface ContractListItemDto {
  id: number
  contractNo: string
  title: string
  typeId: number
  typeName?: string
  amount?: number
  startDate?: string
  endDate?: string
  contractNature: number
  status: number
  creatorName?: string
  createdTime: string
}

export interface ContractStatusGroupDto {
  status: number
  statusName: string
  count: number
}

export interface ContractStatisticsDto {
  totalCount: number
  byStatus: ContractStatusGroupDto[]
}

export interface ContractDto extends ContractListItemDto {
  templateId?: number
  templateName?: string
  relatedContractId?: number
  relatedContractNo?: string
  oaProcessInstanceId?: number
  updaterName?: string
  updatedTime?: string
  parties: ContractPartyDto[]
  clauses: ContractClauseDto[]
  reminders: ContractReminderDto[]
  eSignRecords: ESignRecordDto[]
}

export interface CreateContractPartyRequest {
  partyRole: number
  relatedBusinessType?: string
  relatedBusinessId?: number
  partyName: string
  contact?: string
  phone?: string
  address?: string
}

export interface CreateContractClauseRequest {
  clauseOrder: number
  clauseTitle: string
  clauseContent?: string
  isKeyClause: boolean
}

export interface CreateContractRequest {
  contractNo: string
  title: string
  typeId: number
  templateId?: number
  amount?: number
  startDate?: string
  endDate?: string
  relatedContractId?: number
  contractNature?: number
  parties?: CreateContractPartyRequest[]
  clauses?: CreateContractClauseRequest[]
}

export interface UpdateContractRequest {
  title: string
  typeId: number
  amount?: number
  startDate?: string
  endDate?: string
  parties?: CreateContractPartyRequest[]
  clauses?: CreateContractClauseRequest[]
}

export interface ContractQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  typeId?: number
  status?: number
  contractNature?: number
}

// --- 合同提醒 ---

export interface ContractReminderDto {
  id: number
  contractId: number
  contractNo?: string
  contractTitle?: string
  reminderType: number
  reminderDate: string
  recipientId: number
  isHandled: boolean
  remark?: string
  creatorName?: string
  createdTime: string
}

export interface CreateContractReminderRequest {
  contractId: number
  reminderType: number
  reminderDate: string
  recipientId: number
  remark?: string
}

export interface UpdateContractReminderRequest {
  reminderType: number
  reminderDate: string
  recipientId: number
  remark?: string
}

export interface ContractReminderQueryRequest {
  pageIndex?: number
  pageSize?: number
  contractId?: number
  recipientId?: number
  isHandled?: boolean
}

// --- 电子签 ---

export interface ESignRecordDto {
  id: number
  contractId: number
  contractNo?: string
  signer: string
  signerRole?: string
  signMethod?: string
  signStatus: number
  signedTime?: string
  thirdPartyNo?: string
  signedFilePath?: string
  creatorName?: string
  createdTime: string
}

export interface CreateESignRecordRequest {
  contractId: number
  signer: string
  signerRole?: string
  signMethod?: string
}

export interface ManualSignRequest {
  signedFilePath?: string
}

export interface ESignRecordQueryRequest {
  pageIndex?: number
  pageSize?: number
  contractId?: number
  signStatus?: number
}

// ==================== 合同类型 API ====================

export function getContractTypeList(params: ContractTypeQueryRequest) {
  return get('/contract/types', params)
}

export function getAllEnabledContractTypes() {
  return get('/contract/types/all')
}

export function getContractTypeById(id: number) {
  return get(`/contract/types/${id}`)
}

export function createContractType(data: CreateContractTypeRequest) {
  return post('/contract/types', data)
}

export function updateContractType(id: number, data: UpdateContractTypeRequest) {
  return put(`/contract/types/${id}`, data)
}

export function deleteContractType(id: number) {
  return del(`/contract/types/${id}`)
}

export function updateContractTypeStatus(id: number, status: number) {
  return put(`/contract/types/${id}/status`, { status })
}

// ==================== 合同模板 API ====================

export function getContractTemplateList(params: ContractTemplateQueryRequest) {
  return get('/contract/templates', params)
}

export function getContractTemplateById(id: number) {
  return get(`/contract/templates/${id}`)
}

export function createContractTemplate(data: CreateContractTemplateRequest) {
  return post('/contract/templates', data)
}

export function updateContractTemplate(id: number, data: UpdateContractTemplateRequest) {
  return put(`/contract/templates/${id}`, data)
}

export function deleteContractTemplate(id: number) {
  return del(`/contract/templates/${id}`)
}

export function publishContractTemplate(id: number) {
  return put(`/contract/templates/${id}/publish`)
}

// ==================== 合同 API ====================

export function getContractList(params: ContractQueryRequest) {
  return get('/contract/contracts', params)
}

export function getContractStatistics() {
  return get('/contract/contracts/statistics')
}

export function getContractById(id: number) {
  return get(`/contract/contracts/${id}`)
}

export function createContract(data: CreateContractRequest) {
  return post('/contract/contracts', data)
}

export function updateContract(id: number, data: UpdateContractRequest) {
  return put(`/contract/contracts/${id}`, data)
}

export function deleteContract(id: number) {
  return del(`/contract/contracts/${id}`)
}

export function updateContractStatus(id: number, status: number) {
  return put(`/contract/contracts/${id}/status`, { status })
}

export function renewContract(id: number, data: CreateContractRequest) {
  return post(`/contract/contracts/${id}/renew`, data)
}

// ==================== 合同提醒 API ====================

export function getContractReminderList(params: ContractReminderQueryRequest) {
  return get('/contract/reminders', params)
}

export function getContractReminderById(id: number) {
  return get(`/contract/reminders/${id}`)
}

export function getPendingReminders(recipientId: number) {
  return get(`/contract/reminders/pending/${recipientId}`)
}

export function createContractReminder(data: CreateContractReminderRequest) {
  return post('/contract/reminders', data)
}

export function updateContractReminder(id: number, data: UpdateContractReminderRequest) {
  return put(`/contract/reminders/${id}`, data)
}

export function deleteContractReminder(id: number) {
  return del(`/contract/reminders/${id}`)
}

export function markReminderAsHandled(id: number) {
  return put(`/contract/reminders/${id}/handle`)
}

// ==================== 电子签 API ====================

export function getESignRecordList(params: ESignRecordQueryRequest) {
  return get('/contract/esign', params)
}

export function getESignRecordById(id: number) {
  return get(`/contract/esign/${id}`)
}

export function createESignRecord(data: CreateESignRecordRequest) {
  return post('/contract/esign', data)
}

export function completeESign(id: number, data: ManualSignRequest) {
  return put(`/contract/esign/${id}/complete`, data)
}

export function rejectESign(id: number) {
  return put(`/contract/esign/${id}/reject`)
}

export function deleteESignRecord(id: number) {
  return del(`/contract/esign/${id}`)
}
