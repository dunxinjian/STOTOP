// 财务模块 API
import { get, post, put, del } from './request'

// 科目管理
export function getAccountTree(category?: string, accountSetId?: number): Promise<any[]> {
  const params: any = {}
  if (category) params.category = category
  if (accountSetId) params.accountSetId = accountSetId
  return get('/finance/accounts/tree', Object.keys(params).length > 0 ? params : undefined)
}

// 按辅助核算类别查询关联科目列表
export function getAccountsByAuxType(params: { auxType: string; accountSetId: number }): Promise<{ id: number; code: string; name: string; auxiliary?: string }[]> {
  return get('/finance/accounts/by-aux-type', params)
}

// 批量更新科目辅助核算关联（编辑关联科目弹窗确定）
export function updateAccountAuxiliary(data: { accountSetId: number; auxType: string; accountCodes: string[] }): Promise<void> {
  return post('/finance/accounts/update-auxiliary', data)
}

export function getAccountById(id: number) {
  return get(`/finance/accounts/${id}`)
}

export function createAccount(data: object, accountSetId: number) {
  return post('/finance/accounts', data, { params: { accountSetId } })
}

export function updateAccount(id: number, data: object) {
  return put(`/finance/accounts/${id}`, data)
}

export function deleteAccount(id: number) {
  return del(`/finance/accounts/${id}`)
}

export function toggleAccountStatus(id: number) {
  return post(`/finance/accounts/${id}/toggle-status`)
}

export function getInitialBalances(accountSetId?: number) {
  return get('/finance/accounts/initial-balances', accountSetId ? { accountSetId } : undefined)
}

export function saveInitialBalances(data: { accountSetId: number; items: { accountId: number; debitBalance: number; creditBalance: number }[] }) {
  return post('/finance/accounts/initial-balances', data)
}

// 辅助核算
export function getAuxiliaryTypes(): Promise<{ id: number; code: string; name: string }[]> {
  return get('/finance/auxiliaries/types')
}

// 账套维度辅助核算项 CRUD
export function getAuxiliaryItemsByAccountSet(params: { accountSetId: number; auxType?: string; keyword?: string }) {
  return get('/finance/auxiliary-items', params)
}

export function createAuxiliaryItem(data: {
  accountSetId: number
  auxType: string
  code: string
  name: string
  shortName?: string
  contact?: string
  phone?: string
  address?: string
  remark?: string
}) {
  return post('/finance/auxiliary-items', data)
}

export function updateAuxiliaryItem(id: number, data: any) {
  return put(`/finance/auxiliary-items/${id}`, data)
}

export function deleteAuxiliaryItem(id: number) {
  return del(`/finance/auxiliary-items/${id}`)
}

export function checkAuxiliaryItemUsage(id: number): Promise<any> {
  return get(`/finance/auxiliary-items/${id}/check-usage`)
}

export function checkAuxiliaryCodeExists(accountSetId: number, code: string, excludeId: number): Promise<any> {
  return get('/finance/auxiliary-items/check-code', { accountSetId, code, excludeId })
}

// ========== 辅助核算 - 外部数据源集成 ==========

// 获取可选客户列表（排除已添加）
export function getAvailableCustomers(accountSetId: number) {
  return get('/finance/auxiliary-items/available-customers', { accountSetId })
}

// 获取可选供应商列表（排除已添加）
export function getAvailableSuppliers(accountSetId: number) {
  return get('/finance/auxiliary-items/available-suppliers', { accountSetId })
}

// 获取可选快递品牌列表（排除已添加）
export function getAvailableBrands(accountSetId: number) {
  return get('/finance/auxiliary-items/available-brands', { accountSetId })
}

// 从CRM客户批量添加辅助核算项
export function addFromCustomer(data: { customerIds: number[]; accountSetId: number }) {
  return post('/finance/auxiliary-items/add-from-customer', data)
}

// 从供应商批量添加辅助核算项
export function addFromSupplier(data: { supplierIds: number[]; accountSetId: number }) {
  return post('/finance/auxiliary-items/add-from-supplier', data)
}

// 从快递品牌批量添加辅助核算项
export function addFromBrand(data: { brandIds: number[]; accountSetId: number }) {
  return post('/finance/auxiliary-items/add-from-brand', data)
}

// 获取可选快递网点列表（排除已添加）
export function getAvailableNetworkPoints(accountSetId: number) {
  return get('/finance/auxiliary-items/available-network-points', { accountSetId })
}

// 从快递网点批量添加辅助核算项
export function addFromNetworkPoint(data: { networkPointCodes: string[]; accountSetId: number }) {
  return post('/finance/auxiliary-items/add-from-network-point', data)
}

// 获取可选员工列表（排除已添加）
export function getAvailableEmployees(accountSetId: number) {
  return get('/finance/auxiliary-items/available-employees', { accountSetId })
}

// 获取可选部门列表（排除已添加）
export function getAvailableDepartments(accountSetId: number) {
  return get('/finance/auxiliary-items/available-departments', { accountSetId })
}

// 从员工批量添加辅助核算项
export function addFromUser(data: { userIds: number[]; accountSetId: number }) {
  return post('/finance/auxiliary-items/add-from-user', data)
}

// 从部门批量添加辅助核算项
export function addFromOrg(data: { orgIds: number[]; accountSetId: number }) {
  return post('/finance/auxiliary-items/add-from-org', data)
}

// 唯一性校验
export function checkAuxiliaryUnique(params: {
  accountSetId: number
  auxType: string
  code?: string
  name?: string
  excludeId?: number
}) {
  return get('/finance/auxiliary-items/check-unique', params)
}

// 凭证管理
export function getVoucherList(params?: object) {
  return get('/finance/vouchers', params)
}

export function getVoucherDetail(id: number) {
  return get(`/finance/vouchers/${id}`)
}

export function createVoucher(data: object) {
  return post('/finance/vouchers', data)
}

export function updateVoucher(id: number, data: object) {
  return put(`/finance/vouchers/${id}`, data)
}

export function deleteVoucher(id: number) {
  return del(`/finance/vouchers/${id}`)
}

export function auditVoucher(id: number) {
  return post(`/finance/vouchers/${id}/audit`)
}

export function unauditVoucher(id: number) {
  return post(`/finance/vouchers/${id}/unaudit`)
}

export function saveDraft(data: object) {
  return post('/finance/vouchers/draft', data)
}

export function getDrafts() {
  return get('/finance/vouchers/drafts')
}

