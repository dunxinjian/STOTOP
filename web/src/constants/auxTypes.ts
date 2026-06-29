/**
 * 辅助核算类型「英文码 → 中文名」单一真源。
 *
 * 后端 FinAuxiliaryType.FName / AuxiliaryTypeDto.name 存的都是英文码（outlet / business_direction …），
 * 不含中文，各前端页面需自行翻译为中文展示。此前 AccountManage、AmoebaPLTemplate、
 * AuxiliaryPicker、AuxiliaryBalanceReport 各维护一份重复映射，现统一收敛到此处。
 *
 * 编码与中文以后端 STOTOP.Module.Finance.Constants.AuxTypes 为准。
 */
export const AUX_TYPE_LABELS: Record<string, string> = {
  outlet: '网点',
  business_direction: '业务方向',
  express_brand: '快递品牌',
  business_unit: '经营单元',
  business_object: '业务对象',
  project: '项目',
  department: '部门',
  customer: '客户',
  supplier: '供应商',
  employee: '员工',
  cash_flow: '现金流量',
}

/** 单个英文码 → 中文名；无法识别则原样返回编码。 */
export function auxTypeLabel(code: string | null | undefined): string {
  if (!code) return ''
  return AUX_TYPE_LABELS[code] || code
}

/** 逗号分隔的英文码串 → 顿号分隔的中文串（科目辅助核算声明展示用）。 */
export function formatAuxiliaryCodes(raw: string | null | undefined): string {
  if (!raw || typeof raw !== 'string') return ''
  return raw
    .split(',')
    .map(s => s.trim())
    .filter(Boolean)
    .map(auxTypeLabel)
    .join('、')
}