export function reorderVoucherNumbers(periodId: number) {
  return post(`/finance/vouchers/reorder/${periodId}`)
}

export function getNextVoucherNumber(word: string, periodId: number) {
  return get('/finance/vouchers/next-number', { word, periodId })
}

export function getPendingAuditCount() {
  return get('/finance/vouchers/pending-count')
}

export function copyVoucher(id: number) {
  return post(`/finance/vouchers/copy/${id}`)
}

export function reverseVoucher(id: number) {
  return post(`/finance/vouchers/reverse/${id}`)
}

export function batchAuditVouchers(data: { ids: number[], auditorId: number, auditorName: string }) {
  return post('/finance/vouchers/batch-audit', data)
}

export function checkVoucherGap(params: { accountSetId: number, year: number, periodNo: number }) {
  return get('/finance/vouchers/check-gap', params)
}

// 完成凭证补录
export function completeVoucherRecord(id: number) {
  return post(`/finance/vouchers/${id}/complete-record`)
}

// 会计期间
export function getPeriods(accountSetId?: number): Promise<{ id: number; periodName: string }[]> {
  return get('/finance/periods', accountSetId ? { accountSetId } : undefined)
}

export function getPeriodsByYear(year: number, accountSetId?: number): Promise<any[]> {
  return get(`/finance/periods/year/${year}`, accountSetId !== undefined ? { accountSetId } : undefined)
}

export function getCurrentPeriod(accountSetId?: number): Promise<{ id: number; periodName: string }> {
  return get('/finance/periods/current', accountSetId !== undefined ? { accountSetId } : undefined)
}

// 结账管理
export function getClosingInfo(accountSetId?: number) {
  return get('/finance/periods/closing-info', accountSetId ? { accountSetId } : undefined)
}

export function closePeriod(periodId: number, accountSetId?: number) {
  return post(`/finance/periods/${periodId}/close`, undefined, { params: accountSetId ? { accountSetId } : undefined })
}

export function reopenPeriod(periodId: number, accountSetId?: number) {
  return post(`/finance/periods/${periodId}/reopen`, undefined, { params: accountSetId ? { accountSetId } : undefined })
}

export function preCloseCheck(accountSetId: number, year: number, periodNo: number) {
  return get('/finance/periods/pre-close-check', { accountSetId, year, periodNo })
}

// 财务报表
export function getBalanceSheet(params?: object) {
  return get('/finance/reports/balance-sheet', params)
}

export function getIncomeStatement(params?: object) {
  return get('/finance/reports/income-statement', params)
}

export function getCashFlowStatement(params?: object) {
  return get('/finance/reports/cash-flow', params)
}

// 报表相关 API
export function getAccountBalance(params: { periodId: number, accountId?: number, accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/account-balance', params)
}

// 从凭证计算科目余额表（按年月）
export function getAccountBalanceByYearMonth(params: { year: number, month: number, accountId?: number, accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/account-balance/calculate', params)
}

export function getAuxiliaryBalance(params: { periodId: number, type?: string, accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/auxiliary-balance', params)
}

export function getAssetBalance(params: { periodId?: number, accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/asset-balance', params)
}

// ==================== 资产类别管理 ====================
export function getAssetCategories(accountSetId: number): Promise<any[]> {
  return get('/finance/assets/categories', { accountSetId })
}

export function createAssetCategory(data: any, accountSetId: number) {
  return post('/finance/assets/categories', data, { params: { accountSetId } })
}

export function updateAssetCategory(id: number, data: any, accountSetId: number) {
  return put(`/finance/assets/categories/${id}`, data, { params: { accountSetId } })
}

export function deleteAssetCategory(id: number) {
  return del(`/finance/assets/categories/${id}`)
}

// ==================== 资产卡片管理 ====================
export function getAssetCards(params?: { categoryId?: number; accountSetId?: number }) {
  return get('/finance/assets/cards', params)
}

export function createAssetCard(data: any, accountSetId: number) {
  return post('/finance/assets/cards', data, { params: { accountSetId } })
}

export function updateAssetCard(id: number, data: any, accountSetId: number) {
  return put(`/finance/assets/cards/${id}`, data, { params: { accountSetId } })
}

export function deleteAssetCard(id: number) {
  return del(`/finance/assets/cards/${id}`)
}

// 小番财务格式的资产卡片导入
export function importAssetCardsFromXiaofan(formData: FormData, accountSetId: number): Promise<{
  totalRows: number
  importedCount: number
  skippedCount: number
  errors: { rowNumber: number; message: string }[]
}> {
  return post(`/finance/assets/cards/import/xiaofan?accountSetId=${accountSetId}`, formData as any, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 120000,
  })
}

// 小番财务格式的资产类别导入
export function importAssetCategoriesFromXiaofan(formData: FormData, accountSetId: number): Promise<{
  totalRows: number
  importedCount: number
  skippedCount: number
  errors: { rowNumber: number; message: string }[]
}> {
  return post(`/finance/assets/categories/import/xiaofan?accountSetId=${accountSetId}`, formData as any, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 120000,
  })
}

export function getProfitStatement(params: { startPeriodId: number, endPeriodId: number, format: string, accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/profit-statement', params)
}

// 小企业利润表（按年月查询）
export function getSmallEnterpriseProfitStatement(params: { year: number, month: number, accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/small-enterprise-profit-statement', params)
}

export function getCashFlowReport(params: { startPeriodId: number, endPeriodId: number, accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/cash-flow-report', params)
}

export function getAccountDetail(params: { accountId: number, year: number, periodNo: number, accountSetId?: number }): Promise<any> {
  return get('/finance/reports/account-detail', params)
}

export function getTaxPayable(params: { periodId: number, accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/tax-payable', params)
}

// 阿米巴经营单元树（组织树，分摊配置页使用）
export function getAmoebaTree(): Promise<any[]> {
  return get('/finance/amoeba/units/tree')
}

// 部门组织架构
export function getOrganizationTree(): Promise<any[]> {
  return get('/system/organizations/tree')
}

// ==================== 账套管理 ====================

export interface AccountSetDto {
  id: number
  fName: string
  fCode: string
  fCompanyName: string
  fDescription?: string
  fIsDefault: boolean
  fStatus: number
  fSortOrder: number
  fStartYear: number
  fStartMonth: number
  fOrgId?: number
}

export interface CreateAccountSetParams extends Partial<AccountSetDto> {
  fTemplateType?: string        // "empty" | "industry" | "existing"
  fIndustryCode?: string        // 行业模板编码
  fSourceAccountSetId?: number  // 源账套ID（复制已有账套时）
}

// 获取账套模板列表
export function getAccountSetTemplates(): Promise<any[]> {
  return get('/finance/account-sets/templates')
}

// 预览模板科目
export function getTemplateAccounts(code: string): Promise<any[]> {
  return get(`/finance/account-sets/templates/${code}/accounts`)
}

export function getAccountSets(orgId?: number): Promise<AccountSetDto[]> {
  return get('/finance/account-sets', orgId ? { orgId } : undefined)
}

export function getAccountSet(id: number): Promise<AccountSetDto> {
  return get(`/finance/account-sets/${id}`)
}

export function createAccountSet(data: CreateAccountSetParams): Promise<AccountSetDto> {
  return post('/finance/account-sets', data)
}

export function updateAccountSet(id: number, data: Partial<AccountSetDto>): Promise<AccountSetDto> {
  return put(`/finance/account-sets/${id}`, data)
}

export function deleteAccountSet(id: number) {
  return del(`/finance/account-sets/${id}`)
}

export function initializeAccountSet(id: number, force: boolean = false) {
  return post(`/finance/account-sets/${id}/initialize?force=${force}`)
}

// ==================== 日记账 ====================

// 日记账 - 全部
export function getJournalEntries(params: any) {
  return get('/finance/journal', params)
}
// 日记账 - 现金银行
export function getCashBankJournal(params: any) {
  return get('/finance/journal/cash-bank', params)
}
// 日记账 - 应收应付
export function getReceivablePayableJournal(params: any) {
  return get('/finance/journal/receivable-payable', params)
}
// 新增日记账
export function createJournalEntry(data: any) {
  return post('/finance/journal/create', data)
}
// 调整
export function adjustJournal(data: any) {
  return post('/finance/journal/adjust', data)
}
// 生成凭证
export function generateJournalVoucher(data: any) {
  return post('/finance/journal/generate-voucher', data)
}
// 删除
export function deleteJournalVoucher(voucherId: number, accountSetId?: number) {
  return del(`/finance/journal/${voucherId}`, { params: { accountSetId } })
}

// ==================== 操作日志 ====================
export function getOperationLogs(params: {
  pageIndex?: number
  pageSize?: number
  accountSetId?: number
  module?: string
  operationType?: string
  startDate?: string
  endDate?: string
  keyword?: string
}) {
  return get('/finance/operation-logs', params)
}

// ==================== 附件上传 ====================

export function uploadAttachment(formData: FormData): Promise<{
  fileId: number
  fileName: string
  originalName: string
  filePath: string
  fileSize: number
  contentType: string
  uploadTime: string
}> {
  return post('/finance/files/upload', formData as any, {
    headers: { 'Content-Type': 'multipart/form-data' }
  })
}

export function getAttachments(businessType: string, businessId: number): Promise<{
  id: number
  fileName: string
  originalName: string
  filePath: string
  fileSize: number
  contentType: string
  uploadTime: string
  uploaderName: string
}[]> {
  return get('/finance/files/list', { businessType, businessId })
}

export function deleteAttachment(id: number) {
  return del(`/finance/files/${id}`)
}

export function getAttachmentDownloadUrl(id: number): string {
  return `/api/finance/files/${id}`
}

// ==================== 凭证模板 ====================

export function getVoucherTemplates(accountSetId: number) {
  return get('/finance/voucher-templates', { accountSetId })
}

export function getVoucherTemplateDetail(id: number) {
  return get(`/finance/voucher-templates/${id}`)
}

export function createVoucherTemplate(data: object) {
  return post('/finance/voucher-templates', data)
}

export function updateVoucherTemplate(id: number, data: object) {
  return put(`/finance/voucher-templates/${id}`, data)
}

export function deleteVoucherTemplate(id: number) {
  return del(`/finance/voucher-templates/${id}`)
}

export function generateVoucherFromTemplate(id: number, data: { date: string; accountSetId: number }) {
  return post(`/finance/voucher-templates/${id}/generate`, data)
}

// ==================== 汇率管理 ====================

export function getExchangeRates(accountSetId: number, currencyCode?: string) {
  const params: any = { accountSetId }
  if (currencyCode) params.currencyCode = currencyCode
  return get('/finance/exchange-rates', params)
}

export function getExchangeCurrencies(accountSetId: number): Promise<{ code: string; name: string }[]> {
  return get('/finance/exchange-rates/currencies', { accountSetId })
}

export function getLatestExchangeRate(params: { accountSetId: number; currencyCode: string; date: string }) {
  return get('/finance/exchange-rates/latest', params)
}

export function saveExchangeRate(data: {
  id?: number | null
  accountSetId: number
  currencyCode: string
  currencyName: string
  rate: number
  effectiveDate: string
}) {
  return post('/finance/exchange-rates', data)
}

export function deleteExchangeRate(id: number) {
  return del(`/finance/exchange-rates/${id}`)
}

// ==================== 银行对账 ====================

export function importBankStatements(formData: FormData, accountSetId?: number): Promise<{ importCount: number }> {
  const params = accountSetId ? `?accountSetId=${accountSetId}` : ''
  return post(`/finance/bank/import${params}`, formData as any, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 60000,
  })
}

export function getBankStatements(params: {
  pageIndex?: number
  pageSize?: number
  startDate?: string
  endDate?: string
  matchStatus?: number
  bankAccount?: string
  accountSetId?: number
}): Promise<{
  items: any[]
  total: number
  pageIndex: number
  pageSize: number
  matchedCount: number
  unmatchedCount: number
}> {
  return get('/finance/bank/statements', params)
}

export function getUnmatchedVouchers(params: {
  startDate: string
  endDate: string
  accountSetId?: number
}): Promise<any[]> {
  return get('/finance/bank/unmatched-vouchers', params)
}

export function autoMatchBankStatements(accountSetId?: number): Promise<{ matchCount: number }> {
  return post('/finance/bank/auto-match', undefined, { params: accountSetId ? { accountSetId } : undefined })
}

export function manualMatchBankStatement(data: {
  bankStatementId: number
  voucherId: number
  voucherEntryId?: number
}, accountSetId?: number): Promise<void> {
  return post('/finance/bank/manual-match', data, { params: accountSetId ? { accountSetId } : undefined })
}

export function unmatchBankStatement(id: number): Promise<void> {
  return post(`/finance/bank/unmatch/${id}`)
}

export function getReconciliationReport(params: {
  periodId: number
  accountSetId?: number
}): Promise<any> {
  return get('/finance/bank/reconciliation-report', params)
}

// ==================== 试算平衡 ====================

export function generateTrialBalance(periodId: number, accountSetId?: number) {
  return post('/finance/assets/trial-balance/generate', undefined, { params: { periodId, accountSetId: accountSetId ?? 0 } })
}

export function getTrialBalance(periodId: number, accountSetId?: number) {
  return get('/finance/assets/trial-balance', { periodId, accountSetId: accountSetId ?? 0 })
}

// ==================== 资产折旧 ====================

export function calculateDepreciationPreview(periodId: number, accountSetId?: number) {
  return post('/finance/assets/calculate-depreciation', undefined, { params: { periodId, accountSetId: accountSetId ?? 0 } })
}

export function generateDepreciationVouchers(periodId: number, accountSetId?: number) {
  return post('/finance/assets/generate-depreciation-vouchers', undefined, { params: { periodId, accountSetId: accountSetId ?? 0 } })
}

// ==================== 发票管理 ====================

export interface InvoiceDto {
  id: number
  accountSetId: number
  invoiceType: string
  invoiceNo: string
  invoiceCode?: string
  invoiceDate: string
  sellerName?: string
  sellerTaxNo?: string
  buyerName?: string
  buyerTaxNo?: string
  amount: number
  taxAmount: number
  totalAmount: number
  taxRate: number
  direction: string
  matchStatus: number
  matchedVoucherId?: number
  importBatchId?: number
  status: number
  createdTime: string
}

export interface TaxSummaryDto {
  month: number
  inputTaxAmount: number
  outputTaxAmount: number
  taxPayable: number
}

export function importInvoices(formData: FormData, accountSetId?: number): Promise<{ count: number }> {
  return post(`/finance/invoices/import${accountSetId ? `?accountSetId=${accountSetId}` : ''}`, formData as any, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 60000,
  })
}

export function getInvoiceList(params?: object): Promise<{
  items: InvoiceDto[]
  total: number
  pageIndex: number
  pageSize: number
}> {
  return get('/finance/invoices', params)
}

export function getInvoiceDetail(id: number): Promise<InvoiceDto> {
  return get(`/finance/invoices/${id}`)
}

export function matchInvoice(id: number, data: { voucherId: number }, accountSetId?: number) {
  return post(`/finance/invoices/${id}/match${accountSetId ? `?accountSetId=${accountSetId}` : ''}`, data)
}

export function generateVoucherFromInvoice(id: number, data: { periodId: number; voucherWord?: string }, accountSetId?: number) {
  return post(`/finance/invoices/${id}/generate-voucher${accountSetId ? `?accountSetId=${accountSetId}` : ''}`, data)
}

export function getInvoiceTaxSummary(params: { year: number; accountSetId?: number }): Promise<TaxSummaryDto[]> {
  return get('/finance/invoices/tax-summary', params)
}

// ==================== 公式配置 ====================

export interface FormulaDto {
  id: number
  reportType: string
  itemName: string
  rowIndex: number
  formula?: string
  formulaType: string
  accountCodes?: string
  displayConfig?: string
  isEnabled: boolean
  accountSetId: number
  sortOrder: number
}

export function getFormulas(params: { reportType: string; accountSetId?: number }): Promise<FormulaDto[]> {
  return get('/finance/formulas', params)
}

export function createFormula(data: object): Promise<FormulaDto> {
  return post('/finance/formulas', data)
}

export function updateFormula(id: number, data: object): Promise<FormulaDto> {
  return put(`/finance/formulas/${id}`, data)
}

export function deleteFormula(id: number) {
  return del(`/finance/formulas/${id}`)
}

export function testFormula(data: { formula: string; accountAmounts: Record<string, number>; rowResults: Record<number, number> }): Promise<{ success: boolean; result: number; error?: string }> {
  return post('/finance/formulas/test', data)
}

export function initDefaultFormulas(data: { reportType: string; accountSetId: number }): Promise<number> {
  return post('/finance/formulas/init-defaults', data)
}

// ==================== 报表图表与钻取 ====================

export function getReportDrillDown(params: { reportType: string; rowIndex: number; year: number; month: number; accountSetId?: number; accountCode?: string }): Promise<any[]> {
  return get('/finance/reports/drill-down', params)
}

export function getProfitTrend(params: { year: number; accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/profit-trend', params)
}

export function getRevenueComposition(params: { year: number; month: number; accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/revenue-composition', params)
}

export function getExpenseComposition(params: { year: number; month: number; accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/expense-composition', params)
}

export function getYoYComparison(params: { year: number; month: number; accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/yoy-comparison', params)
}

export function getMoMComparison(params: { year: number; month: number; accountSetId?: number }): Promise<any[]> {
  return get('/finance/reports/mom-comparison', params)
}

// ==================== 资金管理 - 交易渠道 ====================

export interface BankChannelDto {
  id: number
  name: string
  type: number
  accountNo?: string
  bankName?: string
  importTemplate?: string
  status: number
  creatorName?: string
  createdTime: string
  updaterName?: string
  updatedTime?: string
}

export interface CreateBankChannelRequest {
  name: string
  type: number
  accountNo?: string
  bankName?: string
  importTemplate?: string
}

export interface UpdateBankChannelRequest extends CreateBankChannelRequest {
  status: number
}

export interface BankChannelQueryRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  status?: number
}

export function getBankChannelList(params: BankChannelQueryRequest) {
  return get('/finance/banking-channels', params)
}

export function getAllEnabledBankChannels() {
  return get('/finance/banking-channels/all')
}

export function getBankChannelById(id: number) {
  return get(`/finance/banking-channels/${id}`)
}

export function createBankChannel(data: CreateBankChannelRequest) {
  return post('/finance/banking-channels', data)
}

export function updateBankChannel(id: number, data: UpdateBankChannelRequest) {
  return put(`/finance/banking-channels/${id}`, data)
}

export function deleteBankChannel(id: number) {
  return del(`/finance/banking-channels/${id}`)
}

// ==================== 资金管理 - 银行流水 ====================

export interface BankTransactionDto {
  id: number
  channelId: number
  channelName?: string
  transactionDate: string
  transactionNo: string
  counterpartAccount?: string
  counterpartName?: string
  direction: number
  amount: number
  balance?: number
  summary?: string
  remark?: string
  importBatchId?: number
  matchStatus: number
  relatedBusinessType?: string
  relatedBusinessId?: number
  voucherId?: number
  creatorName?: string
  createdTime: string
}

export interface BankTransactionQueryRequest {
  pageIndex?: number
  pageSize?: number
  channelId?: number
  startDate?: string
  endDate?: string
  direction?: number
  matchStatus?: number
  counterpartName?: string
  keyword?: string
}

export interface BankTransactionImportItem {
  channelId: number
  transactionDate: string
  transactionNo: string
  counterpartAccount?: string
  counterpartName?: string
  direction: number
  amount: number
  balance?: number
  summary?: string
  remark?: string
}

export interface BankTransactionImportRequest {
  channelId: number
  items: BankTransactionImportItem[]
}

export interface BankTransactionImportResult {
  totalReceived: number
  importedCount: number
  duplicateCount: number
}

export interface BankTransactionManualMatchRequest {
  transactionId: number
  businessType: string
  businessId: number
}

export interface BankTransactionSkipMatchRequest {
  transactionIds: number[]
}

export interface AutoMatchResult {
  totalProcessed: number
  matchedCount: number
  unmatchedCount: number
}

export function getBankTransactionList(params: BankTransactionQueryRequest) {
  return get('/finance/banking-transactions', params)
}

export function getBankTransactionById(id: number) {
  return get(`/finance/banking-transactions/${id}`)
}

export function importBankTransactions(data: BankTransactionImportRequest) {
  return post('/finance/banking-transactions/import', data, { timeout: 60000 })
}

export function autoMatchBankTransactions() {
  return post('/finance/banking-transactions/auto-match')
}

export function manualMatchBankTransaction(data: BankTransactionManualMatchRequest) {
  return post('/finance/banking-transactions/manual-match', data)
}

export function skipMatchBankTransactions(data: BankTransactionSkipMatchRequest) {
  return post('/finance/banking-transactions/skip-match', data)
}

// ==================== 资金管理 - 凭证规则 ====================

export interface VoucherRuleDto {
  id: number
  ruleName: string
  channelId?: number
  channelName?: string
  matchCondition?: string
  debitAccount?: string
  creditAccount?: string
  summaryTemplate?: string
  priority: number
  status: number
  creatorName?: string
  createdTime: string
  updaterName?: string
  updatedTime?: string
}

export interface CreateVoucherRuleRequest {
  ruleName: string
  channelId?: number
  matchCondition?: string
  debitAccount?: string
  creditAccount?: string
  summaryTemplate?: string
  priority: number
}

export interface UpdateVoucherRuleRequest extends CreateVoucherRuleRequest {
  status: number
}

export interface VoucherRuleQueryRequest {
  pageIndex?: number
  pageSize?: number
  channelId?: number
  status?: number
  keyword?: string
}

export interface VoucherGenerateResult {
  totalProcessed: number
  generatedCount: number
  skippedCount: number
  errors: string[]
}

export interface FundStatisticsDto {
  totalImported: number
  matchedCount: number
  unmatchedCount: number
  skipMatchedCount: number
  voucherGeneratedCount: number
  matchRate: number
  voucherRate: number
}

export function getVoucherRuleList(params: VoucherRuleQueryRequest) {
  return get('/finance/banking-voucher-rules', params)
}

export function getVoucherRulesByPriority() {
  return get('/finance/banking-voucher-rules/by-priority')
}

export function getVoucherRuleById(id: number) {
  return get(`/finance/banking-voucher-rules/${id}`)
}

export function createVoucherRule(data: CreateVoucherRuleRequest) {
  return post('/finance/banking-voucher-rules', data)
}

export function updateVoucherRule(id: number, data: UpdateVoucherRuleRequest) {
  return put(`/finance/banking-voucher-rules/${id}`, data)
}

export function deleteVoucherRule(id: number) {
  return del(`/finance/banking-voucher-rules/${id}`)
}

export function generateVoucherDraft() {
  return post('/finance/banking-voucher-rules/generate-voucher-draft')
}

export function getFundStatistics() {
  return get('/finance/banking-voucher-rules/statistics')
}

// ==================== 预算管理与资金预测 ====================

export interface BudgetVersionDto {
  id: number
  accountSetId: number
  name: string
  scenarioType: string
  year: number
  status: string
  ownerOrgId: number
  createdBy?: string
  createdTime: string
  approvedBy?: string
  approvedTime?: string
}

export interface CreateBudgetVersionRequest {
  accountSetId: number
  name: string
  scenarioType: string
  year: number
  ownerOrgId: number
}

export interface BudgetLineDto {
  id?: number
  budgetVersionId?: number
  period: string
  orgId: number
  amoebaUnitId?: number | null
  accountId?: number | null
  accountCode?: string | null
  plItemId?: number | null
  dimensionJson?: string | null
  amount: number
  quantity?: number | null
  unitPrice?: number | null
  remark?: string | null
}

export interface BudgetExpenseMappingDto {
  id?: number
  accountSetId: number
  orgId?: number | null
  expenseType: string
  accountCode?: string | null
  plItemId?: number | null
  cashCategory: string
  status: number
  remark?: string | null
}

export interface TreasuryAccountBindingDto {
  id?: number
  accountSetId: number
  orgId?: number | null
  channelId?: number | null
  cashAccountId?: number | null
  accountNo?: string | null
  openingSource: string
  manualOpeningAmount?: number | null
  status: number
  remark?: string | null
}

export interface TreasuryPlanLineDto {
  id?: number
  accountSetId: number
  orgId?: number | null
  planDate: string
  weekStartDate?: string
  direction: 'inflow' | 'outflow'
  cashCategory: string
  amount: number
  probability: number
  sourceType: string
  sourceId?: number | null
  counterpartyName?: string | null
  remark?: string | null
}

export interface TreasuryWeekDto {
  weekStartDate: string
  openingCash: number
  inflow: number
  outflow: number
  endingCash: number
  belowSafetyCash: boolean
}

export interface Rolling13WeekTreasuryDto {
  openingCash: number
  safetyCash: number
  weeks: TreasuryWeekDto[]
}

export interface BudgetPreviewRequest {
  accountSetId: number
  orgId: number
  period: string
  sourceType: string
  sourceId?: number | null
  expenseType?: string | null
  accountCode?: string | null
  plItemId?: number | null
  amount: number
}

export interface BudgetPreviewResult {
  mappingMissing: boolean
  missingReason?: string | null
  accountCode?: string | null
  plItemId?: number | null
  budgetAmount: number
  occupiedAmount: number
  availableAmount: number
  requestAmount: number
  gapAmount: number
  policy: string
  blocked: boolean
}

export function getBudgetVersions(params: { accountSetId: number; year?: number }): Promise<BudgetVersionDto[]> {
  return get('/finance/budgets/versions', params)
}

export function createBudgetVersion(data: CreateBudgetVersionRequest): Promise<BudgetVersionDto> {
  return post('/finance/budgets/versions', data)
}

export function submitBudgetVersion(id: number) {
  return post(`/finance/budgets/versions/${id}/submit`)
}

export function approveBudgetVersion(id: number) {
  return post(`/finance/budgets/versions/${id}/approve`)
}

export function getBudgetLines(
  versionId: number,
  params?: { period?: string; orgId?: number },
): Promise<BudgetLineDto[]> {
  return get(`/finance/budgets/versions/${versionId}/lines`, params)
}

export function batchUpsertBudgetLines(versionId: number, lines: BudgetLineDto[]) {
  return post(`/finance/budgets/versions/${versionId}/lines:batch-upsert`, { lines })
}

export function getBudgetExpenseMappings(params: { accountSetId: number; orgId?: number }): Promise<BudgetExpenseMappingDto[]> {
  return get('/finance/budgets/expense-mappings', params)
}

export function saveBudgetExpenseMapping(data: BudgetExpenseMappingDto): Promise<BudgetExpenseMappingDto> {
  return post('/finance/budgets/expense-mappings', data)
}

export function getTreasuryAccountBindings(params: { accountSetId: number; orgId?: number }): Promise<TreasuryAccountBindingDto[]> {
  return get('/finance/treasury-plans/account-bindings', params)
}

export function saveTreasuryAccountBinding(data: TreasuryAccountBindingDto): Promise<TreasuryAccountBindingDto> {
  return post('/finance/treasury-plans/account-bindings', data)
}

export function getTreasuryPlanLines(params: {
  accountSetId: number
  startDate?: string
  endDate?: string
  orgId?: number
}): Promise<TreasuryPlanLineDto[]> {
  return get('/finance/treasury-plans/lines', params)
}

export function saveTreasuryPlanLine(data: TreasuryPlanLineDto): Promise<TreasuryPlanLineDto> {
  return post('/finance/treasury-plans/lines', data)
}

export function deleteTreasuryPlanLine(id: number) {
  return del(`/finance/treasury-plans/lines/${id}`)
}

export function getRolling13WeekTreasuryPlan(params: {
  accountSetId: number
  startDate?: string
  orgId?: number
  safetyCash?: number
}): Promise<Rolling13WeekTreasuryDto> {
  return get('/finance/treasury-plans/rolling-13-weeks', params)
}

export function previewBudgetControl(data: BudgetPreviewRequest): Promise<BudgetPreviewResult> {
  return post('/finance/budget-control/preview', data)
}

// ==================== 科目模板 ====================

export function getAccountTemplates() {
  return get('/finance/account-templates')
}

export function getAccountTemplateDetail(id: number) {
  return get(`/finance/account-templates/${id}`)
}

export function createAccountTemplate(data: object) {
  return post('/finance/account-templates', data)
}

export function updateAccountTemplate(id: number, data: object) {
  return put(`/finance/account-templates/${id}`, data)
}

export function deleteAccountTemplate(id: number) {
  return del(`/finance/account-templates/${id}`)
}

export function getAccountTemplateItems(id: number) {
  return get(`/finance/account-templates/${id}/items`)
}

export function addAccountTemplateItem(id: number, data: object) {
  return post(`/finance/account-templates/${id}/items`, data)
}

export function updateAccountTemplateItem(id: number, itemId: number, data: object) {
  return put(`/finance/account-templates/${id}/items/${itemId}`, data)
}

export function deleteAccountTemplateItem(id: number, itemId: number) {
  return del(`/finance/account-templates/${id}/items/${itemId}`)
}

export function applyAccountTemplate(id: number, accountSetId: number) {
  return post(`/finance/account-templates/${id}/apply/${accountSetId}`)
}


// ==================== 凭证导入导出 ====================

// 导出凭证 Excel（返回 Blob）
export function exportVouchers(ids: number[], accountSetId: number) {
  return get<Blob>('/finance/vouchers/export', { ids: ids.join(','), accountSetId }, { responseType: 'blob' })
}

// 下载凭证导入模板（返回 Blob）
export function exportVoucherTemplate() {
  return get<Blob>('/finance/vouchers/export-template', undefined, { responseType: 'blob' })
}

export function importVouchers(data: FormData) {
  return post<{
    success: boolean
    importedCount: number
    errors: Array<{ rowNumber: number; message: string }>
  }>('/finance/vouchers/import', data)
}

// ========== 辅助核算别名 ==========

export interface AuxiliaryAliasDto {
  id: string
  auxiliaryItemId: number
  auxiliaryItemName: string
  auxiliaryItemCode: string
  alias: string
  auxType: string
  organizationId?: string
}

export function getAuxiliaryAliases(params?: { auxType?: string }): Promise<AuxiliaryAliasDto[]> {
  return get('/finance/auxiliary-aliases', params)
}

export function createAuxiliaryAlias(data: Partial<AuxiliaryAliasDto>): Promise<AuxiliaryAliasDto> {
  return post('/finance/auxiliary-aliases', data)
}

export function updateAuxiliaryAlias(id: string, data: Partial<AuxiliaryAliasDto>): Promise<AuxiliaryAliasDto> {
  return put(`/finance/auxiliary-aliases/${id}`, data)
}

export function deleteAuxiliaryAlias(id: string) {
  return del(`/finance/auxiliary-aliases/${id}`)
}

// ===== 阿米巴新版报表 API =====

/** 获取阿米巴经营报表（多数据源聚合） */
export function getAmoebaReport(data: {
  orgId: number
  accountSetId: number
  startDate: string
  endDate: string
  granularity?: string
  viewMode?: string
  unitIds?: number[]
  siteCodes?: string[]
  brandCodes?: string[]
  direction?: string
}): Promise<any> {
  return post('/finance/reports/amoeba-report', data)
}

/** 钻取明细 */
export function getAmoebaDrillDown(params: {
  unitId: number
  date: string
  category: string
  accountSetId?: number
}): Promise<any> {
  return get('/finance/reports/amoeba-report/drill-down', params)
}

// ===== 阿米巴映射规则 =====
export function getAmoebaMappingRules(): Promise<any> {
  return get('/finance/amoeba/mapping-rules')
}
export function createAmoebaMappingRule(data: any): Promise<any> {
  return post('/finance/amoeba/mapping-rules', data)
}
export function updateAmoebaMappingRule(id: number, data: any): Promise<any> {
  return put(`/finance/amoeba/mapping-rules/${id}`, data)
}
export function deleteAmoebaMappingRule(id: number): Promise<any> {
  return del(`/finance/amoeba/mapping-rules/${id}`)
}


// ===== 阿米巴手工分类 =====
export function getAmoebaUnclassified(params: {
  startDate: string
  endDate: string
  accountSetId: number
}): Promise<any> {
  return get('/finance/amoeba/unclassified', params)
}
export function batchAmoebaClassify(data: {
  items: Array<{ entryId: number; plItemId: number }>
}): Promise<any> {
  return post('/finance/amoeba/classify', data)
}



// ===== 阿米巴损益模板管理 =====
export function getAmoebaPLTemplates(params?: { accountSetId?: number }): Promise<any> {
  return get('/finance/amoeba/templates', params)
}
export function getAmoebaPLTemplateById(id: number): Promise<any> {
  return get(`/finance/amoeba/templates/${id}`)
}
export function createAmoebaPLTemplate(data: any): Promise<any> {
  return post('/finance/amoeba/templates', data)
}
export function updateAmoebaPLTemplate(id: number, data: any): Promise<any> {
  return put(`/finance/amoeba/templates/${id}`, data)
}
export function deleteAmoebaPLTemplate(id: number): Promise<any> {
  return del(`/finance/amoeba/templates/${id}`)
}
export function cloneAmoebaPLTemplate(sourceId: number, data: { name: string; accountSetId: number; description?: string }): Promise<any> {
  return post(`/finance/amoeba/templates/${sourceId}/clone`, data)
}

// ===== 损益项明细钻取 =====
/** 损益项明细钻取（科目汇总→凭证分录） */
export function getAmoebaPLItemDetail(params: {
  templateId: number
  accountSetId: number
  plItemId: number
  startDate: string
  endDate: string
  unitIds?: number[]
}): Promise<any> {
  return post('/finance/reports/amoeba-report/pl-item-detail', params)
}

/** 判断损益项是否为出港收入项 */
export function isOutboundRevenueItem(plItemId: number): Promise<boolean> {
  return get(`/finance/reports/amoeba-report/is-outbound-revenue/${plItemId}`)
}

/** 出港收入按业务对象下钻 */
export interface BillingDrillDownParams {
  plItemId: number
  unitIds?: number[]
  startDate: string
  endDate: string
  accountSetId: number
}

export interface BillingDrillDownClient {
  clientId: string
  clientName: string
  amount: number
}

export interface BillingDrillDownGroup {
  typeCode: string
  typeName: string
  subTotal: number
  clients: BillingDrillDownClient[]
}

export interface BillingDrillDownResponse {
  unitName: string
  dateRange: string
  totalAmount: number
  groups: BillingDrillDownGroup[]
}

export function getBillingDrillDown(params: BillingDrillDownParams): Promise<BillingDrillDownResponse> {
  return post('/finance/reports/amoeba-report/billing-drill-down', params)
}

/** 判断损益项是否为折旧项 */
export function isDepreciationItem(plItemId: number): Promise<boolean> {
  return get(`/finance/reports/amoeba-report/is-depreciation/${plItemId}`)
}

/** 折旧项下钻：资产卡片折旧明细 */
export interface AssetDepreciationDetail {
  assetCardId: number
  assetCode: string
  assetName: string
  originalValue: number
  monthlyDepreciation: number
  periodDepreciation: number
  department: string | null
}

export interface DepreciationDrillDownResponse {
  totalAmount: number
  assets: AssetDepreciationDetail[]
}

export function getDepreciationDrillDown(params: {
  plItemId: number
  startDate: string
  endDate: string
  accountSetId: number
}): Promise<DepreciationDrillDownResponse> {
  return post('/finance/reports/amoeba-report/depreciation-drill-down', params)
}

// ===== 损益项管理 =====
export function addAmoebaPLItem(templateId: number, data: any): Promise<any> {
  return post(`/finance/amoeba/templates/${templateId}/items`, data)
}
export function updateAmoebaPLItem(templateId: number, itemId: number, data: any): Promise<any> {
  return put(`/finance/amoeba/templates/${templateId}/items/${itemId}`, data)
}
export function deleteAmoebaPLItem(templateId: number, itemId: number): Promise<any> {
  return del(`/finance/amoeba/templates/${templateId}/items/${itemId}`)
}
export function reorderAmoebaPLItems(templateId: number, items: { itemId: number; sort: number; parentId: number }[]) {
  return put(`/finance/amoeba/templates/${templateId}/items/reorder`, items)
}

// [已移除] 方向API - 统一树模型下Tab=depth=0 group节点，通过树节点CRUD管理

// 模板科目覆盖率诊断
export function getAmoebaCoverageReport(templateId: number, period: string, accountSetId: number): Promise<any> {
  return get(`/finance/amoeba/templates/${templateId}/coverage`, { period, accountSetId })
}

// 跨模板克隆损益项（将源模板某项目复制到目标模板下，可选指定父级）
// 后端若尚未实现，前端调用方应做容错处理（catch 后给出明确提示）
export function cloneItemFromTemplate(
  targetTemplateId: number,
  payload: { sourceTemplateId: number; sourceItemId: number; targetParentId?: number | null; cloneChildren?: boolean }
): Promise<any> {
  return post(`/finance/amoeba/templates/${targetTemplateId}/items/clone-from`, payload)
}

// ===== 外部凭证迁移 =====

// 迁移方案
export function getMigrationSchemes(params?: { accountSetId?: number }): Promise<any[]> {
  return get('/finance/migration/schemes', params)
}
export function getMigrationScheme(id: string): Promise<any> {
  return get(`/finance/migration/schemes/${id}`)
}
export function createMigrationScheme(data: any): Promise<any> {
  return post('/finance/migration/schemes', data)
}
export function updateMigrationScheme(id: string, data: any): Promise<any> {
  return put(`/finance/migration/schemes/${id}`, data)
}
export function deleteMigrationScheme(id: string): Promise<any> {
  return del(`/finance/migration/schemes/${id}`)
}

// 科目映射
export function getAccountMappings(schemeId: string): Promise<any[]> {
  return get('/finance/migration/account-mappings', { schemeId })
}
export function createAccountMappings(data: any): Promise<any> {
  return post('/finance/migration/account-mappings', data)
}
export function updateAccountMapping(id: string, data: any): Promise<any> {
  return put(`/finance/migration/account-mappings/${id}`, data)
}
export function deleteAccountMapping(id: string): Promise<any> {
  return del(`/finance/migration/account-mappings/${id}`)
}

// 辅助映射
export function getAuxiliaryMappings(schemeId: string): Promise<any[]> {
  return get('/finance/migration/auxiliary-mappings', { schemeId })
}
export function createAuxiliaryMappings(data: any): Promise<any> {
  return post('/finance/migration/auxiliary-mappings', data)
}
export function updateAuxiliaryMapping(id: string, data: any): Promise<any> {
  return put(`/finance/migration/auxiliary-mappings/${id}`, data)
}
export function deleteAuxiliaryMapping(id: string): Promise<any> {
  return del(`/finance/migration/auxiliary-mappings/${id}`)
}

// 资产映射
export function getAssetMappings(schemeId: string): Promise<any[]> {
  return get('/finance/migration/asset-mappings', { schemeId })
}
export function createAssetMappings(data: any): Promise<any> {
  return post('/finance/migration/asset-mappings', data)
}
export function updateAssetMapping(id: string, data: any): Promise<any> {
  return put(`/finance/migration/asset-mappings/${id}`, data)
}
export function deleteAssetMapping(id: string): Promise<any> {
  return del(`/finance/migration/asset-mappings/${id}`)
}

// 向导
export function wizardParseColumns(file: File): Promise<any> {
  const formData = new FormData()
  formData.append('file', file)
  return post('/finance/migration/wizard/parse-columns', formData as any)
}
export function wizardExtractSubjects(data: any): Promise<any> {
  return post('/finance/migration/wizard/extract-subjects', data)
}
export function wizardAutoMatch(data: any): Promise<any> {
  return post('/finance/migration/wizard/auto-match', data)
}
export function wizardPreview(data: any): Promise<any> {
  return post('/finance/migration/wizard/preview', data)
}
export function wizardCommit(data: any): Promise<any> {
  return post('/finance/migration/wizard/commit', data)
}

// ========== 阿米巴多期对比报表 ==========

export interface AmoebaMultiPeriodRequest {
  templateId: number
  orgId: number
  accountSetId: number
  mainPeriod: string        // "202603" — 主期间(YYYYMM)
  includeYoy: boolean       // 是否包含同比
}

export interface AmoebaMultiPeriodResponse {
  tabNodes: TabNode[]
  globalSummaries: GlobalSummaryNode[]
  sections: SectionData[]
  indicatorSections?: SectionData[]
  summary: MultiPeriodSummary
  periodLabels: string[]
  unmatchedWarnings?: string[]
}

export interface TabNode {
  id: number
  name: string
  sort: number
  formulaValue?: number | null
}

export interface GlobalSummaryNode {
  id: number
  name: string
  formula?: string
  value?: number | null
  periodValues?: PeriodValue[]
  unit?: string
}

export interface SectionData {
  sectionName: string
  /** Tab祖先节点ID（对应tabNode.id；undefined表示跨Tab/总览板块） */
  tabAncestorId?: number
  items: MultiPeriodPLItemData[]
  sectionTotals?: PeriodValue[]
}

export interface MultiPeriodPLItemData {
  id: number
  name: string
  unit?: string
  dataSourceRemark?: string
  calculationLogic?: string
  isManualEntry: boolean
  nodeRole: string           // "group" | "data" | "formula" | "indicator"
  itemCategory?: string      // indicator/revenue/cost/profit/section
  valueSource?: string       // system/formula/manual
  decimalPlaces?: number      // 小数位数：null=按单位自动判断(默认2位), 1~4
  depth: number
  periodValues: PeriodValue[]
  momChange?: number         // 环比 %
  yoyChange?: number         // 同比 %
  canDrillDown: boolean
}

export interface PeriodValue {
  periodLabel: string
  amount: number
  perUnitValue?: number
}

export interface MultiPeriodSummary {
  marginTotals: PeriodValue[]
  currentPeriodTickets: number
}

// 手工填报相关
export interface SaveManualDataRequest {
  templateId: number
  orgId: number
  period: string
  items: ManualDataItem[]
}

export interface ManualDataItem {
  plItemId: number
  amount: number
  perUnitValue?: number
}

export interface ManualDataDto {
  id: number
  plItemId: number
  amount: number
  perUnitValue?: number
}

/** 多期对比报表 */
export function getAmoebaMultiPeriodReport(params: AmoebaMultiPeriodRequest): Promise<AmoebaMultiPeriodResponse> {
  return post<AmoebaMultiPeriodResponse>('/finance/reports/amoeba-report/multi-period', params)
}

/** 获取手工填报数据 */
export function getAmoebaManualData(params: { templateId: number; orgId: number; period: string }): Promise<ManualDataDto[]> {
  return get<ManualDataDto[]>('/finance/reports/amoeba-report/manual-data', params)
}

/** 保存手工填报数据（批量UPSERT） */
export function saveAmoebaManualData(data: SaveManualDataRequest): Promise<void> {
  return post<void>('/finance/reports/amoeba-report/manual-data', data)
}

// ========== 暂估数据 CRUD ==========

export interface EstimateDataDto {
  id: number
  plItemId?: number
  amount: number
  perUnitValue?: number
  templateId: number
  orgId: number
  period: string
  dataType: string  // "estimate"
  accountCode?: string
  auxiliaryJson?: string
}

/** 获取暂估数据列表 */
export function getEstimateData(params: { templateId: number; orgId: number; period: string }): Promise<EstimateDataDto[]> {
  return get<EstimateDataDto[]>('/finance/reports/amoeba-report/estimate-data', params)
}

/** 保存暂估数据（UPSERT单条） */
export function saveEstimateData(data: EstimateDataDto): Promise<void> {
  return post<void>('/finance/reports/amoeba-report/estimate-data', data)
}

/** 删除暂估数据 */
export function deleteEstimateData(id: number): Promise<void> {
  return del<void>(`/finance/reports/amoeba-report/estimate-data/${id}`)
}
